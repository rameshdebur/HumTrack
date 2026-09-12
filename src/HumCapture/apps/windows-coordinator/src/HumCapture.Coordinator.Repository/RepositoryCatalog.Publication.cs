using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    public static void PublishCatalog(string databasePath, JournalCommitContext context,
        RepositoryCatalogPublicationRequest request, JournalNormalTransition transition)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false
            }.ConnectionString);
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
            using var transaction = connection.BeginTransaction();
            RequireCurrentState(connection, transaction, transition);
            RejectTransitionIdentityCollision(connection, transaction, transition);
            using (var guard = connection.CreateCommand())
            {
                guard.Transaction = transaction;
                guard.CommandText = """
                    SELECT
                      (SELECT count(*) FROM repository_package_catalog
                       WHERE transaction_id = $transaction_id OR package_id = $package_id OR catalog_entry_id = $entry_id),
                      (SELECT count(*) FROM repository_record_index WHERE record_kind = 'commits' AND package_id = $package_id),
                      commit_record_id, commit_record_content_sha256
                    FROM repository_transactions WHERE transaction_id = $transaction_id;
                    """;
                guard.Parameters.AddWithValue("$transaction_id", Id(context.TransactionId));
                guard.Parameters.AddWithValue("$package_id", Id(context.PackageId));
                guard.Parameters.AddWithValue("$entry_id", Id(request.CatalogEntryId));
                using var reader = guard.ExecuteReader();
                if (!reader.Read() || reader.GetInt64(0) != 0 || reader.GetInt64(1) != 0
                    || !reader.IsDBNull(2) || !reader.IsDBNull(3))
                {
                    throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Unexpected catalog or commit evidence requires reconciliation.");
                }
            }
            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT INTO repository_package_catalog
                    SELECT $entry_id, '1.0.0', repository_id, transaction_id, subject_id, session_id,
                      trial_id, source_id, capture_attempt_id, collection_attempt_id, package_id,
                      package_content_sha256, artifact_set_sha256, package_byte_length, artifact_count,
                      destination_relative_path, verification_record_id, verification_record_content_sha256,
                      1, $recorded_utc
                    FROM repository_transactions WHERE transaction_id = $transaction_id;
                    """;
                insert.Parameters.AddWithValue("$entry_id", Id(request.CatalogEntryId));
                insert.Parameters.AddWithValue("$transaction_id", Id(context.TransactionId));
                insert.Parameters.AddWithValue("$recorded_utc", transition.RecordedUtc);
                if (insert.ExecuteNonQuery() != 1)
                {
                    throw InvalidCatalog("Catalog publication did not resolve one transaction.");
                }
            }
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE repository_transactions SET state = $to_state, revision = $next_revision,
                      state_changed_utc = $recorded_utc
                    WHERE transaction_id = $transaction_id AND state = $from_state AND revision = $expected_revision;
                    INSERT INTO repository_transitions (
                      transition_id, schema_version, transaction_id, transition_sequence, from_state,
                      to_state, operation_id, trigger, actor_kind, actor_windows_account, recorded_utc)
                    VALUES ($transition_id, '1.0.0', $transaction_id, $next_revision, $from_state,
                      $to_state, $operation_id, 'NORMAL', 'SYSTEM', $actor_windows_account, $recorded_utc);
                    """;
                AddTransitionParameters(update, transition);
                if (update.ExecuteNonQuery() != 2)
                {
                    throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Catalog state changed during publication.");
                }
            }
            transaction.Commit();
            connection.Close();
            FlushCatalog(databasePath);
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Atomic catalog publication failed.", exception);
        }
    }

    public static void RequirePublishedCatalog(string databasePath, JournalCommitContext context,
        RepositoryCatalogPublicationRequest request)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM repository_package_catalog c
            JOIN repository_transactions t ON c.transaction_id = t.transaction_id
            WHERE t.transaction_id = $transaction_id AND t.state = 'CATALOGED'
              AND c.catalog_entry_id = $entry_id AND c.schema_version = '1.0.0'
              AND c.repository_id = t.repository_id AND c.subject_id = t.subject_id
              AND c.session_id = t.session_id AND c.trial_id = t.trial_id
              AND c.source_id = t.source_id AND c.capture_attempt_id = t.capture_attempt_id
              AND c.collection_attempt_id = t.collection_attempt_id AND c.package_id = t.package_id
              AND c.package_content_sha256 = t.package_content_sha256
              AND c.artifact_set_sha256 = t.artifact_set_sha256 AND c.package_byte_length = t.package_byte_length
              AND c.artifact_count = t.artifact_count AND c.package_relative_path = t.destination_relative_path
              AND c.verification_record_id = t.verification_record_id
              AND c.verification_record_content_sha256 = t.verification_record_content_sha256
              AND c.catalog_revision = 1 AND c.cataloged_utc = $recorded_utc
              AND t.commit_record_id IS NULL AND t.commit_record_content_sha256 IS NULL
              AND NOT EXISTS (SELECT 1 FROM repository_record_index r WHERE r.record_kind = 'commits' AND r.package_id = t.package_id);
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(context.TransactionId));
        command.Parameters.AddWithValue("$entry_id", Id(request.CatalogEntryId));
        command.Parameters.AddWithValue("$recorded_utc", FormatUtc(request.RecordedAt));
        if ((long)(command.ExecuteScalar() ?? 0L) != 1)
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Catalog entry does not exactly match the publication request and journal.");
        }
    }
}

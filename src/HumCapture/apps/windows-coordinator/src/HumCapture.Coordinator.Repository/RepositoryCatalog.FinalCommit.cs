using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    public static void RequireUncommittedEvidence(string path, Guid transactionId, Guid packageId)
    {
        using var connection = FinalConnection(path, SqliteOpenMode.ReadOnly);
        CheckUncommitted(connection, null, transactionId, packageId);
    }

    private static SqliteConnection FinalConnection(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = mode, Pooling = false
        }.ConnectionString);
        connection.Open();
        return connection;
    }

    private static void CheckUncommitted(SqliteConnection connection, SqliteTransaction? transaction,
        Guid transactionId, Guid packageId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT count(*) FROM repository_transactions t
            WHERE transaction_id = $transaction_id AND state = 'CATALOGED'
              AND commit_record_id IS NULL AND commit_record_content_sha256 IS NULL
              AND NOT EXISTS (SELECT 1 FROM repository_record_index WHERE record_kind = 'commits' AND package_id = $package_id);
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(transactionId));
        command.Parameters.AddWithValue("$package_id", Id(packageId));
        if ((long)(command.ExecuteScalar() ?? 0L) != 1)
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Unexpected commit evidence requires reconciliation.");
        }
    }

    public static void CommitFinalEvidence(string database, JournalCommitContext context,
        RepositoryFinalCommitRequest request, string relativePath, string hash, JournalStartupReconciliation value)
    {
        try
        {
            using var connection = FinalConnection(database, SqliteOpenMode.ReadWrite);
            Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
            using var transaction = connection.BeginTransaction();
            CheckUncommitted(connection, transaction, context.TransactionId, context.PackageId);
            var current = ReadCurrentState(connection, transaction, context.TransactionId);
            if (current.Revision != value.ExpectedRevision)
            {
                throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Transaction revision changed before finalization.");
            }
            using (var index = connection.CreateCommand())
            {
                index.Transaction = transaction;
                index.CommandText = """
                    INSERT INTO repository_record_index (
                      index_entry_id, schema_version, repository_id, record_kind, record_id, record_revision,
                      record_content_sha256, subject_id, session_id, package_id, record_relative_path, indexed_utc)
                    SELECT $index_id, '1.0.0', repository_id, 'commits', $commit_id, 1, $hash,
                      subject_id, session_id, package_id, $path, $finished_utc
                    FROM repository_transactions WHERE transaction_id = $transaction_id;
                    """;
                AddFinalParameters(index, request, relativePath, hash, value);
                if (index.ExecuteNonQuery() != 1)
                {
                    throw InvalidCatalog("Commit index insertion did not resolve one transaction.");
                }
            }
            InsertReconciliation(connection, transaction, value);
            foreach (var observation in value.Observations)
            {
                InsertObservation(connection, transaction, value.ReconciliationId, observation);
            }
            InsertReconciliationTransition(connection, transaction, value);
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE repository_transactions SET state = 'COMMITTED', revision = revision + 1,
                      commit_record_id = $commit_id, commit_record_content_sha256 = $hash,
                      last_reconciliation_id = $reconciliation_id, state_changed_utc = $finished_utc
                    WHERE transaction_id = $transaction_id AND state = 'CATALOGED' AND revision = $expected_revision;
                    """;
                AddFinalParameters(update, request, relativePath, hash, value);
                if (update.ExecuteNonQuery() != 1)
                {
                    throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Final commit lost its expected revision.");
                }
            }
            transaction.Commit();
            connection.Close();
            FlushCatalog(database);
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Final commit transaction failed; retain the immutable record and retry the same request.", exception);
        }
    }

    public static void RequireFinalEvidence(string database, JournalCommitContext context,
        RepositoryFinalCommitRequest request, string relativePath, string hash, JournalStartupReconciliation value)
    {
        using var connection = FinalConnection(database, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM repository_transactions t
            JOIN repository_record_index i ON i.record_id = t.commit_record_id AND i.record_kind = 'commits'
            JOIN repository_transitions x ON x.transaction_id = t.transaction_id AND x.transition_id = $result_transition_id
            JOIN repository_reconciliations r ON r.reconciliation_id = x.reconciliation_id
            WHERE t.transaction_id = $transaction_id AND t.state = 'COMMITTED' AND t.revision = $expected_revision + 1
              AND t.commit_record_id = $commit_id AND t.commit_record_content_sha256 = $hash
              AND t.last_reconciliation_id = $reconciliation_id AND t.state_changed_utc = $finished_utc
              AND i.index_entry_id = $index_id AND i.repository_id = t.repository_id
              AND i.record_revision = 1 AND i.record_content_sha256 = $hash
              AND i.subject_id = t.subject_id AND i.session_id = t.session_id AND i.package_id = t.package_id
              AND i.record_relative_path = $path AND i.indexed_utc = $finished_utc
              AND x.transition_sequence = t.revision AND x.from_state = 'CATALOGED' AND x.to_state = 'COMMITTED'
              AND x.operation_id = $operation_id AND x.trigger = 'PRE_RECEIPT' AND x.actor_kind = 'SYSTEM'
              AND x.actor_windows_account = $actor AND x.recorded_utc = $finished_utc
              AND x.reason_code = 'EXACT_EVIDENCE_RECONCILED' AND x.reason = $explanation
              AND r.reconciliation_id = $reconciliation_id AND r.transaction_id = t.transaction_id
              AND r.trigger = 'PRE_RECEIPT' AND r.actor_kind = 'SYSTEM' AND r.actor_windows_account = $actor
              AND r.prior_state = 'CATALOGED' AND r.result_state = 'COMMITTED' AND r.action_code = 'FINALIZE_COMMIT'
              AND r.automatic = 1 AND r.blocking_reason_codes_json = '[]'
              AND r.started_utc = $started_utc AND r.finished_utc = $finished_utc
              AND r.result_transition_id = x.transition_id AND r.explanation = $explanation
              AND (SELECT count(*) FROM repository_record_index WHERE record_kind = 'commits' AND package_id = t.package_id) = 1;
            """;
        AddFinalParameters(command, request, relativePath, hash, value);
        command.Parameters.AddWithValue("$operation_id", Id(request.OperationId));
        if ((long)(command.ExecuteScalar() ?? 0L) != 1)
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Final commit, index, transition or reconciliation evidence disagrees.");
        }
        var observations = ReadObservations(connection, null, value.ReconciliationId);
        if (observations.Count != value.Observations.Count
            || observations.Zip(value.Observations).Any(pair => !ObservationEquals(pair.First, pair.Second)))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Final reconciliation observations disagree.");
        }
        RequireContiguousCurrentHistory(connection, context.TransactionId, value.ExpectedRevision + 1, "COMMITTED");
    }

    private static void AddFinalParameters(SqliteCommand command, RepositoryFinalCommitRequest request,
        string path, string hash, JournalStartupReconciliation value)
    {
        AddReconciliationParameters(command, value);
        command.Parameters.AddWithValue("$index_id", Id(request.IndexEntryId));
        command.Parameters.AddWithValue("$commit_id", Id(request.CommitRecordId));
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$path", path);
    }
}

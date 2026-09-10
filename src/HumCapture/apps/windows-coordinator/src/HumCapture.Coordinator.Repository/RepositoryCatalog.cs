using System.Reflection;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static class RepositoryCatalog
{
    public static void Create(string databasePath, RepositoryDescriptor descriptor)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
            using var transaction = connection.BeginTransaction();
            using var schema = connection.CreateCommand();
            schema.Transaction = transaction;
            schema.CommandText = LoadSchema();
            schema.ExecuteNonQuery();

            using var metadata = connection.CreateCommand();
            metadata.Transaction = transaction;
            metadata.CommandText = """
                INSERT INTO repository_metadata (
                  singleton_id, schema_version, repository_id, interface_version,
                  namespace_version, catalog_schema_version, created_utc,
                  created_by_windows_account
                ) VALUES (
                  1, $schema_version, $repository_id, $interface_version,
                  $namespace_version, $catalog_schema_version, $created_utc,
                  $created_by_windows_account
                );
                """;
            metadata.Parameters.AddWithValue("$schema_version", descriptor.SchemaVersion);
            metadata.Parameters.AddWithValue("$repository_id", descriptor.RepositoryId);
            metadata.Parameters.AddWithValue("$interface_version", descriptor.InterfaceVersion);
            metadata.Parameters.AddWithValue("$namespace_version", descriptor.NamespaceVersion);
            metadata.Parameters.AddWithValue("$catalog_schema_version", descriptor.CatalogSchemaVersion);
            metadata.Parameters.AddWithValue("$created_utc", descriptor.CreatedUtc);
            metadata.Parameters.AddWithValue("$created_by_windows_account", descriptor.CreatedByWindowsAccount);
            metadata.ExecuteNonQuery();
            transaction.Commit();
            RequireIntegrity(connection);
        }

        using var file = new FileStream(databasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        file.Flush(flushToDisk: true);
    }

    public static void Verify(string databasePath, RepositoryDescriptor descriptor)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        try
        {
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON;");
            RequireIntegrity(connection);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT schema_version, repository_id, interface_version,
                       namespace_version, catalog_schema_version, created_utc,
                       created_by_windows_account
                  FROM repository_metadata
                 WHERE singleton_id = 1;
                """;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw MetadataMismatch("Catalog repository metadata is missing.");
            }

            var actual = new[]
            {
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetString(6)
            };
            var expected = new[]
            {
                descriptor.SchemaVersion, descriptor.RepositoryId, descriptor.InterfaceVersion,
                descriptor.NamespaceVersion, descriptor.CatalogSchemaVersion, descriptor.CreatedUtc,
                descriptor.CreatedByWindowsAccount
            };
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal) || reader.Read())
            {
                throw MetadataMismatch("Catalog repository metadata does not exactly match repository.json.");
            }
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, "Repository catalog could not be verified.", exception);
        }
    }

    public static RepositoryTransactionSnapshot RegisterStagedVerified(
        string databasePath,
        JournalAdmission admission)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        try
        {
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
            using var transaction = connection.BeginTransaction();

            var existing = ReadExisting(connection, transaction, admission);
            if (existing is not null)
            {
                transaction.Rollback();
                return existing with { WasAlreadyPresent = true };
            }

            InsertCurrentTransaction(connection, transaction, admission);
            InsertInitialTransition(connection, transaction, admission);
            InsertVerificationIndex(connection, transaction, admission);
            transaction.Commit();

            var snapshot = ReadExact(connection, admission)
                ?? throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Durable journal admission could not be read back.");
            connection.Close();
            FlushCatalog(databasePath);
            return snapshot;
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Repository journal admission failed.", exception);
        }
    }

    private static RepositoryTransactionSnapshot? ReadExisting(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JournalAdmission admission)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT transaction_id
              FROM repository_transactions
             WHERE transaction_id = $transaction_id
                OR (repository_id = $repository_id AND package_id = $package_id);
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(admission.Registration.TransactionId));
        command.Parameters.AddWithValue("$repository_id", admission.RepositoryId);
        command.Parameters.AddWithValue("$package_id", Id(admission.Registration.PackageId));
        var transactionIds = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                transactionIds.Add(reader.GetString(0));
            }
        }

        if (transactionIds.Count == 0)
        {
            return null;
        }

        if (transactionIds.Count != 1 || transactionIds[0] != Id(admission.Registration.TransactionId))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Package or transaction identity is already bound to another journal entry.");
        }

        return ReadExact(connection, admission, transaction)
            ?? throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Existing journal entry is not an exact idempotent replay.");
    }

    private static RepositoryTransactionSnapshot? ReadExact(
        SqliteConnection connection,
        JournalAdmission admission,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT t.transaction_id, t.revision, t.state, t.subject_id, t.session_id,
                   t.trial_id, t.source_id, t.capture_attempt_id, t.collection_attempt_id,
                   t.package_id, t.package_content_sha256, t.artifact_set_sha256,
                   t.verification_record_id, t.verification_record_content_sha256,
                   t.staging_relative_path, t.destination_relative_path,
                   t.package_byte_length, t.artifact_count, t.created_utc, t.state_changed_utc,
                   x.transition_id, x.operation_id, x.actor_windows_account, x.recorded_utc,
                   i.index_entry_id, i.record_revision, i.record_content_sha256,
                   i.record_relative_path
              FROM repository_transactions t
              JOIN repository_transitions x
                ON x.transaction_id = t.transaction_id AND x.transition_sequence = 1
              JOIN repository_record_index i
                ON i.repository_id = t.repository_id
               AND i.record_kind = 'verifications'
               AND i.record_id = t.verification_record_id
               AND i.package_id = t.package_id
             WHERE t.transaction_id = $transaction_id;
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(admission.Registration.TransactionId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var r = admission.Registration;
        var expected = new object[]
        {
            Id(r.TransactionId), 1L, "STAGED_VERIFIED", Id(r.SubjectId), Id(r.SessionId),
            Id(r.TrialId), Id(r.SourceId), Id(r.CaptureAttemptId), Id(r.CollectionAttemptId),
            Id(r.PackageId), r.PackageContentSha256, r.ArtifactSetSha256,
            Id(r.VerificationRecordId), r.VerificationRecordContentSha256,
            admission.StagingRelativePath, admission.DestinationRelativePath,
            r.PackageByteLength, (long)r.ArtifactCount, admission.RecordedUtc, admission.RecordedUtc,
            Id(r.TransitionId), Id(r.OperationId), r.ActorWindowsAccount, admission.RecordedUtc,
            Id(r.RecordIndexEntryId), (long)admission.VerificationRecordRevision,
            r.VerificationRecordContentSha256, admission.VerificationRecordRelativePath
        };
        for (var index = 0; index < expected.Length; index++)
        {
            if (!Equals(reader.GetValue(index), expected[index]))
            {
                throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Existing journal entry is not an exact idempotent replay.");
            }
        }
        if (reader.Read())
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Journal admission resolved to duplicate rows.");
        }

        return new RepositoryTransactionSnapshot(
            Id(r.TransactionId), 1, "STAGED_VERIFIED", admission.StagingRelativePath,
            admission.DestinationRelativePath, admission.VerificationRecordRelativePath,
            Id(r.OperationId), admission.RecordedUtc, false);
    }

    private static void InsertCurrentTransaction(SqliteConnection connection, SqliteTransaction transaction, JournalAdmission admission)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_transactions (
              transaction_id, schema_version, repository_id, revision, state,
              subject_id, session_id, trial_id, source_id, capture_attempt_id,
              collection_attempt_id, package_id, package_content_sha256,
              artifact_set_sha256, verification_record_id,
              verification_record_content_sha256, staging_relative_path,
              destination_relative_path, package_byte_length, artifact_count,
              commit_record_id, commit_record_content_sha256, quarantine_record_id,
              last_reconciliation_id, created_utc, state_changed_utc
            ) VALUES (
              $transaction_id, '1.0.0', $repository_id, 1, 'STAGED_VERIFIED',
              $subject_id, $session_id, $trial_id, $source_id, $capture_attempt_id,
              $collection_attempt_id, $package_id, $package_content_sha256,
              $artifact_set_sha256, $verification_record_id,
              $verification_record_content_sha256, $staging_relative_path,
              $destination_relative_path, $package_byte_length, $artifact_count,
              NULL, NULL, NULL, NULL, $recorded_utc, $recorded_utc
            );
            """;
        AddIdentityParameters(command, admission);
        command.ExecuteNonQuery();
    }

    private static void InsertInitialTransition(SqliteConnection connection, SqliteTransaction transaction, JournalAdmission admission)
    {
        var r = admission.Registration;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_transitions (
              transition_id, schema_version, transaction_id, transition_sequence,
              from_state, to_state, operation_id, trigger, actor_kind,
              actor_windows_account, reconciliation_id, reason_code, reason, recorded_utc
            ) VALUES (
              $transition_id, '1.0.0', $transaction_id, 1,
              NULL, 'STAGED_VERIFIED', $operation_id, 'NORMAL', 'SYSTEM',
              $actor_windows_account, NULL, NULL, NULL, $recorded_utc
            );
            """;
        command.Parameters.AddWithValue("$transition_id", Id(r.TransitionId));
        command.Parameters.AddWithValue("$transaction_id", Id(r.TransactionId));
        command.Parameters.AddWithValue("$operation_id", Id(r.OperationId));
        command.Parameters.AddWithValue("$actor_windows_account", r.ActorWindowsAccount);
        command.Parameters.AddWithValue("$recorded_utc", admission.RecordedUtc);
        command.ExecuteNonQuery();
    }

    private static void InsertVerificationIndex(SqliteConnection connection, SqliteTransaction transaction, JournalAdmission admission)
    {
        var r = admission.Registration;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_record_index (
              index_entry_id, schema_version, repository_id, record_kind, record_id,
              record_revision, record_content_sha256, subject_id, session_id,
              package_id, record_relative_path, indexed_utc
            ) VALUES (
              $index_entry_id, '1.0.0', $repository_id, 'verifications', $record_id,
              $record_revision, $record_content_sha256, $subject_id, $session_id,
              $package_id, $record_relative_path, $recorded_utc
            );
            """;
        command.Parameters.AddWithValue("$index_entry_id", Id(r.RecordIndexEntryId));
        command.Parameters.AddWithValue("$repository_id", admission.RepositoryId);
        command.Parameters.AddWithValue("$record_id", Id(r.VerificationRecordId));
        command.Parameters.AddWithValue("$record_revision", admission.VerificationRecordRevision);
        command.Parameters.AddWithValue("$record_content_sha256", r.VerificationRecordContentSha256);
        command.Parameters.AddWithValue("$subject_id", Id(r.SubjectId));
        command.Parameters.AddWithValue("$session_id", Id(r.SessionId));
        command.Parameters.AddWithValue("$package_id", Id(r.PackageId));
        command.Parameters.AddWithValue("$record_relative_path", admission.VerificationRecordRelativePath);
        command.Parameters.AddWithValue("$recorded_utc", admission.RecordedUtc);
        command.ExecuteNonQuery();
    }

    private static void AddIdentityParameters(SqliteCommand command, JournalAdmission admission)
    {
        var r = admission.Registration;
        command.Parameters.AddWithValue("$transaction_id", Id(r.TransactionId));
        command.Parameters.AddWithValue("$repository_id", admission.RepositoryId);
        command.Parameters.AddWithValue("$subject_id", Id(r.SubjectId));
        command.Parameters.AddWithValue("$session_id", Id(r.SessionId));
        command.Parameters.AddWithValue("$trial_id", Id(r.TrialId));
        command.Parameters.AddWithValue("$source_id", Id(r.SourceId));
        command.Parameters.AddWithValue("$capture_attempt_id", Id(r.CaptureAttemptId));
        command.Parameters.AddWithValue("$collection_attempt_id", Id(r.CollectionAttemptId));
        command.Parameters.AddWithValue("$package_id", Id(r.PackageId));
        command.Parameters.AddWithValue("$package_content_sha256", r.PackageContentSha256);
        command.Parameters.AddWithValue("$artifact_set_sha256", r.ArtifactSetSha256);
        command.Parameters.AddWithValue("$verification_record_id", Id(r.VerificationRecordId));
        command.Parameters.AddWithValue("$verification_record_content_sha256", r.VerificationRecordContentSha256);
        command.Parameters.AddWithValue("$staging_relative_path", admission.StagingRelativePath);
        command.Parameters.AddWithValue("$destination_relative_path", admission.DestinationRelativePath);
        command.Parameters.AddWithValue("$package_byte_length", r.PackageByteLength);
        command.Parameters.AddWithValue("$artifact_count", r.ArtifactCount);
        command.Parameters.AddWithValue("$recorded_utc", admission.RecordedUtc);
    }

    private static string Id(Guid value) => value.ToString("D");

    private static void FlushCatalog(string databasePath)
    {
        using var file = new FileStream(databasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        file.Flush(flushToDisk: true);
    }

    private static string LoadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HumCapture.Contracts.repository-v1.sql")
            ?? throw new InvalidOperationException("Embedded repository-v1.sql contract is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void RequireIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = command.ExecuteScalar() as string;
        if (!string.Equals(result, "ok", StringComparison.Ordinal))
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, $"Repository catalog integrity check failed: {result ?? "no result"}");
        }
    }

    private static RepositoryException MetadataMismatch(string message) =>
        new(RepositoryErrorCode.CatalogMetadataMismatch, message);
}

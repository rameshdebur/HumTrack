using System.Globalization;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    public static JournalCommitContext ReadCommitContext(
        string databasePath,
        Guid transactionId,
        string expectedRepositoryId)
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
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT repository_id, revision, state, subject_id, session_id,
                       trial_id, source_id, capture_attempt_id, collection_attempt_id,
                       package_id, package_content_sha256, artifact_set_sha256,
                       verification_record_id, verification_record_content_sha256,
                       staging_relative_path, destination_relative_path,
                       package_byte_length, artifact_count
                  FROM repository_transactions
                 WHERE transaction_id = $transaction_id;
                """;
            command.Parameters.AddWithValue("$transaction_id", Id(transactionId));
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new RepositoryException(RepositoryErrorCode.JournalEntryMissing, "Repository transaction does not exist.");
            }

            var repositoryId = reader.GetString(0);
            var revision = reader.GetInt64(1);
            var state = reader.GetString(2);
            var subjectId = ParseCanonicalId(reader.GetString(3), "subject_id");
            var sessionId = ParseCanonicalId(reader.GetString(4), "session_id");
            var context = new JournalCommitContext(
                repositoryId,
                transactionId,
                revision,
                state,
                subjectId,
                sessionId,
                ParseCanonicalId(reader.GetString(5), "trial_id"),
                ParseCanonicalId(reader.GetString(6), "source_id"),
                ParseCanonicalId(reader.GetString(7), "capture_attempt_id"),
                ParseCanonicalId(reader.GetString(8), "collection_attempt_id"),
                ParseCanonicalId(reader.GetString(9), "package_id"),
                RequireSha256(reader.GetString(10), "package_content_sha256"),
                RequireSha256(reader.GetString(11), "artifact_set_sha256"),
                ParseCanonicalId(reader.GetString(12), "verification_record_id"),
                RequireSha256(reader.GetString(13), "verification_record_content_sha256"),
                reader.GetString(14),
                reader.GetString(15),
                RequireCanonicalU64(reader.GetString(16), "package_byte_length"),
                RequirePositiveInt(reader.GetInt64(17), "artifact_count"),
                0,
                string.Empty);
            if (reader.Read())
            {
                throw InvalidCatalog("Repository transaction identity resolved to duplicate rows.");
            }

            if (!string.Equals(repositoryId, expectedRepositoryId, StringComparison.Ordinal))
            {
                throw InvalidCatalog("Repository transaction is bound to another repository identity.");
            }

            reader.Close();
            RequireContiguousCurrentHistory(connection, transactionId, revision, state);

            var expectedStaging = $"staging/{Id(context.CollectionAttemptId)}/{Id(context.PackageId)}";
            var expectedDestination = $"subjects/{Id(subjectId)}/sessions/{Id(sessionId)}/packages/{Id(context.PackageId)}";
            var expectedVerification = $"subjects/{Id(subjectId)}/sessions/{Id(sessionId)}/records/verifications/{Id(context.VerificationRecordId)}.json";
            if (!string.Equals(context.StagingRelativePath, expectedStaging, StringComparison.Ordinal)
                || !string.Equals(context.DestinationRelativePath, expectedDestination, StringComparison.Ordinal))
            {
                throw InvalidCatalog("Repository transaction paths do not match their canonical identities.");
            }

            using var index = connection.CreateCommand();
            index.CommandText = """
                SELECT record_revision, record_relative_path
                  FROM repository_record_index
                 WHERE repository_id = $repository_id
                   AND record_kind = 'verifications'
                   AND record_id = $record_id
                   AND record_content_sha256 = $record_content_sha256
                   AND subject_id = $subject_id
                   AND session_id = $session_id
                   AND package_id = $package_id
                   AND record_relative_path = $record_relative_path;
                """;
            index.Parameters.AddWithValue("$repository_id", repositoryId);
            index.Parameters.AddWithValue("$record_id", Id(context.VerificationRecordId));
            index.Parameters.AddWithValue("$record_content_sha256", context.VerificationRecordContentSha256);
            index.Parameters.AddWithValue("$subject_id", Id(subjectId));
            index.Parameters.AddWithValue("$session_id", Id(sessionId));
            index.Parameters.AddWithValue("$package_id", Id(context.PackageId));
            index.Parameters.AddWithValue("$record_relative_path", expectedVerification);
            using var indexReader = index.ExecuteReader();
            if (!indexReader.Read())
            {
                throw InvalidCatalog("The transaction's immutable verification index is missing or mismatched.");
            }

            var verificationRevision = RequirePositiveInt(indexReader.GetInt64(0), "verification record revision");
            var verificationPath = indexReader.GetString(1);
            if (indexReader.Read())
            {
                throw InvalidCatalog("The transaction's immutable verification index is ambiguous.");
            }

            return context with
            {
                VerificationRecordRevision = verificationRevision,
                VerificationRecordRelativePath = verificationPath
            };
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, "Repository commit context could not be read.", exception);
        }
    }

    public static void AdvanceNormalState(string databasePath, JournalNormalTransition transition)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        try
        {
            using (var connection = new SqliteConnection(builder.ConnectionString))
            {
                connection.Open();
                Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
                using var transaction = connection.BeginTransaction();
                RequireCurrentState(connection, transaction, transition);
                RejectTransitionIdentityCollision(connection, transaction, transition);

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = """
                        UPDATE repository_transactions
                           SET revision = $next_revision,
                               state = $to_state,
                               state_changed_utc = $recorded_utc
                         WHERE transaction_id = $transaction_id
                           AND revision = $expected_revision
                           AND state = $from_state;
                        """;
                    AddTransitionParameters(update, transition);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Repository transaction changed concurrently; reconciliation is required.");
                    }
                }

                using (var insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT INTO repository_transitions (
                          transition_id, schema_version, transaction_id, transition_sequence,
                          from_state, to_state, operation_id, trigger, actor_kind,
                          actor_windows_account, reconciliation_id, reason_code, reason, recorded_utc
                        ) VALUES (
                          $transition_id, '1.0.0', $transaction_id, $next_revision,
                          $from_state, $to_state, $operation_id, 'NORMAL', 'SYSTEM',
                          $actor_windows_account, NULL, NULL, NULL, $recorded_utc
                        );
                        """;
                    AddTransitionParameters(insert, transition);
                    insert.ExecuteNonQuery();
                }

                transaction.Commit();
                var (revision, state) = ReadCurrentState(connection, null, transition.TransactionId);
                if (revision != transition.NextRevision || state != transition.ToState)
                {
                    throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Repository transition could not be read back after commit.");
                }

                connection.Close();
            }

            FlushCatalog(databasePath);
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Repository state transition failed.", exception);
        }
    }

    public static void RequireExactNormalTransition(string databasePath, JournalNormalTransition expected)
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
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT transition_id, transition_sequence, from_state, to_state,
                       operation_id, trigger, actor_kind, actor_windows_account,
                       reconciliation_id, reason_code, reason, recorded_utc
                  FROM repository_transitions
                 WHERE transition_id = $transition_id
                    OR (transaction_id = $transaction_id AND transition_sequence = $next_revision)
                    OR (transaction_id = $transaction_id AND operation_id = $operation_id);
                """;
            command.Parameters.AddWithValue("$transition_id", Id(expected.TransitionId));
            command.Parameters.AddWithValue("$transaction_id", Id(expected.TransactionId));
            command.Parameters.AddWithValue("$next_revision", expected.NextRevision);
            command.Parameters.AddWithValue("$operation_id", Id(expected.OperationId));
            using var reader = command.ExecuteReader();
            if (!reader.Read() || !TransitionMatches(reader, expected) || reader.Read())
            {
                throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Existing repository transition is not an exact idempotent replay.");
            }
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, "Repository transition could not be verified.", exception);
        }
    }

    private static void RequireCurrentState(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JournalNormalTransition transition)
    {
        var (revision, state) = ReadCurrentState(connection, transaction, transition.TransactionId);
        if (revision != transition.ExpectedRevision || state != transition.FromState)
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Repository transaction is not at the requested normal transition boundary.");
        }
    }

    private static void RequireContiguousCurrentHistory(
        SqliteConnection connection,
        Guid transactionId,
        long revision,
        string state)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*), max(transition_sequence),
                   (SELECT to_state
                      FROM repository_transitions
                     WHERE transaction_id = $transaction_id
                     ORDER BY transition_sequence DESC
                     LIMIT 1)
              FROM repository_transitions
             WHERE transaction_id = $transaction_id;
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(transactionId));
        using var reader = command.ExecuteReader();
        if (!reader.Read()
            || reader.GetInt64(0) != revision
            || reader.IsDBNull(1)
            || reader.GetInt64(1) != revision
            || reader.IsDBNull(2)
            || reader.GetString(2) != state
            || reader.Read())
        {
            throw InvalidCatalog("Current repository state does not agree with contiguous transition history.");
        }
    }

    private static (long Revision, string State) ReadCurrentState(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid transactionId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT revision, state FROM repository_transactions WHERE transaction_id = $transaction_id;";
        command.Parameters.AddWithValue("$transaction_id", Id(transactionId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new RepositoryException(RepositoryErrorCode.JournalEntryMissing, "Repository transaction does not exist.");
        }

        var result = (reader.GetInt64(0), reader.GetString(1));
        if (reader.Read())
        {
            throw InvalidCatalog("Repository transaction identity resolved to duplicate rows.");
        }

        return result;
    }

    private static void RejectTransitionIdentityCollision(
        SqliteConnection connection,
        SqliteTransaction transaction,
        JournalNormalTransition transition)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT count(*)
              FROM repository_transitions
             WHERE transition_id = $transition_id
                OR (transaction_id = $transaction_id AND transition_sequence = $next_revision)
                OR (transaction_id = $transaction_id AND operation_id = $operation_id);
            """;
        command.Parameters.AddWithValue("$transition_id", Id(transition.TransitionId));
        command.Parameters.AddWithValue("$transaction_id", Id(transition.TransactionId));
        command.Parameters.AddWithValue("$next_revision", transition.NextRevision);
        command.Parameters.AddWithValue("$operation_id", Id(transition.OperationId));
        if ((long)(command.ExecuteScalar() ?? 0L) != 0)
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Transition or operation identity is already in use.");
        }
    }

    private static void AddTransitionParameters(SqliteCommand command, JournalNormalTransition transition)
    {
        command.Parameters.AddWithValue("$transition_id", Id(transition.TransitionId));
        command.Parameters.AddWithValue("$transaction_id", Id(transition.TransactionId));
        command.Parameters.AddWithValue("$expected_revision", transition.ExpectedRevision);
        command.Parameters.AddWithValue("$next_revision", transition.NextRevision);
        command.Parameters.AddWithValue("$from_state", transition.FromState);
        command.Parameters.AddWithValue("$to_state", transition.ToState);
        command.Parameters.AddWithValue("$operation_id", Id(transition.OperationId));
        command.Parameters.AddWithValue("$actor_windows_account", transition.ActorWindowsAccount);
        command.Parameters.AddWithValue("$recorded_utc", transition.RecordedUtc);
    }

    private static bool TransitionMatches(SqliteDataReader reader, JournalNormalTransition expected) =>
        reader.GetString(0) == Id(expected.TransitionId)
        && reader.GetInt64(1) == expected.NextRevision
        && reader.GetString(2) == expected.FromState
        && reader.GetString(3) == expected.ToState
        && reader.GetString(4) == Id(expected.OperationId)
        && reader.GetString(5) == "NORMAL"
        && reader.GetString(6) == "SYSTEM"
        && reader.GetString(7) == expected.ActorWindowsAccount
        && reader.IsDBNull(8)
        && reader.IsDBNull(9)
        && reader.IsDBNull(10)
        && reader.GetString(11) == expected.RecordedUtc;

    private static Guid ParseCanonicalId(string value, string label)
    {
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty || value != Id(id))
        {
            throw InvalidCatalog($"Repository catalog contains a non-canonical {label}.");
        }

        return id;
    }

    private static string RequireSha256(string value, string label)
    {
        if (value.Length != 64 || value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw InvalidCatalog($"Repository catalog contains an invalid {label}.");
        }

        return value;
    }

    private static string RequireCanonicalU64(string value, string label)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || value != number.ToString(CultureInfo.InvariantCulture))
        {
            throw InvalidCatalog($"Repository catalog contains an invalid {label}.");
        }

        return value;
    }

    private static int RequirePositiveInt(long value, string label)
    {
        if (value is < 1 or > int.MaxValue)
        {
            throw InvalidCatalog($"Repository catalog contains an invalid {label}.");
        }

        return (int)value;
    }

    private static RepositoryException InvalidCatalog(string message) =>
        new(RepositoryErrorCode.CatalogInvalid, message);
}

internal sealed record JournalNormalTransition(
    Guid TransactionId,
    long ExpectedRevision,
    long NextRevision,
    string FromState,
    string ToState,
    Guid TransitionId,
    Guid OperationId,
    string ActorWindowsAccount,
    string RecordedUtc);

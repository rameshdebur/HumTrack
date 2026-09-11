using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    public static RepositoryStartupReconciliationSnapshot? ReadStartupReconciliationReplay(
        string databasePath,
        RepositoryStartupReconciliationRequest request)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.transaction_id, r.prior_state, r.result_state, r.action_code,
                   r.actor_windows_account, r.started_utc, r.finished_utc,
                   r.result_transition_id, t.revision, x.operation_id
              FROM repository_reconciliations r
              JOIN repository_transactions t ON t.transaction_id = r.transaction_id
              LEFT JOIN repository_transitions x ON x.transition_id = r.result_transition_id
             WHERE r.reconciliation_id = $reconciliation_id;
            """;
        command.Parameters.AddWithValue("$reconciliation_id", Id(request.ReconciliationId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        var transitionId = reader.IsDBNull(7) ? null : reader.GetString(7);
        var operationId = reader.IsDBNull(9) ? null : reader.GetString(9);
        if (reader.GetString(0) != Id(request.TransactionId)
            || reader.GetString(4) != request.ActorWindowsAccount
            || reader.GetString(5) != FormatUtc(request.StartedAt)
            || reader.GetString(6) != FormatUtc(request.FinishedAt)
            || transitionId != request.ResultTransitionId?.ToString("D")
            || operationId != request.ResultOperationId?.ToString("D"))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Reconciliation identity is already used by a different request.");
        }
        var transactionId = reader.GetString(0);
        var priorState = reader.GetString(1);
        var resultState = reader.GetString(2);
        var actionCode = reader.GetString(3);
        var revision = reader.GetInt64(8);
        reader.Close();
        return new RepositoryStartupReconciliationSnapshot(Id(request.ReconciliationId), transactionId, revision,
            priorState, resultState, actionCode, transitionId,
            ReadObservations(connection, null, request.ReconciliationId), true);
    }

    public static (string Catalog, string Commit) ObserveLaterAuthorities(string databasePath, JournalCommitContext context)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
              (SELECT count(*) FROM repository_package_catalog WHERE transaction_id = $transaction_id),
              (SELECT count(*) FROM repository_record_index WHERE repository_id = $repository_id AND record_kind = 'commits' AND package_id = $package_id),
              commit_record_id, commit_record_content_sha256
            FROM repository_transactions WHERE transaction_id = $transaction_id;
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(context.TransactionId));
        command.Parameters.AddWithValue("$repository_id", context.RepositoryId);
        command.Parameters.AddWithValue("$package_id", Id(context.PackageId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw InvalidCatalog("Repository authority observation could not resolve one transaction.");
        }

        var catalog = reader.GetInt64(0) == 0 ? "ABSENT" : "MISMATCH";
        var commit = reader.GetInt64(1) == 0 && reader.IsDBNull(2) && reader.IsDBNull(3) ? "ABSENT" : "MISMATCH";
        return (catalog, commit);
    }

    public static RepositoryStartupReconciliationSnapshot RecordAutomaticReconciliation(
        string databasePath,
        JournalStartupReconciliation value)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
        try
        {
            using (var connection = new SqliteConnection(builder.ConnectionString))
            {
                connection.Open();
                Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
                using var transaction = connection.BeginTransaction();
                var existing = ReadReconciliation(connection, transaction, value);
                if (existing is not null)
                {
                    transaction.Rollback();
                    return existing with { WasAlreadyRecorded = true };
                }

                var (revision, state) = ReadCurrentState(connection, transaction, value.TransactionId);
                if (revision != value.ExpectedRevision || state != value.PriorState)
                {
                    throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Repository state changed before startup reconciliation could be recorded.");
                }

                InsertReconciliation(connection, transaction, value);
                foreach (var observation in value.Observations)
                {
                    InsertObservation(connection, transaction, value.ReconciliationId, observation);
                }

                var changed = value.PriorState != value.ResultState;
                if (changed)
                {
                    InsertReconciliationTransition(connection, transaction, value);
                }

                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = changed
                        ? "UPDATE repository_transactions SET revision = revision + 1, state = $result_state, last_reconciliation_id = $reconciliation_id, state_changed_utc = $finished_utc WHERE transaction_id = $transaction_id AND revision = $expected_revision AND state = $prior_state"
                        : "UPDATE repository_transactions SET last_reconciliation_id = $reconciliation_id WHERE transaction_id = $transaction_id AND revision = $expected_revision AND state = $prior_state";
                    AddReconciliationParameters(update, value);
                    if (update.ExecuteNonQuery() != 1)
                    {
                        throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Repository state changed during startup reconciliation.");
                    }
                }

                transaction.Commit();
                var snapshot = ReadReconciliation(connection, null, value)
                    ?? throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Startup reconciliation could not be read back.");
                connection.Close();
                FlushCatalog(databasePath);
                return snapshot;
            }
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "Startup reconciliation failed.", exception);
        }
    }

    private static void InsertReconciliation(SqliteConnection connection, SqliteTransaction transaction, JournalStartupReconciliation value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_reconciliations (
              reconciliation_id, schema_version, transaction_id, trigger, started_utc,
              finished_utc, actor_kind, actor_windows_account, prior_state, result_state,
              action_code, automatic, blocking_reason_codes_json, explanation, result_transition_id
            ) VALUES ($reconciliation_id, '1.0.0', $transaction_id, 'STARTUP', $started_utc,
              $finished_utc, 'SYSTEM', $actor, $prior_state, $result_state,
              $action_code, 1, '[]', $explanation, $result_transition_id);
            """;
        AddReconciliationParameters(command, value);
        command.ExecuteNonQuery();
    }

    private static void InsertObservation(SqliteConnection connection, SqliteTransaction transaction, Guid reconciliationId, RepositoryAuthorityObservation value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_reconciliation_observations (
              reconciliation_id, authority, disposition, expected_package_id,
              observed_package_id, expected_content_sha256, observed_content_sha256,
              expected_byte_length, observed_byte_length, evidence_relative_paths_json
            ) VALUES ($reconciliation_id, $authority, $disposition, $expected_package_id,
              $observed_package_id, $expected_content_sha256, $observed_content_sha256,
              $expected_byte_length, $observed_byte_length, $paths);
            """;
        command.Parameters.AddWithValue("$reconciliation_id", Id(reconciliationId));
        command.Parameters.AddWithValue("$authority", value.Authority);
        command.Parameters.AddWithValue("$disposition", value.Disposition);
        command.Parameters.AddWithValue("$expected_package_id", (object?)value.ExpectedPackageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$observed_package_id", (object?)value.ObservedPackageId ?? DBNull.Value);
        command.Parameters.AddWithValue("$expected_content_sha256", (object?)value.ExpectedContentSha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("$observed_content_sha256", (object?)value.ObservedContentSha256 ?? DBNull.Value);
        command.Parameters.AddWithValue("$expected_byte_length", (object?)value.ExpectedByteLength ?? DBNull.Value);
        command.Parameters.AddWithValue("$observed_byte_length", (object?)value.ObservedByteLength ?? DBNull.Value);
        command.Parameters.AddWithValue("$paths", JsonSerializer.Serialize(value.EvidenceRelativePaths));
        command.ExecuteNonQuery();
    }

    private static void InsertReconciliationTransition(SqliteConnection connection, SqliteTransaction transaction, JournalStartupReconciliation value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO repository_transitions (
              transition_id, schema_version, transaction_id, transition_sequence,
              from_state, to_state, operation_id, trigger, actor_kind,
              actor_windows_account, reconciliation_id, reason_code, reason, recorded_utc
            ) VALUES ($result_transition_id, '1.0.0', $transaction_id, $next_revision,
              $prior_state, $result_state, $result_operation_id, 'STARTUP', 'SYSTEM',
              $actor, $reconciliation_id, 'EXACT_EVIDENCE_RECONCILED', $explanation, $finished_utc);
            """;
        AddReconciliationParameters(command, value);
        command.Parameters.AddWithValue("$next_revision", value.ExpectedRevision + 1);
        command.Parameters.AddWithValue("$result_operation_id", Id(value.ResultOperationId!.Value));
        command.ExecuteNonQuery();
    }

    private static RepositoryStartupReconciliationSnapshot? ReadReconciliation(SqliteConnection connection, SqliteTransaction? transaction, JournalStartupReconciliation expected)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT transaction_id, prior_state, result_state, action_code, actor_windows_account,
                   started_utc, finished_utc, result_transition_id
              FROM repository_reconciliations WHERE reconciliation_id = $reconciliation_id;
            """;
        command.Parameters.AddWithValue("$reconciliation_id", Id(expected.ReconciliationId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        var transition = reader.IsDBNull(7) ? null : reader.GetString(7);
        if (reader.GetString(0) != Id(expected.TransactionId) || reader.GetString(1) != expected.PriorState
            || reader.GetString(2) != expected.ResultState || reader.GetString(3) != expected.ActionCode
            || reader.GetString(4) != expected.ActorWindowsAccount || reader.GetString(5) != expected.StartedUtc
            || reader.GetString(6) != expected.FinishedUtc || transition != expected.ResultTransitionId?.ToString("D") || reader.Read())
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Reconciliation identity is already used by different content.");
        }
        reader.Close();
        var observations = ReadObservations(connection, transaction, expected.ReconciliationId);
        if (observations.Count != expected.Observations.Count
            || observations.Zip(expected.Observations).Any(pair => !ObservationEquals(pair.First, pair.Second)))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Existing reconciliation observations differ from the requested evidence.");
        }
        return new RepositoryStartupReconciliationSnapshot(Id(expected.ReconciliationId), Id(expected.TransactionId),
            expected.ExpectedRevision + (expected.PriorState == expected.ResultState ? 0 : 1), expected.PriorState,
            expected.ResultState, expected.ActionCode, transition, observations, false);
    }

    private static List<RepositoryAuthorityObservation> ReadObservations(SqliteConnection connection, SqliteTransaction? transaction, Guid reconciliationId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT authority, disposition, expected_package_id, observed_package_id,
                   expected_content_sha256, observed_content_sha256, expected_byte_length,
                   observed_byte_length, evidence_relative_paths_json
              FROM repository_reconciliation_observations
             WHERE reconciliation_id = $reconciliation_id ORDER BY authority;
            """;
        command.Parameters.AddWithValue("$reconciliation_id", Id(reconciliationId));
        using var reader = command.ExecuteReader();
        var result = new List<RepositoryAuthorityObservation>();
        while (reader.Read())
        {
            result.Add(new(reader.GetString(0), reader.GetString(1), Nullable(reader, 2), Nullable(reader, 3),
                Nullable(reader, 4), Nullable(reader, 5), Nullable(reader, 6), Nullable(reader, 7),
                JsonSerializer.Deserialize<string[]>(reader.GetString(8)) ?? []));
        }
        return result;
    }

    private static string? Nullable(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static bool ObservationEquals(RepositoryAuthorityObservation left, RepositoryAuthorityObservation right) =>
        left.Authority == right.Authority
        && left.Disposition == right.Disposition
        && left.ExpectedPackageId == right.ExpectedPackageId
        && left.ObservedPackageId == right.ObservedPackageId
        && left.ExpectedContentSha256 == right.ExpectedContentSha256
        && left.ObservedContentSha256 == right.ObservedContentSha256
        && left.ExpectedByteLength == right.ExpectedByteLength
        && left.ObservedByteLength == right.ObservedByteLength
        && left.EvidenceRelativePaths.SequenceEqual(right.EvidenceRelativePaths, StringComparer.Ordinal);

    private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static void AddReconciliationParameters(SqliteCommand command, JournalStartupReconciliation value)
    {
        command.Parameters.AddWithValue("$reconciliation_id", Id(value.ReconciliationId));
        command.Parameters.AddWithValue("$transaction_id", Id(value.TransactionId));
        command.Parameters.AddWithValue("$expected_revision", value.ExpectedRevision);
        command.Parameters.AddWithValue("$prior_state", value.PriorState);
        command.Parameters.AddWithValue("$result_state", value.ResultState);
        command.Parameters.AddWithValue("$action_code", value.ActionCode);
        command.Parameters.AddWithValue("$actor", value.ActorWindowsAccount);
        command.Parameters.AddWithValue("$started_utc", value.StartedUtc);
        command.Parameters.AddWithValue("$finished_utc", value.FinishedUtc);
        command.Parameters.AddWithValue("$explanation", value.Explanation);
        command.Parameters.AddWithValue("$result_transition_id", value.ResultTransitionId is null ? DBNull.Value : Id(value.ResultTransitionId.Value));
    }
}

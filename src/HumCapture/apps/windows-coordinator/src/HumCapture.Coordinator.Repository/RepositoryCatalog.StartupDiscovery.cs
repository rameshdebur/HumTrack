using System.Globalization;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    public static IReadOnlyList<RepositoryStartupCandidate> ListStartupCandidates(string database, Guid? after, int limit)
    {
        using var connection = FinalConnection(database, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT transaction_id, state, revision FROM repository_transactions WHERE transaction_id > $after ORDER BY transaction_id LIMIT $limit";
        command.Parameters.AddWithValue("$after", after?.ToString("D") ?? string.Empty);
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        var result = new List<RepositoryStartupCandidate>();
        while (reader.Read())
        {
            result.Add(new(ParseCanonicalId(reader.GetString(0), "transaction_id"), reader.GetString(1), reader.GetInt64(2)));
        }
        return result;
    }

    public static RepositoryCatalogPublicationRequest ReadRetainedPublication(string database, Guid transactionId)
    {
        using var connection = FinalConnection(database, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.catalog_entry_id, x.transition_id, x.operation_id, x.actor_windows_account, x.recorded_utc
            FROM repository_package_catalog c JOIN repository_transitions x ON x.transaction_id = c.transaction_id
            WHERE c.transaction_id = $id AND x.from_state = 'MOVED' AND x.to_state = 'CATALOGED'
              AND x.trigger IN ('NORMAL', 'STARTUP') AND x.actor_kind = 'SYSTEM';
            """;
        command.Parameters.AddWithValue("$id", Id(transactionId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw InvalidCatalog("Startup cannot resolve catalog publication history.");
        }
        var result = new RepositoryCatalogPublicationRequest
        {
            TransactionId = transactionId, CatalogEntryId = ParseCanonicalId(reader.GetString(0), "catalog_entry_id"),
            TransitionId = ParseCanonicalId(reader.GetString(1), "transition_id"),
            OperationId = ParseCanonicalId(reader.GetString(2), "operation_id"),
            ActorWindowsAccount = reader.GetString(3), RecordedAt = RecoveryTime(reader.GetString(4))
        };
        if (reader.Read())
        {
            throw InvalidCatalog("Startup found ambiguous catalog history.");
        }
        return result;
    }

    public static (RepositoryFinalCommitRequest Request, string Trigger) ReadRetainedFinalRequest(
        string database, RepositoryCatalogPublicationRequest publication)
    {
        using var connection = FinalConnection(database, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.commit_record_id, i.index_entry_id, x.reconciliation_id, x.transition_id,
              x.operation_id, x.actor_windows_account, r.started_utc, r.finished_utc, x.trigger
            FROM repository_transactions t
            JOIN repository_transitions x ON x.transaction_id = t.transaction_id AND x.to_state = 'COMMITTED'
            JOIN repository_reconciliations r ON r.reconciliation_id = x.reconciliation_id
            JOIN repository_record_index i ON i.record_id = t.commit_record_id AND i.record_kind = 'commits'
            WHERE t.transaction_id = $id AND t.state = 'COMMITTED';
            """;
        command.Parameters.AddWithValue("$id", Id(publication.TransactionId));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw InvalidCatalog("Committed history or index is missing.");
        }
        var result = new RepositoryFinalCommitRequest
        {
            Publication = publication, CommitRecordId = ParseCanonicalId(reader.GetString(0), "commit_record_id"),
            IndexEntryId = ParseCanonicalId(reader.GetString(1), "index_entry_id"),
            ReconciliationId = ParseCanonicalId(reader.GetString(2), "reconciliation_id"),
            TransitionId = ParseCanonicalId(reader.GetString(3), "transition_id"),
            OperationId = ParseCanonicalId(reader.GetString(4), "operation_id"),
            ActorWindowsAccount = reader.GetString(5), StartedAt = RecoveryTime(reader.GetString(6)),
            RecordedAt = RecoveryTime(reader.GetString(7))
        };
        var trigger = reader.GetString(8);
        if (trigger is not ("STARTUP" or "PRE_RECEIPT") || reader.Read())
        {
            throw InvalidCatalog("Finalization history is unsupported or ambiguous.");
        }
        return (result, trigger);
    }

    private static DateTimeOffset RecoveryTime(string value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp)
            ? timestamp : throw InvalidCatalog("Invalid retained audit timestamp.");
}

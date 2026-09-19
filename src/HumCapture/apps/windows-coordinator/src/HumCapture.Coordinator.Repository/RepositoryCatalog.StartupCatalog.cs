using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryCatalog
{
    internal const string CatalogRecoveryExplanation = "Exact moved package permits catalog completion.";

    public static void RequirePublicationHistory(string database, JournalCommitContext context,
        RepositoryCatalogPublicationRequest publication, long revision)
    {
        var normal = new JournalNormalTransition(publication.TransactionId, revision - 1, revision,
            "MOVED", "CATALOGED", publication.TransitionId, publication.OperationId,
            publication.ActorWindowsAccount, FormatUtc(publication.RecordedAt));
        using var connection = FinalConnection(database, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT trigger FROM repository_transitions WHERE transition_id = $transition_id";
        command.Parameters.AddWithValue("$transition_id", Id(publication.TransitionId));
        if (command.ExecuteScalar() as string == "NORMAL")
        {
            RequireExactNormalTransition(database, normal);
            return;
        }
        command.CommandText = """
            SELECT r.reconciliation_id FROM repository_transitions x
            JOIN repository_reconciliations r ON r.reconciliation_id = x.reconciliation_id
            WHERE x.transition_id = $transition_id AND x.transaction_id = $transaction_id
              AND x.transition_sequence = $revision AND x.from_state = 'MOVED' AND x.to_state = 'CATALOGED'
              AND x.operation_id = $operation_id AND x.trigger = 'STARTUP' AND x.actor_kind = 'SYSTEM'
              AND x.actor_windows_account = $actor AND x.recorded_utc = $recorded
              AND x.reason_code = 'EXACT_EVIDENCE_RECONCILED' AND x.reason = $explanation
              AND r.transaction_id = x.transaction_id AND r.trigger = 'STARTUP' AND r.actor_kind = 'SYSTEM'
              AND r.actor_windows_account = $actor AND r.result_transition_id = x.transition_id
              AND r.prior_state = 'MOVED' AND r.result_state = 'CATALOGED'
              AND r.action_code = 'COMPLETE_CATALOGING' AND r.automatic = 1
              AND r.blocking_reason_codes_json = '[]' AND r.explanation = $explanation
              AND r.finished_utc = $recorded;
            """;
        command.Parameters.AddWithValue("$transaction_id", Id(publication.TransactionId));
        command.Parameters.AddWithValue("$revision", revision);
        command.Parameters.AddWithValue("$operation_id", Id(publication.OperationId));
        command.Parameters.AddWithValue("$actor", publication.ActorWindowsAccount);
        command.Parameters.AddWithValue("$recorded", FormatUtc(publication.RecordedAt));
        command.Parameters.AddWithValue("$explanation", CatalogRecoveryExplanation);
        var reconciliation = command.ExecuteScalar() as string
            ?? throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Catalog recovery history disagrees.");
        var observations = ReadObservations(connection, null, ParseCanonicalId(reconciliation, "reconciliation_id"));
        var expected = ExpectedMovedObservations(context);
        if (observations.Count != expected.Length
            || observations.Zip(expected).Any(pair => !ObservationEquals(pair.First, pair.Second)))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Catalog recovery observations disagree.");
        }
        RequireContiguousCurrentHistory(connection, context.TransactionId, context.Revision, context.State);
    }

    private static RepositoryAuthorityObservation[] ExpectedMovedObservations(JournalCommitContext c)
    {
        RepositoryAuthorityObservation Item(string authority, string disposition, string? path = null) =>
            new(authority, disposition, Id(c.PackageId), disposition == "MATCH" ? Id(c.PackageId) : null,
                c.PackageContentSha256, disposition == "MATCH" ? c.PackageContentSha256 : null,
                c.PackageByteLength, disposition == "MATCH" ? c.PackageByteLength : null,
                path is null ? [] : [path]);
        return
        [
            Item("CATALOG_LINKAGE", "ABSENT"),
            Item("COMMIT_RECORD", "ABSENT"),
            Item("DESTINATION_PACKAGE", "MATCH", c.DestinationRelativePath),
            Item("JOURNAL", "MATCH", RepositoryConstants.CatalogRelativePath),
            Item("STAGING_PACKAGE", "ABSENT", c.StagingRelativePath),
            Item("VERIFICATION_RECORD", "MATCH", c.VerificationRecordRelativePath)
        ];
    }
}

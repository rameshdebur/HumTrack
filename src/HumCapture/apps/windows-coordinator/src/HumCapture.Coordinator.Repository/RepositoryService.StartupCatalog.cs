namespace HumCapture.Coordinator.Repository;

public sealed partial class RepositoryService
{
    private RepositoryStartupReconciliationSnapshot CompleteStartupCatalog(RepositoryOpenResult opened,
        string database, JournalCommitContext context, RepositoryStartupReconciliationRequest startup)
    {
        RequireMovedPackage(opened.RootPath, context);
        var observed = ObserveStartupAuthorities(opened.RootPath, database, context);
        if (ClassifyStartupAction("MOVED", observed.ToDictionary(item => item.Authority, StringComparer.Ordinal)) is null
            || FindUnindexedCommit(opened.RootPath, context) is not null)
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired,
                "Catalog recovery requires exact moved evidence and absent catalog/commit records; retain conflicts.");
        }
        var publication = new RepositoryCatalogPublicationRequest
        {
            TransactionId = context.TransactionId, CatalogEntryId = Guid.NewGuid(),
            TransitionId = startup.ResultTransitionId!.Value, OperationId = startup.ResultOperationId!.Value,
            ActorWindowsAccount = startup.ActorWindowsAccount, RecordedAt = startup.FinishedAt
        };
        var value = new JournalStartupReconciliation(startup.ReconciliationId, context.TransactionId,
            context.Revision, "MOVED", "CATALOGED", "COMPLETE_CATALOGING",
            startup.ResultTransitionId, startup.ResultOperationId, startup.ActorWindowsAccount,
            Utc(startup.StartedAt), Utc(startup.FinishedAt), RepositoryCatalog.CatalogRecoveryExplanation,
            observed.OrderBy(item => item.Authority, StringComparer.Ordinal).ToArray());
        var transition = new JournalNormalTransition(context.TransactionId, context.Revision, context.Revision + 1,
            "MOVED", "CATALOGED", publication.TransitionId, publication.OperationId,
            publication.ActorWindowsAccount, Utc(publication.RecordedAt));
        RepositoryCatalog.PublishCatalog(database, context, publication, transition, value);
        RevalidateStartupCatalog(opened, database, context.TransactionId);
        return RepositoryCatalog.ReadStartupReconciliationReplay(database, startup)! with { WasAlreadyRecorded = false };
    }

    private void RevalidateStartupCatalog(RepositoryOpenResult opened, string database, Guid transactionId)
    {
        var context = RepositoryCatalog.ReadCommitContext(database, transactionId, opened.Descriptor.RepositoryId);
        if (context.State is not ("CATALOGED" or "COMMITTED"))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Recovered catalog state is no longer supported.");
        }
        var publication = RepositoryCatalog.ReadRetainedPublication(database, transactionId);
        RequireMovedPackage(opened.RootPath, context);
        RepositoryCatalog.RequirePublishedCatalog(database, context, publication, forFinalization: true);
        RepositoryCatalog.RequirePublicationHistory(database, context, publication,
            context.Revision - (context.State == "COMMITTED" ? 1 : 0));
        if (context.State == "COMMITTED")
        {
            RevalidateRetainedCommit(opened, database, transactionId);
        }
    }
}

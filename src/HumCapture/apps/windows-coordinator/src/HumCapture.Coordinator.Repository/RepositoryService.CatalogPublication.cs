namespace HumCapture.Coordinator.Repository;

public sealed partial class RepositoryService
{
    /// <summary>
    /// Revalidates a moved package and atomically publishes its catalog entry and
    /// CATALOGED transition. This does not authorize receipts or source cleanup.
    /// </summary>
    public RepositoryCatalogPublicationSnapshot PublishMovedPackage(
        string rootPath, RepositoryCatalogPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransactionId == Guid.Empty || request.CatalogEntryId == Guid.Empty
            || request.TransitionId == Guid.Empty || request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("Publication identities must not be empty.", nameof(request));
        }
        ValidateWindowsAccount(request.ActorWindowsAccount);
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Catalog publication requires a supported writable repository.");
        }
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            var databasePath = RepositoryPathSafety.ResolveRelativePath(opened.RootPath, RepositoryConstants.CatalogRelativePath);
            var context = RepositoryCatalog.ReadCommitContext(databasePath, request.TransactionId, opened.Descriptor.RepositoryId);
            if (context.State is not ("MOVED" or "CATALOGED"))
            {
                throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Catalog publication requires a proven MOVED boundary or exact CATALOGED replay.");
            }
            RequireMovedPackage(opened.RootPath, context);
            var replay = context.State == "CATALOGED";
            var revision = context.Revision - (replay ? 1 : 0);
            var transition = new JournalNormalTransition(request.TransactionId, revision, revision + 1,
                "MOVED", "CATALOGED", request.TransitionId, request.OperationId,
                request.ActorWindowsAccount, Utc(request.RecordedAt));
            if (replay)
            {
                RepositoryCatalog.RequireExactNormalTransition(databasePath, transition);
                RepositoryCatalog.RequirePublishedCatalog(databasePath, context, request);
            }
            else
            {
                RepositoryCatalog.PublishCatalog(databasePath, context, request, transition);
                RepositoryCatalog.RequireExactNormalTransition(databasePath, transition);
                RepositoryCatalog.RequirePublishedCatalog(databasePath, context, request);
            }
            return new(Id(request.TransactionId), Id(request.CatalogEntryId), revision + 1, replay);
        }
    }
}

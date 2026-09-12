namespace HumCapture.Coordinator.Repository;

/// <summary>Immutable identities for publishing one MOVED package to the catalog.</summary>
public sealed record RepositoryCatalogPublicationRequest
{
    /// <summary>Gets the existing transaction identity.</summary>
    public required Guid TransactionId { get; init; }
    /// <summary>Gets the immutable catalog entry identity.</summary>
    public required Guid CatalogEntryId { get; init; }
    /// <summary>Gets the journal transition identity.</summary>
    public required Guid TransitionId { get; init; }
    /// <summary>Gets the transition idempotency identity.</summary>
    public required Guid OperationId { get; init; }
    /// <summary>Gets the Windows account performing publication.</summary>
    public required string ActorWindowsAccount { get; init; }
    /// <summary>Gets the audit timestamp retained on exact replay.</summary>
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>A cataloged package awaiting final commit reconciliation.</summary>
/// <param name="TransactionId">Transaction identity.</param>
/// <param name="CatalogEntryId">Immutable catalog entry identity.</param>
/// <param name="Revision">Journal revision after publication.</param>
/// <param name="WasAlreadyCataloged">Whether this request was already published.</param>
public sealed record RepositoryCatalogPublicationSnapshot(
    string TransactionId, string CatalogEntryId, long Revision, bool WasAlreadyCataloged);

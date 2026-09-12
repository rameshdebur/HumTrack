namespace HumCapture.Coordinator.Repository;

/// <summary>Retained identities for finalizing an exact catalog publication.</summary>
public sealed record RepositoryFinalCommitRequest
{
    /// <summary>Gets the original catalog publication request.</summary>
    public required RepositoryCatalogPublicationRequest Publication { get; init; }
    /// <summary>Gets the immutable commit record identity.</summary>
    public required Guid CommitRecordId { get; init; }
    /// <summary>Gets the immutable record index identity.</summary>
    public required Guid IndexEntryId { get; init; }
    /// <summary>Gets the retained reconciliation identity.</summary>
    public required Guid ReconciliationId { get; init; }
    /// <summary>Gets the final transition identity.</summary>
    public required Guid TransitionId { get; init; }
    /// <summary>Gets the final operation idempotency identity.</summary>
    public required Guid OperationId { get; init; }
    /// <summary>Gets the Windows account performing finalization.</summary>
    public required string ActorWindowsAccount { get; init; }
    /// <summary>Gets the audit start time.</summary>
    public required DateTimeOffset StartedAt { get; init; }
    /// <summary>Gets the audit completion time retained on replay.</summary>
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>Final repository evidence validated for one package.</summary>
/// <param name="TransactionId">Transaction identity.</param>
/// <param name="Revision">Committed journal revision.</param>
/// <param name="CommitRecordRelativePath">Immutable record location.</param>
/// <param name="CommitRecordSha256">Exact record byte hash.</param>
/// <param name="WasAlreadyCommitted">Whether an existing commit was revalidated.</param>
public sealed record RepositoryFinalCommitSnapshot(string TransactionId, long Revision,
    string CommitRecordRelativePath, string CommitRecordSha256, bool WasAlreadyCommitted);

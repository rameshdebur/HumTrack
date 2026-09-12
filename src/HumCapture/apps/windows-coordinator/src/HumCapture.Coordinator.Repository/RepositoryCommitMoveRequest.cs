namespace HumCapture.Coordinator.Repository;

/// <summary>
/// Supplies the two durable normal-transition identities required to move an exact
/// STAGED_VERIFIED package across the repository boundary without cataloging it.
/// </summary>
public sealed record RepositoryCommitMoveRequest
{
    /// <summary>Gets the existing repository transaction identity.</summary>
    public required Guid TransactionId { get; init; }
    /// <summary>Gets the append-only STAGED_VERIFIED to COMMITTING transition identity.</summary>
    public required Guid CommittingTransitionId { get; init; }
    /// <summary>Gets the idempotency identity for the COMMITTING transition.</summary>
    public required Guid CommittingOperationId { get; init; }
    /// <summary>Gets the append-only COMMITTING to MOVED transition identity.</summary>
    public required Guid MovedTransitionId { get; init; }
    /// <summary>Gets the idempotency identity for the MOVED transition.</summary>
    public required Guid MovedOperationId { get; init; }
    /// <summary>Gets the signed-in Windows account performing the system operation.</summary>
    public required string ActorWindowsAccount { get; init; }
    /// <summary>Gets the audit timestamp recorded with the durable commit intent.</summary>
    public required DateTimeOffset CommittingRecordedAt { get; init; }
    /// <summary>Gets the audit timestamp recorded after the package move is verified.</summary>
    public required DateTimeOffset MovedRecordedAt { get; init; }
}

/// <summary>Represents a package proven at or beyond the repository MOVED boundary.</summary>
/// <param name="TransactionId">Repository transaction identity.</param>
/// <param name="Revision">Current transaction revision.</param>
/// <param name="State">Current controlled state; a first C3 completion returns MOVED.</param>
/// <param name="StagingRelativePath">Canonical now-absent staging path.</param>
/// <param name="DestinationRelativePath">Canonical exact package destination.</param>
/// <param name="CommittingOperationId">Commit-intent idempotency identity.</param>
/// <param name="MovedOperationId">Move-completion idempotency identity.</param>
/// <param name="WasAlreadyMoved">Whether an exact prior C3 operation was returned.</param>
public sealed record RepositoryCommitMoveSnapshot(
    string TransactionId,
    long Revision,
    string State,
    string StagingRelativePath,
    string DestinationRelativePath,
    string CommittingOperationId,
    string MovedOperationId,
    bool WasAlreadyMoved);

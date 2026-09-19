namespace HumCapture.Coordinator.Repository;

/// <summary>Supplies immutable identities for one bounded startup reconciliation.</summary>
public sealed record RepositoryStartupReconciliationRequest
{
    /// <summary>Gets the repository transaction to observe.</summary>
    public required Guid TransactionId { get; init; }
    /// <summary>Gets the append-only reconciliation identity.</summary>
    public required Guid ReconciliationId { get; init; }
    /// <summary>Gets the transition identity when reconciliation changes state.</summary>
    public Guid? ResultTransitionId { get; init; }
    /// <summary>Gets the transition idempotency identity when reconciliation changes state.</summary>
    public Guid? ResultOperationId { get; init; }
    /// <summary>Gets the signed-in Windows account under which the system action runs.</summary>
    public required string ActorWindowsAccount { get; init; }
    /// <summary>Gets the audit start timestamp.</summary>
    public required DateTimeOffset StartedAt { get; init; }
    /// <summary>Gets the audit finish timestamp.</summary>
    public required DateTimeOffset FinishedAt { get; init; }
}

/// <summary>One retained authority observation from startup reconciliation.</summary>
/// <param name="Authority">Controlled repository authority.</param>
/// <param name="Disposition">Controlled observation disposition.</param>
/// <param name="ExpectedPackageId">Expected package identity.</param>
/// <param name="ObservedPackageId">Observed package identity when exactly established.</param>
/// <param name="ExpectedContentSha256">Expected package content identity.</param>
/// <param name="ObservedContentSha256">Observed package content identity when exactly established.</param>
/// <param name="ExpectedByteLength">Expected package byte length.</param>
/// <param name="ObservedByteLength">Observed package byte length when exactly established.</param>
/// <param name="EvidenceRelativePaths">Bounded repository-relative evidence paths.</param>
public sealed record RepositoryAuthorityObservation(
    string Authority,
    string Disposition,
    string? ExpectedPackageId,
    string? ObservedPackageId,
    string? ExpectedContentSha256,
    string? ObservedContentSha256,
    string? ExpectedByteLength,
    string? ObservedByteLength,
    IReadOnlyList<string> EvidenceRelativePaths);

/// <summary>Result of one exact, automatic startup reconciliation.</summary>
/// <param name="ReconciliationId">Reconciliation identity.</param>
/// <param name="TransactionId">Repository transaction identity.</param>
/// <param name="Revision">Current durable transaction revision after the action.</param>
/// <param name="PriorState">State observed before the action.</param>
/// <param name="ResultState">State proven by the action.</param>
/// <param name="ActionCode">Bounded automatic action.</param>
/// <param name="ResultTransitionId">Transition identity when state changed.</param>
/// <param name="Observations">Six retained authority observations.</param>
/// <param name="WasAlreadyRecorded">Whether this is an exact idempotent replay.</param>
public sealed record RepositoryStartupReconciliationSnapshot(
    string ReconciliationId,
    string TransactionId,
    long Revision,
    string PriorState,
    string ResultState,
    string ActionCode,
    string? ResultTransitionId,
    IReadOnlyList<RepositoryAuthorityObservation> Observations,
    bool WasAlreadyRecorded);

internal sealed record JournalStartupReconciliation(
    Guid ReconciliationId,
    Guid TransactionId,
    long ExpectedRevision,
    string PriorState,
    string ResultState,
    string ActionCode,
    Guid? ResultTransitionId,
    Guid? ResultOperationId,
    string ActorWindowsAccount,
    string StartedUtc,
    string FinishedUtc,
    string Explanation,
    IReadOnlyList<RepositoryAuthorityObservation> Observations,
    string Trigger = "STARTUP");

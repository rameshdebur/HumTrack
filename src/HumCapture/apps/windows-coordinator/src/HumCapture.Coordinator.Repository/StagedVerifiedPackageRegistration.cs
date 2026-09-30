namespace HumCapture.Coordinator.Repository;

/// <summary>
/// Supplies the immutable identities and verification evidence required to admit one
/// already-collected package into the repository journal as STAGED_VERIFIED.
/// </summary>
public sealed record StagedVerifiedPackageRegistration
{
    /// <summary>Gets the stable repository transaction identity.</summary>
    public required Guid TransactionId { get; init; }
    /// <summary>Gets the identity of the initial append-only transition.</summary>
    public required Guid TransitionId { get; init; }
    /// <summary>Gets the idempotency identity for the initial operation.</summary>
    public required Guid OperationId { get; init; }
    /// <summary>Gets the immutable verification-record index identity.</summary>
    public required Guid RecordIndexEntryId { get; init; }
    /// <summary>Gets the pseudonymized subject identity.</summary>
    public required Guid SubjectId { get; init; }
    /// <summary>Gets the session identity.</summary>
    public required Guid SessionId { get; init; }
    /// <summary>Gets the trial identity.</summary>
    public required Guid TrialId { get; init; }
    /// <summary>Gets the capture-source identity.</summary>
    public required Guid SourceId { get; init; }
    /// <summary>Gets the capture-attempt identity.</summary>
    public required Guid CaptureAttemptId { get; init; }
    /// <summary>Gets the collection-attempt identity used by the staging namespace.</summary>
    public required Guid CollectionAttemptId { get; init; }
    /// <summary>Gets the immutable package identity.</summary>
    public required Guid PackageId { get; init; }
    /// <summary>Gets the canonical package-content SHA-256.</summary>
    public required string PackageContentSha256 { get; init; }
    /// <summary>Gets the canonical artifact-set SHA-256.</summary>
    public required string ArtifactSetSha256 { get; init; }
    /// <summary>Gets the immutable verification-record identity.</summary>
    public required Guid VerificationRecordId { get; init; }
    /// <summary>Gets the SHA-256 of the exact verification-record bytes.</summary>
    public required string VerificationRecordContentSha256 { get; init; }
    /// <summary>Gets the canonical unsigned-decimal package byte length.</summary>
    public required string PackageByteLength { get; init; }
    /// <summary>Gets the number of manifest artifacts.</summary>
    public required int ArtifactCount { get; init; }
    /// <summary>Gets the signed-in Windows account performing the system operation.</summary>
    public required string ActorWindowsAccount { get; init; }
    /// <summary>Gets the exact immutable verification-record UTF-8 bytes.</summary>
    public required ReadOnlyMemory<byte> VerificationRecordUtf8 { get; init; }
    /// <summary>Gets an optional injected UTC timestamp used for deterministic verification.</summary>
    public DateTimeOffset? RecordedAt { get; init; }
}

/// <summary>Represents the durable initial repository-transaction state.</summary>
/// <param name="TransactionId">Repository transaction identity.</param>
/// <param name="Revision">Current transaction revision.</param>
/// <param name="State">Current controlled state.</param>
/// <param name="StagingRelativePath">Canonical package staging path.</param>
/// <param name="DestinationRelativePath">Canonical future package destination.</param>
/// <param name="VerificationRecordRelativePath">Immutable verification milestone path.</param>
/// <param name="OperationId">Initial idempotency operation identity.</param>
/// <param name="RecordedUtc">Canonical journal time for audit and display.</param>
/// <param name="WasAlreadyPresent">Whether an exact prior admission was returned.</param>
public sealed record RepositoryTransactionSnapshot(
    string TransactionId,
    long Revision,
    string State,
    string StagingRelativePath,
    string DestinationRelativePath,
    string VerificationRecordRelativePath,
    string OperationId,
    string RecordedUtc,
    bool WasAlreadyPresent);

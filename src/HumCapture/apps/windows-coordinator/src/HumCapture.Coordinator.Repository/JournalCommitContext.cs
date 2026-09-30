namespace HumCapture.Coordinator.Repository;

internal sealed record JournalCommitContext(
    string RepositoryId,
    Guid TransactionId,
    long Revision,
    string State,
    Guid SubjectId,
    Guid SessionId,
    Guid TrialId,
    Guid SourceId,
    Guid CaptureAttemptId,
    Guid CollectionAttemptId,
    Guid PackageId,
    string PackageContentSha256,
    string ArtifactSetSha256,
    Guid VerificationRecordId,
    string VerificationRecordContentSha256,
    string StagingRelativePath,
    string DestinationRelativePath,
    string PackageByteLength,
    int ArtifactCount,
    int VerificationRecordRevision,
    string VerificationRecordRelativePath);

internal sealed record StagedPackageEvidenceExpectation(
    Guid SubjectId,
    Guid SessionId,
    Guid TrialId,
    Guid SourceId,
    Guid CaptureAttemptId,
    Guid PackageId,
    string PackageContentSha256,
    string ArtifactSetSha256,
    Guid VerificationRecordId,
    string VerificationRecordContentSha256,
    string PackageByteLength,
    int ArtifactCount,
    ReadOnlyMemory<byte> VerificationRecordUtf8)
{
    public static StagedPackageEvidenceExpectation FromRegistration(StagedVerifiedPackageRegistration registration) =>
        new(
            registration.SubjectId,
            registration.SessionId,
            registration.TrialId,
            registration.SourceId,
            registration.CaptureAttemptId,
            registration.PackageId,
            registration.PackageContentSha256,
            registration.ArtifactSetSha256,
            registration.VerificationRecordId,
            registration.VerificationRecordContentSha256,
            registration.PackageByteLength,
            registration.ArtifactCount,
            registration.VerificationRecordUtf8);
}

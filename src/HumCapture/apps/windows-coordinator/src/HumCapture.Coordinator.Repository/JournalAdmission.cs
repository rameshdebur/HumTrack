namespace HumCapture.Coordinator.Repository;

internal sealed record JournalAdmission(
    string RepositoryId,
    StagedVerifiedPackageRegistration Registration,
    string StagingRelativePath,
    string DestinationRelativePath,
    string VerificationRecordRelativePath,
    int VerificationRecordRevision,
    string RecordedUtc);

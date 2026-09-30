using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record VerificationAdmissionRequest(Guid CollectionAttemptId, Guid PackageId, Guid TransactionId,
    Guid TransitionId, Guid OperationId, Guid RecordIndexEntryId, Guid VerificationRecordId, DateTimeOffset AuditTime);
internal sealed record VerificationAdmissionResult(ProducedVerification Verification, RepositoryTransactionSnapshot? Admission);

public sealed partial class RepositoryService
{
    // Internal integration only. No host command, movement or completion authority.
    internal async Task<VerificationAdmissionResult> VerifyAndAdmitAsync(string rootPath,
        VerificationAdmissionRequest request, string binaries, TimeSpan timeout, CancellationToken token = default,
        ProtocolEvidenceContext? protocol = null)
    {
        token.ThrowIfCancellationRequested();
        var ids = new[] { request.CollectionAttemptId, request.PackageId, request.TransactionId, request.TransitionId,
            request.OperationId, request.RecordIndexEntryId, request.VerificationRecordId };
        if (Array.Exists(ids, id => id == Guid.Empty) || request.AuditTime == default)
        { throw new ArgumentException("Stable nonempty operation IDs and audit time required.", nameof(request)); }
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        { throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Verification admission requires a writable compatible repository."); }
        var path = RepositoryPathSafety.ResolveRelativePath(opened.RootPath,
            $"staging/{request.CollectionAttemptId:D}/{request.PackageId:D}");
        using var lease = PackageInputLease.Open(path, token);
        var manifest = lease.Manifest.Json;
        PackageInputManifest.Need(manifest.GetProperty("package_id").GetGuid() == request.PackageId, "Staging namespace/package identity mismatch.");
        var result = await PackageVerificationProducer.VerifyLeasedAsync(lease, binaries, timeout,
            request.VerificationRecordId, request.AuditTime, token, protocol).ConfigureAwait(false);
        if (result.Outcome != "VERIFIED") { return new(result, null); }
        using var record = JsonDocument.Parse(result.Utf8);
        var registration = new StagedVerifiedPackageRegistration
        {
            CollectionAttemptId = request.CollectionAttemptId, PackageId = request.PackageId,
            TransactionId = request.TransactionId, TransitionId = request.TransitionId, OperationId = request.OperationId,
            RecordIndexEntryId = request.RecordIndexEntryId, VerificationRecordId = request.VerificationRecordId,
            SubjectId = manifest.GetProperty("subject_id").GetGuid(), SessionId = manifest.GetProperty("session_id").GetGuid(),
            TrialId = manifest.GetProperty("trial_id").GetGuid(), SourceId = manifest.GetProperty("source_id").GetGuid(),
            CaptureAttemptId = manifest.GetProperty("capture_attempt_id").GetGuid(),
            PackageContentSha256 = manifest.GetProperty("package_content_sha256").GetString()!,
            ArtifactSetSha256 = manifest.GetProperty("artifact_set_sha256").GetString()!,
            PackageByteLength = manifest.GetProperty("package_byte_length").GetString()!, ArtifactCount = lease.Manifest.Artifacts.Count,
            ActorWindowsAccount = record.RootElement.GetProperty("verified_by_windows_account").GetString()!,
            VerificationRecordContentSha256 = result.Sha256, VerificationRecordUtf8 = result.Utf8, RecordedAt = request.AuditTime
        };
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            token.ThrowIfCancellationRequested();
            lease.CheckUnchanged(token);
            return new(result, RegisterStagedVerifiedPackage(opened.RootPath, registration));
        }
    }
}

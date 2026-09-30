using System.Text.Json;
using System.Text.Json.Nodes;

namespace HumCapture.Coordinator.Repository;

internal sealed record BoundVerificationRequest(Guid AssignmentId, VerificationAdmissionRequest Admission, string SchemaVersion = "1.0.0");
internal sealed record VerificationWorkflowEvent(string State, string ReasonCode, string NextAction,
    byte[]? VerificationUtf8 = null, string? VerificationSha256 = null,
    RepositoryTransactionSnapshot? Admission = null, string SchemaVersion = "1.0.0");

public sealed partial class RepositoryService
{
    internal void InitializeVerificationWorkflow(string rootPath)
    {
        var opened = WritableWorkflow(rootPath);
        using var guard = VerificationWorkflowStore.Lock(opened.RootPath);
        VerificationWorkflowStore.Initialize(opened);
    }
    internal void RecordCaptureAssignment(string rootPath, CaptureAssignment assignment)
    {
        assignment = assignment.Freeze(); assignment.Validate();
        var opened = WritableWorkflow(rootPath);
        using var guard = VerificationWorkflowStore.Lock(opened.RootPath);
        using var store = VerificationWorkflowStore.Open(opened); store.SaveAssignment(assignment);
    }
    internal IReadOnlyList<VerificationWorkflowEvent> VerificationHistory(string rootPath, Guid attemptId, int after = 0, int limit = 100)
    {
        var opened = WritableWorkflow(rootPath);
        using var guard = VerificationWorkflowStore.Lock(opened.RootPath);
        using var store = VerificationWorkflowStore.Open(opened); return store.History(attemptId, after, limit);
    }
    private RepositoryOpenResult WritableWorkflow(string rootPath)
    {
        var opened = Open(rootPath);
        if (!opened.CanMutate) { throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Workflow needs a compatible writable repository."); }
        return opened;
    }

    internal async Task<VerificationWorkflowEvent> CollectVerifyAssignedAsync(string rootPath, string? sourcePath,
        BoundVerificationRequest request, string binaries, TimeSpan timeout, CancellationToken token = default,
        Action<string>? testBoundary = null)
    {
        token.ThrowIfCancellationRequested(); ValidateWorkflowRequest(request);
        var opened = WritableWorkflow(rootPath);
        using var guard = VerificationWorkflowStore.Lock(opened.RootPath);
        using var store = VerificationWorkflowStore.Open(opened);
        var assignment = store.Assignment(request.AssignmentId);
        var id = request.Admission.VerificationRecordId;
        var prior = store.Request(id);
        if (prior is not null)
        {
            PackageInputManifest.Need(prior.AsSpan().SequenceEqual(WorkflowJson.Encode(request)), "Attempt identity/request conflict.");
            return RecoverWorkflow(opened, store, request, assignment, token);
        }
        store.Start(request);
        testBoundary?.Invoke("STARTED");
        try
        {
            if (sourcePath is not null)
            {
                using (var source = PackageInputLease.Open(sourcePath, token))
                {
                    assignment.Bind(source.Manifest.Json);
                    PackageInputManifest.Need(source.Manifest.Json.GetProperty("package_id").GetGuid() == request.Admission.PackageId,
                        "COORDINATOR_ASSIGNMENT_MISMATCH:package");
                }
                _ = CollectLocalFolder(opened.RootPath, sourcePath, request.Admission.CollectionAttemptId, token);
            }
            var path = WorkflowStaging(opened.RootPath, request.Admission);
            using var lease = PackageInputLease.Open(path, token);
            assignment.Bind(lease.Manifest.Json);
            PackageInputManifest.Need(lease.Manifest.Json.GetProperty("package_id").GetGuid() == request.Admission.PackageId, "Staging package identity mismatch.");
            var verification = await PackageVerificationProducer.VerifyLeasedAsync(lease, binaries, timeout,
                id, request.Admission.AuditTime, token, assignment.Context).ConfigureAwait(false);
            var bytes = BindVerification(verification.Utf8, assignment);
            var result = new VerificationWorkflowEvent(verification.Outcome == "VERIFIED" ? "VERIFIED_READY" : "FAILED",
                verification.Outcome == "VERIFIED" ? "VERIFICATION_PASSED" : "VERIFICATION_INCOMPLETE_OR_FAILED",
                verification.Outcome == "VERIFIED" ? "RECOVER_ADMISSION" : "RETRY_NEW_ATTEMPT",
                bytes, PackageInputManifest.Hash(bytes));
            store.Append(id, result);
            if (result.State == "FAILED") { return result; }
            testBoundary?.Invoke("VERIFIED_READY");
            token.ThrowIfCancellationRequested(); lease.CheckUnchanged(token);
            var admitted = AdmitWorkflow(opened, request, assignment, lease, result, token);
            testBoundary?.Invoke("CATALOG_ADMITTED");
            store.Append(id, admitted);
            return admitted;
        }
        catch (OperationCanceledException)
        {
            // Once READY exists retain it for explicit recovery, rather than discard verified evidence.
            if (store.Latest(id).State == "VERIFIED_READY") { throw; }
            var cancelled = new VerificationWorkflowEvent("CANCELLED", "CANCELLED_BEFORE_ADMISSION", "RETRY_NEW_ATTEMPT");
            store.Append(id, cancelled); return cancelled;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or RepositoryException)
        {
            // A durable READY record remains recoverable if admission itself failed.
            if (store.Latest(id).State == "VERIFIED_READY") { throw; }
            var code = error is RepositoryException ? "REPOSITORY_REFUSED" : "SOURCE_OR_ASSIGNMENT_REFUSED";
            var action = error is RepositoryException ? "RECONCILE_REPOSITORY" : "CORRECT_ASSIGNMENT_OR_SOURCE";
            var failed = new VerificationWorkflowEvent("FAILED", code, action);
            store.Append(id, failed); return failed;
        }
    }

    internal VerificationWorkflowEvent RecoverVerificationAttempt(string rootPath, Guid attemptId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var opened = WritableWorkflow(rootPath);
        using var guard = VerificationWorkflowStore.Lock(opened.RootPath);
        using var store = VerificationWorkflowStore.Open(opened);
        var request = WorkflowJson.Read<BoundVerificationRequest>(store.Request(attemptId)
            ?? throw new InvalidDataException("Verification attempt absent."));
        ValidateWorkflowRequest(request);
        PackageInputManifest.Need(request.Admission.VerificationRecordId == attemptId, "Attempt key mismatch.");
        return RecoverWorkflow(opened, store, request, store.Assignment(request.AssignmentId), token);
    }
    private VerificationWorkflowEvent RecoverWorkflow(RepositoryOpenResult opened, VerificationWorkflowStore store,
        BoundVerificationRequest request, CaptureAssignment assignment, CancellationToken token)
    {
        var id = request.Admission.VerificationRecordId; var last = store.Latest(id);
        PackageInputManifest.Need(last.SchemaVersion == "1.0.0"
            && last.State is "STARTED" or "VERIFIED_READY" or "ADMITTED" or "FAILED" or "CANCELLED" or "INTERRUPTED", "Unknown workflow event.");
        if (last.State == "STARTED")
        {
            var interrupted = new VerificationWorkflowEvent("INTERRUPTED", "PROCESS_ENDED_WITHOUT_RESULT", "RETRY_NEW_ATTEMPT");
            store.Append(id, interrupted); return interrupted;
        }
        if (last.State != "VERIFIED_READY") { return last; }
        using var lease = PackageInputLease.Open(WorkflowStaging(opened.RootPath, request.Admission), token);
        assignment.Bind(lease.Manifest.Json);
        token.ThrowIfCancellationRequested();
        var result = AdmitWorkflow(opened, request, assignment, lease, last, token);
        store.Append(id, result); return result;
    }
    private VerificationWorkflowEvent AdmitWorkflow(RepositoryOpenResult opened, BoundVerificationRequest request,
        CaptureAssignment assignment, PackageInputLease lease, VerificationWorkflowEvent ready, CancellationToken token)
    {
        PackageInputManifest.Need(ready.VerificationUtf8 is not null
            && ready.VerificationSha256 == PackageInputManifest.Hash(ready.VerificationUtf8), "Retained verification hash mismatch.");
        var record = new MetadataSchemaValidator().Validate(MetadataKind.Verification, ready.VerificationUtf8!);
        var m = lease.Manifest.Json; var a = request.Admission;
        PackageInputManifest.Need(record.GetProperty("outcome").GetString() == "VERIFIED"
            && record.GetProperty("verification_record_id").GetGuid() == a.VerificationRecordId
            && record.GetProperty("checks").EnumerateArray().Any(c => c.GetProperty("check").GetString() == "PACKAGE_IDENTITY"
                && c.GetProperty("evidence_references").EnumerateArray().Any(e => e.GetString() == AssignmentReference(assignment))),
            "Retained verification does not bind coordinator assignment.");
        var registration = new StagedVerifiedPackageRegistration
        {
            CollectionAttemptId = a.CollectionAttemptId, PackageId = a.PackageId, TransactionId = a.TransactionId,
            TransitionId = a.TransitionId, OperationId = a.OperationId, RecordIndexEntryId = a.RecordIndexEntryId,
            VerificationRecordId = a.VerificationRecordId, SubjectId = assignment.SubjectId, SessionId = assignment.SessionId,
            TrialId = assignment.TrialId, SourceId = assignment.SourceId, CaptureAttemptId = assignment.CaptureAttemptId,
            PackageContentSha256 = m.GetProperty("package_content_sha256").GetString()!,
            ArtifactSetSha256 = m.GetProperty("artifact_set_sha256").GetString()!,
            PackageByteLength = m.GetProperty("package_byte_length").GetString()!, ArtifactCount = lease.Manifest.Artifacts.Count,
            ActorWindowsAccount = record.GetProperty("verified_by_windows_account").GetString()!,
            VerificationRecordContentSha256 = ready.VerificationSha256!, VerificationRecordUtf8 = ready.VerificationUtf8!, RecordedAt = a.AuditTime
        };
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            lease.CheckUnchanged(token);
            token.ThrowIfCancellationRequested();
            var admitted = RegisterStagedVerifiedPackage(opened.RootPath, registration);
            return ready with { State = "ADMITTED", ReasonCode = "STAGED_ADMISSION_RECORDED",
                NextAction = "CONTINUE_EXISTING_REPOSITORY_WORKFLOW", Admission = admitted };
        }
    }
    private static string WorkflowStaging(string root, VerificationAdmissionRequest a) =>
        RepositoryPathSafety.ResolveRelativePath(root, $"staging/{a.CollectionAttemptId:D}/{a.PackageId:D}");
    private static byte[] BindVerification(ReadOnlyMemory<byte> bytes, CaptureAssignment assignment)
    {
        var value = JsonNode.Parse(bytes.Span)!;
        var check = value["checks"]!.AsArray().Single(c => c!["check"]!.GetValue<string>() == "PACKAGE_IDENTITY")!;
        check["evidence_references"]!.AsArray().Add(AssignmentReference(assignment));
        check["reason"] = "Package identity compared to immutable coordinator assignment; full session lifecycle is separate.";
        var result = CaptureCanonicalJson.Encode(JsonSerializer.SerializeToElement(value));
        _ = new MetadataSchemaValidator().Validate(MetadataKind.Verification, result); return result;
    }
    private static string AssignmentReference(CaptureAssignment assignment) =>
        $"coordinator-assignment:{assignment.AssignmentId:D}:sha256:{PackageInputManifest.Hash(WorkflowJson.Encode(assignment))}";
    private static void ValidateWorkflowRequest(BoundVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Admission);
        var a = request.Admission;
        Guid[] ids = [request.AssignmentId, a.CollectionAttemptId, a.PackageId, a.TransactionId, a.TransitionId, a.OperationId, a.RecordIndexEntryId, a.VerificationRecordId];
        PackageInputManifest.Need(request.SchemaVersion == "1.0.0" && Array.TrueForAll(ids, id => id != Guid.Empty)
            && a.AuditTime != default, "Invalid workflow request.");
    }
}

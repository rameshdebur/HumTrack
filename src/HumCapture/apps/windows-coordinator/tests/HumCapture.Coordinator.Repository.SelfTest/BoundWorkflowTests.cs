using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class BoundWorkflowTests
{
    private static readonly DateTimeOffset Audit = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private static void Need(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static void Refuse<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected refusal: " + typeof(T).Name);
    }
    private static BoundVerificationRequest Request(CaptureAssignment a, Guid package) => new(a.AssignmentId,
        new(Guid.NewGuid(), package, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Audit));
    private static CaptureAssignment Assignment(string source)
    {
        // Test-only source-derived fixture. Product callers must supply coordinator intent.
        var context = ScientificEvidenceTests.Context(source);
        using var protocol = JsonDocument.Parse(context.SnapshotBytes);
        var m = PackageInputTests.Manifest(source);
        Guid Id(string key) => Guid.Parse(m[key]!.GetValue<string>());
        return new(Guid.NewGuid(), Id("subject_id"), Id("session_id"), Id("trial_id"),
            protocol.RootElement.GetProperty("trial_slots")[0].GetProperty("slot_id").GetGuid(), Id("source_id"),
            protocol.RootElement.GetProperty("source_roles")[0].GetProperty("role_id").GetGuid(), Id("capture_attempt_id"),
            Id("source_boot_id"), m["source_kind"]!.GetValue<string>(), Id("configuration_id"),
            m["configuration_content_sha256"]!.GetValue<string>(), context.SnapshotBytes.ToArray());
    }
    internal static void With(string fixture, Action<string, string, RepositoryService, CaptureAssignment, BoundVerificationRequest> test)
    {
        PackageInputTests.WithFixture(source =>
        {
            var project = new DirectoryInfo(AppContext.BaseDirectory);
            while (project.Name != "HumCapture" || !File.Exists(Path.Combine(project.FullName, "AGENTS.md")))
            { project = project.Parent ?? throw new InvalidOperationException("Test scope absent."); }
            var container = Path.Combine(project.FullName, "evidence-vault", "workflow-tests", Guid.NewGuid().ToString("N"));
            var root = Path.Combine(container, "repository");
            Directory.CreateDirectory(root);
            try
            {
                var service = new RepositoryService(); _ = service.Initialize(root, "TEST\\operator");
                service.InitializeVerificationWorkflow(root);
                var assignment = Assignment(source);
                test(root, source, service, assignment, Request(assignment, Guid.Parse(PackageInputTests.Manifest(source)["package_id"]!.GetValue<string>())));
            }
            finally { Directory.Delete(container, true); }
        }, fixture);
    }
    private static VerificationWorkflowEvent Run(string root, string? source, RepositoryService service,
        BoundVerificationRequest request, string bin = "absent", Action<string>? boundary = null, CancellationToken token = default) =>
        service.CollectVerifyAssignedAsync(root, source, request, bin, TimeSpan.FromSeconds(30), token, boundary).GetAwaiter().GetResult();
    private static void Sql(string root, string sql)
    {
        using var database = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(root, VerificationWorkflowStore.RelativePath), Pooling = false }.ToString());
        database.Open(); using var command = database.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    internal static void ImmutableAssignments()
    {
        With("uvc-covered", (root, _, service, a, _) =>
        {
            service.RecordCaptureAssignment(root, a); service.RecordCaptureAssignment(root, a);
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a with { SubjectId = Guid.NewGuid() }));
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a with
            { AssignmentId = Guid.NewGuid(), CaptureAttemptId = Guid.NewGuid(), SubjectId = Guid.NewGuid() }));
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a with
            { AssignmentId = Guid.NewGuid(), CaptureAttemptId = Guid.NewGuid(), SourceId = Guid.NewGuid() }));
            Refuse<SqliteException>(() => service.RecordCaptureAssignment(root, a with { AssignmentId = Guid.NewGuid() }));
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a with { RoleId = Guid.NewGuid() }));
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a with { SlotId = Guid.NewGuid() }));
            Refuse<SqliteException>(() => Sql(root, "DELETE FROM capture_assignments"));
            service.InitializeVerificationWorkflow(root);
        });
    }
    internal static void BindingRefusals()
    {
        foreach (var field in new[] { "subject", "session", "trial", "source", "attempt", "boot", "config", "hash" })
        {
            With("uvc-covered", (root, source, service, a, request) =>
            {
                a = field switch
                {
                    "subject" => a with { SubjectId = Guid.NewGuid() }, "session" => a with { SessionId = Guid.NewGuid() },
                    "trial" => a with { TrialId = Guid.NewGuid() }, "source" => a with { SourceId = Guid.NewGuid() },
                    "attempt" => a with { CaptureAttemptId = Guid.NewGuid() }, "boot" => a with { SourceBootId = Guid.NewGuid() },
                    "config" => a with { ConfigurationId = Guid.NewGuid() }, _ => a with { ConfigurationSha256 = new string('0', 64) }
                };
                service.RecordCaptureAssignment(root, a);
                var result = Run(root, source, service, request);
                Need(result.State == "FAILED" && result.Admission is null && result.NextAction == "CORRECT_ASSIGNMENT_OR_SOURCE", "Mismatched assignment admitted: " + field);
                Need(service.VerificationHistory(root, request.Admission.VerificationRecordId).Count == 2, "Failure not retained.");
            });
        }
    }
    internal static void FailedHistory()
    {
        With("uvc-covered", (root, source, service, a, request) =>
        {
            service.RecordCaptureAssignment(root, a);
            var failed = Run(root, source, service, request);
            Need(failed.State == "FAILED" && failed.VerificationUtf8 is not null && failed.Admission is null, "Missing decoder failure discarded.");
            var replay = Run(root, source, new RepositoryService(), request);
            Need(replay.VerificationSha256 == failed.VerificationSha256, "Terminal replay changed record.");
            Need(service.VerificationHistory(root, request.Admission.VerificationRecordId, 1, 1).Single().State == "FAILED", "History pagination differs.");
            Refuse<InvalidDataException>(() => Run(root, source, service, request with { Admission = request.Admission with { AuditTime = Audit.AddSeconds(1) } }));
            Refuse<SqliteException>(() => Sql(root, "DELETE FROM verification_events"));
            var retry = Request(a, request.Admission.PackageId);
            Need(Run(root, source, service, retry).State == "FAILED", "Retry lost failure.");
            Need(service.VerificationHistory(root, request.Admission.VerificationRecordId).Count == 2
                && service.VerificationHistory(root, retry.Admission.VerificationRecordId).Count == 2, "Retry overwrote prior attempt.");
            Refuse<ArgumentOutOfRangeException>(() => service.VerificationHistory(root, retry.Admission.VerificationRecordId, 0, 101));
        });
    }
    internal static void InterruptedAndCancelled()
    {
        With("incomplete-survivors", (root, source, service, a, request) =>
        {
            service.RecordCaptureAssignment(root, a);
            Refuse<InvalidOperationException>(() => Run(root, source, service, request, boundary: _ => throw new InvalidOperationException("simulated exit")));
            Need(new RepositoryService().RecoverVerificationAttempt(root, request.Admission.VerificationRecordId).State == "INTERRUPTED", "STARTED inferred success.");
            using var cancelled = new CancellationTokenSource();
            var retry = Request(a, request.Admission.PackageId);
            var result = Run(root, source, service, retry, boundary: _ => cancelled.Cancel(), token: cancelled.Token);
            Need(result.State == "CANCELLED" && service.VerificationHistory(root, retry.Admission.VerificationRecordId).Count == 2, "Cancelled attempt not retained.");
            var next = Request(a, request.Admission.PackageId);
            Need(Run(root, source, service, next).State == "ADMITTED", "Survivor retry not admitted.");
        });
    }
    internal static void RecoveryBoundaries()
    {
        foreach (var boundary in new[] { "VERIFIED_READY", "CATALOG_ADMITTED" })
        {
            With("incomplete-survivors", (root, source, service, a, request) =>
            {
                service.RecordCaptureAssignment(root, a);
                var before = Directory.GetFiles(source, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => PackageInputManifest.Hash(File.ReadAllBytes(p)));
                Refuse<InvalidOperationException>(() => Run(root, source, service, request,
                    boundary: state => { if (state == boundary) { throw new InvalidOperationException("simulated exit"); } }));
                Need(service.VerificationHistory(root, request.Admission.VerificationRecordId)[^1].State == "VERIFIED_READY", "READY not durable.");
                var recovered = new RepositoryService().RecoverVerificationAttempt(root, request.Admission.VerificationRecordId);
                Need(recovered.State == "ADMITTED" && recovered.Admission?.State == "STAGED_VERIFIED", "Restart failed admission.");
                Need(service.RecoverVerificationAttempt(root, request.Admission.VerificationRecordId).VerificationSha256 == recovered.VerificationSha256
                    && service.VerificationHistory(root, request.Admission.VerificationRecordId).Count == 3, "Recovery appended duplicate event.");
                Need(before.All(p => PackageInputManifest.Hash(File.ReadAllBytes(p.Key)) == p.Value)
                    && !Directory.Exists(Path.Combine(root, recovered.Admission!.DestinationRelativePath)), "Source changed or moved.");
            });
        }
    }
    internal static void LedgerRefusals()
    {
        foreach (var mutation in new[] { "version", "repository", "hash" })
        {
            With("uvc-covered", (root, source, service, a, request) =>
            {
                service.RecordCaptureAssignment(root, a); _ = Run(root, source, service, request);
                var sql = mutation switch
                {
                    "version" => "PRAGMA user_version=2",
                    "repository" => "UPDATE workflow_metadata SET repository_id='wrong'",
                    _ => "DROP TRIGGER events_no_update; UPDATE verification_events SET sha256=printf('%064d',0)"
                };
                Sql(root, sql);
                Refuse<InvalidDataException>(() => service.RecoverVerificationAttempt(root, request.Admission.VerificationRecordId));
            });
        }
    }
    internal static void BusyAndExplicitInit()
    {
        With("uvc-covered", (root, _, service, a, _) =>
        {
            using (var guard = VerificationWorkflowStore.Lock(root))
            { Refuse<IOException>(() => service.RecordCaptureAssignment(root, a)); }
            service.RecordCaptureAssignment(root, a);
            File.Delete(Path.Combine(root, VerificationWorkflowStore.RelativePath));
            Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a));
            Need(!File.Exists(Path.Combine(root, VerificationWorkflowStore.RelativePath)), "Run silently initialized missing ledger.");
        });
    }
    internal static void ProtocolGuards()
    {
        foreach (var mutation in new[] { "count", "kind", "snapshot", "flexible" })
        {
            With("uvc-covered", (root, source, service, a, request) =>
            {
                var snapshot = JsonNode.Parse(a.ProtocolUtf8)!;
                if (mutation == "count") { snapshot["source_count_policy"]!["count"] = 2; }
                if (mutation == "kind") { snapshot["source_roles"]![0]!["allowed_source_kinds"] = new JsonArray("ANDROID_CAMERA2"); }
                if (mutation == "snapshot") { snapshot["protocol_snapshot_id"] = Guid.NewGuid().ToString("D"); }
                if (mutation == "flexible")
                { snapshot["source_count_policy"] = new JsonObject { ["mode"] = "FLEXIBLE", ["minimum"] = 1, ["maximum"] = 2 }; }
                var before = File.ReadAllBytes(Path.Combine(source, "package-manifest.json"));
                var context = ScientificEvidenceTests.Bind(source, snapshot);
                a = a with { ProtocolUtf8 = context.SnapshotBytes.ToArray() };
                if (mutation is "count" or "kind")
                { Refuse<InvalidDataException>(() => service.RecordCaptureAssignment(root, a)); return; }
                service.RecordCaptureAssignment(root, a);
                if (mutation == "snapshot")
                {
                    File.WriteAllBytes(Path.Combine(source, "package-manifest.json"), before);
                    Need(Run(root, source, service, request).State == "FAILED", "Wrong protocol snapshot admitted.");
                }
                else { Need(a.ProtocolUtf8.Length > 0, "Flexible one-source protocol was not retained."); }
            });
        }
    }
    internal static void ChangedRecoveryAndReadyCancellation()
    {
        With("incomplete-survivors", (root, source, service, a, request) =>
        {
            service.RecordCaptureAssignment(root, a);
            using var cancelled = new CancellationTokenSource();
            Refuse<OperationCanceledException>(() => Run(root, source, service, request, boundary: state =>
            { if (state == "VERIFIED_READY") { cancelled.Cancel(); } }, token: cancelled.Token));
            Need(service.VerificationHistory(root, request.Admission.VerificationRecordId)[^1].State == "VERIFIED_READY", "Ready evidence discarded on cancellation.");
            var staging = Path.Combine(root, "staging", request.Admission.CollectionAttemptId.ToString("D"), request.Admission.PackageId.ToString("D"));
            var artifact = Directory.GetFiles(staging, "*", SearchOption.AllDirectories).First(p => !p.EndsWith("manifest.json", StringComparison.Ordinal));
            var saved = File.ReadAllBytes(artifact); File.WriteAllBytes(artifact, [1, 2, 3]);
            Refuse<InvalidDataException>(() => new RepositoryService().RecoverVerificationAttempt(root, request.Admission.VerificationRecordId));
            Need(service.VerificationHistory(root, request.Admission.VerificationRecordId).Count == 2, "Changed package silently admitted.");
            File.WriteAllBytes(artifact, saved);
            Need(service.RecoverVerificationAttempt(root, request.Admission.VerificationRecordId).State == "ADMITTED", "Exact restored bytes cannot recover.");
        });
    }
    internal static int Real(string bin, string media)
    {
        With("uvc-covered", (root, source, service, a, request) =>
        {
            File.Copy(Path.Combine(media, "master3.mp4"), Path.Combine(source, "media/master.mp4"), true);
            PackageInputTests.Rebind(source);
            service.RecordCaptureAssignment(root, a);
            var result = Run(root, source, service, request, bin);
            Need(result.State == "ADMITTED" && result.Admission?.State == "STAGED_VERIFIED", "Bound real decoder workflow failed: " + result.ReasonCode);
            Need(service.VerificationHistory(root, request.Admission.VerificationRecordId).Count == 3, "Bound workflow history incomplete.");
            Need(Directory.Exists(source) && !Directory.Exists(Path.Combine(root, result.Admission!.DestinationRelativePath)), "Bound admission moved source.");
            Console.WriteLine("PASS HC-C14XYZ-REAL collection, assignment, pinned decode, retained record, staged admission; no move or cleanup");
        });
        return 0;
    }
}

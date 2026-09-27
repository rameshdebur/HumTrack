using System.Diagnostics;
using System.Text.Json;
using HumCapture.Coordinator.Repository;
using CoordinatorHost = HumCapture.Coordinator.Host.Program;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class MaintenanceTests
{
    private static void Need(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static void Refuse<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static string Beside(string root, string name) => Path.Combine(Path.GetDirectoryName(root)!, name);
    private static Process Start(string assembly, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(assembly); foreach (var arg in args) { info.ArgumentList.Add(arg); }
        return Process.Start(info) ?? throw new InvalidOperationException("Child absent.");
    }
    private static (int Code, string Output) Run(params string[] args)
    {
        using var child = Start(typeof(CoordinatorHost).Assembly.Location, args);
        var output = child.StandardOutput.ReadToEndAsync(); var errors = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit(60000)) { child.Kill(true); child.WaitForExit(); throw new InvalidOperationException("Host deadline exceeded."); }
        Need(errors.GetAwaiter().GetResult() == "", "Host leaked unstructured error.");
        return (child.ExitCode, output.GetAwaiter().GetResult());
    }
    private static (int Code, string Output) Command(string command, string root, params string[] options) =>
        Run([command, "--root", root, "--engineering", "true", .. options]);
    private static string Request<T>(string root, string name, T value)
    { var path = Beside(root, name); File.WriteAllBytes(path, WorkflowJson.Encode(value)); return path; }
    internal static void HostRoundTrip()
    {
        BoundWorkflowTests.With("incomplete-survivors", (root, source, service, assignment, request) =>
        {
            var a = Request(root, "assignment.json", assignment); var r = Request(root, "request.json", request);
            Need(Command("workflow-init", root).Code == 0 && Command("workflow-assign", root, "--request", a).Code == 0, "Host init/assignment failed.");
            var run = Command("workflow-run", root, "--request", r, "--source", source, "--decoder", Beside(root, "unused"));
            Need(run.Code == 0 && run.Output.Contains("STAGED_VERIFIED", StringComparison.Ordinal), "Host bound admission failed: " + run.Output);
            Need(Command("workflow-history", root, "--attempt", request.Admission.VerificationRecordId.ToString("D")).Output.Contains("HISTORICAL_ONLY", StringComparison.Ordinal), "Missing history.");
            Need(Command("workflow-recover", root, "--attempt", request.Admission.VerificationRecordId.ToString("D")).Code == 0, "Host recovery failed.");
            Need(Run("process-staged", "--root", root).Code == 0 && Run("startup", "--root", root).Code == 0, "Existing commit pipeline failed.");
            var backup = Beside(root, "backup"); var restored = Beside(root, "restored");
            Need(Command("repository-backup", root, "--destination", backup).Code == 0, "Host backup failed.");
            Need(Command("repository-restore", restored, "--source", backup).Code == 0, "Host restore failed.");
            Need(service.Open(root).Descriptor.RepositoryId == service.Open(restored).Descriptor.RepositoryId
                && service.VerificationHistory(restored, request.Admission.VerificationRecordId).Count == 3, "Restore lost identity/history.");
            Need(Run("startup", "--root", restored).Output.Contains("COMMITTED", StringComparison.Ordinal), "Restored committed package did not reconcile.");
            Need(Command("repository-restore", restored, "--source", backup).Code == 3, "Restore overwrote existing root.");
            Need(Run("workflow-init", "--root", root).Code == 2, "Engineering opt-in bypassed.");
            Need(Command("workflow-init", root, "--extra", "x").Code == 2, "Unknown option accepted.");
            using var guard = new Mutex(true, CoordinatorHost.StartupMutexName(root));
            try { Need(Command("repository-backup", root, "--destination", Beside(root, "busy")).Code == 7, "Busy host accepted backup."); }
            finally { guard.ReleaseMutex(); }
        });
    }
    internal static void BackupRefusals()
    {
        BoundWorkflowTests.With("uvc-covered", (root, source, service, assignment, request) =>
        {
            service.RecordCaptureAssignment(root, assignment);
            _ = service.CollectVerifyAssignedAsync(root, source, request, "absent", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var backup = Beside(root, "backup"); _ = RepositoryBackup.Create(root, backup);
            var restored = Beside(root, "restored"); _ = RepositoryBackup.Restore(backup, restored);
            Need(service.VerificationHistory(restored, request.Admission.VerificationRecordId)[^1].State == "FAILED", "Backup promoted failure.");
            Refuse<InvalidDataException>(() => RepositoryBackup.Create(root, Path.Combine(root, "nested")));
            using (var writer = new FileStream(Path.Combine(root, "repository.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            { Refuse<IOException>(() => RepositoryBackup.Create(root, Beside(root, "writer"))); }
            var journal = Path.Combine(root, "catalog", "humcapture.sqlite3-journal"); File.WriteAllBytes(journal, [1]);
            Refuse<InvalidDataException>(() => RepositoryBackup.Create(root, Beside(root, "journal"))); File.Delete(journal);
            var artifact = Directory.GetFiles(Path.Combine(backup, "payload", "staging"), "*", SearchOption.AllDirectories)[0];
            File.AppendAllText(artifact, "changed");
            Refuse<InvalidDataException>(() => RepositoryBackup.Restore(backup, Beside(root, "tampered")));
            Need(!Directory.Exists(Beside(root, "tampered")), "Invalid backup published a restore.");
        });
    }
    internal static int Child(string[] args)
    {
        var mode = args[1]; var root = args[2]; var source = args[3]; var request = args[4]; var stop = args[5];
        using var guard = new Mutex(true, CoordinatorHost.StartupMutexName(root));
        void Boundary(string state)
        {
            if (state != stop) { return; }
            Console.WriteLine("CHECKPOINT:" + state); Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
        }
        try
        {
        if (mode == "backup") { _ = RepositoryBackup.Create(root, request, testBoundary: Boundary); }
        else if (mode == "restore") { _ = RepositoryBackup.Restore(source, request, testBoundary: Boundary); }
        else
        {
            _ = new RepositoryService().CollectVerifyAssignedAsync(root, source,
                WorkflowJson.ReadFile<BoundVerificationRequest>(request), "unused", TimeSpan.FromSeconds(5), testBoundary: Boundary).GetAwaiter().GetResult();
        }
        return 0;
        }
        finally { guard.ReleaseMutex(); }
    }
    internal static void MalformedAndCancelled()
    {
        BoundWorkflowTests.With("incomplete-survivors", (root, source, service, assignment, request) =>
        {
            Need(Command("workflow-assign", root, "--request", Request(root, "null-assignment.json", assignment with { ProtocolUtf8 = null! })).Code == 3,
                "Null assignment caused unstructured failure.");
            Need(Command("workflow-run", root, "--request", Request(root, "null-request.json", request with { Admission = null! }),
                "--source", source, "--decoder", Beside(root, "unused")).Code == 3, "Null request caused unstructured failure.");
            var fresh = Beside(root, "fresh");
            Need(Command("repository-init", fresh).Code == 0 && Command("workflow-init", fresh).Code == 0, "Engineering initialization failed.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Refuse<OperationCanceledException>(() => RepositoryBackup.Create(root, Beside(root, "cancelled"), cancelled.Token));
            Need(!Directory.Exists(Beside(root, "cancelled")), "Cancelled backup published completion.");
            var backup = Beside(root, "backup"); var manifest = RepositoryBackup.Create(root, backup);
            File.WriteAllBytes(Path.Combine(backup, "backup-manifest.json"), WorkflowJson.Encode(manifest with { Files = null! }));
            Need(Command("repository-restore", Beside(root, "null-restore"), "--source", backup).Code == 3, "Null backup caused unstructured failure.");
            service.RecordCaptureAssignment(root, assignment);
        });
    }
    private static void KillAt(string mode, string root, string source, string request, string boundary)
    {
        using var child = Start(typeof(MaintenanceTests).Assembly.Location, "--maintenance-child", mode, root, source, request, boundary);
        var errors = child.StandardError.ReadToEndAsync();
        try
        {
            var line = child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            Need(line == "CHECKPOINT:" + boundary, "Child failed before checkpoint: " + line);
            child.Kill(true); Need(child.WaitForExit(10000), "Killed process did not exit.");
            Need(errors.GetAwaiter().GetResult() == "", "Child execution failed.");
        }
        finally { if (!child.HasExited) { child.Kill(true); child.WaitForExit(); } }
    }
    internal static void KilledWorkflow()
    {
        foreach (var boundary in new[] { "STARTED", "VERIFIED_READY", "CATALOG_ADMITTED" })
        {
            BoundWorkflowTests.With("incomplete-survivors", (root, source, service, assignment, request) =>
            {
                service.RecordCaptureAssignment(root, assignment);
                var file = Request(root, "request.json", request);
                KillAt("workflow", root, source, file, boundary);
                var result = Command("workflow-recover", root, "--attempt", request.Admission.VerificationRecordId.ToString("D"));
                Need(result.Code == (boundary == "STARTED" ? 4 : 0), "Killed workflow recovery differs: " + result.Output);
                Need(service.VerificationHistory(root, request.Admission.VerificationRecordId)[^1].State
                    == (boundary == "STARTED" ? "INTERRUPTED" : "ADMITTED"), "Killed process inferred incorrect state.");
            });
        }
    }
    internal static void KilledBackupRestore()
    {
        BoundWorkflowTests.With("incomplete-survivors", (root, source, service, assignment, request) =>
        {
            service.RecordCaptureAssignment(root, assignment);
            _ = service.CollectVerifyAssignedAsync(root, source, request, "unused", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var backup = Beside(root, "backup");
            KillAt("backup", root, source, backup, "FILE_COPIED");
            Need(!Directory.Exists(backup), "Killed backup published completion.");
            var partial = Directory.GetDirectories(Path.GetDirectoryName(root)!, "backup.partial-*").Single();
            Refuse<InvalidDataException>(() => RepositoryBackup.Restore(partial, Beside(root, "invalid")));
            Need(Command("repository-backup", root, "--destination", backup).Code == 0, "Backup retry failed.");
            var target = Beside(root, "restored"); KillAt("restore", root, backup, target, "RESTORE_READY");
            Need(!Directory.Exists(target), "Killed restore published target.");
            Need(Command("repository-restore", target, "--source", backup).Code == 0, "Restore retry failed.");
            Need(service.VerificationHistory(target, request.Admission.VerificationRecordId)[^1].State == "ADMITTED", "Restarted restore lost history.");
        });
    }
    internal static int Real(string binaries, string media)
    {
        BoundWorkflowTests.With("uvc-covered", (root, source, _, assignment, request) =>
        {
            File.Copy(Path.Combine(media, "master3.mp4"), Path.Combine(source, "media/master.mp4"), true); PackageInputTests.Rebind(source);
            Need(Command("workflow-assign", root, "--request", Request(root, "assignment.json", assignment)).Code == 0, "Real host assignment failed.");
            var run = Command("workflow-run", root, "--request", Request(root, "request.json", request), "--source", source, "--decoder", binaries);
            Need(run.Code == 0, "Real host verification failed: " + run.Output);
            Need(Run("process-staged", "--root", root).Code == 0 && Run("startup", "--root", root).Code == 0, "Real package commit failed.");
            var backup = Beside(root, "backup"); var restored = Beside(root, "restore");
            Need(Command("repository-backup", root, "--destination", backup).Code == 0
                && Command("repository-restore", restored, "--source", backup).Code == 0, "Real host backup/restore failed.");
            Need(Run("startup", "--root", restored).Output.Contains("COMMITTED", StringComparison.Ordinal), "Restored real package did not reconcile.");
            Console.WriteLine("PASS HC-C15-REAL actual host: assignment, collection, pinned decoder, admission, existing commit, full backup, isolated restore and committed reconciliation");
        });
        return 0;
    }
}

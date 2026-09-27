using System.Security.Principal;
using System.Text.Json;
using HumCapture.Coordinator.Repository;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Host;

public static partial class Program
{
    private static bool IsEngineeringCommand(string? value) => value is "repository-init" or "workflow-init" or "workflow-assign"
        or "workflow-run" or "workflow-history" or "workflow-recover" or "repository-backup" or "repository-restore";
    private static int RunEngineering(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 1) { WriteError("INVALID_ARGUMENTS"); return 2; }
        for (var i = 1; i < args.Length; i += 2)
        { if (!options.TryAdd(args[i], args[i + 1])) { WriteError("INVALID_ARGUMENTS"); return 2; } }
        string[] extra = args[0] switch
        {
            "workflow-assign" => ["--request"], "workflow-run" => ["--request", "--source", "--decoder"],
            "workflow-history" or "workflow-recover" => ["--attempt"], "repository-backup" => ["--destination"],
            "repository-restore" => ["--source"], _ => []
        };
        var expected = extra.Concat(["--root", "--engineering"]).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(options.Keys) || options["--engineering"] != "true"
            || options.Any(p => p.Key is not ("--engineering" or "--attempt") && !Path.IsPathFullyQualified(p.Value))
            || options.TryGetValue("--attempt", out var attempt) && (!Guid.TryParseExact(attempt, "D", out var parsed) || parsed == Guid.Empty))
        { WriteError("INVALID_ARGUMENTS"); return 2; }
        var root = options["--root"];
        try
        {
            using var guard = new Mutex(false, StartupMutexName(root)); var acquired = false;
            try
            {
                try { acquired = guard.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { WriteError("STARTUP_ALREADY_RUNNING"); return 7; }
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
                Console.CancelKeyPress += handler;
                try { return EngineeringCommand(args[0], root, options, cancellation.Token); }
                finally { Console.CancelKeyPress -= handler; }
            }
            finally { if (acquired) { guard.ReleaseMutex(); } }
        }
        catch (OperationCanceledException) { WriteError("ENGINEERING_CANCELLED"); return 130; }
        catch (RepositoryException exception) { WriteError(exception.Code.ToString()); return 3; }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or JsonException or FormatException or OverflowException
            or KeyNotFoundException or SqliteException or System.Security.SecurityException)
        { WriteError("ENGINEERING_REFUSED"); return 3; }
    }
    private static int EngineeringCommand(string command, string root, Dictionary<string, string> o, CancellationToken token)
    {
        var service = new RepositoryService();
        switch (command)
        {
            case "repository-init":
                using (var actor = WindowsIdentity.GetCurrent()) { _ = service.Initialize(root, actor.Name); }
                break;
            case "workflow-init": service.InitializeVerificationWorkflow(root); break;
            case "workflow-assign": service.RecordCaptureAssignment(root, WorkflowJson.ReadFile<CaptureAssignment>(o["--request"])); break;
            case "workflow-run":
                return WriteWorkflow(service.CollectVerifyAssignedAsync(root, o["--source"],
                    WorkflowJson.ReadFile<BoundVerificationRequest>(o["--request"]), o["--decoder"], TimeSpan.FromMinutes(5), token).GetAwaiter().GetResult());
            case "workflow-recover": return WriteWorkflow(service.RecoverVerificationAttempt(root, Guid.Parse(o["--attempt"]), token));
            case "workflow-history":
                Console.WriteLine(JsonSerializer.Serialize(new { schema_version = "1.0.0", status = "HISTORICAL_ONLY",
                    items = service.VerificationHistory(root, Guid.Parse(o["--attempt"])).Select(WorkflowProjection) })); return 0;
            case "repository-backup":
                _ = RepositoryBackup.Create(root, o["--destination"], token); break;
            case "repository-restore":
                _ = RepositoryBackup.Restore(o["--source"], root, token); break;
            default: throw new InvalidOperationException("Unknown engineering command.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { schema_version = "1.0.0", status = "COMPLETED", operation = command })); return 0;
    }
    private static object WorkflowProjection(VerificationWorkflowEvent value) => new
    { state = value.State, reason_code = value.ReasonCode, next_action = value.NextAction, verification_sha256 = value.VerificationSha256,
        transaction_id = value.Admission?.TransactionId, repository_state = value.Admission?.State };
    private static int WriteWorkflow(VerificationWorkflowEvent value)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { schema_version = "1.0.0", result = WorkflowProjection(value) }));
        if (value.State == "CANCELLED") { return 130; }
        return value.State == "ADMITTED" ? 0 : 4;
    }
}

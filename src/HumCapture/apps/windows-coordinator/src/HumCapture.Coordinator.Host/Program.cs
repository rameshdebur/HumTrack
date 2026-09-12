using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Host;

/// <summary>Bounded command-line Coordinator recovery and staged-processing host.</summary>
public static class Program
{
    /// <summary>Runs a single explicitly requested repository pass.</summary>
    public static int Main(string[] args)
    {
        if (!TryParse(args, out var root, out var limit, out var after, out var requestPath))
        {
            WriteError("INVALID_ARGUMENTS");
            return 2;
        }
        try
        {
            using var mutex = new Mutex(false, StartupMutexName(root));
            var acquired = false;
            try
            {
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired)
                {
                    WriteError("STARTUP_ALREADY_RUNNING");
                    return 7;
                }
                return args[0] == "admit-staged"
                    ? RunAdmission(root, requestPath!)
                    : Run(root, limit, after, args[0] == "process-staged");
            }
            finally
            {
                if (acquired) { mutex.ReleaseMutex(); }
            }
        }
        catch (RepositoryException exception)
        {
            WriteError(exception.Code.ToString());
            return 3;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or System.Security.SecurityException)
        {
            WriteError("STARTUP_FAILED");
            return 3;
        }
    }

    /// <summary>Names the cooperating-host guard; not a global repository lock.</summary>
    public static string StartupMutexName(string root)
    {
        var canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)).ToUpperInvariant();
        return "Local\\HumCapture.Startup." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static int Run(string root, int limit, Guid? after, bool processStaged)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var service = new RepositoryService();
            var pass = processStaged
                ? service.ProcessStagedPass(root, after, limit, cancellation.Token)
                : service.RunStartupPass(root, after, limit, cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema_version = "1.2.0", status = pass.Status.ToString(),
                last_processed_transaction_id = pass.LastProcessedTransactionId,
                requires_operator_attention = pass.RequiresOperatorAttention,
                items = pass.Items.Select(item => new
                {
                    transaction_id = item.TransactionId,
                    state = item.NormalMove?.State ?? item.Reconciliation?.ResultState,
                    action = ItemAction(item),
                    error_code = item.ErrorCode?.ToString(), next_action = item.NextAction
                })
            }));
            if (pass.Status == RepositoryStartupPassStatus.Cancelled) { return 130; }
            if (pass.RequiresOperatorAttention) { return 4; }
            return pass.Status switch
            {
                RepositoryStartupPassStatus.Completed => 0,
                RepositoryStartupPassStatus.MoreWork => 5,
                RepositoryStartupPassStatus.ReadOnlyInspection => 6,
                _ => 3
            };
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static void WriteError(string code) =>
        Console.WriteLine(JsonSerializer.Serialize(new { schema_version = "1.2.0", status = "Failed", error_code = code }));

    private static int RunAdmission(string root, string requestPath)
    {
        ConsoleCancelEventHandler handler = (_, e) => e.Cancel = true;
        Console.CancelKeyPress += handler;
        try
        {
            var admitted = new RepositoryService().AdmitStagedRequest(root, requestPath);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema_version = "1.2.0", status = "Admitted",
                transaction_id = admitted.TransactionId, state = admitted.State,
                revision = admitted.Revision, was_already_present = admitted.WasAlreadyPresent
            }));
            return 0;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static string? ItemAction(RepositoryStartupItem item)
    {
        if (item.WasSkipped) { return "SKIP_NOT_STAGED"; }
        return item.NormalMove is not null ? "MOVE_STAGED_PACKAGE" : item.Reconciliation?.ActionCode;
    }

    private static bool TryParse(string[] args, out string root, out int limit, out Guid? after, out string? requestPath)
    {
        root = string.Empty;
        limit = 100;
        after = null;
        requestPath = null;
        if (args.Length < 3 || args[0] is not ("startup" or "process-staged" or "admit-staged") || args.Length % 2 != 1) { return false; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var key = args[index];
            var value = args[index + 1];
            if (!seen.Add(key)) { return false; }
            switch (key)
            {
                case "--root": root = value; break;
                case "--request": requestPath = value; break;
                case "--limit":
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit)
                        || limit is < 1 or > 1000) { return false; }
                    break;
                case "--after":
                    if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty) { return false; }
                    after = id;
                    break;
                default: return false;
            }
        }
        var admission = args[0] == "admit-staged";
        if (admission && (requestPath is null || !Path.IsPathFullyQualified(requestPath)
            || seen.Contains("--limit") || seen.Contains("--after"))) { return false; }
        if (!admission && requestPath is not null) { return false; }
        return !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root);
    }
}

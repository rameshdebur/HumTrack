using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Host;

/// <summary>Bounded command-line Coordinator startup process.</summary>
public static class Program
{
    /// <summary>Runs a single explicitly requested startup pass.</summary>
    public static int Main(string[] args)
    {
        if (!TryParse(args, out var root, out var limit, out var after))
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
                return Run(root, limit, after);
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

    private static int Run(string root, int limit, Guid? after)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var pass = new RepositoryService().RunStartupPass(root, after, limit, cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema_version = "1.0.0", status = pass.Status.ToString(),
                last_processed_transaction_id = pass.LastProcessedTransactionId,
                requires_operator_attention = pass.RequiresOperatorAttention,
                items = pass.Items.Select(item => new
                {
                    transaction_id = item.TransactionId,
                    state = item.Reconciliation?.ResultState,
                    action = item.Reconciliation?.ActionCode,
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
        Console.WriteLine(JsonSerializer.Serialize(new { schema_version = "1.0.0", status = "Failed", error_code = code }));

    private static bool TryParse(string[] args, out string root, out int limit, out Guid? after)
    {
        root = string.Empty;
        limit = 100;
        after = null;
        if (args.Length < 3 || args[0] != "startup" || args.Length % 2 != 1) { return false; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var key = args[index];
            var value = args[index + 1];
            if (!seen.Add(key)) { return false; }
            switch (key)
            {
                case "--root": root = value; break;
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
        return !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root);
    }
}

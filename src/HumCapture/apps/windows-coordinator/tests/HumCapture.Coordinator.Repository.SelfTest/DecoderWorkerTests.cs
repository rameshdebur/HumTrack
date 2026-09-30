using System.Diagnostics;
using System.Security.Cryptography;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class DecoderWorkerTests
{
    internal static async Task<int> ChildAsync(string mode)
    {
        switch (mode)
        {
            case "success": await Console.Out.WriteAsync("decoded"); return 0;
            case "failure": return 7;
            case "diagnostic": await Console.Error.WriteAsync("corrupt"); return 0;
            case "wait":
                await Console.Out.WriteLineAsync(Environment.ProcessId.ToString());
                await Console.Out.FlushAsync();
                await Task.Delay(TimeSpan.FromSeconds(30)); return 0;
            case "flood":
                await Task.WhenAll(Console.Out.WriteAsync(new string('o', 262144)), Console.Error.WriteAsync(new string('e', 262144)));
                return 0;
            default: return 8;
        }
    }

    private static Task<DecoderProcessResult> Run(string mode, TimeSpan? timeout = null, int cap = 1048576, CancellationToken token = default)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing self-test executable.");
        var arguments = new List<string>();
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add(typeof(DecoderWorkerTests).Assembly.Location);
        }

        arguments.AddRange(["--decoder-child", mode]);
        return DecoderProcess.RunAsync(executable, arguments, timeout ?? TimeSpan.FromSeconds(10), cap, token);
    }

    private static void Require(bool value, string message)
    { if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void ExitSemantics()
    {
        var success = Run("success").GetAwaiter().GetResult();
        Require(success.Outcome == DecoderExit.Decoded && success.Output == "decoded", "Clean subprocess failed.");
        Require(Run("failure").GetAwaiter().GetResult().Outcome == DecoderExit.Failed, "Nonzero exit passed.");
        Require(Run("diagnostic").GetAwaiter().GetResult().Outcome == DecoderExit.Failed, "Stderr passed.");
    }

    internal static void PipeBounds()
    {
        var drained = Run("flood").GetAwaiter().GetResult();
        Require(drained.Outcome == DecoderExit.Failed && drained.ExitCode == 0
            && drained.Output.Length == 262144 && drained.Error.Length == 262144, "Dual pipe drain deadlocked/lost bytes.");
        var bounded = Run("flood", cap: 1024).GetAwaiter().GetResult();
        Require(bounded.Outcome == DecoderExit.OutputLimit && bounded.Output.Length + bounded.Error.Length <= 1024, "Output cap failed.");
    }

    internal static void Timeout()
    {
        var result = Run("wait", TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        Require(result.Outcome == DecoderExit.TimedOut, "Timeout passed or misclassified.");
        RequireExited(result);
    }

    internal static void Cancellation()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var before = Run("wait", token: cancelled.Token).GetAwaiter().GetResult();
        Require(before.Outcome == DecoderExit.Cancelled && before.ExitCode is null && before.Output.Length == 0, "Pre-cancellation spawned a process.");
        using var during = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = Run("wait", token: during.Token).GetAwaiter().GetResult();
        Require(result.Outcome == DecoderExit.Cancelled, "Cancellation misclassified.");
        RequireExited(result);
    }

    private static void RequireExited(DecoderProcessResult result)
    {
        Require(int.TryParse(result.Output.Trim(), out var pid), "Child did not reach wait state; cannot attest in-flight termination.");
        try { using var process = Process.GetProcessById(pid); Require(process.HasExited, "Child still running."); }
        catch (ArgumentException) { /* Reaped process no longer exists. */ }
    }

    internal static void MissingExecutable()
    {
        var result = DecoderProcess.RunAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), [],
            TimeSpan.FromSeconds(1), 1024).GetAwaiter().GetResult();
        Require(result.Outcome == DecoderExit.StartFailed, "Missing executable passed.");
    }

    internal static void PinAndPathRefusal()
    {
        var root = Path.Combine(Path.GetTempPath(), "hc-decoder-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var fake = Path.Combine(root, "ffmpeg.exe");
            var media = Path.Combine(root, "input.mp4");
            File.WriteAllText(fake, "not executable"); File.WriteAllText(media, "synthetic");
            var result = PinnedDecoderWorker.DecodeAsync(root, media, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Outcome == DecoderExit.Failed && result.Error.Contains("identity", StringComparison.Ordinal), "Bad pin accepted.");
            using (var lease = File.Open(fake, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { Require(lease.CanWrite, "Executable lease leaked."); }
            using (var lease = File.Open(media, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { Require(lease.CanWrite, "Input lease leaked."); }
            Require(PinnedDecoderWorker.DecodeAsync("relative", media, TimeSpan.FromSeconds(1)).GetAwaiter().GetResult().Outcome == DecoderExit.Failed,
                "Relative binary path accepted.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    internal static void ArgumentsPreserveCadence()
    {
        var mediaPath = Path.Combine(Path.GetTempPath(), "synthetic path", "video.mp4");
        var arguments = PinnedDecoderWorker.Arguments(mediaPath);
        Require(arguments[Array.IndexOf(arguments, "-i") + 1] == mediaPath, "Path was split.");
        Require(arguments[Array.IndexOf(arguments, "-fps_mode") + 1] == "passthrough" && !arguments.Contains("-r"), "Cadence rewritten.");
        Require(arguments[Array.IndexOf(arguments, "-hwaccel") + 1] == "none", "Hardware decoder enabled.");
    }

    internal static async Task<int> RealAsync(string bin, string mediaDirectory)
    {
        foreach (var (name, expected) in new[] { ("synthetic.mp4", DecoderExit.Decoded),
            ("synthetic-variable.mp4", DecoderExit.Decoded), ("truncated.mp4", DecoderExit.Failed), ("corrupt.mp4", DecoderExit.Failed) })
        {
            var media = Path.Combine(mediaDirectory, name);
            var before = SHA256.HashData(await File.ReadAllBytesAsync(media));
            var result = await PinnedDecoderWorker.DecodeAsync(bin, media, TimeSpan.FromSeconds(30));
            var after = SHA256.HashData(await File.ReadAllBytesAsync(media));
            Require(result.Outcome == expected && before.SequenceEqual(after), $"Unexpected real decode result: {name}, {result.Outcome}, {result.Error}");
            await Console.Out.WriteLineAsync($"PASS HC-DEC-WORKER-REAL {name} outcome={result.Outcome} exit={result.ExitCode} unchanged=true");
        }
        return 0;
    }
}

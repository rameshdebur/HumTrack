using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace HumCapture.Coordinator.Repository;

internal enum DecoderExit { Decoded, Failed, Cancelled, TimedOut, OutputLimit, StartFailed, CleanupFailed }

internal sealed record DecoderProcessResult(DecoderExit Outcome, int? ExitCode, string Output, string Error);

// Internal until the complete package-verification and deployment gates are satisfied.
internal static class DecoderProcess
{
    internal static async Task<DecoderProcessResult> RunAsync(string executable, IEnumerable<string> arguments,
        TimeSpan timeout, int outputLimit, CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(executable))
        {
            throw new ArgumentException("Absolute executable required.", nameof(executable));
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (outputLimit < 1 || outputLimit > 16 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(outputLimit));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new(DecoderExit.Cancelled, null, "", "");
        }

        using var deadline = new CancellationTokenSource(timeout);
        using var overflow = new CancellationTokenSource();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token, overflow.Token);
        using var process = new Process { StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true
        }};
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.StartInfo.Environment.Remove("FFREPORT");

        try { process.Start(); }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        { return new(DecoderExit.StartFailed, null, "", exception.GetType().Name); }
        process.StandardInput.Close();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        var total = 0;
        async Task DrainAsync(Stream stream, MemoryStream retained)
        {
            var buffer = new byte[4096];
            while (true)
            {
                var count = await stream.ReadAsync(buffer, stop.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    return;
                }

                var after = Interlocked.Add(ref total, count);
                var allowed = Math.Clamp(outputLimit - (after - count), 0, count);
                await retained.WriteAsync(buffer.AsMemory(0, allowed), CancellationToken.None).ConfigureAwait(false);
                if (after > outputLimit) { await overflow.CancelAsync().ConfigureAwait(false); return; }
            }
        }
        var outputTask = DrainAsync(process.StandardOutput.BaseStream, stdout);
        var errorTask = DrainAsync(process.StandardError.BaseStream, stderr);
        var completion = Task.WhenAll(process.WaitForExitAsync(stop.Token), outputTask, errorTask);
        var outcome = DecoderExit.Failed;
        try
        {
            await completion.ConfigureAwait(false);
            outcome = process.ExitCode == 0 && stderr.Length == 0 ? DecoderExit.Decoded : DecoderExit.Failed;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        { outcome = DecoderExit.Failed; }
        if (stop.IsCancellationRequested)
        {
            outcome = overflow.IsCancellationRequested ? DecoderExit.OutputLimit : DecoderExit.TimedOut;
            if (cancellationToken.IsCancellationRequested) { outcome = DecoderExit.Cancelled; }
        }
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or AggregateException or TimeoutException)
        { outcome = DecoderExit.CleanupFailed; }
        // Drains have finished (or observed cancellation) before retained buffers are read/disposed.
        return new(outcome, process.HasExited ? process.ExitCode : null,
            Encoding.UTF8.GetString(stdout.ToArray()), Encoding.UTF8.GetString(stderr.ToArray()));
    }
}

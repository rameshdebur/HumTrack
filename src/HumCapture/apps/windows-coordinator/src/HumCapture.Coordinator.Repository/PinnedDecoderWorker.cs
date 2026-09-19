using System.Security.Cryptography;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal static partial class PinnedDecoderWorker
{
    private static readonly SemaphoreSlim SingleWorker = new(1, 1);
    private static bool cleanupBlocked;

    internal static async Task<DecoderProcessResult> DecodeAsync(string binaryDirectory, string mediaPath,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var deadline = new CancellationTokenSource(timeout);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var entered = false;
        try
        {
            await SingleWorker.WaitAsync(stop.Token).ConfigureAwait(false);
            entered = true;
            if (cleanupBlocked)
            {
                return new(DecoderExit.CleanupFailed, null, "", "Previous process cleanup was not confirmed; restart required.");
            }
            var executable = Path.Combine(binaryDirectory, "ffmpeg.exe");
            using var executableLease = OpenOrdinaryFile(executable);
            using var inputLease = OpenOrdinaryFile(mediaPath);
            if (!string.Equals(Path.GetExtension(mediaPath), ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return new(DecoderExit.Failed, null, "", "Unsupported media profile.");
            }

            using var resource = typeof(PinnedDecoderWorker).Assembly.GetManifestResourceStream("HumCapture.Decoder.lock.json")
                ?? throw new InvalidOperationException("Missing embedded decoder identity.");
            using var manifest = await JsonDocument.ParseAsync(resource, cancellationToken: stop.Token).ConfigureAwait(false);
            var expected = manifest.RootElement.GetProperty("executable_sha256").GetProperty("ffmpeg.exe").GetString();
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(executableLease, stop.Token).ConfigureAwait(false));
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                return new(DecoderExit.Failed, null, "", "Decoder identity mismatch.");
            }

            var result = await DecoderProcess.RunAsync(executable, Arguments(mediaPath), timeout, 1024 * 1024, stop.Token).ConfigureAwait(false);
            cleanupBlocked = result.Outcome == DecoderExit.CleanupFailed;
            return result.Outcome == DecoderExit.Cancelled && !cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested
                ? result with { Outcome = DecoderExit.TimedOut } : result;
        }
        catch (OperationCanceledException)
        { return new(cancellationToken.IsCancellationRequested ? DecoderExit.Cancelled : DecoderExit.TimedOut, null, "", ""); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(DecoderExit.Failed, null, "", exception.GetType().Name); }
        finally { if (entered)
            {
                SingleWorker.Release();
            }
        }
    }

    internal static string[] Arguments(string mediaPath) =>
    ["-hide_banner", "-nostdin", "-v", "error", "-xerror", "-abort_on", "empty_output",
        "-hwaccel", "none", "-threads", "1", "-protocol_whitelist", "file", "-f", "mov",
        "-i", mediaPath, "-map", "0:v:0", "-an", "-sn", "-dn", "-fps_mode", "passthrough", "-f", "null", "NUL"];

    private static FileStream OpenOrdinaryFile(string file)
    {
        if (!Path.IsPathFullyQualified(file) || file.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Ordinary absolute local path required.", nameof(file));
        }

        for (var current = Path.GetFullPath(file); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Reparse paths are not supported.");
            }
        }

        return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }
}

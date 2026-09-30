using System.Security.Cryptography;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal static partial class PinnedDecoderWorker
{
    internal static async Task<MediaInspectionResult> InspectAsync(string binaryDirectory, string mediaPath,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(24)) { throw new ArgumentOutOfRangeException(nameof(timeout)); }
        using var deadline = new CancellationTokenSource(timeout);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var entered = false;
        try
        {
            await SingleWorker.WaitAsync(stop.Token).ConfigureAwait(false);
            entered = true;
            if (cleanupBlocked) { return new(DecoderExit.CleanupFailed, null, "Previous cleanup not confirmed; restart required."); }
            var decoder = Path.Combine(binaryDirectory, "ffmpeg.exe");
            var probe = Path.Combine(binaryDirectory, "ffprobe.exe");
            using var decoderLease = OpenOrdinaryFile(decoder);
            using var probeLease = OpenOrdinaryFile(probe);
            using var inputLease = OpenOrdinaryFile(mediaPath);
            if (!string.Equals(Path.GetExtension(mediaPath), ".mp4", StringComparison.OrdinalIgnoreCase))
            { return new(DecoderExit.Failed, null, "Unsupported media profile."); }
            using var resource = typeof(PinnedDecoderWorker).Assembly.GetManifestResourceStream("HumCapture.Decoder.lock.json")
                ?? throw new InvalidOperationException("Missing decoder identity.");
            using var manifest = await JsonDocument.ParseAsync(resource, cancellationToken: stop.Token).ConfigureAwait(false);
            var hashes = manifest.RootElement.GetProperty("executable_sha256");
            if (!await MatchesPinAsync(decoderLease, hashes.GetProperty("ffmpeg.exe").GetString(), stop.Token).ConfigureAwait(false)
                || !await MatchesPinAsync(probeLease, hashes.GetProperty("ffprobe.exe").GetString(), stop.Token).ConfigureAwait(false))
            { return new(DecoderExit.Failed, null, "Decoder/probe identity mismatch."); }
            var mediaHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(inputLease, stop.Token).ConfigureAwait(false));
            var probeResult = await DecoderProcess.RunAsync(probe, InspectionArguments(mediaPath), timeout, 16 * 1024 * 1024, stop.Token).ConfigureAwait(false);
            cleanupBlocked = probeResult.Outcome == DecoderExit.CleanupFailed;
            if (probeResult.Outcome != DecoderExit.Decoded) { return InspectionFailure(probeResult, cancellationToken, deadline); }
            var video = DecodedMediaParser.Parse(probeResult.Output, stop.Token);
            var decodeResult = await DecoderProcess.RunAsync(decoder, Arguments(mediaPath), timeout, 1024 * 1024, stop.Token).ConfigureAwait(false);
            cleanupBlocked = decodeResult.Outcome == DecoderExit.CleanupFailed;
            if (decodeResult.Outcome != DecoderExit.Decoded) { return InspectionFailure(decodeResult, cancellationToken, deadline); }
            stop.Token.ThrowIfCancellationRequested();
            return new(DecoderExit.Decoded, new(mediaHash, inputLease.Length, video), "");
        }
        catch (OperationCanceledException)
        { return new(cancellationToken.IsCancellationRequested ? DecoderExit.Cancelled : DecoderExit.TimedOut, null, ""); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
            or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { return new(DecoderExit.Failed, null, exception.GetType().Name); }
        finally { if (entered) { SingleWorker.Release(); } }
    }

    private static async Task<bool> MatchesPinAsync(Stream file, string? expected, CancellationToken token) =>
        string.Equals(Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token).ConfigureAwait(false)), expected, StringComparison.Ordinal);

    private static MediaInspectionResult InspectionFailure(DecoderProcessResult result, CancellationToken caller, CancellationTokenSource deadline)
    {
        var outcome = result.Outcome == DecoderExit.Cancelled && !caller.IsCancellationRequested && deadline.IsCancellationRequested
            ? DecoderExit.TimedOut : result.Outcome;
        return new(outcome, null, result.Error);
    }

    internal static string[] InspectionArguments(string mediaPath) =>
    ["-v", "error", "-threads", "1", "-protocol_whitelist", "file", "-f", "mov", "-select_streams", "v",
        "-show_frames", "-show_streams", "-show_entries",
        "frame=stream_index,pts,width,height:frame_side_data=:stream=index,codec_type,codec_name,width,height,time_base:stream_disposition=attached_pic:stream_tags=",
        "-of", "json", mediaPath];
}

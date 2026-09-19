using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class MediaInspectionTests
{
    private static JsonObject Fixture() => JsonNode.Parse("""
        {"streams":[{"index":2,"codec_type":"video","codec_name":"h264","width":160,"height":120,
          "time_base":"1/15360","disposition":{"attached_pic":0}}],
         "frames":[{"stream_index":2,"pts":0,"width":160,"height":120},
                   {"stream_index":2,"pts":2048,"width":160,"height":120}]}
        """)!.AsObject();

    private static void Require(bool value, string reason)
    { if (!value) { throw new InvalidOperationException(reason); } }

    private static void Rejected(string json)
    {
        try { DecodedMediaParser.Parse(json); }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException) { return; }
        throw new InvalidOperationException("Invalid probe evidence accepted.");
    }

    internal static void ExactEvidence()
    {
        var fixture = Fixture();
        var frames = fixture["frames"]!.AsArray();
        frames[0]!["pts"] = 9007199254740993L;
        frames[1]!["pts"] = long.MinValue;
        frames[1]!["width"] = 320;
        var video = DecodedMediaParser.Parse(fixture.ToJsonString());
        Require(video.StreamIndex == 2 && video.TimeBaseNumerator == 1 && video.TimeBaseDenominator == 15360, "Stream/time base changed.");
        Require(video.Frames[0].PresentationTicks == 9007199254740993L && video.Frames[1].PresentationTicks == long.MinValue
            && video.Frames[1].PresentationIndex == 1 && video.Frames[1].Width == 320, "PTS ordering/precision/geometry lost.");
        frames[1]!["pts"] = 9007199254740993L;
        video = DecodedMediaParser.Parse(fixture.ToJsonString());
        Require(video.Frames[0].PresentationTicks == video.Frames[1].PresentationTicks, "Duplicate PTS silently repaired.");
    }

    internal static void StreamRefusals()
    {
        foreach (var mutation in new Action<JsonObject>[]
        {
            f => f["streams"]!.AsArray().Clear(),
            f => f["streams"]!.AsArray().Add(f["streams"]![0]!.DeepClone()),
            f => f["streams"]![0]!["disposition"]!["attached_pic"] = 1,
            f => f["streams"]![0]!["codec_type"] = "audio",
            f => f["streams"]![0]!["time_base"] = "0/1",
            f => f["streams"]![0]!["time_base"] = "1/0",
            f => f["streams"]![0]!["time_base"] = "1.5/30",
            f => f["streams"]![0]!["width"] = 0
        })
        { var fixture = Fixture(); mutation(fixture); Rejected(fixture.ToJsonString()); }
    }

    internal static void FrameRefusals()
    {
        foreach (var mutation in new Action<JsonObject>[]
        {
            f => f["frames"]!.AsArray().Clear(),
            f => f["frames"]![0]!.AsObject().Remove("pts"),
            f => f["frames"]![0]!["pts"] = "N/A",
            f => f["frames"]![0]!["pts"] = 0.5,
            f => f["frames"]![0]!["stream_index"] = 3,
            f => f["frames"]![0]!["height"] = -1
        })
        { var fixture = Fixture(); mutation(fixture); Rejected(fixture.ToJsonString()); }
        Rejected(Fixture().ToJsonString().Replace("\"pts\":0", "\"pts\":0,\"pts\":1", StringComparison.Ordinal));
        Rejected("{");
        Rejected(Fixture().ToJsonString().Replace("\"pts\":0", "\"pts\":9223372036854775808", StringComparison.Ordinal));
    }

    internal static void EnvelopeAndCancellation()
    {
        var fixture = Fixture();
        var frame = fixture["frames"]![0]!.ToJsonString();
        var json = "{\"streams\":" + fixture["streams"]!.ToJsonString()
            + ",\"frames\":[" + string.Join(',', Enumerable.Repeat(frame, DecodedMediaParser.MaximumFrames + 1)) + "]}";
        Rejected(json);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { DecodedMediaParser.Parse(Fixture().ToJsonString(), cancellation.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Cancelled parsing passed.");
    }

    internal static void PinAndFailureEvidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "hc-inspect-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe", "input.mp4" }) { File.WriteAllText(Path.Combine(root, name), "synthetic"); }
            var result = PinnedDecoderWorker.InspectAsync(root, Path.Combine(root, "input.mp4"), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Outcome == DecoderExit.Failed && result.Evidence is null && result.Reason.Contains("identity", StringComparison.Ordinal), "Pin rejection leaked passing evidence.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            result = PinnedDecoderWorker.InspectAsync(root, Path.Combine(root, "input.mp4"), TimeSpan.FromSeconds(5), cancellation.Token).GetAwaiter().GetResult();
            Require(result.Outcome == DecoderExit.Cancelled && result.Evidence is null, "Cancelled inspection produced evidence.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    internal static void ArgumentsUseObservedPts()
    {
        var args = PinnedDecoderWorker.InspectionArguments(Path.Combine(Path.GetTempPath(), "space path", "input.mp4"));
        var entries = args[Array.IndexOf(args, "-show_entries") + 1];
        Require(entries.Contains(",pts,", StringComparison.Ordinal) && !entries.Contains("best_effort", StringComparison.Ordinal)
            && !entries.Contains("frame_rate", StringComparison.Ordinal), "Inferred timestamps requested.");
        Require(args[Array.IndexOf(args, "-select_streams") + 1] == "v", "Additional video streams hidden.");
    }

    internal static async Task<int> RealAsync(string bin, string mediaDirectory)
    {
        foreach (var (name, count) in new[] { ("synthetic.mp4", 30), ("synthetic-variable.mp4", 20), ("truncated.mp4", 0), ("corrupt.mp4", 0) })
        {
            var media = Path.Combine(mediaDirectory, name);
            var before = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(media)));
            var result = await PinnedDecoderWorker.InspectAsync(bin, media, TimeSpan.FromSeconds(30));
            var after = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(media)));
            Require(before == after, "Inspection changed source bytes.");
            if (count > 0)
            {
                var evidence = result.Evidence ?? throw new InvalidOperationException(result.Reason);
                Require(result.Outcome == DecoderExit.Decoded && evidence.MediaSha256 == before
                    && evidence.ByteLength == new FileInfo(media).Length && evidence.Video.Frames.Count == count
                    && evidence.Video.Width == 160 && evidence.Video.Height == 120, "Actual inspection mismatch.");
                var pts = evidence.Video.Frames.Select(f => f.PresentationTicks).ToArray();
                var deltaCount = pts.Skip(1).Select((p, i) => p - pts[i]).Distinct().Count();
                Require(deltaCount == (count == 20 ? 2 : 1), "Fixed/variable intervals lost.");
            }
            else { Require(result.Outcome == DecoderExit.Failed && result.Evidence is null, "Damaged media produced passing evidence."); }
            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { Test = "HC-MEDIA-REAL", File = name, Passed = true,
                Outcome = result.Outcome.ToString(), SourceUnchanged = true, Evidence = result.Evidence }));
        }
        return 0;
    }
}

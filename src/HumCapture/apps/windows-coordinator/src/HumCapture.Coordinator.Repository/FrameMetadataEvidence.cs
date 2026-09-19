using System.Globalization;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal enum TimingMetadataVersion { LegacyV1, ExactV1_1 }
internal sealed record FrameMetadataResult(FrameAssociationEvidence Association, bool MappedTimesRecomputed, int MappedFramesCompared = 0);

// Reads only caller-supplied finalized bytes/evidence. File/hash/manifest admission remains separate.
internal sealed class FrameMetadataEvidence
{
    private readonly MetadataSchemaValidator schemas = new();

    internal FrameMetadataResult Compare(ReadOnlyMemory<byte> timingBytes, ReadOnlyMemory<byte> cameraBytes,
        TimingStream<SourceFrame> frames, DecodedVideo video, CancellationToken token = default,
        TimingMetadataVersion version = TimingMetadataVersion.LegacyV1)
    {
        var kind = TimingClockBindings.SchemaKind(version);
        var timing = schemas.Validate(kind, timingBytes, token);
        var camera = schemas.Validate(MetadataKind.Camera, cameraBytes, token);
        Require(Id(timing, "capture_attempt_id") == frames.CaptureAttemptId
            && Id(camera, "capture_attempt_id") == frames.CaptureAttemptId
            && Id(camera, "camera_stream_id") == frames.StreamId, "Frame metadata identity mismatch.");
        var bindings = new TimingClockBindings(timing, version, token);
        var stream = bindings.Stream(frames.StreamId, "FRAME_TIMESTAMPS", "timing/frame-timestamps.bin");
        Require(stream.TryGetProperty("presentation_clock_id", out var presentation), "Missing presentation clock.");
        var mappedFramesCompared = 0;
        foreach (var frame in frames.Records)
        {
            token.ThrowIfCancellationRequested();
            if (bindings.Compare(stream, frame.NativeTicks, frame.HostArrivalTicks, frame.MappedSessionTicks,
                frame.ClockModelId, frame.UncertaintyNs)) { mappedFramesCompared++; }
        }
        var observed = camera.GetProperty("observed_timing");
        Require(observed.GetProperty("record_count").GetInt64() == frames.Records.Count
            && observed.GetProperty("accepted_frame_count").GetInt64() == frames.Records.Count(f => f.Disposition == 1), "Camera record counts disagree.");
        var mode = camera.GetProperty("negotiated_mode");
        Require(mode.GetProperty("width").GetInt64() == video.Width && mode.GetProperty("height").GetInt64() == video.Height
            && video.Frames.All(f => f.Width == video.Width && f.Height == video.Height), "Decoded coded geometry differs from negotiated mode.");
        var generated = new List<GeneratedVideoFrame>();
        if (camera.TryGetProperty("video_transform_events", out var transformations))
        {
            foreach (var item in transformations.EnumerateArray())
            { generated.Add(new GeneratedVideoFrame(U64(item, "video_frame_index"), U64(item, "source_frame_sequence"), item.GetProperty("reason").GetString()!)); }
        }
        var association = FrameEvidenceAssociation.Compare(frames.Records, video,
            bindings.Clocks[presentation.GetGuid()].GetProperty("ticks_per_second").GetUInt64(), generated, token);
        // Legacy or absent mapped evidence is not promoted to a successful comparison.
        return new FrameMetadataResult(association, mappedFramesCompared > 0, mappedFramesCompared);
    }

    private static Guid Id(JsonElement item, string key) => item.GetProperty(key).GetGuid();
    private static ulong U64(JsonElement item, string key)
    {
        Require(ulong.TryParse(item.GetProperty(key).GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value), "Unsigned metadata integer overflow.");
        return value;
    }
    private static void Require(bool value, string reason)
    { if (!value) { throw new InvalidDataException(reason); } }
}

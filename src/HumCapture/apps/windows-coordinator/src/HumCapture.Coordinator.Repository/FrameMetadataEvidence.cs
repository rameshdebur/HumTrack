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
        var kind = version switch
        {
            TimingMetadataVersion.LegacyV1 => MetadataKind.Timing,
            TimingMetadataVersion.ExactV1_1 => MetadataKind.TimingExact,
            _ => throw new InvalidDataException("Unsupported timing metadata version.")
        };
        var timing = schemas.Validate(kind, timingBytes, token);
        var camera = schemas.Validate(MetadataKind.Camera, cameraBytes, token);
        Require(Id(timing, "capture_attempt_id") == frames.CaptureAttemptId
            && Id(camera, "capture_attempt_id") == frames.CaptureAttemptId
            && Id(camera, "camera_stream_id") == frames.StreamId, "Frame metadata identity mismatch.");
        var clocks = Unique(timing.GetProperty("clocks"), "clock_id");
        var streams = Unique(timing.GetProperty("streams"), "stream_id");
        Require(clocks.ContainsKey(Id(timing, "session_clock_id")), "Unknown session clock.");
        Require(streams.TryGetValue(frames.StreamId, out var stream)
            && stream.GetProperty("kind").GetString() == "FRAME_TIMESTAMPS"
            && stream.GetProperty("relative_path").GetString() == "timing/frame-timestamps.bin", "Frame stream binding mismatch.");
        foreach (var declared in streams.Values)
        {
            foreach (var key in new[] { "native_clock_id", "host_arrival_clock_id", "presentation_clock_id" })
            {
                if (declared.TryGetProperty(key, out var clock))
                { Require(clocks.ContainsKey(clock.GetGuid()), "Stream references unknown clock."); }
            }
        }
        var models = new Dictionary<uint, JsonElement>();
        var exactModels = new Dictionary<uint, (ExactClockScale Scale, long Offset)>();
        foreach (var model in timing.GetProperty("clock_models").EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            Require(models.TryAdd(model.GetProperty("model_id").GetUInt32(), model), "Duplicate clock model.");
            Require(clocks.ContainsKey(Id(model, "source_clock_id")) && clocks.ContainsKey(Id(model, "target_clock_id")), "Unknown model clock.");
            Require(U64(model, "valid_from_ticks") <= U64(model, "valid_through_ticks"), "Reversed model interval.");
            Require(long.TryParse(model.GetProperty("offset_ticks").GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset), "Model offset overflow.");
            Require(double.IsFinite(model.GetProperty("scale").GetDouble()), "Nonfinite model scale.");
            var unitScale = model.GetProperty("scale").GetDouble().Equals(1d);
            if (version == TimingMetadataVersion.ExactV1_1)
            {
                var scale = ClockQuantization.ParseScale(model.GetProperty("scale").GetRawText());
                exactModels.Add(model.GetProperty("model_id").GetUInt32(), (scale, offset));
                unitScale = scale.IsOne;
                if (model.GetProperty("kind").GetString() == "OFFSET") { Require(unitScale, "OFFSET requires exact unit scale."); }
            }
            if (model.GetProperty("kind").GetString() == "IDENTITY")
            {
                var source = clocks[Id(model, "source_clock_id")]; var target = clocks[Id(model, "target_clock_id")];
                Require(Id(source, "epoch_id") == Id(target, "epoch_id")
                    && source.GetProperty("ticks_per_second").GetUInt64() == target.GetProperty("ticks_per_second").GetUInt64()
                    && unitScale && offset == 0, "Unproven identity model.");
            }
        }
        Require(stream.TryGetProperty("presentation_clock_id", out var presentation), "Missing presentation clock.");
        var mappedFramesCompared = 0;
        foreach (var frame in frames.Records)
        {
            token.ThrowIfCancellationRequested();
            if (frame.HostArrivalTicks.HasValue)
            { Require(stream.TryGetProperty("host_arrival_clock_id", out _), "Host arrival lacks clock binding."); }
            if (!frame.MappedSessionTicks.HasValue) { continue; }
            Require(models.TryGetValue(frame.ClockModelId, out var model), "Mapped frame references unknown model.");
            Require(Id(model, "source_clock_id") == Id(stream, "native_clock_id")
                && Id(model, "target_clock_id") == Id(timing, "session_clock_id")
                && model.GetProperty("quality").GetString() != "UNAVAILABLE"
                && frame.NativeTicks >= U64(model, "valid_from_ticks") && frame.NativeTicks <= U64(model, "valid_through_ticks")
                && frame.UncertaintyNs >= model.GetProperty("uncertainty_ns").GetUInt32(), "Frame model binding/range/uncertainty mismatch.");
            if (version == TimingMetadataVersion.ExactV1_1)
            {
                var exact = exactModels[frame.ClockModelId];
                Require(ClockQuantization.Map(exact.Scale, frame.NativeTicks, exact.Offset) == frame.MappedSessionTicks.Value,
                    "Recorded mapped frame time differs from exact quantization.");
                mappedFramesCompared++;
            }
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
            clocks[presentation.GetGuid()].GetProperty("ticks_per_second").GetUInt64(), generated, token);
        // Legacy or absent mapped evidence is not promoted to a successful comparison.
        return new FrameMetadataResult(association, mappedFramesCompared > 0, mappedFramesCompared);
    }

    private static Dictionary<Guid, JsonElement> Unique(JsonElement array, string key)
    {
        var result = new Dictionary<Guid, JsonElement>();
        foreach (var item in array.EnumerateArray()) { Require(result.TryAdd(Id(item, key), item), "Duplicate metadata identity."); }
        return result;
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

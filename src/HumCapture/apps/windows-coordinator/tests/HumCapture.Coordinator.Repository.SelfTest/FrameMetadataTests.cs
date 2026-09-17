using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class FrameMetadataTests
{
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TimingVectors", name));
    private static JsonNode Timing()
    {
        var timing = JsonNode.Parse(Fixture("valid-timing-metadata.json"))!;
        var clock = timing["clocks"]![0]!.DeepClone();
        clock["clock_id"] = "20000000-0000-4000-8000-000000000009";
        clock["ticks_per_second"] = 90000; clock["provenance"] = "ENCODER_PTS"; clock["authority"] = "PRESENTATION";
        timing["clocks"]!.AsArray().Add(clock);
        timing["streams"]![0]!["presentation_clock_id"] = clock["clock_id"]!.DeepClone();
        return timing;
    }
    private static JsonNode Camera() => JsonNode.Parse(Fixture("valid-camera-metadata.json"))!;
    private static TimingStream<SourceFrame> Frames() => TimingBinaryReader.Frames(Fixture("valid-frame-timestamps.bin"));
    private static DecodedVideo Video() => new(0, "h264", 1920, 1080, 1, 90000,
        [new(0, 0, 1920, 1080), new(1, 3000, 1920, 1080), new(2, 6000, 1920, 1080)]);
    private static FrameMetadataResult Run(JsonNode timing, JsonNode camera) => new FrameMetadataEvidence().Compare(
        JsonSerializer.SerializeToUtf8Bytes(timing), JsonSerializer.SerializeToUtf8Bytes(camera), Frames(), Video());
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Mismatched frame metadata accepted.");
    }
    internal static void Bindings()
    {
        foreach (var cadence in new[] { "FIXED", "VARIABLE", "ADAPTIVE", "UNKNOWN" })
        {
            var camera = Camera(); camera["negotiated_mode"]!["rate_control_class"] = cadence;
            var result = Run(Timing(), camera);
            if (result.Association.AcceptedFrames != 3 || !result.Association.AllPresentationTimesCompared || result.MappedTimesRecomputed)
            { throw new InvalidOperationException("Association result or incomplete mapping evidence changed."); }
        }
        // Standalone historical fixtures are not an integrated package: their declared
        // presentation clock has 1 GHz ticks, but binary PTS uses 90 kHz.
        Reject(() => Run(JsonNode.Parse(Fixture("valid-timing-metadata.json"))!, Camera()));
    }
    internal static void ClockGuards()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            n => n["capture_attempt_id"] = Guid.NewGuid().ToString(),
            n => n["clocks"]!.AsArray().Add(n["clocks"]![0]!.DeepClone()),
            n => n["streams"]!.AsArray().Add(n["streams"]![0]!.DeepClone()),
            n => n["streams"]![0]!.AsObject().Remove("presentation_clock_id"),
            n => n["streams"]![0]!.AsObject().Remove("host_arrival_clock_id"),
            n => n["streams"]![0]!["native_clock_id"] = Guid.NewGuid().ToString(),
            n => n["clock_models"]![0]!["valid_from_ticks"] = "18446744073709551616",
            n => n["clock_models"]![0]!["valid_through_ticks"] = "1",
            n => n["clock_models"]![0]!["model_id"] = 2,
            n => n["clock_models"]![0]!["quality"] = "UNAVAILABLE",
            n => n["clock_models"]![0]!["uncertainty_ns"] = 250001,
            n => n["clock_models"]![0]!["kind"] = "IDENTITY",
            n => n["clock_models"]![0]!["offset_ticks"] = "9223372036854775808"
        }) { var timing = Timing(); change(timing); Reject(() => Run(timing, Camera())); }
    }
    internal static void CameraGuards()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            n => n["camera_stream_id"] = Guid.NewGuid().ToString(),
            n => n["observed_timing"]!["record_count"] = 4,
            n => n["observed_timing"]!["accepted_frame_count"] = 2,
            n => n["negotiated_mode"]!["width"] = 1080,
            n => n["video_transform_events"] = JsonNode.Parse("""[{"video_frame_index":"18446744073709551616","kind":"GENERATED_DUPLICATE","source_frame_sequence":"0","reason":"test"}]""")
        }) { var camera = Camera(); change(camera); Reject(() => Run(Timing(), camera)); }
    }

    private static JsonNode ExactTiming()
    {
        var timing = Timing(); timing["schema_version"] = "1.1.0";
        timing["mapping_policy"] = "EXACT_DECIMAL_NEAREST_TIES_EVEN";
        return timing;
    }
    private static TimingStream<SourceFrame> ExactFrames()
    {
        var original = Frames(); ulong[] mapped = [5000000002, 5033333335, 5066666668];
        return original with { Records = original.Records.Select((f, i) => f with { MappedSessionTicks = mapped[i] }).ToArray() };
    }
    private static FrameMetadataResult RunExact(JsonNode timing, TimingStream<SourceFrame> frames,
        TimingMetadataVersion version = TimingMetadataVersion.ExactV1_1) => new FrameMetadataEvidence().Compare(
        JsonSerializer.SerializeToUtf8Bytes(timing), JsonSerializer.SerializeToUtf8Bytes(Camera()), frames, Video(), version: version);

    internal static void ExactMappedValues()
    {
        var frames = ExactFrames();
        var result = RunExact(ExactTiming(), frames);
        if (!result.MappedTimesRecomputed || result.MappedFramesCompared != 3 || frames.Records[0].NativeTicks != 1000000000)
        { throw new InvalidOperationException("Mapped comparison or native preservation failed."); }
        Reject(() => RunExact(ExactTiming(), Frames())); // Preserve/reject old mismatched observations, never repair them.
        var changed = frames.Records.ToArray(); changed[0] = changed[0] with { MappedSessionTicks = 5000000003, UncertaintyNs = uint.MaxValue };
        Reject(() => RunExact(ExactTiming(), frames with { Records = changed }));
        var absent = frames with { Records = frames.Records.Select(f => f with { MappedSessionTicks = null, ClockModelId = 0, UncertaintyNs = 0 }).ToArray() };
        var noEvidence = RunExact(ExactTiming(), absent);
        if (noEvidence.MappedTimesRecomputed || noEvidence.MappedFramesCompared != 0)
        { throw new InvalidOperationException("Absent mapped evidence became PASS."); }
    }

    internal static void ExactCompatibility()
    {
        Reject(() => RunExact(Timing(), ExactFrames()));
        Reject(() => RunExact(ExactTiming(), ExactFrames(), TimingMetadataVersion.LegacyV1));
        Reject(() => RunExact(ExactTiming(), ExactFrames(), (TimingMetadataVersion)99));
        var badPolicy = ExactTiming(); badPolicy["mapping_policy"] = "FLOOR";
        Reject(() => RunExact(badPolicy, ExactFrames()));
        var offset = ExactTiming(); offset["clock_models"]![0]!["kind"] = "OFFSET";
        Reject(() => RunExact(offset, ExactFrames()));
        var text = System.Text.Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(offset))
            .Replace("1.000000002", "1.0000000000000000001", StringComparison.Ordinal);
        Reject(() => new FrameMetadataEvidence().Compare(System.Text.Encoding.UTF8.GetBytes(text),
            JsonSerializer.SerializeToUtf8Bytes(Camera()), ExactFrames(), Video(), version: TimingMetadataVersion.ExactV1_1));
        var legacy = Run(Timing(), Camera());
        if (legacy.MappedTimesRecomputed) { throw new InvalidOperationException("Legacy mappings implicitly upgraded."); }
        var precise = ExactTiming();
        precise["clock_models"]![0]!["valid_from_ticks"] = "10000000000000000";
        precise["clock_models"]![0]!["valid_through_ticks"] = "10000000000000002";
        precise["clock_models"]![0]!["offset_ticks"] = "0";
        var raw = System.Text.Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(precise))
            .Replace("1.000000002", "1.0000000000000001", StringComparison.Ordinal);
        var large = Frames() with { Records = Frames().Records.Select((f, i) => f with
            { NativeTicks = 10000000000000000UL + (ulong)i, MappedSessionTicks = 10000000000000001UL + (ulong)i }).ToArray() };
        var exactResult = new FrameMetadataEvidence().Compare(System.Text.Encoding.UTF8.GetBytes(raw),
            JsonSerializer.SerializeToUtf8Bytes(Camera()), large, Video(), version: TimingMetadataVersion.ExactV1_1);
        if (exactResult.MappedFramesCompared != 3) { throw new InvalidOperationException("Serialized scale precision lost."); }
    }
}

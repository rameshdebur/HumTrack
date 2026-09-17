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
}

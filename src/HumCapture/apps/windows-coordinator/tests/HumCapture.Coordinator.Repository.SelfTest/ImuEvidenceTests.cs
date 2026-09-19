using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class ImuEvidenceTests
{
    internal static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TimingVectors", name));
    internal static byte[] Bytes(JsonNode value) => JsonSerializer.SerializeToUtf8Bytes(value);
    internal static JsonNode Metadata() => JsonNode.Parse(Fixture("valid-imu-metadata.json"))!;
    internal static TimingStream<ImuSample> Samples() => TimingBinaryReader.Imu(Fixture("valid-imu-samples.bin"));
    internal static JsonNode Timing(bool exact = false)
    {
        var value = FrameMetadataTests.Timing();
        value["clock_models"]![0]!["source_clock_id"] = value["streams"]![1]!["native_clock_id"]!.DeepClone();
        value["clock_models"]![0]!["uncertainty_ns"] = 100000;
        value["clock_models"]![0]!["scale"] = 1;
        if (exact) { value["schema_version"] = "1.1.0"; value["mapping_policy"] = "EXACT_DECIMAL_NEAREST_TIES_EVEN"; }
        return value;
    }
    private static ImuMetadataResult Run(JsonNode timing, JsonNode metadata, TimingStream<ImuSample>? samples = null,
        TimingMetadataVersion version = TimingMetadataVersion.LegacyV1) => new ImuMetadataEvidence().Compare(Bytes(timing), Bytes(metadata), samples ?? Samples(), version: version);
    internal static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Inconsistent evidence accepted.");
    }
    internal static void Bindings()
    {
        var legacy = Run(Timing(), Metadata());
        // Standalone historical values were not generated from this exact model.
        // Keep those immutable; construct explicitly synthetic matching observations.
        var exactSamples = Samples() with { Records = Samples().Records.Select(s => s with { MappedSessionTicks = s.NativeTicks + 4000000000 }).ToArray() };
        var exact = Run(Timing(true), Metadata(), exactSamples, TimingMetadataVersion.ExactV1_1);
        Reject(() => Run(Timing(true), Metadata(), version: TimingMetadataVersion.ExactV1_1));
        if (legacy.SampleCount != 3 || legacy.MappedSamplesCompared != 0 || exact.MappedSamplesCompared != 3
            || legacy.Sensors.Count != 2 || legacy.Sensors.Any(s => s.SequenceAnomalies != 0))
        { throw new InvalidOperationException("IMU evidence counts/legacy semantics incorrect."); }
        var absent = Samples() with { Records = Samples().Records.Select(s => s with { MappedSessionTicks = null, ClockModelId = 0 }).ToArray() };
        if (Run(Timing(true), Metadata(), absent, TimingMetadataVersion.ExactV1_1).MappedSamplesCompared != 0)
        { throw new InvalidOperationException("Absent mapping promoted to PASS."); }
        var bad = Samples() with { Records = Samples().Records.Select(s => s with { MappedSessionTicks = s.MappedSessionTicks + 1, UncertaintyNs = uint.MaxValue }).ToArray() };
        Reject(() => Run(Timing(true), Metadata(), bad, TimingMetadataVersion.ExactV1_1));
        Reject(() => Run(Timing(), Metadata(), version: TimingMetadataVersion.ExactV1_1));
        Reject(() => Run(Timing(true), Metadata()));
    }
    internal static void SensorGuards()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            n => n["capture_attempt_id"] = Guid.NewGuid().ToString(),
            n => n["imu_file_stream_id"] = Guid.NewGuid().ToString(),
            n => n["sensor_streams"]![0]!["units"] = "RAD_PER_S",
            n => n["sensor_streams"]![0]!["sensor_kind"] = "GRAVITY",
            n => n["sensor_streams"]![0]!["availability"] = "NOT_AVAILABLE",
            n => n["sensor_streams"]!.AsArray().RemoveAt(0),
            n => n["sensor_streams"]!.AsArray().Add(n["sensor_streams"]![0]!.DeepClone()),
            n => n["sensor_streams"]![1]!["sensor_uuid"] = n["sensor_streams"]![0]!["sensor_uuid"]!.DeepClone(),
            n => n["camera_associations"]![0]!["camera_stream_id"] = Guid.NewGuid().ToString(),
            n => n["camera_associations"]![0]!["valid_from_segment"] = 2,
            n => n["camera_associations"]![0]!["imu_to_camera_rotation_row_major"]![0] = -1,
            n => n["camera_associations"]![0]!["imu_to_camera_rotation_row_major"]![1] = 1
        })
        {
            var metadata = Metadata(); metadata["camera_associations"]![0]!["valid_through_segment"] = 1;
            change(metadata); Reject(() => Run(Timing(), metadata));
        }
    }
    internal static void ClockGuards()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            n => n["clock_models"]![0]!["source_clock_id"] = n["streams"]![0]!["native_clock_id"]!.DeepClone(),
            n => n["clock_models"]![0]!["uncertainty_ns"] = 100001,
            n => n["clock_models"]![0]!["quality"] = "UNAVAILABLE",
            n => n["clock_models"]![0]!["model_id"] = 2,
            n => n["clock_models"]![0]!["valid_from_ticks"] = "2000000000",
            n => n["streams"]![1]!.AsObject().Remove("host_arrival_clock_id"),
            n => n["streams"]![1]!["relative_path"] = "imu/wrong.bin"
        }) { var timing = Timing(); change(timing); Reject(() => Run(timing, Metadata())); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { new ImuMetadataEvidence().Compare(Bytes(Timing()), Bytes(Metadata()), Samples(), cancelled.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Cancellation ignored.");
    }
    internal static void MissingAndAnomalousEvidence()
    {
        var metadata = Metadata(); metadata["sensor_streams"]![1]!["availability"] = "NOT_SUPPORTED";
        var missingGyro = Samples() with { Records = Samples().Records.Where(s => s.SensorKind == 1).ToArray() };
        var result = Run(Timing(), metadata, missingGyro);
        if (result.Sensors.Single(s => s.SensorStreamId == 2).SampleCount != 0)
        { throw new InvalidOperationException("Missing gyroscope synthesized."); }
        var samples = Samples(); var first = samples.Records[0];
        var anomalous = samples with { Records = [first, first with { Sequence = 5, NativeTicks = first.NativeTicks - 1, SegmentId = 1, MappedSessionTicks = null }] };
        var sensor = Run(Timing(), Metadata(), anomalous).Sensors.Single(s => s.SensorStreamId == first.SensorStreamId);
        if (sensor.SequenceAnomalies != 1 || sensor.NativeTimeRegressions != 1 || sensor.SegmentChanges != 1)
        { throw new InvalidOperationException("Acquisition anomalies hidden."); }
        metadata = Metadata(); metadata["sensor_streams"]![0]!["availability"] = "PERMISSION_LIMITED";
        _ = Run(Timing(), metadata); // Partial availability does not imply no measurements.
    }
}

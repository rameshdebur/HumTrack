using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record ImuSensorEvidence(ushort SensorStreamId, string Availability, int SampleCount,
    int SequenceAnomalies, int NativeTimeRegressions, int SegmentChanges);
internal sealed record ImuMetadataResult(int SampleCount, int MappedSamplesCompared,
    IReadOnlyList<ImuSensorEvidence> Sensors);

// Consistency only: does not infer required sensors, rate compliance or physical calibration.
internal sealed class ImuMetadataEvidence
{
    private readonly MetadataSchemaValidator schemas = new();
    private static readonly string[] Kinds = ["", "ACCELEROMETER", "GYROSCOPE", "ROTATION_VECTOR", "GRAVITY", "LINEAR_ACCELERATION", "MAGNETOMETER"];
    private static readonly string[] Units = ["", "M_PER_S2", "RAD_PER_S", "UNIT_QUATERNION", "M_PER_S2", "M_PER_S2", "MICROTESLA"];

    internal ImuMetadataResult Compare(ReadOnlyMemory<byte> timingBytes, ReadOnlyMemory<byte> imuBytes,
        TimingStream<ImuSample> samples, CancellationToken token = default,
        TimingMetadataVersion version = TimingMetadataVersion.LegacyV1)
    {
        var timing = schemas.Validate(TimingClockBindings.SchemaKind(version), timingBytes, token);
        var imu = schemas.Validate(MetadataKind.Imu, imuBytes, token);
        Require(timing.GetProperty("capture_attempt_id").GetGuid() == samples.CaptureAttemptId
            && imu.GetProperty("capture_attempt_id").GetGuid() == samples.CaptureAttemptId
            && imu.GetProperty("imu_file_stream_id").GetGuid() == samples.StreamId, "IMU identity mismatch.");
        var bindings = new TimingClockBindings(timing, version, token);
        var stream = bindings.Stream(samples.StreamId, "IMU_SAMPLES", "imu/imu-samples.bin");
        var declarations = new Dictionary<ushort, JsonElement>();
        var uuids = new HashSet<Guid>();
        foreach (var sensor in imu.GetProperty("sensor_streams").EnumerateArray())
        {
            var kind = Array.IndexOf(Kinds, sensor.GetProperty("sensor_kind").GetString());
            Require(kind > 0 && sensor.GetProperty("units").GetString() == Units[kind], "Sensor kind/units mismatch.");
            Require(declarations.TryAdd(sensor.GetProperty("sensor_stream_id").GetUInt16(), sensor)
                && uuids.Add(sensor.GetProperty("sensor_uuid").GetGuid()), "Duplicate IMU sensor identity.");
        }
        CheckAssociations(imu, bindings, token);
        var previous = new Dictionary<ushort, ImuSample>();
        var counts = declarations.Keys.ToDictionary(id => id, _ => new int[4]);
        var compared = 0;
        foreach (var sample in samples.Records)
        {
            token.ThrowIfCancellationRequested();
            Require(declarations.TryGetValue(sample.SensorStreamId, out var sensor), "Undeclared IMU sensor.");
            Require(sample.SensorKind > 0 && sample.SensorKind < Kinds.Length
                && Kinds[sample.SensorKind] == sensor.GetProperty("sensor_kind").GetString(), "Binary/metadata sensor kind mismatch.");
            Require(sensor.GetProperty("availability").GetString() is not ("NOT_AVAILABLE" or "NOT_SUPPORTED"), "Unavailable sensor has recorded samples.");
            if (bindings.Compare(stream, sample.NativeTicks, sample.HostArrivalTicks, sample.MappedSessionTicks,
                sample.ClockModelId, sample.UncertaintyNs)) { compared++; }
            var count = counts[sample.SensorStreamId]; count[0]++;
            if (previous.TryGetValue(sample.SensorStreamId, out var prior))
            {
                // Report acquisition anomalies, never repair sequences or synthesize samples.
                if (prior.Sequence == ulong.MaxValue || sample.Sequence != prior.Sequence + 1) { count[1]++; }
                if (sample.NativeTicks < prior.NativeTicks) { count[2]++; }
                if (sample.SegmentId != prior.SegmentId) { count[3]++; }
            }
            previous[sample.SensorStreamId] = sample;
        }
        return new ImuMetadataResult(samples.Records.Count, compared, declarations.Select(pair =>
        {
            var c = counts[pair.Key];
            return new ImuSensorEvidence(pair.Key, pair.Value.GetProperty("availability").GetString()!, c[0], c[1], c[2], c[3]);
        }).ToArray());
    }

    private static void CheckAssociations(JsonElement imu, TimingClockBindings bindings, CancellationToken token)
    {
        if (!imu.TryGetProperty("camera_associations", out var associations)) { return; }
        foreach (var item in associations.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            Require(bindings.Streams.TryGetValue(item.GetProperty("camera_stream_id").GetGuid(), out var camera)
                && camera.GetProperty("kind").GetString() == "FRAME_TIMESTAMPS", "IMU association references unknown camera stream.");
            if (item.TryGetProperty("valid_through_segment", out var through))
            { Require(through.GetUInt32() >= item.GetProperty("valid_from_segment").GetUInt32(), "Reversed association segment range."); }
            if (!item.TryGetProperty("imu_to_camera_rotation_row_major", out var rotation)) { continue; }
            var m = rotation.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            Require(m.Length == 9 && Array.TrueForAll(m, double.IsFinite), "Invalid rotation matrix.");
            for (var row = 0; row < 3; row++)
            {
                for (var other = row; other < 3; other++)
                {
                    var dot = Enumerable.Range(0, 3).Sum(i => m[row * 3 + i] * m[other * 3 + i]);
                    Require(Math.Abs(dot - (row == other ? 1 : 0)) <= 1e-4, "Non-orthonormal rotation matrix.");
                }
            }
            var determinant = m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6])
                + m[2] * (m[3] * m[7] - m[4] * m[6]);
            Require(Math.Abs(determinant - 1) <= 1e-4, "Rotation matrix contains reflection.");
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidDataException(message); } }
}

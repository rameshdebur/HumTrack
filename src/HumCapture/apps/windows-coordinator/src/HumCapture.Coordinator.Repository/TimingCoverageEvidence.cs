using System.Globalization;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record CoverageSample(ushort Lane, uint Segment, ulong Sequence, ulong Ticks);
internal sealed record TimingCoverageResult(string Disposition, int ArtifactsChecked, string Reason);

// Reconstruct from decoded native records. No nominal rates or global-time interpolation.
internal static class TimingCoverageEvidence
{
    internal const int MaximumSpans = 16384;
    internal static TimingCoverageResult Compare(PackageInputManifest manifest, JsonElement? timing,
        TimingStream<SourceFrame>? frames, TimingStream<ImuSample>? imu, CancellationToken token)
    {
        var timed = manifest.Artifacts.Where(a => a.Role is "FRAME_TIMESTAMPS" or "IMU_SAMPLES" or "SCIENTIFIC_MASTER_VIDEO").ToArray();
        if (timed.Length == 0) { return new("NOT_APPLICABLE", 0, "No timed artifacts retained."); }
        if (manifest.Json.GetProperty("schema_version").GetString() != "1.1.0")
        { return new("NOT_ASSESSED", 0, "Legacy coverage lacks versioned per-lane/run semantics."); }
        if (!timing.HasValue) { return new("NOT_ASSESSED", 0, "Native clock metadata unavailable."); }
        var bindings = new TimingClockBindings(timing.Value, manifest.TimingVersion!.Value, token);
        var checkedCount = 0;
        foreach (var artifact in timed)
        {
            token.ThrowIfCancellationRequested();
            Guid streamId; IEnumerable<CoverageSample> records; string role; string path;
            if (artifact.Role == "IMU_SAMPLES")
            {
                if (imu is null) { return new("NOT_ASSESSED", checkedCount, "IMU samples unavailable."); }
                streamId = imu.StreamId; role = "IMU_SAMPLES"; path = "imu/imu-samples.bin";
                records = imu.Records.Select(s => new CoverageSample(s.SensorStreamId, s.SegmentId, s.Sequence, s.NativeTicks));
            }
            else
            {
                if (frames is null) { return new("NOT_ASSESSED", checkedCount, "Source frames unavailable."); }
                streamId = frames.StreamId; role = "FRAME_TIMESTAMPS"; path = "timing/frame-timestamps.bin";
                records = frames.Records.Where(s => artifact.Role != "SCIENTIFIC_MASTER_VIDEO" || s.Disposition == 1)
                    .Select(s => new CoverageSample(0, s.SegmentId, s.Sequence, s.NativeTicks));
            }
            var stream = bindings.Stream(streamId, role, path);
            var clockId = stream.GetProperty("native_clock_id").GetGuid();
            var expected = Build(streamId, clockId, bindings.Clocks[clockId].GetProperty("ticks_per_second").GetUInt64(), records, token);
            var declared = manifest.Json.GetProperty("artifacts").EnumerateArray()
                .Single(a => a.GetProperty("artifact_id").GetString() == artifact.Digest.ArtifactId).GetProperty("timing_coverage");
            PackageInputManifest.Need(CaptureCanonicalJson.Encode(declared, token).AsSpan()
                .SequenceEqual(CaptureCanonicalJson.Encode(expected, token)), "Timing coverage differs from native records: " + artifact.Role);
            checkedCount++;
        }
        return new("PASS", checkedCount, "Exact native coverage reconstructed per lane and run.");
    }

    internal static JsonElement Build(Guid stream, Guid clock, ulong frequency,
        IEnumerable<CoverageSample> samples, CancellationToken token = default)
    {
        var lanes = new SortedDictionary<ushort, List<CoverageSample>>();
        var count = 0;
        foreach (var sample in samples)
        {
            token.ThrowIfCancellationRequested();
            PackageInputManifest.Need(++count <= TimingBinaryReader.MaximumImuRecords, "Coverage record limit exceeded.");
            if (!lanes.TryGetValue(sample.Lane, out var lane)) { lane = []; lanes.Add(sample.Lane, lane); }
            lane.Add(sample);
        }
        var spans = new List<object>();
        foreach (var (laneId, lane) in lanes)
        {
            var first = 0; var run = 0; var gaps = 0;
            void Add(int end)
            {
                PackageInputManifest.Need(spans.Count < MaximumSpans, "Coverage span envelope exceeded.");
                spans.Add(new { lane_id = laneId, run_index = run++, segment_id = lane[first].Segment,
                    record_count = Decimal(end - first + 1), first_ticks = Decimal(lane[first].Ticks),
                    last_ticks = Decimal(lane[end].Ticks), first_sequence = Decimal(lane[first].Sequence),
                    last_sequence = Decimal(lane[end].Sequence), sequence_gap_count = Decimal(gaps) });
            }
            for (var index = 1; index < lane.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                var prior = lane[index - 1]; var current = lane[index];
                if (current.Segment != prior.Segment || current.Ticks < prior.Ticks || current.Sequence <= prior.Sequence)
                { Add(index - 1); first = index; gaps = 0; }
                else if (current.Sequence - prior.Sequence > 1) { gaps++; }
            }
            Add(lane.Count - 1);
        }
        return JsonSerializer.SerializeToElement(new { stream_id = stream.ToString("D"), clock_id = clock.ToString("D"),
            ticks_per_second = frequency, record_count = Decimal(count), spans });
    }
    private static string Decimal(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Decimal(int value) => value.ToString(CultureInfo.InvariantCulture);
}

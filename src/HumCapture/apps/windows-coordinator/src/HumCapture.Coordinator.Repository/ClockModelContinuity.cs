using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record ClockContinuityEvidence(int RecordsChecked, int MappedRecordsChecked, int Barriers);

// Observed declaration consistency, not proof of physical epoch/synchronization truth.
internal sealed class ClockModelContinuity
{
    private sealed class Lane
    {
        internal ulong Sequence, Ticks;
        internal uint Segment;
        internal int Generation;
        internal bool HasPrevious;
        internal readonly HashSet<uint> Segments = [];
        internal readonly Dictionary<uint, int> Models = [];
    }
    private readonly Dictionary<(Guid Clock, uint Model), uint> modelSegments = [];
    private int records, mapped, barriers;

    internal static ClockContinuityEvidence Check(TimingClockBindings bindings, TimingStream<SourceFrame>? frames,
        TimingStream<ImuSample>? imu, CancellationToken token = default)
    {
        var check = new ClockModelContinuity();
        if (frames is not null)
        {
            var stream = bindings.Stream(frames.StreamId, "FRAME_TIMESTAMPS", "timing/frame-timestamps.bin");
            var lane = new Lane();
            foreach (var frame in frames.Records)
            {
                token.ThrowIfCancellationRequested();
                bindings.Compare(stream, frame.NativeTicks, frame.HostArrivalTicks, frame.MappedSessionTicks, frame.ClockModelId, frame.UncertaintyNs);
                check.Observe(lane, stream, frame.Sequence, frame.NativeTicks, frame.SegmentId, frame.ClockModelId, frame.MappedSessionTicks.HasValue);
            }
        }
        if (imu is not null)
        {
            var stream = bindings.Stream(imu.StreamId, "IMU_SAMPLES", "imu/imu-samples.bin");
            var lanes = new Dictionary<ushort, Lane>();
            foreach (var sample in imu.Records)
            {
                token.ThrowIfCancellationRequested();
                if (!lanes.TryGetValue(sample.SensorStreamId, out var lane)) { lane = new Lane(); lanes.Add(sample.SensorStreamId, lane); }
                bindings.Compare(stream, sample.NativeTicks, sample.HostArrivalTicks, sample.MappedSessionTicks, sample.ClockModelId, sample.UncertaintyNs);
                check.Observe(lane, stream, sample.Sequence, sample.NativeTicks, sample.SegmentId, sample.ClockModelId, sample.MappedSessionTicks.HasValue);
            }
        }
        return new(check.records, check.mapped, check.barriers);
    }

    private void Observe(Lane lane, JsonElement stream, ulong sequence, ulong ticks, uint segment, uint model, bool hasMapping)
    {
        records++;
        if (lane.HasPrevious && (segment != lane.Segment || ticks < lane.Ticks || sequence < lane.Sequence))
        {
            if (segment != lane.Segment && lane.Segments.Contains(segment))
            { throw new InvalidDataException("Capture segment ID was reused within a logical stream."); }
            lane.Generation++; barriers++;
        }
        lane.Segments.Add(segment);
        if (hasMapping)
        {
            if (lane.Models.TryGetValue(model, out var generation) && generation != lane.Generation)
            { throw new InvalidDataException("Clock model crosses a restart or regression barrier."); }
            lane.Models[model] = lane.Generation;
            var key = (stream.GetProperty("native_clock_id").GetGuid(), model);
            if (modelSegments.TryGetValue(key, out var previousSegment) && previousSegment != segment)
            { throw new InvalidDataException("Clock model crosses declared capture segments."); }
            modelSegments[key] = segment; mapped++;
        }
        lane.HasPrevious = true; lane.Sequence = sequence; lane.Ticks = ticks; lane.Segment = segment;
    }
}

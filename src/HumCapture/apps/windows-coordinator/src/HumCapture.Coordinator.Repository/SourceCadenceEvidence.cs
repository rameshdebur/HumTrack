using System.Numerics;

namespace HumCapture.Coordinator.Repository;

internal sealed record RationalObservation(BigInteger Numerator, BigInteger Denominator)
{
    internal int Compare(long threshold) => Numerator.CompareTo((BigInteger)threshold * Denominator);
}

internal sealed record SourceCadenceEvidence(int RecordCount, BigInteger SequenceGaps, int SequenceDuplicates,
    int SequenceRegressions, int TimestampRegressions, int TimestampDuplicates, int SegmentChanges,
    RationalObservation? ObservedMilliHz, RationalObservation? MaximumIntervalUs)
{
    internal static SourceCadenceEvidence Measure(IReadOnlyList<SourceFrame> records, ulong ticksPerSecond,
        CancellationToken token = default)
    {
        if (ticksPerSecond == 0) { throw new InvalidDataException("Native clock frequency is zero."); }
        BigInteger gaps = 0;
        var duplicates = 0; var sequenceRegressions = 0; var timeRegressions = 0; var timeDuplicates = 0; var segments = 0;
        ulong? maximumInterval = null;
        for (var i = 1; i < records.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var previous = records[i - 1]; var current = records[i];
            if (current.SegmentId != previous.SegmentId) { segments++; continue; }
            if (current.Sequence == previous.Sequence) { duplicates++; }
            else if (current.Sequence < previous.Sequence) { sequenceRegressions++; }
            else { gaps += (BigInteger)current.Sequence - previous.Sequence - 1; }
            if (current.NativeTicks < previous.NativeTicks) { timeRegressions++; continue; }
            if (current.NativeTicks == previous.NativeTicks) { timeDuplicates++; }
            if (current.Sequence < previous.Sequence) { continue; }
            var delta = current.NativeTicks - previous.NativeTicks;
            maximumInterval = Math.Max(maximumInterval ?? 0, delta);
        }
        RationalObservation? rate = null;
        if (records.Count > 1 && segments == 0 && sequenceRegressions == 0 && duplicates == 0
            && timeRegressions == 0 && timeDuplicates == 0)
        { rate = new((BigInteger)(records.Count - 1) * ticksPerSecond * 1000, records[^1].NativeTicks - records[0].NativeTicks); }
        RationalObservation? maximum = maximumInterval.HasValue ? new((BigInteger)maximumInterval.Value * 1000000, ticksPerSecond) : null;
        return new(records.Count, gaps, duplicates, sequenceRegressions, timeRegressions, timeDuplicates, segments, rate, maximum);
    }
}

using System.Numerics;

namespace HumCapture.Coordinator.Repository;

internal sealed record GeneratedVideoFrame(ulong VideoFrameIndex, ulong SourceFrameSequence, string Reason);
internal sealed record FrameAssociationEvidence(int AcceptedFrames, int GeneratedFrames, bool AllPresentationTimesCompared);

// Caller must independently validate metadata/identity and establish the presentation clock.
// This primitive neither validates JSON nor confers package/protocol acceptance.
internal static class FrameEvidenceAssociation
{
    internal static FrameAssociationEvidence Compare(IReadOnlyList<SourceFrame> records, DecodedVideo video,
        ulong presentationTicksPerSecond, IReadOnlyList<GeneratedVideoFrame> generated, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Require(presentationTicksPerSecond > 0 && video.TimeBaseNumerator > 0 && video.TimeBaseDenominator > 0,
            "Presentation clock/time base missing.");
        Require(video.Frames.Count > 0 && video.Frames.Count <= DecodedMediaParser.MaximumFrames,
            "Decoded frame envelope invalid.");
        var covered = new HashSet<ulong>();
        var sourceCounts = new Dictionary<ulong, int>();
        var accepted = 0;
        foreach (var frame in records)
        {
            token.ThrowIfCancellationRequested();
            if (frame.Disposition != 1) { continue; }
            Require(frame.VideoFrameIndex.HasValue && frame.EncodedPtsTicks.HasValue, "Accepted frame lacks video index/PTS.");
            var index = frame.VideoFrameIndex!.Value;
            Cover(index, video, covered);
            var decoded = video.Frames[(int)index];
            // BigInteger prevents overflow and any rounding through double or nominal FPS.
            Require((BigInteger)frame.EncodedPtsTicks!.Value * video.TimeBaseDenominator
                == (BigInteger)decoded.PresentationTicks * video.TimeBaseNumerator * presentationTicksPerSecond,
                "Source/container presentation timestamps disagree.");
            sourceCounts[frame.Sequence] = sourceCounts.GetValueOrDefault(frame.Sequence) + 1;
            accepted++;
        }
        foreach (var frame in generated)
        {
            token.ThrowIfCancellationRequested();
            Require(!string.IsNullOrWhiteSpace(frame.Reason) && sourceCounts.GetValueOrDefault(frame.SourceFrameSequence) == 1,
                "Generated frame source is missing or ambiguous across segments.");
            Cover(frame.VideoFrameIndex, video, covered);
        }
        Require(covered.Count == video.Frames.Count, "Decoded frames lack complete source/transformation coverage.");
        // v1 generated-duplicate events contain no expected PTS; never invent that evidence.
        return new FrameAssociationEvidence(accepted, generated.Count, generated.Count == 0);
    }

    private static void Cover(ulong index, DecodedVideo video, HashSet<ulong> covered)
    {
        Require(index < (ulong)video.Frames.Count && covered.Add(index), "Duplicate or out-of-range video frame index.");
        Require(video.Frames[(int)index].PresentationIndex == (int)index, "Decoded presentation order mismatch.");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) { throw new InvalidDataException(reason); } }
}

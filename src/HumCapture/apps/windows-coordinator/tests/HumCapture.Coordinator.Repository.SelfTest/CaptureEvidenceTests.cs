using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class CaptureEvidenceTests
{
    private static JsonElement Vectors()
    {
        using var doc = JsonDocument.Parse(ImuEvidenceTests.Fixture("capture-evidence-vectors.json"));
        return doc.RootElement.Clone();
    }
    private static string Text(JsonElement v, string name) => v.GetProperty(name).GetString()!;
    private static CaptureFinalizationExpectation Expectation(JsonElement manifest)
    {
        var identity = new CaptureIdentity(Text(manifest, "package_id"), Text(manifest, "session_id"),
            Text(manifest, "trial_id"), Text(manifest, "source_id"), Text(manifest, "source_boot_id"),
            Text(manifest, "capture_attempt_id"), Text(manifest, "source_kind"));
        ArtifactDigest Artifact(string role)
        {
            var item = manifest.GetProperty("artifacts").EnumerateArray().Single(a => Text(a, "role") == role);
            return new ArtifactDigest(Text(item, "artifact_id"), long.Parse(Text(item, "byte_length"), System.Globalization.CultureInfo.InvariantCulture), Text(item, "sha256"));
        }
        return new CaptureFinalizationExpectation(identity, Artifact("CAPTURE_EVENTS"), Artifact("FINALIZATION_RECORD"),
            Text(manifest, "finalization_outcome"), Text(manifest, "finalized_utc"),
            manifest.TryGetProperty("finalization_reason", out var reason) ? reason.GetString() : null);
    }
    internal static void SharedFinalizationVectors()
    {
        foreach (var vector in Vectors().GetProperty("vectors").EnumerateArray())
        {
            var expected = Expectation(vector.GetProperty("manifest"));
            var archive = Convert.FromBase64String(Text(vector, "archive")); var summary = Convert.FromBase64String(Text(vector, "summary"));
            CaptureFinalizationResult Run() => new CaptureFinalizationEvidence().Compare(expected, archive, summary);
            if (!vector.GetProperty("accepted").GetBoolean()) { ImuEvidenceTests.Reject(() => Run()); continue; }
            var result = Run();
            if (result.FinalizationOutcome != expected.Outcome || (Text(vector, "id") == "incomplete-partial-gap" && result.ObservedEventGaps != 1))
            { throw new InvalidOperationException("Finalization result diverged from shared vector."); }
        }
    }
    private static ArtifactDigest Digest(string id, byte[] bytes) => new(id, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static byte[] Canonical(JsonNode value)
    {
        using var doc = JsonDocument.Parse(value.ToJsonString());
        return CaptureCanonicalJson.Encode(doc.RootElement);
    }
    private static (CaptureFinalizationExpectation Expected, byte[] Archive, byte[] Summary) Bundle(bool complete = true, string kind = "UVC")
    {
        var vector = Vectors().GetProperty("vectors")[complete ? 0 : 1];
        var expected = Expectation(vector.GetProperty("manifest"));
        var archive = JsonNode.Parse(Convert.FromBase64String(Text(vector, "archive")))!;
        var summary = JsonNode.Parse(Convert.FromBase64String(Text(vector, "summary")))!;
        var attempt = ImuEvidenceTests.Samples().CaptureAttemptId;
        archive["capture_attempt_id"] = attempt.ToString(); summary["capture_attempt_id"] = attempt.ToString();
        archive["source_kind"] = kind; summary["source_kind"] = kind;
        var archiveBytes = Canonical(archive); var archiveDigest = Digest(expected.Archive.ArtifactId, archiveBytes);
        summary["event_archive_sha256"] = archiveDigest.Sha256; var summaryBytes = Canonical(summary);
        expected = expected with { Identity = expected.Identity with { CaptureAttemptId = attempt.ToString(), SourceKind = kind }, Archive = archiveDigest,
            Summary = Digest(expected.Summary.ArtifactId, summaryBytes) };
        return (expected, archiveBytes, summaryBytes);
    }
    internal static void CanonicalAndBindingGuards()
    {
        var bundle = Bundle();
        foreach (var text in new[] { Encoding.UTF8.GetString(bundle.Archive) + " ",
            Encoding.UTF8.GetString(bundle.Archive).Replace("{", "{ ", StringComparison.Ordinal),
            Encoding.UTF8.GetString(bundle.Archive).Replace("\"schema_version\":\"1.0.0\"", "\"schema_version\":\"1.0.0\",\"schema_version\":\"1.0.0\"", StringComparison.Ordinal) })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            // Rebind hash to isolate canonical checks rather than merely rejecting stale digests.
            var expected = bundle.Expected with { Archive = Digest(bundle.Expected.Archive.ArtifactId, bytes) };
            ImuEvidenceTests.Reject(() => new CaptureFinalizationEvidence().Compare(expected, bytes, bundle.Summary));
        }
        foreach (var raw in new[] { "\"\\ud800\"", "\"\\udfff\"", "9007199254740992", "1.25" })
        {
            using var doc = JsonDocument.Parse(raw);
            ImuEvidenceTests.Reject(() => CaptureCanonicalJson.Encode(doc.RootElement));
        }
        ImuEvidenceTests.Reject(() => new CaptureFinalizationEvidence().Compare(bundle.Expected with { Profile = "unknown" }, bundle.Archive, bundle.Summary));
        ImuEvidenceTests.Reject(() => new CaptureFinalizationEvidence().Compare(bundle.Expected with { Archive = bundle.Expected.Archive with { ByteLength = 1 } }, bundle.Archive, bundle.Summary));
    }
    private static BoundEvidenceArtifact Bound(byte[] bytes) => new(Digest(Guid.NewGuid().ToString(), bytes), bytes);
    private static ScientificEvidenceInput Scientific(bool imu)
    {
        var timing = FrameMetadataTests.Timing();
        // Synthetic shared-clock declaration for these standalone binary fixtures only.
        timing["streams"]![1]!["native_clock_id"] = timing["streams"]![0]!["native_clock_id"]!.DeepClone();
        timing["clock_models"]![0]!["uncertainty_ns"] = 100000;
        var master = Bound(Encoding.UTF8.GetBytes("SYNTHETIC OBSERVATIONS; NOT DECODED MEDIA"));
        var frames = new FrameEvidenceInput(master, Bound(ImuEvidenceTests.Fixture("valid-frame-timestamps.bin")),
            Bound(ImuEvidenceTests.Fixture("valid-camera-metadata.json")),
            new DecodedMediaEvidence(master.Digest.Sha256, master.Digest.ByteLength, FrameMetadataTests.Video()));
        return new ScientificEvidenceInput(Bound(ImuEvidenceTests.Bytes(timing)), TimingMetadataVersion.LegacyV1, frames,
            imu ? new ImuEvidenceInput(Bound(ImuEvidenceTests.Fixture("valid-imu-samples.bin")), Bound(ImuEvidenceTests.Fixture("valid-imu-metadata.json"))) : null);
    }
    internal static void CombinedSyntheticEvidence()
    {
        var verifier = new InternalCaptureEvidence();
        foreach (var kind in new[] { "UVC", "ANDROID" })
        {
            var b = Bundle(kind: kind); var input = Scientific(kind == "ANDROID");
            var result = verifier.Compare(b.Expected, b.Archive, b.Summary, input);
            if (result.Frames?.Association.AcceptedFrames != 3 || (kind == "ANDROID" && result.Imu?.SampleCount != 3)
                || !result.NotAssessed.Contains("FRAME_MAPPED_VALUE_COMPARISON") || !result.NotAssessed.Contains("DECODER_PROVENANCE"))
            { throw new InvalidOperationException("Combined evidence overstated or omitted comparisons."); }
        }
        var incomplete = Bundle(false);
        var partial = verifier.Compare(incomplete.Expected, incomplete.Archive, incomplete.Summary, null);
        if (partial.Finalization.FinalizationOutcome != "FINALIZED_INCOMPLETE" || partial.Frames is not null || partial.Imu is not null)
        { throw new InvalidOperationException("Incomplete capture promoted to completion."); }
        _ = verifier.Compare(incomplete.Expected, incomplete.Archive, incomplete.Summary, Scientific(true) with { Frames = null });
    }
    internal static void CombinedFailurePaths()
    {
        var b = Bundle(); var scientific = Scientific(true); var verifier = new InternalCaptureEvidence();
        void Reject(ScientificEvidenceInput? input) => ImuEvidenceTests.Reject(() => verifier.Compare(b.Expected, b.Archive, b.Summary, input));
        Reject(null); Reject(scientific with { Frames = null });
        var frames = scientific.Frames!;
        Reject(scientific with { Frames = frames with { Decoded = frames.Decoded with { MediaSha256 = new string('0', 64) } } });
        Reject(scientific with { Frames = frames with { Master = frames.Master with { Bytes = new byte[] { 1, 2 } } } });
        Reject(scientific with { Timing = scientific.Timing with { Digest = scientific.Timing.Digest with { ArtifactId = b.Expected.Archive.ArtifactId } } });
        Reject(scientific with { Version = TimingMetadataVersion.ExactV1_1 });
        var foreign = JsonNode.Parse(scientific.Timing.Bytes.Span)!; foreign["capture_attempt_id"] = Guid.NewGuid().ToString();
        Reject(scientific with { Timing = Bound(ImuEvidenceTests.Bytes(foreign)) });
        var android = Bundle(kind: "ANDROID");
        ImuEvidenceTests.Reject(() => verifier.Compare(android.Expected, android.Archive, android.Summary, Scientific(false)));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { verifier.Compare(b.Expected, b.Archive, b.Summary, scientific, cancelled.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Combined cancellation ignored.");
    }
}

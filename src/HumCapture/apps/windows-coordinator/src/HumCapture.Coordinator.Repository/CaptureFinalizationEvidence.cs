using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record CaptureIdentity(string PackageId, string SessionId, string TrialId, string SourceId,
    string SourceBootId, string CaptureAttemptId, string SourceKind);
internal sealed record ArtifactDigest(string ArtifactId, long ByteLength, string Sha256)
{
    internal void Compare(ReadOnlyMemory<byte> bytes)
    {
        if (!Guid.TryParse(ArtifactId, out var artifactId) || artifactId == Guid.Empty || ByteLength != bytes.Length
            || Sha256 != Convert.ToHexStringLower(SHA256.HashData(bytes.Span)))
        { throw new InvalidDataException("Artifact identity/length/hash mismatch."); }
    }
}
// Trusted projection AFTER manifest/profile admission: not an external input API.
internal sealed record CaptureFinalizationExpectation(CaptureIdentity Identity, ArtifactDigest Archive,
    ArtifactDigest Summary, string Outcome, string FinalizedUtc, string? IncompleteReason,
    string Profile = "HC-IF-ART-001@1.0.0");
internal sealed record CaptureFinalizationResult(string ArchiveCompleteness, int ObservedEventGaps, string FinalizationOutcome);

internal sealed class CaptureFinalizationEvidence
{
    private readonly MetadataSchemaValidator schemas = new();

    internal CaptureFinalizationResult Compare(CaptureFinalizationExpectation expected,
        ReadOnlyMemory<byte> archiveBytes, ReadOnlyMemory<byte> summaryBytes, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        Require(expected.Profile == "HC-IF-ART-001@1.0.0", "Unsupported capture artifact profile.");
        expected.Archive.Compare(archiveBytes); expected.Summary.Compare(summaryBytes);
        Require(expected.Archive.ArtifactId != expected.Summary.ArtifactId, "Duplicate capture artifact identity.");
        var archive = Canonical(MetadataKind.CaptureEvents, archiveBytes, token);
        var summary = Canonical(MetadataKind.Finalization, summaryBytes, token);
        CheckIdentity(archive, expected.Identity); CheckIdentity(summary, expected.Identity);
        Require(Text(summary, "event_archive_artifact_id") == expected.Archive.ArtifactId
            && Text(summary, "event_archive_sha256") == expected.Archive.Sha256, "Summary does not bind exact archive.");
        var outcome = Text(summary, "outcome"); var completeness = Text(archive, "completeness");
        Require(outcome == expected.Outcome && Text(summary, "finalized_utc") == expected.FinalizedUtc
            && (outcome != "FINALIZED_INCOMPLETE" || Text(summary, "reason") == expected.IncompleteReason), "Finalization differs from manifest projection.");
        Require(outcome != "FINALIZED_COMPLETE" || completeness == "COMPLETE", "Partial archive cannot establish complete finalization.");
        var events = archive.GetProperty("events").EnumerateArray().ToArray();
        var firstSamples = events.Count(e => Text(e, "event_type") == "FIRST_MASTER_SAMPLE");
        Require(firstSamples <= 1 && (outcome != "FINALIZED_COMPLETE" || firstSamples == 1), "Invalid first-master event count.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var gaps = 0;
        for (var index = 0; index < events.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var item = events[index]; var time = item.GetProperty("event_source_time"); var ticks = Ticks(time);
            Require(ids.Add(Text(item, "event_id")), "Duplicate event identity.");
            Require(Text(item, "event_type") != "FIRST_MASTER_SAMPLE" || Text(item, "resulting_state") == "RECORDING", "First sample must enter recording.");
            Require(index == events.Length - 1 || Text(item, "event_type") != "FINALIZATION_RESULT", "Multiple terminal events.");
            if (index == 0) { continue; }
            var prior = events[index - 1]; var priorTime = prior.GetProperty("event_source_time");
            var delta = item.GetProperty("event_sequence").GetInt64() - prior.GetProperty("event_sequence").GetInt64();
            var revision = item.GetProperty("resulting_source_revision").GetInt64();
            var priorRevision = prior.GetProperty("resulting_source_revision").GetInt64();
            Require(delta > 0 && revision >= priorRevision
                && (Text(item, "prior_state") == Text(item, "resulting_state") || revision > priorRevision), "Event sequence/revision regressed.");
            Require(Text(time, "clock_id") == Text(priorTime, "clock_id")
                && time.GetProperty("ticks_per_second").GetUInt64() == priorTime.GetProperty("ticks_per_second").GetUInt64()
                && ticks >= Ticks(priorTime), "Event clock changed or native time regressed.");
            if (delta != 1) { gaps++; }
            Require(completeness != "COMPLETE" || (delta == 1 && Text(item, "prior_state") == Text(prior, "resulting_state")), "Complete archive has event gap/broken adjacency.");
        }
        var terminal = events[^1];
        Require(Text(terminal, "event_type") == "FINALIZATION_RESULT"
            && Text(terminal, "event_id") == Text(summary, "terminal_event_id")
            && Text(terminal, "prior_state") == "FINALIZING" && Text(terminal, "resulting_state") == outcome
            && terminal.TryGetProperty("reason", out var reason) && reason.GetString() == Text(summary, "reason"), "Terminal event does not prove summary.");
        return new CaptureFinalizationResult(completeness, gaps, outcome);
    }

    private JsonElement Canonical(MetadataKind kind, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        var value = schemas.Validate(kind, bytes, token);
        Require(bytes.Span.SequenceEqual(CaptureCanonicalJson.Encode(value, token)), "Noncanonical capture artifact.");
        return value;
    }
    private static void CheckIdentity(JsonElement value, CaptureIdentity expected)
    {
        Require(Text(value, "package_id") == expected.PackageId
            && Text(value, "session_id") == expected.SessionId
            && Text(value, "trial_id") == expected.TrialId
            && Text(value, "source_id") == expected.SourceId
            && Text(value, "source_boot_id") == expected.SourceBootId
            && Text(value, "capture_attempt_id") == expected.CaptureAttemptId
            && Text(value, "source_kind") == expected.SourceKind, "Capture identity mismatch.");
    }
    private static ulong Ticks(JsonElement time)
    {
        Require(ulong.TryParse(Text(time, "ticks"), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks), "Event time overflow.");
        return ticks;
    }
    private static string Text(JsonElement value, string key) => value.GetProperty(key).GetString()!;
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidDataException(message); } }
}

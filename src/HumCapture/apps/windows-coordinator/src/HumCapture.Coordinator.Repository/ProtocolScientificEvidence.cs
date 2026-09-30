using System.Numerics;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

// Assignment is a coordinator projection, not an authenticated admission endpoint.
internal sealed record ProtocolEvidenceContext(ReadOnlyMemory<byte> SnapshotBytes, Guid SourceId, Guid RoleId)
{
    internal ProtocolEvidenceContext Freeze()
    {
        PackageInputManifest.Need(SnapshotBytes.Length > 0 && SnapshotBytes.Length <= MetadataSchemaValidator.MaximumBytes,
            "Protocol snapshot exceeds input envelope.");
        return this with { SnapshotBytes = SnapshotBytes.ToArray() };
    }
}
internal sealed record ScientificCondition(string RuleId, string Metric, string Level, string Outcome, string EvidenceKind);
internal sealed record ProtocolScientificEvidence(string Outcome, string Reason, IReadOnlyList<ScientificCondition> Conditions)
{
    internal static string Aggregate(IReadOnlyList<ScientificCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            Need(condition.Level is "REQUIRED" or "PREFERRED" or "INFORMATIONAL"
                && condition.Outcome is "PASS" or "FAIL" or "NOT_ASSESSED", "Unsupported scientific condition.");
        }
        if (conditions.Any(c => c.Level == "REQUIRED" && c.Outcome == "FAIL")) { return "REJECTED"; }
        if (conditions.Any(c => c.Level == "REQUIRED" && c.Outcome != "PASS")
            || !conditions.Any(c => c.Level is "REQUIRED" or "PREFERRED")) { return "REVIEW_REQUIRED"; }
        if (conditions.Any(c => c.Level == "PREFERRED" && c.Outcome != "PASS")) { return "DEGRADED_ACCEPTABLE"; }
        return "CONFORMANT";
    }

    internal static ProtocolScientificEvidence Evaluate(ProtocolEvidenceContext? context, JsonElement manifest,
        SourceCadenceEvidence? cadence, JsonElement? camera, DecodedVideo? video, bool cameraClockBound, CancellationToken token)
    {
        if (context is null) { return new("REVIEW_REQUIRED", "PROTOCOL_RULES_NOT_SUPPLIED", []); }
        var snapshot = ReadSnapshot(context.SnapshotBytes, token);
        Need(context.SourceId == manifest.GetProperty("source_id").GetGuid(), "Protocol assignment source differs from package.");
        Need(snapshot.GetProperty("protocol_snapshot_id").GetGuid() == manifest.GetProperty("protocol_snapshot_id").GetGuid()
            && snapshot.GetProperty("snapshot_content_sha256").GetString() == manifest.GetProperty("protocol_snapshot_content_sha256").GetString(),
            "Protocol snapshot differs from package binding.");
        var roles = new Dictionary<Guid, JsonElement>();
        foreach (var role in snapshot.GetProperty("source_roles").EnumerateArray())
        { Need(roles.TryAdd(role.GetProperty("role_id").GetGuid(), role), "Duplicate protocol role."); }
        Need(roles.TryGetValue(context.RoleId, out var selected), "Unknown assigned protocol role.");
        var sourceKind = manifest.GetProperty("source_kind").GetString() == "UVC" ? "WINDOWS_UVC" : "ANDROID_CAMERA2";
        Need(selected.GetProperty("allowed_source_kinds").EnumerateArray().Any(kind => kind.GetString() == sourceKind), "Protocol role excludes source kind.");
        if (snapshot.GetProperty("schema_version").GetString() == "1.0.0")
        { return new("REVIEW_REQUIRED", "LEGACY_PROTOCOL_HAS_NO_SCIENTIFIC_RULES", []); }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var conditions = new List<ScientificCondition>();
        foreach (var rule in snapshot.GetProperty("scientific_rules").EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            var id = rule.GetProperty("rule_id").GetString()!;
            Need(ids.Add(id) && roles.ContainsKey(rule.GetProperty("role_id").GetGuid()), "Duplicate rule or unknown rule role.");
            ValidateRule(rule);
            if (rule.GetProperty("role_id").GetGuid() != context.RoleId) { continue; }
            var metric = rule.GetProperty("metric").GetString()!;
            var outcome = "NOT_ASSESSED";
            var threshold = rule.GetProperty("threshold");
            var numeric = Numeric(metric, cadence, camera, video);
            if (numeric is not null)
            {
                var comparison = numeric.Compare(threshold.GetInt64());
                var passed = rule.GetProperty("operator").GetString() switch { "GE" => comparison >= 0, "LE" => comparison <= 0, _ => comparison == 0 };
                outcome = passed ? "PASS" : "FAIL";
            }
            else if (camera.HasValue && (metric == "RATE_CONTROL_CLASS" || metric == "TIMESTAMP_PROVENANCE" && cameraClockBound))
            {
                var reported = metric == "TIMESTAMP_PROVENANCE" ? camera.Value.GetProperty("timestamp_provenance").GetString()
                    : camera.Value.GetProperty("negotiated_mode").GetProperty("rate_control_class").GetString();
                outcome = reported == threshold.GetString() ? "PASS" : "FAIL";
            }
            var evidenceKind = metric switch
            {
                "TIMESTAMP_PROVENANCE" or "RATE_CONTROL_CLASS" or "FOCAL_LENGTH_PRESENT" => "REPORTED_METADATA",
                "DECODED_WIDTH" or "DECODED_HEIGHT" => "DECODED_OBSERVATION",
                _ => "NATIVE_FRAME_TIMESTAMPS"
            };
            conditions.Add(new(id, metric, rule.GetProperty("level").GetString()!, outcome, evidenceKind));
        }
        return new(Aggregate(conditions), conditions.Count == 0 ? "NO_RULES_FOR_ASSIGNED_ROLE" : "RULE_SUBSET_ONLY_NOT_TAKE_ACCEPTANCE", conditions.AsReadOnly());
    }

    private static JsonElement ReadSnapshot(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        Need(bytes.Length > 0 && bytes.Length <= MetadataSchemaValidator.MaximumBytes, "Protocol snapshot exceeds input envelope.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 64 });
        var version = document.RootElement.GetProperty("schema_version").GetString();
        var kind = version switch
        {
            "1.0.0" => MetadataKind.Protocol, "1.1.0" => MetadataKind.ProtocolScientific,
            _ => throw new InvalidDataException("Unsupported protocol snapshot version.")
        };
        var snapshot = new MetadataSchemaValidator().Validate(kind, bytes, token);
        Need(bytes.Span.SequenceEqual(CaptureCanonicalJson.Encode(snapshot, token)), "Noncanonical protocol snapshot.");
        Need(PackageInputManifest.Hash(CaptureCanonicalJson.Encode(snapshot, token, "snapshot_content_sha256"))
            == snapshot.GetProperty("snapshot_content_sha256").GetString(), "Protocol snapshot content hash mismatch.");
        return snapshot;
    }

    private static void ValidateRule(JsonElement rule)
    {
        var metric = rule.GetProperty("metric").GetString();
        var threshold = rule.GetProperty("threshold");
        if (metric is "TIMESTAMP_PROVENANCE" or "RATE_CONTROL_CLASS")
        {
            Need(rule.GetProperty("operator").GetString() == "EQ" && threshold.ValueKind == JsonValueKind.String, "Categorical rule requires string equality.");
            var values = metric == "RATE_CONTROL_CLASS" ? new[] { "FIXED", "VARIABLE", "ADAPTIVE", "UNKNOWN" }
                : new[] { "ANDROID_SENSOR_TIMESTAMP", "MF_DEVICE_TIMESTAMP", "MF_SAMPLE_TIME", "HOST_SAMPLE", "HOST_ARRIVAL" };
            Need(Array.Exists(values, value => value == threshold.GetString()), "Unsupported categorical threshold.");
        }
        else
        {
            Need(threshold.ValueKind == JsonValueKind.Number && threshold.TryGetInt64(out _), "Numeric rule requires integer threshold.");
            if (metric == "FOCAL_LENGTH_PRESENT")
            { Need(rule.GetProperty("operator").GetString() == "EQ" && threshold.GetInt64() is 0 or 1, "Presence rule requires equality to zero or one."); }
        }
    }

    private static RationalObservation? Numeric(string metric, SourceCadenceEvidence? cadence, JsonElement? camera, DecodedVideo? video)
    {
        if (metric == "DECODED_WIDTH") { return video is null ? null : new(video.Width, 1); }
        if (metric == "DECODED_HEIGHT") { return video is null ? null : new(video.Height, 1); }
        if (metric == "FOCAL_LENGTH_PRESENT")
        {
            if (!camera.HasValue) { return null; }
            var present = camera.Value.TryGetProperty("lens", out var lens) && lens.TryGetProperty("focal_length_mm", out _);
            if (present) { return new(1, 1); }
            var reportedUnavailable = camera.Value.GetProperty("unavailable_fields").EnumerateArray()
                .Any(item => item.GetProperty("field").GetString() == "lens.focal_length_mm");
            return reportedUnavailable ? new(0, 1) : null;
        }
        if (cadence is null || cadence.RecordCount == 0) { return null; }
        if (metric == "OBSERVED_MILLIHZ") { return cadence.ObservedMilliHz; }
        if (metric == "MAX_INTERVAL_US")
        { return cadence.SegmentChanges + cadence.SequenceRegressions + cadence.TimestampRegressions > 0 ? null : cadence.MaximumIntervalUs; }
        BigInteger? value = metric switch
        {
            "SEQUENCE_GAPS" => cadence.SequenceGaps, "SEQUENCE_DUPLICATES" => cadence.SequenceDuplicates,
            "SEQUENCE_REGRESSIONS" => cadence.SequenceRegressions, "TIMESTAMP_REGRESSIONS" => cadence.TimestampRegressions,
            "TIMESTAMP_DUPLICATES" => cadence.TimestampDuplicates, _ => null
        };
        return value.HasValue ? new(value.Value, 1) : null;
    }
    private static void Need(bool condition, string message)
    { if (!condition) { throw new InvalidDataException(message); } }
}

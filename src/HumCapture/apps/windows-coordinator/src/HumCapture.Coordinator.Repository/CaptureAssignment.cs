using System.Text.Json;
using System.Text.Json.Serialization;

namespace HumCapture.Coordinator.Repository;

internal sealed record CaptureAssignment(Guid AssignmentId, Guid SubjectId, Guid SessionId, Guid TrialId,
    Guid SlotId, Guid SourceId, Guid RoleId, Guid CaptureAttemptId, Guid SourceBootId,
    string SourceKind, Guid ConfigurationId, string ConfigurationSha256, byte[] ProtocolUtf8,
    string SchemaVersion = "1.0.0")
{
    internal CaptureAssignment Freeze()
    {
        ArgumentNullException.ThrowIfNull(ProtocolUtf8);
        return this with { ProtocolUtf8 = ProtocolUtf8.ToArray() };
    }
    internal ProtocolEvidenceContext Context => new(ProtocolUtf8, SourceId, RoleId);

    internal void Validate()
    {
        Guid[] ids = [AssignmentId, SubjectId, SessionId, TrialId, SlotId, SourceId, RoleId, CaptureAttemptId, SourceBootId, ConfigurationId];
        PackageInputManifest.Need(SchemaVersion == "1.0.0" && Array.TrueForAll(ids, id => id != Guid.Empty)
            && SourceKind is "UVC" or "ANDROID" && ConfigurationSha256 is not null && ConfigurationSha256.Length == 64
            && ConfigurationSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "Invalid capture assignment.");
        PackageInputManifest.Need(ProtocolUtf8.Length is > 0 and <= 16777216, "Protocol snapshot exceeds envelope.");
        using var document = JsonDocument.Parse(ProtocolUtf8);
        var protocol = document.RootElement;
        var projection = JsonSerializer.SerializeToElement(new { source_id = SourceId, source_kind = SourceKind,
            protocol_snapshot_id = protocol.GetProperty("protocol_snapshot_id").GetGuid(),
            protocol_snapshot_content_sha256 = protocol.GetProperty("snapshot_content_sha256").GetString() });
        _ = ProtocolScientificEvidence.Evaluate(Context, projection, null, null, null, false, default);
        var count = protocol.GetProperty("selected_source_count").GetInt32();
        var policy = protocol.GetProperty("source_count_policy");
        var validCount = policy.GetProperty("mode").GetString() == "FIXED"
            ? count == policy.GetProperty("count").GetInt32()
            : count >= policy.GetProperty("minimum").GetInt32() && count <= policy.GetProperty("maximum").GetInt32();
        PackageInputManifest.Need(validCount && protocol.GetProperty("source_roles").GetArrayLength() == count, "Selected source-count policy disagrees.");
        var slots = protocol.GetProperty("trial_slots").EnumerateArray().Select(s => s.GetProperty("slot_id").GetGuid()).ToArray();
        PackageInputManifest.Need(slots.Distinct().Count() == slots.Length && slots.Contains(SlotId), "Assignment slot is absent or ambiguous.");
    }

    internal void Bind(JsonElement manifest)
    {
        (string Key, Guid Expected)[] ids = [("subject_id", SubjectId), ("session_id", SessionId), ("trial_id", TrialId),
            ("source_id", SourceId), ("capture_attempt_id", CaptureAttemptId), ("source_boot_id", SourceBootId), ("configuration_id", ConfigurationId)];
        foreach (var (key, expected) in ids)
        { PackageInputManifest.Need(manifest.GetProperty(key).GetGuid() == expected, "COORDINATOR_ASSIGNMENT_MISMATCH:" + key); }
        PackageInputManifest.Need(manifest.GetProperty("source_kind").GetString() == SourceKind
            && manifest.GetProperty("configuration_content_sha256").GetString() == ConfigurationSha256,
            "COORDINATOR_ASSIGNMENT_MISMATCH:configuration/source-kind");
        _ = ProtocolScientificEvidence.Evaluate(Context, manifest, null, null, null, false, default);
    }
}

internal static class WorkflowJson
{
    internal const int MaximumBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    internal static byte[] Encode<T>(T value)
    {
        var bytes = CaptureCanonicalJson.Encode(JsonSerializer.SerializeToElement(value, Options));
        PackageInputManifest.Need(bytes.Length <= MaximumBytes, "Workflow payload exceeds envelope.");
        return bytes;
    }
    internal static T Read<T>(byte[] bytes)
    {
        PackageInputManifest.Need(bytes.Length > 0 && bytes.Length <= MaximumBytes, "Workflow payload exceeds envelope.");
        var value = JsonSerializer.Deserialize<T>(bytes, Options) ?? throw new InvalidDataException("Missing workflow payload.");
        PackageInputManifest.Need(bytes.AsSpan().SequenceEqual(Encode(value)), "Workflow payload is not canonical.");
        return value;
    }
    internal static T ReadFile<T>(string path)
    {
        PackageInputManifest.Need(Path.IsPathFullyQualified(path), "Absolute request path required.");
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        using var handle = PackageInputLease.OpenHandle(path, 0x80000000, 1, 0x00200000);
        PackageInputLease.CheckHandle(handle, false);
        using var input = new FileStream(handle, FileAccess.Read);
        PackageInputManifest.Need(input.Length is > 0 and <= MaximumBytes, "Request envelope exceeded.");
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return Read<T>(bytes);
    }
}

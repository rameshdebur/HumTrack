using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record PackageInputArtifact(string Role, string Path, string MediaType, string? Version, bool Required, ArtifactDigest Digest);
internal sealed record PackageInputManifest(JsonElement Json, CaptureFinalizationExpectation Finalization,
    TimingMetadataVersion? TimingVersion, IReadOnlyList<PackageInputArtifact> Artifacts)
{
    internal PackageInputArtifact? Find(string role) => Artifacts.SingleOrDefault(a => a.Role == role);

    internal static PackageInputManifest Parse(ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        var value = new MetadataSchemaValidator().Validate(MetadataKind.PackageManifest, bytes, token);
        Need(bytes.Span.SequenceEqual(CaptureCanonicalJson.Encode(value, token)), "Manifest bytes must be canonical JSON.");
        Need(Text(value, "package_content_sha256") == Hash(CaptureCanonicalJson.Encode(value, token, "package_content_sha256")), "Manifest content hash mismatch.");
        foreach (var key in new[] { "package_id", "subject_id", "session_id", "trial_id", "source_id", "source_boot_id", "capture_attempt_id", "protocol_snapshot_id", "configuration_id" })
        { CheckId(Text(value, key)); }
        var profiles = value.TryGetProperty("interface_profiles", out var declarations)
            ? declarations.EnumerateArray().Select(p => p.GetString()!).ToArray() : [];
        string[] supported = ["HC-IF-ART-001@1.0.0", "HC-IF-TIM-001@1.0.0", "HC-IF-TIM-001@1.1.0", "HC-IF-XFR-001@1.2.0"];
        Need(profiles.Contains("HC-IF-ART-001@1.0.0") && Array.TrueForAll(profiles, supported.Contains), "Unsupported or missing capture package profile.");
        var timingProfiles = profiles.Where(p => p.StartsWith("HC-IF-TIM-001@", StringComparison.Ordinal)).ToArray();
        Need(timingProfiles.Length <= 1, "Conflicting timing profiles.");
        TimingMetadataVersion? version = null;
        if (timingProfiles.Length == 1)
        { version = timingProfiles[0].EndsWith("@1.1.0", StringComparison.Ordinal) ? TimingMetadataVersion.ExactV1_1 : TimingMetadataVersion.LegacyV1; }
        var artifactArray = value.GetProperty("artifacts");
        Need(artifactArray.GetArrayLength() <= 256 && value.GetProperty("artifact_count").GetInt64() == artifactArray.GetArrayLength(), "Artifact count exceeds envelope or differs.");
        var artifacts = new List<PackageInputArtifact>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); ulong total = 0;
        foreach (var item in artifactArray.EnumerateArray())
        {
            token.ThrowIfCancellationRequested(); var id = Text(item, "artifact_id"); CheckId(id);
            var path = Text(item, "relative_path"); StagedPackageEvidenceValidator.ValidateArtifactPath(path);
            Need(ids.Add(id) && paths.Add(path), "Duplicate artifact identity/path.");
            foreach (var key in new[] { "session_id", "trial_id", "source_id", "capture_attempt_id" })
            { Need(Text(value, key) == Text(item, key), "Artifact identity differs from manifest."); }
            var length = U64(item, "byte_length");
            Need(length <= long.MaxValue && total <= ulong.MaxValue - length, "Artifact/package length exceeds supported range."); total += length;
            var role = Text(item, "role");
            if (role is "SCIENTIFIC_MASTER_VIDEO" or "FRAME_TIMESTAMPS" or "IMU_SAMPLES")
            { Need(item.TryGetProperty("timing_coverage", out _), "Timed artifact lacks coverage declaration."); }
            if (item.TryGetProperty("timing_coverage", out var coverage))
            {
                Need(U64(coverage, "first_ticks") <= U64(coverage, "last_ticks"), "Reversed timing coverage.");
                _ = U64(coverage, "record_count"); _ = U64(coverage, "discontinuity_count");
                if (coverage.TryGetProperty("first_sequence", out _)) { _ = U64(coverage, "first_sequence"); }
                if (coverage.TryGetProperty("last_sequence", out _)) { _ = U64(coverage, "last_sequence"); }
            }
            artifacts.Add(new PackageInputArtifact(role, path, Text(item, "media_type"),
                item.TryGetProperty("format_version", out var format) ? format.GetString() : null,
                item.GetProperty("required").GetBoolean(), new ArtifactDigest(id, (long)length, Text(item, "sha256"))));
        }
        Need(total == U64(value, "package_byte_length"), "Package length differs from artifact sum.");
        var inventory = string.Concat(artifacts.Select(a => $"{a.Path}\t{a.Digest.ByteLength.ToString(CultureInfo.InvariantCulture)}\t{a.Digest.Sha256}\n").Order(StringComparer.Ordinal));
        Need(Hash(Encoding.UTF8.GetBytes(inventory)) == Text(value, "artifact_set_sha256"), "Artifact inventory hash mismatch.");
        var complete = Text(value, "finalization_outcome") == "FINALIZED_COMPLETE";
        var android = Text(value, "source_kind") == "ANDROID";
        foreach (var group in artifacts.GroupBy(a => a.Role))
        {
            if (group.Key is "CALIBRATION" or "AUXILIARY_EVIDENCE")
            { Need(!group.Any(a => a.Required), "Required auxiliary/calibration semantics are not implemented."); }
            else { Need(group.Count() == 1, "Ambiguous duplicate artifact role."); }
        }
        PackageInputArtifact? Get(string role) => artifacts.SingleOrDefault(a => a.Role == role);
        void Profile(string role, string media, string format, string? path, bool mandatory)
        {
            var item = Get(role);
            Need(!mandatory || (item is not null && item.Required), "Required profile artifact missing or optional: " + role);
            if (item is null) { return; }
            Need(item.MediaType == media && item.Version == format && (path is null || item.Path == path), "Artifact profile mismatch: " + role);
        }
        Profile("CAPTURE_EVENTS", "application/json", "1.0.0", null, true);
        Profile("FINALIZATION_RECORD", "application/json", "1.0.0", null, true);
        var scientific = artifacts.Exists(a => a.Role is "FRAME_TIMESTAMPS" or "TIMING_METADATA" or "CAMERA_METADATA" or "IMU_METADATA" or "IMU_SAMPLES" or "SCIENTIFIC_MASTER_VIDEO");
        Need(!(complete || scientific) || version.HasValue, "Scientific artifacts need an explicit supported timing profile.");
        Profile("FRAME_TIMESTAMPS", "application/vnd.humcapture.frame-timestamps", "1.0", "timing/frame-timestamps.bin", complete);
        Profile("TIMING_METADATA", "application/json", version == TimingMetadataVersion.ExactV1_1 ? "1.1.0" : "1.0.0", "metadata/timing-metadata.json", complete);
        Profile("CAMERA_METADATA", "application/json", "1.0.0", "metadata/camera-metadata.json", complete);
        Profile("IMU_SAMPLES", "application/vnd.humcapture.imu-samples", "1.0", "imu/imu-samples.bin", complete && android);
        Profile("IMU_METADATA", "application/json", "1.0.0", "imu/imu-metadata.json", complete && android);
        var master = Get("SCIENTIFIC_MASTER_VIDEO");
        Need(!complete || (master is not null && master.Required), "Complete package lacks required master.");
        if (master is not null) { Need(master.MediaType == "video/mp4" && Path.GetExtension(master.Path).Equals(".mp4", StringComparison.OrdinalIgnoreCase), "Unsupported master container."); }
        var identity = new CaptureIdentity(Text(value, "package_id"), Text(value, "session_id"), Text(value, "trial_id"),
            Text(value, "source_id"), Text(value, "source_boot_id"), Text(value, "capture_attempt_id"), Text(value, "source_kind"));
        var expectation = new CaptureFinalizationExpectation(identity, Get("CAPTURE_EVENTS")!.Digest, Get("FINALIZATION_RECORD")!.Digest,
            Text(value, "finalization_outcome"), Text(value, "finalized_utc"), value.TryGetProperty("finalization_reason", out var reason) ? reason.GetString() : null);
        return new PackageInputManifest(value, expectation, version, artifacts.AsReadOnly());
    }

    private static void CheckId(string value) => Need(Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && value == id.ToString("D"), "Expected canonical nonempty UUID.");
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Need(bool condition, string message) { if (!condition) { throw new InvalidDataException(message); } }
    private static string Text(JsonElement item, string key) => item.GetProperty(key).GetString()!;
    private static ulong U64(JsonElement item, string key)
    {
        Need(ulong.TryParse(Text(item, key), NumberStyles.None, CultureInfo.InvariantCulture, out var number), "Unsigned integer overflow."); return number;
    }
}

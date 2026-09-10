using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HumCapture.Coordinator.Repository;

internal static partial class StagedPackageEvidenceValidator
{
    private const string ManifestFileName = "package-manifest.json";
    private static readonly string[] AllChecks =
    [
        "MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES",
        "ARTIFACT_SET", "BYTE_LENGTH", "SHA256", "MASTER_STRUCTURE",
        "MASTER_FULL_DECODE", "TIMING_EVENTS", "METADATA_PROFILE", "FINALIZATION"
    ];
    private static readonly HashSet<string> AlwaysRequiredChecks =
    [
        "MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES",
        "ARTIFACT_SET", "BYTE_LENGTH", "SHA256", "FINALIZATION"
    ];

    public static ValidatedStagedEvidence Validate(
        string stagingDirectory,
        StagedVerifiedPackageRegistration registration)
    {
        RepositoryPathSafety.RequireSafeDirectoryTree(stagingDirectory);
        var manifestPath = Path.Combine(stagingDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw Mismatch("Staged package manifest is missing.");
        }

        RepositoryPathSafety.RequireSingleLinkFile(manifestPath);
        using var manifestDocument = Parse(File.ReadAllBytes(manifestPath), "package manifest");
        var manifest = manifestDocument.RootElement;
        RequireObject(manifest, "Package manifest");
        RequireOnlyProperties(manifest,
        [
            "schema_version", "interface_profiles", "package_id", "package_content_sha256",
            "artifact_set_sha256", "subject_id", "session_id", "trial_id", "source_id",
            "capture_attempt_id", "source_kind", "source_boot_id", "protocol_snapshot_id",
            "protocol_snapshot_content_sha256", "configuration_id", "configuration_content_sha256",
            "finalization_outcome", "finalization_reason", "finalized_utc", "artifact_count",
            "package_byte_length", "artifacts"
        ], "Package manifest");
        RequireProperties(manifest,
        [
            "schema_version", "package_id", "package_content_sha256", "artifact_set_sha256",
            "subject_id", "session_id", "trial_id", "source_id", "capture_attempt_id",
            "source_kind", "source_boot_id", "protocol_snapshot_id",
            "protocol_snapshot_content_sha256", "configuration_id", "configuration_content_sha256",
            "finalization_outcome", "finalized_utc", "artifact_count", "package_byte_length", "artifacts"
        ], "Package manifest");

        Equal("1.0.0", String(manifest, "schema_version"), "Manifest schema version");
        Bind(registration.PackageId, String(manifest, "package_id"), "package_id");
        Bind(registration.SubjectId, String(manifest, "subject_id"), "subject_id");
        Bind(registration.SessionId, String(manifest, "session_id"), "session_id");
        Bind(registration.TrialId, String(manifest, "trial_id"), "trial_id");
        Bind(registration.SourceId, String(manifest, "source_id"), "source_id");
        Bind(registration.CaptureAttemptId, String(manifest, "capture_attempt_id"), "capture_attempt_id");
        Equal(registration.PackageContentSha256, Sha256(manifest, "package_content_sha256"), "package_content_sha256");
        Equal(registration.ArtifactSetSha256, Sha256(manifest, "artifact_set_sha256"), "artifact_set_sha256");
        Equal(registration.PackageByteLength, U64(manifest, "package_byte_length"), "package_byte_length");
        Equal(registration.ArtifactCount, Integer(manifest, "artifact_count"), "artifact_count");
        _ = CanonicalUuid(manifest, "source_boot_id");
        _ = CanonicalUuid(manifest, "protocol_snapshot_id");
        _ = Sha256(manifest, "protocol_snapshot_content_sha256");
        _ = CanonicalUuid(manifest, "configuration_id");
        _ = Sha256(manifest, "configuration_content_sha256");
        RequireUtc(manifest, "finalized_utc");
        var sourceKind = String(manifest, "source_kind");
        if (sourceKind is not ("ANDROID" or "UVC"))
        {
            throw Mismatch("Manifest source_kind is not supported.");
        }
        var finalizationOutcome = String(manifest, "finalization_outcome");
        if (finalizationOutcome is not ("FINALIZED_COMPLETE" or "FINALIZED_INCOMPLETE"))
        {
            throw Mismatch("Manifest finalization outcome is not controlled.");
        }
        if (finalizationOutcome == "FINALIZED_INCOMPLETE")
        {
            RequireBoundedString(manifest, "finalization_reason", 3, 2000);
        }
        else if (manifest.TryGetProperty("finalization_reason", out _))
        {
            throw Mismatch("A complete package must not contain finalization_reason.");
        }

        var artifactsElement = Required(manifest, "artifacts");
        if (artifactsElement.ValueKind != JsonValueKind.Array || artifactsElement.GetArrayLength() == 0)
        {
            throw Mismatch("Manifest artifacts must be a non-empty array.");
        }

        var artifacts = artifactsElement.EnumerateArray().Select(ParseArtifact).ToArray();
        if (artifacts.Length != registration.ArtifactCount)
        {
            throw Mismatch("Manifest artifact count does not match the registration.");
        }

        RequireUnique(artifacts.Select(item => item.ArtifactId), "artifact IDs");
        RequireUnique(artifacts.Select(item => item.RelativePath.ToUpperInvariant()), "Windows-normalized artifact paths");
        string[] requiredRoles = ["CAPTURE_EVENTS", "FINALIZATION_RECORD"];
        if (finalizationOutcome == "FINALIZED_COMPLETE")
        {
            requiredRoles = ["SCIENTIFIC_MASTER_VIDEO", "FRAME_TIMESTAMPS", "CAMERA_METADATA", "CAPTURE_EVENTS", "FINALIZATION_RECORD"];
            if (sourceKind == "ANDROID")
            {
                requiredRoles = [.. requiredRoles, "IMU_SAMPLES", "IMU_METADATA"];
            }
        }
        foreach (var role in requiredRoles)
        {
            if (!Array.Exists(artifacts, item => item.Role == role && item.IsRequired))
            {
                throw Mismatch($"Required manifest artifact role is absent: {role}");
            }
        }

        ulong total = 0;
        foreach (var artifact in artifacts)
        {
            Equal(registration.SessionId.ToString("D"), artifact.SessionId, "artifact session_id");
            Equal(registration.TrialId.ToString("D"), artifact.TrialId, "artifact trial_id");
            Equal(registration.SourceId.ToString("D"), artifact.SourceId, "artifact source_id");
            Equal(registration.CaptureAttemptId.ToString("D"), artifact.CaptureAttemptId, "artifact capture_attempt_id");

            var artifactPath = RepositoryPathSafety.ResolveRelativePath(stagingDirectory, artifact.RelativePath);
            if (!File.Exists(artifactPath) || Directory.Exists(artifactPath))
            {
                throw Mismatch($"Manifest artifact is missing or not a regular file: {artifact.RelativePath}");
            }

            RepositoryPathSafety.RequireSingleLinkFile(artifactPath);
            var info = new FileInfo(artifactPath);
            if ((ulong)info.Length != artifact.Length)
            {
                throw Mismatch($"Artifact byte length differs: {artifact.RelativePath}");
            }

            using var stream = new FileStream(artifactPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(stream));
            Equal(artifact.ContentSha256, actualHash, $"Artifact SHA-256 for {artifact.RelativePath}");
            checked { total += artifact.Length; }
        }

        Equal(registration.PackageByteLength, total.ToString(CultureInfo.InvariantCulture), "Artifact byte-length sum");
        var actualFiles = Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(stagingDirectory, path).Replace('\\', '/'))
            .Where(path => !string.Equals(path, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expectedFiles = artifacts.Select(item => item.RelativePath).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!actualFiles.SequenceEqual(expectedFiles, StringComparer.OrdinalIgnoreCase))
        {
            throw Mismatch("Staged package file set differs from the manifest artifact set.");
        }

        var inventory = string.Concat(artifacts
            .Select(item => $"{item.RelativePath}\t{item.Length.ToString(CultureInfo.InvariantCulture)}\t{item.ContentSha256}\n")
            .Order(StringComparer.Ordinal));
        Equal(registration.ArtifactSetSha256,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory))),
            "Computed artifact-set SHA-256");

        Equal(registration.PackageContentSha256, ComputeCanonicalManifestHash(manifest), "Computed package-content SHA-256");

        var verificationBytes = registration.VerificationRecordUtf8.ToArray();
        Equal(registration.VerificationRecordContentSha256,
            Convert.ToHexStringLower(SHA256.HashData(verificationBytes)),
            "Verification-record SHA-256");
        using var verificationDocument = Parse(verificationBytes, "verification record");
        var revision = ValidateVerification(verificationDocument.RootElement, registration, artifacts);
        return new ValidatedStagedEvidence(revision);
    }

    private static int ValidateVerification(
        JsonElement verification,
        StagedVerifiedPackageRegistration registration,
        IReadOnlyList<ManifestArtifact> artifacts)
    {
        RequireObject(verification, "Verification record");
        RequireOnlyProperties(verification,
        [
            "schema_version", "verification_record_id", "revision", "package_id",
            "package_content_sha256", "artifact_set_sha256", "verified_by_windows_account",
            "verifier_name", "verifier_version", "verified_utc", "outcome", "checks",
            "artifact_results"
        ], "Verification record");
        RequireProperties(verification,
        [
            "schema_version", "verification_record_id", "revision", "package_id",
            "package_content_sha256", "artifact_set_sha256", "verified_by_windows_account",
            "verifier_name", "verifier_version", "verified_utc", "outcome", "checks", "artifact_results"
        ], "Verification record");
        Equal("1.0.0", String(verification, "schema_version"), "Verification schema version");
        Bind(registration.VerificationRecordId, String(verification, "verification_record_id"), "verification_record_id");
        Bind(registration.PackageId, String(verification, "package_id"), "verification package_id");
        Equal(registration.PackageContentSha256, Sha256(verification, "package_content_sha256"), "verification package hash");
        Equal(registration.ArtifactSetSha256, Sha256(verification, "artifact_set_sha256"), "verification artifact-set hash");
        Equal("VERIFIED", String(verification, "outcome"), "Verification outcome");
        RequireBoundedString(verification, "verified_by_windows_account", 1, 256);
        RequireBoundedString(verification, "verifier_name", 1, 128);
        if (!VersionPattern().IsMatch(String(verification, "verifier_version")))
        {
            throw Mismatch("Verifier version must be semantic version syntax.");
        }
        RequireUtc(verification, "verified_utc");
        var revision = Integer(verification, "revision");
        if (revision < 1)
        {
            throw Mismatch("Verification revision must be positive.");
        }

        var checksElement = Required(verification, "checks");
        if (checksElement.ValueKind != JsonValueKind.Array)
        {
            throw Mismatch("Verification checks must be an array.");
        }

        var checks = checksElement.EnumerateArray().Select(ParseCheck).ToArray();
        RequireUnique(checks.Select(item => item.Name), "verification checks");
        if (!checks.Select(item => item.Name).ToHashSet(StringComparer.Ordinal).SetEquals(AllChecks))
        {
            throw Mismatch("Verification record must explicitly contain every controlled check exactly once.");
        }

        var requiredChecks = new HashSet<string>(AlwaysRequiredChecks, StringComparer.Ordinal);
        if (artifacts.Any(item => item.Role == "SCIENTIFIC_MASTER_VIDEO"))
        {
            requiredChecks.Add("MASTER_STRUCTURE");
            requiredChecks.Add("MASTER_FULL_DECODE");
        }
        if (artifacts.Any(item => item.Role is "SCIENTIFIC_MASTER_VIDEO" or "FRAME_TIMESTAMPS" or "IMU_SAMPLES" or "CAPTURE_EVENTS"))
        {
            requiredChecks.Add("TIMING_EVENTS");
        }
        if (artifacts.Any(item => item.Role == "CAMERA_METADATA"))
        {
            requiredChecks.Add("METADATA_PROFILE");
        }

        foreach (var requiredCheck in requiredChecks)
        {
            var check = checks.Single(item => item.Name == requiredCheck);
            if (!check.IsRequired || check.Disposition != "PASS")
            {
                throw Mismatch($"Required verification check did not pass: {requiredCheck}");
            }
        }
        if (Array.Exists(checks, item => item.IsRequired && item.Disposition != "PASS"))
        {
            throw Mismatch("VERIFIED outcome contradicts a required verification check.");
        }

        var resultsElement = Required(verification, "artifact_results");
        if (resultsElement.ValueKind != JsonValueKind.Array)
        {
            throw Mismatch("Artifact results must be an array.");
        }

        var results = resultsElement.EnumerateArray().Select(ParseArtifactResult).ToArray();
        RequireUnique(results.Select(item => item.ArtifactId), "verification artifact IDs");
        if (results.Length != artifacts.Count)
        {
            throw Mismatch("Verification does not cover every manifest artifact.");
        }

        foreach (var result in results)
        {
            var artifact = artifacts.SingleOrDefault(item => item.ArtifactId == result.ArtifactId)
                ?? throw Mismatch("Verification references an unknown artifact.");
            if (result.RelativePath != artifact.RelativePath
                || result.IsRequired != artifact.IsRequired
                || result.ExpectedByteLength != artifact.Length
                || result.ExpectedContentSha256 != artifact.ContentSha256)
            {
                throw Mismatch("Verification artifact expectation differs from the manifest.");
            }
            var observedMatch = result.ObservedByteLength == artifact.Length && result.ObservedContentSha256 == artifact.ContentSha256;
            if ((result.Disposition == "PASS") != observedMatch || (result.IsRequired && result.Disposition != "PASS"))
            {
                throw Mismatch("Verification artifact result contradicts observed identity.");
            }
        }
        return revision;
    }

    private static ManifestArtifact ParseArtifact(JsonElement value)
    {
        RequireObject(value, "Manifest artifact");
        RequireOnlyProperties(value,
        [
            "artifact_id", "relative_path", "role", "media_type", "format_version", "byte_length",
            "sha256", "required", "session_id", "trial_id", "source_id", "capture_attempt_id",
            "timing_coverage"
        ], "Manifest artifact");
        RequireProperties(value,
        [
            "artifact_id", "relative_path", "role", "media_type", "byte_length", "sha256",
            "required", "session_id", "trial_id", "source_id", "capture_attempt_id"
        ], "Manifest artifact");
        var path = String(value, "relative_path");
        ValidateArtifactPath(path);
        var role = String(value, "role");
        if (!ArtifactRoles().IsMatch(role))
        {
            throw Mismatch("Manifest artifact role is not controlled.");
        }
        RequireBoundedString(value, "media_type", 3, 127);
        return new ManifestArtifact(
            CanonicalUuid(value, "artifact_id"), path, role,
            U64Value(value, "byte_length"), Sha256(value, "sha256"), Boolean(value, "required"),
            CanonicalUuid(value, "session_id"), CanonicalUuid(value, "trial_id"),
            CanonicalUuid(value, "source_id"), CanonicalUuid(value, "capture_attempt_id"));
    }

    private static VerificationCheck ParseCheck(JsonElement value)
    {
        RequireObject(value, "Verification check");
        RequireOnlyProperties(value, ["check", "required", "disposition", "evidence_references", "reason"], "Verification check");
        RequireProperties(value, ["check", "required", "disposition", "evidence_references"], "Verification check");
        var disposition = String(value, "disposition");
        if (disposition is not ("PASS" or "FAIL" or "NOT_APPLICABLE" or "NOT_ASSESSED"))
        {
            throw Mismatch("Verification check disposition is not controlled.");
        }
        var references = Required(value, "evidence_references");
        if (references.ValueKind != JsonValueKind.Array
            || references.EnumerateArray().Any(reference => reference.ValueKind != JsonValueKind.String
                || reference.GetString() is not { Length: >= 1 and <= 1024 }))
        {
            throw Mismatch("Verification evidence references are invalid.");
        }
        ValidateOptionalReason(value);
        return new VerificationCheck(String(value, "check"), Boolean(value, "required"), disposition);
    }

    private static ArtifactResult ParseArtifactResult(JsonElement value)
    {
        RequireObject(value, "Verification artifact result");
        RequireOnlyProperties(value,
        [
            "artifact_id", "relative_path", "required", "expected_byte_length", "observed_byte_length",
            "expected_sha256", "observed_sha256", "disposition", "reason"
        ], "Verification artifact result");
        RequireProperties(value,
        [
            "artifact_id", "relative_path", "required", "expected_byte_length", "observed_byte_length",
            "expected_sha256", "observed_sha256", "disposition"
        ], "Verification artifact result");
        var disposition = String(value, "disposition");
        if (disposition is not ("PASS" or "FAIL"))
        {
            throw Mismatch("Artifact-result disposition is not controlled.");
        }
        ValidateOptionalReason(value);
        return new ArtifactResult(
            CanonicalUuid(value, "artifact_id"), String(value, "relative_path"), Boolean(value, "required"),
            U64Value(value, "expected_byte_length"), U64Value(value, "observed_byte_length"),
            Sha256(value, "expected_sha256"), Sha256(value, "observed_sha256"), disposition);
    }

    private static string ComputeCanonicalManifestHash(JsonElement manifest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteCanonical(writer, manifest, "package_content_sha256");
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element, string? excludedProperty = null)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                    .Where(property => property.Name != excludedProperty)
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out var integer))
                {
                    throw Mismatch("Manifest contains a non-integer JSON number.");
                }

                writer.WriteNumberValue(integer);
                break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw Mismatch("Manifest contains an unsupported JSON value.");
        }
    }

    private static void ValidateArtifactPath(string path)
    {
        if (path.Length is < 1 or > 1024 || path != path.Normalize(NormalizationForm.FormC)
            || path.StartsWith('/') || path.Contains('\\') || UnsafePathCharacters().IsMatch(path))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Artifact path is not normalized Windows-safe relative syntax.");
        }

        var segments = path.Split('/');
        if (Array.Exists(segments, segment => string.IsNullOrEmpty(segment) || segment is "." or ".."
            || segment.EndsWith('.') || segment.EndsWith(' ') || WindowsDeviceName().IsMatch(segment)))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Artifact path contains an unsafe segment.");
        }

        if (string.Equals(path, ManifestFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Manifest cannot inventory itself.");
        }
    }

    private static JsonDocument Parse(byte[] bytes, string label)
    {
        try { return JsonDocument.Parse(bytes); }
        catch (JsonException exception) { throw new RepositoryException(RepositoryErrorCode.EvidenceMismatch, $"Invalid {label} JSON.", exception); }
    }

    private static JsonElement Required(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) ? property : throw Mismatch($"Required property is missing: {name}");
    private static string String(JsonElement value, string name)
    {
        var property = Required(value, name);
        return property.ValueKind == JsonValueKind.String && property.GetString() is { } text
            ? text : throw Mismatch($"Property must be a string: {name}");
    }
    private static bool Boolean(JsonElement value, string name)
    {
        var property = Required(value, name);
        return property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean() : throw Mismatch($"Property must be Boolean: {name}");
    }
    private static int Integer(JsonElement value, string name)
    {
        var property = Required(value, name);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number)
            ? number : throw Mismatch($"Property must be an integer: {name}");
    }
    private static string CanonicalUuid(JsonElement value, string name)
    {
        var text = String(value, name);
        if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || text != id.ToString("D"))
        {
            throw Mismatch($"Property must be a canonical non-empty UUID: {name}");
        }

        return text;
    }
    private static string Sha256(JsonElement value, string name)
    {
        var text = String(value, name);
        if (!Sha256Pattern().IsMatch(text))
        {
            throw Mismatch($"Property must be lowercase SHA-256: {name}");
        }

        return text;
    }
    private static string U64(JsonElement value, string name) => U64Value(value, name).ToString(CultureInfo.InvariantCulture);
    private static ulong U64Value(JsonElement value, string name)
    {
        var text = String(value, name);
        if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || text != number.ToString(CultureInfo.InvariantCulture))
        {
            throw Mismatch($"Property must be a canonical unsigned 64-bit decimal string: {name}");
        }

        return number;
    }
    private static void Bind(Guid expected, string actual, string label) => Equal(expected.ToString("D"), actual, label);
    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw Mismatch($"{label} does not match the staged registration.");
        }
    }
    private static void RequireUnique(IEnumerable<string> values, string label)
    {
        var array = values.ToArray();
        if (array.Distinct(StringComparer.Ordinal).Count() != array.Length)
        {
            throw Mismatch($"Duplicate {label} are prohibited.");
        }
    }
    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Mismatch($"{label} must be a JSON object.");
        }
    }
    private static void RequireOnlyProperties(JsonElement value, IEnumerable<string> allowed, string label)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        if (value.EnumerateObject().Any(property => !set.Contains(property.Name)))
        {
            throw Mismatch($"{label} contains an unknown property.");
        }
    }
    private static void RequireProperties(JsonElement value, IEnumerable<string> required, string label)
    {
        foreach (var name in required)
        {
            if (!value.TryGetProperty(name, out _))
            {
                throw Mismatch($"{label} is missing required property: {name}");
            }
        }
    }
    private static void RequireBoundedString(JsonElement value, string name, int minimum, int maximum)
    {
        var text = String(value, name);
        if (text.Length < minimum || text.Length > maximum)
        {
            throw Mismatch($"String property has invalid length: {name}");
        }
    }
    private static void RequireUtc(JsonElement value, string name)
    {
        var text = String(value, name);
        if (!text.EndsWith('Z') || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            || timestamp.Offset != TimeSpan.Zero)
        {
            throw Mismatch($"Property must be an RFC3339 UTC timestamp: {name}");
        }
    }
    private static void ValidateOptionalReason(JsonElement value)
    {
        if (value.TryGetProperty("reason", out var reason)
            && (reason.ValueKind != JsonValueKind.String || reason.GetString() is not { Length: >= 3 and <= 2000 }))
        {
            throw Mismatch("Optional reason must be a bounded string.");
        }
    }
    private static RepositoryException Mismatch(string message) => new(RepositoryErrorCode.EvidenceMismatch, message);

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
    [GeneratedRegex("[<>:\"|?*\\u0000-\\u001f]", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafePathCharacters();
    [GeneratedRegex("^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsDeviceName();
    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
    [GeneratedRegex("^(SCIENTIFIC_MASTER_VIDEO|FRAME_TIMESTAMPS|TIMING_METADATA|CAMERA_METADATA|CAPTURE_EVENTS|FINALIZATION_RECORD|IMU_SAMPLES|IMU_METADATA|CALIBRATION|AUXILIARY_EVIDENCE)$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactRoles();

    internal sealed record ValidatedStagedEvidence(int VerificationRecordRevision);
    private sealed record ManifestArtifact(string ArtifactId, string RelativePath, string Role, ulong Length, string ContentSha256, bool IsRequired, string SessionId, string TrialId, string SourceId, string CaptureAttemptId);
    private sealed record VerificationCheck(string Name, bool IsRequired, string Disposition);
    private sealed record ArtifactResult(string ArtifactId, string RelativePath, bool IsRequired, ulong ExpectedByteLength, ulong ObservedByteLength, string ExpectedContentSha256, string ObservedContentSha256, string Disposition);
}

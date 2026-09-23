using System.Globalization;
using System.Security.Principal;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record ProducedVerification(string Outcome, ReadOnlyMemory<byte> Utf8, string Sha256,
    PackageDecodeResult Evaluation);

// Only real pinned-worker evaluation reaches this producer. No supplied PASS values.
internal static class PackageVerificationProducer
{
    internal static async Task<ProducedVerification> VerifyAsync(string directory, string binaries,
        TimeSpan timeout, Guid recordId, DateTimeOffset auditTime, CancellationToken token = default,
        ProtocolEvidenceContext? protocol = null)
    {
        using var lease = PackageInputLease.Open(directory, token);
        return await VerifyLeasedAsync(lease, binaries, timeout, recordId, auditTime, token, protocol).ConfigureAwait(false);
    }

    internal static async Task<ProducedVerification> VerifyLeasedAsync(PackageInputLease lease, string binaries,
        TimeSpan timeout, Guid recordId, DateTimeOffset auditTime, CancellationToken token, ProtocolEvidenceContext? protocol)
    {
        if (recordId == Guid.Empty || auditTime == default) { throw new ArgumentException("Stable nonempty record ID and audit time required."); }
        protocol = protocol?.Freeze();
        using var identity = WindowsIdentity.GetCurrent();
        var actor = identity.Name;
        var evaluation = await PackageInputEvidence.EvaluateLeasedAsync(lease, binaries, timeout, token, protocol).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        lease.CheckUnchanged(token);
        return Build(lease.Manifest, evaluation, recordId, auditTime, actor, token);
    }

    private static ProducedVerification Build(PackageInputManifest manifest, PackageDecodeResult evaluation,
        Guid id, DateTimeOffset time, string actor, CancellationToken token)
    {
        var input = evaluation.Input;
        var checks = new List<object>();
        var failed = false;
        void Check(string name, bool required, string disposition, string reason, IEnumerable<string>? extra = null)
        {
            if (required && disposition != "PASS") { failed = true; }
            var references = new List<string> { "manifest-sha256:" + manifest.Json.GetProperty("package_content_sha256").GetString() };
            if (extra is not null) { references.AddRange(extra); }
            checks.Add(new { check = name, required, disposition, reason, evidence_references = references });
        }
        foreach (var name in new[] { "MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES", "ARTIFACT_SET", "BYTE_LENGTH", "SHA256" })
        { Check(name, true, "PASS", "Validated from immutable leased package bytes; identity consistency only."); }
        var master = manifest.Find("SCIENTIFIC_MASTER_VIDEO") is not null;
        var decoded = evaluation.Inspection?.Outcome == DecoderExit.Decoded && evaluation.Inspection.Evidence is not null;
        var decodeDisposition = decoded ? "PASS" : "NOT_ASSESSED";
        if (!master) { decodeDisposition = "NOT_APPLICABLE"; }
        var decodeReason = master ? "Actual pinned decoder outcome: " + (evaluation.Inspection?.Outcome.ToString() ?? "Unavailable")
            : "No master retained; this does not establish complete capture.";
        if (evaluation.Inspection is not null)
        { decodeReason += "; " + evaluation.Inspection.Reason[..Math.Min(1600, evaluation.Inspection.Reason.Length)]; }
        using var lockStream = typeof(PackageVerificationProducer).Assembly.GetManifestResourceStream("HumCapture.Decoder.lock.json")!;
        var lockHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(lockStream));
        Check("MASTER_STRUCTURE", master, decodeDisposition, decodeReason, ["decoder-lock-sha256:" + lockHash]);
        Check("MASTER_FULL_DECODE", master, decodeDisposition, decodeReason, ["decoder-lock-sha256:" + lockHash]);
        var frame = manifest.Find("FRAME_TIMESTAMPS") is not null;
        var imu = manifest.Find("IMU_SAMPLES") is not null;
        var camera = manifest.Find("CAMERA_METADATA") is not null;
        var imuMetadata = manifest.Find("IMU_METADATA") is not null;
        var timingMetadata = manifest.Find("TIMING_METADATA") is not null;
        var timingReady = input is not null && input.Coverage?.Disposition is "PASS" or "NOT_APPLICABLE"
            && (!frame || input.Evidence.Frames is not null) && (!imu || input.Evidence.Imu is not null)
            && !input.Evidence.NotAssessed.Contains("LEGACY_MAPPED_VALUES");
        var references = input?.Evidence.NotAssessed.Select(m => "not-assessed:" + m).ToList() ?? ["not-assessed:PACKAGE_SEMANTICS"];
        if (input?.Scientific is not null)
        {
            references.Add("scientific-outcome:" + input.Scientific.Protocol.Outcome);
            references.AddRange(input.Scientific.Protocol.Conditions.Select(c => "scientific-condition:" + JsonSerializer.Serialize(c)));
        }
        Check("TIMING_EVENTS", true, timingReady ? "PASS" : "NOT_ASSESSED",
            timingReady ? "Event/finalization and applicable native coverage/metadata comparisons passed."
                : "Required timing evidence unavailable or legacy; not a scientific-quality rejection.", references);
        var metadataReady = input is not null && (!camera || input.Evidence.Frames is not null)
            && (!imuMetadata || input.Evidence.Imu is not null)
            && (!timingMetadata || input.Evidence.Frames is not null || input.Evidence.Imu is not null);
        Check("METADATA_PROFILE", camera || imu || imuMetadata || timingMetadata, metadataReady ? "PASS" : "NOT_ASSESSED",
            "Technical metadata consistency only; no physical calibration, session authorization or take acceptance.", references);
        Check("FINALIZATION", true, input is null ? "NOT_ASSESSED" : "PASS",
            input is null ? "Semantic evaluation did not complete." : "Bound declared outcome: " + input.Evidence.Finalization.FinalizationOutcome);
        var outcome = failed ? "FAILED" : "VERIFIED";
        var value = JsonSerializer.SerializeToElement(new
        {
            schema_version = "1.0.0", verification_record_id = id.ToString("D"), revision = 1,
            package_id = manifest.Json.GetProperty("package_id").GetString(),
            package_content_sha256 = manifest.Json.GetProperty("package_content_sha256").GetString(),
            artifact_set_sha256 = manifest.Json.GetProperty("artifact_set_sha256").GetString(),
            verified_by_windows_account = actor, verifier_name = "HumCapture.InternalPackageVerifier", verifier_version = "1.0.0",
            verified_utc = time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture),
            outcome, checks,
            artifact_results = manifest.Artifacts.Select(a => new
            {
                artifact_id = a.Digest.ArtifactId, relative_path = a.Path, required = a.Required,
                expected_byte_length = a.Digest.ByteLength.ToString(CultureInfo.InvariantCulture),
                observed_byte_length = a.Digest.ByteLength.ToString(CultureInfo.InvariantCulture),
                expected_sha256 = a.Digest.Sha256, observed_sha256 = a.Digest.Sha256, disposition = "PASS"
            })
        });
        var bytes = CaptureCanonicalJson.Encode(value, token);
        _ = new MetadataSchemaValidator().Validate(MetadataKind.Verification, bytes, token);
        return new(outcome, bytes, PackageInputManifest.Hash(bytes), evaluation);
    }
}

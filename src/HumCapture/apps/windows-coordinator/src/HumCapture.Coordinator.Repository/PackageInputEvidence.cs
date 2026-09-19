using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record PackageInputResult(string PackageId, string PackageContentSha256, int BoundArtifactCount,
    InternalCaptureEvidenceResult Evidence);

// File input admission only. Actual pinned decoder invocation/quality/record production
// belong to subsequent batches. The observation callback is internal, not a host API.
internal static class PackageInputEvidence
{
    internal static PackageInputResult Evaluate(string directory,
        Func<string, CancellationToken, DecodedMediaEvidence?>? observe = null, CancellationToken token = default)
    {
        using var lease = PackageInputLease.Open(directory, token);
        var manifest = lease.Manifest; var expected = manifest.Finalization;
        ReadOnlyMemory<byte> Read(string role) => lease.ReadArtifact(manifest.Find(role)!, token);
        var finalization = new CaptureFinalizationEvidence().Compare(expected, Read("CAPTURE_EVENTS"), Read("FINALIZATION_RECORD"), token);
        var missing = new List<string> { "PROTOCOL_ADMISSION", "PHYSICAL_CALIBRATION_AND_SYNCHRONIZATION", "CADENCE_ACCEPTANCE",
            "DECODER_PROVENANCE", "CLOCK_MODEL_SEGMENT_CONTINUITY" };
        var schema = new MetadataSchemaValidator();
        JsonElement? Metadata(string role, MetadataKind kind)
        {
            if (manifest.Find(role) is null) { return null; }
            var value = schema.Validate(kind, Read(role), token);
            PackageInputManifest.Need(value.GetProperty("capture_attempt_id").GetString() == expected.Identity.CaptureAttemptId,
                "Metadata belongs to another capture.");
            return value;
        }
        var timing = Metadata("TIMING_METADATA", TimingClockBindings.SchemaKind(manifest.TimingVersion ?? TimingMetadataVersion.LegacyV1));
        var camera = Metadata("CAMERA_METADATA", MetadataKind.Camera);
        var imuMetadata = Metadata("IMU_METADATA", MetadataKind.Imu);
        var frameStream = manifest.Find("FRAME_TIMESTAMPS") is null ? null : TimingBinaryReader.Frames(Read("FRAME_TIMESTAMPS").Span, token);
        var imuStream = manifest.Find("IMU_SAMPLES") is null ? null : TimingBinaryReader.Imu(Read("IMU_SAMPLES").Span, token);
        var attempt = Guid.Parse(expected.Identity.CaptureAttemptId);
        PackageInputManifest.Need(frameStream is null || frameStream.CaptureAttemptId == attempt, "Frame binary belongs to another capture.");
        PackageInputManifest.Need(imuStream is null || imuStream.CaptureAttemptId == attempt, "IMU binary belongs to another capture.");
        DecodedMediaEvidence? decoded = null;
        var master = manifest.Find("SCIENTIFIC_MASTER_VIDEO");
        if (master is not null && observe is not null)
        {
            token.ThrowIfCancellationRequested(); decoded = observe(lease.MasterPath(master), token);
            if (decoded is not null)
            { PackageInputManifest.Need(decoded.MediaSha256 == master.Digest.Sha256 && decoded.ByteLength == master.Digest.ByteLength, "Decoded observations do not bind admitted master bytes."); }
        }
        FrameMetadataResult? frames = null; ImuMetadataResult? imu = null;
        if (timing.HasValue && camera.HasValue && frameStream is not null && decoded is not null)
        { frames = new FrameMetadataEvidence().Compare(Read("TIMING_METADATA"), Read("CAMERA_METADATA"), frameStream, decoded.Video, token, manifest.TimingVersion!.Value); }
        else { missing.Add("FRAME_EVIDENCE"); }
        if (timing.HasValue && imuMetadata.HasValue && imuStream is not null)
        { imu = new ImuMetadataEvidence().Compare(Read("TIMING_METADATA"), Read("IMU_METADATA"), imuStream, token, manifest.TimingVersion!.Value); }
        else { missing.Add("IMU_EVIDENCE"); }
        if (decoded is null) { missing.Add("MASTER_FULL_DECODE"); }
        if (frames is not null && !frames.MappedTimesRecomputed) { missing.Add("FRAME_MAPPED_VALUE_COMPARISON"); }
        if (imu is not null && imu.MappedSamplesCompared == 0) { missing.Add("IMU_MAPPED_VALUE_COMPARISON"); }
        foreach (var auxiliary in manifest.Artifacts.Where(a => a.Role is "CALIBRATION" or "AUXILIARY_EVIDENCE"))
        { missing.Add("ARTIFACT_SEMANTICS:" + auxiliary.Digest.ArtifactId); }
        lease.CheckUnchanged(token);
        return new PackageInputResult(expected.Identity.PackageId, manifest.Json.GetProperty("package_content_sha256").GetString()!,
            manifest.Artifacts.Count, new InternalCaptureEvidenceResult(finalization, frames, imu, missing.AsReadOnly()));
    }
}

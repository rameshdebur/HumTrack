namespace HumCapture.Coordinator.Repository;

internal sealed record BoundEvidenceArtifact(ArtifactDigest Digest, ReadOnlyMemory<byte> Bytes);
internal sealed record FrameEvidenceInput(BoundEvidenceArtifact Master, BoundEvidenceArtifact Frames,
    BoundEvidenceArtifact Camera, DecodedMediaEvidence Decoded);
internal sealed record ImuEvidenceInput(BoundEvidenceArtifact Samples, BoundEvidenceArtifact Metadata);
internal sealed record ScientificEvidenceInput(BoundEvidenceArtifact Timing, TimingMetadataVersion Version,
    FrameEvidenceInput? Frames, ImuEvidenceInput? Imu);
internal sealed record InternalCaptureEvidenceResult(CaptureFinalizationResult Finalization,
    FrameMetadataResult? Frames, ImuMetadataResult? Imu, IReadOnlyList<string> NotAssessed);

// Internal composition only. Caller supplies admitted manifest projections and finalized
// bytes under its own read-only lease. This type has no repository/host/deletion authority.
internal sealed class InternalCaptureEvidence
{
    internal InternalCaptureEvidenceResult Compare(CaptureFinalizationExpectation expected,
        ReadOnlyMemory<byte> archive, ReadOnlyMemory<byte> summary, ScientificEvidenceInput? scientific,
        CancellationToken token = default)
    {
        var finalization = new CaptureFinalizationEvidence().Compare(expected, archive, summary, token);
        var complete = finalization.FinalizationOutcome == "FINALIZED_COMPLETE";
        if (complete && (scientific?.Frames is null || (expected.Identity.SourceKind == "ANDROID" && scientific.Imu is null)))
        { throw new InvalidDataException("Complete capture lacks source-required scientific evidence."); }
        FrameMetadataResult? frames = null; ImuMetadataResult? imu = null;
        var missing = new List<string> { "FULL_MANIFEST_AND_PROTOCOL_ADMISSION", "PHYSICAL_CALIBRATION_AND_SYNCHRONIZATION", "CADENCE_ACCEPTANCE", "DECODER_PROVENANCE" };
        var artifacts = new HashSet<string>(StringComparer.Ordinal) { expected.Archive.ArtifactId, expected.Summary.ArtifactId };
        void Bind(BoundEvidenceArtifact item)
        {
            token.ThrowIfCancellationRequested();
            if (!artifacts.Add(item.Digest.ArtifactId)) { throw new InvalidDataException("Duplicate composed artifact identity."); }
            item.Digest.Compare(item.Bytes);
        }
        if (scientific is not null)
        {
            Bind(scientific.Timing);
            var metadata = new MetadataSchemaValidator().Validate(TimingClockBindings.SchemaKind(scientific.Version), scientific.Timing.Bytes, token);
            if (metadata.GetProperty("capture_attempt_id").GetString() != expected.Identity.CaptureAttemptId)
            { throw new InvalidDataException("Scientific evidence belongs to another capture."); }
            if (scientific.Frames is not null)
            {
                var group = scientific.Frames; Bind(group.Master); Bind(group.Frames); Bind(group.Camera);
                if (group.Decoded.MediaSha256 != group.Master.Digest.Sha256 || group.Decoded.ByteLength != group.Master.Digest.ByteLength)
                { throw new InvalidDataException("Decoded observations belong to different master bytes."); }
                frames = new FrameMetadataEvidence().Compare(scientific.Timing.Bytes, group.Camera.Bytes,
                    TimingBinaryReader.Frames(group.Frames.Bytes.Span, token), group.Decoded.Video, token, scientific.Version);
            }
            if (scientific.Imu is not null)
            {
                Bind(scientific.Imu.Samples); Bind(scientific.Imu.Metadata);
                imu = new ImuMetadataEvidence().Compare(scientific.Timing.Bytes, scientific.Imu.Metadata.Bytes,
                    TimingBinaryReader.Imu(scientific.Imu.Samples.Bytes.Span, token), token, scientific.Version);
            }
        }
        if (frames is null) { missing.Add("FRAME_EVIDENCE"); }
        if (imu is null) { missing.Add("IMU_EVIDENCE"); }
        if (frames is not null && !frames.MappedTimesRecomputed) { missing.Add("FRAME_MAPPED_VALUE_COMPARISON"); }
        if (imu is not null && imu.MappedSamplesCompared == 0) { missing.Add("IMU_MAPPED_VALUE_COMPARISON"); }
        // Even exact per-record arithmetic does not establish segment/epoch model validity.
        missing.Add("CLOCK_MODEL_SEGMENT_CONTINUITY");
        return new InternalCaptureEvidenceResult(finalization, frames, imu, missing);
    }
}

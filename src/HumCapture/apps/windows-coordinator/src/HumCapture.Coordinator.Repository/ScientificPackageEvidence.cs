using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record ScientificPackageEvidence(SourceCadenceEvidence? Cadence, ClockContinuityEvidence? ClockContinuity,
    ProtocolScientificEvidence Protocol)
{
    internal static ScientificPackageEvidence Compare(PackageInputManifest manifest, JsonElement? timing, JsonElement? camera,
        TimingStream<SourceFrame>? frames, TimingStream<ImuSample>? imu, DecodedVideo? video,
        ProtocolEvidenceContext? protocol, CancellationToken token)
    {
        SourceCadenceEvidence? cadence = null;
        ClockContinuityEvidence? continuity = null;
        var cameraClockBound = false;
        if (camera.HasValue)
        {
            var expectedKind = manifest.Json.GetProperty("source_kind").GetString() == "UVC" ? "WINDOWS_UVC" : "ANDROID_CAMERA2";
            PackageInputManifest.Need(camera.Value.GetProperty("camera_type").GetString() == expectedKind, "Camera type differs from source kind.");
            if (frames is not null)
            {
                PackageInputManifest.Need(camera.Value.GetProperty("camera_stream_id").GetGuid() == frames.StreamId, "Camera metadata belongs to another frame stream.");
                var counts = camera.Value.GetProperty("observed_timing");
                PackageInputManifest.Need(counts.GetProperty("record_count").GetInt64() == frames.Records.Count
                    && counts.GetProperty("accepted_frame_count").GetInt64() == frames.Records.Count(frame => frame.Disposition == 1),
                    "Camera record counts disagree with native frames.");
            }
        }
        if (timing.HasValue)
        {
            var bindings = new TimingClockBindings(timing.Value, manifest.TimingVersion!.Value, token);
            continuity = ClockModelContinuity.Check(bindings, frames, imu, token);
            if (frames is not null)
            {
                var stream = bindings.Stream(frames.StreamId, "FRAME_TIMESTAMPS", "timing/frame-timestamps.bin");
                var clock = bindings.Clocks[stream.GetProperty("native_clock_id").GetGuid()];
                PackageInputManifest.Need(clock.GetProperty("authority").GetString() is not ("PRESENTATION" or "AUDIT_ONLY")
                    && clock.GetProperty("provenance").GetString() is not ("ENCODER_PTS" or "UTC_AUDIT"), "Native frame clock is not an acquisition clock.");
                if (camera.HasValue)
                {
                    var reported = camera.Value.GetProperty("timestamp_provenance").GetString();
                    var expectedProvenance = reported switch { "HOST_SAMPLE" => "MF_SAMPLE_TIME", "HOST_ARRIVAL" => "QPC_HOST_ARRIVAL", _ => reported };
                    var expectedAuthority = reported switch
                    { "ANDROID_SENSOR_TIMESTAMP" => "SENSOR", "MF_DEVICE_TIMESTAMP" => "DEVICE", "MF_SAMPLE_TIME" or "HOST_SAMPLE" => "HOST_SAMPLE", _ => "HOST_ARRIVAL" };
                    PackageInputManifest.Need(clock.GetProperty("provenance").GetString() == expectedProvenance
                        && clock.GetProperty("authority").GetString() == expectedAuthority, "Camera/native clock provenance disagrees.");
                    cameraClockBound = true;
                }
                cadence = SourceCadenceEvidence.Measure(frames.Records, clock.GetProperty("ticks_per_second").GetUInt64(), token);
            }
        }
        return new(cadence, continuity, ProtocolScientificEvidence.Evaluate(protocol, manifest.Json, cadence, camera, video, cameraClockBound, token));
    }
}

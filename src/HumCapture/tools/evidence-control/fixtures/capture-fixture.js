import { createHash } from "node:crypto";
import { CAPTURE_ARTIFACT_PROFILE, encodeCaptureArtifact } from "../src/capture-artifact-conformance.js";
import { computeArtifactSetSha256, computePackageContentSha256 } from "../src/transfer-contract-conformance.js";
const id = (n) => `10000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");
export function fixture({ complete = true, partial = false, editArchive = () => {}, editSummary = () => {} } = {}) {
  const identity = { package_id: id(1), session_id: id(2), trial_id: id(3), source_id: id(4), source_boot_id: id(5), capture_attempt_id: id(6), source_kind: "UVC" };
  const outcome = complete ? "FINALIZED_COMPLETE" : "FINALIZED_INCOMPLETE";
  const reason = complete ? "Normal stop and finalization." : "Device loss; declared survivors retained.";
  const states = ["STARTING", "RECORDING", "STOPPING", "FINALIZING", outcome];
  const archive = { schema_version: "1.0.0", ...identity, completeness: partial ? "PARTIAL" : "COMPLETE",
    events: states.slice(1).map((state, i) => ({ event_id: id(20 + i), event_sequence: i + 1,
      event_type: i === 0 ? "FIRST_MASTER_SAMPLE" : i === 3 ? "FINALIZATION_RESULT" : "STATE_TRANSITION",
      event_source_time: { clock_id: id(7), ticks: String(1000 + i), ticks_per_second: 10000000 },
      prior_state: states[i], resulting_state: state, resulting_source_revision: i + 1,
      ...(i === 3 ? { reason } : {}) })) };
  if (partial) archive.reason = "Some earlier source events were lost.";
  editArchive(archive);
  const archiveBytes = encodeCaptureArtifact(archive);
  const summary = { schema_version: "1.0.0", ...identity, outcome, reason,
    terminal_event_id: id(23), event_archive_artifact_id: id(100), event_archive_sha256: hash(archiveBytes),
    finalized_utc: "2026-09-15T10:00:00Z" };
  editSummary(summary);
  const summaryBytes = encodeCaptureArtifact(summary);
  function artifact(n, role, relativePath, bytes, timed = false) {
    return { artifact_id: id(n), role, relative_path: relativePath, media_type: "application/json", format_version: "1.0.0", required: true,
      session_id: identity.session_id, trial_id: identity.trial_id, source_id: identity.source_id, capture_attempt_id: identity.capture_attempt_id,
      byte_length: String(bytes.length), sha256: hash(bytes), ...(timed ? { timing_coverage: { clock_id: id(7), ticks_per_second: 10000000, first_ticks: "1000", last_ticks: "1003", record_count: "4", discontinuity_count: "0" } } : {}) };
  }
  const artifacts = [artifact(100, "CAPTURE_EVENTS", "events/capture.json", archiveBytes), artifact(101, "FINALIZATION_RECORD", "metadata/finalization.json", summaryBytes)];
  if (complete) artifacts.push(artifact(102, "SCIENTIFIC_MASTER_VIDEO", "media/master.mp4", Buffer.from("synthetic; not decoded"), true),
    artifact(103, "FRAME_TIMESTAMPS", "timing/frames.bin", Buffer.from("synthetic timing"), true),
    artifact(104, "CAMERA_METADATA", "metadata/camera.json", Buffer.from("{}")));
  const manifest = { schema_version: "1.0.0", ...identity, subject_id: id(8), interface_profiles: [CAPTURE_ARTIFACT_PROFILE],
    protocol_snapshot_id: id(9), protocol_snapshot_content_sha256: "a".repeat(64), configuration_id: id(10), configuration_content_sha256: "b".repeat(64),
    finalization_outcome: outcome, finalized_utc: "2026-09-15T10:00:00Z", ...(complete ? {} : { finalization_reason: reason }),
    artifacts, artifact_count: artifacts.length, package_byte_length: artifacts.reduce((sum, item) => sum + BigInt(item.byte_length), 0n).toString(),
    artifact_set_sha256: computeArtifactSetSha256(artifacts) };
  manifest.package_content_sha256 = computePackageContentSha256(manifest);
  return { manifest, archiveBytes, summaryBytes };
}

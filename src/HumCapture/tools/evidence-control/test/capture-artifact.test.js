import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import test from "node:test";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { CAPTURE_ARTIFACT_PROFILE, encodeCaptureArtifact, validateCaptureArtifacts } from "../src/capture-artifact-conformance.js";
import { computeArtifactSetSha256, computePackageContentSha256 } from "../src/transfer-contract-conformance.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const ajv = new Ajv2020({ strict: true, allErrors: true }); addFormats(ajv);
ajv.addSchema(JSON.parse(await readFile(path.join(root, "docs/interfaces/schemas/control/v1/common.schema.json"), "utf8")));
for (const name of await readdir(path.join(root, "docs/interfaces/schemas/capture/v1"))) {
  ajv.addSchema(JSON.parse(await readFile(path.join(root, "docs/interfaces/schemas/capture/v1", name), "utf8")));
}
const validators = {
  validateArchive: ajv.getSchema("https://humtrack.invalid/humcapture/capture/v1/capture-event-archive.schema.json"),
  validateSummary: ajv.getSchema("https://humtrack.invalid/humcapture/capture/v1/finalization-summary.schema.json")
};
const id = (n) => `10000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");
const run = (bundle) => validateCaptureArtifacts(bundle.manifest, bundle.archiveBytes, bundle.summaryBytes, validators);
const rejects = (bundle, code) => assert.throws(() => run(bundle), (error) => error.code === code);

function fixture({ complete = true, partial = false, editArchive = () => {}, editSummary = () => {} } = {}) {
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

test("HC-ART-TEST-001 schemas compile; acyclic complete UVC event/summary bindings pass", () => {
  const bundle = fixture(); const result = run(bundle);
  assert.equal(result.finalizationOutcome, "FINALIZED_COMPLETE");
  assert.equal(result.observedEventGaps, 0);
  assert.ok(!bundle.manifest.artifacts.some((item) => item.role.startsWith("IMU")));
  assert.equal(result.outcome, undefined); // No VERIFIED outcome is produced.
});

test("HC-ART-TEST-002 incomplete survivors and declared partial event archive remain incomplete", () => {
  const bundle = fixture({ complete: false, partial: true, editArchive: (a) => { a.events.splice(1, 1); } });
  assert.equal(run(bundle).observedEventGaps, 1);
  assert.equal(run(bundle).finalizationOutcome, "FINALIZED_INCOMPLETE");
  rejects(fixture({ partial: true }), "CAPTURE_FALSE_COMPLETION");
});

test("HC-ART-TEST-003 foreign capture/source identity is refused", () => {
  rejects(fixture({ editArchive: (a) => { a.capture_attempt_id = id(999); } }), "CAPTURE_IDENTITY_MISMATCH");
  rejects(fixture({ editSummary: (s) => { s.source_boot_id = id(999); } }), "CAPTURE_IDENTITY_MISMATCH");
});

test("HC-ART-TEST-004 required event gaps and duplicate IDs fail", () => {
  rejects(fixture({ editArchive: (a) => { a.events.splice(1, 1); } }), "CAPTURE_EVENT_GAP");
  rejects(fixture({ editArchive: (a) => { a.events[1].event_id = a.events[0].event_id; } }), "CAPTURE_EVENT_DUPLICATE");
});

test("HC-ART-TEST-005 source clock regressions frequency changes and uint64 overflow fail", () => {
  for (const mutate of [
    (a) => { a.events[1].event_source_time.ticks = "1"; },
    (a) => { a.events[1].event_source_time.ticks_per_second = 1; },
    (a) => { a.events[1].event_source_time.clock_id = id(999); },
    (a) => { a.events[0].event_source_time.ticks = "18446744073709551616"; }
  ]) rejects(fixture({ editArchive: mutate }), "CAPTURE_TIME_INVALID");
});

test("HC-ART-TEST-006 finalization requires the exact last terminal event", () => {
  rejects(fixture({ editArchive: (a) => { a.events = [a.events.at(-1)]; } }), "CAPTURE_FIRST_SAMPLE_INVALID");
  rejects(fixture({ editSummary: (s) => { s.terminal_event_id = id(999); } }), "CAPTURE_TERMINAL_MISMATCH");
  rejects(fixture({ editArchive: (a) => { a.events.at(-1).resulting_state = "FINALIZED_INCOMPLETE"; } }), "CAPTURE_TERMINAL_MISMATCH");
  rejects(fixture({ editArchive: (a) => { a.events.at(-1).reason = "Different reason."; } }), "CAPTURE_TERMINAL_MISMATCH");
});

test("HC-ART-TEST-007 summary/archive hash or artifact binding cannot be substituted", () => {
  rejects(fixture({ editSummary: (s) => { s.event_archive_sha256 = "f".repeat(64); } }), "CAPTURE_ARCHIVE_BINDING");
  const bundle = fixture(); bundle.archiveBytes = Buffer.from(bundle.archiveBytes.toString().replace("1000", "1001"));
  assert.throws(() => run(bundle));
});

test("HC-ART-TEST-008 reject recursive package hash unknown fields and unsupported artifact schema", () => {
  rejects(fixture({ editArchive: (a) => { a.package_content_sha256 = "0".repeat(64); } }), "CAPTURE_SCHEMA_INVALID");
  rejects(fixture({ editSummary: (s) => { s.schema_version = "2.0.0"; } }), "CAPTURE_SCHEMA_INVALID");
  rejects(fixture({ editArchive: (a) => { a.events[0].authoritative_state = {}; } }), "CAPTURE_SCHEMA_INVALID");
});

test("HC-ART-TEST-009 canonical JSON rejects duplicate keys and invalid UTF-8", () => {
  const duplicate = fixture(); duplicate.archiveBytes = Buffer.from('{"schema_version":"1.0.0",' + duplicate.archiveBytes.toString().slice(1));
  rejects(duplicate, "CAPTURE_CANONICAL_INVALID");
  const malformed = fixture(); malformed.archiveBytes = Buffer.from([0xff, 0xfe]);
  rejects(malformed, "CAPTURE_JSON_INVALID");
});

test("HC-ART-TEST-010 historical packages are not silently upgraded", () => {
  const bundle = fixture(); delete bundle.manifest.interface_profiles;
  bundle.manifest.package_content_sha256 = computePackageContentSha256(bundle.manifest);
  rejects(bundle, "CAPTURE_PROFILE_UNSUPPORTED");
});

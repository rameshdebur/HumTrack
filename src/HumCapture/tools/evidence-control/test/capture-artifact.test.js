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

import { fixture } from "../fixtures/capture-fixture.js";
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

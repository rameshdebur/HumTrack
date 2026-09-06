import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  classifyPackageIdentity,
  computeArtifactSetSha256,
  computePackageContentSha256,
  contentDigest,
  offlineArtifactRecoveryAction,
  reconcileRangeResponse,
  strongEtag,
  validateCheckpointTransition,
  validateCollectionCheckpoint,
  validatePackageManifest,
  validatePackagePath,
  validatePackageVerification,
  validateVerifiedCommit
} from "../src/transfer-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const transferSchemas = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "transfer", "v1");
const controlSchemas = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "control", "v1");
const openApiPath = path.join(humCaptureRoot, "docs", "interfaces", "openapi", "transfer-v1.openapi.json");
const ids = {
  package_id: "10000000-0000-4000-8000-000000000001", subject_id: "10000000-0000-4000-8000-000000000002",
  session_id: "10000000-0000-4000-8000-000000000003", trial_id: "10000000-0000-4000-8000-000000000004",
  source_id: "10000000-0000-4000-8000-000000000005", capture_attempt_id: "10000000-0000-4000-8000-000000000006",
  source_boot_id: "10000000-0000-4000-8000-000000000007", protocol_snapshot_id: "10000000-0000-4000-8000-000000000008",
  configuration_id: "10000000-0000-4000-8000-000000000009"
};

async function json(file) { return JSON.parse(await readFile(file, "utf8")); }
function clone(value) { return structuredClone(value); }
function throwsCode(fn, code) { assert.throws(fn, (error) => error?.code === code); }
function artifact(index, role, relativePath, timed = false) {
  const base = {
    artifact_id: `20000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
    relative_path: relativePath, role, media_type: role === "SCIENTIFIC_MASTER_VIDEO" ? "video/mp4" : "application/json",
    byte_length: String(100 + index), sha256: index.toString(16).padStart(64, "0"), required: true,
    session_id: ids.session_id, trial_id: ids.trial_id, source_id: ids.source_id, capture_attempt_id: ids.capture_attempt_id
  };
  if (timed) base.timing_coverage = { clock_id: "30000000-0000-4000-8000-000000000001", ticks_per_second: 1000000000, first_ticks: "10", last_ticks: "20", record_count: "2", discontinuity_count: "0" };
  return base;
}
function manifest() {
  const artifacts = [
    artifact(1, "SCIENTIFIC_MASTER_VIDEO", "media/master.mp4", true),
    artifact(2, "FRAME_TIMESTAMPS", "timing/frames.json", true),
    artifact(3, "CAMERA_METADATA", "metadata/camera.json"), artifact(4, "CAPTURE_EVENTS", "events/capture.json"),
    artifact(5, "FINALIZATION_RECORD", "metadata/finalization.json"), artifact(6, "IMU_SAMPLES", "imu/samples.json", true),
    artifact(7, "IMU_METADATA", "imu/metadata.json")
  ];
  const value = {
    schema_version: "1.0.0", ...ids, package_content_sha256: "0".repeat(64), artifact_set_sha256: computeArtifactSetSha256(artifacts),
    source_kind: "ANDROID", protocol_snapshot_content_sha256: "a".repeat(64), configuration_content_sha256: "b".repeat(64),
    finalization_outcome: "FINALIZED_COMPLETE", finalized_utc: "2026-09-05T10:00:00.000Z", artifact_count: artifacts.length,
    package_byte_length: artifacts.reduce((sum, item) => sum + BigInt(item.byte_length), 0n).toString(), artifacts
  };
  value.package_content_sha256 = computePackageContentSha256(value);
  return value;
}
function incompleteManifest() {
  const value = manifest(); value.finalization_outcome = "FINALIZED_INCOMPLETE"; value.finalization_reason = "Recorder loss after source interruption";
  value.artifacts = value.artifacts.filter((item) => ["CAPTURE_EVENTS", "FINALIZATION_RECORD"].includes(item.role)); value.artifact_count = value.artifacts.length;
  value.package_byte_length = value.artifacts.reduce((sum, item) => sum + BigInt(item.byte_length), 0n).toString(); value.artifact_set_sha256 = computeArtifactSetSha256(value.artifacts); value.package_content_sha256 = computePackageContentSha256(value);
  return value;
}
function checkpoint(value, method = "HTTPS_RANGE") {
  return {
    schema_version: "1.0.0", checkpoint_id: "40000000-0000-4000-8000-000000000001", revision: 1,
    package_id: value.package_id, package_content_sha256: value.package_content_sha256, source_id: value.source_id, collection_method: method,
    created_utc: "2026-09-05T10:01:00.000Z", updated_utc: "2026-09-05T10:02:00.000Z",
    artifacts: value.artifacts.map((item) => ({ artifact_id: item.artifact_id, relative_path: item.relative_path, expected_byte_length: item.byte_length,
      expected_sha256: item.sha256, ...(method === "HTTPS_RANGE" ? { strong_etag: strongEtag(item.sha256) } : {}), state: "STAGED",
      received_ranges: [{ start: "0", end_exclusive: item.byte_length }], staged_byte_length: item.byte_length, attempt_count: 1, restart_count: 0 }))
  };
}
function verification(value, outcome = "VERIFIED") {
  const checks = ["MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES", "ARTIFACT_SET", "BYTE_LENGTH", "SHA256", "MASTER_STRUCTURE", "MASTER_FULL_DECODE", "TIMING_EVENTS", "METADATA_PROFILE", "FINALIZATION"]
    .map((check) => ({ check, required: true, disposition: "PASS", evidence_references: [`evidence/${check}.json`] }));
  return {
    schema_version: "1.0.0", verification_record_id: "50000000-0000-4000-8000-000000000001", revision: 1,
    package_id: value.package_id, package_content_sha256: value.package_content_sha256, artifact_set_sha256: value.artifact_set_sha256,
    verified_by_windows_account: "TEST\\operator", verifier_name: "HumCaptureVerifier", verifier_version: "1.0.0",
    verified_utc: "2026-09-05T10:03:00.000Z", outcome, checks,
    artifact_results: value.artifacts.map((item) => ({ artifact_id: item.artifact_id, relative_path: item.relative_path, required: item.required,
      expected_byte_length: item.byte_length, observed_byte_length: item.byte_length, expected_sha256: item.sha256, observed_sha256: item.sha256, disposition: "PASS" }))
  };
}

test("HC-XFR-TEST-001 transfer schemas compile and accept the complete valid bundle", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  ajv.addSchema(await json(path.join(controlSchemas, "common.schema.json")));
  const files = (await readdir(transferSchemas)).filter((name) => name.endsWith(".schema.json")).sort();
  for (const file of files) ajv.addSchema(await json(path.join(transferSchemas, file)));
  const value = manifest();
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/transfer/v1/package-manifest.schema.json")(value), true);
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/transfer/v1/collection-checkpoint.schema.json")(checkpoint(value)), true);
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/transfer/v1/package-verification-record.schema.json")(verification(value)), true);
  assert.deepEqual(files, ["collection-checkpoint.schema.json", "package-manifest.schema.json", "package-verification-record.schema.json"]);
});

test("HC-XFR-TEST-002 OpenAPI is HTTPS pull, versioned 3.1.2, range-aware, and bound to mutual TLS", async () => {
  const api = await json(openApiPath);
  assert.equal(api.openapi, "3.1.2"); assert.match(api.servers[0].url, /^https:/);
  assert.deepEqual(api.security, [{ HumCaptureMutualTLS: [] }]); assert.equal(api["x-humcapture-security-binding"], "HC-IF-SEC-001@1.0.0");
  const manifestRef = api.paths["/packages/{packageId}/manifest"].get.responses["200"].content["application/json"].schema.$ref;
  assert.deepEqual(await json(path.resolve(path.dirname(openApiPath), manifestRef)), await json(path.join(transferSchemas, "package-manifest.schema.json")));
  const artifact = api.paths["/packages/{packageId}/artifacts/{artifactId}"];
  assert.ok(artifact.get.responses["200"]); assert.ok(artifact.get.responses["206"]); assert.ok(artifact.get.responses["416"]);
  for (const header of ["ETag", "Content-Range", "Repr-Digest", "Content-Digest", "Accept-Ranges"]) assert.ok(artifact.get.responses["206"].headers[header]);
  assert.ok(artifact.get.parameters.some((item) => item.name === "Range")); assert.ok(artifact.get.parameters.some((item) => item.name === "If-Range"));
});

test("HC-XFR-TEST-003 manifest binds canonical content, artifact inventory, exact identity, timing, and package length", () => {
  assert.equal(validatePackageManifest(manifest()), true);
  const uvc = manifest(); uvc.source_kind = "UVC"; uvc.artifacts = uvc.artifacts.filter((item) => !item.role.startsWith("IMU_"));
  uvc.artifact_count = uvc.artifacts.length; uvc.package_byte_length = uvc.artifacts.reduce((sum, item) => sum + BigInt(item.byte_length), 0n).toString();
  uvc.artifact_set_sha256 = computeArtifactSetSha256(uvc.artifacts); uvc.package_content_sha256 = computePackageContentSha256(uvc);
  assert.equal(validatePackageManifest(uvc), true);
  assert.equal(validatePackageManifest(incompleteManifest()), true);
  for (const mutate of [
    (m) => { m.artifact_set_sha256 = "f".repeat(64); },
    (m) => { m.artifacts[0].source_id = "90000000-0000-4000-8000-000000000001"; },
    (m) => { delete m.artifacts[0].timing_coverage; },
    (m) => { m.package_byte_length = "1"; }
  ]) { const value = manifest(); mutate(value); assert.throws(() => validatePackageManifest(value)); }
});

test("HC-XFR-TEST-004 package paths fail closed for traversal, Windows aliases, normalization, collisions, and manifest recursion", () => {
  for (const value of ["../x", "/x", "x\\y", "CON.txt", "x. ", "package-manifest.json", "e\u0301.json"]) assert.throws(() => validatePackagePath(value));
  const value = manifest(); value.artifacts[1].relative_path = value.artifacts[0].relative_path.toUpperCase();
  throwsCode(() => validatePackageManifest(value), "DUPLICATE_ARTIFACT_PATH");
});

test("HC-XFR-TEST-005 checkpoints require the exact artifact set, bounded non-overlapping ranges, and truthful completion", () => {
  const value = manifest(); assert.equal(validateCollectionCheckpoint(value, checkpoint(value)), true);
  const overlap = checkpoint(value); overlap.artifacts[0].state = "RECEIVING"; overlap.artifacts[0].received_ranges = [{ start: "0", end_exclusive: "80" }, { start: "70", end_exclusive: "101" }]; overlap.artifacts[0].staged_byte_length = "111";
  throwsCode(() => validateCollectionCheckpoint(value, overlap), "CHECKPOINT_RANGE_INVALID");
  const falseComplete = checkpoint(value); falseComplete.artifacts[0].received_ranges = [{ start: "0", end_exclusive: "10" }]; falseComplete.artifacts[0].staged_byte_length = "10";
  throwsCode(() => validateCollectionCheckpoint(value, falseComplete), "ARTIFACT_FALSE_COMPLETE");
  const current = checkpoint(value); const next = clone(current); next.revision = 2; next.updated_utc = "2026-09-05T10:03:00.000Z";
  assert.equal(validateCheckpointTransition(current, next), true);
  const unaudited = clone(next); unaudited.artifacts[0].received_ranges = []; unaudited.artifacts[0].staged_byte_length = "0"; unaudited.artifacts[0].state = "PENDING";
  throwsCode(() => validateCheckpointTransition(current, unaudited), "CHECKPOINT_UNAUDITED_RESTART");
  const restart = clone(unaudited); restart.artifacts[0].restart_count = 1; restart.artifacts[0].last_restart_reason = "PARTIAL_CORRUPT";
  assert.equal(validateCheckpointTransition(current, restart), true);
});

test("HC-XFR-TEST-006 HTTPS resume combines only the same strong representation and handles RFC 9110 full/unsatisfied responses safely", () => {
  const etag = strongEtag("a".repeat(64));
  assert.equal(reconcileRangeResponse({ priorBytes: "100", expectedEtag: etag, status: 206, responseEtag: etag, contentRange: "bytes 100-199/1000" }), "APPEND_PARTIAL");
  throwsCode(() => reconcileRangeResponse({ priorBytes: "100", expectedEtag: etag, status: 206, responseEtag: strongEtag("b".repeat(64)), contentRange: "bytes 100-199/1000" }), "REPRESENTATION_VALIDATOR_CHANGED");
  assert.equal(reconcileRangeResponse({ priorBytes: "100", expectedEtag: etag, status: 200, responseEtag: etag }), "DISCARD_PARTIAL_AND_WRITE_FULL");
  assert.equal(reconcileRangeResponse({ priorBytes: "100", expectedEtag: etag, status: 416, responseEtag: etag }), "RECONCILE_MANIFEST_AND_RESTART_ARTIFACT");
});

test("HC-XFR-TEST-007 USB/MTP and local recovery reuse the package and verifier but restart incomplete artifacts", () => {
  const value = manifest(); assert.equal(validateCollectionCheckpoint(value, checkpoint(value, "USB_MTP")), true); assert.equal(validateCollectionCheckpoint(value, checkpoint(value, "COORDINATOR_LOCAL")), true);
  const staged = checkpoint(value, "USB_MTP").artifacts[0];
  assert.equal(offlineArtifactRecoveryAction(staged), "RESTART_ARTIFACT_FROM_ZERO");
  staged.state = "VERIFIED"; staged.artifact_verification_id = "50000000-0000-4000-8000-000000000001";
  assert.equal(offlineArtifactRecoveryAction(staged), "SKIP_VERIFIED_ARTIFACT");
  const partial = checkpoint(value, "USB_MTP"); partial.artifacts[0].state = "RECEIVING"; partial.artifacts[0].received_ranges = [{ start: "0", end_exclusive: "10" }]; partial.artifacts[0].staged_byte_length = "10";
  throwsCode(() => validateCollectionCheckpoint(value, partial), "OFFLINE_PARTIAL_RESUME_FORBIDDEN");
});

test("HC-XFR-TEST-008 verification rejects missing, unassessed, false-pass, and incomplete required evidence", () => {
  const value = manifest(); assert.equal(validatePackageVerification(value, verification(value)), true);
  const incomplete = incompleteManifest(); const survivorVerification = verification(incomplete);
  for (const check of survivorVerification.checks.filter((item) => ["MASTER_STRUCTURE", "MASTER_FULL_DECODE", "METADATA_PROFILE"].includes(item.check))) { check.required = false; check.disposition = "NOT_APPLICABLE"; check.evidence_references = []; }
  assert.equal(validatePackageVerification(incomplete, survivorVerification), true);
  const unassessed = verification(value); unassessed.checks[0].disposition = "NOT_ASSESSED";
  throwsCode(() => validatePackageVerification(value, unassessed), "VERIFICATION_FALSE_AGGREGATE");
  const falseArtifact = verification(value); falseArtifact.artifact_results[0].observed_sha256 = "f".repeat(64);
  throwsCode(() => validatePackageVerification(value, falseArtifact), "VERIFICATION_ARTIFACT_FALSE_RESULT");
});

test("HC-XFR-TEST-009 package identity is idempotent only for the same content hash", () => {
  const value = manifest(); assert.equal(classifyPackageIdentity(undefined, value), "NEW"); assert.equal(classifyPackageIdentity(value, clone(value)), "IDEMPOTENT");
  const conflict = clone(value); conflict.package_content_sha256 = "f".repeat(64);
  assert.equal(classifyPackageIdentity(value, conflict), "QUARANTINE_IDENTITY_CONFLICT");
});

test("HC-XFR-TEST-010 commit receipt requires exact full verification and committed identity", () => {
  const value = manifest(); const verified = verification(value);
  const custody = { schema_version: "1.0.0", custody_record_id: "60000000-0000-4000-8000-000000000001", revision: 8,
    session_id: value.session_id, trial_id: value.trial_id, source_id: value.source_id, capture_attempt_id: value.capture_attempt_id,
    package_id: value.package_id, package_content_sha256: value.package_content_sha256, state: "COMMITTED", authority: "REPOSITORY", collection_method: "HTTPS",
    verification_record_id: verified.verification_record_id, commit_id: "60000000-0000-4000-8000-000000000002", repository_relative_path: "subjects/S1/session/package", recorded_utc: "2026-09-05T10:04:00.000Z" };
  const receipt = { schema_version: "1.0.0", receipt_id: "60000000-0000-4000-8000-000000000003", receipt_revision: 1, commit_id: custody.commit_id,
    session_id: value.session_id, trial_id: value.trial_id, source_id: value.source_id, capture_attempt_id: value.capture_attempt_id, package_id: value.package_id,
    package_content_sha256: value.package_content_sha256, artifact_set_sha256: value.artifact_set_sha256, repository_relative_path: custody.repository_relative_path,
    issued_by_coordinator_id: "60000000-0000-4000-8000-000000000004", issued_utc: "2026-09-05T10:05:00.000Z" };
  assert.equal(validateVerifiedCommit(value, verified, custody, receipt), true);
  const bad = clone(receipt); bad.artifact_set_sha256 = "f".repeat(64); throwsCode(() => validateVerifiedCommit(value, verified, custody, bad), "RECEIPT_ARTIFACT_SET_MISMATCH");
});

test("HC-XFR-TEST-011 digest and validator encodings are deterministic and strong", () => {
  assert.equal(strongEtag("a".repeat(64)), `"sha256-${"a".repeat(64)}"`);
  assert.equal(contentDigest(Buffer.from("HumCapture", "utf8")), "sha-256=:B3I/Jv+vnEgcR1Y1ruwc8EVD1wcDkCLPpJpAec9tHF0=:");
});

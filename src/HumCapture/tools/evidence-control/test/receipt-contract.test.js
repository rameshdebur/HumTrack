import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  classifyCommitReceiptReplay,
  validateCleanupRequest,
  validateCleanupResult,
  validateCleanupRetry,
  validateCommitReceipt,
  validateReceiptAcknowledgement,
  validateReceiptStatusReconciliation
} from "../src/control-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "control", "v1");
const id = (suffix) => `81000000-0000-4000-8000-${suffix.toString().padStart(12, "0")}`;
const hash = (letter) => letter.repeat(64);
const clone = structuredClone;
const throwsCode = (fn, code) => assert.throws(fn, (error) => error?.code === code);

async function validators() {
  const ajv = new Ajv2020({ allErrors: true, strict: true });
  addFormats(ajv);
  for (const name of (await readdir(schemaRoot)).filter((value) => value.endsWith(".schema.json")).sort()) {
    ajv.addSchema(JSON.parse(await readFile(path.join(schemaRoot, name), "utf8")));
  }
  return ajv;
}

function receipt() {
  return {
    schema_version: "1.0.0", receipt_id: id(1), receipt_revision: 1, commit_id: id(2),
    session_id: id(3), trial_id: id(4), device_id: id(5), source_id: id(6), capture_attempt_id: id(7), package_id: id(8),
    package_content_sha256: hash("a"), artifact_set_sha256: hash("b"), repository_relative_path: "subjects/S1/sessions/A/packages/P1",
    issued_by_coordinator_id: id(9), issued_utc: "2026-09-07T08:00:00.000Z"
  };
}

function custody(value = receipt(), state = "COMMITTED") {
  return {
    schema_version: "1.0.0", custody_record_id: id(10), revision: 8,
    session_id: value.session_id, trial_id: value.trial_id, source_id: value.source_id, capture_attempt_id: value.capture_attempt_id,
    package_id: value.package_id, package_content_sha256: value.package_content_sha256, state, authority: "REPOSITORY", collection_method: "HTTPS",
    verification_record_id: id(11), commit_id: value.commit_id, repository_relative_path: value.repository_relative_path,
    recorded_utc: "2026-09-07T07:59:59.000Z"
  };
}

function acknowledgement(value = receipt(), result = "ACCEPTED") {
  return {
    schema_version: "1.0.0", acknowledgement_id: id(12), receipt_id: value.receipt_id, receipt_revision: value.receipt_revision,
    device_id: value.device_id, source_id: value.source_id, package_id: value.package_id, package_content_sha256: value.package_content_sha256,
    receipt_durably_stored: result !== "REJECTED_MISMATCH", result,
    ...(result === "REJECTED_MISMATCH" ? { reason_code: "PACKAGE_HASH_MISMATCH" } : {}),
    received_utc: "2026-09-07T08:00:02.000Z"
  };
}

function cleanupRequest(value = receipt(), ack = acknowledgement(value), paths = ["master.mp4", "manifest.json"]) {
  return {
    schema_version: "1.0.0", cleanup_request_id: id(13), device_id: value.device_id, operator_confirmation: true, operator_event_id: id(14),
    packages: [{ receipt_id: value.receipt_id, receipt_revision: 1, acknowledgement_id: ack.acknowledgement_id,
      source_id: value.source_id, package_id: value.package_id, package_content_sha256: value.package_content_sha256,
      package_size_bytes: "123456", capture_finalized_utc: "2026-09-07T07:55:00.000Z", committed_utc: value.issued_utc,
      artifact_relative_paths: paths }],
    requested_utc: "2026-09-07T08:01:00.000Z"
  };
}

function cleanupResult(request, outcome = "DELETED", remaining = []) {
  const selected = request.packages[0];
  return {
    schema_version: "1.0.0", cleanup_result_id: id(15), cleanup_request_id: request.cleanup_request_id, device_id: request.device_id,
    packages: [{ receipt_id: selected.receipt_id, source_id: selected.source_id, package_id: selected.package_id,
      package_content_sha256: selected.package_content_sha256, result: outcome, remaining_artifact_relative_paths: remaining,
      ...(outcome === "PARTIAL_DELETE" ? { reason_code: "FILE_LOCKED" } : {}) }],
    completed_utc: "2026-09-07T08:01:02.000Z"
  };
}

test("HC-RCP-TEST-001 receipt, acknowledgement, recovery and cleanup schemas compile and validate", async () => {
  const ajv = await validators();
  const value = receipt(); const ack = acknowledgement(value); const request = cleanupRequest(value, ack); const result = cleanupResult(request);
  const query = { schema_version: "1.0.0", query_id: id(16), receipt_id: value.receipt_id, receipt_revision: 1, device_id: value.device_id,
    source_id: value.source_id, package_id: value.package_id, package_content_sha256: value.package_content_sha256, queried_utc: "2026-09-07T08:00:03.000Z" };
  const response = { ...query, status: "ACKNOWLEDGED", acknowledgement_id: ack.acknowledgement_id, reported_utc: "2026-09-07T08:00:04.000Z" };
  delete response.queried_utc;
  for (const [name, record] of [["package-commit-receipt", value], ["receipt-acknowledgement", ack], ["receipt-status-query", query], ["receipt-status-response", response], ["cleanup-request", request], ["cleanup-result", result]]) {
    assert.equal(ajv.getSchema(`https://humtrack.invalid/humcapture/control/v1/${name}.schema.json`)(record), true, name);
  }
  const historicalReceipt = clone(value); delete historicalReceipt.device_id;
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/control/v1/package-commit-receipt.schema.json")(historicalReceipt), true, "backward-compatible historical receipt parse");
  const api = JSON.parse(await readFile(path.join(humCaptureRoot, "docs", "interfaces", "asyncapi", "control-v1.asyncapi.json"), "utf8"));
  assert.equal(api.info.version, "1.3.0");
  for (const name of ["receiptAcknowledgement", "receiptStatusQuery", "receiptStatusResponse", "cleanupRequest", "cleanupResult"]) {
    assert.ok(api.components.messages[name]?.payload?.schema?.$ref, `missing AsyncAPI message ${name}`);
  }
  for (const name of ["receiveReceiptAcknowledgement", "sendReceiptStatusQuery", "receiveReceiptStatusResponse", "sendCleanupRequest", "receiveCleanupResult"]) {
    assert.ok(api.operations[name], `missing AsyncAPI operation ${name}`);
  }
});

test("HC-RCP-TEST-002 receipt binds exact durable commit and cannot be revised or superseded", () => {
  const value = receipt(); assert.equal(validateCommitReceipt(custody(value), value), true);
  const preCommit = custody(value, "VERIFIED"); preCommit.authority = "VERIFIER";
  throwsCode(() => validateCommitReceipt(preCommit, value), "RECEIPT_BEFORE_COMMIT");
  const revised = clone(value); revised.receipt_revision = 2;
  throwsCode(() => validateCommitReceipt(custody(value), revised), "RECEIPT_REVISION_INVALID");
});

test("HC-RCP-TEST-003 receipt replay is idempotent and conflicting identity enters recovery", () => {
  const value = receipt();
  assert.equal(classifyCommitReceiptReplay([], value), "NEW");
  assert.equal(classifyCommitReceiptReplay([value], clone(value)), "IDEMPOTENT_REPLAY");
  const conflict = clone(value); conflict.package_content_sha256 = hash("c");
  throwsCode(() => classifyCommitReceiptReplay([value], conflict), "RECEIPT_IMMUTABILITY_CONFLICT");
});

test("HC-RCP-TEST-004 accepted acknowledgement proves durable exact storage; mismatch retains files", () => {
  const value = receipt(); const ack = acknowledgement(value); const local = { device_id: value.device_id, source_id: value.source_id, package_id: value.package_id, package_content_sha256: value.package_content_sha256 };
  assert.equal(validateReceiptAcknowledgement(value, ack, local), "RECEIPT_ACKNOWLEDGED");
  const falseAcceptance = clone(ack); falseAcceptance.package_content_sha256 = hash("c");
  throwsCode(() => validateReceiptAcknowledgement(value, falseAcceptance, local), "RECEIPT_ACK_IDENTITY_MISMATCH");
  const rejection = acknowledgement(value, "REJECTED_MISMATCH"); rejection.package_content_sha256 = hash("c");
  assert.equal(validateReceiptAcknowledgement(value, rejection, local), "RETAIN_AND_RECOVER");
});

test("HC-RCP-TEST-005 lost acknowledgement reconciles before replaying the same receipt", () => {
  const value = receipt();
  const query = { query_id: id(16), receipt_id: value.receipt_id, receipt_revision: 1, device_id: value.device_id, source_id: value.source_id, package_id: value.package_id, package_content_sha256: value.package_content_sha256 };
  assert.equal(validateReceiptStatusReconciliation(value, query, { ...query, status: "NOT_STORED" }), "RESEND_IDENTICAL_RECEIPT");
  assert.equal(validateReceiptStatusReconciliation(value, query, { ...query, status: "STORED_ACK_PENDING" }), "REQUEST_SAME_ACKNOWLEDGEMENT");
  assert.equal(validateReceiptStatusReconciliation(value, query, { ...query, status: "ACKNOWLEDGED", acknowledgement_id: id(12) }), "RECONCILE_EXISTING_ACKNOWLEDGEMENT");
});

test("HC-RCP-TEST-006 cleanup requires explicit confirmation, durable acknowledgement, exact binding and idle package", () => {
  const value = receipt(); const ack = acknowledgement(value); const request = cleanupRequest(value, ack);
  assert.equal(validateCleanupRequest(request, [value], [ack]), true);
  const unconfirmed = clone(request); unconfirmed.operator_confirmation = false;
  throwsCode(() => validateCleanupRequest(unconfirmed, [value], [ack]), "CLEANUP_CONFIRMATION_REQUIRED");
  throwsCode(() => validateCleanupRequest(request, [value], [ack], { activePackageIds: [value.package_id] }), "CLEANUP_PACKAGE_ACTIVE");
  throwsCode(() => validateCleanupRequest(request, [value], [], {}), "CLEANUP_DURABLE_EVIDENCE_MISSING");
});

test("HC-RCP-TEST-007 USB/MTP offline completion cannot authorize automatic deletion", () => {
  const value = receipt(); const ack = acknowledgement(value); const request = cleanupRequest(value, ack);
  throwsCode(() => validateCleanupRequest(request, [value], [ack], { offlineOnlyPackageIds: [value.package_id] }), "CLEANUP_OFFLINE_AUTOMATION_FORBIDDEN");
});

test("HC-RCP-TEST-008 cleanup result reports every package and never hides remaining files", () => {
  const value = receipt(); const request = cleanupRequest(value, acknowledgement(value));
  assert.equal(validateCleanupResult(request, cleanupResult(request)), true);
  const falseDeleted = cleanupResult(request, "DELETED", ["master.mp4"]);
  throwsCode(() => validateCleanupResult(request, falseDeleted), "CLEANUP_FALSE_DELETED");
  const partial = cleanupResult(request, "PARTIAL_DELETE", ["master.mp4"]);
  assert.equal(validateCleanupResult(request, partial), true);
});

test("HC-RCP-TEST-009 partial retry targets only reconciled remaining artifacts", () => {
  const value = receipt(); const ack = acknowledgement(value); const request = cleanupRequest(value, ack);
  const partial = cleanupResult(request, "PARTIAL_DELETE", ["master.mp4"]);
  const retry = cleanupRequest(value, ack, ["master.mp4"]); retry.cleanup_request_id = id(17);
  assert.equal(validateCleanupRetry(request, partial, retry), true);
  retry.packages[0].artifact_relative_paths.push("manifest.json");
  throwsCode(() => validateCleanupRetry(request, partial, retry), "CLEANUP_RETRY_SET_MISMATCH");
});

test("HC-RCP-TEST-010 commit remains session completion authority; cleanup records contain no subject PII or secret/signature fields", () => {
  const value = receipt(); const ack = acknowledgement(value); const request = cleanupRequest(value, ack); const result = cleanupResult(request);
  for (const record of [value, ack, request, result]) {
    const keys = JSON.stringify(record).toLowerCase();
    for (const forbidden of ["subject_name", "date_of_birth", "token", "secret", "signature", "private_key"]) assert.equal(keys.includes(forbidden), false);
  }
  assert.equal(custody(value).state, "COMMITTED");
  assert.equal(validateCleanupRequest(request, [value], [ack]), true);
});

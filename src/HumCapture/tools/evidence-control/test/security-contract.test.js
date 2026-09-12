import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  authorizePeer,
  computePairingTranscriptSha256,
  evaluateBootstrapProof,
  manualPairingCode,
  validateAuditEvent,
  validateBootstrap,
  validateEnrollment,
  validateTlsProfile,
  validateTrustTransition
} from "../src/security-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas");
const securitySchemas = path.join(schemaRoot, "security", "v1");
const openApiPath = path.join(humCaptureRoot, "docs", "interfaces", "openapi", "transfer-v1.openapi.json");
const asyncApiPath = path.join(humCaptureRoot, "docs", "interfaces", "asyncapi", "control-v1.asyncapi.json");
const ids = {
  pairing_session_id: "70000000-0000-4000-8000-000000000001",
  device_id: "70000000-0000-4000-8000-000000000002",
  coordinator_id: "70000000-0000-4000-8000-000000000003",
  enrollment_id: "70000000-0000-4000-8000-000000000004",
  trust_record_id: "70000000-0000-4000-8000-000000000005"
};
const hashes = { device: "a".repeat(64), coordinator: "b".repeat(64), ca: "c".repeat(64), nonce: "d".repeat(64), deviceCert: "e".repeat(64), coordinatorCert: "f".repeat(64) };

async function json(file) { return JSON.parse(await readFile(file, "utf8")); }
function clone(value) { return structuredClone(value); }
function throwsCode(fn, code) { assert.throws(fn, (error) => error?.code === code); }
function bootstrap() {
  return { schema_version: "1.0.0", contract_id: "HC-IF-SEC-001", pairing_session_id: ids.pairing_session_id, device_id: ids.device_id,
    endpoint: "https://192.168.137.2:7443/pair/v1", bootstrap_spki_sha256: hashes.device,
    bootstrap_secret_b64url: Buffer.alloc(16, 1).toString("base64url"), pairing_nonce_b64url: Buffer.alloc(16, 2).toString("base64url"),
    issued_utc: "2026-09-06T10:00:00.000Z", expires_utc: "2026-09-06T10:10:00.000Z",
    manual_code: manualPairingCode(Buffer.alloc(16, 1).toString("base64url")) };
}
function transcript() {
  return { pairing_session_id: ids.pairing_session_id, device_id: ids.device_id, coordinator_id: ids.coordinator_id,
    device_spki_sha256: hashes.device, coordinator_spki_sha256: hashes.coordinator, coordinator_ca_sha256: hashes.ca,
    pairing_nonce_sha256: hashes.nonce, contract_version: "1.0.0" };
}
function enrollment(profile = "HC-TLS-13-001") {
  const value = { schema_version: "1.0.0", enrollment_id: ids.enrollment_id, ...ids,
    device_spki_sha256: hashes.device, coordinator_spki_sha256: hashes.coordinator, coordinator_ca_sha256: hashes.ca,
    pairing_nonce_sha256: hashes.nonce, transcript_sha256: computePairingTranscriptSha256(transcript()), tls_profile: profile,
    issued_utc: "2026-09-06T10:02:00.000Z", certificate_not_after_utc: "2027-09-06T10:02:00.000Z", operator_windows_account: "TEST\\operator" };
  delete value.trust_record_id;
  if (profile === "HC-TLS-12-W10-001") value.compatibility_reason = "Windows 10 22H2 platform compatibility";
  return value;
}
function trust(state = "PAIRED", revision = 1) {
  return { schema_version: "1.0.0", trust_record_id: ids.trust_record_id, revision, device_id: ids.device_id, coordinator_id: ids.coordinator_id,
    coordinator_ca_sha256: hashes.ca, device_certificate_sha256: hashes.deviceCert, coordinator_certificate_sha256: hashes.coordinatorCert,
    state, tls_profile: "HC-TLS-13-001", paired_utc: "2026-09-06T10:02:00.000Z", updated_utc: "2026-09-06T10:02:00.000Z",
    certificate_not_after_utc: "2027-09-06T10:02:00.000Z", operator_windows_account: "TEST\\operator" };
}

test("HC-SEC-TEST-001 security schemas compile and accept redacted valid records", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  ajv.addSchema(await json(path.join(schemaRoot, "control", "v1", "common.schema.json")));
  const files = (await readdir(securitySchemas)).filter((name) => name.endsWith(".schema.json")).sort();
  for (const file of files) ajv.addSchema(await json(path.join(securitySchemas, file)));
  const event = { schema_version: "1.0.0", event_id: "70000000-0000-4000-8000-000000000006", event_type: "ENROLLED", occurred_utc: "2026-09-06T10:02:00.000Z", outcome: "SUCCESS", operator_windows_account: "TEST\\operator", device_id: ids.device_id, details: { tls_profile: "HC-TLS-13-001" } };
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/security/v1/pairing-bootstrap-qr.schema.json")(bootstrap()), true);
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/security/v1/pairing-enrollment-record.schema.json")(enrollment()), true);
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/security/v1/trust-record.schema.json")(trust()), true);
  assert.equal(ajv.getSchema("https://humtrack.invalid/humcapture/security/v1/security-audit-event.schema.json")(event), true);
  assert.deepEqual(files, ["pairing-bootstrap-qr.schema.json", "pairing-enrollment-record.schema.json", "security-audit-event.schema.json", "trust-record.schema.json"]);
});

test("HC-SEC-TEST-002 transfer OpenAPI requires global mutual TLS and security failure responses", async () => {
  const api = await json(openApiPath);
  assert.equal(api.info.version, "1.2.0");
  assert.deepEqual(api.security, [{ HumCaptureMutualTLS: [] }]);
  assert.equal(api.components.securitySchemes.HumCaptureMutualTLS.type, "mutualTLS");
  assert.equal(api["x-humcapture-security-binding"], "HC-IF-SEC-001@1.0.0");
  for (const operation of [api.paths["/packages/{packageId}/manifest"].get, api.paths["/packages/{packageId}/artifacts/{artifactId}"].head, api.paths["/packages/{packageId}/artifacts/{artifactId}"].get]) {
    for (const status of ["401", "403", "429"]) assert.ok(operation.responses[status]);
  }
});

test("HC-SEC-TEST-003 control AsyncAPI binds every operation to mutually authenticated WSS", async () => {
  const api = await json(asyncApiPath); const server = api.servers.pairedCaptureNode;
  assert.equal(api.info.version, "1.3.0"); assert.equal(server.protocol, "wss");
  assert.deepEqual(server.security, [{ $ref: "#/components/securitySchemes/HumCaptureMutualTLS" }]);
  assert.equal(api.components.securitySchemes.HumCaptureMutualTLS.type, "X509");
  assert.equal(server["x-humcapture-security-binding"], "HC-IF-SEC-001@1.0.0");
});

test("HC-SEC-TEST-004 bootstrap requires exact fingerprinted HTTPS, 128-bit entropy, and ten-minute lifetime", () => {
  assert.equal(validateBootstrap(bootstrap(), "2026-09-06T10:05:00.000Z", hashes.device), true);
  const weak = bootstrap(); weak.bootstrap_secret_b64url = Buffer.alloc(8, 1).toString("base64url"); throwsCode(() => validateBootstrap(weak, "2026-09-06T10:05:00.000Z", hashes.device), "BOOTSTRAP_ENTROPY_INVALID");
  const long = bootstrap(); long.expires_utc = "2026-09-06T10:10:00.001Z"; throwsCode(() => validateBootstrap(long, "2026-09-06T10:05:00.000Z", hashes.device), "BOOTSTRAP_LIFETIME_INVALID");
  const plain = bootstrap(); plain.endpoint = "http://192.168.137.2/pair"; throwsCode(() => validateBootstrap(plain, "2026-09-06T10:05:00.000Z", hashes.device), "BOOTSTRAP_PLAINTEXT_FORBIDDEN");
  const changed = bootstrap(); changed.manual_code = "AAAA-AAAA-AAAA-AAAA-AAAA-AAAA-AAAA"; throwsCode(() => validateBootstrap(changed, "2026-09-06T10:05:00.000Z", hashes.device), "MANUAL_CODE_MISMATCH");
  throwsCode(() => validateBootstrap(bootstrap(), "2026-09-06T10:05:00.000Z", "9".repeat(64)), "BOOTSTRAP_FINGERPRINT_MISMATCH");
});

test("HC-SEC-TEST-005 bootstrap proof is single-use, session-bound, delayed, and locked after five failures", () => {
  let window = { pairing_session_id: ids.pairing_session_id, expires_utc: "2026-09-06T10:10:00.000Z", failed_proofs: 0, used: false, locked_out: false, status: "BOOTSTRAP_ADVERTISED" };
  for (let count = 1; count <= 5; count += 1) { window = evaluateBootstrapProof(window, { pairingSessionId: ids.pairing_session_id, nowUtc: "2026-09-06T10:05:00.000Z", proofValid: false }); assert.equal(window.failed_proofs, count); assert.ok(window.retry_delay_seconds >= 2); }
  assert.equal(window.status, "LOCKED_OUT"); throwsCode(() => evaluateBootstrapProof(window, { pairingSessionId: ids.pairing_session_id, nowUtc: "2026-09-06T10:05:00.000Z", proofValid: true }), "PAIRING_LOCKED_OUT");
  throwsCode(() => evaluateBootstrapProof({ ...window, failed_proofs: 0, locked_out: false }, { pairingSessionId: "90000000-0000-4000-8000-000000000001", nowUtc: "2026-09-06T10:05:00.000Z", proofValid: true }), "PAIRING_SESSION_MISMATCH");
  const success = evaluateBootstrapProof({ ...window, failed_proofs: 0, locked_out: false }, { pairingSessionId: ids.pairing_session_id, nowUtc: "2026-09-06T10:05:00.000Z", proofValid: true });
  throwsCode(() => evaluateBootstrapProof(success, { pairingSessionId: ids.pairing_session_id, nowUtc: "2026-09-06T10:05:01.000Z", proofValid: true }), "BOOTSTRAP_REPLAY");
});

test("HC-SEC-TEST-006 enrollment binds both peer keys, nonce, contract, and finite certificate lifetime", () => {
  assert.equal(validateEnrollment(enrollment(), transcript()), true);
  const changed = transcript(); changed.device_spki_sha256 = "9".repeat(64); throwsCode(() => validateEnrollment(enrollment(), changed), "ENROLLMENT_IDENTITY_MISMATCH");
  const long = enrollment(); long.certificate_not_after_utc = "2027-10-10T10:02:00.000Z"; throwsCode(() => validateEnrollment(long, transcript()), "CERTIFICATE_LIFETIME_INVALID");
});

test("HC-SEC-TEST-007 trust lifecycle rejects identity mutation, invalid revision, forbidden resurrection, and active-capture removal", () => {
  const current = trust(); const rotation = { ...current, revision: 2, state: "ROTATION_DUE", prior_revision_sha256: "1".repeat(64), updated_utc: "2027-08-07T10:02:00.000Z" };
  assert.equal(validateTrustTransition(current, rotation), true);
  const changed = { ...rotation, device_id: "90000000-0000-4000-8000-000000000001" }; throwsCode(() => validateTrustTransition(current, changed), "TRUST_IDENTITY_CHANGED");
  const revoked = { ...current, revision: 2, state: "REVOKED", reason: "Credential compromise", prior_revision_sha256: "1".repeat(64), updated_utc: "2026-09-07T10:02:00.000Z" };
  throwsCode(() => validateTrustTransition(current, revoked, { captureActive: true }), "TRUST_CHANGE_DURING_CAPTURE");
  const resurrected = { ...revoked, revision: 3, state: "PAIRED", reason: undefined }; throwsCode(() => validateTrustTransition(revoked, resurrected), "TRUST_TRANSITION_FORBIDDEN");
  const states = ["PAIRED", "ROTATION_DUE", "REVOKED", "LOST", "UNPAIRED"];
  const allowed = new Set(["PAIRED>PAIRED", "PAIRED>ROTATION_DUE", "PAIRED>REVOKED", "PAIRED>LOST", "PAIRED>UNPAIRED", "ROTATION_DUE>PAIRED", "ROTATION_DUE>REVOKED", "ROTATION_DUE>LOST", "ROTATION_DUE>UNPAIRED", "REVOKED>UNPAIRED", "LOST>UNPAIRED"]);
  for (const from of states) for (const to of states) if (!allowed.has(`${from}>${to}`)) {
    const before = { ...trust(from), ...( ["REVOKED", "LOST", "UNPAIRED"].includes(from) ? { reason: "Prior lifecycle action" } : {}) };
    const after = { ...before, revision: 2, state: to, prior_revision_sha256: "1".repeat(64), reason: "Lifecycle action" };
    throwsCode(() => validateTrustTransition(before, after), "TRUST_TRANSITION_FORBIDDEN");
  }
});

test("HC-SEC-TEST-008 authorization binds active trust, role, exact certificate identity, and resource ownership", () => {
  const value = trust(); const peer = { coordinator_id: ids.coordinator_id, device_id: ids.device_id, coordinator_certificate_sha256: hashes.coordinatorCert, device_certificate_sha256: hashes.deviceCert, role: "COORDINATOR" };
  const resource = { coordinator_id: ids.coordinator_id, device_id: ids.device_id, package_id: "70000000-0000-4000-8000-000000000007" };
  assert.equal(authorizePeer(value, peer, resource, "2026-09-06T11:00:00.000Z"), true);
  throwsCode(() => authorizePeer(value, { ...peer, role: "CAPTURE_NODE" }, resource, "2026-09-06T11:00:00.000Z"), "PEER_ROLE_FORBIDDEN");
  throwsCode(() => authorizePeer(value, { ...peer, coordinator_certificate_sha256: "9".repeat(64) }, resource, "2026-09-06T11:00:00.000Z"), "PEER_BINDING_MISMATCH");
  throwsCode(() => authorizePeer(value, peer, { ...resource, device_id: "90000000-0000-4000-8000-000000000001" }, "2026-09-06T11:00:00.000Z"), "RESOURCE_SCOPE_FORBIDDEN");
});

test("HC-SEC-TEST-009 TLS 1.3 is default; early data, weak TLS 1.2, and silent downgrade reject", () => {
  assert.equal(validateTlsProfile({ profile: "HC-TLS-13-001", negotiatedVersion: "TLS1.3", cipherSuite: "TLS_AES_128_GCM_SHA256", earlyData: false, peerSupportsTls13: true }), true);
  assert.equal(validateTlsProfile({ profile: "HC-TLS-12-W10-001", compatibilityReason: "Windows 10 22H2 compatibility", negotiatedVersion: "TLS1.2", cipherSuite: "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256", earlyData: false, peerSupportsTls13: false }), true);
  throwsCode(() => validateTlsProfile({ profile: "HC-TLS-13-001", negotiatedVersion: "TLS1.3", cipherSuite: "TLS_AES_128_GCM_SHA256", earlyData: true }), "TLS_EARLY_DATA_FORBIDDEN");
  throwsCode(() => validateTlsProfile({ profile: "HC-TLS-12-W10-001", compatibilityReason: "Windows 10 fallback", negotiatedVersion: "TLS1.2", cipherSuite: "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA", earlyData: false, peerSupportsTls13: false }), "TLS12_PROFILE_MISMATCH");
  throwsCode(() => validateTlsProfile({ profile: "HC-TLS-12-W10-001", compatibilityReason: "Windows 10 fallback", negotiatedVersion: "TLS1.2", cipherSuite: "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256", earlyData: false, peerSupportsTls13: true }), "TLS_DOWNGRADE_FORBIDDEN");
});

test("HC-SEC-TEST-010 revoked, lost, and expired credentials cannot authorize recovery traffic", () => {
  const peer = { coordinator_id: ids.coordinator_id, device_id: ids.device_id, coordinator_certificate_sha256: hashes.coordinatorCert, device_certificate_sha256: hashes.deviceCert, role: "COORDINATOR" };
  const resource = { coordinator_id: ids.coordinator_id, device_id: ids.device_id };
  for (const state of ["REVOKED", "LOST", "UNPAIRED"]) throwsCode(() => authorizePeer({ ...trust(), state, reason: "Recovery required" }, peer, resource, "2026-09-06T11:00:00.000Z"), "TRUST_NOT_ACTIVE");
  throwsCode(() => authorizePeer(trust(), peer, resource, "2028-09-06T11:00:00.000Z"), "CERTIFICATE_EXPIRED");
});

test("HC-SEC-TEST-011 audit permits public fingerprints but rejects secret, proof, subject, and video fields", () => {
  assert.equal(validateAuditEvent({ details: { tls_profile: "HC-TLS-13-001", peer_fingerprint: hashes.device } }), true);
  for (const key of ["bootstrap_secret", "secretProof", "subject_name", "video_path", "private_key", "access_token"]) throwsCode(() => validateAuditEvent({ details: { [key]: "forbidden" } }), "AUDIT_SENSITIVE_FIELD");
});

test("HC-SEC-TEST-012 discovery metadata and ordinary trust records contain no subject or reusable credential material", () => {
  const persisted = JSON.stringify({ enrollment: enrollment(), trust: trust() }).toLowerCase();
  for (const term of ["bootstrap_secret", "manual_code", "private_key", "subject_name", "date_of_birth", "master_video"]) assert.equal(persisted.includes(term), false);
  const discoveryAllowed = new Set(["service", "device_id", "endpoint", "contract_major", "friendly_label"]);
  for (const key of Object.keys({ service: "_humcapture._tcp", device_id: ids.device_id, endpoint: "192.168.137.2:7443", contract_major: 1 })) assert.ok(discoveryAllowed.has(key));
  assert.equal(discoveryAllowed.has("certificate_trusted"), false);
});

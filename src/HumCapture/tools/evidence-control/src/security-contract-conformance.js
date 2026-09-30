import { createHash } from "node:crypto";
import { ContractConformanceError } from "./control-contract-conformance.js";
import { canonicalJson } from "./transfer-contract-conformance.js";

const MAX_BOOTSTRAP_MS = 10 * 60 * 1000;
const MAX_FAILED_PROOFS = 5;
const MAX_CERTIFICATE_MS = 397 * 24 * 60 * 60 * 1000;
const IMMUTABLE_TRUST = ["trust_record_id", "device_id", "coordinator_id", "coordinator_ca_sha256", "paired_utc"];
const TRUST_TRANSITIONS = new Map([
  ["PAIRED", new Set(["PAIRED", "ROTATION_DUE", "REVOKED", "LOST", "UNPAIRED"])],
  ["ROTATION_DUE", new Set(["PAIRED", "REVOKED", "LOST", "UNPAIRED"])],
  ["REVOKED", new Set(["UNPAIRED"])],
  ["LOST", new Set(["UNPAIRED"])],
  ["UNPAIRED", new Set()]
]);
const FORBIDDEN_DETAIL_KEY = /(secret|token|password|private|proof|subject|video|dob|name)/i;
const BASE32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

function reject(code, message) {
  throw new ContractConformanceError(code, message);
}

function parseUtc(value, label) {
  const result = Date.parse(value);
  if (!Number.isFinite(result)) reject("UTC_INVALID", `${label} must be a valid UTC timestamp.`);
  return result;
}

function base64urlBytes(value, label) {
  if (typeof value !== "string" || !/^[A-Za-z0-9_-]+$/.test(value)) reject("BOOTSTRAP_ENTROPY_INVALID", `${label} must be unpadded base64url.`);
  const bytes = Buffer.from(value, "base64url");
  if (bytes.toString("base64url") !== value || bytes.length < 16) reject("BOOTSTRAP_ENTROPY_INVALID", `${label} must canonically encode at least 128 bits.`);
  return bytes;
}

function base32(bytes) {
  let bits = 0; let accumulator = 0; let output = "";
  for (const byte of bytes) {
    accumulator = (accumulator << 8) | byte; bits += 8;
    while (bits >= 5) { bits -= 5; output += BASE32[(accumulator >>> bits) & 31]; }
    accumulator &= (1 << bits) - 1;
  }
  if (bits) output += BASE32[(accumulator << (5 - bits)) & 31];
  return output;
}

export function manualPairingCode(secretB64url) {
  const secret = base64urlBytes(secretB64url, "Bootstrap secret");
  const checksum = base32(createHash("sha256").update(secret).digest()).slice(0, 2);
  return `${base32(secret)}${checksum}`.match(/.{1,4}/g).join("-");
}

export function computePairingTranscriptSha256(fields) {
  const required = ["pairing_session_id", "device_id", "coordinator_id", "device_spki_sha256", "coordinator_spki_sha256", "coordinator_ca_sha256", "pairing_nonce_sha256", "contract_version"];
  const transcript = {};
  for (const key of required) {
    if (typeof fields[key] !== "string" || !fields[key]) reject("TRANSCRIPT_FIELD_MISSING", `${key} is required.`);
    transcript[key] = fields[key];
  }
  return createHash("sha256").update(canonicalJson(transcript), "utf8").digest("hex");
}

export function validateBootstrap(bootstrap, nowUtc, observedBootstrapSpkiSha256) {
  base64urlBytes(bootstrap.bootstrap_secret_b64url, "Bootstrap secret");
  base64urlBytes(bootstrap.pairing_nonce_b64url, "Pairing nonce");
  if (bootstrap.manual_code !== manualPairingCode(bootstrap.bootstrap_secret_b64url)) reject("MANUAL_CODE_MISMATCH", "Manual code must encode the QR secret and checksum.");
  if (bootstrap.bootstrap_spki_sha256 !== observedBootstrapSpkiSha256) reject("BOOTSTRAP_FINGERPRINT_MISMATCH", "Connected bootstrap certificate differs from the attended record.");
  const issued = parseUtc(bootstrap.issued_utc, "Issued time");
  const expires = parseUtc(bootstrap.expires_utc, "Expiry time");
  const now = parseUtc(nowUtc, "Current time");
  if (expires <= issued || expires - issued > MAX_BOOTSTRAP_MS) reject("BOOTSTRAP_LIFETIME_INVALID", "Pairing window must be positive and no longer than ten minutes.");
  if (now < issued || now >= expires) reject("BOOTSTRAP_EXPIRED", "Pairing record is not active at the supplied time.");
  if (!bootstrap.endpoint.startsWith("https://")) reject("BOOTSTRAP_PLAINTEXT_FORBIDDEN", "Bootstrap endpoint must use HTTPS.");
  return true;
}

export function evaluateBootstrapProof(window, { pairingSessionId, nowUtc, proofValid }) {
  if (window.pairing_session_id !== pairingSessionId) reject("PAIRING_SESSION_MISMATCH", "Proof is bound to one pairing session.");
  if (window.used) reject("BOOTSTRAP_REPLAY", "Bootstrap secret is single-use.");
  if (window.locked_out || window.failed_proofs >= MAX_FAILED_PROOFS) reject("PAIRING_LOCKED_OUT", "Pairing window is locked.");
  const now = parseUtc(nowUtc, "Proof time");
  if (now >= parseUtc(window.expires_utc, "Window expiry")) reject("BOOTSTRAP_EXPIRED", "Pairing window expired.");
  const next = structuredClone(window);
  if (proofValid) {
    next.used = true;
    next.status = "BOOTSTRAP_VERIFIED";
  } else {
    next.failed_proofs += 1;
    next.locked_out = next.failed_proofs >= MAX_FAILED_PROOFS;
    next.status = next.locked_out ? "LOCKED_OUT" : "BOOTSTRAP_ADVERTISED";
    next.retry_delay_seconds = Math.min(2 ** next.failed_proofs, 60);
  }
  return next;
}

export function validateEnrollment(enrollment, transcriptFields) {
  const issued = parseUtc(enrollment.issued_utc, "Certificate issue time");
  const expires = parseUtc(enrollment.certificate_not_after_utc, "Certificate expiry");
  if (expires <= issued || expires - issued > MAX_CERTIFICATE_MS) reject("CERTIFICATE_LIFETIME_INVALID", "Leaf certificate exceeds the accepted lifetime.");
  for (const key of ["pairing_session_id", "device_id", "coordinator_id", "device_spki_sha256", "coordinator_spki_sha256", "coordinator_ca_sha256", "pairing_nonce_sha256"]) {
    if (enrollment[key] !== transcriptFields[key]) reject("ENROLLMENT_IDENTITY_MISMATCH", `${key} differs from the verified transcript.`);
  }
  if (enrollment.transcript_sha256 !== computePairingTranscriptSha256(transcriptFields)) reject("TRANSCRIPT_HASH_MISMATCH", "Enrollment transcript hash does not match.");
  validateTlsProfile({ profile: enrollment.tls_profile, compatibilityReason: enrollment.compatibility_reason, negotiatedVersion: enrollment.tls_profile === "HC-TLS-13-001" ? "TLS1.3" : "TLS1.2", cipherSuite: enrollment.tls_profile === "HC-TLS-13-001" ? "TLS_AES_128_GCM_SHA256" : "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256", earlyData: false, peerSupportsTls13: false });
  return true;
}

export function validateTrustTransition(current, next, { captureActive = false } = {}) {
  if (next.revision !== current.revision + 1) reject("TRUST_REVISION_INVALID", "Trust revision must increment exactly once.");
  for (const key of IMMUTABLE_TRUST) if (current[key] !== next[key]) reject("TRUST_IDENTITY_CHANGED", `${key} is immutable.`);
  if (!next.prior_revision_sha256) reject("TRUST_HISTORY_LINK_MISSING", "A new trust revision must link to the prior revision hash.");
  if (!TRUST_TRANSITIONS.get(current.state)?.has(next.state)) reject("TRUST_TRANSITION_FORBIDDEN", `${current.state} cannot transition to ${next.state}.`);
  if (current.state === "PAIRED" && next.state === "PAIRED" && current.device_certificate_sha256 === next.device_certificate_sha256 && current.coordinator_certificate_sha256 === next.coordinator_certificate_sha256) reject("TRUST_NOOP_FORBIDDEN", "A direct PAIRED revision must rotate at least one leaf certificate.");
  if (captureActive && ["REVOKED", "LOST", "UNPAIRED"].includes(next.state)) reject("TRUST_CHANGE_DURING_CAPTURE", "Trust removal is forbidden during capture/finalization.");
  if (["REVOKED", "LOST", "UNPAIRED"].includes(next.state) && !next.reason) reject("TRUST_REASON_REQUIRED", "Trust removal requires a reason.");
  return true;
}

export function validateTlsProfile({ profile, compatibilityReason, negotiatedVersion, cipherSuite, earlyData, peerSupportsTls13 }) {
  if (earlyData) reject("TLS_EARLY_DATA_FORBIDDEN", "TLS early data is replayable and forbidden.");
  if (profile === "HC-TLS-13-001") {
    if (negotiatedVersion !== "TLS1.3" || !/^TLS_AES_(128|256)_GCM_SHA(256|384)$/.test(cipherSuite)) reject("TLS13_PROFILE_MISMATCH", "TLS 1.3 profile was not negotiated as declared.");
    return true;
  }
  if (profile !== "HC-TLS-12-W10-001") reject("TLS_PROFILE_UNKNOWN", "TLS profile is not recognized.");
  if (peerSupportsTls13) reject("TLS_DOWNGRADE_FORBIDDEN", "TLS 1.3-capable peers cannot silently select the compatibility profile.");
  if (typeof compatibilityReason !== "string" || compatibilityReason.length < 10) reject("TLS_COMPATIBILITY_REASON_REQUIRED", "TLS 1.2 requires a recorded compatibility reason.");
  if (negotiatedVersion !== "TLS1.2" || !/^TLS_ECDHE_ECDSA_WITH_AES_(128|256)_GCM_SHA(256|384)$/.test(cipherSuite)) reject("TLS12_PROFILE_MISMATCH", "Compatibility profile requires ECDHE/ECDSA/AES-GCM.");
  return true;
}

export function authorizePeer(trust, peer, resource, nowUtc) {
  if (!["PAIRED", "ROTATION_DUE"].includes(trust.state)) reject("TRUST_NOT_ACTIVE", "Peer trust is not active.");
  const now = parseUtc(nowUtc, "Authorization time");
  if (now >= parseUtc(trust.certificate_not_after_utc, "Certificate expiry")) reject("CERTIFICATE_EXPIRED", "Peer certificate expired.");
  if (peer.coordinator_id !== trust.coordinator_id || peer.device_id !== trust.device_id || peer.coordinator_certificate_sha256 !== trust.coordinator_certificate_sha256 || peer.device_certificate_sha256 !== trust.device_certificate_sha256) reject("PEER_BINDING_MISMATCH", "Presented peer identity does not match active trust.");
  if (peer.role !== "COORDINATOR") reject("PEER_ROLE_FORBIDDEN", "Only the enrolled coordinator role may control or collect.");
  if (resource.device_id !== trust.device_id || resource.coordinator_id !== trust.coordinator_id) reject("RESOURCE_SCOPE_FORBIDDEN", "Target resource is outside the paired identity scope.");
  return true;
}

export function validateAuditEvent(event) {
  const visit = (value, path = "details") => {
    if (!value || typeof value !== "object") return;
    for (const [key, child] of Object.entries(value)) {
      if (FORBIDDEN_DETAIL_KEY.test(key)) reject("AUDIT_SENSITIVE_FIELD", `${path}.${key} is forbidden in security audit data.`);
      visit(child, `${path}.${key}`);
    }
  };
  visit(event.details);
  return true;
}

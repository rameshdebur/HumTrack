import { createHash } from "node:crypto";
import { ContractConformanceError, computeContentSha256, validateCommitReceipt } from "./control-contract-conformance.js";

const UINT64_MAX = 18446744073709551615n;
const TIMED_ROLES = new Set(["SCIENTIFIC_MASTER_VIDEO", "FRAME_TIMESTAMPS", "IMU_SAMPLES"]);
const REQUIRED_CORE_ROLES = new Set(["SCIENTIFIC_MASTER_VIDEO", "FRAME_TIMESTAMPS", "CAMERA_METADATA", "CAPTURE_EVENTS", "FINALIZATION_RECORD"]);
const ANDROID_IMU_ROLES = new Set(["IMU_SAMPLES", "IMU_METADATA"]);
const WINDOWS_DEVICE = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;
const ALL_CHECKS = new Set(["MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES", "ARTIFACT_SET", "BYTE_LENGTH", "SHA256", "MASTER_STRUCTURE", "MASTER_FULL_DECODE", "TIMING_EVENTS", "METADATA_PROFILE", "FINALIZATION"]);
const ALWAYS_REQUIRED_CHECKS = new Set(["MANIFEST_SCHEMA", "PACKAGE_IDENTITY", "PATH_SAFETY", "REGULAR_FILES", "ARTIFACT_SET", "BYTE_LENGTH", "SHA256", "FINALIZATION"]);

function reject(code, message) {
  throw new ContractConformanceError(code, message);
}

function u64(value, label) {
  if (typeof value !== "string" || !/^(0|[1-9][0-9]{0,19})$/.test(value) || BigInt(value) > UINT64_MAX) {
    reject("UINT64_INVALID", `${label} must be a canonical unsigned 64-bit decimal string.`);
  }
  return BigInt(value);
}

function unique(values, code, label) {
  if (new Set(values).size !== values.length) reject(code, `${label} must be unique.`);
}

function same(left, right, keys, code) {
  for (const key of keys) if (left[key] !== right[key]) reject(code, `${key} differs across bound records.`);
}

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

export function strongEtag(sha256) {
  return `"sha256-${sha256}"`;
}

export function contentDigest(bytes) {
  return `sha-256=:${createHash("sha256").update(bytes).digest("base64")}:`;
}

export function computeArtifactSetSha256(artifacts) {
  const inventory = artifacts
    .map(({ relative_path, byte_length, sha256 }) => `${relative_path}\t${byte_length}\t${sha256}\n`)
    .sort()
    .join("");
  return createHash("sha256").update(inventory, "utf8").digest("hex");
}

export function computePackageContentSha256(manifest) {
  return computeContentSha256(manifest, "package_content_sha256");
}

export function validatePackagePath(relativePath) {
  if (typeof relativePath !== "string" || relativePath !== relativePath.normalize("NFC") || relativePath.startsWith("/") || relativePath.includes("\\") || /[<>:"|?*\u0000-\u001f]/u.test(relativePath)) {
    reject("PACKAGE_PATH_UNSAFE", "Artifact path must be normalized, relative, slash-separated, and Windows-safe.");
  }
  const segments = relativePath.split("/");
  if (!segments.length || segments.some((part) => !part || part === "." || part === ".." || part.endsWith(".") || part.endsWith(" ") || WINDOWS_DEVICE.test(part))) {
    reject("PACKAGE_PATH_UNSAFE", "Artifact path contains an unsafe segment.");
  }
  if (relativePath.toLowerCase() === "package-manifest.json") reject("MANIFEST_RECURSION", "The manifest cannot inventory itself.");
  return true;
}

export function validatePackageManifest(manifest) {
  unique(manifest.artifacts.map((item) => item.artifact_id), "DUPLICATE_ARTIFACT_ID", "Artifact IDs");
  unique(manifest.artifacts.map((item) => item.relative_path.toLowerCase()), "DUPLICATE_ARTIFACT_PATH", "Windows-normalized artifact paths");
  if (manifest.artifact_count !== manifest.artifacts.length) reject("ARTIFACT_COUNT_MISMATCH", "Artifact count does not match inventory.");
  let total = 0n;
  for (const artifact of manifest.artifacts) {
    validatePackagePath(artifact.relative_path);
    same(manifest, artifact, ["session_id", "trial_id", "source_id", "capture_attempt_id"], "ARTIFACT_IDENTITY_MISMATCH");
    const length = u64(artifact.byte_length, "Artifact byte length");
    total += length;
    if (total > UINT64_MAX) reject("PACKAGE_LENGTH_OVERFLOW", "Package byte length exceeds unsigned 64-bit range.");
    if (TIMED_ROLES.has(artifact.role) && !artifact.timing_coverage) reject("TIMING_COVERAGE_MISSING", `${artifact.role} requires timing coverage.`);
    if (artifact.timing_coverage) {
      const first = u64(artifact.timing_coverage.first_ticks, "First ticks");
      const last = u64(artifact.timing_coverage.last_ticks, "Last ticks");
      u64(artifact.timing_coverage.record_count, "Record count");
      u64(artifact.timing_coverage.discontinuity_count, "Discontinuity count");
      if (last < first) reject("TIMING_COVERAGE_REVERSED", "Timing coverage cannot regress.");
    }
  }
  const requiredRoles = manifest.finalization_outcome === "FINALIZED_COMPLETE"
    ? (manifest.source_kind === "ANDROID" ? new Set([...REQUIRED_CORE_ROLES, ...ANDROID_IMU_ROLES]) : REQUIRED_CORE_ROLES)
    : new Set(["CAPTURE_EVENTS", "FINALIZATION_RECORD"]);
  for (const role of requiredRoles) {
    if (!manifest.artifacts.some((item) => item.role === role && item.required)) reject("REQUIRED_ARTIFACT_ROLE_MISSING", `Required ${role} artifact is absent.`);
  }
  if (total.toString() !== manifest.package_byte_length) reject("PACKAGE_LENGTH_MISMATCH", "Package byte length is not the artifact sum.");
  if (computeArtifactSetSha256(manifest.artifacts) !== manifest.artifact_set_sha256) reject("ARTIFACT_SET_HASH_MISMATCH", "Artifact-set hash does not match inventory.");
  if (computePackageContentSha256(manifest) !== manifest.package_content_sha256) reject("PACKAGE_CONTENT_HASH_MISMATCH", "Package content hash does not match canonical manifest content.");
  return true;
}

export function validateCollectionCheckpoint(manifest, checkpoint) {
  validatePackageManifest(manifest);
  same(manifest, checkpoint, ["package_id", "package_content_sha256", "source_id"], "CHECKPOINT_PACKAGE_MISMATCH");
  if (checkpoint.artifacts.length !== manifest.artifacts.length) reject("CHECKPOINT_ARTIFACT_SET_MISMATCH", "Checkpoint must cover every manifest artifact.");
  unique(checkpoint.artifacts.map((item) => item.artifact_id), "DUPLICATE_CHECKPOINT_ARTIFACT", "Checkpoint artifacts");
  for (const progress of checkpoint.artifacts) {
    const artifact = manifest.artifacts.find((item) => item.artifact_id === progress.artifact_id);
    if (!artifact) reject("CHECKPOINT_ARTIFACT_SET_MISMATCH", "Checkpoint references an unknown artifact.");
    same(artifact, progress, ["relative_path"], "CHECKPOINT_ARTIFACT_MISMATCH");
    if (artifact.byte_length !== progress.expected_byte_length || artifact.sha256 !== progress.expected_sha256) reject("CHECKPOINT_ARTIFACT_MISMATCH", "Checkpoint expectation differs from manifest.");
    const expected = u64(progress.expected_byte_length, "Expected byte length");
    let covered = 0n;
    let priorEnd = 0n;
    for (const [index, range] of progress.received_ranges.entries()) {
      const start = u64(range.start, "Range start");
      const end = u64(range.end_exclusive, "Range end");
      if (end <= start || end > expected || (index > 0 && start < priorEnd)) reject("CHECKPOINT_RANGE_INVALID", "Ranges must be sorted, non-overlapping, non-empty, and bounded.");
      covered += end - start;
      priorEnd = end;
    }
    if (covered.toString() !== progress.staged_byte_length) reject("CHECKPOINT_BYTE_COUNT_MISMATCH", "Staged bytes must equal received-range coverage.");
    if (checkpoint.collection_method === "HTTPS_RANGE") {
      if (progress.strong_etag !== strongEtag(artifact.sha256)) reject("CHECKPOINT_ETAG_MISMATCH", "HTTPS checkpoint requires the manifest-derived strong ETag.");
    } else {
      if (progress.strong_etag !== undefined) reject("OFFLINE_ETAG_FORBIDDEN", "USB/MTP and local collection do not persist an HTTP validator.");
      const full = progress.received_ranges.length === 1 && progress.received_ranges[0].start === "0" && progress.received_ranges[0].end_exclusive === artifact.byte_length;
      if (progress.received_ranges.length && !full) reject("OFFLINE_PARTIAL_RESUME_FORBIDDEN", "Offline collection resumes only at verified artifact boundaries.");
    }
    const fullCoverage = progress.received_ranges.length === 1 && progress.received_ranges[0].start === "0" && progress.received_ranges[0].end_exclusive === artifact.byte_length;
    if (["STAGED", "VERIFIED"].includes(progress.state) && !fullCoverage) reject("ARTIFACT_FALSE_COMPLETE", "Staged or verified artifact requires full coverage.");
    if (progress.state === "VERIFIED" && !progress.artifact_verification_id) reject("ARTIFACT_VERIFICATION_MISSING", "Verified artifact requires a verification record identity.");
  }
  return true;
}

export function validateCheckpointTransition(current, next) {
  same(current, next, ["checkpoint_id", "package_id", "package_content_sha256", "source_id", "collection_method"], "CHECKPOINT_IDENTITY_CHANGED");
  if (next.revision !== current.revision + 1) reject("CHECKPOINT_REVISION_INVALID", "Checkpoint revision must increment exactly once.");
  if (next.artifacts.length !== current.artifacts.length) reject("CHECKPOINT_ARTIFACT_SET_CHANGED", "Checkpoint artifact set cannot change.");
  for (const prior of current.artifacts) {
    const after = next.artifacts.find((item) => item.artifact_id === prior.artifact_id);
    if (!after) reject("CHECKPOINT_ARTIFACT_SET_CHANGED", "Checkpoint artifact disappeared.");
    same(prior, after, ["relative_path", "expected_byte_length", "expected_sha256", "strong_etag"], "CHECKPOINT_ARTIFACT_CHANGED");
    if (after.attempt_count < prior.attempt_count || after.restart_count < prior.restart_count) reject("CHECKPOINT_COUNTER_REGRESSION", "Attempt and restart counters cannot regress.");
    if (prior.state === "VERIFIED" && after.state !== "VERIFIED") reject("VERIFIED_ARTIFACT_REGRESSION", "A verified artifact cannot silently regress.");
    const coverageRemoved = prior.received_ranges.some((range) => !after.received_ranges.some((candidate) => BigInt(candidate.start) <= BigInt(range.start) && BigInt(candidate.end_exclusive) >= BigInt(range.end_exclusive)));
    if (coverageRemoved) {
      if (after.restart_count !== prior.restart_count + 1 || after.received_ranges.length !== 0 || after.staged_byte_length !== "0" || !after.last_restart_reason || !["PENDING", "RETRY_REQUIRED"].includes(after.state)) {
        reject("CHECKPOINT_UNAUDITED_RESTART", "Removed coverage requires a complete, attributed artifact restart.");
      }
    } else if (after.restart_count !== prior.restart_count) {
      reject("CHECKPOINT_SPURIOUS_RESTART", "Restart counter cannot change without discarding prior coverage.");
    }
  }
  return true;
}

export function reconcileRangeResponse({ priorBytes, expectedEtag, status, responseEtag, contentRange }) {
  const prior = u64(priorBytes, "Prior bytes");
  if (status === 206) {
    if (responseEtag !== expectedEtag) reject("REPRESENTATION_VALIDATOR_CHANGED", "A partial response cannot be combined across strong validators.");
    const match = /^bytes ([0-9]+)-([0-9]+)\/([0-9]+)$/.exec(contentRange ?? "");
    if (!match || BigInt(match[1]) !== prior || BigInt(match[2]) < BigInt(match[1])) reject("CONTENT_RANGE_MISMATCH", "Partial response must start at the requested resume offset.");
    return "APPEND_PARTIAL";
  }
  if (status === 200) return prior === 0n ? "WRITE_FULL" : "DISCARD_PARTIAL_AND_WRITE_FULL";
  if (status === 416) return "RECONCILE_MANIFEST_AND_RESTART_ARTIFACT";
  reject("RANGE_RESPONSE_UNEXPECTED", "Resume accepts only 200, 206, or 416.");
}

export function offlineArtifactRecoveryAction(progress) {
  return progress.state === "VERIFIED" && progress.artifact_verification_id
    ? "SKIP_VERIFIED_ARTIFACT"
    : "RESTART_ARTIFACT_FROM_ZERO";
}

export function validatePackageVerification(manifest, verification) {
  validatePackageManifest(manifest);
  same(manifest, verification, ["package_id", "package_content_sha256", "artifact_set_sha256"], "VERIFICATION_PACKAGE_MISMATCH");
  unique(verification.checks.map((item) => item.check), "DUPLICATE_VERIFICATION_CHECK", "Verification checks");
  unique(verification.artifact_results.map((item) => item.artifact_id), "DUPLICATE_VERIFICATION_ARTIFACT", "Verification artifacts");
  for (const check of ALL_CHECKS) if (!verification.checks.some((item) => item.check === check)) reject("VERIFICATION_CHECK_MISSING", `${check} must be explicitly reported.`);
  const requiredChecks = new Set(ALWAYS_REQUIRED_CHECKS);
  if (manifest.artifacts.some((item) => item.role === "SCIENTIFIC_MASTER_VIDEO")) { requiredChecks.add("MASTER_STRUCTURE"); requiredChecks.add("MASTER_FULL_DECODE"); }
  if (manifest.artifacts.some((item) => TIMED_ROLES.has(item.role) || item.role === "CAPTURE_EVENTS")) requiredChecks.add("TIMING_EVENTS");
  if (manifest.artifacts.some((item) => item.role === "CAMERA_METADATA")) requiredChecks.add("METADATA_PROFILE");
  for (const requiredCheck of requiredChecks) if (!verification.checks.some((item) => item.check === requiredCheck && item.required)) reject("REQUIRED_VERIFICATION_CHECK_MISSING", `${requiredCheck} must be required for the present artifacts.`);
  if (verification.artifact_results.length !== manifest.artifacts.length) reject("VERIFICATION_ARTIFACT_SET_MISMATCH", "Verification must cover every manifest artifact.");
  for (const result of verification.artifact_results) {
    const artifact = manifest.artifacts.find((item) => item.artifact_id === result.artifact_id);
    if (!artifact) reject("VERIFICATION_ARTIFACT_SET_MISMATCH", "Verification references an unknown artifact.");
    if (result.relative_path !== artifact.relative_path || result.required !== artifact.required || result.expected_byte_length !== artifact.byte_length || result.expected_sha256 !== artifact.sha256) reject("VERIFICATION_ARTIFACT_MISMATCH", "Verification expectation differs from manifest.");
    const matches = result.observed_byte_length === artifact.byte_length && result.observed_sha256 === artifact.sha256;
    if ((result.disposition === "PASS") !== matches) reject("VERIFICATION_ARTIFACT_FALSE_RESULT", "Artifact disposition must match observed length and hash.");
  }
  const failed = verification.checks.some((item) => item.required && item.disposition !== "PASS") || verification.artifact_results.some((item) => item.required && item.disposition !== "PASS");
  if ((verification.outcome === "VERIFIED") === failed) reject("VERIFICATION_FALSE_AGGREGATE", "Aggregate verification outcome contradicts required evidence.");
  return true;
}

export function classifyPackageIdentity(existing, incoming) {
  if (!existing) return "NEW";
  if (existing.package_id !== incoming.package_id) return "NEW";
  return existing.package_content_sha256 === incoming.package_content_sha256 ? "IDEMPOTENT" : "QUARANTINE_IDENTITY_CONFLICT";
}

export function validateVerifiedCommit(manifest, verification, custody, receipt) {
  validatePackageVerification(manifest, verification);
  if (verification.outcome !== "VERIFIED") reject("COMMIT_WITHOUT_VERIFICATION", "Only a fully verified package may be committed.");
  same(verification, custody, ["verification_record_id", "package_id", "package_content_sha256"], "CUSTODY_VERIFICATION_MISMATCH");
  if (receipt.artifact_set_sha256 !== manifest.artifact_set_sha256) reject("RECEIPT_ARTIFACT_SET_MISMATCH", "Receipt artifact-set identity differs from the manifest.");
  validateCommitReceipt(custody, receipt);
  return true;
}

export function canonicalJson(value) {
  return canonical(value);
}

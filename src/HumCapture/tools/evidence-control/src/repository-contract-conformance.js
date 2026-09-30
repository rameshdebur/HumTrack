import { ContractConformanceError } from "./control-contract-conformance.js";

const UINT64_MAX = 18446744073709551615n;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const SHA256 = /^[0-9a-f]{64}$/;
const WINDOWS_DEVICE = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;
const AUTHORITIES = ["JOURNAL", "STAGING_PACKAGE", "DESTINATION_PACKAGE", "CATALOG_LINKAGE", "VERIFICATION_RECORD", "COMMIT_RECORD"];
const BAD_DISPOSITIONS = new Set(["MISMATCH", "UNREADABLE", "UNSAFE"]);
const TERMINAL_STATES = new Set(["COMMITTED", "QUARANTINED"]);
const AUTOMATIC_ACTIONS = new Set(["NO_ACTION", "RETRY_FROM_STAGED", "RESUME_AFTER_MOVE", "COMPLETE_CATALOGING", "FINALIZE_COMMIT", "CONFIRM_IDEMPOTENT_COMMIT"]);
const OPERATOR_ACTIONS = new Set(["RETRY_RECONCILIATION", "QUARANTINE_CONFLICT", "RETAIN_FOR_INVESTIGATION", "EXPORT_DIAGNOSTICS"]);
const IMMUTABLE_TRANSACTION_FIELDS = [
  "schema_version", "transaction_id", "repository_id", "subject_id", "session_id",
  "trial_id", "source_id", "capture_attempt_id", "collection_attempt_id", "package_id",
  "package_content_sha256", "artifact_set_sha256", "verification_record_id",
  "verification_record_content_sha256", "staging_relative_path",
  "destination_relative_path", "package_byte_length", "artifact_count", "created_utc"
];
const NORMAL_EDGES = new Set([
  "STAGED_VERIFIED>COMMITTING",
  "COMMITTING>MOVED",
  "MOVED>CATALOGED",
  "CATALOGED>COMMITTED"
]);

function reject(code, message) {
  throw new ContractConformanceError(code, message);
}

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

function assertUuid(value, label) {
  if (!UUID.test(value)) reject("REPOSITORY_UUID_INVALID", `${label} must be a canonical lowercase UUID.`);
}

function assertSha256(value, label) {
  if (!SHA256.test(value)) reject("REPOSITORY_HASH_INVALID", `${label} must be lowercase SHA-256 hex.`);
}

function assertU64(value, label) {
  if (typeof value !== "string" || !/^(0|[1-9][0-9]{0,19})$/.test(value) || BigInt(value) > UINT64_MAX) {
    reject("REPOSITORY_U64_INVALID", `${label} must be a canonical unsigned 64-bit decimal string.`);
  }
}

export function validateRepositoryRelativePath(value) {
  if (typeof value !== "string" || value.length === 0 || value.length > 512 || value !== value.normalize("NFC") || value.startsWith("/") || /^[A-Za-z]:/.test(value) || value.includes("\\") || /[<>:"|?*\u0000-\u001f]/u.test(value)) {
    reject("REPOSITORY_PATH_UNSAFE", "Repository path must be normalized, relative, slash-separated, and Windows-safe.");
  }
  const segments = value.split("/");
  if (segments.some((part) => !part || part === "." || part === ".." || part.endsWith(".") || part.endsWith(" ") || WINDOWS_DEVICE.test(part))) {
    reject("REPOSITORY_PATH_UNSAFE", "Repository path contains an unsafe segment.");
  }
  return true;
}

export function stagingPath(transaction) {
  return `staging/${transaction.collection_attempt_id}/${transaction.package_id}`;
}

export function destinationPath(transaction) {
  return `subjects/${transaction.subject_id}/sessions/${transaction.session_id}/packages/${transaction.package_id}`;
}

export function recordPath(record) {
  return `subjects/${record.subject_id}/sessions/${record.session_id}/records/${record.record_kind}/${record.record_id}.json`;
}

export function validateDescriptorCompatibility(descriptor, supportedRequiredFeatures) {
  const [major] = descriptor.interface_version.split(".").map(Number);
  if (major !== 1) return "READ_ONLY_UNSUPPORTED_MAJOR";
  const unknown = descriptor.required_features.filter((feature) => !supportedRequiredFeatures.has(feature));
  return unknown.length ? "READ_ONLY_UNKNOWN_REQUIRED_FEATURE" : "MUTATION_ALLOWED";
}

export function validateTransactionBinding(transaction) {
  for (const field of ["transaction_id", "repository_id", "subject_id", "session_id", "trial_id", "source_id", "capture_attempt_id", "collection_attempt_id", "package_id", "verification_record_id"]) assertUuid(transaction[field], field);
  for (const field of ["package_content_sha256", "artifact_set_sha256", "verification_record_content_sha256"]) assertSha256(transaction[field], field);
  assertU64(transaction.package_byte_length, "package_byte_length");
  validateRepositoryRelativePath(transaction.staging_relative_path);
  validateRepositoryRelativePath(transaction.destination_relative_path);
  if (transaction.staging_relative_path !== stagingPath(transaction)) reject("STAGING_PATH_BINDING_INVALID", "Staging path does not match collection/package identity.");
  if (transaction.destination_relative_path !== destinationPath(transaction)) reject("DESTINATION_PATH_BINDING_INVALID", "Destination path does not match subject/session/package identity.");
  return true;
}

export function validateCurrentTransactionUpdate(current, next, transition) {
  for (const field of IMMUTABLE_TRANSACTION_FIELDS) {
    if (current[field] !== next[field]) reject("TRANSACTION_IDENTITY_CHANGED", `${field} is immutable for a repository transaction.`);
  }
  if (next.revision !== current.revision + 1) reject("TRANSACTION_REVISION_INVALID", "Current transaction revision must increment exactly once.");
  if (transition.from_state !== current.state || transition.to_state !== next.state) reject("TRANSACTION_TRANSITION_MISMATCH", "Current row update must match its transition.");
  return true;
}

export function validateTransition(history, transition) {
  const prior = history.at(-1);
  if (!prior) {
    if (transition.transition_sequence !== 1 || transition.from_state !== null || transition.to_state !== "STAGED_VERIFIED") reject("INITIAL_TRANSITION_INVALID", "The first transition must establish STAGED_VERIFIED with no from-state.");
    return true;
  }
  if (transition.transaction_id !== prior.transaction_id) reject("TRANSITION_TRANSACTION_CHANGED", "Transition history cannot change transaction identity.");
  if (transition.transition_sequence !== prior.transition_sequence + 1) reject("TRANSITION_SEQUENCE_INVALID", "Transition sequence must be contiguous and positive.");
  if (transition.from_state !== prior.to_state) reject("TRANSITION_FROM_STATE_INVALID", "Transition from-state must equal the prior durable state.");
  if (TERMINAL_STATES.has(prior.to_state)) reject("TERMINAL_STATE_TRANSITION", "Committed and quarantined transactions are terminal.");
  const edge = `${transition.from_state}>${transition.to_state}`;
  const recoveryEntry = transition.to_state === "RECOVERY_REQUIRED" && transition.from_state !== "RECOVERY_REQUIRED";
  const recoveryExit = transition.from_state === "RECOVERY_REQUIRED" && ["STAGED_VERIFIED", "MOVED", "CATALOGED", "COMMITTED", "QUARANTINED"].includes(transition.to_state) && transition.reconciliation_id;
  if (!NORMAL_EDGES.has(edge) && !recoveryEntry && !recoveryExit) reject("TRANSITION_EDGE_FORBIDDEN", "Transition skips or contradicts an accepted repository durability boundary.");
  return true;
}

function operationRequest(transition) {
  const { transition_id: ignoredId, recorded_utc: ignoredUtc, ...request } = transition;
  return request;
}

export function classifyOperationReplay(existing, incoming) {
  if (!existing) return "NEW";
  if (existing.transaction_id !== incoming.transaction_id || existing.operation_id !== incoming.operation_id) return "NEW";
  return canonical(operationRequest(existing)) === canonical(operationRequest(incoming)) ? "IDEMPOTENT" : "RECOVERY_REQUIRED_CONFLICT";
}

function observationsByAuthority(reconciliation) {
  const map = new Map();
  for (const observation of reconciliation.observations) {
    if (map.has(observation.authority)) reject("DUPLICATE_RECONCILIATION_AUTHORITY", "Each reconciliation authority must appear once.");
    map.set(observation.authority, observation);
  }
  if (map.size !== AUTHORITIES.length || AUTHORITIES.some((authority) => !map.has(authority))) reject("RECONCILIATION_OBSERVATION_INCOMPLETE", "All six repository authorities must be observed exactly once.");
  return map;
}

function requireDisposition(map, authority, expected) {
  if (map.get(authority).disposition !== expected) reject("AUTOMATIC_ACTION_PREDICATE_FAILED", `${authority} must be ${expected}.`);
}

export function validateReconciliation(transaction, reconciliation) {
  if (reconciliation.transaction_id !== transaction.transaction_id || reconciliation.prior_state !== transaction.state) reject("RECONCILIATION_TRANSACTION_MISMATCH", "Reconciliation must bind the current transaction and state.");
  const map = observationsByAuthority(reconciliation);
  for (const observation of map.values()) {
    if (observation.expected_package_id !== null && observation.expected_package_id !== transaction.package_id) reject("OBSERVATION_EXPECTATION_MISMATCH", "Expected package identity must bind the transaction.");
    if (observation.expected_content_sha256 !== null && observation.expected_content_sha256 !== transaction.package_content_sha256) reject("OBSERVATION_EXPECTATION_MISMATCH", "Expected content hash must bind the transaction.");
    if (observation.expected_byte_length !== null && observation.expected_byte_length !== transaction.package_byte_length) reject("OBSERVATION_EXPECTATION_MISMATCH", "Expected length must bind the transaction.");
    for (const path of observation.evidence_relative_paths) validateRepositoryRelativePath(path);
  }
  if (reconciliation.automatic) {
    if (!AUTOMATIC_ACTIONS.has(reconciliation.action_code) || reconciliation.actor_kind !== "SYSTEM" || reconciliation.blocking_reason_codes.length) reject("AUTOMATIC_ACTION_INVALID", "Automatic reconciliation must be system-owned, non-blocked, and enumerated.");
    if ([...map.values()].some((observation) => BAD_DISPOSITIONS.has(observation.disposition))) reject("AUTOMATIC_ACTION_CONFLICT", "Automatic action is prohibited for mismatched, unreadable, or unsafe evidence.");
    if (map.get("STAGING_PACKAGE").disposition === "MATCH" && map.get("DESTINATION_PACKAGE").disposition === "MATCH") reject("AUTOMATIC_DUAL_PATH_CONFLICT", "Material at both package paths requires operator disposition.");
    validateAutomaticPredicates(reconciliation, map);
  } else {
    if (!OPERATOR_ACTIONS.has(reconciliation.action_code) || reconciliation.actor_kind !== "OPERATOR" || reconciliation.blocking_reason_codes.length === 0) reject("OPERATOR_ACTION_INVALID", "Operator reconciliation requires an enumerated action and blocking reason.");
    if (reconciliation.result_state === "COMMITTED") reject("OPERATOR_FORCE_COMMIT_FORBIDDEN", "Operator action cannot force a transaction committed.");
    const expectedState = reconciliation.action_code === "QUARANTINE_CONFLICT" ? "QUARANTINED" : "RECOVERY_REQUIRED";
    if (reconciliation.result_state !== expectedState) reject("OPERATOR_RESULT_STATE_INVALID", "Operator action has an invalid result state.");
  }
  const changed = reconciliation.prior_state !== reconciliation.result_state;
  if (changed !== (reconciliation.result_transition_id !== null)) reject("RECONCILIATION_TRANSITION_LINK_INVALID", "A state-changing reconciliation must link exactly one transition.");
  return true;
}

function validateAutomaticPredicates(reconciliation, map) {
  const action = reconciliation.action_code;
  if (action === "NO_ACTION") {
    if (reconciliation.result_state !== reconciliation.prior_state) reject("NO_ACTION_STATE_CHANGED", "NO_ACTION cannot change state.");
    requireDisposition(map, "JOURNAL", "MATCH");
    requireDisposition(map, "VERIFICATION_RECORD", "MATCH");
    const stateEvidence = {
      STAGED_VERIFIED: ["MATCH", "ABSENT", "ABSENT", "ABSENT"],
      COMMITTING: ["MATCH", "ABSENT", "ABSENT", "ABSENT"],
      MOVED: ["ABSENT", "MATCH", "ABSENT", "ABSENT"],
      CATALOGED: ["ABSENT", "MATCH", "MATCH", "ABSENT"],
      COMMITTED: ["ABSENT", "MATCH", "MATCH", "MATCH"]
    }[reconciliation.prior_state];
    if (!stateEvidence) reject("AUTOMATIC_ACTION_PREDICATE_FAILED", "NO_ACTION requires a directly proven normal state.");
    for (const [authority, disposition] of [["STAGING_PACKAGE", stateEvidence[0]], ["DESTINATION_PACKAGE", stateEvidence[1]], ["CATALOG_LINKAGE", stateEvidence[2]], ["COMMIT_RECORD", stateEvidence[3]]]) requireDisposition(map, authority, disposition);
    return;
  }
  requireDisposition(map, "JOURNAL", "MATCH");
  requireDisposition(map, "VERIFICATION_RECORD", "MATCH");
  if (action === "RETRY_FROM_STAGED") {
    requireDisposition(map, "STAGING_PACKAGE", "MATCH"); requireDisposition(map, "DESTINATION_PACKAGE", "ABSENT");
    requireDisposition(map, "CATALOG_LINKAGE", "ABSENT"); requireDisposition(map, "COMMIT_RECORD", "ABSENT");
    if (!["COMMITTING", "RECOVERY_REQUIRED"].includes(reconciliation.prior_state) || reconciliation.result_state !== "STAGED_VERIFIED") reject("AUTOMATIC_RESULT_STATE_INVALID", "Retry must return an interrupted or recovery state to STAGED_VERIFIED.");
  } else if (action === "RESUME_AFTER_MOVE") {
    requireDisposition(map, "STAGING_PACKAGE", "ABSENT"); requireDisposition(map, "DESTINATION_PACKAGE", "MATCH");
    requireDisposition(map, "CATALOG_LINKAGE", "ABSENT"); requireDisposition(map, "COMMIT_RECORD", "ABSENT");
    if (!["COMMITTING", "RECOVERY_REQUIRED"].includes(reconciliation.prior_state) || reconciliation.result_state !== "MOVED") reject("AUTOMATIC_RESULT_STATE_INVALID", "Move recovery must prove MOVED from an interrupted or recovery state.");
  } else if (action === "COMPLETE_CATALOGING") {
    requireDisposition(map, "STAGING_PACKAGE", "ABSENT"); requireDisposition(map, "DESTINATION_PACKAGE", "MATCH");
    requireDisposition(map, "CATALOG_LINKAGE", "ABSENT"); requireDisposition(map, "COMMIT_RECORD", "ABSENT");
    if (reconciliation.prior_state !== "MOVED" || reconciliation.result_state !== "CATALOGED") reject("AUTOMATIC_RESULT_STATE_INVALID", "Catalog completion must advance MOVED to CATALOGED.");
  } else if (action === "FINALIZE_COMMIT") {
    requireDisposition(map, "STAGING_PACKAGE", "ABSENT"); requireDisposition(map, "DESTINATION_PACKAGE", "MATCH");
    requireDisposition(map, "CATALOG_LINKAGE", "MATCH"); requireDisposition(map, "COMMIT_RECORD", "ABSENT");
    if (reconciliation.prior_state !== "CATALOGED" || reconciliation.result_state !== "COMMITTED") reject("AUTOMATIC_RESULT_STATE_INVALID", "Commit finalization must advance CATALOGED to COMMITTED.");
  } else if (action === "CONFIRM_IDEMPOTENT_COMMIT") {
    requireDisposition(map, "STAGING_PACKAGE", "ABSENT"); requireDisposition(map, "DESTINATION_PACKAGE", "MATCH");
    requireDisposition(map, "CATALOG_LINKAGE", "MATCH"); requireDisposition(map, "COMMIT_RECORD", "MATCH");
    if (reconciliation.prior_state !== "COMMITTED" || reconciliation.result_state !== "COMMITTED") reject("AUTOMATIC_RESULT_STATE_INVALID", "Idempotent confirmation retains COMMITTED.");
  }
}

export function validateRecordIndexPath(record) {
  validateRepositoryRelativePath(record.record_relative_path);
  if (record.record_relative_path !== recordPath(record)) reject("RECORD_INDEX_PATH_BINDING_INVALID", "Record path does not match kind and identities.");
  return true;
}

export function validateCommittedEvidence(transaction, catalog, commitRecord, indexEntry) {
  const shared = [
    "repository_id", "transaction_id", "subject_id", "session_id", "package_id",
    "package_content_sha256", "artifact_set_sha256", "package_byte_length",
    "artifact_count", "verification_record_id", "verification_record_content_sha256"
  ];
  for (const field of shared) {
    if (catalog[field] !== transaction[field] || commitRecord[field] !== transaction[field]) reject("COMMITTED_EVIDENCE_MISMATCH", `${field} must agree across transaction, catalog, and commit record.`);
  }
  if (catalog.package_relative_path !== transaction.destination_relative_path || commitRecord.package_relative_path !== transaction.destination_relative_path) reject("COMMITTED_EVIDENCE_MISMATCH", "Committed package paths must equal the transaction destination.");
  if (commitRecord.catalog_entry_id !== catalog.catalog_entry_id || commitRecord.catalog_revision !== catalog.catalog_revision) reject("COMMITTED_EVIDENCE_MISMATCH", "Commit record must bind the exact catalog entry and revision.");
  if (transaction.commit_record_id !== commitRecord.commit_record_id || indexEntry.record_kind !== "commits" || indexEntry.record_id !== commitRecord.commit_record_id || indexEntry.record_content_sha256 !== transaction.commit_record_content_sha256) reject("COMMITTED_EVIDENCE_MISMATCH", "Transaction and index must bind the exact immutable commit record.");
  if (indexEntry.repository_id !== transaction.repository_id || indexEntry.subject_id !== transaction.subject_id || indexEntry.session_id !== transaction.session_id || indexEntry.package_id !== transaction.package_id) reject("COMMITTED_EVIDENCE_MISMATCH", "Commit index identities must bind the transaction.");
  validateRecordIndexPath(indexEntry);
  return true;
}

export function isReceiptEligible(transaction, reconciliation) {
  if (transaction.state !== "COMMITTED" || transaction.last_reconciliation_id !== reconciliation.reconciliation_id || reconciliation.result_state !== "COMMITTED" || reconciliation.blocking_reason_codes.length || !reconciliation.automatic || reconciliation.actor_kind !== "SYSTEM" || !["NO_ACTION", "CONFIRM_IDEMPOTENT_COMMIT"].includes(reconciliation.action_code)) return false;
  const map = observationsByAuthority(reconciliation);
  const expected = {
    JOURNAL: "MATCH",
    STAGING_PACKAGE: "ABSENT",
    DESTINATION_PACKAGE: "MATCH",
    CATALOG_LINKAGE: "MATCH",
    VERIFICATION_RECORD: "MATCH",
    COMMIT_RECORD: "MATCH"
  };
  return AUTHORITIES.every((authority) => map.get(authority).disposition === expected[authority]);
}

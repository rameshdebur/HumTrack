import { createHash } from "node:crypto";

export class ContractConformanceError extends Error {
  constructor(code, message) {
    super(`${code}: ${message}`);
    this.name = "ContractConformanceError";
    this.code = code;
  }
}

function reject(code, message) {
  throw new ContractConformanceError(code, message);
}

function unique(values, code, label) {
  if (new Set(values).size !== values.length) reject(code, `${label} must be unique.`);
}

const UINT64_MAX = 18446744073709551615n;

function checkedUnsigned64(value, code, label) {
  if (typeof value !== "string" || !/^(0|[1-9][0-9]{0,19})$/.test(value)) {
    reject(code, `${label} must be a canonical unsigned decimal string.`);
  }
  if (BigInt(value) > UINT64_MAX) reject(code, `${label} exceeds unsigned 64-bit range.`);
  return BigInt(value);
}

function validateMonotonicInstant(instant, expectedClockId, code = "MONOTONIC_TIME_INVALID") {
  const ticks = checkedUnsigned64(instant.ticks, code, "Monotonic ticks");
  if (expectedClockId && instant.clock_id !== expectedClockId) {
    reject("CLOCK_IDENTITY_MISMATCH", "Monotonic instant does not use the expected clock identity.");
  }
  if (!Number.isSafeInteger(instant.ticks_per_second) || instant.ticks_per_second <= 0) {
    reject(code, "Tick frequency must be a positive safe integer.");
  }
  return ticks;
}

function sameIdentity(left, right, keys, code) {
  for (const key of keys) if (left[key] !== right[key]) reject(code, `${key} changed across bound records.`);
}

function canonical(value) {
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonical(value[key])}`).join(",")}}`;
  }
  return JSON.stringify(value);
}

export function computeContentSha256(record, contentHashField) {
  if (!record || typeof record !== "object" || Array.isArray(record)) {
    reject("CONTENT_HASH_INPUT_INVALID", "Content hashing requires a JSON object.");
  }
  if (typeof contentHashField !== "string" || !contentHashField.endsWith("_content_sha256")) {
    reject("CONTENT_HASH_FIELD_INVALID", "Content hash field must name a *_content_sha256 property.");
  }
  const content = structuredClone(record);
  delete content[contentHashField];
  return createHash("sha256").update(canonical(content), "utf8").digest("hex");
}

export function validateContentSha256(record, contentHashField) {
  const expected = computeContentSha256(record, contentHashField);
  if (record[contentHashField] !== expected) {
    reject("CONTENT_HASH_MISMATCH", `${contentHashField} does not match the RFC 8785 canonical JSON content.`);
  }
  return true;
}

export function validateCommandReplay(original, replayed) {
  if (original.command_id !== replayed.command_id) return true;
  const semantic = (command) => {
    const copy = structuredClone(command);
    delete copy.message_id;
    delete copy.issued_utc;
    return copy;
  };
  if (canonical(semantic(original)) !== canonical(semantic(replayed))) {
    reject("COMMAND_ID_CONFLICT", "The same command ID cannot carry different semantic content.");
  }
  return true;
}

export function validateStartPlan(plan, sourceStates = []) {
  const startTicks = validateMonotonicInstant({
    clock_id: plan.session_start.session_clock_id,
    ticks: plan.session_start.ticks,
    ticks_per_second: plan.session_start.ticks_per_second
  });
  const deadlineTicks = validateMonotonicInstant({
    clock_id: plan.decision_deadline.session_clock_id,
    ticks: plan.decision_deadline.ticks,
    ticks_per_second: plan.decision_deadline.ticks_per_second
  });
  if (plan.session_start.session_clock_id !== plan.decision_deadline.session_clock_id
    || plan.session_start.ticks_per_second !== plan.decision_deadline.ticks_per_second
    || plan.session_start.clock_model_id !== plan.decision_deadline.clock_model_id) {
    reject("START_PLAN_SESSION_CLOCK_MISMATCH", "Start and decision deadline must use the same session clock.");
  }
  if (deadlineTicks >= startTicks) reject("START_PLAN_DEADLINE_INVALID", "Decision deadline must precede scheduled start.");
  const maximum = checkedUnsigned64(plan.maximum_duration_ns, "DURATION_INVALID", "Maximum duration");
  if (maximum === 0n) reject("DURATION_INVALID", "Maximum duration must be greater than zero.");
  if (plan.planned_duration_ns !== undefined
    && checkedUnsigned64(plan.planned_duration_ns, "DURATION_INVALID", "Planned duration") > maximum) {
    reject("PLANNED_DURATION_EXCEEDS_MAXIMUM", "Planned duration cannot exceed maximum duration.");
  }
  unique(plan.required_source_attempts.map((item) => item.source_id), "DUPLICATE_START_SOURCE", "Start-plan source IDs");
  unique(plan.required_source_attempts.map((item) => item.capture_attempt_id), "DUPLICATE_START_ATTEMPT", "Start-plan attempt IDs");
  for (const item of plan.required_source_attempts) {
    validateMonotonicInstant(item.source_start);
    checkedUnsigned64(item.mapped_uncertainty_ns, "UNCERTAINTY_INVALID", "Mapped uncertainty");
  }
  if (sourceStates.length) {
    unique(sourceStates.map((item) => item.source_id), "DUPLICATE_START_SOURCE_STATE", "Source states supplied to start plan");
    if (sourceStates.length !== plan.required_source_attempts.length) reject("START_PLAN_SOURCE_SET_MISMATCH", "Every and only required source must be supplied.");
    for (const item of plan.required_source_attempts) {
      const state = sourceStates.find((candidate) => candidate.source_id === item.source_id);
      if (!state || state.capture_attempt_id !== item.capture_attempt_id) reject("START_PLAN_SOURCE_SET_MISMATCH", "Start plan attempt does not match authoritative source state.");
      if (state.state !== "ARMED") reject("START_PLAN_SOURCE_NOT_ARMED", "Every required source must be ARMED before commit.");
      if (state.configuration_id !== item.configuration_id || state.configuration_content_sha256 !== item.configuration_content_sha256) {
        reject("START_PLAN_CONFIGURATION_MISMATCH", "Start plan must bind each source's actual configuration.");
      }
      if (state.source_clock.clock_id !== item.source_start.clock_id
        || state.source_clock.ticks_per_second !== item.source_start.ticks_per_second) {
        reject("START_PLAN_SOURCE_CLOCK_MISMATCH", "Scheduled source time must use the source's active clock.");
      }
    }
  }
  return true;
}

export function validateSourceConfiguration(configuration, state) {
  sameIdentity(configuration, state, ["session_id", "trial_id", "source_id", "capture_attempt_id", "source_kind"], "CONFIGURATION_SOURCE_IDENTITY_MISMATCH");
  if (configuration.configuration_id !== state.configuration_id
    || configuration.configuration_content_sha256 !== state.configuration_content_sha256) {
    reject("CONFIGURATION_BINDING_MISMATCH", "Source state must bind the supplied immutable configuration.");
  }
  checkedUnsigned64(configuration.maximum_duration_ns, "DURATION_INVALID", "Configuration maximum duration");
  return true;
}

export function validateProtocolSnapshot(snapshot) {
  const policy = snapshot.source_count_policy;
  if (policy.mode === "FIXED" && snapshot.selected_source_count !== policy.count) {
    reject("SOURCE_COUNT_FIXED_MISMATCH", "Selected source count must equal the fixed protocol count.");
  }
  if (policy.mode === "FLEXIBLE") {
    if (policy.minimum > policy.maximum) {
      reject("SOURCE_COUNT_RANGE_INVALID", "Flexible minimum cannot exceed maximum.");
    }
    if (snapshot.selected_source_count < policy.minimum || snapshot.selected_source_count > policy.maximum) {
      reject("SOURCE_COUNT_OUT_OF_RANGE", "Selected source count is outside the approved flexible range.");
    }
  }
  if (snapshot.source_roles.length !== snapshot.selected_source_count) {
    reject("SOURCE_ROLE_COUNT_MISMATCH", "Instantiated source roles must equal selected source count.");
  }
  unique(snapshot.source_roles.map((item) => item.role_id), "DUPLICATE_SOURCE_ROLE", "Source role IDs");
  unique(snapshot.trial_slots.map((item) => item.slot_id), "DUPLICATE_TRIAL_SLOT", "Trial slot IDs");
  if (!snapshot.source_roles.some((item) => item.completion_role === "REQUIRED")) {
    reject("MISSING_REQUIRED_SOURCE_ROLE", "At least one source role must be required for completion.");
  }
  if (!snapshot.trial_slots.some((item) => item.required)) {
    reject("MISSING_REQUIRED_TRIAL_SLOT", "At least one trial slot must be required for completion.");
  }
  const sequences = snapshot.trial_slots.filter((item) => item.sequence !== undefined).map((item) => item.sequence);
  unique(sequences, "DUPLICATE_TRIAL_SEQUENCE", "Defined trial-slot sequences");
  if (snapshot.snapshot_revision === 1 && snapshot.supersedes_protocol_snapshot_id !== undefined) {
    reject("INVALID_SNAPSHOT_ANCESTRY", "First snapshot revision cannot supersede another snapshot.");
  }
  if (snapshot.snapshot_revision > 1 && snapshot.supersedes_protocol_snapshot_id === undefined) {
    reject("MISSING_SNAPSHOT_ANCESTRY", "Revised snapshot must identify the snapshot it supersedes.");
  }
  return true;
}

const sourceTransitions = new Map([
  ["CREATED|CONFIGURE", new Set(["CONFIGURING", "CONFIGURED"])],
  ["CONFIGURING|CONFIGURE", new Set(["CONFIGURED"])],
  ["CONFIGURED|CHECK_READINESS", new Set(["CHECKING_READINESS", "READY"])],
  ["CHECKING_READINESS|CHECK_READINESS", new Set(["READY"])],
  ["READY|ARM", new Set(["ARMING", "ARMED"])],
  ["ARMING|ARM", new Set(["ARMED"])],
  ["ARMED|DISARM", new Set(["READY"])],
  ["ARMED|PREPARE_START", new Set(["ARMED"])],
  ["ARMED|COMMIT_START", new Set(["START_SCHEDULED"])],
  ["START_SCHEDULED|CANCEL_START_PLAN", new Set(["ARMED"])],
  ["RECORDING|STOP_AT", new Set(["STOPPING"])],
  ["STARTING|EMERGENCY_STOP", new Set(["CANCELLED_BEFORE_CAPTURE", "STOPPING"])],
  ["RECORDING|EMERGENCY_STOP", new Set(["STOPPING"])]
]);

const sourceIdentityKeys = ["session_id", "trial_id", "source_id", "capture_attempt_id", "source_kind", "source_boot_id"];

const sourceEventTransitions = new Map([
  ["CONFIGURING", new Set(["CONFIGURED"])],
  ["CHECKING_READINESS", new Set(["READY", "CONFIGURED"])],
  ["ARMING", new Set(["ARMED", "READY"])],
  ["START_SCHEDULED", new Set(["STARTING", "ARMED"])],
  ["STARTING", new Set(["RECORDING", "CANCELLED_BEFORE_CAPTURE", "FINALIZATION_FAILED"])],
  ["RECORDING", new Set(["STOPPING"])],
  ["STOPPING", new Set(["FINALIZING"])],
  ["FINALIZING", new Set(["FINALIZED_COMPLETE", "FINALIZED_INCOMPLETE", "FINALIZATION_FAILED"])]
]);

export function validateSourceState(state) {
  validateMonotonicInstant(state.updated_source_time, state.source_clock.clock_id);
  if (state.updated_source_time.ticks_per_second !== state.source_clock.ticks_per_second) {
    reject("CLOCK_FREQUENCY_MISMATCH", "State timestamp and source clock frequency differ.");
  }
  checkedUnsigned64(state.maximum_duration_ns, "DURATION_INVALID", "Maximum duration");
  if (state.has_acquired_master_sample) {
    if (!state.first_master_sample) reject("FIRST_MASTER_SAMPLE_MISSING", "Acquired-master state requires first-sample evidence.");
    validateMonotonicInstant(state.first_master_sample.source_time, state.source_clock.clock_id);
    checkedUnsigned64(state.first_master_sample.first_sequence, "SAMPLE_SEQUENCE_INVALID", "First master sequence");
    checkedUnsigned64(state.first_master_sample.mapped_session_time.ticks, "MAPPED_TIME_INVALID", "Mapped session ticks");
    checkedUnsigned64(state.first_master_sample.mapped_session_time.uncertainty_ns, "UNCERTAINTY_INVALID", "Mapped uncertainty");
  } else if (state.first_master_sample !== undefined) {
    reject("FIRST_MASTER_SAMPLE_CONTRADICTION", "First-sample evidence cannot exist when acquired-master history is false.");
  }
  if (state.state === "RECORDING" && !state.has_acquired_master_sample) {
    reject("RECORDING_WITHOUT_MASTER_SAMPLE", "RECORDING requires accepted first-master-sample evidence.");
  }
  unique(state.finalized_package_inventory.map((item) => item.package_id), "DUPLICATE_FINALIZED_PACKAGE", "Finalized package IDs");
  return true;
}

export function validateSourceTransition({ current, command, next, event, startPlan }) {
  sameIdentity(current, next, sourceIdentityKeys, "SOURCE_IDENTITY_CHANGED");
  sameIdentity(current, command, ["session_id", "trial_id", "source_id", "capture_attempt_id"], "COMMAND_SOURCE_IDENTITY_MISMATCH");
  if (command.expected_source_state !== current.state) reject("SOURCE_STATE_CONFLICT", "Command expected state does not match actual state.");
  if (next.revision !== current.revision + 1) reject("SOURCE_REVISION_INVALID", "Accepted source transition must increment revision exactly once.");
  const allowed = sourceTransitions.get(`${current.state}|${command.command_type}`);
  if (!allowed || !allowed.has(next.state)) reject("SOURCE_TRANSITION_FORBIDDEN", `${current.state} cannot apply ${command.command_type} to reach ${next.state}.`);
  if (["PREPARE_START", "COMMIT_START"].includes(command.command_type)) {
    if (!startPlan) reject("START_PLAN_REQUIRED", "Start commands require the complete immutable start plan.");
    if (command.payload.start_plan_id !== startPlan.start_plan_id
      || command.payload.start_plan_content_sha256 !== startPlan.start_plan_content_sha256) {
      reject("START_COMMAND_PLAN_MISMATCH", "Start command identity/hash does not bind the supplied plan.");
    }
    validateStartPlan(startPlan, [current]);
    if (command.command_type === "COMMIT_START"
      && (next.start_plan_id !== startPlan.start_plan_id || next.start_plan_content_sha256 !== startPlan.start_plan_content_sha256)) {
      reject("SOURCE_STATE_PLAN_MISMATCH", "Scheduled source state must retain the committed start plan identity/hash.");
    }
  }
  if (command.command_type === "STOP_AT") {
    validateMonotonicInstant(command.payload.scheduled_source_stop, current.source_clock.clock_id);
    checkedUnsigned64(command.payload.scheduled_session_stop.ticks, "MAPPED_TIME_INVALID", "Scheduled session stop ticks");
    checkedUnsigned64(command.payload.scheduled_session_stop.uncertainty_ns, "UNCERTAINTY_INVALID", "Scheduled stop uncertainty");
  }
  if (current.has_acquired_master_sample && !next.has_acquired_master_sample) reject("MASTER_HISTORY_ERASED", "Acquired-master history cannot return to false.");
  if (["ARMING", "ARMED", "START_SCHEDULED", "STARTING", "RECORDING", "STOPPING", "FINALIZING", "FINALIZED_COMPLETE", "FINALIZED_INCOMPLETE", "FINALIZATION_FAILED"].includes(current.state)) {
    if (current.configuration_id !== next.configuration_id || current.configuration_content_sha256 !== next.configuration_content_sha256) {
      reject("SOURCE_CONFIGURATION_CHANGED_AFTER_ARMING", "Configuration cannot change from arming through finalization.");
    }
  }
  validateSourceState(current);
  validateSourceState(next);
  if (event) {
    sameIdentity(next, event, ["session_id", "trial_id", "source_id", "capture_attempt_id", "source_boot_id"], "EVENT_SOURCE_IDENTITY_MISMATCH");
    if (event.prior_state !== current.state || event.resulting_state !== next.state || event.resulting_source_revision !== next.revision) {
      reject("EVENT_STATE_MISMATCH", "Event state/revision must match the authoritative transition.");
    }
    if (event.event_sequence !== next.last_event_sequence || event.authoritative_state.revision !== next.revision) {
      reject("EVENT_SEQUENCE_MISMATCH", "Event sequence and embedded authoritative state must match the result.");
    }
    validateMonotonicInstant(event.event_source_time, current.source_clock.clock_id);
    if (event.related_command_id !== command.command_id) reject("EVENT_COMMAND_MISMATCH", "Command-driven event must reference its command ID.");
  }
  return true;
}

export function validateSourceEventTransition(current, next, event) {
  sameIdentity(current, next, sourceIdentityKeys, "SOURCE_IDENTITY_CHANGED");
  sameIdentity(next, event, ["session_id", "trial_id", "source_id", "capture_attempt_id", "source_boot_id"], "EVENT_SOURCE_IDENTITY_MISMATCH");
  if (!sourceEventTransitions.get(current.state)?.has(next.state)) {
    reject("SOURCE_EVENT_TRANSITION_FORBIDDEN", `${current.state} cannot report ${next.state}.`);
  }
  if (next.revision !== current.revision + 1 || event.resulting_source_revision !== next.revision) {
    reject("SOURCE_REVISION_INVALID", "Source event transition must increment revision exactly once.");
  }
  if (event.event_sequence !== current.last_event_sequence + 1 || next.last_event_sequence !== event.event_sequence) {
    reject("EVENT_SEQUENCE_MISMATCH", "Source event sequence must increase exactly once.");
  }
  if (event.prior_state !== current.state || event.resulting_state !== next.state) reject("EVENT_STATE_MISMATCH", "Event does not describe the state transition.");
  if (canonical(event.authoritative_state) !== canonical(next)) reject("EVENT_SNAPSHOT_MISMATCH", "Embedded authoritative state must equal the resulting snapshot.");
  const currentTicks = validateMonotonicInstant(current.updated_source_time, current.source_clock.clock_id);
  const eventTicks = validateMonotonicInstant(event.event_source_time, current.source_clock.clock_id);
  if (eventTicks < currentTicks) reject("EVENT_TIME_REGRESSION", "Source event monotonic time regressed.");
  if (event.event_type === "FIRST_MASTER_SAMPLE") {
    if (next.state !== "RECORDING" || !next.has_acquired_master_sample || !event.first_master_sample) {
      reject("FIRST_MASTER_SAMPLE_INVALID", "FIRST_MASTER_SAMPLE must authoritatively enter RECORDING with evidence.");
    }
    if (canonical(event.first_master_sample) !== canonical(next.first_master_sample)) {
      reject("FIRST_MASTER_SAMPLE_MISMATCH", "Event and state first-sample evidence differ.");
    }
    const sampleTicks = validateMonotonicInstant(event.first_master_sample.source_time, current.source_clock.clock_id);
    if (sampleTicks > eventTicks) reject("FIRST_MASTER_SAMPLE_AFTER_EVENT", "First-sample source time cannot occur after its event.");
  }
  if (current.has_acquired_master_sample && !next.has_acquired_master_sample) reject("MASTER_HISTORY_ERASED", "Acquired-master history cannot return to false.");
  validateSourceState(current);
  validateSourceState(next);
  return true;
}

export function validateSourceReconciliation(prior, snapshot) {
  if (prior.source_id !== snapshot.source_id) reject("RECONCILIATION_SOURCE_MISMATCH", "Snapshot belongs to another source.");
  if (prior.source_boot_id !== snapshot.source_boot_id || prior.source_clock.clock_id !== snapshot.source_clock.clock_id) {
    if (prior.capture_attempt_id === snapshot.capture_attempt_id) {
      reject("ATTEMPT_CONTINUED_ACROSS_BOOT", "A new boot/clock epoch requires a new capture-attempt ID.");
    }
    return true;
  }
  if (snapshot.capture_attempt_id === prior.capture_attempt_id && snapshot.revision < prior.revision) {
    reject("STALE_RECONCILIATION_SNAPSHOT", "A same-attempt snapshot cannot regress the authoritative revision.");
  }
  return true;
}

export function validateReadinessSnapshot(snapshot, sourceState) {
  sameIdentity(snapshot, sourceState, ["session_id", "trial_id", "source_id", "capture_attempt_id", "source_boot_id"], "READINESS_SOURCE_IDENTITY_MISMATCH");
  if (snapshot.configuration_id !== sourceState.configuration_id
    || snapshot.configuration_content_sha256 !== sourceState.configuration_content_sha256) {
    reject("READINESS_CONFIGURATION_MISMATCH", "Readiness must bind the actual source configuration.");
  }
  validateMonotonicInstant(snapshot.measured_source_time, sourceState.source_clock.clock_id);
  if (snapshot.valid_until_source_time) {
    const measured = validateMonotonicInstant(snapshot.measured_source_time, sourceState.source_clock.clock_id);
    const validUntil = validateMonotonicInstant(snapshot.valid_until_source_time, sourceState.source_clock.clock_id);
    if (snapshot.valid_until_source_time.ticks_per_second !== snapshot.measured_source_time.ticks_per_second || validUntil <= measured) {
      reject("READINESS_VALIDITY_INVALID", "Readiness validity end must be later on the same source clock/frequency.");
    }
  }
  unique(snapshot.checks.map((item) => item.check_id), "DUPLICATE_READINESS_CHECK", "Readiness check IDs");
  const blocking = snapshot.checks.some((item) => ["BLOCKING_FAILURE", "NOT_ASSESSED"].includes(item.disposition));
  const unresolvedOverride = snapshot.checks.some((item) => item.disposition === "OVERRIDE_REQUIRED" && !item.override);
  const invalidOverride = snapshot.checks.some((item) => item.override && item.override_policy !== "PROTOCOL_ALLOWED");
  if (invalidOverride) reject("READINESS_OVERRIDE_NOT_ALLOWED", "Only a protocol-allowed check may carry an override.");
  for (const check of snapshot.checks.filter((item) => item.override)) {
    if (check.disposition !== "OVERRIDE_REQUIRED") reject("READINESS_OVERRIDE_DISPOSITION_INVALID", "Overrides apply only to OVERRIDE_REQUIRED checks.");
    if (Date.parse(check.override.expires_utc) <= Date.parse(check.override.authorized_utc)
      || Date.parse(check.override.expires_utc) < Date.parse(snapshot.recorded_utc)) {
      reject("READINESS_OVERRIDE_EXPIRED", "Override must be valid after authorization and at snapshot creation.");
    }
  }
  if (blocking && snapshot.aggregate_disposition !== "BLOCKED") reject("READINESS_FALSE_READY", "Blocking or unassessed checks require BLOCKED.");
  if (!blocking && unresolvedOverride && snapshot.aggregate_disposition !== "READY_WITH_OVERRIDES_REQUIRED") {
    reject("READINESS_OVERRIDE_UNRESOLVED", "Unresolved required overrides must remain visible.");
  }
  if (!blocking && !unresolvedOverride && snapshot.checks.some((item) => item.override)
    && snapshot.aggregate_disposition !== "READY_WITH_AUTHORIZED_DEVIATIONS") {
    reject("READINESS_OVERRIDE_AGGREGATE_MISMATCH", "Authorized deviations require their explicit aggregate disposition.");
  }
  if (!blocking && !unresolvedOverride && !snapshot.checks.some((item) => item.override)) {
    const expected = snapshot.checks.some((item) => item.disposition === "WARNING") ? "READY_WITH_WARNINGS" : "READY";
    if (snapshot.aggregate_disposition !== expected) reject("READINESS_AGGREGATE_MISMATCH", `Readiness aggregate must be ${expected}.`);
  }
  return true;
}

const custodyTransitions = new Map([
  ["BUILDING", new Set(["FINALIZED_LOCAL"])],
  ["FINALIZED_LOCAL", new Set(["COLLECTION_PENDING"])],
  ["COLLECTION_PENDING", new Set(["COLLECTING"])],
  ["COLLECTING", new Set(["STAGED", "COLLECTION_INTERRUPTED"])],
  ["COLLECTION_INTERRUPTED", new Set(["COLLECTING"])],
  ["STAGED", new Set(["VERIFYING"])],
  ["VERIFYING", new Set(["VERIFIED", "VERIFICATION_FAILED", "QUARANTINED"])],
  ["VERIFICATION_FAILED", new Set(["QUARANTINED"])],
  ["VERIFIED", new Set(["COMMITTING"])],
  ["COMMITTING", new Set(["COMMITTED", "COMMIT_RECOVERY_REQUIRED", "QUARANTINED"])],
  ["COMMIT_RECOVERY_REQUIRED", new Set(["COMMITTING", "COMMITTED", "QUARANTINED"])],
  ["COMMITTED", new Set(["RECEIPT_PENDING"])],
  ["RECEIPT_PENDING", new Set(["RECEIPT_ACKNOWLEDGED", "RECEIPT_STATUS_UNKNOWN"])],
  ["RECEIPT_STATUS_UNKNOWN", new Set(["RECEIPT_ACKNOWLEDGED", "RECEIPT_PENDING"])],
  ["RECEIPT_ACKNOWLEDGED", new Set(["SAFE_TO_DELETE"])],
  ["SAFE_TO_DELETE", new Set(["DELETED_FROM_SOURCE"])]
]);

export function validateCustodyTransition(current, next) {
  sameIdentity(current, next, ["custody_record_id", "session_id", "trial_id", "source_id", "capture_attempt_id", "package_id", "package_content_sha256"], "CUSTODY_IDENTITY_CHANGED");
  if (next.revision !== current.revision + 1) reject("CUSTODY_REVISION_INVALID", "Custody revision must increment exactly once.");
  if (!custodyTransitions.get(current.state)?.has(next.state)) reject("CUSTODY_TRANSITION_FORBIDDEN", `${current.state} cannot reach ${next.state}.`);
  if (current.collection_method !== "NOT_STARTED" && next.collection_method !== current.collection_method) {
    reject("COLLECTION_METHOD_CHANGED", "A collection attempt cannot silently change transfer method.");
  }
  return true;
}

export function validateCustodyRecord(record) {
  const authorityStates = {
    SOURCE: new Set(["BUILDING", "FINALIZED_LOCAL", "DELETED_FROM_SOURCE"]),
    COLLECTOR: new Set(["COLLECTION_PENDING", "COLLECTING", "STAGED", "COLLECTION_INTERRUPTED"]),
    VERIFIER: new Set(["VERIFYING", "VERIFIED", "VERIFICATION_FAILED", "QUARANTINED"]),
    REPOSITORY: new Set(["COMMITTING", "COMMITTED", "RECEIPT_PENDING", "RECEIPT_ACKNOWLEDGED", "SAFE_TO_DELETE", "COMMIT_RECOVERY_REQUIRED", "RECEIPT_STATUS_UNKNOWN"])
  };
  if (!authorityStates[record.authority]?.has(record.state)) reject("CUSTODY_AUTHORITY_MISMATCH", "Custody state is not reported by its owning authority.");
  if (record.bytes_verified !== undefined) checkedUnsigned64(record.bytes_verified, "BYTES_VERIFIED_INVALID", "Verified byte count");
  return true;
}

export function validateCommitReceipt(custody, receipt) {
  validateCustodyRecord(custody);
  sameIdentity(custody, receipt, ["session_id", "trial_id", "source_id", "capture_attempt_id", "package_id", "package_content_sha256", "commit_id", "repository_relative_path"], "RECEIPT_IDENTITY_MISMATCH");
  if (!["COMMITTED", "RECEIPT_PENDING", "RECEIPT_ACKNOWLEDGED", "SAFE_TO_DELETE", "DELETED_FROM_SOURCE", "RECEIPT_STATUS_UNKNOWN"].includes(custody.state)) {
    reject("RECEIPT_BEFORE_COMMIT", "A receipt can only bind an already committed package.");
  }
  if (receipt.receipt_revision !== 1) reject("RECEIPT_REVISION_INVALID", "An exact commit/package hash has one immutable receipt at revision 1.");
  if (!receipt.device_id) reject("RECEIPT_DEVICE_REQUIRED", "A receipt must bind the exact capture device.");
  if (receipt.supersedes_receipt_id !== undefined) reject("RECEIPT_SUPERSESSION_FORBIDDEN", "Commit receipts are immutable and cannot supersede another receipt.");
  if (custody.receipt_id && custody.receipt_id !== receipt.receipt_id) reject("RECEIPT_ID_CONFLICT", "Custody references a different receipt.");
  return true;
}

const receiptIdentityKeys = [
  "receipt_id", "receipt_revision", "commit_id", "issued_by_coordinator_id", "device_id",
  "session_id", "trial_id", "source_id", "capture_attempt_id", "package_id",
  "package_content_sha256", "artifact_set_sha256", "repository_relative_path", "issued_utc"
];

export function classifyCommitReceiptReplay(existingReceipts, incoming) {
  const byReceipt = existingReceipts.find((item) => item.receipt_id === incoming.receipt_id);
  const byCommit = existingReceipts.find((item) => item.commit_id === incoming.commit_id);
  const byPackageHash = existingReceipts.find((item) => item.package_id === incoming.package_id
    && item.package_content_sha256 === incoming.package_content_sha256);
  const existing = byReceipt ?? byCommit ?? byPackageHash;
  if (!existing) return "NEW";
  const identical = receiptIdentityKeys.every((key) => existing[key] === incoming[key]);
  if (identical) return "IDEMPOTENT_REPLAY";
  reject("RECEIPT_IMMUTABILITY_CONFLICT", "Receipt, commit, or package/hash identity was reused with different content; recovery is required and source files must be retained.");
}

const receiptAckIdentityKeys = ["receipt_id", "receipt_revision", "device_id", "source_id", "package_id", "package_content_sha256"];

export function validateReceiptAcknowledgement(receipt, acknowledgement, localPackage, priorAcknowledgement) {
  if (acknowledgement.result === "REJECTED_MISMATCH") {
    const receiptMatches = receiptAckIdentityKeys.every((key) => receipt[key] === acknowledgement[key]);
    const packageMatches = ["device_id", "source_id", "package_id", "package_content_sha256"].every((key) => receipt[key] === localPackage[key]);
    if (acknowledgement.receipt_durably_stored !== false || (receiptMatches && packageMatches)) {
      reject("RECEIPT_ACK_FALSE_MISMATCH", "Mismatch rejection must reflect a real identity/hash mismatch and must not store the receipt.");
    }
    return "RETAIN_AND_RECOVER";
  }
  sameIdentity(receipt, acknowledgement, receiptAckIdentityKeys, "RECEIPT_ACK_IDENTITY_MISMATCH");
  sameIdentity(receipt, localPackage, ["device_id", "source_id", "package_id", "package_content_sha256"], "RECEIPT_LOCAL_PACKAGE_MISMATCH");
  if (acknowledgement.receipt_durably_stored !== true) reject("RECEIPT_ACK_NOT_DURABLE", "Acceptance cannot be emitted until the exact receipt is durably stored.");
  if (acknowledgement.result === "ALREADY_ACKNOWLEDGED") {
    if (!priorAcknowledgement) reject("RECEIPT_ACK_HISTORY_MISSING", "ALREADY_ACKNOWLEDGED requires a durable prior acknowledgement.");
    sameIdentity(priorAcknowledgement, acknowledgement, receiptAckIdentityKeys, "RECEIPT_ACK_REPLAY_CONFLICT");
  } else if (priorAcknowledgement) {
    reject("RECEIPT_ACK_DUPLICATE_AS_NEW", "A durable prior acknowledgement must replay as ALREADY_ACKNOWLEDGED.");
  }
  return "RECEIPT_ACKNOWLEDGED";
}

export function validateReceiptStatusReconciliation(receipt, query, response) {
  sameIdentity(receipt, query, receiptAckIdentityKeys, "RECEIPT_QUERY_IDENTITY_MISMATCH");
  sameIdentity(query, response, ["query_id", ...receiptAckIdentityKeys], "RECEIPT_STATUS_IDENTITY_MISMATCH");
  if (response.status === "NOT_STORED") return "RESEND_IDENTICAL_RECEIPT";
  if (response.status === "STORED_ACK_PENDING") return "REQUEST_SAME_ACKNOWLEDGEMENT";
  if (response.status === "ACKNOWLEDGED") return "RECONCILE_EXISTING_ACKNOWLEDGEMENT";
  reject("RECEIPT_STATUS_UNKNOWN", "Unknown receipt status requires recovery with source files retained.");
}

function indexedBy(values, key, code) {
  unique(values.map((value) => value[key]), code, key);
  return new Map(values.map((value) => [value[key], value]));
}

export function validateCleanupRequest(request, receipts, acknowledgements, runtime = {}) {
  if (request.operator_confirmation !== true) reject("CLEANUP_CONFIRMATION_REQUIRED", "Cleanup requires one explicit informed operator confirmation.");
  const receiptById = indexedBy(receipts, "receipt_id", "DUPLICATE_RECEIPT_ID");
  const acknowledgementById = indexedBy(acknowledgements, "acknowledgement_id", "DUPLICATE_ACKNOWLEDGEMENT_ID");
  unique(request.packages.map((item) => item.package_id), "DUPLICATE_CLEANUP_PACKAGE", "Cleanup package IDs");
  const active = new Set(runtime.activePackageIds ?? []);
  const offlineOnly = new Set(runtime.offlineOnlyPackageIds ?? []);
  for (const selected of request.packages) {
    if (active.has(selected.package_id)) reject("CLEANUP_PACKAGE_ACTIVE", "Capture, finalization, transfer, or recovery is active for a selected package.");
    if (offlineOnly.has(selected.package_id)) reject("CLEANUP_OFFLINE_AUTOMATION_FORBIDDEN", "USB/MTP-only completion supports informed manual cleanup, not automatic deletion authority.");
    const receipt = receiptById.get(selected.receipt_id);
    const acknowledgement = acknowledgementById.get(selected.acknowledgement_id);
    if (!receipt || !acknowledgement) reject("CLEANUP_DURABLE_EVIDENCE_MISSING", "Each selected package requires its durable receipt and acknowledgement.");
    sameIdentity(receipt, selected, ["receipt_id", "receipt_revision", "source_id", "package_id", "package_content_sha256"], "CLEANUP_RECEIPT_MISMATCH");
    sameIdentity(acknowledgement, selected, ["acknowledgement_id", "receipt_id", "receipt_revision", "source_id", "package_id", "package_content_sha256"], "CLEANUP_ACKNOWLEDGEMENT_MISMATCH");
    if (receipt.device_id !== request.device_id || acknowledgement.device_id !== request.device_id) reject("CLEANUP_DEVICE_MISMATCH", "Cleanup request does not bind the receipt device.");
    if (!["ACCEPTED", "ALREADY_ACKNOWLEDGED"].includes(acknowledgement.result)) reject("CLEANUP_RECEIPT_NOT_ACKNOWLEDGED", "A rejected receipt cannot authorize cleanup.");
  }
  return true;
}

export function validateCleanupResult(request, result) {
  sameIdentity(request, result, ["cleanup_request_id", "device_id"], "CLEANUP_RESULT_REQUEST_MISMATCH");
  const requested = indexedBy(request.packages, "package_id", "DUPLICATE_CLEANUP_PACKAGE");
  const reported = indexedBy(result.packages, "package_id", "DUPLICATE_CLEANUP_RESULT");
  if (requested.size !== reported.size) reject("CLEANUP_RESULT_INCOMPLETE", "Cleanup result must report every requested package exactly once.");
  for (const [packageId, selected] of requested) {
    const outcome = reported.get(packageId);
    if (!outcome) reject("CLEANUP_RESULT_INCOMPLETE", "A requested package has no cleanup result.");
    sameIdentity(selected, outcome, ["receipt_id", "source_id", "package_id", "package_content_sha256"], "CLEANUP_RESULT_IDENTITY_MISMATCH");
    const remaining = outcome.remaining_artifact_relative_paths;
    if (outcome.result === "DELETED" && remaining.length !== 0) reject("CLEANUP_FALSE_DELETED", "DELETED requires no remaining artifacts.");
    if (outcome.result === "PARTIAL_DELETE" && remaining.length === 0) reject("CLEANUP_PARTIAL_WITHOUT_REMAINDER", "PARTIAL_DELETE must name every remaining artifact.");
  }
  return true;
}

export function validateCleanupRetry(previousRequest, previousResult, retryRequest) {
  if (retryRequest.packages.length !== 1) reject("CLEANUP_RETRY_SCOPE_INVALID", "A partial retry must target one reconciled package.");
  const selected = retryRequest.packages[0];
  const priorRequest = previousRequest.packages.find((item) => item.package_id === selected.package_id);
  const priorResult = previousResult.packages.find((item) => item.package_id === selected.package_id);
  if (!priorRequest || !priorResult || priorResult.result !== "PARTIAL_DELETE") reject("CLEANUP_RETRY_NOT_PARTIAL", "Only a prior partial deletion may be retried.");
  sameIdentity(priorRequest, selected, ["receipt_id", "receipt_revision", "acknowledgement_id", "source_id", "package_id", "package_content_sha256"], "CLEANUP_RETRY_IDENTITY_MISMATCH");
  const expected = [...priorResult.remaining_artifact_relative_paths].sort();
  const actual = [...selected.artifact_relative_paths].sort();
  if (canonical(expected) !== canonical(actual)) reject("CLEANUP_RETRY_SET_MISMATCH", "Retry may target only the reconciled remaining artifact set.");
  return true;
}

export function validateQualityAssessment(assessment) {
  unique(assessment.dimensions.map((item) => item.dimension), "DUPLICATE_QUALITY_DIMENSION", "Quality dimensions");
  const blocking = assessment.dimensions.some((item) => ["DEVIATION_REVIEW_REQUIRED", "FAIL_RETAKE_REQUIRED", "ASSESSING", "NOT_ASSESSED"].includes(item.outcome));
  if (blocking && ["PASS", "PASS_WITH_WARNINGS"].includes(assessment.outcome)) {
    reject("QUALITY_FALSE_PASS", "Overall quality cannot pass while a dimension remains blocking or unresolved.");
  }
  if (!blocking) {
    const hasWarnings = assessment.dimensions.some((item) => item.outcome === "PASS_WITH_WARNINGS");
    if (assessment.outcome === "PASS" && hasWarnings) reject("QUALITY_WARNING_HIDDEN", "Overall PASS cannot hide a warning dimension.");
    if (assessment.outcome === "PASS_WITH_WARNINGS" && !hasWarnings) reject("QUALITY_WARNING_AGGREGATE_MISMATCH", "PASS_WITH_WARNINGS requires at least one warning dimension.");
  }
  if (assessment.assessment_revision === 1 && assessment.supersedes_assessment_id !== undefined) {
    reject("QUALITY_ANCESTRY_INVALID", "First assessment revision cannot supersede another assessment.");
  }
  if (assessment.assessment_revision > 1 && assessment.supersedes_assessment_id === undefined) {
    reject("QUALITY_ANCESTRY_MISSING", "Reassessment must preserve the prior assessment identity.");
  }
  return true;
}

const transitions = new Map([
  ["DRAFT|PLAN_SESSION", new Set(["PLANNED"])],
  ["DRAFT|CANCEL_SESSION", new Set(["CANCELLED"])],
  ["DRAFT|RECONCILE_SESSION", new Set(["DRAFT"])],
  ["PLANNED|REVISE_SESSION_PLAN", new Set(["PLANNED"])],
  ["PLANNED|BEGIN_SESSION", new Set(["IN_PROGRESS"])],
  ["PLANNED|CANCEL_SESSION", new Set(["CANCELLED"])],
  ["PLANNED|RECONCILE_SESSION", new Set(["PLANNED", "RECOVERY_REQUIRED"])],
  ["IN_PROGRESS|ENTER_COMPLETION_REVIEW", new Set(["COMPLETION_REVIEW"])],
  ["IN_PROGRESS|CLOSE_SESSION_INCOMPLETE", new Set(["CLOSED_INCOMPLETE"])],
  ["IN_PROGRESS|RECONCILE_SESSION", new Set(["IN_PROGRESS", "RECOVERY_REQUIRED"])],
  ["COMPLETION_REVIEW|COMPLETE_SESSION", new Set(["COMPLETE"])],
  ["COMPLETION_REVIEW|BEGIN_SESSION", new Set(["IN_PROGRESS"])],
  ["COMPLETION_REVIEW|CLOSE_SESSION_INCOMPLETE", new Set(["CLOSED_INCOMPLETE"])],
  ["COMPLETION_REVIEW|RECONCILE_SESSION", new Set(["COMPLETION_REVIEW", "RECOVERY_REQUIRED"])],
  ["RECOVERY_REQUIRED|RECONCILE_SESSION", new Set(["RECOVERY_REQUIRED", "IN_PROGRESS", "COMPLETION_REVIEW"])],
  ["RECOVERY_REQUIRED|CLOSE_SESSION_INCOMPLETE", new Set(["CLOSED_INCOMPLETE"])],
  ["COMPLETE|REOPEN_SESSION_FOR_REVIEW", new Set(["COMPLETION_REVIEW"])],
  ["COMPLETE|RECONCILE_SESSION", new Set(["COMPLETE"])]
]);

function validateSlotProgress(state, protocolSnapshot) {
  unique(state.slot_progress.map((item) => item.slot_id), "DUPLICATE_SLOT_PROGRESS", "Session slot-progress IDs");
  for (const slot of state.slot_progress) {
    unique(slot.candidate_trial_ids, "DUPLICATE_TRIAL_CANDIDATE", `Candidate trials for slot ${slot.slot_id}`);
    if (slot.state === "SATISFIED") {
      if (!slot.accepted_trial_id || !slot.candidate_trial_ids.includes(slot.accepted_trial_id)) {
        reject("INVALID_ACCEPTED_TRIAL", `Satisfied slot ${slot.slot_id} needs one accepted candidate trial.`);
      }
    } else if (slot.accepted_trial_id !== undefined) {
      reject("ACCEPTED_TRIAL_WITHOUT_SATISFACTION", `Only a satisfied slot may identify an accepted trial.`);
    }
  }
  if (protocolSnapshot) {
    const progress = new Map(state.slot_progress.map((item) => [item.slot_id, item]));
    for (const planned of protocolSnapshot.trial_slots) {
      const actual = progress.get(planned.slot_id);
      if (!actual || actual.required !== planned.required) {
        reject("TRIAL_PLAN_MISMATCH", `Session progress does not match planned slot ${planned.slot_id}.`);
      }
    }
    for (const actual of state.slot_progress) {
      if (!protocolSnapshot.trial_slots.some((item) => item.slot_id === actual.slot_id)) {
        reject("UNPLANNED_SLOT", `Session progress includes unplanned slot ${actual.slot_id}.`);
      }
    }
  }
}

export function validateSessionCompletionState(state, protocolSnapshot) {
  validateSlotProgress(state, protocolSnapshot);
  if (state.state !== "COMPLETE") reject("NOT_COMPLETE_STATE", "Completion predicates apply only to COMPLETE.");
  const unresolvedSlots = state.slot_progress.filter((slot) => slot.required && slot.state !== "SATISFIED");
  if (unresolvedSlots.length) reject("REQUIRED_SLOT_UNSATISFIED", "Every required trial slot must be satisfied.");
  if (state.active_trial_ids.length) reject("ACTIVE_TRIAL_AT_COMPLETION", "A complete session cannot have active trials.");
  if (state.unresolved_required_work) reject("UNRESOLVED_REQUIRED_WORK", "Required work remains unresolved.");
  if (!state.completion_record_id || !state.handoff_manifest_id) {
    reject("MISSING_COMPLETION_ARTIFACT", "Completion and handoff records are required.");
  }
  return true;
}

export function validateCompletionBundle(state, completion, handoff, protocolSnapshot) {
  validateSessionCompletionState(state, protocolSnapshot);
  const identities = [completion.session_id, handoff.session_id];
  if (identities.some((value) => value !== state.session_id)) reject("SESSION_IDENTITY_MISMATCH", "Completion bundle session IDs differ.");
  if (completion.subject_id !== state.subject_id || handoff.subject_id !== state.subject_id) {
    reject("SUBJECT_IDENTITY_MISMATCH", "Completion bundle subject IDs differ.");
  }
  if (completion.protocol_snapshot_id !== state.protocol_snapshot_id || handoff.protocol_snapshot_id !== state.protocol_snapshot_id) {
    reject("PROTOCOL_SNAPSHOT_MISMATCH", "Completion bundle snapshot IDs differ.");
  }
  if (completion.protocol_snapshot_content_sha256 !== state.protocol_snapshot_content_sha256 || handoff.protocol_snapshot_content_sha256 !== state.protocol_snapshot_content_sha256) {
    reject("PROTOCOL_HASH_MISMATCH", "Completion bundle snapshot hashes differ.");
  }
  if (completion.completion_record_id !== state.completion_record_id || completion.completion_record_revision !== state.completion_record_revision) {
    reject("COMPLETION_RECORD_MISMATCH", "State does not reference the supplied completion record.");
  }
  if (handoff.handoff_manifest_id !== state.handoff_manifest_id || handoff.handoff_manifest_revision !== state.handoff_manifest_revision) {
    reject("HANDOFF_RECORD_MISMATCH", "State does not reference the supplied handoff manifest.");
  }
  if (completion.handoff_manifest_id !== handoff.handoff_manifest_id || completion.handoff_manifest_revision !== handoff.handoff_manifest_revision) {
    reject("COMPLETION_HANDOFF_MISMATCH", "Completion record and handoff manifest do not mutually agree.");
  }
  if (handoff.completion_record_id !== completion.completion_record_id || handoff.completion_record_revision !== completion.completion_record_revision) {
    reject("HANDOFF_COMPLETION_MISMATCH", "Handoff manifest does not reference the completion record.");
  }
  const requiredSlots = state.slot_progress.filter((slot) => slot.required).map((slot) => slot.slot_id).sort();
  const acceptedTrials = state.slot_progress.filter((slot) => slot.required).map((slot) => slot.accepted_trial_id).sort();
  if (JSON.stringify([...completion.satisfied_required_slots].sort()) !== JSON.stringify(requiredSlots)) {
    reject("COMPLETION_SLOT_SET_MISMATCH", "Completion record must list exactly the satisfied required slots.");
  }
  if (JSON.stringify([...completion.accepted_trial_ids].sort()) !== JSON.stringify(acceptedTrials)) {
    reject("COMPLETION_TRIAL_SET_MISMATCH", "Completion record must list exactly the accepted required trials.");
  }
  if (completion.finalized_by_windows_account !== state.operator_windows_account) {
    reject("COMPLETION_OPERATOR_MISMATCH", "Completion record operator does not match the authoritative session state.");
  }
  unique(handoff.artifacts.map((item) => item.relative_path), "DUPLICATE_HANDOFF_PATH", "Handoff artifact paths");
  const expectedSessionPath = `subjects/${state.subject_id}/sessions/${state.session_id}`;
  if (handoff.repository_relative_session_path !== expectedSessionPath) {
    reject("HANDOFF_DESTINATION_MISMATCH", "Handoff destination must be the exact subject/session repository path.");
  }
  return true;
}

export function validateSessionTransition({ current, command, next, protocolSnapshot }) {
  if (command.session_id !== current.session_id || next.session_id !== current.session_id) {
    reject("SESSION_IDENTITY_CHANGED", "A transition cannot change session identity.");
  }
  if (next.subject_id !== current.subject_id) reject("SUBJECT_IDENTITY_CHANGED", "A transition cannot change subject identity.");
  if (command.operator_windows_account !== next.operator_windows_account) {
    reject("OPERATOR_ATTRIBUTION_MISMATCH", "Resulting state must retain the command operator attribution.");
  }
  if (command.expected_session_revision !== current.revision) reject("SESSION_REVISION_CONFLICT", "Command expected revision is stale or premature.");
  if (next.revision !== current.revision + 1) reject("SESSION_REVISION_INVALID", "Accepted transition must increment revision exactly once.");
  const allowed = transitions.get(`${current.state}|${command.command_type}`);
  if (!allowed || !allowed.has(next.state)) reject("SESSION_TRANSITION_FORBIDDEN", `${current.state} cannot apply ${command.command_type} to reach ${next.state}.`);

  if (current.has_acquired_master_sample && !next.has_acquired_master_sample) {
    reject("MASTER_HISTORY_ERASED", "Acquired-master history cannot return to false.");
  }
  const nextHasBinding = next.protocol_snapshot_id !== undefined;
  if (current.has_acquired_master_sample || next.has_acquired_master_sample) {
    if (!nextHasBinding || current.protocol_snapshot_id !== next.protocol_snapshot_id || current.protocol_snapshot_content_sha256 !== next.protocol_snapshot_content_sha256) {
      reject("PROTOCOL_CHANGE_AFTER_CAPTURE", "Protocol snapshot cannot change after capture begins.");
    }
  }

  if (command.command_type === "REVISE_SESSION_PLAN") {
    if (current.has_acquired_master_sample) reject("PROTOCOL_CHANGE_AFTER_CAPTURE", "Captured session plans cannot be revised.");
    if (next.protocol_snapshot_id === current.protocol_snapshot_id || next.protocol_snapshot_revision <= current.protocol_snapshot_revision) {
      reject("SNAPSHOT_REVISION_INVALID", "Plan revision needs a new snapshot ID and increasing revision.");
    }
    if (!protocolSnapshot || protocolSnapshot.supersedes_protocol_snapshot_id !== current.protocol_snapshot_id) {
      reject("SNAPSHOT_SUPERSESSION_MISMATCH", "Revised snapshot must identify the prior snapshot.");
    }
  } else if (current.protocol_snapshot_id && next.protocol_snapshot_id !== current.protocol_snapshot_id) {
    reject("UNAUTHORIZED_PROTOCOL_CHANGE", "Only pre-capture plan revision may change snapshot identity.");
  }

  if (["PLAN_SESSION", "REVISE_SESSION_PLAN"].includes(command.command_type)) {
    if (command.payload.protocol_snapshot_id !== next.protocol_snapshot_id
      || command.payload.protocol_snapshot_revision !== next.protocol_snapshot_revision
      || command.payload.protocol_snapshot_content_sha256 !== next.protocol_snapshot_content_sha256) {
      reject("COMMAND_SNAPSHOT_MISMATCH", "Plan command and resulting snapshot binding differ.");
    }
  }

  if (command.command_type === "CANCEL_SESSION") {
    if (current.has_acquired_master_sample || next.has_acquired_master_sample) reject("CANCEL_AFTER_CAPTURE", "A session with acquired samples cannot be cancelled.");
    if (next.closure?.outcome !== "CANCELLED") reject("CANCEL_RECORD_MISSING", "Cancellation outcome record is required.");
  }
  if (command.command_type === "CLOSE_SESSION_INCOMPLETE" && next.closure?.outcome !== "CLOSED_INCOMPLETE") {
    reject("INCOMPLETE_CLOSURE_RECORD_MISSING", "Incomplete closure outcome record is required.");
  }
  if (command.command_type === "COMPLETE_SESSION") validateSessionCompletionState(next, protocolSnapshot);
  if (command.command_type === "REOPEN_SESSION_FOR_REVIEW") {
    if (command.payload.prior_completion_record_id !== current.completion_record_id
      || command.payload.prior_handoff_manifest_id !== current.handoff_manifest_id) {
      reject("REOPEN_REFERENCE_MISMATCH", "Reopen command must reference the current completion and handoff records.");
    }
    if (!next.prior_completion_record_ids?.includes(current.completion_record_id)) {
      reject("PRIOR_COMPLETION_NOT_PRESERVED", "Reopening must preserve the prior completion record reference.");
    }
    if (next.completion_record_id !== undefined || next.handoff_manifest_id !== undefined) {
      reject("STALE_COMPLETION_STILL_ACTIVE", "Reopened review cannot retain old records as current completion.");
    }
  }
  validateSlotProgress(next, protocolSnapshot);
  return true;
}

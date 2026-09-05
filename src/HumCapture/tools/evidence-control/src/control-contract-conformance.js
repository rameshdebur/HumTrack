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

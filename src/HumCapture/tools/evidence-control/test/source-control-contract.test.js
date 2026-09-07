import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  ContractConformanceError,
  computeContentSha256,
  validateCommandReplay,
  validateContentSha256,
  validateCommitReceipt,
  validateCustodyTransition,
  validateQualityAssessment,
  validateReadinessSnapshot,
  validateSourceEventTransition,
  validateSourceConfiguration,
  validateSourceReconciliation,
  validateSourceState,
  validateSourceTransition,
  validateStartPlan
} from "../src/control-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "control", "v1");
const fixtureRoot = path.join(toolRoot, "fixtures", "control");

async function json(filePath) { return JSON.parse(await readFile(filePath, "utf8")); }

async function validators() {
  const ajv = new Ajv2020({ allErrors: true, strict: true });
  addFormats(ajv);
  for (const name of (await readdir(schemaRoot)).filter((item) => item.endsWith(".schema.json")).sort()) {
    ajv.addSchema(await json(path.join(schemaRoot, name)));
  }
  return (name) => ajv.getSchema(`https://humtrack.invalid/humcapture/control/v1/${name}`);
}

function eventFor(current, next, eventType = "STATE_TRANSITION") {
  return {
    contract_version: "1.0.0",
    message_id: "67000000-0000-4000-8000-000000000001",
    event_id: "67000000-0000-4000-8000-000000000002",
    event_type: eventType,
    source_boot_id: next.source_boot_id,
    event_sequence: next.last_event_sequence,
    event_source_time: structuredClone(next.updated_source_time),
    session_id: next.session_id,
    trial_id: next.trial_id,
    source_id: next.source_id,
    capture_attempt_id: next.capture_attempt_id,
    prior_state: current.state,
    resulting_state: next.state,
    resulting_source_revision: next.revision,
    authoritative_state: structuredClone(next),
    recorded_utc: next.updated_utc
  };
}

test("HC-CTRL-TEST-007 source bundle schemas and cross-record predicates pass", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const schema = await validators();
  const records = [
    ["source-configuration.schema.json", fixture.configuration],
    ["readiness-snapshot.schema.json", fixture.readiness],
    ["start-plan.schema.json", fixture.start_plan],
    ["source-state.schema.json", fixture.current_source_state],
    ["source-command.schema.json", fixture.command],
    ["source-state.schema.json", fixture.next_source_state],
    ["package-custody-record.schema.json", fixture.custody],
    ["package-commit-receipt.schema.json", fixture.receipt],
    ["quality-assessment.schema.json", fixture.quality]
  ];
  for (const [name, record] of records) assert.equal(schema(name)(record), true, `${name}: ${JSON.stringify(schema(name).errors)}`);
  const mixedPayload = structuredClone(fixture.command);
  mixedPayload.payload.readiness_request_id = "6b000000-0000-4000-8000-000000000001";
  assert.equal(schema("source-command.schema.json")(mixedPayload), false, "command payloads must be type-exact");
  validateSourceState(fixture.current_source_state);
  validateSourceConfiguration(fixture.configuration, fixture.current_source_state);
  validateReadinessSnapshot(fixture.readiness, fixture.current_source_state);
  validateStartPlan(fixture.start_plan, [fixture.current_source_state]);
  const notArmed = { ...structuredClone(fixture.current_source_state), state: "READY" };
  assert.throws(() => validateStartPlan(fixture.start_plan, [notArmed]), (error) => error.code === "START_PLAN_SOURCE_NOT_ARMED");
  const event = eventFor(fixture.current_source_state, fixture.next_source_state, "COMMAND_RESULT");
  event.related_command_id = fixture.command.command_id;
  assert.equal(schema("source-state-event.schema.json")(event), true, JSON.stringify(schema("source-state-event.schema.json").errors));
  validateSourceTransition({ current: fixture.current_source_state, command: fixture.command, next: fixture.next_source_state, event, startPlan: fixture.start_plan });
  validateCommitReceipt(fixture.custody, fixture.receipt);
  validateQualityAssessment(fixture.quality);

  const acknowledgement = {
    contract_version: "1.0.0",
    message_id: "67000000-0000-4000-8000-000000000003",
    acknowledgement_id: "67000000-0000-4000-8000-000000000004",
    command_id: fixture.command.command_id,
    session_id: fixture.command.session_id,
    trial_id: fixture.command.trial_id,
    source_id: fixture.command.source_id,
    capture_attempt_id: fixture.command.capture_attempt_id,
    source_boot_id: fixture.current_source_state.source_boot_id,
    disposition: "ACCEPTED",
    actual_source_state: fixture.current_source_state.state,
    actual_source_revision: fixture.current_source_state.revision,
    recorded_source_time: fixture.current_source_state.updated_source_time,
    recorded_utc: "2026-09-05T11:00:04.100Z"
  };
  assert.equal(schema("source-command-acknowledgement.schema.json")(acknowledgement), true);
  assert.equal(acknowledgement.actual_source_state, "ARMED", "acknowledgement must not imply the scheduled outcome");
});

test("HC-CTRL-TEST-008 decimal-string monotonic values preserve exact uint64 and reject overflow", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  fixture.current_source_state.updated_source_time.ticks = "18446744073709551615";
  validateSourceState(fixture.current_source_state);
  fixture.current_source_state.updated_source_time.ticks = "18446744073709551616";
  assert.throws(() => validateSourceState(fixture.current_source_state), (error) => error.code === "MONOTONIC_TIME_INVALID");
  fixture.current_source_state.updated_source_time.ticks = "01";
  assert.throws(() => validateSourceState(fixture.current_source_state), (error) => error.code === "MONOTONIC_TIME_INVALID");
});

test("HC-CTRL-TEST-009 command replay is idempotent only for identical semantic content", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const replay = structuredClone(fixture.command);
  replay.message_id = "68000000-0000-4000-8000-000000000001";
  replay.issued_utc = "2026-09-05T11:00:04.500Z";
  assert.equal(validateCommandReplay(fixture.command, replay), true);
  replay.payload.start_plan_content_sha256 = "e".repeat(64);
  assert.throws(() => validateCommandReplay(fixture.command, replay), (error) => error.code === "COMMAND_ID_CONFLICT");
});

test("HC-CTRL-TEST-010 first master sample, event order, and source authority gate RECORDING", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const scheduled = fixture.next_source_state;
  const starting = structuredClone(scheduled);
  starting.state = "STARTING";
  starting.revision = 3;
  starting.last_event_sequence = 2;
  starting.updated_source_time.ticks = "1999999";
  starting.updated_utc = "2026-09-05T11:00:06.000Z";
  validateSourceEventTransition(scheduled, starting, eventFor(scheduled, starting));

  const recording = structuredClone(starting);
  recording.state = "RECORDING";
  recording.revision = 4;
  recording.last_event_sequence = 3;
  recording.has_acquired_master_sample = true;
  recording.recorder_status = "WRITING";
  recording.updated_source_time.ticks = "2000001";
  recording.updated_utc = "2026-09-05T11:00:07.000Z";
  recording.first_master_sample = {
    start_plan_id: fixture.start_plan.start_plan_id,
    first_sequence: "0",
    native_timestamp: "camera2.sensor_timestamp=2000000",
    source_time: { clock_id: recording.source_clock.clock_id, ticks: "2000000", ticks_per_second: 1000000000 },
    mapped_session_time: structuredClone(fixture.start_plan.session_start),
    provenance: "SENSOR",
    late: false,
    discontinuity_detected: false
  };
  const first = eventFor(starting, recording, "FIRST_MASTER_SAMPLE");
  first.first_master_sample = structuredClone(recording.first_master_sample);
  validateSourceEventTransition(starting, recording, first);
  const missing = structuredClone(recording);
  delete missing.first_master_sample;
  assert.throws(() => validateSourceState(missing), (error) => error.code === "FIRST_MASTER_SAMPLE_MISSING");
});

test("HC-CTRL-TEST-011 restart reconciliation forbids an attempt spanning boot or clock epochs", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const restarted = structuredClone(fixture.current_source_state);
  restarted.source_boot_id = "69000000-0000-4000-8000-000000000001";
  restarted.source_clock.clock_id = "69000000-0000-4000-8000-000000000002";
  restarted.updated_source_time.clock_id = restarted.source_clock.clock_id;
  assert.throws(() => validateSourceReconciliation(fixture.current_source_state, restarted), (error) => error.code === "ATTEMPT_CONTINUED_ACROSS_BOOT");
  restarted.capture_attempt_id = "69000000-0000-4000-8000-000000000003";
  assert.equal(validateSourceReconciliation(fixture.current_source_state, restarted), true);
});

test("HC-CTRL-TEST-012 readiness cannot hide blocking checks or unauthorized overrides", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  fixture.readiness.checks[0].disposition = "BLOCKING_FAILURE";
  assert.throws(() => validateReadinessSnapshot(fixture.readiness, fixture.current_source_state), (error) => error.code === "READINESS_FALSE_READY");
  fixture.readiness.aggregate_disposition = "BLOCKED";
  assert.equal(validateReadinessSnapshot(fixture.readiness, fixture.current_source_state), true);
  fixture.readiness.checks[0].override = {
    override_id: "6a000000-0000-4000-8000-000000000001",
    reason: "Test-only unauthorized override.",
    operator_windows_account: "TEST\\operator",
    authorized_utc: "2026-09-05T11:00:00.000Z",
    expires_utc: "2026-09-05T12:00:00.000Z"
  };
  assert.throws(() => validateReadinessSnapshot(fixture.readiness, fixture.current_source_state), (error) => error.code === "READINESS_OVERRIDE_NOT_ALLOWED");
});

test("HC-CTRL-TEST-013 custody matrix fails closed and receipt requires exact durable commit", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  assert.equal(validateCommitReceipt(fixture.custody, fixture.receipt), true);
  const premature = structuredClone(fixture.custody);
  premature.state = "VERIFYING";
  premature.authority = "VERIFIER";
  assert.throws(() => validateCommitReceipt(premature, fixture.receipt), (error) => error.code === "RECEIPT_BEFORE_COMMIT");

  const states = ["BUILDING", "FINALIZED_LOCAL", "COLLECTION_PENDING", "COLLECTING", "STAGED", "VERIFYING", "VERIFIED", "COMMITTING", "COMMITTED", "RECEIPT_PENDING", "RECEIPT_ACKNOWLEDGED", "SAFE_TO_DELETE", "DELETED_FROM_SOURCE", "COLLECTION_INTERRUPTED", "VERIFICATION_FAILED", "QUARANTINED", "COMMIT_RECOVERY_REQUIRED", "RECEIPT_STATUS_UNKNOWN"];
  const allowed = new Set([
    "BUILDING|FINALIZED_LOCAL", "FINALIZED_LOCAL|COLLECTION_PENDING", "COLLECTION_PENDING|COLLECTING",
    "COLLECTING|STAGED", "COLLECTING|COLLECTION_INTERRUPTED", "COLLECTION_INTERRUPTED|COLLECTING",
    "STAGED|VERIFYING", "VERIFYING|VERIFIED", "VERIFYING|VERIFICATION_FAILED", "VERIFYING|QUARANTINED",
    "VERIFICATION_FAILED|QUARANTINED", "VERIFIED|COMMITTING", "COMMITTING|COMMITTED",
    "COMMITTING|COMMIT_RECOVERY_REQUIRED", "COMMITTING|QUARANTINED", "COMMIT_RECOVERY_REQUIRED|COMMITTING",
    "COMMIT_RECOVERY_REQUIRED|COMMITTED", "COMMIT_RECOVERY_REQUIRED|QUARANTINED", "COMMITTED|RECEIPT_PENDING",
    "RECEIPT_PENDING|RECEIPT_ACKNOWLEDGED", "RECEIPT_PENDING|RECEIPT_STATUS_UNKNOWN",
    "RECEIPT_STATUS_UNKNOWN|RECEIPT_ACKNOWLEDGED", "RECEIPT_STATUS_UNKNOWN|RECEIPT_PENDING",
    "RECEIPT_ACKNOWLEDGED|SAFE_TO_DELETE", "SAFE_TO_DELETE|DELETED_FROM_SOURCE"
  ]);
  let forbidden = 0;
  for (const currentState of states) for (const nextState of states) {
    if (allowed.has(`${currentState}|${nextState}`)) continue;
    const current = { ...structuredClone(fixture.custody), state: currentState, revision: 1 };
    const next = { ...structuredClone(current), state: nextState, revision: 2 };
    assert.throws(() => validateCustodyTransition(current, next), (error) => error instanceof ContractConformanceError && error.code === "CUSTODY_TRANSITION_FORBIDDEN");
    forbidden += 1;
  }
  assert.equal(forbidden, 299);
});

test("HC-CTRL-TEST-014 quality cannot report pass over unresolved dimensions", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  fixture.quality.dimensions[1].outcome = "FAIL_RETAKE_REQUIRED";
  assert.throws(() => validateQualityAssessment(fixture.quality), (error) => error.code === "QUALITY_FALSE_PASS");
  fixture.quality.outcome = "FAIL_RETAKE_REQUIRED";
  assert.equal(validateQualityAssessment(fixture.quality), true);
});

test("HC-CTRL-TEST-015 named negative source fixtures reject with their expected code", async () => {
  const cases = await json(path.join(fixtureRoot, "invalid", "source-cases.json"));
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const operations = {
    "uint64 overflow": () => {
      const state = structuredClone(fixture.current_source_state);
      state.updated_source_time.ticks = "18446744073709551616";
      return validateSourceState(state);
    },
    "command id conflict": () => {
      const changed = structuredClone(fixture.command);
      changed.payload.start_plan_content_sha256 = "e".repeat(64);
      return validateCommandReplay(fixture.command, changed);
    },
    "attempt continued across boot": () => {
      const restarted = structuredClone(fixture.current_source_state);
      restarted.source_boot_id = "69000000-0000-4000-8000-000000000001";
      return validateSourceReconciliation(fixture.current_source_state, restarted);
    },
    "readiness false ready": () => {
      const readiness = structuredClone(fixture.readiness);
      readiness.checks[0].disposition = "BLOCKING_FAILURE";
      return validateReadinessSnapshot(readiness, fixture.current_source_state);
    },
    "receipt before commit": () => {
      const custody = { ...structuredClone(fixture.custody), state: "VERIFYING", authority: "VERIFIER" };
      return validateCommitReceipt(custody, fixture.receipt);
    },
    "quality false pass": () => {
      const quality = structuredClone(fixture.quality);
      quality.dimensions[0].outcome = "FAIL_RETAKE_REQUIRED";
      return validateQualityAssessment(quality);
    },
    "custody identity changed": () => {
      const current = structuredClone(fixture.custody);
      const next = { ...structuredClone(current), state: "RECEIPT_PENDING", revision: current.revision + 1, package_id: "6c000000-0000-4000-8000-000000000001" };
      return validateCustodyTransition(current, next);
    }
  };
  for (const item of cases) {
    assert.equal(typeof operations[item.name], "function", `unknown negative fixture ${item.name}`);
    assert.throws(operations[item.name], (error) => error instanceof ContractConformanceError && error.code === item.expected_error_code, item.name);
  }
});

test("HC-CTRL-TEST-016 AsyncAPI exposes every approved I0.2A source record", async () => {
  const document = await json(path.join(humCaptureRoot, "docs", "interfaces", "asyncapi", "control-v1.asyncapi.json"));
  const names = [
    "sourceCommand", "sourceConfiguration", "sourceCommandAcknowledgement", "sourceStateEvent",
    "sourceStateSnapshot", "startPlan", "readinessSnapshot",
    "packageCustodyRecord", "packageCommitReceipt", "qualityAssessment"
  ];
  const expected = [
    "package-commit-receipt.schema.json", "package-custody-record.schema.json",
    "quality-assessment.schema.json", "readiness-snapshot.schema.json",
    "source-command-acknowledgement.schema.json", "source-command.schema.json", "source-configuration.schema.json",
    "source-state-event.schema.json", "source-state.schema.json", "start-plan.schema.json"
  ];
  const actual = names.map((name) => path.basename(document.components.messages[name].payload.schema.$ref)).sort();
  assert.deepEqual(actual, expected);
  for (const name of names) {
    const reference = document.components.messages[name].payload.schema.$ref;
    await readFile(path.resolve(humCaptureRoot, "docs", "interfaces", "asyncapi", reference));
  }
  assert.deepEqual(Object.keys(document.operations).filter((name) => /Source|StartPlan|Readiness|Custody|Receipt|Quality/.test(name)).sort(), [
    "receivePackageCustodyRecord", "receiveQualityAssessment", "receiveReadinessSnapshot", "receiveReceiptAcknowledgement", "receiveReceiptStatusResponse",
    "receiveSourceCommandAcknowledgement", "receiveSourceStateEvent", "receiveSourceStateSnapshot",
    "sendPackageCommitReceipt", "sendReceiptStatusQuery", "sendSourceCommand", "sendSourceConfiguration", "sendStartPlan"
  ]);
});

test("HC-CTRL-TEST-017 every unlisted source command transition fails closed", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const states = ["CREATED", "CONFIGURING", "CONFIGURED", "CHECKING_READINESS", "READY", "ARMING", "ARMED", "START_SCHEDULED", "STARTING", "RECORDING", "STOPPING", "FINALIZING", "FINALIZED_COMPLETE", "CANCELLED_BEFORE_CAPTURE", "FINALIZED_INCOMPLETE", "FINALIZATION_FAILED"];
  const commands = ["CONFIGURE", "CHECK_READINESS", "ARM", "DISARM", "PREPARE_START", "COMMIT_START", "CANCEL_START_PLAN", "STOP_AT", "EMERGENCY_STOP", "RECONCILE_STATE", "GET_STATE"];
  const allowed = new Set([
    "CREATED|CONFIGURE|CONFIGURING", "CREATED|CONFIGURE|CONFIGURED", "CONFIGURING|CONFIGURE|CONFIGURED",
    "CONFIGURED|CHECK_READINESS|CHECKING_READINESS", "CONFIGURED|CHECK_READINESS|READY",
    "CHECKING_READINESS|CHECK_READINESS|READY", "READY|ARM|ARMING", "READY|ARM|ARMED",
    "ARMING|ARM|ARMED", "ARMED|DISARM|READY", "ARMED|PREPARE_START|ARMED",
    "ARMED|COMMIT_START|START_SCHEDULED", "START_SCHEDULED|CANCEL_START_PLAN|ARMED",
    "RECORDING|STOP_AT|STOPPING", "STARTING|EMERGENCY_STOP|CANCELLED_BEFORE_CAPTURE",
    "STARTING|EMERGENCY_STOP|STOPPING", "RECORDING|EMERGENCY_STOP|STOPPING"
  ]);
  let forbidden = 0;
  for (const currentState of states) for (const commandType of commands) for (const nextState of states) {
    if (allowed.has(`${currentState}|${commandType}|${nextState}`)) continue;
    const current = { ...structuredClone(fixture.current_source_state), state: currentState, revision: 1 };
    const command = { ...structuredClone(fixture.command), command_type: commandType, expected_source_state: currentState };
    const next = { ...structuredClone(current), state: nextState, revision: 2 };
    assert.throws(() => validateSourceTransition({ current, command, next }), (error) => error instanceof ContractConformanceError && error.code === "SOURCE_TRANSITION_FORBIDDEN");
    forbidden += 1;
  }
  assert.equal(forbidden, 2799);
});

test("HC-CTRL-TEST-018 every unlisted autonomous source event transition fails closed", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  const states = ["CREATED", "CONFIGURING", "CONFIGURED", "CHECKING_READINESS", "READY", "ARMING", "ARMED", "START_SCHEDULED", "STARTING", "RECORDING", "STOPPING", "FINALIZING", "FINALIZED_COMPLETE", "CANCELLED_BEFORE_CAPTURE", "FINALIZED_INCOMPLETE", "FINALIZATION_FAILED"];
  const allowed = new Set([
    "CONFIGURING|CONFIGURED", "CHECKING_READINESS|READY", "CHECKING_READINESS|CONFIGURED",
    "ARMING|ARMED", "ARMING|READY", "START_SCHEDULED|STARTING", "START_SCHEDULED|ARMED",
    "STARTING|RECORDING", "STARTING|CANCELLED_BEFORE_CAPTURE", "STARTING|FINALIZATION_FAILED",
    "RECORDING|STOPPING", "STOPPING|FINALIZING", "FINALIZING|FINALIZED_COMPLETE",
    "FINALIZING|FINALIZED_INCOMPLETE", "FINALIZING|FINALIZATION_FAILED"
  ]);
  let forbidden = 0;
  for (const currentState of states) for (const nextState of states) {
    if (allowed.has(`${currentState}|${nextState}`)) continue;
    const current = { ...structuredClone(fixture.current_source_state), state: currentState, revision: 1, last_event_sequence: 1 };
    const next = { ...structuredClone(current), state: nextState, revision: 2, last_event_sequence: 2 };
    const event = eventFor(current, next);
    assert.throws(() => validateSourceEventTransition(current, next, event), (error) => error instanceof ContractConformanceError && error.code === "SOURCE_EVENT_TRANSITION_FORBIDDEN");
    forbidden += 1;
  }
  assert.equal(forbidden, 241);
});

test("HC-CTRL-TEST-019 content hashes use RFC 8785 canonical JSON and detect tampering", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "source-control-bundle.json"));
  assert.equal(validateContentSha256(fixture.configuration, "configuration_content_sha256"), true);
  assert.equal(validateContentSha256(fixture.start_plan, "start_plan_content_sha256"), true);

  const reordered = Object.fromEntries(Object.entries(fixture.configuration).reverse());
  assert.equal(
    computeContentSha256(reordered, "configuration_content_sha256"),
    fixture.configuration.configuration_content_sha256,
    "member order must not change canonical content identity"
  );

  const tampered = structuredClone(fixture.configuration);
  tampered.maximum_duration_ns = "60000000001";
  assert.throws(
    () => validateContentSha256(tampered, "configuration_content_sha256"),
    (error) => error instanceof ContractConformanceError && error.code === "CONTENT_HASH_MISMATCH"
  );
});

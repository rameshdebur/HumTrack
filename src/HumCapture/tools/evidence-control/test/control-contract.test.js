import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  ContractConformanceError,
  validateCompletionBundle,
  validateProtocolSnapshot,
  validateSessionTransition
} from "../src/control-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "control", "v1");
const fixtureRoot = path.join(toolRoot, "fixtures", "control");

async function json(filePath) {
  return JSON.parse(await readFile(filePath, "utf8"));
}

async function validators() {
  const ajv = new Ajv2020({ allErrors: true, strict: true });
  addFormats(ajv);
  const files = (await readdir(schemaRoot)).filter((name) => name.endsWith(".schema.json")).sort();
  for (const name of files) ajv.addSchema(await json(path.join(schemaRoot, name)));
  return { ajv, files };
}

function resolveTransitions(fixture) {
  return fixture.transitions.map((item, index, all) => ({
    ...item,
    current: item.current ?? all[item.current_ref].next,
    index
  }));
}

function pointer(root, value) {
  if (value === "") return root;
  return value.split("/").slice(1).reduce((item, token) => item[token.replaceAll("~1", "/").replaceAll("~0", "~")], root);
}

function mutate(root, operation) {
  const tokens = operation.path.split("/").slice(1).map((token) => token.replaceAll("~1", "/").replaceAll("~0", "~"));
  const key = tokens.pop();
  const parent = tokens.reduce((item, token) => item[token], root);
  if (operation.op === "remove") delete parent[key];
  else if (operation.op === "set") parent[key] = operation.value;
  else throw new Error(`Unsupported fixture mutation: ${operation.op}`);
}

function baseState(state, revision = 1) {
  return {
    schema_version: "1.0.0",
    session_id: "50000000-0000-4000-8000-000000000001",
    subject_id: "60000000-0000-4000-8000-000000000001",
    operator_windows_account: "TEST\\operator",
    state,
    revision,
    protocol_snapshot_id: "20000000-0000-4000-8000-000000000001",
    protocol_snapshot_revision: 1,
    protocol_snapshot_content_sha256: "2".repeat(64),
    has_acquired_master_sample: false,
    slot_progress: [],
    active_trial_ids: [],
    unresolved_required_work: true,
    updated_utc: "2026-09-05T10:00:00.000Z"
  };
}

function baseCommand(commandType, revision = 1) {
  return {
    contract_version: "1.0.0",
    message_id: "70000000-0000-4000-8000-000000000001",
    command_id: "80000000-0000-4000-8000-000000000001",
    session_id: "50000000-0000-4000-8000-000000000001",
    expected_session_revision: revision,
    command_type: commandType,
    issued_utc: "2026-09-05T10:00:00.000Z",
    operator_windows_account: "TEST\\operator",
    payload: {}
  };
}

test("HC-CTRL-TEST-001 all control schemas compile as JSON Schema 2020-12", async () => {
  const { files } = await validators();
  assert.deepEqual(files, [
    "command-acknowledgement.schema.json",
    "common.schema.json",
    "package-commit-receipt.schema.json",
    "package-custody-record.schema.json",
    "protocol-snapshot.schema.json",
    "quality-assessment.schema.json",
    "readiness-snapshot.schema.json",
    "session-command.schema.json",
    "session-completion-record.schema.json",
    "session-handoff-manifest.schema.json",
    "session-state-event.schema.json",
    "session-state.schema.json",
    "source-command-acknowledgement.schema.json",
    "source-command.schema.json",
    "source-configuration.schema.json",
    "source-state-event.schema.json",
    "source-state.schema.json",
    "start-plan.schema.json"
  ]);
});

test("HC-CTRL-TEST-002 AsyncAPI 3.1 document references every session message schema", async () => {
  const document = await json(path.join(humCaptureRoot, "docs", "interfaces", "asyncapi", "control-v1.asyncapi.json"));
  assert.equal(document.asyncapi, "3.1.0");
  assert.equal(document.info.version, "1.2.0");
  const references = ["sessionCommand", "commandAcknowledgement", "sessionStateEvent", "sessionStateSnapshot"]
    .map((name) => document.components.messages[name].payload.schema.$ref).sort();
  assert.deepEqual(references, [
    "../schemas/control/v1/command-acknowledgement.schema.json",
    "../schemas/control/v1/session-command.schema.json",
    "../schemas/control/v1/session-state-event.schema.json",
    "../schemas/control/v1/session-state.schema.json"
  ]);
  for (const reference of references) await readFile(path.resolve(humCaptureRoot, "docs", "interfaces", "asyncapi", reference));
});

test("HC-CTRL-TEST-003 lifecycle, plan revision, reopen, completion, and handoff validate", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "fixed-session-lifecycle.json"));
  const { ajv } = await validators();
  const schema = (name) => ajv.getSchema(`https://humtrack.invalid/humcapture/control/v1/${name}`);
  assert.equal(schema("protocol-snapshot.schema.json")(fixture.protocol_snapshot), true);
  validateProtocolSnapshot(fixture.protocol_snapshot);
  for (const item of resolveTransitions(fixture)) {
    assert.equal(schema("session-state.schema.json")(item.current), true, `current state ${item.index}`);
    assert.equal(schema("session-command.schema.json")(item.command), true, `command ${item.index}`);
    assert.equal(schema("session-state.schema.json")(item.next), true, `next state ${item.index}`);
    validateSessionTransition({ ...item, protocolSnapshot: fixture.protocol_snapshot });
  }
  assert.equal(schema("session-completion-record.schema.json")(fixture.completion_record), true);
  assert.equal(schema("session-handoff-manifest.schema.json")(fixture.handoff_manifest), true);
  const complete = fixture.transitions.at(-1).next;
  validateCompletionBundle(complete, fixture.completion_record, fixture.handoff_manifest, fixture.protocol_snapshot);

  const planned = fixture.transitions[0].next;
  const revisedProtocol = structuredClone(fixture.protocol_snapshot);
  revisedProtocol.protocol_snapshot_id = "20000000-0000-4000-8000-000000000002";
  revisedProtocol.snapshot_revision = 2;
  revisedProtocol.supersedes_protocol_snapshot_id = fixture.protocol_snapshot.protocol_snapshot_id;
  revisedProtocol.snapshot_content_sha256 = "4".repeat(64);
  revisedProtocol.source_count_policy = { mode: "FLEXIBLE", minimum: 1, maximum: 2 };
  validateProtocolSnapshot(revisedProtocol);
  const reviseCommand = {
    ...baseCommand("REVISE_SESSION_PLAN", planned.revision),
    payload: {
      protocol_snapshot_id: revisedProtocol.protocol_snapshot_id,
      protocol_snapshot_revision: revisedProtocol.snapshot_revision,
      protocol_snapshot_content_sha256: revisedProtocol.snapshot_content_sha256
    }
  };
  const revisedPlan = {
    ...structuredClone(planned),
    revision: planned.revision + 1,
    protocol_snapshot_id: revisedProtocol.protocol_snapshot_id,
    protocol_snapshot_revision: revisedProtocol.snapshot_revision,
    protocol_snapshot_content_sha256: revisedProtocol.snapshot_content_sha256,
    updated_utc: "2026-09-05T10:01:03.000Z"
  };
  assert.equal(schema("session-command.schema.json")(reviseCommand), true);
  assert.equal(schema("session-state.schema.json")(revisedPlan), true);
  validateSessionTransition({ current: planned, command: reviseCommand, next: revisedPlan, protocolSnapshot: revisedProtocol });

  const reopenCommand = {
    ...baseCommand("REOPEN_SESSION_FOR_REVIEW", complete.revision),
    payload: {
      reason: "Additional documented review required.",
      prior_completion_record_id: complete.completion_record_id,
      prior_handoff_manifest_id: complete.handoff_manifest_id
    }
  };
  const reopened = structuredClone(complete);
  reopened.state = "COMPLETION_REVIEW";
  reopened.revision += 1;
  reopened.prior_completion_record_ids = [complete.completion_record_id];
  reopened.updated_utc = "2026-09-05T10:06:00.000Z";
  delete reopened.completion_record_id;
  delete reopened.completion_record_revision;
  delete reopened.handoff_manifest_id;
  delete reopened.handoff_manifest_revision;
  assert.equal(schema("session-command.schema.json")(reopenCommand), true);
  assert.equal(schema("session-state.schema.json")(reopened), true);
  validateSessionTransition({ current: complete, command: reopenCommand, next: reopened, protocolSnapshot: fixture.protocol_snapshot });
});

test("HC-CTRL-TEST-004 every transition outside the accepted matrix fails closed", async () => {
  const matrix = await json(path.join(fixtureRoot, "session-transition-matrix.json"));
  const states = Object.keys(matrix);
  const commands = [
    "PLAN_SESSION", "REVISE_SESSION_PLAN", "BEGIN_SESSION", "ENTER_COMPLETION_REVIEW",
    "COMPLETE_SESSION", "CLOSE_SESSION_INCOMPLETE", "CANCEL_SESSION",
    "REOPEN_SESSION_FOR_REVIEW", "RECONCILE_SESSION"
  ];
  let forbidden = 0;
  for (const state of states) {
    for (const commandType of commands) {
      for (const nextState of states) {
        if (matrix[state]?.[commandType]?.includes(nextState)) continue;
        const current = baseState(state);
        const next = { ...baseState(nextState, 2) };
        assert.throws(
          () => validateSessionTransition({ current, command: baseCommand(commandType), next }),
          (error) => error instanceof ContractConformanceError && error.code === "SESSION_TRANSITION_FORBIDDEN"
        );
        forbidden += 1;
      }
    }
  }
  assert.equal(forbidden, 553);
});

test("HC-CTRL-TEST-005 invalid fixtures fail with their named rule or schema disposition", async () => {
  const base = await json(path.join(fixtureRoot, "valid", "fixed-session-lifecycle.json"));
  const cases = await json(path.join(fixtureRoot, "invalid", "cases.json"));
  const { ajv } = await validators();
  for (const item of cases) {
    const fixture = structuredClone(base);
    for (const operation of item.operations) mutate(fixture, operation);
    if (item.kind === "schema") {
      const validate = ajv.getSchema(`https://humtrack.invalid/humcapture/control/v1/${item.schema}`);
      assert.equal(validate(pointer(fixture, item.instance_path)), false, item.name);
      continue;
    }
    const operation = () => {
      if (item.kind === "protocol") return validateProtocolSnapshot(fixture.protocol_snapshot);
      if (item.kind === "bundle") return validateCompletionBundle(fixture.transitions.at(-1).next, fixture.completion_record, fixture.handoff_manifest, fixture.protocol_snapshot);
      const transition = resolveTransitions(fixture)[item.transition_index];
      return validateSessionTransition({ ...transition, protocolSnapshot: fixture.protocol_snapshot });
    };
    assert.throws(operation, (error) => error instanceof ContractConformanceError && error.code === item.expected_error_code, item.name);
  }
});

test("HC-CTRL-TEST-006 flexible source counts accept only the approved inclusive range", async () => {
  const fixture = await json(path.join(fixtureRoot, "valid", "fixed-session-lifecycle.json"));
  fixture.protocol_snapshot.source_count_policy = { mode: "FLEXIBLE", minimum: 1, maximum: 2 };
  validateProtocolSnapshot(fixture.protocol_snapshot);
  fixture.protocol_snapshot.selected_source_count = 3;
  assert.throws(() => validateProtocolSnapshot(fixture.protocol_snapshot), (error) => error.code === "SOURCE_COUNT_OUT_OF_RANGE");
  fixture.protocol_snapshot.source_count_policy = { mode: "FLEXIBLE", minimum: 3, maximum: 2 };
  assert.throws(() => validateProtocolSnapshot(fixture.protocol_snapshot), (error) => error.code === "SOURCE_COUNT_RANGE_INVALID");
});

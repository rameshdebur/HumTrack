import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { DatabaseSync } from "node:sqlite";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  classifyOperationReplay,
  destinationPath,
  isReceiptEligible,
  recordPath,
  stagingPath,
  validateCommittedEvidence,
  validateCurrentTransactionUpdate,
  validateDescriptorCompatibility,
  validateReconciliation,
  validateRecordIndexPath,
  validateRepositoryRelativePath,
  validateTransactionBinding,
  validateTransition
} from "../src/repository-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "repository", "v1");
const fixtureRoot = path.join(humCaptureRoot, "docs", "interfaces", "fixtures", "repository", "v1");
const ddlPath = path.join(humCaptureRoot, "docs", "interfaces", "sqlite", "repository-v1.sql");
const schemaNames = [
  "common.schema.json",
  "repository-commit-record.schema.json",
  "repository-descriptor.schema.json",
  "repository-package-catalog-entry.schema.json",
  "repository-reconciliation.schema.json",
  "repository-record-index-entry.schema.json",
  "repository-transaction.schema.json",
  "repository-transition.schema.json"
];
const supportedFeatures = new Set(["JOURNAL_HISTORY", "IMMUTABLE_RECORD_INDEX", "BOUNDED_RECONCILIATION"]);

async function json(file) { return JSON.parse(await readFile(file, "utf8")); }
function clone(value) { return structuredClone(value); }
function throwsCode(fn, code) { assert.throws(fn, (error) => error?.code === code); }
function validator(ajv, name) { return ajv.getSchema(`https://humtrack.invalid/humcapture/repository/v1/${name}`); }
function txAtState(base, state) {
  const value = clone(base);
  value.state = state;
  value.revision = { STAGED_VERIFIED: 1, COMMITTING: 2, MOVED: 3, CATALOGED: 4, COMMITTED: 5, RECOVERY_REQUIRED: 6, QUARANTINED: 7 }[state];
  if (state !== "COMMITTED") { value.commit_record_id = null; value.commit_record_content_sha256 = null; }
  if (state !== "QUARANTINED") value.quarantine_record_id = null;
  if (state === "QUARANTINED") value.quarantine_record_id = "50000000-0000-4000-8000-000000000001";
  return value;
}
function observation(transaction, authority, disposition) {
  const matched = disposition === "MATCH";
  const mismatched = disposition === "MISMATCH";
  return {
    authority,
    disposition,
    expected_package_id: transaction.package_id,
    observed_package_id: matched ? transaction.package_id : (mismatched ? "90000000-0000-4000-8000-000000000001" : null),
    expected_content_sha256: transaction.package_content_sha256,
    observed_content_sha256: matched ? transaction.package_content_sha256 : (mismatched ? "f".repeat(64) : null),
    expected_byte_length: transaction.package_byte_length,
    observed_byte_length: matched ? transaction.package_byte_length : (mismatched ? "1" : null),
    evidence_relative_paths: []
  };
}
function reconciliation(transaction, dispositions, action, resultState, automatic = true, blockingReason = null) {
  const reconciliationId = "60000000-0000-4000-8000-000000000001";
  return {
    schema_version: "1.0.0",
    reconciliation_id: reconciliationId,
    transaction_id: transaction.transaction_id,
    trigger: automatic ? "STARTUP" : "OPERATOR",
    started_utc: "2026-09-10T10:06:00.000Z",
    finished_utc: "2026-09-10T10:06:01.000Z",
    actor_kind: automatic ? "SYSTEM" : "OPERATOR",
    actor_windows_account: "TEST\\operator",
    observations: [
      observation(transaction, "JOURNAL", "MATCH"),
      observation(transaction, "STAGING_PACKAGE", dispositions.staging),
      observation(transaction, "DESTINATION_PACKAGE", dispositions.destination),
      observation(transaction, "CATALOG_LINKAGE", dispositions.catalog),
      observation(transaction, "VERIFICATION_RECORD", dispositions.verification),
      observation(transaction, "COMMIT_RECORD", dispositions.commit)
    ],
    prior_state: transaction.state,
    result_state: resultState,
    action_code: action,
    automatic,
    blocking_reason_codes: blockingReason ? [blockingReason] : [],
    explanation: automatic ? "Exact durable evidence permits the bounded action." : "Conflicting evidence requires trained-operator disposition.",
    result_transition_id: transaction.state === resultState ? null : "70000000-0000-4000-8000-000000000001"
  };
}
function insertRow(db, table, value) {
  const keys = Object.keys(value);
  const sql = `INSERT INTO ${table} (${keys.join(",")}) VALUES (${keys.map((key) => `@${key}`).join(",")})`;
  db.prepare(sql).run(value);
}

test("HC-REP-TEST-001 repository schemas compile and accept the complete valid lifecycle", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  const files = (await readdir(schemaRoot)).filter((name) => name.endsWith(".schema.json")).sort();
  assert.deepEqual(files, schemaNames);
  ajv.addSchema(await json(path.join(schemaRoot, "common.schema.json")));
  for (const name of files.slice(1)) ajv.addSchema(await json(path.join(schemaRoot, name)));
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const pairs = [
    ["repository-descriptor.schema.json", lifecycle.descriptor],
    ["repository-transaction.schema.json", lifecycle.transaction],
    ["repository-package-catalog-entry.schema.json", lifecycle.catalog_entry],
    ["repository-commit-record.schema.json", lifecycle.commit_record],
    ["repository-record-index-entry.schema.json", lifecycle.record_index_entry]
  ];
  for (const [name, value] of pairs) assert.equal(validator(ajv, name)(value), true, JSON.stringify(validator(ajv, name).errors));
  for (const transition of lifecycle.transitions) assert.equal(validator(ajv, "repository-transition.schema.json")(transition), true, JSON.stringify(validator(ajv, "repository-transition.schema.json").errors));
});

test("HC-REP-TEST-002 descriptor compatibility fails closed for unsupported required capability", async () => {
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  assert.equal(validateDescriptorCompatibility(lifecycle.descriptor, supportedFeatures), "MUTATION_ALLOWED");
  const major = clone(lifecycle.descriptor); major.interface_version = "2.0.0";
  assert.equal(validateDescriptorCompatibility(major, supportedFeatures), "READ_ONLY_UNSUPPORTED_MAJOR");
  const feature = clone(lifecycle.descriptor); feature.required_features.push("FUTURE_REQUIRED_FEATURE");
  assert.equal(validateDescriptorCompatibility(feature, supportedFeatures), "READ_ONLY_UNKNOWN_REQUIRED_FEATURE");
});

test("HC-REP-TEST-003 SQLite DDL executes and creates the controlled table and trigger inventory", async () => {
  const db = new DatabaseSync(":memory:");
  db.exec(await readFile(ddlPath, "utf8"));
  const tables = db.prepare("SELECT name FROM sqlite_schema WHERE type='table' ORDER BY name").all().map(({ name }) => name);
  assert.deepEqual(tables, ["repository_metadata", "repository_package_catalog", "repository_reconciliation_observations", "repository_reconciliations", "repository_record_index", "repository_transactions", "repository_transitions"]);
  const triggers = db.prepare("SELECT name FROM sqlite_schema WHERE type='trigger' ORDER BY name").all().map(({ name }) => name);
  assert.equal(triggers.length, 12);
  db.close();
});

test("HC-REP-TEST-004 SQLite constraints retain immutable current identity and append-only history", async () => {
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const db = new DatabaseSync(":memory:"); db.exec(await readFile(ddlPath, "utf8"));
  const d = lifecycle.descriptor;
  insertRow(db, "repository_metadata", { singleton_id: 1, schema_version: d.schema_version, repository_id: d.repository_id, interface_version: d.interface_version, namespace_version: d.namespace_version, catalog_schema_version: d.catalog_schema_version, created_utc: d.created_utc, created_by_windows_account: d.created_by_windows_account });
  const initial = txAtState(lifecycle.transaction, "STAGED_VERIFIED"); initial.last_reconciliation_id = null;
  insertRow(db, "repository_transactions", initial);
  insertRow(db, "repository_transitions", lifecycle.transitions[0]);
  db.exec("BEGIN");
  db.prepare("UPDATE repository_transactions SET state='COMMITTING', revision=2 WHERE transaction_id=?").run(initial.transaction_id);
  insertRow(db, "repository_transitions", lifecycle.transitions[1]);
  db.exec("COMMIT");
  assert.deepEqual({ ...db.prepare("SELECT state, revision FROM repository_transactions WHERE transaction_id=?").get(initial.transaction_id) }, { state: "COMMITTING", revision: 2 });
  db.exec("BEGIN");
  db.prepare("UPDATE repository_transactions SET state='MOVED', revision=3 WHERE transaction_id=?").run(initial.transaction_id);
  const conflicting = clone(lifecycle.transitions[2]); conflicting.operation_id = lifecycle.transitions[1].operation_id;
  assert.throws(() => insertRow(db, "repository_transitions", conflicting), /UNIQUE/);
  db.exec("ROLLBACK");
  assert.deepEqual({ ...db.prepare("SELECT state, revision FROM repository_transactions WHERE transaction_id=?").get(initial.transaction_id) }, { state: "COMMITTING", revision: 2 });
  assert.throws(() => db.prepare("UPDATE repository_transactions SET package_id=? WHERE transaction_id=?").run("90000000-0000-4000-8000-000000000001", initial.transaction_id), /immutable/);
  assert.throws(() => db.prepare("UPDATE repository_transitions SET reason=? WHERE transition_id=?").run("changed", lifecycle.transitions[0].transition_id), /append-only/);
  assert.throws(() => db.prepare("DELETE FROM repository_transitions WHERE transition_id=?").run(lifecycle.transitions[0].transition_id), /append-only/);
  assert.throws(() => db.prepare("INSERT INTO repository_transitions SELECT transition_id, schema_version, transaction_id, transition_sequence+10, from_state, to_state, operation_id, trigger, actor_kind, actor_windows_account, reconciliation_id, reason_code, reason, recorded_utc FROM repository_transitions WHERE transition_sequence=2").run(), /UNIQUE/);
  db.close();
});

test("HC-REP-TEST-005 canonical namespace binds transaction and immutable-record identities", async () => {
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  assert.equal(validateTransactionBinding(lifecycle.transaction), true);
  assert.equal(lifecycle.transaction.staging_relative_path, stagingPath(lifecycle.transaction));
  assert.equal(lifecycle.transaction.destination_relative_path, destinationPath(lifecycle.transaction));
  assert.equal(validateRecordIndexPath(lifecycle.record_index_entry), true);
  assert.equal(lifecycle.record_index_entry.record_relative_path, recordPath(lifecycle.record_index_entry));
  for (const unsafe of ["C:\\capture", "/capture", "../capture", "subjects/CON/file", "subjects\\x", "subjects/x. "]) throwsCode(() => validateRepositoryRelativePath(unsafe), "REPOSITORY_PATH_UNSAFE");
});

test("HC-REP-TEST-006 lifecycle is contiguous, no-skip, and terminal", async () => {
  const { transitions } = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const history = [];
  for (const transition of transitions) { assert.equal(validateTransition(history, transition), true); history.push(transition); }
  const skipped = clone(transitions[2]); skipped.transition_sequence = 2; skipped.from_state = "STAGED_VERIFIED"; skipped.to_state = "MOVED";
  throwsCode(() => validateTransition([transitions[0]], skipped), "TRANSITION_EDGE_FORBIDDEN");
  const afterCommit = clone(transitions[4]); afterCommit.transition_sequence = 6; afterCommit.from_state = "COMMITTED"; afterCommit.to_state = "RECOVERY_REQUIRED";
  throwsCode(() => validateTransition(transitions, afterCommit), "TERMINAL_STATE_TRANSITION");
});

test("HC-REP-TEST-007 exhaustive state pairs admit only normal boundaries and controlled recovery", () => {
  const states = ["STAGED_VERIFIED", "COMMITTING", "MOVED", "CATALOGED", "COMMITTED", "RECOVERY_REQUIRED", "QUARANTINED"];
  const allowed = new Set(["STAGED_VERIFIED>COMMITTING", "STAGED_VERIFIED>RECOVERY_REQUIRED", "COMMITTING>MOVED", "COMMITTING>RECOVERY_REQUIRED", "MOVED>CATALOGED", "MOVED>RECOVERY_REQUIRED", "CATALOGED>COMMITTED", "CATALOGED>RECOVERY_REQUIRED", "RECOVERY_REQUIRED>STAGED_VERIFIED", "RECOVERY_REQUIRED>MOVED", "RECOVERY_REQUIRED>CATALOGED", "RECOVERY_REQUIRED>COMMITTED", "RECOVERY_REQUIRED>QUARANTINED"]);
  for (const from of states) for (const to of states) {
    const prior = { transaction_id: "10000000-0000-4000-8000-000000000002", transition_sequence: 1, to_state: from };
    const next = { transaction_id: prior.transaction_id, transition_sequence: 2, from_state: from, to_state: to, reconciliation_id: from === "RECOVERY_REQUIRED" ? "60000000-0000-4000-8000-000000000001" : null };
    if (allowed.has(`${from}>${to}`)) assert.equal(validateTransition([prior], next), true);
    else assert.throws(() => validateTransition([prior], next));
  }
});

test("HC-REP-TEST-008 identity bindings are immutable and operation replay must be exact", async () => {
  const { transaction, transitions } = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const current = txAtState(transaction, "STAGED_VERIFIED"); const next = txAtState(transaction, "COMMITTING");
  assert.equal(validateCurrentTransactionUpdate(current, next, transitions[1]), true);
  const changed = clone(next); changed.package_content_sha256 = "f".repeat(64);
  throwsCode(() => validateCurrentTransactionUpdate(current, changed, transitions[1]), "TRANSACTION_IDENTITY_CHANGED");
  const replay = clone(transitions[1]); replay.transition_id = "90000000-0000-4000-8000-000000000002"; replay.recorded_utc = "2026-09-10T11:00:00.000Z";
  assert.equal(classifyOperationReplay(transitions[1], replay), "IDEMPOTENT");
  replay.to_state = "RECOVERY_REQUIRED";
  assert.equal(classifyOperationReplay(transitions[1], replay), "RECOVERY_REQUIRED_CONFLICT");
});

test("HC-REP-TEST-009 reconciliation requires six unique, transaction-bound observations", async () => {
  const { transaction } = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const current = txAtState(transaction, "COMMITTED");
  const value = reconciliation(current, { staging: "ABSENT", destination: "MATCH", catalog: "MATCH", verification: "MATCH", commit: "MATCH" }, "CONFIRM_IDEMPOTENT_COMMIT", "COMMITTED");
  assert.equal(validateReconciliation(current, value), true);
  const missing = clone(value); missing.observations.pop();
  throwsCode(() => validateReconciliation(current, missing), "RECONCILIATION_OBSERVATION_INCOMPLETE");
  const wrong = clone(value); wrong.observations[0].expected_package_id = "90000000-0000-4000-8000-000000000001";
  throwsCode(() => validateReconciliation(current, wrong), "OBSERVATION_EXPECTATION_MISMATCH");
});

test("HC-REP-TEST-010 crash fixtures allow only their bounded automatic or operator result", async () => {
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const cases = await json(path.join(fixtureRoot, "crash-reconciliation-cases.json"));
  for (const item of cases) {
    const current = txAtState(lifecycle.transaction, item.prior_state);
    const value = reconciliation(current, { staging: item.staging, destination: item.destination, catalog: item.catalog, verification: item.verification, commit: item.commit }, item.action, item.result_state, item.automatic, item.blocking_reason);
    assert.equal(validateReconciliation(current, value), true, item.case_id);
  }
});

test("HC-REP-TEST-011 automatic conflict and operator force-commit are rejected", async () => {
  const { transaction } = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const current = txAtState(transaction, "RECOVERY_REQUIRED");
  const dual = reconciliation(current, { staging: "MATCH", destination: "MATCH", catalog: "ABSENT", verification: "MATCH", commit: "ABSENT" }, "NO_ACTION", "RECOVERY_REQUIRED");
  throwsCode(() => validateReconciliation(current, dual), "AUTOMATIC_DUAL_PATH_CONFLICT");
  const absent = reconciliation(current, { staging: "ABSENT", destination: "ABSENT", catalog: "ABSENT", verification: "MATCH", commit: "ABSENT" }, "NO_ACTION", "RECOVERY_REQUIRED");
  throwsCode(() => validateReconciliation(current, absent), "AUTOMATIC_ACTION_PREDICATE_FAILED");
  const force = reconciliation(current, { staging: "ABSENT", destination: "MISMATCH", catalog: "MISMATCH", verification: "MATCH", commit: "ABSENT" }, "RETAIN_FOR_INVESTIGATION", "COMMITTED", false, "IDENTITY_CONFLICT");
  throwsCode(() => validateReconciliation(current, force), "OPERATOR_FORCE_COMMIT_FORBIDDEN");
});

test("HC-REP-TEST-012 receipt requires a post-operation exact COMMITTED reconciliation", async () => {
  const { transaction } = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const current = txAtState(transaction, "COMMITTED");
  const exact = reconciliation(current, { staging: "ABSENT", destination: "MATCH", catalog: "MATCH", verification: "MATCH", commit: "MATCH" }, "CONFIRM_IDEMPOTENT_COMMIT", "COMMITTED");
  current.last_reconciliation_id = exact.reconciliation_id;
  assert.equal(isReceiptEligible(current, exact), true);
  const stale = clone(exact); stale.reconciliation_id = "90000000-0000-4000-8000-000000000001";
  assert.equal(isReceiptEligible(current, stale), false);
  const missingCommit = clone(exact); missingCommit.observations.find(({ authority }) => authority === "COMMIT_RECORD").disposition = "ABSENT";
  assert.equal(isReceiptEligible(current, missingCommit), false);
  assert.equal(isReceiptEligible(txAtState(transaction, "CATALOGED"), exact), false);
});

test("HC-REP-TEST-013 invalid fixtures exercise schema, path, and semantic rejection", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  for (const name of schemaNames) ajv.addSchema(await json(path.join(schemaRoot, name)));
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  const absolute = await json(path.join(fixtureRoot, "invalid-absolute-path.json"));
  const badTransaction = clone(lifecycle.transaction); badTransaction[absolute.mutation] = absolute.invalid_value;
  assert.equal(validator(ajv, "repository-transaction.schema.json")(badTransaction), false);
  const missingFixture = await json(path.join(fixtureRoot, "invalid-observation-set.json"));
  const current = txAtState(lifecycle.transaction, "COMMITTED");
  const missing = reconciliation(current, { staging: "ABSENT", destination: "MATCH", catalog: "MATCH", verification: "MATCH", commit: "MATCH" }, "CONFIRM_IDEMPOTENT_COMMIT", "COMMITTED");
  missing.observations = missing.observations.filter(({ authority }) => authority !== missingFixture.invalid_value);
  assert.equal(validator(ajv, "repository-reconciliation.schema.json")(missing), false);
  const conflictFixture = await json(path.join(fixtureRoot, "invalid-automatic-conflict.json"));
  const conflict = reconciliation(txAtState(lifecycle.transaction, "RECOVERY_REQUIRED"), { staging: conflictFixture.staging_disposition, destination: conflictFixture.destination_disposition, catalog: "ABSENT", verification: "MATCH", commit: "ABSENT" }, "NO_ACTION", "RECOVERY_REQUIRED");
  assert.throws(() => validateReconciliation(txAtState(lifecycle.transaction, "RECOVERY_REQUIRED"), conflict));
});

test("HC-REP-TEST-014 repository records minimize PII, secret, raw-media, and absolute-path surfaces", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  for (const name of schemaNames) ajv.addSchema(await json(path.join(schemaRoot, name)));
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  for (const [schema, field, value] of [
    ["repository-descriptor.schema.json", "subject_name", "Test Subject"],
    ["repository-transaction.schema.json", "demographics", { age: 55 }],
    ["repository-transition.schema.json", "authorization", "Bearer token"],
    ["repository-reconciliation.schema.json", "raw_media", "capture.mp4"]
  ]) {
    const source = schema.includes("descriptor") ? lifecycle.descriptor : schema.includes("transaction") ? lifecycle.transaction : schema.includes("transition") ? lifecycle.transitions[0] : reconciliation(txAtState(lifecycle.transaction, "COMMITTED"), { staging: "ABSENT", destination: "MATCH", catalog: "MATCH", verification: "MATCH", commit: "MATCH" }, "CONFIRM_IDEMPOTENT_COMMIT", "COMMITTED");
    const invalid = clone(source); invalid[field] = value;
    assert.equal(validator(ajv, schema)(invalid), false, field);
  }
  const secretReason = clone(lifecycle.transitions[1]); secretReason.reason_code = "DIAGNOSTIC"; secretReason.reason = `${"authori"}zation: bearer concealed-value`;
  assert.equal(validator(ajv, "repository-transition.schema.json")(secretReason), false);
});

test("HC-REP-TEST-015 catalog, commit record, and immutable index agree exactly", async () => {
  const lifecycle = await json(path.join(fixtureRoot, "valid-lifecycle.json"));
  assert.equal(validateCommittedEvidence(lifecycle.transaction, lifecycle.catalog_entry, lifecycle.commit_record, lifecycle.record_index_entry), true);
  for (const [recordName, field] of [["catalog_entry", "package_content_sha256"], ["commit_record", "catalog_revision"], ["record_index_entry", "record_content_sha256"]]) {
    const changed = clone(lifecycle[recordName]);
    changed[field] = field === "catalog_revision" ? 2 : "f".repeat(64);
    const args = [lifecycle.transaction, lifecycle.catalog_entry, lifecycle.commit_record, lifecycle.record_index_entry];
    args[{ catalog_entry: 1, commit_record: 2, record_index_entry: 3 }[recordName]] = changed;
    throwsCode(() => validateCommittedEvidence(...args), "COMMITTED_EVIDENCE_MISMATCH");
  }
});

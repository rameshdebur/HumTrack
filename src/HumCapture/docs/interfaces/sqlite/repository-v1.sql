PRAGMA foreign_keys = ON;

CREATE TABLE repository_metadata (
  singleton_id INTEGER PRIMARY KEY CHECK (singleton_id = 1),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  repository_id TEXT NOT NULL UNIQUE CHECK (length(repository_id) = 36 AND repository_id = lower(repository_id)),
  interface_version TEXT NOT NULL CHECK (interface_version = '1.3.0'),
  namespace_version TEXT NOT NULL CHECK (namespace_version = '1.0.0'),
  catalog_schema_version TEXT NOT NULL CHECK (catalog_schema_version = '1.0.0'),
  created_utc TEXT NOT NULL,
  created_by_windows_account TEXT NOT NULL CHECK (length(created_by_windows_account) BETWEEN 1 AND 256)
) STRICT;

CREATE TABLE repository_transactions (
  transaction_id TEXT PRIMARY KEY CHECK (length(transaction_id) = 36 AND transaction_id = lower(transaction_id)),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  repository_id TEXT NOT NULL REFERENCES repository_metadata(repository_id),
  revision INTEGER NOT NULL CHECK (revision >= 1),
  state TEXT NOT NULL CHECK (state IN ('STAGED_VERIFIED','COMMITTING','MOVED','CATALOGED','COMMITTED','RECOVERY_REQUIRED','QUARANTINED')),
  subject_id TEXT NOT NULL CHECK (length(subject_id) = 36 AND subject_id = lower(subject_id)),
  session_id TEXT NOT NULL CHECK (length(session_id) = 36 AND session_id = lower(session_id)),
  trial_id TEXT NOT NULL CHECK (length(trial_id) = 36 AND trial_id = lower(trial_id)),
  source_id TEXT NOT NULL CHECK (length(source_id) = 36 AND source_id = lower(source_id)),
  capture_attempt_id TEXT NOT NULL CHECK (length(capture_attempt_id) = 36 AND capture_attempt_id = lower(capture_attempt_id)),
  collection_attempt_id TEXT NOT NULL CHECK (length(collection_attempt_id) = 36 AND collection_attempt_id = lower(collection_attempt_id)),
  package_id TEXT NOT NULL CHECK (length(package_id) = 36 AND package_id = lower(package_id)),
  package_content_sha256 TEXT NOT NULL CHECK (length(package_content_sha256) = 64 AND package_content_sha256 = lower(package_content_sha256)),
  artifact_set_sha256 TEXT NOT NULL CHECK (length(artifact_set_sha256) = 64 AND artifact_set_sha256 = lower(artifact_set_sha256)),
  verification_record_id TEXT NOT NULL CHECK (length(verification_record_id) = 36 AND verification_record_id = lower(verification_record_id)),
  verification_record_content_sha256 TEXT NOT NULL CHECK (length(verification_record_content_sha256) = 64 AND verification_record_content_sha256 = lower(verification_record_content_sha256)),
  staging_relative_path TEXT NOT NULL CHECK (length(staging_relative_path) BETWEEN 1 AND 512 AND substr(staging_relative_path,1,1) <> '/' AND instr(staging_relative_path,'\\') = 0 AND instr(staging_relative_path,':') = 0 AND instr('/' || staging_relative_path || '/','/../') = 0),
  destination_relative_path TEXT NOT NULL CHECK (length(destination_relative_path) BETWEEN 1 AND 512 AND substr(destination_relative_path,1,1) <> '/' AND instr(destination_relative_path,'\\') = 0 AND instr(destination_relative_path,':') = 0 AND instr('/' || destination_relative_path || '/','/../') = 0),
  package_byte_length TEXT NOT NULL CHECK (length(package_byte_length) BETWEEN 1 AND 20 AND package_byte_length NOT GLOB '*[^0-9]*' AND (package_byte_length = '0' OR substr(package_byte_length,1,1) <> '0')),
  artifact_count INTEGER NOT NULL CHECK (artifact_count >= 1),
  commit_record_id TEXT,
  commit_record_content_sha256 TEXT,
  quarantine_record_id TEXT,
  last_reconciliation_id TEXT,
  created_utc TEXT NOT NULL,
  state_changed_utc TEXT NOT NULL,
  UNIQUE (repository_id, package_id, package_content_sha256),
  CHECK ((commit_record_id IS NULL) = (commit_record_content_sha256 IS NULL)),
  CHECK (commit_record_id IS NULL OR (length(commit_record_id) = 36 AND commit_record_id = lower(commit_record_id))),
  CHECK (commit_record_content_sha256 IS NULL OR (length(commit_record_content_sha256) = 64 AND commit_record_content_sha256 = lower(commit_record_content_sha256))),
  CHECK (quarantine_record_id IS NULL OR (length(quarantine_record_id) = 36 AND quarantine_record_id = lower(quarantine_record_id))),
  CHECK (last_reconciliation_id IS NULL OR (length(last_reconciliation_id) = 36 AND last_reconciliation_id = lower(last_reconciliation_id))),
  CHECK (state <> 'COMMITTED' OR commit_record_id IS NOT NULL),
  CHECK (state <> 'QUARANTINED' OR quarantine_record_id IS NOT NULL)
) STRICT;

CREATE TABLE repository_transitions (
  transition_id TEXT PRIMARY KEY CHECK (length(transition_id) = 36 AND transition_id = lower(transition_id)),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  transaction_id TEXT NOT NULL REFERENCES repository_transactions(transaction_id),
  transition_sequence INTEGER NOT NULL CHECK (transition_sequence >= 1),
  from_state TEXT CHECK (from_state IS NULL OR from_state IN ('STAGED_VERIFIED','COMMITTING','MOVED','CATALOGED','COMMITTED','RECOVERY_REQUIRED','QUARANTINED')),
  to_state TEXT NOT NULL CHECK (to_state IN ('STAGED_VERIFIED','COMMITTING','MOVED','CATALOGED','COMMITTED','RECOVERY_REQUIRED','QUARANTINED')),
  operation_id TEXT NOT NULL CHECK (length(operation_id) = 36 AND operation_id = lower(operation_id)),
  trigger TEXT NOT NULL CHECK (trigger IN ('NORMAL','STARTUP','RETRY','PRE_RECEIPT','OPERATOR')),
  actor_kind TEXT NOT NULL CHECK (actor_kind IN ('SYSTEM','OPERATOR')),
  actor_windows_account TEXT NOT NULL CHECK (length(actor_windows_account) BETWEEN 1 AND 256),
  reconciliation_id TEXT,
  reason_code TEXT,
  reason TEXT CHECK (reason IS NULL OR length(reason) BETWEEN 1 AND 512),
  recorded_utc TEXT NOT NULL,
  UNIQUE (transaction_id, transition_sequence),
  UNIQUE (transaction_id, operation_id),
  CHECK ((transition_sequence = 1 AND from_state IS NULL AND to_state = 'STAGED_VERIFIED') OR (transition_sequence > 1 AND from_state IS NOT NULL)),
  CHECK (trigger <> 'OPERATOR' OR actor_kind = 'OPERATOR')
) STRICT;

CREATE TABLE repository_reconciliations (
  reconciliation_id TEXT PRIMARY KEY CHECK (length(reconciliation_id) = 36 AND reconciliation_id = lower(reconciliation_id)),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  transaction_id TEXT NOT NULL REFERENCES repository_transactions(transaction_id),
  trigger TEXT NOT NULL CHECK (trigger IN ('NORMAL','STARTUP','RETRY','PRE_RECEIPT','OPERATOR')),
  started_utc TEXT NOT NULL,
  finished_utc TEXT NOT NULL,
  actor_kind TEXT NOT NULL CHECK (actor_kind IN ('SYSTEM','OPERATOR')),
  actor_windows_account TEXT NOT NULL CHECK (length(actor_windows_account) BETWEEN 1 AND 256),
  prior_state TEXT NOT NULL CHECK (prior_state IN ('STAGED_VERIFIED','COMMITTING','MOVED','CATALOGED','COMMITTED','RECOVERY_REQUIRED','QUARANTINED')),
  result_state TEXT NOT NULL CHECK (result_state IN ('STAGED_VERIFIED','COMMITTING','MOVED','CATALOGED','COMMITTED','RECOVERY_REQUIRED','QUARANTINED')),
  action_code TEXT NOT NULL CHECK (action_code IN ('NO_ACTION','RETRY_FROM_STAGED','RESUME_AFTER_MOVE','COMPLETE_CATALOGING','FINALIZE_COMMIT','CONFIRM_IDEMPOTENT_COMMIT','RETRY_RECONCILIATION','QUARANTINE_CONFLICT','RETAIN_FOR_INVESTIGATION','EXPORT_DIAGNOSTICS')),
  automatic INTEGER NOT NULL CHECK (automatic IN (0,1)),
  blocking_reason_codes_json TEXT NOT NULL CHECK (json_valid(blocking_reason_codes_json)),
  explanation TEXT NOT NULL CHECK (length(explanation) BETWEEN 1 AND 512),
  result_transition_id TEXT,
  CHECK ((automatic = 1 AND actor_kind = 'SYSTEM' AND action_code IN ('NO_ACTION','RETRY_FROM_STAGED','RESUME_AFTER_MOVE','COMPLETE_CATALOGING','FINALIZE_COMMIT','CONFIRM_IDEMPOTENT_COMMIT')) OR (automatic = 0 AND actor_kind = 'OPERATOR' AND action_code IN ('RETRY_RECONCILIATION','QUARANTINE_CONFLICT','RETAIN_FOR_INVESTIGATION','EXPORT_DIAGNOSTICS')))
) STRICT;

CREATE TABLE repository_reconciliation_observations (
  reconciliation_id TEXT NOT NULL REFERENCES repository_reconciliations(reconciliation_id),
  authority TEXT NOT NULL CHECK (authority IN ('JOURNAL','STAGING_PACKAGE','DESTINATION_PACKAGE','CATALOG_LINKAGE','VERIFICATION_RECORD','COMMIT_RECORD')),
  disposition TEXT NOT NULL CHECK (disposition IN ('ABSENT','MATCH','MISMATCH','UNREADABLE','UNSAFE','NOT_APPLICABLE')),
  expected_package_id TEXT,
  observed_package_id TEXT,
  expected_content_sha256 TEXT,
  observed_content_sha256 TEXT,
  expected_byte_length TEXT,
  observed_byte_length TEXT,
  evidence_relative_paths_json TEXT NOT NULL CHECK (json_valid(evidence_relative_paths_json)),
  PRIMARY KEY (reconciliation_id, authority)
) STRICT;

CREATE TABLE repository_package_catalog (
  catalog_entry_id TEXT PRIMARY KEY CHECK (length(catalog_entry_id) = 36 AND catalog_entry_id = lower(catalog_entry_id)),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  repository_id TEXT NOT NULL REFERENCES repository_metadata(repository_id),
  transaction_id TEXT NOT NULL UNIQUE REFERENCES repository_transactions(transaction_id),
  subject_id TEXT NOT NULL,
  session_id TEXT NOT NULL,
  trial_id TEXT NOT NULL,
  source_id TEXT NOT NULL,
  capture_attempt_id TEXT NOT NULL,
  collection_attempt_id TEXT NOT NULL,
  package_id TEXT NOT NULL,
  package_content_sha256 TEXT NOT NULL,
  artifact_set_sha256 TEXT NOT NULL,
  package_byte_length TEXT NOT NULL,
  artifact_count INTEGER NOT NULL CHECK (artifact_count >= 1),
  package_relative_path TEXT NOT NULL UNIQUE,
  verification_record_id TEXT NOT NULL,
  verification_record_content_sha256 TEXT NOT NULL,
  catalog_revision INTEGER NOT NULL CHECK (catalog_revision >= 1),
  cataloged_utc TEXT NOT NULL,
  UNIQUE (repository_id, package_id)
) STRICT;

CREATE TABLE repository_record_index (
  index_entry_id TEXT PRIMARY KEY CHECK (length(index_entry_id) = 36 AND index_entry_id = lower(index_entry_id)),
  schema_version TEXT NOT NULL CHECK (schema_version = '1.0.0'),
  repository_id TEXT NOT NULL REFERENCES repository_metadata(repository_id),
  record_kind TEXT NOT NULL CHECK (record_kind IN ('protocol-snapshots','verifications','commits','receipts','quality-assessments','completions','handoffs')),
  record_id TEXT NOT NULL,
  record_revision INTEGER NOT NULL CHECK (record_revision >= 1),
  record_content_sha256 TEXT NOT NULL CHECK (length(record_content_sha256) = 64 AND record_content_sha256 = lower(record_content_sha256)),
  subject_id TEXT NOT NULL,
  session_id TEXT NOT NULL,
  package_id TEXT,
  record_relative_path TEXT NOT NULL UNIQUE,
  indexed_utc TEXT NOT NULL,
  UNIQUE (repository_id, record_kind, record_id, record_revision)
) STRICT;

CREATE TRIGGER repository_transaction_identity_immutable
BEFORE UPDATE OF repository_id, subject_id, session_id, trial_id, source_id,
  capture_attempt_id, collection_attempt_id, package_id, package_content_sha256,
  artifact_set_sha256, verification_record_id,
  verification_record_content_sha256, staging_relative_path,
  destination_relative_path, package_byte_length, artifact_count
ON repository_transactions
BEGIN
  SELECT RAISE(ABORT, 'repository transaction identity is immutable');
END;

CREATE TRIGGER repository_transaction_no_delete
BEFORE DELETE ON repository_transactions
BEGIN
  SELECT RAISE(ABORT, 'repository transaction deletion is prohibited');
END;

CREATE TRIGGER repository_transition_no_update
BEFORE UPDATE ON repository_transitions
BEGIN
  SELECT RAISE(ABORT, 'repository transition history is append-only');
END;

CREATE TRIGGER repository_transition_no_delete
BEFORE DELETE ON repository_transitions
BEGIN
  SELECT RAISE(ABORT, 'repository transition history is append-only');
END;

CREATE TRIGGER repository_reconciliation_no_update
BEFORE UPDATE ON repository_reconciliations
BEGIN
  SELECT RAISE(ABORT, 'repository reconciliation history is append-only');
END;

CREATE TRIGGER repository_reconciliation_no_delete
BEFORE DELETE ON repository_reconciliations
BEGIN
  SELECT RAISE(ABORT, 'repository reconciliation history is append-only');
END;

CREATE TRIGGER repository_observation_no_update
BEFORE UPDATE ON repository_reconciliation_observations
BEGIN
  SELECT RAISE(ABORT, 'repository observations are append-only');
END;

CREATE TRIGGER repository_observation_no_delete
BEFORE DELETE ON repository_reconciliation_observations
BEGIN
  SELECT RAISE(ABORT, 'repository observations are append-only');
END;

CREATE TRIGGER repository_catalog_no_update
BEFORE UPDATE ON repository_package_catalog
BEGIN
  SELECT RAISE(ABORT, 'repository package catalog entries are immutable');
END;

CREATE TRIGGER repository_catalog_no_delete
BEFORE DELETE ON repository_package_catalog
BEGIN
  SELECT RAISE(ABORT, 'repository package catalog entries are immutable');
END;

CREATE TRIGGER repository_record_index_no_update
BEFORE UPDATE ON repository_record_index
BEGIN
  SELECT RAISE(ABORT, 'repository record index entries are immutable');
END;

CREATE TRIGGER repository_record_index_no_delete
BEFORE DELETE ON repository_record_index
BEGIN
  SELECT RAISE(ABORT, 'repository record index entries are immutable');
END;

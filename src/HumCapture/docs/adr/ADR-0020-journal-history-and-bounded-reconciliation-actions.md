# ADR-0020 — Journal history and bounded reconciliation actions

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0019 fixes the internal repository transaction states. B3B must now define
what the SQLite journal retains and when recovery may continue automatically.
A single mutable state row is useful for Coordinator queries but cannot, by
itself, show how a transaction reached that state. Conversely, requiring the
operator to approve every idempotent restart would add avoidable burden to the
trained-operator MVP.

Reconciliation must distinguish expected absence from mismatch, record observed
facts separately from the resulting action, and avoid treating UTC time or the
newest-looking record as authority. It must not create another mutable JSON
authority or add a new filesystem milestone kind to ADR-0018.

## Decision

The SQLite journal shall contain one current transaction row and append-only
transition and reconciliation rows. Updates to current state and insertion of
the corresponding transition row occur in one SQLite transaction.

### Current transaction row

The current row contains these logical fields:

- `schema_version`, `transaction_id`, `repository_id`, `revision`, and `state`;
- `subject_id`, `session_id`, `trial_id`, `source_id`, `capture_attempt_id`,
  `collection_attempt_id`, and `package_id`;
- `package_content_sha256`, `artifact_set_sha256`,
  `verification_record_id`, and `verification_record_content_sha256`;
- canonical `staging_relative_path` and `destination_relative_path`;
- `package_byte_length` as an unsigned decimal string and integer
  `artifact_count`;
- nullable `commit_record_id`, `commit_record_content_sha256`,
  `quarantine_record_id`, and `last_reconciliation_id`; and
- `created_utc` and `state_changed_utc` for audit/display only.

Identity fields and expected hashes are immutable after the initial
`STAGED_VERIFIED` row. A change requires a new transaction identity. Revision
and the append-only transition sequence, not UTC ordering, establish journal
order.

### Append-only transition row

Every attempted or completed state change records:

- `schema_version`, `transition_id`, `transaction_id`, and positive
  `transition_sequence`;
- `from_state`, `to_state`, and idempotency `operation_id`;
- `trigger`: `NORMAL`, `STARTUP`, `RETRY`, `PRE_RECEIPT`, or `OPERATOR`;
- `actor_kind`: `SYSTEM` or `OPERATOR`, plus the signed-in
  `actor_windows_account`;
- nullable `reconciliation_id`, controlled `reason_code`, and bounded
  human-readable `reason`; and
- `recorded_utc` for audit/display only.

The first transition has no `from_state`. Reusing an `operation_id` is
idempotent only when the complete requested transition content is identical;
conflicting reuse enters recovery.

### Reconciliation row

Each reconciliation records:

- `schema_version`, `reconciliation_id`, `transaction_id`, `trigger`,
  `started_utc`, `finished_utc`, `actor_kind`, and signed-in Windows account;
- one observation for each required authority: `JOURNAL`, `STAGING_PACKAGE`,
  `DESTINATION_PACKAGE`, `CATALOG_LINKAGE`, `VERIFICATION_RECORD`, and
  `COMMIT_RECORD`;
- per observation: disposition `ABSENT`, `MATCH`, `MISMATCH`, `UNREADABLE`,
  `UNSAFE`, or `NOT_APPLICABLE`, plus expected and observed identity/hash/length
  values when available and repository-relative evidence references;
- `prior_state`, `result_state`, controlled `action_code`, boolean
  `automatic`, blocking reason codes, and bounded explanatory text; and
- the transition identity produced by an action, when an action changes state.

Observations are retained even when no state change occurs. Subject name,
demographics, secrets, raw media, and absolute paths are prohibited from these
journal records.

### Automatic recovery policy

The Coordinator may perform and record only these automatic actions:

- `NO_ACTION`: retain the directly proven state;
- `RETRY_FROM_STAGED`: exact verified staging package exists and destination is
  absent;
- `RESUME_AFTER_MOVE`: exact destination exists, staging is absent, and durable
  intent plus verification agree;
- `COMPLETE_CATALOGING`: the exact moved package, journal intent, and immutable
  verification evidence agree and no conflicting catalog binding exists;
- `FINALIZE_COMMIT`: cataloged package and every required immutable/indexed
  record agree; and
- `CONFIRM_IDEMPOTENT_COMMIT`: the already committed identity and all evidence
  match exactly.

Automatic action requires supported schemas/features, canonical safe paths,
exact identity/hash agreement, expected absence where required, no conflicting
duplicate, and no action that deletes, overwrites, or discards the only verified
copy. It remains visible in audit and recovery history; it is not silent repair.

### Operator-controlled policy

Operator action is required for hash/identity conflict, both staging and
destination containing material, missing/corrupt immutable verification or
commit evidence, unsafe links/paths/unexpected files, unsupported versions,
conflicting catalog bindings, or uncertain durability.

The allowed operator actions are `RETRY_RECONCILIATION`,
`QUARANTINE_CONFLICT`, `RETAIN_FOR_INVESTIGATION`, and `EXPORT_DIAGNOSTICS`.
Quarantine preserves material under a new quarantine-record UUID and records
what moved. None of these actions may force `COMMITTED`, overwrite evidence,
delete the only verified copy, manufacture a missing immutable record, or
rewrite source capture/finalization facts.

Exact JSON Schema and SQLite DDL representation is B3C work. This decision does
not authorize application implementation.

## Alternatives considered

- **Only a mutable transaction row:** rejected because state-transition and
  recovery history could be lost through replacement.
- **Put journal history in JSON/JSONL files:** rejected because it would create
  another operational authority and complicate interrupted append recovery.
- **Require operator approval for every restart:** rejected because exact,
  non-destructive idempotent continuation is deterministic and can be audited.
- **Automatically repair every disagreement:** rejected because conflicts and
  missing immutable evidence require accountable operator disposition.
- **Use timestamps to choose the newest authority:** rejected because UTC order
  does not prove identity, content, durability, or valid state transition.
- **Offer a force-commit control:** rejected because it bypasses the accepted
  acquisition-completion and cleanup safety boundary.

## Rationale

The current row keeps normal queries simple, append-only rows preserve how the
state was reached, and bounded automatic actions recover common crash points
without adding routine operator work. Exact observations make disagreements
reviewable and keep all commit claims tied to durable evidence.

## Consequences

- B3C schemas must reject mutable identities, transition gaps, conflicting
  operation replay, unbound observations, unsafe paths, and prohibited data.
- Pre-receipt reconciliation is explicit even when no recovery was needed.
- Automatic continuation is permitted only for the enumerated actions and exact
  evidence predicates; all other conditions become actionable recovery.
- Backup/restore must preserve current, transition, and reconciliation rows as
  one catalog set.
- Runtime, crash/power-loss, HIL, field, independent, and regulatory evidence
  remain separate.

## Affected components and interfaces

Repository/SQLite journal, common verifier, package custody, immutable
verification/commit records, receipt/cleanup, Coordinator recovery UI, audit,
backup/export, simulator/QA, risk and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0009, ADR-0014, ADR-0016, ADR-0018, and ADR-0019; supersedes none.

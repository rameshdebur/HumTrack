# ADR-0018 — Split operational and immutable record authority

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0016 establishes SQLite plus immutable filesystem packages and ADR-0017
fixes the shallow UUID namespace. The repository still needs a clear answer for
subject/session records: mirroring all mutable state into JSON creates two
competing authorities, while keeping every milestone only in SQLite weakens
portable verification and catalog-recovery evidence.

Subject name and demographics are required and mutable Coordinator-local PII.
Protocol snapshots, package verifications, commits, receipts, quality
assessments, completion records, and handoff manifests are immutable or
revisioned milestones already identified by UUID/content hash. These record
classes have different authority and recovery needs.

## Decision

SQLite is the sole current operational authority for:

- subject name, code, demographics, and their revision/audit history;
- protocol drafts and current protocol registry state;
- current session, trial, source, device, transfer, custody, receipt, cleanup,
  and recovery state;
- transfer checkpoints and the repository transaction journal; and
- append-oriented Coordinator audit events.

Mutable current state shall not be mirrored into replaceable JSON files.
Subject PII remains in the Coordinator catalog for MVP and shall not appear in
capture packages, repository paths, `repository.json`, or default handoff.

The filesystem is authoritative for scientific package bytes and immutable
milestone records. HC-IF-REP-001 version 1.1.0 adds:

```text
subjects/{subject_id}/sessions/{session_id}/records/
├── protocol-snapshots/{record_id}.json
├── verifications/{record_id}.json
├── commits/{record_id}.json
├── receipts/{record_id}.json
├── quality-assessments/{record_id}.json
├── completions/{record_id}.json
└── handoffs/{record_id}.json
```

Each milestone file is immutable, schema-versioned, named by its canonical
record UUID, and content-hashed using the accepted canonical JSON rules.
Reassessment, correction, reopening, or supersession creates a new record and
revision; it never replaces an existing milestone file. SQLite indexes the
record type, UUID, revision, content hash, session/subject binding, and
repository-relative path.

Authority and disagreement rules are:

- scientific payload bytes: the verified filesystem package is authoritative;
- mutable workflow/current state and subject PII: SQLite is authoritative;
- immutable milestone identity/content: the schema-valid JSON file and its
  content hash are authoritative, while SQLite is its operational index; and
- missing, mismatched, duplicated-conflicting, or cross-session indexed
  evidence enters `RECOVERY_REQUIRED`; it is not silently regenerated,
  overwritten, deleted, or treated as complete.

No receipt may be issued and no affected workflow may complete while required
repository evidence disagrees.

The root `repository.json` is an atomically replaced descriptor containing
only: descriptor schema version, repository UUID, repository contract/version,
namespace version, catalog schema version, creation UTC, creating Windows
account, catalog-relative path, and required feature identifiers. It contains
no subject data, secrets, absolute paths, mutable capture state, or filesystem
volume binding. Exact JSON and SQLite schemas are I0.4B-B3 work.

Compatibility behavior is:

- an unsupported major repository version refuses mutation and may offer only
  explicitly safe read-only inspection/export;
- a newer minor version with an unknown required feature refuses mutation;
- a known compatible minor version opens normally;
- an older supported version is not silently migrated on open; and
- approved migrations may update descriptor, catalog, or record indexes but
  shall not rewrite historical capture packages or milestone records unless a
  separately approved migration contract explicitly requires it.

## Alternatives considered

- **Mirror the full SQLite state as JSON:** rejected because concurrent mutable
  authorities create split-brain recovery and unnecessary write complexity.
- **Keep every record only in SQLite:** rejected because immutable milestones
  would be harder to verify, export, reconstruct, and preserve independently of
  catalog availability.
- **Write subject PII into subject/session filesystem records:** rejected for
  MVP because it duplicates mutable identifying data and expands exposure;
  catalog backup/restore remains mandatory later work.
- **Use one append-only JSONL session ledger:** rejected because interrupted
  append recovery and partial-line corruption complicate individual
  content-addressed verification.
- **Silently upgrade repositories on open:** rejected because it changes
  controlled state before the operator can review compatibility and recovery.
- **Treat SQLite index data as overriding mismatched milestone files:** rejected
  because an index cannot prove the immutable evidence bytes it references.

## Rationale

The split follows the data lifecycle instead of duplicating storage blindly.
SQLite efficiently supports trained-operator workflows and mutable local PII.
Immutable files preserve important evidence with stable hashes and can be
verified independently. Explicit disagreement rules prevent either authority
from manufacturing apparent completion.

## Consequences

- Backup/restore must treat the SQLite catalog and repository tree as one
  coordinated set even though their authority differs by record class.
- Catalog reconstruction may recover package and milestone indexes but cannot
  recreate lost subject PII; backup verification is therefore a later release
  gate.
- Record publication needs write, flush, atomic rename, schema/hash validation,
  and catalog indexing rules in B3.
- UI current-state queries use SQLite; direct filesystem scans are recovery and
  verification operations, not normal workflow authority.
- Compatibility, schema, crash-point, runtime, power-loss, field, independent,
  and regulatory evidence remain separate.

## Affected components and interfaces

Coordinator catalog and host, subject/protocol/session/trial services,
repository/transaction journal, transfer/verifier, custody/receipt/cleanup,
quality/completion/handoff, audit, backup/export, simulator/QA, risk and
regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0004, ADR-0009, ADR-0010, ADR-0014, ADR-0016, and ADR-0017;
supersedes none.

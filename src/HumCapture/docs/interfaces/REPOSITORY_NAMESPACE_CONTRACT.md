# HumCapture Repository Namespace Contract

**Contract ID:** HC-IF-REP-001  
**Version:** 1.1.0  
**Status:** Accepted namespace and record-authority baseline  
**Date:** 2026-09-10

## 1. Scope

This contract fixes the repository-relative namespace and record authorities
used by collection, verification, commit, reconciliation, receipt, export, and
backup work. It does not yet define executable repository descriptor, SQLite
catalog, transaction-journal, or reconciliation schemas and does not authorize
application implementation.

## 2. Canonical paths

| Purpose | Repository-relative path |
|---|---|
| Repository descriptor | `repository.json` |
| SQLite catalog | `catalog/humcapture.sqlite3` |
| Collected package | `staging/{collection_attempt_id}/{package_id}/` |
| Quarantined package | `quarantine/{quarantine_record_id}/{package_id}/` |
| Committed package | `subjects/{subject_id}/sessions/{session_id}/packages/{package_id}/` |
| Immutable milestone record | `subjects/{subject_id}/sessions/{session_id}/records/{record_kind}/{record_id}.json` |

Every identity placeholder is its canonical lowercase hyphenated UUID string.
All paths are resolved relative to one configured data root. Staging,
quarantine, catalog, and committed packages remain under that root and on its
filesystem volume.

## 3. Package envelope

The package-ID directory contains the exact HC-IF-XFR-001 package that passed
the common verifier. Its `package-manifest.json`, artifact set, relative paths,
lengths, and bytes are unchanged. The repository shall not insert its own
metadata or markers into this envelope. Lower-level trial, source,
capture-attempt, device, and boot identities are read from and cross-checked
against the package manifest and catalog.

## 4. Subject identity and privacy

`subject_id` is the folder authority. Required subject name and demographics
remain Coordinator-local catalog data. Names, codes, demographics, protocol or
camera labels, dates, and operator account names shall not occur in canonical
repository paths or `repository.json`.

## 5. Path safety and conflict behavior

The repository rejects paths that are absolute, drive-relative, escaping,
non-canonical, case-colliding, alternate-data-stream addressed, or associated
with Windows device aliases, hard links, symbolic links, junctions, or other
reparse points. The existing destination is never overwritten. An identical
package identity/content is handled idempotently; conflicting content is
quarantined under a new quarantine-record UUID.

The implementation shall validate the complete resolved path against the
active Windows/filesystem capability before collection and again before
commit. Unexpected entries are surfaced for reconciliation and are not
automatically deleted.

## 6. Versioning

Version 1.1.0 adds the record-authority, descriptor and compatibility semantics
below without changing version 1.0.0 package paths.

SQLite is authoritative for mutable operational/current state, subject PII,
transfer checkpoints, transaction journal and audit events. Mutable current
state is not mirrored into replaceable JSON. Scientific package bytes and the
following immutable milestone kinds are filesystem evidence:

`protocol-snapshots`, `verifications`, `commits`, `receipts`,
`quality-assessments`, `completions`, and `handoffs`.

Each milestone filename uses its canonical record UUID and the file is
schema-versioned, content-hashed and immutable. SQLite indexes its type, UUID,
revision, hash, subject/session binding and repository-relative path. A
conflict or mismatch enters recovery and blocks affected receipt/completion.

`repository.json` declares the descriptor schema version, repository UUID,
HC-IF-REP-001 version, namespace version, catalog schema version, creation UTC,
creating Windows account, catalog-relative path, and required features. It has
no subject PII, secret, absolute path, mutable capture state, or persistent
volume binding. Publication uses validated write/flush/atomic replacement.

Unsupported major versions refuse mutation and may expose only explicitly safe
read-only inspection/export. A newer minor version with an unknown required
feature refuses mutation; known compatible versions open normally. Opening an
older supported repository does not migrate it silently. Historical packages
and milestone files are not moved, renamed, or rewritten by a later reader.

## 7. Deferred decisions

I0.4B-B3 will define descriptor, milestone-index, catalog and journal schemas,
transaction transitions, crash-point fixtures, and recovery outcomes. Runtime,
SQLite DDL/configuration, backup/restore, retention, power-loss, HIL, field,
independent and regulatory evidence remain open.

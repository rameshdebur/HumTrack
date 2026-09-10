# HumCapture Repository Namespace Contract

**Contract ID:** HC-IF-REP-001  
**Version:** 1.0.0  
**Status:** Accepted namespace baseline  
**Date:** 2026-09-10

## 1. Scope

This contract fixes the repository-relative namespace used by collection,
verification, commit, reconciliation, receipt, export, and backup work. It does
not yet define repository descriptor, SQLite catalog, transaction-journal, or
compatibility schemas and does not authorize application implementation.

## 2. Canonical paths

| Purpose | Repository-relative path |
|---|---|
| Repository descriptor | `repository.json` |
| SQLite catalog | `catalog/humcapture.sqlite3` |
| Collected package | `staging/{collection_attempt_id}/{package_id}/` |
| Quarantined package | `quarantine/{quarantine_record_id}/{package_id}/` |
| Committed package | `subjects/{subject_id}/sessions/{session_id}/packages/{package_id}/` |

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

`repository.json` declares `HC-IF-REP-001@1.0.0` and the repository UUID.
Unknown required features and unsupported major versions fail closed without
modifying packages. Historical finalized or committed packages are not moved,
renamed, or internally rewritten by a later layout reader.

## 7. Deferred decisions

I0.4B-B2/B3 will define subject/session operational records, descriptor,
catalog and journal schemas, transaction transitions, compatibility window,
crash-point fixtures, and recovery outcomes. Runtime, backup/restore,
retention, power-loss, HIL, field, independent and regulatory evidence remain
open.

# ADR-0017 — Shallow UUID repository namespace

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0016 requires a Coordinator-owned data root with same-volume staging,
quarantine, immutable committed packages, a SQLite catalog, and recoverable
transactions. The exact namespace must now be fixed before journal/catalog
schemas can bind destinations and before runtime code can be designed.

The capture hierarchy includes subject, session, trial, source, attempt, and
package identities. Repeating every identity as a directory would make paths
needlessly deep on the supported Windows 10/11 baseline. Using subject names or
other display text would make paths mutable, identifying, collision-prone, and
unsafe. Conversely, flattening every package into one global directory would
make operator recovery and subject-level inspection difficult.

The accepted transfer contract also makes the source package immutable after
publication. A repository layout cannot silently reorganize its internal
artifact paths into new `video`, `timing`, `imu`, or `metadata` directories.

## Decision

The HC-IF-REP-001 version 1.0.0 namespace is:

```text
DATA_ROOT/
├── repository.json
├── catalog/
│   └── humcapture.sqlite3
├── staging/
│   └── COLLECTION_ATTEMPT_UUID/
│       └── PACKAGE_UUID/
├── quarantine/
│   └── QUARANTINE_RECORD_UUID/
│       └── PACKAGE_UUID/
└── subjects/
    └── SUBJECT_UUID/
        └── sessions/
            └── SESSION_UUID/
                └── packages/
                    └── PACKAGE_UUID/
                        └── EXACT_VERIFIED_SOURCE_PACKAGE
```

All identity directory segments are canonical lowercase hyphenated UUIDs.
Subject names, codes, demographics, protocol names, camera names, dates, and
other display text shall not form any path segment. Subject name remains a
required Coordinator-local catalog field.

The committed package path is exactly:

```text
subjects/{subject_id}/sessions/{session_id}/packages/{package_id}/
```

Trial, source, capture-attempt, device, and boot identities remain
authoritative in the immutable package manifest and SQLite catalog rather than
being repeated in directory depth. This does not weaken their required
identity/hash validation.

The `PACKAGE_UUID` directory is an opaque repository envelope containing the
source package exactly as verified, including its canonical
`package-manifest.json` and original artifact relative paths. Repository
metadata, commit markers, receipts, or indexes shall not be inserted into,
renamed within, or removed from that directory.

`repository.json` identifies the repository UUID, namespace contract/version,
and supported required features; it contains no subject PII. SQLite database,
WAL, shared-memory, and other SQLite-owned support files remain under
`catalog/`. Exact descriptor and database schemas are subsequent controlled
work.

Staging, quarantine, and committed subjects reside beneath the same resolved
data root and on the same filesystem volume. Existing final destinations are
never overwritten. Repository path handling rejects traversal, absolute or
drive-relative paths, Windows device aliases, alternate data streams,
non-canonical UUID segments, case-insensitive collisions, symbolic links,
junctions, other reparse points, and hard links. Unexpected repository entries
are reported for reconciliation and are never silently deleted.

## Alternatives considered

- **Repeat trial/source/attempt/package in the directory hierarchy:** rejected
  because those identities already exist in the manifest/catalog and the extra
  depth reduces Windows/tool compatibility without adding authority.
- **Use subject name or subject code in folder names:** rejected because names
  and codes can change, collide, reveal identity, or contain unsafe characters.
- **Use one global package directory:** rejected because subject/session-level
  recovery, inspection, export, and backup become less understandable.
- **Reorganize package artifacts into standard media subfolders at commit:**
  rejected because it changes the verified package bytes/paths and breaks
  idempotent identity and historical compatibility.
- **Put transaction markers inside committed packages:** rejected because the
  repository must not mutate the verified package envelope.

## Rationale

The chosen hierarchy preserves the operator-visible Subject → Session boundary
while keeping package storage shallow. UUIDs keep paths stable and
pseudonymized. All lower-level acquisition identities remain fully traceable in
the records that already bind them. Treating package contents as opaque ensures
the same verified bytes survive network transfer, USB/MTP recovery, local UVC
collection, repository commit, and downstream handoff.

## Consequences

- Repository paths can be derived deterministically from three immutable
  identities and compared without display-name normalization.
- Subject demographic corrections never rename committed paths.
- Trial/source-oriented UI and export views must query manifests/catalog data;
  they cannot infer those identities from directory names.
- Repository-local operational records must live outside committed package
  envelopes.
- Total resolved path-length checks remain necessary before collection/commit;
  the shallow namespace reduces but does not eliminate Windows/tool limits.
- Schema, compatibility, crash-point, runtime, power-loss, backup/restore,
  field, independent, and regulatory verification remain separate work.

## Affected components and interfaces

Coordinator repository and catalog, transfer staging, verifier, quarantine,
commit journal, receipt paths, subject/session UI, recovery, backup/export,
HumTrack handoff, simulator/QA, risk and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0004, ADR-0012, ADR-0014, and ADR-0016; supersedes none.

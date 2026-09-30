# ADR-0016 — Recoverable repository commit across filesystem and catalog

**Status:** Accepted  
**Date:** 2026-09-09

## Context

ADR-0004 makes durable repository commit the acquisition-completion boundary,
ADR-0012 requires every collection path to pass the same verifier, and
ADR-0014 permits a cleanup receipt only after commit. The Coordinator uses a
SQLite operational catalog while scientific masters remain immutable,
self-contained filesystem packages. SQLite and the filesystem cannot
participate in one native atomic transaction, so a crash between their updates
could otherwise produce false completion, an orphaned package, or an unsafe
cleanup receipt.

Subject name is required for the trained-operator workflow but is mutable and
may not be unique. It must not become the authoritative repository identity or
leak into Android packages, discovery, routine logs, or pseudonymized handoff.

## Decision

HumCapture shall use a configurable, Coordinator-owned data root outside the
source-code checkout. The signed-in Windows account is the MVP operator and
local repository authority. SQLite is the operational catalog; immutable
filesystem packages are the authoritative scientific payloads.

The subject UUID, not subject name, is the authoritative folder key. The
required subject name and demographics remain in the Coordinator-local subject
record and are presented by the UI without becoming package or directory
identity.

Collection staging and quarantine remain outside the committed subject
hierarchy. Commit staging and the final repository destination shall be on the
same filesystem volume. A repository commit uses a recoverable journal:

1. verify the complete staged package;
2. revalidate its identity, content hashes, intended destination, available
   space, and repository availability;
3. durably record a `COMMITTING` journal entry;
4. atomically rename the immutable package into its final destination;
5. transactionally write the catalog, custody, verification, and commit
   linkage in SQLite;
6. reconcile the journal, filesystem destination, catalog, and verification
   record; and
7. mark the commit `COMMITTED` only when those authorities agree.

A completion receipt may be created only after step 7. A process restart,
power interruption, or I/O failure invokes deterministic reconciliation:

- the same package ID and content hash is idempotent success;
- the same package ID with different content is quarantined without overwrite;
- partial or unverified material remains outside the committed hierarchy;
- a catalog/filesystem/journal disagreement enters recovery and produces no
  receipt; and
- repository absence or insufficient space blocks commit but does not change a
  source's already-recorded capture/finalization facts.

The final folder layout, journal and catalog schemas, durability primitives,
backup/restore behavior, retention, and compatibility window are controlled by
subsequent I0.4B decisions and verification. This decision does not authorize
application implementation.

## Alternatives considered

- **Treat a SQLite row as the complete repository:** rejected because large
  scientific masters are better retained as self-contained files and external
  tooling must be able to verify them without reconstructing database blobs.
- **Treat final-directory presence as commit:** rejected because it does not
  prove catalog, verification, custody, and receipt linkage.
- **Update SQLite and the filesystem without a journal:** rejected because no
  atomic transaction spans both authorities and crash state becomes ambiguous.
- **Copy directly into the final subject folder:** rejected because partial
  content could appear committed and cross-volume copy cannot provide the
  required atomic rename boundary.
- **Use subject name as a folder key:** rejected because names are mutable,
  non-unique, path-sensitive, and identifying.
- **Issue the receipt before reconciliation finishes:** rejected because it
  could authorize cleanup without a proven durable commit.

## Rationale

This model retains the simple local-first MVP architecture while making the
accepted completion and cleanup boundary recoverable. Immutable package bytes
remain independently verifiable, SQLite supports Coordinator queries and
workflow state, UUID paths remain stable when demographics are corrected, and
the journal provides an explicit recovery point when the two storage
authorities cannot change atomically.

## Consequences

- The repository root must enforce same-volume staging for commit; transfers
  from another volume first collect into repository-local staging.
- Commit code must use platform-appropriate durable-write and atomic-rename
  primitives and verify their results; a normal rename alone is not durability
  evidence.
- Startup must reconcile unfinished journal entries before treating affected
  packages as committed or issuing receipts.
- Catalog reconstruction and backup/restore need explicit later rules because
  neither catalog nor payload alone represents all operational state.
- The UI may show subject names but paths, receipts, logs, and source packages
  continue to bind UUIDs and hashes.
- Contract, automated crash-point, runtime, power-loss, field, independent, and
  regulatory verification remain separate evidence levels.

## Affected components and interfaces

Coordinator host, SQLite catalog, transfer staging, common verifier, repository
and commit journal, subject/session/trial hierarchy, custody and receipt
records, startup reconciliation, backup/restore, Android cleanup eligibility,
quality/handoff, simulator/QA, risk and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0004, ADR-0009, ADR-0012, and ADR-0014; supersedes none.

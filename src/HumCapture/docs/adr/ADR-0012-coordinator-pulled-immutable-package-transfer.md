# ADR-0012 — Coordinator-pulled immutable package transfer

**Status:** Accepted  
**Date:** 2026-09-05

## Context

ADR-0004 makes a verified, transactionally committed package the workflow
boundary. Android capture must survive network loss, USB/MTP must provide a
field fallback, UVC capture is already local to the coordinator, and every
path must reach the same verifier without weakening scientific-master
acquisition or inventing different package formats.

## Decision

Each source publishes `package-manifest.json` only after all package artifacts
are closed and hashed. The finalized package is an immutable directory, not a
mandatory archive. The manifest binds package, subject, session, trial, source,
capture-attempt, source-boot, protocol-snapshot, configuration, finalization,
artifact identities, lengths, SHA-256 values, roles, and timing coverage.

For Android automatic collection, the coordinator is the client and pulls the
manifest and artifact bytes over HTTPS. Artifacts support HTTP byte ranges. A
partial copy may be combined only while its strong ETag remains unchanged;
`If-Range` governs resume. The coordinator stages all partial material outside
the subject repository. It verifies the whole artifact set before transactional
commit and creates a receipt only after durable commit.

USB/MTP imports the identical package. It resumes at artifact boundaries:
fully verified artifacts may be skipped; an incomplete artifact restarts from
byte zero. Coordinator-local UVC packages enter the same verifier without a
network-copy step.

Identical package ID and content hash is idempotent. Reuse of a package ID with
different content is quarantined. Android data remains protected until the
commit receipt is acknowledged and the trained operator explicitly confirms
cleanup.

## Alternatives considered

- Mandatory ZIP/archive: rejected because interruption can require repacking or
  restarting a large object and MTP handling becomes less transparent.
- Device-pushed upload: rejected because the accepted coordinator authority and
  collection scheduling model requires coordinator pull.
- tus: rejected for this slice because tus defines resumable uploads, while the
  selected direction is coordinator download from the capture source.
- Custom partial-upload protocol: rejected in favor of standardized HTTP range,
  validator, and digest semantics.
- Transfer-method-specific manifests or verifiers: rejected because they permit
  integrity and completion behavior to diverge.

## Consequences

- Source artifacts and the manifest cannot change after publication.
- Coordinator staging/checkpoint state is recoverable but never a committed
  subject record.
- Artifact identity is addressed by manifest `artifact_id`; untrusted relative
  paths are never accepted as request paths.
- SHA-256 in the manifest is the authoritative stored-artifact integrity value.
  HTTP digests provide transport-message and representation checks.
- Range size, parallelism, exact credentials/pairing/TLS binding, receipt
  signing, offline receipt conveyance, and repository transaction implementation
  remain separate decisions. Authentication and encryption are mandatory; this
  deferral does not authorize anonymous or plaintext deployment.

## Affected components and interfaces

Android finalizer/transfer/cleanup, coordinator transfer manager, UVC finalizer,
common verifier, staging/repository/receipt services, security/privacy, audit,
simulator/QA, risk and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0004; supersedes none.

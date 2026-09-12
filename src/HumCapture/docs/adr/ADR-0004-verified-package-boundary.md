# ADR-0004 — Verified package is the completion and handoff boundary

**Status:** Accepted  
**Date:** 2026-08-26

## Context

Recording stop or file-copy completion does not prove that a workflow is complete or data is correctly associated.

## Decision

Each source finalizes an immutable package with manifest and hashes. The coordinator verifies schema, identity, required files, lengths, SHA-256, and structure before transactional commit. A trial/session completes only when protocol-required packages/trials are committed. HumCapture hands off a versioned pseudonymized package by default.

## Alternatives considered

- Completion on recording stop: rejected.
- Completion on transfer byte count: rejected.
- Manual destination selection: rejected due cross-subject risk.

## Rationale

This provides integrity, recoverability, idempotency, and an auditable boundary.

## Consequences

Network resume, USB/MTP recovery, quarantine, receipts, quality reports, and repository transactions are required. Android deletion is allowed only after commit acknowledgement and user confirmation.

## Affected components and interfaces

Android finalizer/transfer/cleanup, UVC finalizer, transfer manager, verifier, repository, handoff.

## Supersedes / Superseded by

None.


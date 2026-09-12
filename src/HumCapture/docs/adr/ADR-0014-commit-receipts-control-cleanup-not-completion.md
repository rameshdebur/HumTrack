# ADR-0014 — Commit receipts control cleanup, not acquisition completion

**Status:** Accepted  
**Date:** 2026-09-07

## Context

Android masters must remain recoverable through network failure while phone
storage must eventually be reclaimed. A coordinator commit proves that the
verified package reached its durable subject-repository destination. Delivery
of that fact back to Android can be delayed or its acknowledgement lost. If
session completion depended on that round trip, an already safe acquisition
would become falsely incomplete; if deletion depended only on coordinator
belief, a stale or mismatched message could destroy the remaining source copy.

The MVP also supports USB/MTP manual transfer, but has no trusted offline
receipt-ingestion mechanism on Android and should not add signing hardware,
cloud services, or security machinery beyond the approved mTLS channel.

## Decision

Durable coordinator commit is the completion authority. The coordinator issues
one immutable, hash-bound receipt afterward. Android durably matches and stores
that exact receipt, then acknowledges it. Receipt acknowledgement creates
cleanup eligibility only; it does not complete the session and does not delete
data.

Cleanup is a separate trained-operator command over mTLS. It binds explicitly
selected package/hash/receipt/acknowledgement identities, is blocked during
active package operations, and returns truthful per-package results including
remaining paths after partial deletion. Lost acknowledgements are reconciled
by status query and identical replay. Conflicts retain data and require
recovery. USB/MTP permits verified coordinator commit and informed manual
cleanup, but not automatic Android deletion in the MVP.

## Alternatives considered

- Make receipt acknowledgement part of session completion: rejected because a
  network-only failure would falsely fail an already durable acquisition.
- Delete automatically after acknowledgement, after an age threshold, or in a
  background job: rejected because it removes trained-operator control and can
  race active/recovery work.
- Treat coordinator commit alone as Android deletion authority: rejected
  because Android has not proven durable receipt match.
- Create a new receipt after lost acknowledgement: rejected because it breaks
  immutability and obscures replay versus conflict.
- Import unsigned offline receipts into Android: rejected for MVP because it
  creates an unapproved trust channel; manual cleanup remains available.
- Add receipt signing: deferred because existing mTLS authenticates online
  delivery and signing does not solve the MVP offline workflow by itself.

## Consequences

- Acquisition remains complete through receipt/cleanup network failures.
- Android retains data until it has local durable evidence and an explicit
  operator command.
- Programmatic USB/MTP cleanup is unavailable in MVP; operators receive an
  informed manual fallback.
- Both peers need persistent receipt, acknowledgement, conflict, cleanup, and
  partial-result history in the later runtime implementation.
- This ADR is contract-source evidence only, not runtime, HIL, field,
  certification, or CDSCO conformity evidence.

## Affected components and interfaces

Coordinator repository/catalog/control UI, Android package catalog and cleanup
worker, transfer recovery, HC-IF-CTRL-001 1.3.0, HC-IF-RCP-001 1.0.0,
audit/privacy, simulator/QA, risk, and trained-operator workflow.

## Supersedes / Superseded by

Refines ADR-0004, ADR-0009, ADR-0012, and ADR-0013; supersedes none.

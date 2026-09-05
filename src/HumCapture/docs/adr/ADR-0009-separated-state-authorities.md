# ADR-0009 — Separate workflow, acquisition, custody, and health state authorities

**Status:** Accepted  
**Date:** 2026-09-05

## Context

HumCapture coordinates Android and UVC acquisition, post-capture collection,
verification, repository commit, quality review, and operator workflows. These
activities can advance or fail independently. A single state such as
`recording`, `failed`, or `complete` cannot truthfully represent whether a
source is acquiring master samples, whether its package is committed, or
whether only preview/connectivity has degraded.

The coordinator controls session and trial intent, but it cannot authoritatively
declare physical acquisition facts produced by Android/UVC recorders or durable
repository facts produced by the commit boundary.

## Decision

HumCapture separates state into four domains with explicit authorities:

1. The coordinator host owns session/trial workflow intent and progression.
2. The Android capture service or isolated UVC worker owns source-attempt
   acquisition and finalization facts.
3. Transfer, verification, and repository services own package-custody facts.
4. Connection, preview, recorder, storage, thermal, timing, and related health
   are observations associated with those lifecycles, not substitutes for them.

Commands request transitions. Acknowledgement means that a command was accepted
or rejected; it does not prove the requested physical or durable outcome.
Authoritative state events and evidence establish outcomes. The UI observes
these authorities and never becomes an independent state owner.

## Alternatives considered

- **One coordinator-owned state machine:** rejected because coordinator or
  network loss could misrepresent an Android recorder that continues locally.
- **One state machine per UI screen:** rejected because UI restart/navigation
  would become coupled to scientific acquisition.
- **One flat cross-system state enumeration:** rejected because capture,
  connectivity, transfer, verification, quality, and completion can diverge.
- **Treat all state as eventual device reports:** rejected because repository
  commit and protocol completion are coordinator-owned durable decisions.

## Rationale

The separation preserves the acquisition invariant, makes failure and recovery
truthful, and prevents `COMMAND_ACCEPTED`, byte transfer, preview health, or
source assertion from being mistaken for recording, verification, commit, or
workflow completion.

## Consequences

- Cross-domain projections are required for operator views.
- Restart/reconnection requires authority-aware reconciliation.
- Events and commands require stable identities, ordering, and idempotency.
- A source can remain `RECORDING` while control and preview are disconnected.
- A finalized package can remain collection- or verification-pending.
- Only repository `COMMITTED` packages can satisfy trial completion.
- More explicit state records are stored, but ambiguous generic failure states
  and unsafe inferred transitions are avoided.

## Affected components and interfaces

Coordinator host/UI, Android capture service, UVC workers, control protocol,
transfer/verifier/repository services, quality assessor, simulator, audit, and
handoff contracts.

## Supersedes / Superseded by

None. Complements ADR-0002, ADR-0003, ADR-0004, and ADR-0005.


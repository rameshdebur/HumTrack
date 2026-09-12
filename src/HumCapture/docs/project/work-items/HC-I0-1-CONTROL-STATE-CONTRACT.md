# HC-I0-1 — Coordinator Control and State Contract

**Tags:** I0.1 | INTERFACES | COORDINATOR-CONTROL | STATE-MACHINE | IDEMPOTENCY | RECOVERY | QUALITY

## Goal

Create the reviewed control/state contract required before Android, coordinator,
UVC, repository, or simulator feature implementation.

## Classification and ownership

- Change: cross-component interface and architecture.
- Primary owner: System Architect.
- Affected owners: coordinator, Android, UVC, transfer/repository, simulator/QA,
  security, risk/regulatory, and UI.
- Verification owner: QA/simulator owner after executable schema baseline.
- Paths: `docs/interfaces/`, `docs/adr/`, linked requirements/risk/project/
  traceability records, and the non-production conformance harness under
  `tools/evidence-control/`.
- Application implementation: not authorized by this work item.

## Accepted discussion decisions

- [x] I0.1A: four authority/state domains.
- [x] I0.1B: trial workflow and explicit complete/cancelled/incomplete outcomes.
- [x] I0.1C: common Android/UVC source-attempt lifecycle.
- [x] I0.1D: persistent idempotent commands and restart reconciliation.
- [x] I0.1E: prepared/committed future monotonic start and evidence-based stop.
- [x] I0.1F: readiness, invalidation, and protocol-scoped overrides.
- [x] I0.1G: package custody, verification, commit, receipt, and safe cleanup.
- [x] I0.1H: quality, retake, exclusion, supersession, and trial completion.
- [x] I0.1I: session/protocol execution lifecycle, immutable snapshot binding,
  completion/handoff, incomplete closure, and controlled reopening.

## Current status

ADR-0009 records the accepted state-authority decision. ADR-0010 records the
accepted immutable session/protocol snapshot decision. I0.1A-I are consolidated
as engineering interface baseline `HC-IF-CTRL-001` version `1.0.0`; approved
I0.2A subsequently extends the aggregate interface to version `1.1.0` without
rewriting the I0.1 record-schema versions.

The executable session/protocol slice consists of AsyncAPI 3.1.0, JSON Schema
2020-12 artifacts, and conformance fixtures/tests. This work item does not
authorize application feature implementation or constitute independent design,
quality, regulatory, or release approval.

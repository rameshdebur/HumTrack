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
- Paths: `docs/interfaces/`, `docs/adr/`, and linked project/traceability records.
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
- [ ] I0.1I: session/protocol execution lifecycle — explicit approval required.

## Current status

ADR-0009 records the accepted state-authority decision. The accepted I0.1A-H
content is consolidated as controlled draft `HC-IF-CTRL-001` version
`0.1-draft`. It is not yet a baselined executable interface.

The next gate is explicit approval of I0.1I followed by whole-contract coherence
review. Only then may the AsyncAPI, JSON Schemas, conformance fixtures, atomic
requirements, and implementation delegation be baselined.


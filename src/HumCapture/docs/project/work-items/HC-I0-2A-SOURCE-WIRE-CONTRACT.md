# HC-I0.2A — Source Message Wire Contract

**Tags:** I0.2A | CONTROL | SOURCE-MESSAGES | WIRE-CONTRACT | MONOTONIC-TIME | RECONCILIATION | CUSTODY | QUALITY

## Goal

Create the transport-neutral source-control records required before Android,
coordinator, UVC, repository, or simulator runtime implementation.

## Classification and ownership

- Change: cross-component interface, architecture, timing representation, and
  persistence record design.
- Primary owner: System Architect.
- Affected owners: Android, coordinator, UVC, timing, transfer/repository,
  simulator/QA, security, and regulatory/risk.
- Verification owner: QA/simulator owner after executable baseline.
- Paths: `docs/interfaces/`, `docs/adr/`, linked requirements/risk/project/
  traceability records, and `tools/evidence-control/`.
- Application implementation: not authorized.

## Approved decisions

- [x] Transport-neutral JSON records precede WebSocket/security binding.
- [x] Native monotonic time uses `clock_id`, canonical decimal-string uint64
  `ticks`, and explicit `ticks_per_second`.
- [x] Mapped session time also carries session-clock/model identity and
  non-implicit uncertainty.
- [x] UTC supports audit/display and never replaces scientific ordering.
- [x] JSON content identities use RFC 8785 canonical UTF-8 JSON plus SHA-256,
  omitting only the record's own content-hash property.
- [x] Source command, acknowledgement, event, state snapshot, start plan,
  readiness, custody, receipt, and quality records fail closed on unsupported
  identities, versions, states, clocks, and required fields.
- [x] Receipt issuance requires durable commit; acknowledgement does not prove
  acquisition outcome; `FIRST_MASTER_SAMPLE` proves `RECORDING`.

## Executable scope

HC-IF-CTRL-001 version 1.1.0, AsyncAPI 3.1.0, 18 JSON Schema 2020-12 files,
source-control fixtures, and HC-CTRL-TEST-007–019. The prior session/protocol
slice remains compatible at record-schema version 1.0.0.

## Deferred boundaries

Authenticated encrypted WebSocket binding, discovery/pairing payloads, transfer
OpenAPI/range behavior, capture/media manifests, timing/IMU binary formats,
repository implementation, receipt signing/offline conveyance, runtime code,
HIL, field, regulatory, and release approval remain separate.

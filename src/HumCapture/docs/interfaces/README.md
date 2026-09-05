# HumCapture Interface Specifications

## Current interface work

`CONTROL_STATE_MACHINE.md` is accepted engineering interface baseline
`HC-IF-CTRL-001` version `1.0.0`. It consolidates explicitly accepted I0.1A-I
decisions. ADR-0010 records immutable session protocol snapshots.

The executable session/protocol slice is:

- `asyncapi/control-v1.asyncapi.json` — logical AsyncAPI 3.1.0 channels and
  operations, without transport/authentication bindings;
- `schemas/control/v1/` — JSON Schema 2020-12 protocol, session, command,
  acknowledgement, event, completion, and handoff records; and
- `tools/evidence-control/fixtures/control/` plus HC-CTRL-TEST-001–006 — valid,
  invalid, cross-record, and exhaustive forbidden-transition conformance.

No runtime interface implementation is authorized yet. Before cross-component
feature work, complete and review the remaining contracts:

| Contract | Planned representation | Required evidence |
|---|---|---|
| Remaining source/readiness/custody/quality control messages | AsyncAPI plus JSON Schema | State/idempotency/reconciliation/malformed-message tests |
| Transfer API | OpenAPI | Range/resume/authentication/restart tests |
| Capture manifest | JSON Schema | Valid/invalid and compatibility fixtures |
| Timing stream | Binary specification and test vectors | Ordering/discontinuity/round-trip parser tests |
| IMU stream | Binary specification and test vectors | Timestamp/coordinate/provenance tests |
| Completion receipt | JSON Schema and signing rules | Replay/idempotency/identity tests |
| Quality report | JSON Schema | Protocol-threshold and versioning tests |
| Handoff manifest | JSON Schema | Relative-path/hash/package reconstruction tests |

## Contract rules

- Every contract has a stable identifier and version.
- Required semantic changes receive a new compatible version or explicit migration.
- Unknown required features fail safely; unknown optional fields are handled as specified.
- Historical finalized packages are never silently rewritten.
- Implementations consume shared conformance fixtures.
- Timestamp source, units, coordinate frames, uncertainty, and unavailable values are explicit.

# HumCapture Interface Specifications

## Current interface work

`CONTROL_STATE_MACHINE.md` is accepted engineering interface baseline
`HC-IF-CTRL-001` version `1.1.0`. It consolidates explicitly accepted I0.1A-I
and I0.2A decisions. ADR-0010 records immutable session protocol snapshots;
ADR-0011 records exact JSON monotonic-time representation.

The executable transport-neutral control slice is:

- `asyncapi/control-v1.asyncapi.json` — logical AsyncAPI 3.1.0 channels and
  operations, without transport/authentication bindings;
- `schemas/control/v1/` — JSON Schema 2020-12 session/protocol plus source
  command, acknowledgement, configuration, state/event, start-plan, readiness,
  custody/receipt, quality, completion, and handoff records; and
- `tools/evidence-control/fixtures/control/` plus HC-CTRL-TEST-001–019 — valid,
  invalid, cross-record, precision, replay/restart, and exhaustive
  forbidden-transition conformance.

No runtime interface implementation is authorized yet. Before cross-component
feature work, complete and review the remaining contracts:

| Contract | Planned representation | Required evidence |
|---|---|---|
| Transfer API | OpenAPI | Range/resume/authentication/restart tests |
| Capture manifest | JSON Schema | Valid/invalid and compatibility fixtures |
| Timing stream | Binary specification and test vectors | Ordering/discontinuity/round-trip parser tests |
| IMU stream | Binary specification and test vectors | Timestamp/coordinate/provenance tests |
| Completion receipt signing and offline conveyance | Signing/security rules | Replay/idempotency/identity and USB recovery tests |
| Quality implementation profile | Protocol-specific rules | Threshold/versioning and reassessment tests |
| Handoff manifest | JSON Schema | Relative-path/hash/package reconstruction tests |

## Contract rules

- Every contract has a stable identifier and version.
- Required semantic changes receive a new compatible version or explicit migration.
- Unknown required features fail safely; unknown optional fields are handled as specified.
- Historical finalized packages are never silently rewritten.
- Implementations consume shared conformance fixtures.
- Timestamp source, units, coordinate frames, uncertainty, and unavailable values are explicit.

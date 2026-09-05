# HumCapture Interface Specifications

## Current interface work

`CONTROL_STATE_MACHINE.md` is controlled draft `HC-IF-CTRL-001` version
`0.1-draft`. It consolidates explicitly accepted I0.1A-H decisions. The
session/protocol execution lifecycle remains an explicit approval gate, so no
executable control schema or application implementation is yet authorized.

No runtime interface implementation is authorized yet. Before cross-component feature work, create and review:

| Contract | Planned representation | Required evidence |
|---|---|---|
| Control commands/events | AsyncAPI plus JSON Schema | State/idempotency/malformed-message tests |
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

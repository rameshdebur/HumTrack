# HumCapture Interface Specifications

## Current interface work

`CONTROL_STATE_MACHINE.md` is accepted engineering interface baseline
`HC-IF-CTRL-001` version `1.2.0`. It consolidates explicitly accepted I0.1A-I
and I0.2A decisions. ADR-0010 records immutable session protocol snapshots;
ADR-0011 records exact JSON monotonic-time representation. ADR-0012 and
`TRANSFER_AND_PACKAGE_CONTRACT.md` baseline `HC-IF-XFR-001` version `1.1.0`.
ADR-0013 and `PAIRING_AND_TRANSPORT_SECURITY_CONTRACT.md` baseline
`HC-IF-SEC-001` version `1.0.0`.

The executable transport-neutral control slice is:

- `asyncapi/control-v1.asyncapi.json` — AsyncAPI 3.1.0 channels and operations
  bound to mutually authenticated WSS;
- `schemas/control/v1/` — JSON Schema 2020-12 session/protocol plus source
  command, acknowledgement, configuration, state/event, start-plan, readiness,
  custody/receipt, quality, completion, and handoff records; and
- `tools/evidence-control/fixtures/control/` plus HC-CTRL-TEST-001–019 — valid,
  invalid, cross-record, precision, replay/restart, and exhaustive
  forbidden-transition conformance.

The executable transfer/package slice adds:

- `openapi/transfer-v1.openapi.json` — coordinator-pulled HTTPS manifest and
  artifact GET/HEAD with range, validator, and digest semantics;
- `schemas/transfer/v1/` — immutable package manifest, coordinator collection
  checkpoint, and common verification record; and
- HC-XFR-TEST-001–011 — schema/API, identity, path, range/restart, USB/MTP,
  false-verification, idempotency/quarantine, receipt, and digest conformance.

The executable security slice adds four JSON Schemas for ephemeral bootstrap,
enrollment, trust lifecycle, and redacted audit records; mutual-TLS bindings in
both interface documents; and HC-SEC-TEST-001–012 negative-path conformance.

No runtime interface implementation is authorized yet. Before cross-component
feature work, complete and review the remaining contracts:

| Contract | Planned representation | Required evidence |
|---|---|---|
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

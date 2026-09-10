# HumCapture Interface Specifications

## Current interface work

`CONTROL_STATE_MACHINE.md` is accepted engineering interface baseline
`HC-IF-CTRL-001` version `1.3.0`. It consolidates explicitly accepted I0.1A-I
and I0.2A decisions. ADR-0010 records immutable session protocol snapshots;
ADR-0011 records exact JSON monotonic-time representation. ADR-0012 and
`TRANSFER_AND_PACKAGE_CONTRACT.md` baseline `HC-IF-XFR-001` version `1.2.0`.
ADR-0014 and `RECEIPT_ACKNOWLEDGEMENT_AND_CLEANUP_CONTRACT.md` baseline
`HC-IF-RCP-001` version `1.0.0`.
ADR-0013 and `PAIRING_AND_TRANSPORT_SECURITY_CONTRACT.md` baseline
`HC-IF-SEC-001` version `1.0.0`.
ADR-0015 and `TIMING_AND_IMU_BINARY_CONTRACT.md` baseline `HC-IF-TIM-001`
version `1.0.0` with executable binary vectors and timing/camera/IMU schemas.
ADR-0016 accepts the I0.4B-A recoverable SQLite/filesystem commit architecture;
ADR-0017/0018 and `REPOSITORY_NAMESPACE_CONTRACT.md` baseline the shallow UUID
namespace and split operational/immutable record authority as `HC-IF-REP-001`
version `1.1.0`. Executable descriptor, catalog/journal and reconciliation
schemas remain I0.4B-B3 work.

The executable transport-neutral control slice is:

- `asyncapi/control-v1.asyncapi.json` — AsyncAPI 3.1.0 channels and operations
  bound to mutually authenticated WSS;
- `schemas/control/v1/` — JSON Schema 2020-12 session/protocol plus source
  command, acknowledgement, configuration, state/event, start-plan, readiness,
  custody/receipt/acknowledgement/status/cleanup, quality, completion, and handoff records; and
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
| Timing and IMU runtime producers/consumers | Implement HC-IF-TIM-001 1.0.0 in Android, UVC and Coordinator | Runtime/HIL production, ingestion and cross-platform vector tests |
| Repository records and compatibility | HC-IF-REP-001 descriptor, journal/catalog records and recovery rules under ADR-0016/0017 | Schema plus crash-point, restart, canonical-path, package-opacity, idempotency, conflict, same-volume and receipt-ordering tests |
| Receipt signing and trusted offline acknowledgement | Future signing/security rules | Existing replay/idempotency/identity and USB manual-boundary regression tests |
| Quality implementation profile | Protocol-specific rules | Threshold/versioning and reassessment tests |
| Handoff manifest | JSON Schema | Relative-path/hash/package reconstruction tests |

## Contract rules

- Every contract has a stable identifier and version.
- Required semantic changes receive a new compatible version or explicit migration.
- Unknown required features fail safely; unknown optional fields are handled as specified.
- Historical finalized packages are never silently rewritten.
- Implementations consume shared conformance fixtures.
- Timestamp source, units, coordinate frames, uncertainty, and unavailable values are explicit.

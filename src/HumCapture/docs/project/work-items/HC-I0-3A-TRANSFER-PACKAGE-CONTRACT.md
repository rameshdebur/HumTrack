# HC-I0.3A — Transfer, Package Manifest, and Resume Contract

**Tags:** I0.3A | TRANSFER | PACKAGE-MANIFEST | RANGE-RESUME | USB-MTP | COMMON-VERIFIER | COMMIT-RECEIPT | CONTRACT

## Goal

Baseline the immutable package and resumable coordinator-collection boundary
before Android, coordinator, UVC, verifier, or repository application features.

## Classification and ownership

- Change: cross-component interface, architecture, persistence, integrity, and
  privacy/security-adjacent contract design.
- Primary owner: System Architect.
- Affected owners: Android, coordinator, UVC, transfer/repository,
  security/privacy, simulator/QA, and regulatory/risk.
- Verification owner: QA/simulator owner after executable baseline.
- Paths: HumCapture documentation and `tools/evidence-control/` only.
- Application implementation: not authorized.

## Approved decisions

- [x] Immutable finalized directory plus canonical manifest; no mandatory ZIP.
- [x] Coordinator-pulled HTTPS manifest/artifact GET with byte ranges, strong
  ETag/If-Range, and HTTP content/representation digests.
- [x] Manifest SHA-256 remains authoritative stored-artifact integrity.
- [x] Partial bytes stay in staging and cannot enter the subject repository.
- [x] USB/MTP conveys the identical package, skips only verified artifacts, and
  restarts incomplete artifacts from zero.
- [x] UVC local collection uses the identical verifier.
- [x] Same package identity/content is idempotent; same ID/different content is
  quarantined.
- [x] Full verification plus durable commit precedes receipt.
- [x] Android cleanup requires acknowledged receipt and informed operator action.

## Executable scope

`HC-IF-XFR-001` version 1.0.0, OpenAPI 3.1.2, three JSON Schema 2020-12
records, semantic conformance helpers, and HC-XFR-TEST-001–011.

## Deferred boundaries

I0.3B pairing/authentication/TLS/replay binding; range sizing and parallelism;
receipt signing/offline conveyance; repository transaction implementation;
runtime, HIL, field, regulatory, clinical, and controlled-release approval.

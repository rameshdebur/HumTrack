# HC-I0.4A — Timing, IMU and camera metadata contract

**Tags:** I0.4A | TIMING | IMU | BINARY | CLOCK-MAPPING | CAMERA-METADATA | QUALITY | TEST-VECTORS | CONTRACT

## Goal

Baseline deterministic Android/UVC timing evidence, IMU observations,
camera/lens/orientation metadata and explicit clock/transform models before any
runtime producer, Coordinator consumer or analytics implementation.

## Classification and ownership

- Change: cross-component interface, architectural, scientific timing,
  analytics-readiness and integrity contract.
- Primary owner: System Architect / timing owner.
- Affected owners: Android, UVC, Coordinator, repository/transfer,
  simulator/QA, risk/regulatory and SBOM/release.
- Verification owner: engineering QA using the shared contract oracle;
  independent human reviewer remains pending.
- Allowed paths: `src/HumCapture` documentation, schemas, fixtures and existing
  evidence-control tooling only.
- Application implementation: not authorized.

## Approved decisions

- [x] Separate frame timestamp and Android IMU masters with versioned JSON sidecars.
- [x] Native clocks/timestamps authoritative; UTC, arrival, PTS and mappings separate.
- [x] Raw Android acceleration/gyro preserved; derived sensors remain distinct.
- [x] 100 Hz raw and 50 Hz derived Android requests; actual cadence measured.
- [x] Fixed-size little-endian records, presence bits, CRC32C and package SHA-256.
- [x] Explicit affine clock models, validity segments and uncertainty.
- [x] Source-frame/video association and visible loss/disposition.
- [x] Protocol-scoped required/preferred/informational quality gates.
- [x] Fixed/variable/adaptive camera classes; no adaptive-rate universal gate.
- [x] Camera/lens/orientation metadata and explicit camera–IMU association.
- [x] Immutable positive/negative conformance vectors and evidence-level separation.

## Executable scope

HC-IF-TIM-001 version 1.0.0; frame record v1.0 (96 bytes); IMU record v1.0
(80 bytes); three JSON Schema 2020-12 metadata records; deterministic generator,
reference encoder/decoder/validator and HC-TIM-TEST-001–012. HC-IF-XFR-001
version 1.2.0 adds compatible profile/version declarations.

## Deferred boundaries

Android/UVC production writers, Coordinator ingestion, live video association,
runtime recovery, actual cadence and clock-fit thresholds, camera qualification,
analytics algorithms/UI, runtime/HIL/field evidence, independent review,
qualified regulatory review and controlled release.


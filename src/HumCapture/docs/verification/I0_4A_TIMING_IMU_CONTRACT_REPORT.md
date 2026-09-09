# I0.4A Timing and IMU Contract Verification

**Report ID:** HC-VR-I0-4A-001  
**Date:** 2026-09-09  
**Status:** Local source/static/automated verification passed; commit and exact-SHA CI pending

## Scope

This report covers HC-IF-TIM-001 1.0.0, ADR-0015, the additive
HC-IF-XFR-001 1.2.0 profile fields, three metadata schemas, fixed binary
frame/IMU layouts, deterministic golden vectors, reference codec/validator,
requirements/risk traceability and HC-TIM-TEST-001–012.

It does not cover Android/UVC production, Coordinator ingestion, real camera or
IMU output, clock-fit performance, runtime recovery, hardware-in-the-loop,
field workflow, analytics correctness, independent review, regulatory/clinical
review or controlled release.

## Acceptance oracle

- Native timestamps and per-stream sequences remain authoritative.
- Arrival, PTS, UTC, mappings and uncertainty remain separately identified.
- Binary magic/version/size/presence/CRC/count and package SHA fail closed.
- Only an incomplete final record may be discarded from a non-finalized stream.
- IMU sensors retain independent sequence domains and finite present values.
- Measured cadence and discontinuities are evidence; advertised/requested FPS is not.
- Fixed-rate cameras and unavailable optional lens fields remain supportable.
- Protocol-required failure blocks/rejects; preferred failure degrades visibly.
- Accepted source frames bind uniquely to completely decoded video frames.
- Camera/IMU mappings, coordinate frames and derived analytics remain explicit and uncertainty-bounded.

## Local results

| Evidence | Result |
|---|---|
| HC-TIM-TEST-001–012 | 12/12 pass |
| Evidence-control full regression | 76/76 pass |
| Capability regression | 55/55 pass |
| SBOM policy and retained inventory | 6/6 pass; 20 components/21 nodes validate; retained SHA-256 `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57` matches |
| Dependency audits | Both locked production Node surfaces report zero vulnerabilities |
| JSON/static/diff | 73 tracked/new JSON documents parse; three new JS files pass syntax checks; `git diff --check` passes |
| OpenAPI regression | Redocly CLI 2.51.2 accepts HC-IF-XFR-001 1.2.0 with the existing documented advisory-rule exclusions |
| Generator determinism | All 15 timing fixture files reproduce byte-identically |
| Binary vectors | Nine deterministic files: six accepted and three rejected; decoded references, byte lengths/counts/SHA-256 checked by tests |
| Exact implementation SHA CI | Pending commit/push |

## Evidence classification

- Source implemented: yes.
- Build/static checks: passed for the contract/static scope.
- Automated behavior: 76/76 shared evidence-control tests pass.
- Runtime integration: not verified.
- Hardware-in-the-loop: not verified.
- Field workflow: not verified.
- Regulatory or clinical review: not completed.

No camera qualification, production parser safety, synchronization accuracy,
analytics accuracy, CDSCO approval, certification, clinical or release claim is
supported by this report.

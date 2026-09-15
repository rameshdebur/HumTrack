# C14 Package Verifier — Approved Objective and Baseline Gaps

ID: HC-CHG-20260915-002. Date: 2026-09-15.
Status: C14 objective and prerequisite choices approved; C14A contract baseline
implemented. Runtime verifier implementation not started.

Approved: independently decode collected video, check timing/camera/events/
finalization and applicable IMU evidence, preserve measured fixed/variable cadence,
and produce a versioned verification record. Only all required checks passing may
permit VERIFIED. No automatic commit, receipts, source modification or deletion.

Primary owner: Coordinator/repository engineer. Affected: Architect, Android,
UVC, QA, Risk/Regulatory and Release/SBOM. Verification owner: engineering QA.
Classification: cross-component artifact contract and runtime dependency decision.
Existing HC-IF-XFR-001, HC-IF-TIM-001 and HC-RISK-022/030/031 apply.
No regulatory applicability change or conformity claim.

## Inspection evidence

- The verification-record schema defines required check names and dispositions,
  but does not specify the contents of every input artifact.
- Timing/IMU binaries and camera/timing/IMU JSON have versioned definitions.
- CAPTURE_EVENTS and FINALIZATION_RECORD are manifest roles, but no standalone
  archive/summary artifact schemas currently define their production payloads.
  The control source-state-event schema is reusable but is not by itself an
  accepted on-disk archive and finalization-summary contract.
- Repository self-test fixture payloads are synthetic event/outcome strings.
  Their hash/admission tests are not semantic finalization verification evidence.
- Verify-ShortCapture.ps1 performs ffprobe inspection and ffmpeg full decode as
  an exploratory probe using commands resolved from PATH. That is not a pinned,
  reviewed or SBOM-bound Coordinator runtime dependency.

## Prerequisite decisions — resolved by accepted ADR-0027

1. Version a minimal event archive using existing source-state-event records and
   a finalization summary bound to capture/source identity, terminal event and
   declared outcome. Define complete/incomplete and missing-evidence handling;
   do not reinterpret historical fixtures or silently upgrade packages.
2. Use a controlled FFmpeg/ffprobe worker for full decode rather than silently
   invoking arbitrary PATH binaries. Select exact runtime/build provenance,
   license/supply-chain review, SBOM coverage, cancellation/time/resource bounds,
   and supported media profile. Alternative: a Windows Media Foundation worker;
   its decode/error/reporting behavior would need separate implementation/tests.

User continuation authorizes the minimal schemas and controlled decoder baseline.
ADR-0027 resolves the circular-hash issue through an archive projection rather
than a full wire-message archive. Two schemas and HC-ART-TEST-001–010 are implemented.
FFmpeg 9.0.1 is archive-pinned and inventoried as a disabled planned dependency;
the archive has not been downloaded or runtime-qualified. Seven SBOM controls
include rejection of unpinned or silently activated decoder candidates.
Missing required runtime checks must remain NOT_ASSESSED/failed, never PASS.
No camera access or capture-data mutation occurred. AI contribution: inspection,
design, schemas/conformance tests and documentation; independent review is open.

Evidence: ../../verification/I0_4B_C14A_VERIFIER_BASELINE_REPORT.md.

C14A local and hosted engineering verification passed: 101 contract, 7 SBOM and
135 repository runtime tests; zero-warning/error build. Source
dcd6bd87901602450020747726b0653529722ac3 passed CI 34975585870 and 34975589810.
C14B remains: obtain/verify exact decoder binaries, implement the bounded worker
and independent verifier, and test actual media. C14 overall is not complete.

# C14 Package Verifier — Approved Objective and Baseline Gaps

ID: HC-CHG-20260915-002. Date: 2026-09-15.
Status: C14 objective and prerequisite choices approved; C14A contract baseline
and C14B engineering decoder probe implemented. C14C internal bounded worker now
implemented and locally verified; full runtime package verifier remains incomplete.

Approved: independently decode collected video, check timing/camera/events/
finalization and applicable IMU evidence, preserve measured fixed/variable cadence,
and produce a versioned verification record. Only all required checks passing may
permit VERIFIED. No automatic commit, receipts, source modification or deletion.

Primary owner: Coordinator/repository engineer. Affected: Architect, Android,
UVC, QA, Risk/Regulatory and Release/SBOM. Verification owner: engineering QA.
Classification: cross-component artifact contract and runtime dependency decision.
Existing HC-IF-XFR-001, HC-IF-TIM-001 and HC-RISK-022/030/031 apply.
No regulatory applicability change or conformity claim.

## Historical prerequisite inspection (before C14A)

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
C14B now supplies downloaded archive/executable identity and seven synthetic media
checks plus four guard regressions. This bounded engineering slice does not implement
the production worker. C14C next: asynchronous bounded decoder worker with cancellation
and failure tests, then independent scientific artifact integration. Full C14 remains
incomplete; no automatic verification, admission, commit or cleanup is introduced.

Primary owner: Coordinator engineer; affected Release/SBOM and QA. Scope: isolated
probe, disabled decoder lock observation, SBOM evidence and documentation under
HumCapture. Classification: local engineering qualification under ADR-0027, no
application interface/persistence/claim change. Risk controls HC-RISK-022/030/031
remain unfulfilled until all required runtime package checks are integrated.
Evidence: ../../verification/I0_4B_C14B_DECODER_REPORT.md. AI implemented and tested
this slice; independent human review remains open.

C14B source 20299681cc485eb5b9ebda0030d818c0fc731839 passed push CI34981598252
and PR CI34981605120. Local real-decoder/synthetic-media proof remains separate
from hosted guard/regression tests. Source, SBOM and evidence changes are confined
to HumCapture; the candidate is not installed into or enabled by the Coordinator.

## C14C — internal bounded worker, 2026-09-16

Continuation authorizes the next planned engineering step. ADR-0028 records the
internal execution boundary, trade-offs and owner review scope. Primary Coordinator
engineer; affected Architect, QA, Release/SBOM and Risk. No new external protocol,
persistence format, dependency version, intended use or regulatory claim. Existing
India preliminary baseline remains applicable; this is not a medically positioned release.

Internal source implemented, no host entry point: embedded pinned identity, read-only
leases, serial queue, asynchronous pipe drain, deadline/cancellation/overflow outcomes,
bounded cleanup and conservative success predicate. Seven new subprocess/guard cases
bring runtime tests to 142. Four real pinned-decoder synthetic cases passed. SBOM
regenerated because the Coordinator project embeds the lock. Full scientific package
verification, deployment/library/licence review and activation remain open for C14D+.
Evidence: ../../verification/I0_4B_C14C_DECODER_WORKER_REPORT.md.
AI implementation/testing/documentation; independent human review pending.

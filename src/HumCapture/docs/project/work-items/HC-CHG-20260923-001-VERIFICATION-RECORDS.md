# C14U-W: Verification records and repository integration

User authorized the related batch. Scope remains HumCapture only: produce
evidence-bound verification records and integrate with existing staged admission;
retain test evidence, traceability and supply-chain records. Host activation,
automatic package movement, receipts and source cleanup remain excluded.

## Current gate

Preflight identified a coverage-contract gap, documented in ADR-0037 and
HC-VR-I0-4B-C14UVW-PREFLIGHT-001. User accepted the clarification on 2026-09-23.
TIMING_COVERAGE_V1_1.md is baselined before implementation. Batch completion
requires implementation and recorded checks; it is not established by approval.

The architecture skill informed the trade-off review and requires clarification
when success criteria are missing. Acceptance now follows explicit user approval.
Primary Architect/Repository engineer; affected Timing, Android/UVC, QA, Risk
and Release/SBOM owners. Required tests include mismatched coverage, interleaved
IMU lanes, segment changes, legacy NOT_ASSESSED, decoder failure, admission
retry/conflict and no movement/receipt/cleanup side effects.

Implementation: manifest 1.1 / XFR 1.3 coverage schema and native reconstruction;
private record construction from leased real-decoder results; internal
VerifyAndAdmitAsync using existing durable staged admission. No new host API.
All 201 runtime, 115 contract, 13 SBOM/register and five actual-decoder synthetic
cases pass with a clean Release build. See HC-VR-I0-4B-C14UVW-001. The new
embedded schema resources change the build surface; SBOM/register refreshed to
c14w with no new dependency. No hardware or regulatory evidence is claimed.

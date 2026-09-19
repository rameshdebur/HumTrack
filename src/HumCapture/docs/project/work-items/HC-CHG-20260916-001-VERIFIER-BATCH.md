# Approved verifier integration batch

Date: 2026-09-16. User authorized continuing related work without per-slice
approval, including short local UVC captures, until a genuine decision or physical
action is required. Modification boundary remains src/HumCapture only.

Primary: Coordinator/repository engineer. Affected: timing/Architect, Android,
UVC, QA, Risk and Release/SBOM. Verification owner: engineering QA. Classification:
cross-component implementation of accepted HC-IF-TIM-001 and C14 contracts.
Existing ARD/PRD/SRS, roles, governance, preliminary India baseline and
HC-RISK-022/030/031 apply. No intended-use or regulatory claim change.

Implement independent finalized frame/IMU binary readers, exact video association,
and continue metadata/common-verifier integration where existing authority permits.
Correct contract-test defects without silently changing schema versions. Preserve
fixed/variable cadence, source values, explicit missing evidence and rejected takes.
No host activation, automatic commit, receipt or source cleanup follows from these
internal checks. No NDI device is assumed. Captures require identifying the local
UVC source first; synthetic results never establish hardware acceptance.

Record tests, risk/traceability, SBOM and project state together; scoped commits/push
are authorized. Stop for new dependency/licensing/deployment decisions, material
contract changes, unavailable hardware or physical/operator observations. AI
implementation/review is not independent human or regulatory approval.

Implemented boundary and tests: ADR-0030 / HC-VR-I0-4B-C14E-001. Local build,
154 runtime / 105 contract / 7 SBOM tests and CycloneDX validation pass. Next
decision: HC-DECISION-20260916-METADATA-VALIDATION.md. No package dependency,
camera use, public contract change or automatic verification/commit introduced.

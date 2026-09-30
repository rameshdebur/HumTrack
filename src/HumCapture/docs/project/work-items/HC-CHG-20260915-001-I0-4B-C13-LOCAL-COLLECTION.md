# C13 Local Collection Change Record

ID: HC-CHG-20260915-001. Date: 2026-09-15.
State: Implemented; local and hosted engineering verification passed.

Scope/architecture review: ADR-0026 and HC-IF-COL-LOCAL-001 1.0.0.
Primary: Coordinator/repository engineer. Affected: Architect, QA, Risk/Regulatory,
Release/SBOM. Verification owner: engineering QA. Additive host/API and staging
persistence; existing transfer checkpoint schema, no repository schema migration.
All changes remain in HumCapture.

Requirements: HC-DATA-REQ-001–003/009–012; risks HC-RISK-022/030/031.
Existing engineering/governance/India baseline applies. No changed intended use,
medical claim, clinical or certification decision. Independent qualified review
and controlled release remain open. AI contribution: design, implementation,
tests and documentation; this is not independent human approval.

Verification: HC-REP-RUNTIME-126–135 plus prior regressions, build/static,
contracts and SBOM. See ../../verification/I0_4B_C13_LOCAL_COLLECTION_REPORT.md.

135 runtime, 91 contract and 6 SBOM tests pass; build has zero warnings/errors.
Source 64121a53f09b970d004eaaafcbd4bfa5d141f3d1 passed hosted runs
34968227193 and 34968233021. No HIL, field or independent human acceptance.

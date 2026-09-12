# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260912-013  
**Title:** C8 bounded Coordinator startup entry point  
**State:** Local implementation verified; CI and independent review pending  
**Date:** 2026-09-12

User authorization: "lets do it" following the proposal to connect recovery
to Coordinator startup. Scope is the existing Coordinator assembly, not a new
executable, service installation, UI, scheduler or capture runtime.

Primary owner: Coordinator/repository engineer. Affected owners: Architect,
QA, Risk/Regulatory and Release/SBOM. Verification owner: Engineering QA.
Classification: local orchestration of accepted HC-IF-REP-001 1.3.0 behavior.
Architecture review: reuse the current process-local repository gate and C4/C7
reconciliation. One bounded page per invocation, at most one action per
transaction. No new process, persistence format, dependency or state transition.
ADR-0020 remains authoritative. Independent human review remains pending.

Engineering baseline for this scope: accepted ARD/PRD/user stories,
HC-DATA-REQ-033–038, existing roles/governance, preliminary India/CDSCO baseline
and accepted repository ADRs/interfaces. No intended-use, clinical claim,
privacy boundary or regulatory applicability change; qualified release review
remains required. HC-RISK-022/030/031 controls remain in force.

The startup entry point opens/compatibility-checks the root, gets the current
Windows identity, discovers one bounded page and reconciles candidates under
the repository gate. Cancellation is checked between transactions, never inside
finalization. Each result separates reconciliation success from workflow
completion; pending STAGED_VERIFIED/MOVED work stays pending.
Failures preserve material and return a controlled error and safe next action.
Unsupported repositories remain inspection-only. Continuation cursors are
in-memory progress, not durable receipt or whole-repository readiness evidence.

AI contribution: implementation, tests, architecture review and documentation.
No independent or regulatory approval is implied.

HC-REP-RUNTIME-088–095 pass; total 95 runtime tests, 91 contract tests and
6 SBOM tests. Release build passes with zero warnings/errors. No new dependency.
See HC-VR-I0-4B-C8-001 for limitations and CI evidence.

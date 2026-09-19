# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260912-012  
**Title:** C7 startup discovery and later-state recovery  
**Date:** 2026-09-12  
**State:** Local and remote implementation verified; independent review pending

The user authorized the next repository slice. Primary owner: Coordinator
repository engineer. Affected owners: Architect, QA, Risk/Regulatory and
Release/SBOM. Verification owner: Engineering QA.
Classification: local persistence implementation under HC-IF-REP-001 1.3.0
and accepted ADR-0016–0022, especially ADR-0020 automatic reconciliation.
No schema, persistence format, dependency, intended-use, regulatory
applicability or scientific timing guarantee changes.

Traceability: HC-US-XFR-021–034; HC-DATA-REQ-030–038;
HC-RISK-022/030/031. All edits remain within src/HumCapture.

Architecture review: retain the existing immutable-record-before-index
publication order and SQLite atomic finalization. Reconstruct identity from
retained catalog/final-transition history rather than the latest reconciliation
pointer, which legitimately changes after confirmation. Existing public C6
retry semantics and early C4 reconciliation remain compatible. STARTUP
FINALIZE_COMMIT and CONFIRM_IDEMPOTENT_COMMIT already exist in the contract.

Scope: bounded read-only transaction discovery, CATALOGED finalization,
COMMITTED fresh confirmation, retained orphan reuse and fresh replay checks.
No host lifecycle loop, automatic MOVED-to-CATALOGED publication, receipts,
source deletion, session completion, operator quarantine or UI.

AI contribution: source, regression tests, architecture/source review and
controlled-record drafting. Independent human review remains pending.
See HC-VR-I0-4B-C7-001 for commands, results and evidence limitations.

Implementation: `282977955f9ab8ed929b0021d5103396ccc97ab5`.
CI push `34686843552` and PR `34686845066` passed on that source.

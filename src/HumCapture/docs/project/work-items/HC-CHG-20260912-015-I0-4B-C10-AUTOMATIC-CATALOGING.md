# C10 Automatic Cataloging Change Record

**ID:** HC-CHG-20260912-015  
**Date:** 2026-09-12  
**State:** Local and remote engineering verification passed; independent review pending

User authorized the next automatic catalog-recovery step. Primary owner:
Coordinator/repository engineer. Affected: Architect, QA, Risk/Regulatory and
Release/SBOM. Verification owner: Engineering QA. All writes stay in HumCapture.

Classification: persistence implementation of accepted HC-IF-REP-001 1.3.0
and ADR-0020 COMPLETE_CATALOGING; no schema, dependency or deployment change.
Architecture review: one atomic SQLite transaction for catalog, observations,
reconciliation and transition. Recovered STARTUP publication history must remain
verifiable by later finalization; do not relabel it NORMAL or relax normal replay.
Existing MOVED inspection with no result identities remains compatible.

Baseline: accepted design/requirements/governance and preliminary India baseline
as recorded for C1–C9. Trace HC-US-XFR-021–034, HC-DATA-REQ-033–038,
HC-RISK-022/030/031. No intended-use, subject-data boundary, timing, regulatory
applicability or medical claim change. Independent review remains pending.

Scope: exact MOVED evidence to CATALOGED at startup, durable history and replay,
host integration. One action per package per pass. No automatic move from
STAGED_VERIFIED, receipts, cleanup, UI, forced state or conflict deletion.
AI contribution: implementation, tests, architecture/source review and records.

HC-REP-RUNTIME-103–109 pass; total 109 runtime tests, 91 contracts and six
SBOM tests. Release build has zero warnings/errors. See HC-VR-I0-4B-C10-001.

Implementation: `94e9242cad2c64c80958d2c7581fbc7fa3d45566`.
Push CI `34692584529` and PR CI `34692586207` passed on that source.

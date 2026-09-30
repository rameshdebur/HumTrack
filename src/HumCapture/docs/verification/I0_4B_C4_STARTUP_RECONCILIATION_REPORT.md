# I0.4B-C4 Startup Reconciliation Verification Report

**Verification ID:** HC-VR-I0-4B-C4-001  
**Date:** 2026-09-11  
**Status:** Local and exact-commit remote implementation verification passed; independent review pending

## Object under verification

The headless Windows Coordinator repository observes the six authorities fixed
by HC-IF-REP-001 at startup and performs only the exact automatic recovery
actions supported by the implemented C2/C3 boundary. It retains one append-only
reconciliation, all observations, and any state-changing transition atomically.

C4 does not publish the catalog, decide final `COMMITTED`, create a receipt,
authorize cleanup, move material to quarantine, or implement an operator UI.

## Automated results

- Strict Release build: 0 warnings and 0 errors.
- HC-REP-RUNTIME-001–056: 56/56 passed on Windows; tests 046–056 cover C4.
- Existing evidence/control tests: 91/91 passed.
- Phase 0 capability regression: 55/55 passed.
- SBOM policy tests: 6/6 passed.
- CycloneDX 1.7 SBOM: 31 components, 32 dependency nodes, SHA-256
  `c35505dabe44602dce788065ed20690af69738922ae62104973c19c2052ec911`;
  project validation and official CycloneDX CLI 0.33.1 validation passed.
- Both Node production dependency audits report zero vulnerabilities.
- NuGet current-source vulnerable and deprecated package queries report no
  findings when run separately.
- `git diff --check` passed.

## C4 failure-path evidence

HC-REP-RUNTIME-046–056 verify six-authority `NO_ACTION` for directly proven
staged and moved states, interrupted pre-move `RETRY_FROM_STAGED`, interrupted
post-move `RESUME_AFTER_MOVE`, exact replay, reconciliation-identity conflict,
dual-path refusal, changed-package refusal, unexpected catalog-linkage refusal,
transactional rollback, and a successful C3 retry with contiguous revisions
after reconciliation.

## Evidence classification

- Source implemented: yes.
- Build/static checks passed: yes.
- Automated behavior verified: yes.
- Runtime integration verified: local Windows process/filesystem/SQLite only.
- Hardware-in-the-loop verified: no.
- Field workflow verified: no.
- Regulatory or clinical review completed: no.

## Limitations

Failures are injected at controlled transactional boundaries; abrupt process
termination and Windows/power loss are not tested. Conflicting evidence is
preserved and rejected with a specific recovery-required result, but persistent
operator classification and quarantine actions remain unimplemented. Catalog
publication, final commit, receipts, cleanup, cross-process writer exclusion,
backup/restore, HIL, field, independent and qualified regulatory review remain
open.

Implementation commit: `e7e2a2e50aacb4acf71b0c0ac3c495bb34a04e39`.
Exact-commit remote CI: push run `34609304657` and PR run `34609310422`
passed, including managed and native camera regressions.

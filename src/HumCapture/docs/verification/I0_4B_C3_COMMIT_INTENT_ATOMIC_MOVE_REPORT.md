# I0.4B-C3 Commit Intent and Atomic Move Verification Report

**Verification ID:** HC-VR-I0-4B-C3-001  
**Date:** 2026-09-11  
**Status:** Local and exact-commit remote implementation verification passed; independent review open

## Object under verification

The headless Windows Coordinator repository advances an exact verified package
from `STAGED_VERIFIED` through durable `COMMITTING` intent to `MOVED`. It uses a
same-volume, non-overwriting, write-through Windows directory rename and then
revalidates the package at its canonical destination.

C3 stops at `MOVED`. It does not publish catalog milestones, decide
`COMMITTED`, issue receipts, complete a session or authorize source cleanup.

## Automated results

- Release build under inherited analyzers/warnings-as-errors: 0 warnings and
  0 errors.
- HC-REP-RUNTIME-001–045: 45/45 passed on Windows; tests 032–045 cover C3.
- Existing evidence/control tests: 91/91 passed, including HC-REP-TEST-001–015
  and HC-XFR-TEST-001–011.
- Phase 0 capability regression: 55/55 passed.
- SBOM policy tests: 6/6 passed.
- CycloneDX 1.7 SBOM: 31 components, 32 dependency nodes, SHA-256
  `58161eca97eaa69c14ebfb47d66f7f814c63f241f4bca94b43beba35545f0dfd`;
  project validation, retained hash readback and official CycloneDX CLI 0.33.1
  validation passed.
- Both Node production dependency audits report zero vulnerabilities.
- NuGet current-source vulnerable and deprecated package queries report no
  findings when run as the required separate commands.
- Managed camera regression: release build and 7/7 self-tests passed.
- Native Media Foundation regression: both projects built and 8/8 capture
  self-tests passed. Local MSBuild warning MSB8029 reflects only that the
  isolated worktree resides below the Windows temporary directory.
- 88 JSON documents parse and `git diff --check` passes.

## C3 failure-path evidence

HC-REP-RUNTIME-032–045 cover normal durable intent and move, exact contiguous
content-bound transitions, exact idempotent replay, changed staged bytes,
pre-existing destination with no overwrite, SQLite rollback before durable
intent, retained `COMMITTING` after a post-move journal failure, both observed
interruption locations, moved-package tampering, conflicting operation replay,
absent transaction, read-only compatibility and current/history disagreement.

## Evidence classification

- Source implemented: yes.
- Build/static checks passed: yes; the strict Release build is authoritative.
- Automated behavior verified: yes.
- Runtime integration verified: local Windows process/filesystem/SQLite only.
- Hardware-in-the-loop verified: no; camera self-tests are regression only.
- Field workflow verified: no.
- Regulatory or clinical review completed: no.

## Limitations

The test harness injects failures at transactional boundaries; it does not kill
the process or interrupt Windows/power during `MoveFileExW`. Cross-process
writer exclusion, actual OS/power-loss durability, removable-storage behavior,
startup reconciliation, quarantine, `CATALOGED`, `COMMITTED`, receipts,
cleanup, backup/restore, HIL, field, independent and qualified regulatory
review remain open. A supplemental full-tree formatting check also reports
pre-existing repository line-ending and naming-policy drift; no broad
normalization was performed, while the strict build and targeted tests pass.

Implementation commit: `0b715d34d4b854eec7e4ebefd0a73e0d83e70f80`.
Exact-commit remote CI: push run `34595856604` and PR run `34595859162`
passed.

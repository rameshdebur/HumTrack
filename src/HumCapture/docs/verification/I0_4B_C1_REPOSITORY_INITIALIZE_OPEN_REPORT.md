# I0.4B-C1 Repository Initialize/Open Verification Report

**Verification ID:** HC-VR-I0-4B-C1-001  
**Date:** 2026-09-10  
**Status:** Local implementation verification passed; independent review open

## Object under verification

The headless Windows Coordinator repository library initializes the exact
HC-IF-REP-001 1.3.0 namespace/catalog and opens repositories through an
explicit mutation-versus-read-only compatibility decision.

## Automated results

- Release build under inherited analyzers/warnings-as-errors: 0 warnings and
  0 errors.
- HC-REP-RUNTIME-001–017: 17/17 passed on Windows.
- Existing evidence/control tests: 91/91 passed, including HC-REP-TEST-001–015.
- SBOM policy tests: 6/6 passed after adding NuGet/project discovery.
- Regenerated CycloneDX 1.7 SBOM: 31 components, 32 dependency nodes,
  SHA-256 `d650ce9089e8bff62f26843c0629aebe71ae537bb317109a1c0c3c1bf9d67552`;
  project and official CycloneDX CLI 0.33.1 validation passed.
- NuGet current-source vulnerable-package query: no findings.
- NuGet current-source deprecated-package query: no findings.
- Exact implementation commit
  `7594e4ee2e1670ad7457c169d0a9833f5ec74bbc` passed HumCapture CI push run
  `34509901600` and PR run `34509907387`.

The runtime tests create real temporary directories and SQLite files, execute
the embedded accepted DDL, query catalog metadata, check supported reopen,
prove catalog bytes/write time are unchanged on read-only compatibility paths,
and exercise missing/corrupt/mismatched, hard-link and retained-lock failure
paths.

## Evidence classification

- Source implemented: yes.
- Build/static checks passed: yes.
- Automated behavior verified: yes.
- Runtime integration verified: local Windows process/filesystem/SQLite only.
- Hardware-in-the-loop verified: no.
- Field workflow verified: no.
- Regulatory or clinical review completed: no.

## Limitations

The tests do not establish storage survival after OS crash or power removal,
nor do they cover repository package commits, reconciliation, backup/restore,
UI workflow, deployment packaging, HIL or field operation. File flush plus
same-directory atomic publication is implemented and process-tested, but a
power-loss durability claim remains explicitly open.

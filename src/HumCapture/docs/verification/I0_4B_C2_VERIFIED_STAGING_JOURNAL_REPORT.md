# I0.4B-C2 Verified Staging Journal Verification Report

**Verification ID:** HC-VR-I0-4B-C2-001  
**Date:** 2026-09-10  
**Status:** Local and exact-commit remote implementation verification passed; independent review open

## Object under verification

The headless Windows Coordinator repository admits an already-collected package
to the initial `STAGED_VERIFIED` durability boundary only after rechecking the
staged package, manifest, immutable verification record and canonical identity
bindings. It then atomically creates the current journal row, sequence-1
transition and verification-record index in SQLite.

This is a repository admission boundary. It does not collect packages or
perform the common verifier's media decode.

## Automated results

- Release build under inherited analyzers/warnings-as-errors: 0 warnings and
  0 errors.
- HC-REP-RUNTIME-001–031: 31/31 passed on Windows; tests 018–031 cover C2.
- Existing evidence/control tests: 91/91 passed, including HC-REP-TEST-001–015
  and HC-XFR-TEST-001–011.
- Phase 0 capability regression: 55/55 passed.
- SBOM policy tests: 6/6 passed.
- CycloneDX 1.7 SBOM: 31 components, 32 dependency nodes, SHA-256
  `a9f22faa53f26c4e3e533a966c8f241238475d0c6e758b2414e14841f8534267`;
  project validation, retained hash readback and official CycloneDX CLI 0.33.1
  validation passed.
- Both Node production dependency audits report zero vulnerabilities.
- NuGet current-source vulnerable and deprecated package queries report no
  findings.
- Managed camera regression: release build and 7/7 self-tests passed.
- Native Media Foundation regression: both projects built and 8/8 capture
  self-tests passed. Local MSBuild warning MSB8029 reflects only that the
  isolated worktree resides below the Windows temporary directory.
- 88 JSON documents parse and `git diff --check` passes.
- Independent Node canonicalization produced the two fixed RFC 8785 hashes used
  as the .NET staging-test oracle.

## C2 failure-path evidence

HC-REP-RUNTIME-018–031 cover successful admission, exact idempotent replay,
missing stage, pre-existing destination, changed artifact bytes, false aggregate
verification, artifact-result mismatch, verification-record hash mismatch,
unknown verification field, transaction/package identity conflict, immutable
record collision, read-only compatibility, hard-linked staged artifacts and
SQLite rollback when the final index insertion fails.

## Evidence classification

- Source implemented: yes.
- Build/static checks passed: yes.
- Automated behavior verified: yes.
- Runtime integration verified: local Windows process/filesystem/SQLite only.
- Hardware-in-the-loop verified: no; camera self-tests are regression only.
- Field workflow verified: no.
- Regulatory or clinical review completed: no.

## Limitations

C2 begins only after a future transfer/common-verifier service has stopped
writing and produced the verification record. It does not implement collection,
media decoding, `COMMITTING` or later states, startup reconciliation, quarantine
moves, receipts, cleanup, backup/restore, external concurrent-writer exclusion,
abrupt process termination, OS crash or power-loss survival. File flush and
SQLite `synchronous=FULL` are process-tested controls, not a power-loss claim.

Implementation commit: `dfda300c754f8e99a084aac47acd423bf2b6fd8b`.
Exact-commit remote CI: push run `34514312741` and PR run `34514321335`
passed.

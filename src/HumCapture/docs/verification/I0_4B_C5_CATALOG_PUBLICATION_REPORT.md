# C5 Catalog Publication Verification

**Verification ID:** HC-VR-I0-4B-C5-001  
**Date:** 2026-09-12  
**Disposition:** Local engineering checks passed; remote checks pending

PublishMovedPackage requires MOVED, exact final package and verification
evidence, absent staging, and no conflicting catalog or indexed commit
evidence. It atomically inserts a catalog entry and CATALOGED transition with
the current journal update. Catalog identity, all package bindings, account,
operation identity and timestamp must match on replay. Publication does not
produce a commit record or receipt.

| Test | Objective | Result |
|---|---|---|
| HC-REP-RUNTIME-057 | Publication and reopen replay retain one exact catalog row and unchanged package | Pass |
| HC-REP-RUNTIME-058 | Injected transition failure rolls back catalog/state/history and preserves destination | Pass |
| HC-REP-RUNTIME-059 | Reject premature STAGED_VERIFIED publication | Pass |
| HC-REP-RUNTIME-060 | Reject altered final artifact | Pass |
| HC-REP-RUNTIME-061 | Preserve and reject existing catalog conflict | Pass |
| HC-REP-RUNTIME-062 | Reject catalog/operation identity changes on replay | Pass |
| HC-REP-RUNTIME-063 | Refuse mutation with unsupported descriptor version | Pass |

Repository runtime tests: 63/63. Contract tests: 91/91.
Strict Release build of repository and test project: zero warnings/errors.
SBOM retains 31 components/32 dependency nodes; version 0.1.0-i0.4b-c5.
SHA-256: 5d7c6f2110ad2946fe805a246037fab60a473a17666397220a2b6a6dcd9ddbd0.
Generation UTC: 2026-09-12T07:16:40.526Z.
Official CycloneDX CLI 0.33.1 accepts the BOM.

Evidence levels: source implemented; build/static and automated behavior
verified; runtime integration covers a local Windows process, filesystem and
SQLite. Abrupt process/OS/power loss, cross-process exclusion, startup support
for CATALOGED, final commit/receipt, HIL, field and independent/regulatory
review are not verified by this slice.

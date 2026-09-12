# C8 Startup Orchestration Verification

**Verification ID:** HC-VR-I0-4B-C8-001  
**Date:** 2026-09-12  
**Disposition:** Local engineering verification passed; CI/independent review pending

Objective: connect compatibility/open, current Windows account, bounded
discovery and per-package reconciliation through one host-callable entry point
in the existing Coordinator assembly. No new executable or deployment surface.

| Test | Objective / fault | Result |
|---|---|---|
| HC-REP-RUNTIME-088 | Empty repository startup without mutation | Pass |
| HC-REP-RUNTIME-089 | Actual Windows identity, cataloged finalization, reopened confirmation | Pass |
| HC-REP-RUNTIME-090 | One-item page, cursor continuation, staged work remains incomplete | Pass |
| HC-REP-RUNTIME-091 | Malformed committed record with another valid transaction | Pass; failed bytes retained, other item reconciled |
| HC-REP-RUNTIME-092 | Pre-cancelled startup | Pass; zero actions and unchanged repository |
| HC-REP-RUNTIME-093 | Unsupported repository version | Pass; inspection-only, no mutation |
| HC-REP-RUNTIME-094 | Interrupted COMMITTING with retained staging | Pass; RETRY_FROM_STAGED, not forced commit |
| HC-REP-RUNTIME-095 | Invalid page limits | Pass; rejected before mutation |

Executed from HumCapture root:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore -warnaserror
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
```

95/95 repository tests, 91/91 contract tests, 6/6 SBOM tests pass.
Build has zero warnings/errors. Official CycloneDX CLI 0.33.1 validation passes.
SBOM 0.1.0-i0.4b-c8: 31 components, 32 graph nodes; timestamp
2026-09-12T10:26:38.644Z; SHA-256
e435f9daa97eb34221988649d8226e7e282fd96a8251a8daacb8c8e64ef4b0f6.
Initial static findings (nested conditional and empty test catch) were corrected
before passing verification.

Evidence: source, static/build, automated behavior and Windows
process/filesystem/SQLite integration. No executable-host launch, mid-pass
cancellation injection, cross-process concurrency, process-kill/power-loss,
HIL, field or qualified regulatory/clinical review. No certification claim.

Completed describes candidate-page exhaustion only. Callers must aggregate
RequiresOperatorAttention across pages; failed items advance the cursor.
Failure results are in-memory, not a new durable failure journal.
Root/open/discovery failures propagate. Cancellation waits for the current
transaction; page size bounds count, not bytes or elapsed time. Audit request
timestamps are not measured reconciliation duration. Host invocation belongs
before capture and off the UI/acquisition thread.

Next integration remains an executable Coordinator host and explicit readiness/
failure presentation; automatic MOVED-to-CATALOGED, receipt services, source
cleanup, backup/restore and whole-session completion remain separate work.

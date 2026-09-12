# C10 Automatic Cataloging Verification

**ID:** HC-VR-I0-4B-C10-001  
**Date:** 2026-09-12  
**Disposition:** Local engineering verification passed; CI/independent review pending

Objective: recover an exact MOVED package to CATALOGED under the accepted
COMPLETE_CATALOGING action. Require exact package/verification and expected
absences, refuse unexpected commit files, then atomically save catalog,
six pre-action observations, STARTUP reconciliation and transition. Validate
recovered publication history again before final commit. Preserve normal
publication replay and existing observation-only MOVED startup calls.

| Test | Objective / fault | Result |
|---|---|---|
| HC-REP-RUNTIME-103 | Atomic catalog evidence and exact reopen replay | Pass |
| HC-REP-RUNTIME-104 | Real host passes: catalog, final commit, confirmation | Pass |
| HC-REP-RUNTIME-105 | Abort CATALOGED transition in SQLite then retry | Pass; catalog/reconciliation rolled back |
| HC-REP-RUNTIME-106 | Alter moved package | Pass; rejected, no catalog row |
| HC-REP-RUNTIME-107 | Unexpected matching commit file | Pass; rejected, original bytes retained |
| HC-REP-RUNTIME-108 | Corrupt retained catalog observations | Pass; finalization refused |
| HC-REP-RUNTIME-109 | Replay catalog recovery after final commit with missing commit file | Pass; refused |

The first run of test 108 was blocked by the append-only trigger. The synthetic
fixture now explicitly drops that trigger to inject corruption; no production
protection was weakened. The entire rerun passes: 109/109 runtime tests.
Existing C5/C6/C7/C8/C9 tests remain passing.

Executed from HumCapture:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore -warnaserror
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
```

91/91 contract tests, 6/6 SBOM tests, zero build warnings/errors.
Official CycloneDX CLI 0.33.1 accepts the C10 BOM: 32 components/33 graph nodes,
generated 2026-09-12T11:59:53.212Z, SHA-256
e30ed6dcd17c4e3e1532aee152cfbc84d63e8c5bc689b7a1e4a4287b0e2edce9.
No schema or dependency change.

Source, static/build, automated behavior and Windows child-process/filesystem/
SQLite integration verified. Injected SQL failure is not abrupt process/OS/
power-loss testing. No new HIL, field, clinical or regulatory evidence.
Normal staged-move continuation, receipts/cleanup, capture-readiness and UI
remain separate. Host performs only one action per package per pass.

# C9 Executable Host Verification

**ID:** HC-VR-I0-4B-C9-001  
**Date:** 2026-09-12  
**Disposition:** Local and remote engineering verification passed; independent review pending

Objective: execute the Coordinator startup API as a real Windows child process,
with explicit root selection, bounded output and non-success exits for recovery
problems. The self-test launches the built host DLL through dotnet with redirected
output, no visible window and a 60-second test timeout.

| Test | Objective | Result |
|---|---|---|
| HC-REP-RUNTIME-096 | Empty root, JSON version, no mutation or root-path leak | Pass |
| HC-REP-RUNTIME-097 | Missing/duplicate/invalid arguments and absent repository | Pass; no implicit creation |
| HC-REP-RUNTIME-098 | Real-process finalization followed by reopened confirmation | Pass |
| HC-REP-RUNTIME-099 | Bounded page and cursor continuation | Pass |
| HC-REP-RUNTIME-100 | Unsupported version | Pass; exit 6 and unchanged bytes |
| HC-REP-RUNTIME-101 | Malformed committed evidence | Pass; exit 4, retained bytes |
| HC-REP-RUNTIME-102 | Another process's same-session guard | Pass; exit 7 |

Commands from HumCapture:

```powershell
dotnet restore apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -p:RestoreLockedMode=false
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
```

Unlocked restore was used only to generate/reconcile the newly introduced
first-party project lock files. Subsequent verification uses locked restore.
102/102 runtime tests, 91/91 contracts and 6/6 SBOM tests pass.
Build: zero warnings/errors after brace-style findings were corrected.
Project validator and official CycloneDX CLI 0.33.1 accept the C9 inventory:
32 components / 33 graph nodes; 2026-09-12T11:41:13.083Z;
SHA-256 800e2b87f5169144c410478f3311ffe53672f562fbcdd452e8d0570edd5a3b7b.

Source, build/static, automated behavior and actual Windows child-process/
filesystem/SQLite integration verified. No camera activation, HIL, field,
Ctrl+C signal injection, abandoned mutex, process-kill/power-loss, installer,
cross-session exclusion, clinical or qualified regulatory review.
DLL-based process execution does not establish deployed apphost installation.
The local built Windows .exe was also launched with an invalid relative root:
it emitted versioned INVALID_ARGUMENTS JSON and exit 2 without repository writes.
Completed is scan exhaustion, not capture readiness or session completion.
Failure results remain caller-visible output rather than a durable failure log.

Implementation: `501dd224262b4b632309c224ef4c73c5e94fa23c`.
[Push CI 34691808768](https://github.com/rameshdebur/HumTrack/actions/runs/34691808768)
and [PR CI 34691810332](https://github.com/rameshdebur/HumTrack/actions/runs/34691810332)
passed, including managed/native camera builds and self-tests.
Both npm production audits and host NuGet vulnerable/deprecated queries
reported no current findings.

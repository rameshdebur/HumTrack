# C7 Startup Recovery Verification

**Verification ID:** HC-VR-I0-4B-C7-001  
**Date:** 2026-09-12  
**Disposition:** Local and remote engineering verification passed; independent review pending

## Objective and implementation

Discover journal candidates without mutation and recover later-state work
without requiring the interrupted process's in-memory request.
ListStartupTransactions uses stable transaction-ID pagination with a maximum
of 1000 entries per call. Stored state is not an evidence verdict.

CATALOGED recovery verifies retained publication history and package evidence,
finds zero or one matching unindexed commit record and finalizes through the
existing atomic record-index/observation/reconciliation/transition transaction.
An existing record must match expected bytes, with its original actor/time
retained. New startup actor/time describe the recovery separately.
COMMITTED confirmation revalidates current evidence and retains six observations
without increasing state revision. Historical finalization is validated through
its own transition/reconciliation, not the mutable latest-reconciliation pointer.
Replaying a successful startup request still rechecks current evidence.

## Results

| Test | Objective and fault condition | Outcome |
|---|---|---|
| HC-REP-RUNTIME-077 | Paginate candidates without repository mutation | Pass |
| HC-REP-RUNTIME-078 | Complete CATALOGED after reopening without original request | Pass; COMMITTED and six observations |
| HC-REP-RUNTIME-079 | Recover orphan record from failed finalization | Pass; identical bytes and original audit retained |
| HC-REP-RUNTIME-080 | Repeat fresh COMMITTED confirmations and original C6 replay | Pass; revision unchanged |
| HC-REP-RUNTIME-081 | Remove a committed record before startup | Pass; refused without recreation |
| HC-REP-RUNTIME-082 | Alter record after startup confirmation, then replay | Pass; conflict refused |
| HC-REP-RUNTIME-083 | Malformed orphan record | Pass; recovery blocked, bytes preserved |
| HC-REP-RUNTIME-084 | Multiple matching orphan records | Pass; ambiguity refused |
| HC-REP-RUNTIME-085 | Abort startup final transition in SQLite, then retry | Pass; rollback and retained-byte recovery |
| HC-REP-RUNTIME-086 | Unsupported descriptor version | Pass; discovery refused |
| HC-REP-RUNTIME-087 | Alter package after startup finalization, then replay | Pass; current evidence mismatch refused |

Commands executed from the HumCapture root:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release -warnaserror --no-restore
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
```

Results: 87/87 repository runtime tests, 91/91 contract tests, 6/6 SBOM tests.
Release build: zero warnings/errors. Project and official CycloneDX CLI 0.33.1
validation pass. SBOM 0.1.0-i0.4b-c7 has 31 components/32 graph nodes,
generated 2026-09-12T09:47:03.168Z, SHA-256
814b3adebe9473b3aa50872aa330669541c863b76e864bfc6f4f3c113399ed27.
No dependency added.

Implementation commit: `282977955f9ab8ed929b0021d5103396ccc97ab5`.
HumCapture CI [push 34686843552](https://github.com/rameshdebur/HumTrack/actions/runs/34686843552)
and [PR 34686845066](https://github.com/rameshdebur/HumTrack/actions/runs/34686845066)
passed on that source, including managed/native camera builds and self-tests.
Both npm production audits and separate NuGet vulnerable/deprecated queries
reported no findings. These results are point-in-time engineering evidence.

## Evidence limits and next integration

Source implemented; build/static and automated behavior verified; runtime
integration covers Windows process/filesystem/SQLite with synthetic fixtures.
Injected SQLite failure is not process-kill, abrupt OS or power-loss testing.
No camera HIL, field workflow, independent QA or regulatory/clinical review.
This is not certification or a controlled release.

Host startup orchestration, automatic MOVED-to-CATALOGED publication,
operator recovery dispositions, cross-process writer exclusion, backup/restore,
fresh receipt authorization/delivery, source cleanup and whole-session completion
remain open. Malformed unrelated files in a session commit directory
conservatively block orphan discovery; retain them for investigation.
Candidate enumeration is not a transactional snapshot across pages.

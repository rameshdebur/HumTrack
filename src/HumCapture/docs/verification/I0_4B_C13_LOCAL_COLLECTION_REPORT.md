# C13 Local Collection Verification

HC-VR-I0-4B-C13-001, 2026-09-15. Local runtime: 135/135 passed.
Build: zero warnings/errors. Contract tests: 91/91. SBOM tests: 6/6.
Project and official CycloneDX 0.33.1 validators pass. Hosted CI pending.
SBOM 0.1.0-i0.4b-c13: 32 components/33 graph nodes, unchanged dependencies;
generated 2026-09-15T12:17:56.075Z; SHA-256
48a7808a54cd658acca15b2b64309e472c84ffc7cc090258106dcc737b9c47eb.
Evidence-control production npm audit and host transitive NuGet advisory query
report no known vulnerabilities from configured sources; no VEX/licence approval.

| Test (HC-REP-RUNTIME-) | Objective | Result |
|---|---|---|
| 126 | Exact retry, unchanged originals, no transaction, no VERIFIED checkpoint | PASS |
| 127 | Restart owned partial, reuse intact file | PASS |
| 128 | Reject changed destination; retain bytes | PASS |
| 129 | Reject damaged source before staging mutation | PASS |
| 130 | Reject overlapping trees | PASS |
| 131 | Pre-cancellation has no staging writes | PASS |
| 132 | Windows child-process command and mutex | PASS |
| 133 | Retain/refuse changed binding | PASS |
| 134 | Retain/refuse cross-attempt package conflict | PASS |
| 135 | Admitted packages require recovery | PASS |

Synthetic finalized-incomplete fixtures contain their declared survivors, not
real video. The prior 125 tests regress C2 verifier requirements after extraction
of byte-only validation. Initial builds identified a mutex-finally/static-check
issue and two test-style errors; corrected before the passing suite.

Commands from HumCapture:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore -warnaserror
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
```

Evidence limitations: synthetic Windows filesystem/SQLite/child-process checks;
no MTP/phone/USB hardware, full decode, live-capture load, physical power loss,
field workflow, clinical or regulatory acceptance. Partial restart seeds scratch
bytes; it does not power-cycle the machine. Source/destination hashing is
synchronous and uncancellable. Conflicts are retained/refused, not repaired.

# C12 Host Staged Admission Verification

**ID:** HC-VR-I0-4B-C12-001  
**Date:** 2026-09-12  
**Disposition:** Local and hosted engineering verification passed

Objective: expose existing C2 admission through a bounded versioned request and
host command without fabricating verifier evidence or conflating admission with
collection, processing or commit.

All eight tests below pass. Full runtime summary: `total=125 passed=125 failed=0`.

| Test | Objective |
|---|---|
| HC-REP-RUNTIME-118 | Host admission, exact replay, actual Windows account |
| HC-REP-RUNTIME-119 | Synthetic host admission -> movement -> catalog -> commit |
| HC-REP-RUNTIME-120 | Malformed JSON, duplicate keys, unknown/version/actor fields, missing time, invalid base64 |
| HC-REP-RUNTIME-121 | Oversized request |
| HC-REP-RUNTIME-122 | Changed staged package is refused and retained |
| HC-REP-RUNTIME-123 | Missing staging and unsupported repository |
| HC-REP-RUNTIME-124 | Host guard and incompatible CLI options |
| HC-REP-RUNTIME-125 | Changed verifier bytes fail evidence binding |

Executed from HumCapture:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore -warnaserror
npm.cmd --prefix tools/evidence-control run test:contracts
npm.cmd --prefix tools/sbom test
```

Official CycloneDX CLI 0.33.1 accepts the C12 BOM: unchanged 32 components/33
graph nodes, 2026-09-12T13:43:07.999Z,
SHA-256 6a93468599a1dbca12b3baf64e91ad470f1e9518e39d8bb1b6a1937c75783ac3.
Host output is 1.2.0; input envelope is independently versioned 1.0.0.
Existing enum numeric values are preserved by appending the request-error code.

Local checks: Release build passes with zero warnings/errors; 91/91 contract
tests and 6/6 SBOM tests pass. Project SBOM validation passes. The evidence-control
production npm audit reports zero vulnerabilities; host transitive NuGet queries
report no known vulnerable or deprecated packages from the configured sources.
The dependency-free SBOM tool has no lockfile, so its npm audit is unavailable
(`ENOLOCK`), not a passed audit.

Implementation commit: `74d4d4c7e34d208e3e403b79bfa047fcb10983ea`.
Both hosted workflows completed successfully for that source revision:
[push run](https://github.com/rameshdebur/HumTrack/actions/runs/34697777735)
and [PR run](https://github.com/rameshdebur/HumTrack/actions/runs/34697779828).
Their retained workflow logs are the hosted test/build evidence. A subsequent
documentation-only commit records these results; these links attest the source
revision above, not an untested release or hardware acceptance.

Source/build/automated evidence concerns synthetic Windows process/filesystem/
SQLite behavior only. Verifier records are test fixtures, not independently
generated media-decoding evidence. No collection, verifier implementation,
HIL, field, Ctrl+C/abrupt-loss, cross-session exclusion, deployment, clinical
or regulatory verification. Admission stops at STAGED_VERIFIED; no receipt or
source-cleanup authority. Independent review remains pending.

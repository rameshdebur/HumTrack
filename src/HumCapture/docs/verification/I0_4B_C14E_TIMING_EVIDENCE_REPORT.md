# C14E Timing and IMU Evidence Verification

HC-VR-I0-4B-C14E-001, 2026-09-16. ADR-0030 / HC-CHG-20260916-001.
Primary Coordinator/timing engineer; affected Architect, QA, Android/UVC, Risk
and Release/SBOM. AI implementation, review and tests; independent review open.

## Objective and outcome

Independently decode accepted finalized binary timing/IMU artifacts in .NET and
compare accepted source encoded timestamps against decoded presentation evidence.
Implemented internal read-only primitives only. No metadata JSON validation,
clock identity/binding, camera geometry/cadence summary checks, event/finalization
integration or full package-VERIFIED result is claimed. No host activation.

Exact signed/unsigned integers, canonical UUID byte order, optional presence,
sensor components and segment evidence are retained. CRC, version/layout, flags,
record counts/tails, mapping presence and finite components are checked. Reader
limits are explicit engineering limits, not capture protocol/camera rejection.
Presentation association uses exact rational BigInteger comparison and covers
each decoded index once; no FPS-derived timestamps, sorting or repair.
Generated duplicates without expected PTS report incomplete timing comparison;
ambiguous source-sequence references across segments fail rather than guessing.

## Reproducible local checks

From src/HumCapture:

```powershell
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore -warnaserror
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build
& 'C:\Program Files\nodejs\npm.cmd' --prefix tools/evidence-control run test:contracts
& 'C:\Program Files\nodejs\npm.cmd' --prefix tools/sbom test
git diff --check
```

Build: zero warnings/errors. Runtime: 154/154. Contracts: 105/105. SBOM: 7/7.
Runtime output retained in I0_4B_C14E_RUNTIME_RESULTS.txt. Initial build rejected
an exact float comparison style; test now compares IEEE-754 bit patterns, which
is the intended binary-fidelity assertion. Clean rebuild preceded test execution.

Retained LF UTF-8 runtime log SHA-256:
4eb5e96a351396d47dfc8dbac613589891c00a2847a40ac06fed4023b76fbff8.
Tested local Release Repository DLL SHA-256:
471e9d3715a42240ea84dd08b26beee1f093a7371ed9cc4d68f6df536d98ed3d.
These identify local evidence, not a signed/distributed release or hosted binary.

| Runtime test | Objective / expected result |
|---|---|
| 149 | Standard CRC32C check; verify all nine vector hashes and outcomes; all decoded fields/UUIDs match cross-language references |
| 150 | Refuse wrong magic/version/layout/flags, nonfinalized/count/tail/limit/corrupt header; honor cancellation |
| 151 | Refuse frame flag/disposition/absent/mapping errors; preserve ulong.MaxValue, long.MinValue and present zero |
| 152 | Refuse IMU CRC/flags/reserved/sensor/accuracy/non-finite/negative-zero-hidden-field/mapping faults |
| 153 | Exact PTS above 2^53 and signed boundary, duplicate PTS and equivalent rational bases; reject changed clock rate |
| 154 | Refuse missing/wrong/duplicate/out-of-range associations, incomplete coverage and ambiguous generated source; do not invent generated PTS |

Expanded HC-TIM-TEST-012 closes a count-only association defect: an accepted or
generated index could previously exceed the decoded frame range despite matching
the count. The two new negative assertions pass with no contract version change.

SBOM regenerated for the test-project fixture inclusion/build surface:
0.1.0-i0.4b-c14e, 33 components / 34 dependency nodes. Project validator and
official CycloneDX 1.7 validator pass. SHA-256:
4669d7f3f619e5ce1543aa8b76081168c2ae675fc845b76223fff5240c305f53.
No new third-party dependency. Proposed schema library is not installed/inventoried
as an actual dependency. Decoder remains disabled/excluded from distribution.

## Evidence levels and handoff

Source and build/static checks: passed. Automated behavior: passed locally.
Production runtime integration, actual media/source-package association, camera
HIL, field workflow, independent human, clinical/regulatory approval: not verified
by this batch. No UVC/NDI camera accessed; short UVC capture authority is retained.
Existing synthetic decoder evidence is not reused as hardware proof.

Next genuine decision is recorded in
../project/work-items/HC-DECISION-20260916-METADATA-VALIDATION.md. Complete
metadata/common-verifier integration after dependency approach is chosen. Do not
reopen individual sprint approval gates inside the approved batch. Existing
decoder redistribution and qualified regulatory review gates remain separate.

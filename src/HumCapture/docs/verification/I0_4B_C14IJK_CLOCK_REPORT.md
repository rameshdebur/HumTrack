# C14I-K — Exact decimal clock quantization

HC-VR-I0-4B-C14IJK-001 | 2026-09-17 | engineering evidence only.
Change HC-CHG-20260917-003; ADR-0032; user accepted rounding recommendation.
Primary timing/Coordinator/Architect; verification engineering QA.
Baseline before changes: 2ded61945289e7f722df141cc6cf6108244250c3.
Implementation identified by the commit containing this report. Independent
human, field, clinical and regulatory review are not established here.

## Batch and evidence

| Slice | Objective | Implemented / verified |
|---|---|---|
| C14I | Explicit contract, no legacy reinterpretation | Timing profile/schema 1.1 with required policy; 1.0 schemas/binaries unchanged; manifest dispatch rejects unknown/conflicting versions |
| C14J | Exact decimal arithmetic | BigInteger rational scale/native/offset; one final ties-to-even rounding; 30 shared Node/.NET vectors and 1,000 halfway parity checks |
| C14K | Frame verifier integration | Models parsed once; mapped ticks compared exactly after existing clock/range/uncertainty checks; count exposed; absent evidence and legacy mappings do not become PASS |

Accepted cases include >2^53 native values, uint64 maximum, int64 minimum offset,
decimal values beyond floating-point precision, exponent notation, both directions
of halfway rounding and negative offsets. Rejections include malformed/oversized
scale tokens, exponent envelope, pre-rounding range overflow, unknown policy,
version mismatch, nonunit OFFSET, and one-tick mismatches even with large uncertainty.

Serialized scale precision is tested through the actual schema/metadata adapter:
1.0000000000000001 multiplied by source ticks around 10^16 retains the extra tick.
No calculation rounds scale through double before rational construction. Existing
legacy structural checks remain unchanged. Tests synthesize new observations in
memory, never alter historical fixture/binary bytes.

## Local verification

- Release build: zero warnings/errors.
- Runtime: 164 passed, zero failed (new HC-REP-RUNTIME-161-164).
- Contract: 108 passed, zero failed (new HC-CLK-001/002).
- SBOM/licence: 13 passed, zero failed; retained SBOM/source-manifest consistency,
  hash and project CycloneDX validation pass. No new dependency or licence change.
- No camera, decoder execution, physical action or external deployment required.

Raw results: [runtime](I0_4B_C14IJK_RUNTIME_RESULTS.txt),
[contracts](I0_4B_C14IJK_CONTRACT_RESULTS.txt),
[SBOM](I0_4B_C14IJK_SBOM_RESULTS.txt).

```text
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
node tools/sbom/src/licence-register.js --check
git diff --check
```

SBOM 0.1.0-i0.4b-c14k: 37 components plus product; SHA-256
`a7fe77457814a6349b638259c824988e4ff8e9e47321822742f81787c70735b8`.
Hosted source-revision evidence is recorded after CI completes; local success
does not itself establish hosted success. No standalone official CycloneDX CLI,
fresh licence interpretation or full vulnerability review was performed here.

Hosted evidence checked 2026-09-17: implementation
433309957f8205d302fa278679a7b9ab950f9de1 passed push 35220978752 and PR
35220984585. Subsequent C14L-N addresses internal IMU/finalization composition;
the remaining-boundaries section below records the original C14I-K endpoint.

## Explicit remaining boundaries

This checks arithmetic consistency, not clock calibration/uncertainty accuracy or
hardware synchronization. V1.0 mapped arithmetic remains NOT_ASSESSED. V1.1 callers
must use the profile declared in validated manifest evidence; internal enum dispatch
is not yet a complete package verifier. No new host endpoint is activated.

Full package verification, IMU model/sample integration, canonical artifact/hash/
lease integration, event/finalization semantics and remaining camera/cadence
controls remain subsequent batch work. No VERIFIED/commit/receipt/cleanup follows
from these internal results. Master/native data and rejected takes remain intact.

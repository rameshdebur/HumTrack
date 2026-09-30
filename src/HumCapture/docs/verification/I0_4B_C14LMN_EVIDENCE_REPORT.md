# C14L-N — Internal IMU, finalization and combined evidence

HC-VR-I0-4B-C14LMN-001 | 2026-09-17 | engineering evidence only.
Change HC-CHG-20260917-004; ADR-0033; user-approved related-sprint batch.
Baseline 433309957f8205d302fa278679a7b9ab950f9de1. Implementation is identified
by the commit containing this report. Material AI-assisted implementation and
self-verification; independent human review remains outstanding.

## Objectives and implementation

| Slice | Objective | Result and evidence |
|---|---|---|
| C14L | Bind IMU data to declared metadata | Sensor IDs/UUIDs/kinds/units, stream/attempt/clock/model references, ranges/uncertainty; opt-in exact 1.1 mapping; rotation matrix structural checks. Per-sensor counts, sequence anomalies, native regressions and segment changes remain visible. |
| C14M | Verify recorded finalization consistently | Schema and safe-integer canonical JSON bytes, exact archive/summary digests, textual identities, event order/revisions/clock/state continuity and terminal summary/outcome. 22 shared Node/.NET vectors. |
| C14N | Compose checks without granting completion | Bound finalized bytes plus caller-supplied decoded observations; synthetic UVC without IMU, Android with IMU and incomplete survivors; explicit unassessed fields and failure paths. No VERIFIED or repository mutation authority. |

Frame and IMU validators share clock binding logic; existing frame regression
coverage is retained. Available sensor declarations with no samples remain zero,
not synthesized evidence. NOT_AVAILABLE/NOT_SUPPORTED with samples is rejected;
PERMISSION_LIMITED can retain available measurements. No required-sensor or
cadence acceptance policy is invented by these internal checks.

Finalization expectations are a typed projection from an already admitted
manifest, not a new manifest admission API. Artifact role/count/required/version/
path validation belongs to that upstream gate. Textual UUID identities are not
silently case-normalized across capture artifacts. Canonicalization is restricted
to the capture schemas' safe-integer/string values, with UTF-16 key sorting,
preserved valid Unicode and invalid-surrogate rejection; not general floating JCS.
The JS contract encoder now rejects invalid Unicode as required by the existing
canonical contract. Neither schema version nor public interface changed.

## Verification and discrepancy

Initial runtime run: 171/172 passed. HC-REP-RUNTIME-165 failed because the old
standalone IMU binary contains mapped value 5010000002 for native 1010000000;
the test's exact scale 1 plus offset 4000000000 requires 5010000000. The verifier
correctly rejected it. Historical bytes were NOT edited. The test now retains
this rejection and constructs separate explicitly synthetic matching observations.
No production tolerance or rounding was relaxed. Initial raw failure retained.

Final local results: Release zero warnings/errors; 172 runtime, 110 contract
and 13 SBOM/register test groups passed, zero failures. Raw evidence:

- [Release build](I0_4B_C14LMN_BUILD_RESULTS.txt).
- [Runtime](I0_4B_C14LMN_RUNTIME_RESULTS.txt): 172 test groups.
- [Contract](I0_4B_C14LMN_CONTRACT_RESULTS.txt): 110 test groups.
- [SBOM/register](I0_4B_C14LMN_SBOM_RESULTS.txt): 13 test groups and project validation.
- [Initial runtime discrepancy](I0_4B_C14LMN_INITIAL_RUNTIME_RESULTS.txt).

Commands from HumCapture root:

```text
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
node tools/sbom/src/licence-register.js --check
git diff --check
```

SBOM 0.1.0-i0.4b-c14n, 37 components plus product, SHA-256
fd7adab60f64d91fd2e463aca86eaf735decfe2564843a7d11ec6a0b24b8bf23.
No new dependency, fee commitment or licensing interpretation. Published schema
package engineering authorization remains distinct from distribution/commercial
clearance. No fresh full vulnerability or official CycloneDX CLI review this batch.

## Evidence level and remaining work

Source and automated synthetic behavior only for new components. No camera,
actual decoder execution, hardware timing, field workflow, clinical/regulatory
review, host activation, receipt or source cleanup was performed by this batch.
Existing runtime regression tests exercise their previously scoped host paths;
they do not activate this new internal verifier.

Decoded observations in the new combined tests are synthetic, not proof that
the master bytes decode. Hash agreement cannot establish trustworthy decoder
provenance. Complete finalization is not scientific quality or package VERIFIED.
Incomplete archive gaps remain counted and incomplete.

Full immutable manifest/file-lease admission, production profile dispatch,
remaining camera/cadence checks, model segment/epoch continuity, actual calibration,
decoder provenance and final verifier-record integration remain open. No nominal
FPS, requested sensor period or host arrival is substituted for source evidence.
Next related batch should close the bounded package-admission/composition gaps
before any production activation decision. Independent review and release gates
remain unchanged. Hosted success must be recorded against the actual source SHA;
local success is not hosted evidence.

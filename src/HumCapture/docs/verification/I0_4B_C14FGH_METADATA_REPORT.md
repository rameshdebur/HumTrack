# C14F-H — Published schema and frame-metadata integration

HC-VR-I0-4B-C14FGH-001 | 2026-09-17 | engineering evidence only.
Change: HC-CHG-20260917-002. Architecture: ADR-0031. Owner: Coordinator/QA.
Source baseline: 4af664873b9150bc91b4c745ffe40726437ba9d0; changes identified
by the commit containing this report. No independent human approval claimed.

## Implemented batch

| Slice | Objective | Outcome |
|---|---|---|
| C14F | Published JsonSchema.Net dependency and local structural gate | 9.4.0 locked; five embedded schemas plus common definitions; mandatory formats; no arbitrary schema selection or fetch |
| C14G | Cross-engine behaviour and input failures | 43 shared Ajv/.NET vectors; version, required/unknown fields, UUID/type/date checks; duplicate keys, malformed UTF-8/JSON, depth/size/cancellation and parallel evaluations |
| C14H | Bind metadata to finalized frame evidence | Capture/stream IDs, unique clocks/models/streams, references, native validity interval, mapped-model uncertainty, counts, coded geometry and exact PTS association |
| GOV | Dependency provenance and live register | Four packages added, candidate reconciled; 37 components plus product; serial-seed newline regression corrected |

The registered packages are JsonSchema.Net 9.4.0, JsonPointer.Net 7.0.2,
Json.More.Net 3.0.1 and Humanizer.Core 3.0.10. All three json-everything packages
contain byte-identical OSMFEULA.txt, SHA-256
`5c805ac94dfdb4a3be55547f04a2f4b9f1bb87d7e9ee6d6fe54b0e72093900c3`.
Humanizer's exact nuspec declares MIT. User approved the published route; this
does not prove payment/exemption or confer release/legal approval. No purchase made.

## Verification

- Locked restore succeeded; Release build zero warnings/errors.
- Runtime: 160/160 pass, including HC-REP-RUNTIME-155-160.
- Contract tests: 106/106 pass, including HC-META-001 and all previous contracts.
- SBOM/register: 13/13 pass; exact source-manifest comparison and retained hash
  check pass. Project CycloneDX validator accepts the BOM. Official standalone
  CycloneDX CLI was not rerun for this batch; no release qualification claimed.
- NuGet vulnerability query (include transitive, current configured sources):
  no vulnerable packages reported for the SelfTest dependency graph. This is not
  proof of absence of vulnerabilities or a complete machine inventory.

Retained BOM version 0.1.0-i0.4b-c14h, SHA-256
`c15f3c6b55e2ec5abc2a9ee691682b1f44ad11e82126de2f7081d76c2ee8c52a`.
Generation timestamp is the controlled batch identifier, not a test execution time.

Raw results: [runtime](I0_4B_C14FGH_RUNTIME_RESULTS.txt),
[contracts](I0_4B_C14FGH_CONTRACT_RESULTS.txt),
[SBOM](I0_4B_C14FGH_SBOM_RESULTS.txt).

Commands from HumCapture:

```text
dotnet restore apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest --locked-mode
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest --no-restore -c Release
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
dotnet list apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest package --vulnerable --include-transitive --no-restore
```

## Defects found and disposition

1. Compiled schemas retain JsonElements. Disposing their source document caused
   ObjectDisposedException during evaluation. Clone schema elements before build;
   shared fixtures and repeated/parallel tests now pass. No operator data involved.
2. Previous governance commit's hosted runs 35200971957/35200976056 failed only
   fresh-versus-retained serialNumber equality. NuGet restores CRLF while Git
   retains LF. Normalize NuGet lock text only for the serial seed; package hashes
   and retained SBOM hashes remain exact. Regression verifies newline equality
   without hiding dependency-value changes. Hosted re-verification follows push.
3. Standalone timing fixture declares a 1 GHz presentation clock while golden
   binary PTS uses 90 kHz. Do not rewrite historical vectors: the integration test
   rejects that combination and supplies a separately identified 90 kHz clock for
   its valid integrated case. Separate fixture conformance is not package validity.

## Limits and next gate

Internal code only; no host endpoint, automatic VERIFIED, commit, receipt, cleanup,
decoder deployment or camera capture. Existing native master records are unchanged.
Fixed/variable/adaptive/unknown rate classes are preserved. No nominal FPS creates
timestamps. No hardware or field verification performed in this batch.

Schema validity is not canonical-byte or scientific validity. Remaining work:
canonical JSON/manifest/hash/lease integration, provenance and codec semantics,
lens/orientation changes, cadence summaries, IMU/event/finalization semantic checks,
and full package verification. Geometry comparison covers coded dimensions only.
Resource limits are 16 MiB and depth 64; third-party evaluation cannot be hard-
cancelled internally, so production workload qualification remains necessary.

Mapped model references/ranges are checked but MappedTimesRecomputed is false:
v1 specifies affine mapping without integer quantization/precision policy.
See HC-DECISION-20260917-CLOCK-QUANTIZATION. Do not invent tolerance or rounding
and silently change scientific acceptance. Regulatory/clinical/legal review and
release clearance are not established by these tests.

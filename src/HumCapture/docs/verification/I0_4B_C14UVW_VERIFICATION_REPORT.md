# HC-VR-I0-4B-C14UVW-001: Coverage, verification records and staged admission

Date: 2026-09-23. User-approved C14U-W and ADR-0037 clarification.
AI-assisted implementation and verification; independent human review remains
open. Scope is exclusively HumCapture, not parent HumTrack.

## Objective and implementation

Prevent schema-valid but unchecked timing summaries from becoming passing
verification. Opt-in manifest 1.1 / XFR profile 1.3 binds exact native coverage
per IMU sensor lane and clock-continuity run. Master coverage selects accepted
source frames, not nominal FPS or decoded presentation timestamps. Empty
streams have no invented endpoints. The 16,384-span / 16 MiB manifest envelope
refuses oversized verification; source capture and retention are not disabled.
Legacy manifest 1.0 and immutable historical fixture bytes remain unchanged;
unsupported legacy coverage is explicitly NOT_ASSESSED.

PackageVerificationProducer accepts no supplied outcomes or decoder callbacks.
It produces the existing verification-record schema from a held package lease
and actual pinned inspection. Records retain artifact hashes/lengths, twelve
checks, the Windows account, decoder-lock hash, scientific-condition dispositions
and unassessed dimensions. Canonical record bytes have an external SHA-256.
Decoder unavailability/failure cannot become a PASS; detailed worker reasons
are retained. In the existing aggregate vocabulary, required NOT_ASSESSED means
FAILED verification, not a measured scientific-quality rejection.

The internal repository adapter keeps the source lease through existing
STAGED_VERIFIED admission, checks cancellation before durable admission and
derives identities from the manifest/account. Stable IDs and audit time support
exact retries. Changed immutable records conflict rather than overwrite.
No new host command, package move, commit, receipt, cleanup or take completion.
Verified incomplete-survivor custody remains FINALIZED_INCOMPLETE.
Nonverified records are returned without journal mutation; a production
failed-record retention/review workflow is not activated by this batch.

## Verification and retained results

| Evidence level | Observed result | Raw result file |
|---|---|---|
| Build/static | Release, zero warnings/errors | I0_4B_C14UVW_BUILD_RESULTS.txt |
| Full automated runtime | 201/201 | I0_4B_C14UVW_RUNTIME_RESULTS.txt |
| Contract regression | 115/115 | I0_4B_C14UVW_CONTRACT_RESULTS.txt |
| SBOM/register | 13/13 plus official schema and inventory checks | I0_4B_C14UVW_SBOM_RESULTS.txt |
| Actual pinned decoder + repository integration | Five synthetic-media cases | I0_4B_C14UVW_REAL_RESULTS.txt |
| Hosted regression | Independently SHA-bound push/PR checks on the containing source commit | GitHub Actions, HumCapture validation |
| HIL / field / clinical / regulatory | Not performed | No claim |

Runtime groups 194-201 cover independent JS/.NET fixture agreement; tampered
stream/clock/frequency/count/endpoints/lane/run/segment/gaps; old versions and
unknown/mixed profiles; unexpected fields, uint64 overflow, run ordering,
empty spans and span limits; interleaved lanes, sequence/time regression,
empty streams; decoder-unavailable record shape; Windows-held lease release;
survivor staged admission/retry/conflict; failed/cancelled no-admission and an
empty journal; and filtering dropped frames out of master coverage.
Earlier 173-193 package/decoder/scientific guard tests also pass.

Actual pinned-decoder cases:

1. Covered three-frame master verifies and enters STAGED_VERIFIED; exact retry
   returns the prior admission. Original package hashes remain unchanged and
   no package appears in the destination.
2. Successful decode of legacy coverage remains NOT_ASSESSED / not VERIFIED.
3. A deliberately failing required cadence rule remains REJECTED in the
   retained evidence while technically valid custody can still be VERIFIED.
4. Corrupt media cannot produce VERIFIED.
5. Different decoded presentation timestamps are refused.

The media and pinned binaries are the existing C14R/C14B retained synthetic
inputs, not new real-subject or camera recordings. See the C14R report for
media hashes/generation provenance and the controlled decoder lock for pins.

## Discrepancies resolved and limitations

Preflight HC-VR-I0-4B-C14UVW-PREFLIGHT-001 found the synthetic Android v1 IMU
summary inconsistent with its samples. New independently generated v1.1
fixtures represent correct per-lane bounds; the old fixture is retained as
legacy evidence, not silently rewritten.

The first runtime integration run passed 25/27 groups but failed groups 198/199
at SQLite initialization: the generated test root was nested under the long
build-output path. No admission behavior ran in those failures. Test repositories
now use uniquely owned shorter paths inside HumCapture/evidence-vault and are
removed by test cleanup. The final full run passes all 201 groups. This is not
a claim of SQLite long-path support.

Scientific condition dispositions do not establish authenticated session/role
approval, physical synchronization, calibrated lens/IMU accuracy or complete
take acceptance. Full coordinator session/protocol admission, legacy migration,
failed-record persistence and controlled production scheduling/activation remain
later gates. The record producer is internal and the decoder remains disabled
for production distribution under the existing supply-chain gate.

## Reproduction and supply chain

From HumCapture, run:

```powershell
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build
npm run test:contracts --prefix tools/evidence-control
npm test --prefix tools/sbom
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --verification-real <pinned-binary-directory> <C14R-synthetic-media-directory>
```

Regenerate coverage vectors with
`node tools/evidence-control/fixtures/coverage-package-vectors.js --write`.
Generated fixture equality is tested; .NET validates the independent vectors.

CycloneDX 1.7 SBOM version 0.1.0-i0.4b-c14w, 37 components plus product,
timestamp 2026-09-23T08:17:20Z. SHA-256:
`563d18b3476b6a457b773a0e49cb18c8685eddac64348b6f3890e1a3bd97b842`.
No dependency was added; published JsonSchema.Net 9.4.0 remains pinned.
The live licence/cost/obligations register was regenerated and checked.
Inventory is not legal clearance, redistribution permission, medical-device
certification or CDSCO approval.

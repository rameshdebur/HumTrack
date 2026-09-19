# C14O-Q — Finalized-package file input

HC-VR-I0-4B-C14OPQ-001 | 2026-09-19 | engineering evidence only.
Change HC-CHG-20260919-001; ADR-0034. Baseline afbed422acaaebb4ae2c5ba29253b82e809b5272.
Implementation is identified by the commit containing this report. Material
AI-assisted source and self-verification; independent human review remains open.
Primary Coordinator/Repository engineer; affected Architect, Timing, Android/UVC,
QA and Release/SBOM. HC-DATA-REQ-001/002/008/009 and HC-RISK-022/030/031 apply.

## Batch outcomes

| Slice | Objective | Implemented and exercised |
|---|---|---|
| C14O | Admit manifest and declared artifact profiles | Embedded schema, strict JSON/UTF-8, canonical bytes and content hash, exact inventory/size/hash/identity, canonical paths, supported profile dispatch, required roles and versions; no inferred legacy upgrade. |
| C14P | Bind stable file inputs | Local Windows file handles deny writers/deletion; directory/ancestor handles deny rename/deletion; handle reparse/hard-link inspection; streamed hashes; bounded sidecar reads; final inventory snapshot. |
| C14Q | Evaluate package-folder evidence | Existing frame/IMU/finalization validators consume the admitted files; UVC without IMU, exact Android, incomplete survivors, foreign/corrupt files, conflicting profiles, mutations, cancellation and lease disposal. |

`PackageInputEvidence.Evaluate` is internal and read-only. Its optional observation
callback supplies decoded observations but does NOT attest to decoder provenance.
No callback/result means MASTER_FULL_DECODE and frame comparison stay unassessed.
No production host command invokes this adapter, and no verification record,
journal admission, move, receipt or cleanup is created.

Complete packages use the declared timing profile (1.0 or 1.1) and required
scientific artifact set. Incomplete packages may retain only events/finalization;
any supplied scientific artifacts still need supported profile declarations.
Optional calibration/auxiliary files are hash-bound and explicitly unassessed;
required unimplemented semantics are refused. Corruption stops this verification
attempt without deleting or rewriting survivors; collection remains separate.

## Local verification

- Release build: zero warnings/errors; [raw build](I0_4B_C14OPQ_BUILD_RESULTS.txt).
- Final focused Release run: all eight test groups HC-REP-RUNTIME-173-180 passed;
  [raw runtime](I0_4B_C14OPQ_RUNTIME_RESULTS.txt). Runner flag `--package-input`
  selects this test class only; ordinary CI invocation still runs the full suite.
- All 111 contract tests passed, including independent Node/Ajv validation of the
  three shared synthetic file fixtures; [raw contracts](I0_4B_C14OPQ_CONTRACT_RESULTS.txt).
- All 13 SBOM/register tests and project validation passed;
  [raw SBOM](I0_4B_C14OPQ_SBOM_RESULTS.txt).
- A 129 MiB synthetic master exceeds the sidecar memory allowance yet is accepted
  for streamed integrity checking. Oversized metadata is refused, not truncated.
- Real Windows filesystem tests prove writer/delete/file-rename/directory-rename
  exclusion, existing-writer refusal, final extra-file detection, hard-link and
  junction refusal, long-path access, and handle release after failure/cancellation.
- No real media decode or camera acquisition was performed by this batch.

Commands:

```text
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-input
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
node tools/sbom/src/licence-register.js --check
git diff --check
```

## Discrepancies and resolution

Initial focused run failed on deep paths: .NET created/read the fixtures, but
native CreateFileW lacked extended-length syntax (Win32 error 3). The adapter
now adds the native prefix only after validating an ordinary absolute local
drive path. Caller-supplied device/UNC paths remain unsupported. Mutation tests
then passed. The rejection test helper was also corrected to catch
InvalidDataException explicitly, rather than assuming it derives from IOException.
Raw [initial focused failure](I0_4B_C14OPQ_INITIAL_FOCUSED_FAILURE.txt) is retained.

A second test-setup failure used the safe-integer canonical writer for a timing
fixture containing a decimal clock scale. Timing metadata does not use this
restricted canonical encoder; the test now serializes its synthetic mutation as
ordinary JSON. Production canonical rules were not relaxed. Raw
[fixture failure](I0_4B_C14OPQ_FIXTURE_FAILURE.txt) and subsequent
[focused Debug success](I0_4B_C14OPQ_FOCUSED_DEBUG_RESULTS.txt) are retained.

The first full local regression process had loaded the pre-fix binary. It was
stopped by exact owned process identity after checking it had no children, rather
than treating it as evidence for corrected source. Its partial output is retained
as [superseded, non-acceptance evidence](I0_4B_C14OPQ_SUPERSEDED_REGRESSION.txt).
Final local acceptance here is the focused Release suite, not a claimed full
180-test local run. Hosted full-suite results must be tied to the final source SHA.

## Supply chain and boundaries

SBOM 0.1.0-i0.4b-c14q: 37 components plus product; SHA-256
3d21e93282f9b040968e6f3e566fcd5e412d7e5546e119df578e1109f944fcb5.
Embedded manifest schema changes the build surface; SBOM and licence-register
references are refreshed. No new dependency or fee commitment. No new legal
interpretation, clinical/CDSCO claim, full vulnerability assessment or official
CycloneDX CLI validation is supplied by this batch.

The lease applies during evaluation, not forever after disposal. File additions
are detected at the final inventory snapshot; directory sharing does not prevent
creation. No protection against privileged raw-disk writes is claimed. Local
ordinary Windows paths only; limits are documented in ADR-0034.

Full decoder provenance/invocation, protocol/cadence acceptance, timing coverage
against observed records, model segment/epoch continuity, physical calibration,
verifier record production and production activation remain later gates. Master
and native timestamp bytes are never repaired or synthesized by the adapter.

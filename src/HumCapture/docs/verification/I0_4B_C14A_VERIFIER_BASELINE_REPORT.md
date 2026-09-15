# C14A Verifier Baseline Verification

HC-VR-I0-4B-C14A-001, 2026-09-15. Local and hosted engineering verification passed.

Repository regression: 135/135 passed. Release build: zero warnings/errors.

Source revision: dcd6bd87901602450020747726b0653529722ac3.
[Push CI](https://github.com/rameshdebur/HumTrack/actions/runs/34975585870) and
[PR CI](https://github.com/rameshdebur/HumTrack/actions/runs/34975589810) both
completed successfully for this revision. Their retained logs are hosted
build/test evidence. A subsequent documentation-only commit records the result;
these runs attest the source revision above, not decoder/runtime qualification.

101/101 contract tests and 7/7 SBOM tests pass. Project and official CycloneDX
0.33.1 validators pass: 33 components/34 nodes, including a planned excluded
decoder archive. BOM timestamp 2026-09-15T13:29:39.883Z; SHA-256
dfefd310c3379cceaa9ed2b32cc05f4b1500d5ae0ec9f5863c0b8cabb507c154.
Evidence-control production npm audit reports zero known vulnerabilities;
this does not assess the planned FFmpeg binary or its embedded dependencies.
All ten named HC-ART-TEST cases below passed locally.

Objective: baseline acyclic event/finalization artifacts and a controlled planned
decoder dependency without generating false VERIFIED evidence or promoting probes.

| Test | Objective |
|---|---|
| HC-ART-TEST-001 | Strict schemas and acyclic complete UVC bindings, no hidden IMU or VERIFIED result |
| 002 | Incomplete survivors and declared partial archives; reject false complete |
| 003 | Reject foreign capture/source identity |
| 004 | Reject missing required events and duplicate IDs |
| 005 | Reject source-clock/frequency change, regression and uint64 overflow |
| 006 | Exact terminal event, first-master event, outcome and reason |
| 007 | Byte hash/archive binding cannot be substituted |
| 008 | Reject recursive package hash, unknown fields and future schemas |
| 009 | Reject duplicate-key/noncanonical JSON and invalid UTF-8 |
| 010 | Historical packages are not silently upgraded |

Short test IDs use HC-ART-TEST- prefix. Fixtures contain synthetic placeholder
media, not decoded video. They demonstrate contract checks only. Initial strict
schema compilation rejected a conditional required-property declaration; it was
corrected without disabling strict validation.

The seventh SBOM test rejects mutable/latest archive references, absent hashes
and silent runtime/licence/redistribution promotion. The graph has an excluded
planned archive component, not an installed FFmpeg runtime. Publisher checksum
retrieval is not downloaded-binary verification or legal approval.

Commands: npm.cmd --prefix tools/evidence-control run test:contracts;
npm.cmd --prefix tools/sbom test; dotnet run --project
apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release;
dotnet build the same project -c Release --no-restore -warnaserror; project and
official CycloneDX validation; git diff --check.

No runtime verifier, full video decode, binary download, installation, hardware,
field, clinical, regulatory, legal or independent-human acceptance is claimed.
No schema mismatch is repaired by rewriting source packages. Decoder activation
needs exact binary verification, provenance/licences and runtime tests in C14B.

# HC-VR-I0-4B-C14ST-001: Protocol-bound scientific evidence

Date 2026-09-22. User-approved C14S-T and contract decision, ADR-0036 /
HC-CHG-20260922-001. AI-assisted implementation and verification. Independent
human review and regulatory/clinical assessment remain open.

## Objective and outcome

Implement opt-in snapshot 1.1 per-role rules, missing-evidence versus measured
failure, source-native cadence and declared clock-model continuity. Old snapshots
and legacy JavaScript policy remain unchanged. No nominal/adaptive-rate default,
second-camera requirement, external service, new dependency or host activation.

Internal package evaluation accepts bounded canonical snapshot bytes plus the
coordinator's source/role projection. It verifies schema/content hash/manifest
binding and validates all rule identities/types. This is not authentication of
protocol approval or full session admission. PROTOCOL_ADMISSION remains open.
Rule-subset CONFORMANT never implies finalization, full verification or completion.

Exact rational native-frame measurements drive thresholds. Reported camera
class/lens and decoded dimensions remain different evidence kinds. Camera type,
stream, counts and native-clock provenance must agree even without decoding.
Presentation/audit clocks cannot masquerade as acquisition-native clocks.
Clock continuity respects independent IMU lanes, unmapped barrier records and
cross-stream declared segment/model binding. Missing physical epoch evidence is
explicitly unassessed; physical synchronization is not established.

## Verification

| Evidence | Result / retained artifact |
|---|---|
| Release build/static checks | Zero warnings/errors; I0_4B_C14ST_BUILD_RESULTS.txt |
| Focused runtime | 21 groups, 173-193; I0_4B_C14ST_RUNTIME_RESULTS.txt |
| Contract regression | 113 passing; I0_4B_C14ST_CONTRACT_RESULTS.txt |
| SBOM/register | 13 passing; I0_4B_C14ST_SBOM_RESULTS.txt |
| Real pinned synthetic-media integration | Four cases; I0_4B_C14ST_REAL_RESULTS.txt |
| Full hosted runtime | Separate SHA-bound CI gate; not inferred from focused results |
| Camera/HIL, field, clinical/regulatory | Not performed |

New tests 185-193 cover ten shared policy vectors, integer precision above 2^53,
exact boundary comparisons, variable intervals, gaps/duplicates/regressions,
missing intervals, legacy and absent rules, hash/source/role rejection, rule
type/enum/identity validation, restart/model replacement, reuse across unmapped
barriers, independent/interleaved IMU sensors, cross-stream segment binding,
fixed/variable/adaptive/unknown classes, incomplete survivors, camera identity /
provenance / count mismatch, presentation-clock substitution, cancellation and
explicit versus unexplained lens unavailability. Both Ajv and .NET compile the
versioned protocol schemas and consume the shared policy vectors.

The extended opt-in real decoder tests reuse C14R synthetic files/hashes and
pinned C14B binaries (see HC-VR-I0-4B-C14R-001). The three-frame valid case now
passes bound scientific rules through the asynchronous actual-decoder adapter.
Different PTS, corrupt media and geometry mismatch still fail at their intended
boundaries. All source bytes remain unchanged; lease release is checked. These
are generated recordings, not evidence from an attached camera or a real subject.

## Discrepancies and limitations

Code review found synthetic package fixture labels inconsistent with their
source types: the UVC fixture inherited an Android clock and the Android fixture
inherited UVC camera labels. Only the generated synthetic package inputs were
corrected and rehashed; original golden timing vectors were not rewritten.
The new rejection tests prevent such mismatches from silently passing.

The local full-suite attempt was deliberately stopped after 17 passing storage
tests because Windows durable-storage tests progressed slowly. Its partial log
is I0_4B_C14ST_LOCAL_STORAGE_PARTIAL.txt and is NOT full regression acceptance.
Final focused results are from the corrected Release build; full hosted checks
must pass against the pushed source SHA. No run is represented as broader
evidence than it supplies.

Overall cadence is not synthesized across discontinuities. The raw source
records are authoritative; camera JSON observed_hz is not used for threshold
acceptance. Missing or unsupported scientific dimensions and physical epoch
detection remain explicit. Full session/assignment admission, quality record
production, controlled activation and capture-priority scheduling are later work.

## Supply chain and reproduction

SBOM 0.1.0-i0.4b-c14t, 37 components plus product. SHA-256:
2f8f50090767824eb3464491aa95d7880dd4d33a55a9b76b0687bc10ae8c6dc3.
Project schema resources changed; dependencies/licence choices did not. Project
SBOM validation and licence-register coverage pass; this is not legal clearance.
The excluded engineering decoder is still not enabled for redistribution.

```text
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-input
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-decode-real ABS_BIN ABS_MEDIA
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
node tools/sbom/src/cli.js validate --input sbom/humcapture.cdx.json
node tools/sbom/src/licence-register.js --check
```

Full runtime uses the same dotnet run without --package-input. Reproduction of
retained synthetic files and exact decoder identity is documented in C14R.

# C14D Decoded Media Evidence Verification

HC-VR-I0-4B-C14D-001, 2026-09-16. Local checks passed; hosted CI pending.
Scope ADR-0029 / HC-CHG-20260915-002. Coordinator engineer/Architect; affected timing,
QA, Release/SBOM and risk. Independent human review pending. No deployment approval.

## Implemented boundary

Internal InspectAsync checks both pinned binaries, hashes the input, and retains
read-only leases across ffprobe and full ffmpeg decode. Output binds byte length/
SHA-256 to one non-attached-picture video stream, codec, header geometry, exact
rational time base and each decoded frame's signed int64 PTS/dimensions/order.
No best-effort timestamp, nominal FPS, reordering or padding. Duplicate/regressing
PTS and geometry changes remain observations, not silently repaired data or a claim
of conformance. Input hash must later be matched against the package manifest.

Every failure returns null evidence. One video stream per inspected file is an
inspection-profile limit, not a single-camera trial rule. Non-video media, rotation/
display transformations, codec qualification and source-clock association are not
verified here. Evidence is an internal in-memory model, not a new persisted package
schema. The JSONL below is synthetic test output only.

## Tests

148/148 runtime tests; 105/105 contract tests; 7/7 SBOM tests. Release build zero
warnings/errors, git diff --check passed. Six new runtime cases:

| Test | Verified behavior |
|---|---|
| HC-REP-RUNTIME-143 | Exact signed 64-bit PTS, preserved duplicates/regression/order/dimensions |
| 144 | Reject zero/multiple video streams, attached picture, invalid dimensions/type/time base |
| 145 | Reject absent/fractional/overflow PTS, foreign frame, duplicates and malformed JSON |
| 146 | Reject 100,001 frames and cancelled parsing |
| 147 | Pin failure/cancellation emits no partial evidence |
| 148 | Probe requests actual PTS and all video streams, not inferred rates/timestamps |

Short IDs use HC-REP-RUNTIME-. Existing timeout, concurrent pipe drain, output
overflow and process cancellation tests also pass. The inspector shares the existing
execution controls; denied-kill cleanup and queue-contention fault injection remain open.

## Actual pinned binaries, synthetic media

[Retained machine output](I0_4B_C14D_REAL_MEDIA_RESULTS.jsonl), LF-normalized SHA-256:
1f6b692d3883fdd6f38a30cbcee301091b8ddd25dac651697e2c1a6d7f5d5c6f.
Locally tested repository DLL SHA-256:
c68c76260d8a124c9187e9fe1b951e6e16dda3f63d5a6f35c10405c8e48422b2.

Self-test --inspect-real used the C14B pinned binaries and probe-Ttk8JK fixtures:
30 fixed-interval frames and 20 variable-interval frames, 160x120 H.264, time base
1/15360. Fixed PTS delta1024; variable deltas1024/2048. Machine output retains every
frame and the file hashes. Truncated/corrupt cases returned Failed with no evidence.
Every input's hash was unchanged. Host Windows kernel10.0.26200 x64, SDK10.0.401.
No Windows10, camera, Android, HIL or field test was conducted.

Commands: Release build with --no-restore -warnaserror; self-test --no-build;
self-test --no-build -- --inspect-real BIN SYNTHETIC_MEDIA_DIRECTORY;
evidence-control test:contracts; SBOM test/generate/validate and official validator.
Reference: [ffprobe options](https://ffmpeg.org/ffprobe.html), checked 2026-09-16.

SBOM 0.1.0-i0.4b-c14d: 33 components/34 nodes; project and official CycloneDX0.33.1
validation pass. SHA-256:
9386f64f5d3ed53a251e7bedb8bfe641c10da4c426c26ffaaebedfed8dddc45c.
No dependency version change; decoder remains excluded/deployment-disabled.

## Remaining work

Inspection is limited to 16 MiB process output and 100,000 frames; no truncated
evidence is accepted. These are engineering limits, not capture-duration/camera
requirements. No hard OS memory quota; stream output before deploying larger protocols.
Next C14E is source-frame/timing/camera association, followed by other scientific
artifacts and the full common verifier. No host activation, admission, receipt,
cleanup, source mutation, clinical/regulatory approval or VERIFIED package.

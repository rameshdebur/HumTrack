# C14B Decoder Candidate Engineering Evidence

HC-VR-I0-4B-C14B-001, 2026-09-15. Local candidate checks passed; hosted guard CI pending.
Scope: isolated engineering qualification under ADR-0027, not a production worker.
Primary owner: Coordinator engineer; affected Release/SBOM and QA; independent
human review open. No regulatory applicability or claims change.

## Observations and provenance

The first download timed out after 180 seconds with 66,019,044 of 111,253,802 bytes.
No extraction/execution occurred. A resumed download completed and matched the
unchanged publisher archive SHA-256 before extraction. The archived LICENSE and
README remain in the ignored local evidence area. System/PATH FFmpeg is unchanged.

The [versioned publisher archive](https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.1-essentials_build.zip)
matched fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9.
Executable hashes for ffmpeg, ffprobe and unexecuted ffplay are in decoder-lock.json.
Included LICENSE SHA-256: 8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903.
README identifies source [bf1b838f2a](https://github.com/FFmpeg/FFmpeg/commit/bf1b838f2a)
and GPLv3; this is supplier provenance, not redistribution approval or reproducible-build proof.

Actual ffmpeg/ffprobe report 9.0.1-essentials_build-www.gyan.dev, gcc16.1.0 and
their complete build configuration/core library versions in the machine report.
This does not enumerate exact versions/hashes of all statically embedded libraries.
Host: Windows kernel10.0.26200 x64, Node22.12.0. No Windows10 execution was tested.

## Tests and retained proof

[Machine report](I0_4B_C14B_DECODER_PROBE_RESULTS.json) retains process exit codes,
errors/stdout/stderr, executable identities, probe-source/lock digests and host context.
Report SHA-256: 14f06f34dca5fe44012393013b4fd9bdd37ac5286093d4486f88b7a57e916990.

| Test | Objective and observed result |
|---|---|
| HC-DEC-TEST-001 | Generate synthetic H.264/MP4, clean exit |
| 002 | Probe: one H.264 stream, 160x120, 30 decoded frames |
| 003 | Full software decode to null, no errors |
| 004 | Half-truncated fast-start MP4 rejected with invalid-data diagnostics |
| 005 | Invalid container bytes rejected, not a timeout/spawn failure |
| 006 | Original valid video SHA-256 unchanged after inspection/decode |
| 007 | 20-frame variable-interval file retains two positive PTS deltas and decodes |

All seven passed twice locally; the retained report is the second run with
source/lock/environment bindings. Test IDs 002–007 have prefix HC-DEC-TEST-.
HC-DEC-GUARD-001–004 verify hash refusal, clean-exit predicate, process timeout/
output overflow, and explicit software/passthrough arguments. These are probe
guards, not application cancellation/resource acceptance. Full contract suite:
105/105 passing. SBOM tests: 7/7 passing. Repository regression: 135/135 passing;
Release build: zero warnings/errors. git diff --check passed.

The probe uses -xerror, empty-output refusal and -fps_mode passthrough, consistent
with [FFmpeg documentation](https://ffmpeg.org/ffmpeg.html). Probe counts/PTS use
[ffprobe](https://ffmpeg.org/ffprobe.html). Decodable truncations may still succeed
in other containers: later manifest/frame/timing association is mandatory, and a
clean decode alone must never produce package VERIFIED.

SBOM 0.1.0-i0.4b-c14b retains the excluded archive and observed executable hashes:
33 components/34 graph nodes. Project and official CycloneDX0.33.1 validation pass.
BOM SHA-256: 8ce0bac883da00a076971be63c71b02f18f35520022b2b0eb18671830085cb9e.

## Remaining gates

No Coordinator runtime integration, scientific timing/camera/events/IMU verification,
hardware, field, regulatory, clinical or licence approval. No packages admitted,
sources modified or cleanup authorized. Production worker implementation, bounded
asynchronous cancellation/resource handling and complete independent verifier remain
next. Keep runtime_enabled=false and redistribution_approved=false. The local archive
and generated video remain ignored, not bundled or committed.

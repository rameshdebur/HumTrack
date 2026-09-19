# C14C Bounded Decoder Worker Verification

HC-VR-I0-4B-C14C-001, 2026-09-16. Local engineering tests and hosted CI passed.
Scope ADR-0028 / HC-CHG-20260915-002. Primary Coordinator engineer; QA verification;
independent human review pending. No conformity or deployment approval.

Source revision f267ee1716bb1612f2342aa4ae849e6eb4f0fb25 passed
[push CI](https://github.com/rameshdebur/HumTrack/actions/runs/35097073099) and
[PR CI](https://github.com/rameshdebur/HumTrack/actions/runs/35097076289).
Hosted runs test the worker's subprocess controls and all existing regressions.
Actual pinned FFmpeg execution remains separate local evidence. A subsequent
documentation-only commit records these outcomes; these CI links attest the source
revision above, not the follow-up documentation commit.

## Source and behavior

Internal .NET worker in existing repository assembly. No public host command or
package-state mutation. Uses embedded C14B executable hash, explicit path/argument
list, read-only leases, software decode to null, passthrough cadence, one worker
per process, cancellation and deadline (including queue/hash), one MiB combined
retained stdout/stderr, concurrent pipe drain, and process-tree termination.
Parent cleanup wait is bounded to five seconds; unconfirmed cleanup is failure
and blocks further worker calls until restart. FFREPORT is removed from child
environment; no automatic decoder download, installation or redistribution.

Decoded means only successful first-video-stream decode with no stderr; it is NOT
VERIFIED. Stream count/codec-profile, source-frame association and all scientific
metadata/package checks remain pending. Scope is ordinary MP4 paths; hardware
acceleration and nominal FPS rewriting are not used.

## Automated tests

142/142 runtime tests pass, including these seven additions:

| Test | Verified behavior |
|---|---|
| HC-REP-RUNTIME-136 | Clean success, nonzero exit and zero-exit stderr rejection |
| 137 | Concurrent 256-KiB stdout/stderr drain; combined 1-KiB overflow refusal |
| 138 | Deadline kills a running child; PID no longer running |
| 139 | Pre-cancel does not spawn; in-flight cancellation terminates child |
| 140 | Missing executable is StartFailed |
| 141 | Wrong executable pin/relative path fail; leases released |
| 142 | Space-containing path, software decode and cadence-preserving arguments |

Short IDs above use HC-REP-RUNTIME-. These are real subprocess tests using the
self-test child, not a mock decoder. 105/105 contract and 7/7 SBOM tests also pass.
Release build: zero warnings/errors; diff whitespace check passes.

Initial builds caught style/namespace/test-code analyzer violations. They were fixed
without suppressing rules. An inadvertent --no-build run used the prior 135-test
binary after a failed build; its output is EXCLUDED from C14C evidence. A concurrent
build then encountered that binary's file lock. After it exited, a clean successful
build preceded all accepted 142-test and actual-worker results.

## Actual decoder execution (local only)

[Retained output](I0_4B_C14C_REAL_DECODER_RESULTS.txt), normalized to LF:
SHA-256 992f55f48a67ad7298399567a97e7e4be4529e3b7934a0e73c40226d633f16ab.
Actual worker library SHA-256:
1e061bee9d1f017f7663c3b9d0c778c0643b733ea4514f7fd57b0e0045135766.
Local compiler/build artifact identity is separate from a hosted rebuild.

Invoked the self-test --decoder-real mode against the C14B pinned ffmpeg and four
synthetic files from probe-Ttk8JK. Fixed/variable H.264 files decoded successfully;
truncated/corrupt files failed with exit -1094995529. SHA-256 before/after every
input was unchanged. All four cases passed twice after a clean build. Windows
kernel10.0.26200 x64 / SDK10.0.401; no actual Windows10 or capture device tested.
Binary identity/provenance is retained in decoder-lock.json and C14B machine report.

Commands: dotnet build self-test -c Release --no-restore -warnaserror;
dotnet run self-test -c Release --no-build; same with --decoder-real BIN MEDIA_DIR;
npm.cmd --prefix tools/evidence-control run test:contracts; npm.cmd --prefix tools/sbom test.

SBOM 0.1.0-i0.4b-c14c, 33 components/34 nodes, project and official CycloneDX0.33.1
validators pass. SHA-256:
18b12f904616fc0b2a5d2a70bc39ea3c60e2d4d935e1b5380dafcb88fd33a8be.
Dependency versions unchanged; decoder remains excluded/deployment-disabled.

## Limits / next gate

No host/UI integration, capture-priority scheduling, hard OS CPU/RAM quota, general
codec qualification, hardware, field, clinical or regulatory review. Kill(true)
does not attest every descendant's exit; pinned FFmpeg is not expected to spawn
children. Cleanup failure latch, queue contention and denied-kill OS behavior have
not been fault-injected. Full C14D scientific verification and library/licence/
vulnerability review remain open. No package admitted or marked VERIFIED.

Implementation references: [Process termination semantics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)
and [FFmpeg options](https://ffmpeg.org/ffmpeg.html), checked 2026-09-16.

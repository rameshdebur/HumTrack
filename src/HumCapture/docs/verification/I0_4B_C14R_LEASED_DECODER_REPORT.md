# HC-VR-I0-4B-C14R-001: Leased package decoder integration

Date 2026-09-19. Scope C14R only, ADR-0035 / HC-CHG-20260919-002.
AI-assisted implementation and engineering verification; independent human
review remains open. No camera, field, clinical, licensing or regulatory claim.

## Objective and behavior

Connect the existing pinned FFmpeg/ffprobe inspection to admitted package files
without releasing the package read leases. Actual observations must bind the
manifest's master hash/length and existing frame/camera comparisons. Callback
observations remain provenance-unassessed. Worker failure produces a typed
inspection outcome and no package result; missing-master survivors remain partial.
There is no host activation, verification record, journal change, receipt or cleanup.

## Evidence

- Release build: zero warnings/errors, I0_4B_C14R_BUILD_RESULTS.txt.
- Focused package regression: 12 groups (173-184), I0_4B_C14R_RUNTIME_RESULTS.txt.
- Contract tests: 111 passed, I0_4B_C14R_CONTRACT_RESULTS.txt.
- SBOM/register: 13 passed, I0_4B_C14R_SBOM_RESULTS.txt. Local schema validation
  and exact licence-register coverage pass; these are not legal clearances.
- Real pinned synthetic media: four cases in I0_4B_C14R_REAL_RESULTS.txt.
  Matching three-frame H.264 binds actual decoded evidence; different PTS fails
  the presentation timestamp comparison; mismatched negotiated width fails the
  geometry comparison; corrupt media returns no package result. All input bytes
  remain unchanged and files reopen exclusively for writing after each case.
- Full corrected-source runtime regression is a hosted gate, recorded separately
  against the pushed source SHA. No full local runtime-suite claim in this report.

Initial build rejected an empty expected-exception test catch under Sonar S108.
It was replaced with an explicit lease-release check, then strengthened to assert
the specific expected comparison error. Final build and tests use corrected code.

## Reproduction and retained media

Build/run:

```text
dotnet build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-restore
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-input
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-decode-real ABS_BIN ABS_MEDIA
npm.cmd run test:contracts --prefix tools/evidence-control
npm.cmd test --prefix tools/sbom
```

ABS_BIN is the retained C14B extracted pinned binary directory. Recheck executable
hashes against apps/windows-coordinator/decoder/decoder-lock.json before generation.
ABS_MEDIA is local ignored evidence-vault/decoder-c14r-071ee96894b948eeaf20c322bf466ee7.
Create a new empty directory when reproducing. With the pinned ffmpeg executable,
generate master3.mp4 at RATE=30 and different-pts.mp4 at RATE=15:

```text
ffmpeg.exe -nostdin -hide_banner -loglevel error -f lavfi -i color=c=blue:size=1920x1080:rate=RATE -frames:v 3 -c:v libx264 -threads 1 -bf 0 -pix_fmt yuv420p -video_track_timescale 90000 -n OUTPUT.mp4
```

corrupt.mp4 is the unchanged synthetic corrupt input from C14B probe-Ttk8JK.
The test copies media into disposable synthetic manifest fixtures, rebinds file
hashes/inventory, and compares them. Geometry mismatch changes only the synthetic
negotiated width. Nothing edits the retained media or an actual capture package.

Retained SHA-256:

| Input | SHA-256 |
|---|---|
| master3.mp4 | 6ee8357700349bba0110d2b2d7fdbbfdc083ce9173554613f79dfc7b72230c87 |
| different-pts.mp4 | 67e287fe0d0650c40c6b45d79e0686410b1f54a7ae8dbe7604502cef3d1a5dce |
| corrupt.mp4 | f600eca824e84a43f0691b267bd620e462c50da165c5b80e17aecb7a924f1fa8 |

SBOM 0.1.0-i0.4b-c14r: 37 components plus product, SHA-256
6db9a78f3efb6d26cbdaaaf64ed331f20b7dc7dd38124e213a46c69efd7a622b.
No dependency/version/licence selection changed. Decoder remains excluded from
production runtime and redistribution; prior engineering-use review gates remain.

## Limits and next work

The decoder deadline excludes synchronous package admission; caller cancellation
still applies there. Existing worker timeout/termination tests are inherited,
not new hardware evidence. This sprint does not directly fault-inject cancellation
midway through real FFmpeg execution. Package lease mutation tests remain 176/177.
No hard CPU/RAM sandbox or capture-priority scheduling is claimed.

C14S/T remain open: protocol-bound cadence/camera assessment and clock-model
segment continuity. A valid decode is not scientific acceptance or synchronization
qualification. Incomplete/unsupported metadata stays explicit. Verification-record
production and controlled activation remain later batches.

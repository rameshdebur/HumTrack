# Engineering decoder candidate (runtime disabled)

C14C adds internal PinnedDecoderWorker/DecoderProcess in the Coordinator repository
assembly (ADR-0028). It is not called by the host and cannot admit a package. It
embeds this lock, checks the ffmpeg executable, holds read-only input/executable
leases, serializes decoder work, and bounds deadline/output with cancellation and
termination handling. It does not invoke this Node probe. Real .NET-worker tests
are available only in the self-test executable via `--decoder-real ABS_BIN ABS_SYNTHETIC_MEDIA_DIRECTORY`.
The media directory must contain the four named files produced by probe.mjs.
Production deployment remains disabled; complete package verification is pending.

C14A selected an exact FFmpeg/ffprobe archive identity. C14B downloaded that archive,
verified its SHA-256 before extraction, and recorded all three included executable
hashes in decoder-lock.json. ffmpeg/ffprobe version/build output and seven passing
synthetic media checks are retained in docs/verification/I0_4B_C14B_DECODER_PROBE_RESULTS.json.
ffplay is inventoried but was not executed. No system installation or PATH change.
SBOM scope remains excluded: local engineering use is not Coordinator activation.

Before worker activation: complete embedded-library inventory/review, full verifier
integration and capture-priority scheduling. C14C bounds process time/diagnostic bytes,
not OS-level CPU/RAM or adversarial descendant processes. The worker
must use explicit validated executable paths, never arbitrary PATH resolution.
Use software decoding first; hardware decoding is a separate qualification.

## Reproduce the isolated engineering probe

Download the exact archive_url from decoder-lock.json into the ignored
evidence-vault/decoder-c14b folder. Compare its SHA-256 to archive_sha256 BEFORE
extracting to a new empty directory. Retain the included LICENSE and README.txt.
Do not use a partial download or silently substitute another release.

Run `node apps/windows-coordinator/decoder/probe.mjs ABS_ARCHIVE ABS_EXTRACTED_BIN_DIRECTORY`.
The probe rechecks archive/executable hashes, uses explicit paths, and writes only
new synthetic files/report beneath evidence-vault/decoder-c14b/probe-*.
It uses a synchronous, 30-second/1-MiB-bounded subprocess helper for this short
engineering probe only. It is NOT a production asynchronous/cancellable worker,
and does not read capture packages, change journal state or issue verification records.
Guard regressions are included in evidence-control test:contracts; actual binary
execution requires the locally downloaded candidate and is not part of hosted CI.

Tested profile: synthetic H.264/MP4, 160x120, 30 fixed-interval frames plus 20
variable-interval frames; software decode, invalid/truncated rejection, unchanged
valid input. No general codec, Windows 10, phone, camera or clinical qualification.

Runtime integration and redistribution are different gates. The publisher labels
these builds GPLv3; this is not a legal determination that redistribution with
HumCapture is approved. Legal review, vulnerabilities and supplier/binary coverage
remain open. No system Chocolatey/PATH installation is changed.

Primary sources checked 2026-09-15:

- https://ffmpeg.org/download.html — upstream provides source and links Windows builders.
- https://www.gyan.dev/ffmpeg/builds/ — version, Windows requirement and licence declaration.
- https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.1-essentials_build.zip.sha256 — pinned publisher checksum.
- https://github.com/GyanD/codexffmpeg/releases/tag/9.0.1 — versioned builder release/source reference.
- https://ffmpeg.org/legal.html — upstream licence/legal considerations.

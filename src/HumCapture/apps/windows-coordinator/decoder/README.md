# Planned decoder dependency

C14A selects an exact FFmpeg/ffprobe archive identity, not an enabled worker.
decoder-lock.json pins Gyan Windows x64 essentials 9.0.1 and the publisher-declared
archive SHA-256. No binary has been downloaded/verified/installed by this change.
The SBOM component is explicitly excluded/planned and records these limitations.

Before worker activation: obtain the pinned archive, verify bytes before extraction,
record executable hashes and version/build configuration, retain licences/source
provenance and embedded-library inventory, run valid/corrupt/truncated synthetic
media tests, and add bounded cancellation/output/time/resource handling. The worker
must use explicit validated executable paths, never arbitrary PATH resolution.
Use software decoding first; hardware decoding is a separate qualification.

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

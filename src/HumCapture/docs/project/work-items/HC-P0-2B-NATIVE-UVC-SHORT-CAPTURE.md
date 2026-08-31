# HC-P0-2B — Native UVC Short Capture

**Tags:** P0.2B | UVC | CORE | CAPTURE | TIMING | HARDWARE

## Problem

Enumeration alone does not prove exact media-type selection, per-sample timing,
or safe media finalization through the selected native Media Foundation boundary.

## Goal

Implement and exercise a bounded native Windows x64 diagnostic that rejects the
unavailable 1080p60 profile without substitution and records a short exact native
H.264 1080p30 stream independently from each attached Logitech C920.

## Scope

- Native Media Foundation Source Reader and Sink Writer diagnostic.
- Exact device-interface and native-media-type selection.
- Sequential, single-camera diagnostics on both named C920 devices.
- Raw source presentation timestamps plus independently observed host QPC time.
- Machine-readable result, frame/sample CSV, and finalized MP4 outside source control.
- Media readability check with `ffprobe`.

## Non-goals

- Concurrent two-camera capture.
- Preview, UI, coordinator integration, transfer, repository commit, or subject data.
- 30-minute qualification, disconnect/reconnect, external hub qualification, or
  clinical/regulatory claims.
- Treating 1080p30 as a passing 1080p60 procedure.

## Acceptance criteria

- [x] Native x64 source builds with warnings treated as errors.
- [x] Exact device selection rejects missing and ambiguous identities.
- [x] Exact H.264 1920×1080 at 60 fps request returns
      `REQUESTED_PROFILE_UNSUPPORTED`, creates no media, and performs no fallback.
- [x] Each named C920 independently negotiates exactly native H.264 1920×1080 at
      30/1 fps.
- [x] Each short run records non-empty sample evidence with original source
      presentation time and separately converted host-QPC arrival time.
- [x] Timestamp regression, stream ticks, media-type changes, and sample flags are
      visible rather than discarded.
- [x] Sink finalization succeeds and `ffprobe` reads a 1920×1080 H.264 video stream.
- [ ] Measured delivery rate is 29–31 fps for each exact 30/1 diagnostic; do not
      substitute the negotiated or encoded nominal rate for this measurement.
- [x] Runtime video/evidence remains outside source control.
- [x] P0.1 shared evidence tests remain green and project/risk/traceability records
      distinguish diagnostic hardware evidence from qualification.

## Dependencies and blockers

- P0.1 gate: passed.
- P0.2A API spike: passed.
- ADR-0006: accepted by the user on 2026-08-31.
- Two Logitech C920 cameras: present; native H.264 1080p30 reported.
- 1080p60 UVC hardware: unavailable and explicitly not required for this diagnostic.

## Status

blocked — implementation and most runtime criteria pass, but C920 A delivered
approximately 25.98 fps in round 1 and 24.00 fps in round 2. The two-round
acceptance limit is exhausted. C920 B delivered approximately 29.92 fps.

## Execution gate

blocked — determine whether C920 A low-light/exposure behavior, device controls,
USB topology, or the duplicate native profile is responsible before a new run

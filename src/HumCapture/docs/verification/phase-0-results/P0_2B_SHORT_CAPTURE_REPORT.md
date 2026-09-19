# P0.2B Native UVC Short-Capture Report

**Report ID:** HC-P0-VR-002B  
**Date:** 2026-08-31  
**Disposition:** Source implemented; hardware acceptance blocked by C920 A measured cadence  
**Tags:** P0.2B | UVC | CORE | CAPTURE | TIMING | HARDWARE

## Scope

This report covers an isolated native Media Foundation x64 diagnostic, exact
device and H.264 profile selection, per-sample timing evidence, MP4
finalization, and short sequential runs on two attached Logitech C920 cameras.
It is not coordinator code, a production UVC worker, concurrent capture, a
30-minute qualification, a field workflow, or regulatory/clinical evidence.

## Implemented diagnostic

- Exact device-interface selection; missing or ambiguous identity is rejected.
- Exact native media-type selection with conversion disabled.
- Explicit rejection of unavailable H.264 1920×1080 at 60/1 with no fallback.
- H.264 pass-through to a finalized MP4 through Media Foundation Sink Writer.
- Raw Media Foundation presentation timestamps in 100 ns ticks.
- Separate host-arrival observations from `QueryPerformanceCounter`, converted
  to nanoseconds without representing them as camera-sensor time.
- Sequence, discontinuity, stream-tick, media-type-change, sample-flag,
  encoded-size, and timestamp-regression evidence.
- Machine-readable result JSON and frame CSV, plus an `ffprobe` verifier.

## Named hardware

| Label | Device interface | Parent serial | USB location |
|---|---|---|---|
| C920 A | `USB\VID_046D&PID_082D&MI_00\6&DBA5B52&2&0000` | `0E1A0C0F` | `USB(1)#USB(2)` |
| C920 B | `USB\VID_046D&PID_082D&MI_00\7&2C44E5B7&0&0000` | `0352323F` | `USB(1)#USB(3)` |

Both used native type index 560, reported as H.264 1920×1080 at 30/1. The
same signature is also exposed at index 567, so the diagnostic correctly
requires an explicit index rather than choosing silently.

## Verification evidence

### Build and negative paths

- Release x64 rebuild with warning level 4 and warnings-as-errors: passed,
  zero warnings and zero errors.
- Native selection self-tests: `SELF_TEST_PASS tests=8`.
- Exact H.264 1080p60 request on C920 A: exit 3,
  `REQUESTED_PROFILE_UNSUPPORTED`, and no output directory created.
- H.264 1080p30 without an index: exit 3,
  `REQUESTED_PROFILE_AMBIGUOUS`, and no output directory created.

### Short hardware runs

| Camera/run | Negotiated | Samples | Duration | Measured average | Result |
|---|---:|---:|---:|---:|---|
| C920 A round 1, `C90DE56D-79BE-449C-8456-E2C5CE439AD4` | exact 1080p30 H.264 | 79 | 3.041333 s | 25.9752 fps | Fail: below 29–31 fps acceptance band |
| C920 A round 2, `87D3B2EF-F366-4822-B95B-E2A426E9BA85` | exact 1080p30 H.264 | 121 | 5.041300 s | 24.0016 fps | Fail: reproduced below band |
| C920 B, `9A9D066F-5B5B-4A4C-BCFE-6D3CCDAF181E` | exact 1080p30 H.264 | 91 | 3.041267 s | 29.9217 fps | Pass |

Every run finalized a readable H.264 1920×1080 MP4, had a decoded-frame count
equal to its recorded sample count, recorded non-empty timing rows, reported one
stream tick, and reported zero media-type changes and zero source-timestamp
regressions. Runtime artifacts are under the signed-in account's temporary
directory, outside source control.

The MP4 bitstream/container reports `r_frame_rate=60/1` even though measured
average rate and source timestamps show approximately 24–30 fps. Therefore the
nominal stream value is not accepted as measured capture rate.

## Disposition and next diagnostic

P0.2B source implementation is available, and C920 B passed the bounded short
diagnostic. Overall P0.2B acceptance is blocked because C920 A failed the
measured-rate check twice. Before another acceptance run, inspect C920 A
exposure/low-light controls, compare native indices 560 and 567 explicitly, and
recheck its USB topology. Preserve the two failed runs as diagnostic evidence.

Later P0.2C indicator runs observed C920 A at approximately 24 fps and then
approximately 30 fps under the same requested/negotiated profile. This shows
variability rather than resolving the cadence condition; P0.2B remains blocked.

This does not fail or pass the overall P0.2 gate. Soak, disconnect/reconnect,
P0.1 evidence-package integration, concurrent capture, and a real 1080p60 UVC
device remain open.

## Evidence levels

- Source implemented: **Yes**, for the isolated P0.2B diagnostic.
- Build/static checks passed: **Yes**.
- Automated behavior verified: **Yes**, for eight native selection tests.
- Runtime integration verified: **Partial**; exact capture/finalization works,
  but one named camera failed the measured cadence criterion.
- Hardware-in-the-loop verified: **Partial/conditional**; C920 B passed the
  short run, C920 A did not.
- Field workflow verified: **No**.
- Regulatory or clinical review completed: **No**.

# HC-P0-2D — Concurrent C920 Diagnostic

**Tags:** P0.2D | MULTI-UVC | LOGITECH-C920 | USB | H264 | MEDIA-INTEGRITY | HARDWARE

## Goal

Determine whether the two attached C920 units can concurrently produce valid
native H.264 1920×1080 at 30/1 streams on the named Windows/driver/USB
configuration. This does not create a universal two-camera requirement.

## Acceptance criteria

- [x] Both exact device identities can be opened concurrently without substitution.
- [x] Both processes finalize separate artifacts and release their media sources.
- [x] Requested, negotiated, measured, and decoded results are assessed per camera.
- [ ] Both streams decode completely without media errors in two repeat runs.
- [x] Findings are bounded to the named cameras, driver, ports, and host.
- [x] Verification rejects decoder corruption even when frame counts appear complete.

## Status

blocked — simultaneous opening works, but C920 A produced repeatable H.264 decode
corruption in both concurrent runs while C920 B remained clean

## Execution gate

blocked — do not approve this configuration for protocol-required concurrent
capture until driver/USB/profile reconciliation and clean repeat evidence pass


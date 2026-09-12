# HC-P0-2F — Concurrent C920 Physical Port Swap

**Tags:** P0.2F | MULTI-UVC | PORT-SWAP | USB-TOPOLOGY | H264 | MEDIA-INTEGRITY | HARDWARE

## Goal

Determine whether concurrent H.264 corruption follows a physical C920/cable or
the USB port/path when the two camera connections are exchanged.

## Acceptance criteria

- [x] Parent serial and Windows interface identity are correlated before capture.
- [x] The two physical cameras exchange USB paths 2 and 3.
- [x] Two concurrent exact H.264 1080p30 runs finalize independently.
- [x] Every stream undergoes full-decode validation.
- [x] Failure-following-camera versus failure-following-port is recorded.

## Status

done — corruption moved with USB path 2 to physical C920 B in both repeats;
physical C920 A became clean on path 3

## Execution gate

closed — camera/cable hypothesis not supported as primary cause; alternate
port/controller reconciliation is next


# HC-P0-2E — Reverse Concurrent Start Order

**Tags:** P0.2E | MULTI-UVC | START-ORDER | H264 | MEDIA-INTEGRITY | HARDWARE

## Goal

Determine whether P0.2D corruption follows C920 A or the first camera started.

## Acceptance criteria

- [x] C920 B starts first and C920 A starts three seconds later.
- [x] Both exact identities capture concurrently and finalize independently.
- [x] Each file undergoes complete decode validation.
- [x] Result is recorded without generalizing beyond the named configuration.

## Status

done — corruption followed C920 A when it started second; C920 B remained clean
when it started first

## Execution gate

closed — startup-order hypothesis tested; physical port-swap diagnostic is next


# HC-P0-2H — Controlled Exposure Cadence Diagnostic

**Tags:** P0.2H | MULTI-UVC | CAMERA-CONTROL | EXPOSURE | CADENCE | H264 | HARDWARE

## Goal

Test whether holding Camera B at its reported default exposure value prevents
automatic low-light exposure from reducing source cadence on the reconciled USB
topology.

## Acceptance criteria

- [x] Camera B exposure state and supported range are read from the exact device.
- [x] Camera B is temporarily set to manual exposure `-5` with user authorization.
- [x] A single-camera run passes cadence, timestamp, finalization, and full-decode checks.
- [x] Two concurrent runs pass independently for both exact cameras.
- [x] Camera B is returned to automatic exposure and independently re-read.

## Status

done — Camera B passed alone and both cameras passed two simultaneous 30-second
runs at approximately 30 fps with zero decode errors under the tested topology
and temporary manual exposure condition

## Execution gate

closed for the short diagnostic only — exposure was restored to automatic;
configuration persistence, scene-range testing, soak, and disconnect/reconnect
evidence remain open before qualification


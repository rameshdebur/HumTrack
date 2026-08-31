# HC-P0-2G — Alternate Root-Port Topology

**Tags:** P0.2G | MULTI-UVC | USB-TOPOLOGY | ROOT-PORT | H264 | CADENCE | HARDWARE

## Goal

Determine whether moving the camera from the previously corrupt downstream USB
path to a direct root-port branch removes concurrent H.264 corruption without
changing the selected native capture profile.

## Acceptance criteria

- [x] Exact camera identities and new Windows location paths are recorded.
- [x] Both cameras retain exact native H.264 1920x1080 at 30 fps, profile 560.
- [x] A concurrent 30-second run is finalized and fully decoded per stream.
- [x] The moved camera is also tested alone to distinguish concurrency from
      camera/control cadence behavior.
- [x] Corruption and cadence are reported separately.

## Status

done — the prior H.264 corruption was not reproduced on the direct root-port
branch, but Camera B delivered approximately 15 fps both concurrently and alone
under automatic exposure; a well-lit retry improved cadence without meeting the
acceptance gate

## Execution gate

closed — port/path reconciliation succeeded for short-run media integrity;
exposure/cadence reconciliation continued in P0.2H


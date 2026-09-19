# HC-P0-2A — Windows Camera API Spike

**Tags:** P0.2A | UVC | CORE | ENUMERATION | HARDWARE

## Problem

HumCapture requires a Windows/UVC probe, but the permanent camera API boundary
must not be chosen before comparing the managed Windows capture surface with
native Media Foundation on the available hardware.

## Goal

Produce an isolated, non-production Windows x64 spike that compares camera
identity and reported-format visibility through both API surfaces and records a
reviewable architecture recommendation.

## Scope

- `tools/capability-probes/windows/` only for executable source.
- Managed Windows camera enumeration and format inspection.
- Native Media Foundation device and media-type enumeration.
- Explicit errors and unavailable values; no silent profile substitution.
- Runtime comparison on the integrated HP Wide Vision HD USB/UVC camera.
- Proposed ADR and Phase 0 verification/state updates.

## Non-goals

- Production coordinator or UVC-worker implementation.
- Scientific-master recording, preview, transfer, subject data, or UI.
- Sustained, disconnect/reconnect, multi-camera, or external-hub qualification.
- Clinical, regulatory, certification, or generic-hardware claims.

## Acceptance criteria

- [x] Managed and native projects build for Windows x64.
- [x] Both surfaces enumerate video-capture devices and emit machine-readable
      output without substituting an unrequested device.
- [x] The integrated HP camera is correlated by stable Windows identity.
- [x] Reported formats are preserved as reported, including rational frame rate.
- [x] Initialization or permission failures are explicit and machine-readable.
- [x] Synthetic self-tests cover identity normalization, selection ambiguity,
      and exact device selection.
- [x] A proposed ADR records the comparison, selected boundary, trade-offs, and
      deferred evidence.
- [x] Source, build, automated, runtime, hardware, field, and regulatory evidence
      levels are reported separately.

## Dependencies and blockers

- P0.1 evidence-contract gate: passed.
- Integrated HP Wide Vision HD Camera: present and identified by Windows as
  USB Video Class (`usbvideo`).
- External UVC/hub hardware: unavailable; not required for this spike.

## Status

done

## Execution gate

allowed — accepted Phase 0 plan and passed P0.1 prerequisite

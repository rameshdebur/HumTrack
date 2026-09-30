# ADR-0006 — Use native Media Foundation for Windows/UVC acquisition

**Status:** Accepted  
**Date:** 2026-08-31
**Tags:** P0.2A | UVC | CORE | API-SELECTION | HARDWARE

## Context

P0.2 requires truthful UVC identity, exact media-type negotiation, per-sample
timestamps and provenance, device-loss handling, controls, and finalization on
Windows 11 with a Windows 10 22H2 technical floor. The first available named
device is the integrated HP Wide Vision HD Camera, exposed by Windows through
the USB Video Class driver.

The P0.2A spike compared:

- managed `DeviceInformation` plus `MediaCapture`; and
- native Media Foundation `MFEnumDeviceSources` plus `IMFSourceReader`.

Both surfaces returned the same 17 unique format signatures for the HP camera.
On each of two attached Logitech C920 cameras, native Media Foundation exposed
455 unique signatures while managed `MediaCapture` exposed 336; the 119
native-only signatures were H.264 profiles. Managed `VideoDeviceController`
exposed standard control ranges. Native Media Foundation also exposes the
lower-level media type and sample-reader boundary required for the next
timestamp, discontinuity, device-loss, and finalization experiments.

## Decision

Use native Media Foundation as the P0.2 Windows/UVC acquisition boundary.

Use managed Windows device enumeration only as optional identity/control
enrichment where it adds information without opening or owning the acquisition
stream. Use Windows PnP/SetupAPI surfaces for driver, USB identity, and topology
evidence. Do not open one camera concurrently through managed and native capture
surfaces in the production direction.

The Phase 0 implementation begins with `IMFSourceReader` for bounded diagnostic
sample acquisition. Acceptance of this API direction does not accept its
hardware behavior: P0.2 must separately verify timestamp semantics, exact
requested versus negotiated media types, queue behavior, device loss, and
finalized media. If Source Reader cannot preserve the required evidence, a Media
Foundation capture-engine or custom media-session path requires a new or
superseding decision.

## Alternatives considered

### Managed `MediaCapture` for acquisition

- Advantages: concise .NET API; direct standard-control access; complete format
  inventory on the tested camera.
- Disadvantages: abstracts sample and pipeline behavior needed for the scientific
  timestamp/provenance and device-loss experiments; mixing it with a separate
  native owner would add contention and divergent state.

### Native Media Foundation for acquisition

- Advantages: direct media types, samples, timestamps, HRESULTs, and device
  lifecycle; compatible with isolated native UVC workers.
- Disadvantages: COM lifetime and resource management are more complex; camera
  controls and USB topology need additional Windows APIs; source-reader
  timestamp meaning still requires measurement and explicit provenance.

### Separate managed and native production capture paths

- Rejected because two authoritative capture paths would duplicate negotiation,
  failure, and finalization behavior and increase qualification scope.

## Rationale

The scientific acquisition boundary needs the least-abstracted Windows surface
that can expose native media types, samples, timestamps, and device failures.
The spike found no format-inventory advantage in keeping managed capture as a
second acquisition implementation. A single native owner also aligns with the
per-source worker isolation accepted in ADR-0003.

## Consequences

- The production direction requires a native Windows/UVC worker boundary.
- .NET/Avalonia coordinator code must communicate with that worker through a
  future versioned interface rather than owning camera capture.
- Standard controls and PnP/USB information require deliberate adapters.
- Device inventory must use the Windows video-capture interface selector and
  PnP correlation, not only the PnP `Camera` class; both tested C920 interfaces
  were classified as `Image`.
- Unsupported or unavailable timestamp/topology/control values remain explicit.
- No generic UVC, 1080p60, synchronization, or hardware qualification claim
  follows from this API selection.

## Affected components and interfaces

- `tools/capability-probes/windows/` Phase 0 diagnostics.
- Future `services/uvc/` worker architecture.
- Future UVC capability/configure/status/finalize interface.
- Timing/provenance and Phase 0 evidence generation.

## References

- [Microsoft: Audio/Video Capture in Media Foundation](https://learn.microsoft.com/en-us/windows/win32/medfound/audio-video-capture-in-media-foundation)
- [Microsoft: Set format, resolution, and frame rate for MediaCapture](https://learn.microsoft.com/en-us/windows/apps/develop/camera/set-media-encoding-properties)
- [Microsoft: USB topology address](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_topology_address)

## Supersedes / Superseded by

None. Complements ADR-0003.

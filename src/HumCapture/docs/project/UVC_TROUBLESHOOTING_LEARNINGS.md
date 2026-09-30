# UVC Capture Troubleshooting Learnings

**Document ID:** HC-OPS-UVC-001  
**Status:** Active Phase 0 engineering guidance  
**Evidence basis:** P0.2B–H, two physical Logitech C920 cameras, 2026-08-31  
**Tags:** UVC | TROUBLESHOOTING | USB-TOPOLOGY | EXPOSURE | CADENCE | MEDIA-INTEGRITY

## Reusable principle

A camera that opens, reports the requested profile, produces the expected frame
count, or finalizes a playable file has not thereby passed acquisition. Treat
the exact camera, driver, native profile, USB path/controller, camera controls,
scene conditions, and concurrent load as one testable configuration.

## Diagnostic order

1. Record stable physical identity, Windows interface identity, parent serial,
   driver, native profile index, and complete USB location path.
2. Keep requested, reported, negotiated, container-nominal, and measured cadence
   separate. Use source timestamps and decoded duration/frame count for cadence.
3. Fully decode every required stream. A frame count or successful finalization
   can coexist with H.264 corruption.
4. Repeat once before changing conditions to establish reproducibility.
5. Reverse source start order. If the failure does not move, startup order is
   not the supported cause.
6. Swap the physical cameras between the same ports. A failure that follows the
   camera suggests camera/fixed-cable/identity behavior; a failure that follows
   the path suggests port/hub/controller/driver interaction.
7. Move the failing path to a direct root-port branch or genuinely different
   controller, remap identities, and repeat full-decode testing.
8. Test the affected camera alone. This separates concurrency from camera,
   control, lighting, or profile behavior.
9. Inspect exposure and other cadence-affecting controls. Change one control at
   a time only with authorization, record its initial state, restore it in a
   guaranteed cleanup path, and independently read it back afterward.
10. Require repeat short passes, then soak and recovery evidence. Preserve a
    restriction as conditional when it depends on a named port, controller,
    profile, exposure policy, scene range, or power state.

## Product reconciliation rule

For a protocol-required source, HumCapture shall not silently accept degraded
cadence, corrupt media, or an unqualified topology. Readiness should either:

- apply and verify a protocol-qualified configuration;
- offer an explicit supported reconciliation, such as another qualified port,
  profile, exposure policy, or reduced protocol source count; or
- reject that source combination with an actionable reason.

An optional second camera remains optional unless the selected protocol requires
it. A single-camera protocol is not blocked by failure of an unused multi-camera
configuration.

## Evidence boundary

P0.2G–H showed that corruption followed one USB path rather than either camera
body, and that automatic-exposure behavior could reduce cadence while temporary
manual exposure `-5` restored approximately 30 fps in the tested scene. This is
named conditional evidence, not a universal Logitech rule, hardware
qualification, or a production control policy.


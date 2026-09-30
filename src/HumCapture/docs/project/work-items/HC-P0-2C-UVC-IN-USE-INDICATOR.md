# HC-P0-2C — UVC In-Use Indicator

**Tags:** P0.2C | UVC | HARDWARE | IN-USE-INDICATOR | PRIVACY | OPERATOR

## Goal

Verify the manufacturer-documented C920 activity indicators against exact
Windows device identity, active fresh-frame delivery, source finalization, and
release. Indicator absence is not a universal camera failure; this work applies
because both tested C920 units document and provide an indicator.

## Acceptance criteria

- [x] Indicator applicability is classified without creating a universal LED requirement.
- [x] C920 A has two consistent off/on/off observations during exact-identity capture.
- [x] C920 B has two consistent off/on/off observations during exact-identity capture.
- [x] The non-selected camera remains off and the two Windows identities address different physical units.
- [x] Every associated capture finalizes, contains fresh readable H.264 1920×1080 frames, and releases the source.
- [x] Indicator evidence is explicitly excluded from timing and synchronization claims.
- [x] Requirements, risk, traceability, verification, and project-state records are updated.

## Status

done — accepted diagnostic evidence for the two named C920 configurations only

## Execution gate

closed — all acceptance criteria verified on 2026-08-31


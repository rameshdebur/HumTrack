# UVC In-Use Indicator Test Protocol

**Document ID:** HC-VP-UVC-LED-001  
**Status:** Accepted for Phase 0 and future device qualification  
**Tags:** P0.2C | UVC | HARDWARE | IN-USE-INDICATOR | PRIVACY | OPERATOR

## 1. Purpose

Verify that a UVC camera's manufacturer-documented physical in-use indicator
truthfully corresponds to acquisition-stream ownership. This is an operator and
privacy safeguard. It is not a timestamp source and shall not be used for
synchronization, frame timing, start-time measurement, or scientific evidence.

## 2. Applicability

An activity indicator is an optional camera capability, not a universal
HumCapture hardware requirement. A camera is not rejected merely because it was
designed without one.

First classify the exact model as `documented_present`, `documented_absent`, or
`unknown`. Run the off/on/off observation only for `documented_present`.
`documented_absent` is `NOT_APPLICABLE` and does not block readiness or
qualification. If manufacturer information is unavailable, record `unknown`;
do not invent an indicator requirement. An indicator that is documented or
physically present but inaccessible, obscured, or unobservable during its
required test is `INCONCLUSIVE`, not a pass.

For the Logitech C920, the manufacturer states that its activity light
illuminates while an application is using the webcam:

- [Logitech C920 Quick Start Guide](https://www.logitech.com/assets/45920/8/hd-pro-webcam-c920-quick-start-guide.pdf)

## 3. Preconditions

- Identify the exact Windows device-interface ID, model, serial or stable parent
  identity, driver version, and physical USB location.
- Correlate the selected Windows identity with the physical camera being watched.
- Close other applications that may own the camera.
- Use synthetic test content and no subject data.
- Position the camera so the trained operator can see the indicator directly.
- Record ambient lighting sufficient to see the indicator.

## 4. Procedure

Test one camera at a time.

1. With the camera connected but unopened, observe the indicator for at least
   five seconds. Record `baseline_off`, `baseline_on`, or `not_observable`.
2. Start exact-profile acquisition and keep it active for at least 30 seconds.
3. During active frame delivery, record `active_on`, `active_off`,
   `intermittent`, or `not_observable`.
4. Stop acquisition, finalize the media, and release/shut down the media source.
5. Observe for at least five seconds and record `released_off`, `released_on`,
   or `not_observable`.
6. Confirm that the media contains fresh frames from the same selected device.
7. Repeat once. A qualification decision requires consistent results.

The observation record shall include operator identity, local observation time,
device identity, requested and negotiated profile, physical USB location,
application/process identity, the three observations, comments, and links to
the associated capture result. Observation time is administrative provenance,
not capture timing.

## 5. Result rules

For a camera classified `documented_present`:

- **PASS:** baseline is off, the indicator remains visibly on while fresh frames
  are delivered, and it turns off after the source is released, in both runs.
- **CONDITIONAL:** correct behavior requires a documented approved setting or
  operating condition that can be checked during preflight.
- **FAIL:** fresh frames are delivered while the indicator is off; the indicator
  remains on after confirmed source release; or the behavior is inconsistent
  after reconciliation and repeat.
- **INCONCLUSIVE:** physical identity, visibility, other-process ownership, or
  observation conditions prevent a defensible decision.

Capture API state remains authoritative for system state. A physical indicator
observation does not prove recording, finalization, identity, frame cadence, or
timestamp integrity.

## 6. Reconciliation and rejection

On mismatch:

1. Block qualification of the named camera/configuration.
2. Verify physical-to-Windows identity mapping and check for another process
   retaining the camera.
3. Inspect manufacturer software/firmware settings and Windows driver state.
4. Repeat on a direct USB port under observable conditions.
5. If behavior becomes correct only under a reproducible approved condition,
   classify the configuration `CONDITIONAL` and make that condition a preflight.
6. If the documented indicator still does not correspond to use, classify the
   named configuration `UNSUPPORTED_FOR_PROTOCOL` and reject it from approved
   capture selection. Do not silently waive the mismatch.

An indicator failure does not permit discarding an already acquired master.
Safely finalize it, record the deviation, and apply the protocol's quality and
retake decision.

## 7. Final-product behavior

- The coordinator shall show camera-in-use state from worker/API evidence and
  shall not infer it from the LED.
- Device qualification shall record indicator applicability and result.
- Absence of an indicator shall not block readiness when the exact camera model
  is classified `documented_absent`; normal API-state and capture-health checks
  remain mandatory.
- A documented-indicator mismatch shall block readiness unless an approved
  protocol/device policy explicitly permits a recorded conditional use.
- The operator shall receive the expected and observed state, reason, and
  reconciliation choices; status shall not be conveyed by color alone.

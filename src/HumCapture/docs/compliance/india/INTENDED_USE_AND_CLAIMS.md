# Intended Use and Claims Register

**Status:** Draft; not regulatory-approved

## Working intended-use statement

HumCapture is intended for a trained operator to acquire, coordinate, recover, verify, and organize video and device-sensor data from Android and UVC cameras during supervised motion-capture sessions for later analysis by compatible downstream software.

## Intended users and environment

- Trained clinician, researcher, laboratory technician, or motion-analysis technician.
- Laboratory, clinic, or field trial using local network/hotspot.
- Not subject self-capture, emergency use, continuous monitoring, or unsupervised home use.

## Outputs

- Original master videos.
- Timing, Android IMU, camera/lens, calibration, and synchronization evidence.
- Verification, acquisition-quality, and protocol-deviation reports.
- Versioned pseudonymized-by-default handoff package.

## Claims currently permitted internally

- Package files passed the specified schema/identity/length/hash checks.
- Capture conformed or deviated from a named protocol according to versioned rules.
- Software-derived clock models and uncertainty are preserved.

## Claims prohibited without new evidence and approval

- CDSCO approved, certified, or licensed.
- Medical-device class or conformity.
- Clinically accurate or diagnostically valid.
- Perfect, hardware-level, or exposure-level synchronization.
- Valid multi-camera 3D reconstruction.
- Suitable for treatment or emergency decisions.

## Change control

Any change to target population, operator, medical purpose, downstream decision, clinical claim, automation, deployment, or combined HumTrack boundary requires updated applicability, risk, requirements, architecture, verification, and qualified review.


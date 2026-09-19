# HumCapture Product Requirements Document

**Document ID:** HC-PRD-001  
**Version:** 0.1  
**Status:** Draft baseline from accepted product discussion  
**Date:** 2026-08-26

## Product statement

HumCapture is a local-first motion-capture acquisition system for a trained operator. It configures, coordinates, monitors, recovers, verifies, and organizes Android and UVC video/sensor recordings for later processing by HumTrack or another compatible analytics system.

HumCapture provides acquisition evidence and conformity information. It does not diagnose, recommend treatment, or guarantee downstream clinical validity.

## Primary user and environment

The primary user is a trained clinician, researcher, laboratory technician, or motion-analysis technician conducting a supervised capture. Subject self-capture, home capture, emergency use, continuous monitoring, and cloud collaboration are outside MVP scope.

Supported environments are laboratory, clinic, and field trial. Internet is not required. A dedicated AP is preferred; a Windows laptop hotspot is a supported qualified field profile.

## User problem

Uncoordinated cameras create separately started files, uncertain timing, lost device/lens/orientation evidence, inconsistent identity, manual-copy errors, premature deletion, and ambiguous completion. HumCapture creates one controlled flow:

```text
Subject → protocol → session/trials → cameras → readiness → capture
→ transfer/recovery → verification/commit → quality → handoff
```

## Product goals

- Repeatable single- and multi-source acquisition.
- Local masters protected from UI, preview, coordinator, and network failure.
- Measured timing, camera/lens, and Android IMU evidence.
- Common workflow for Android and UVC.
- Clear incomplete, recovery, verified, committed, and quality states.
- Network resume and USB/MTP recovery.
- Informed mobile cleanup only after durable commit.
- Versioned package usable without conversation history.

## Identity and capture hierarchy

```text
Subject → Session → Trial → Source recording
```

Required subject fields are subject code, full name, date of birth, controlled sex field, and height with explicit units. Full identity remains coordinator-local. Session fields may be protocol-dependent. Trials support planned repetitions, flexible additions, retakes, exclusions, and supersession without overwrite.

## Protocols

Protocols are reusable, versioned templates created occasionally and selected during routine capture. They define fixed or flexible source count, source roles/viewpoints, profiles, metadata, timing/calibration thresholds, maximum duration, trial plan, overrides, and completion.

The signed-in trained Windows operator may create, validate, and explicitly approve local MVP protocol versions. Approved versions are immutable; changes create new versions.

## Normal operator workflow

1. Select or create subject.
2. Select approved protocol.
3. Enter session metadata and review trial plan.
4. Choose source count when the protocol is flexible.
5. Assign Android/UVC sources and viewpoints.
6. Inspect previews and resolve readiness.
7. Confirm arming summary and scheduled start.
8. Monitor master and preview health separately.
9. Stop and finalize.
10. Continue later trials if local packages are safe and storage passes.
11. Transfer or recover all required packages.
12. Verify, commit, review quality, retake/exclude if needed.
13. Finalize complete or explicitly close incomplete.
14. Export handoff and make informed mobile-cleanup decisions.

## Android product behavior

- Guided permissions, device identity, pairing, capability/readiness, foreground scientific capture, local preview, emergency stop, finalization, inventory, USB export, receipt, and cleanup.
- Coordinator-controlled ordinary configuration.
- No full subject name/date of birth stored or displayed.
- Network/preview/coordinator loss does not stop master recording.
- No pause within a trial; interruption becomes a finalized attempt and retake.

## Coordinator product behavior

- Windows-account operator identity only for MVP.
- Dashboard, subjects, protocols, guided capture, active monitoring, sessions, transfer/recovery, devices, settings, backup/export, diagnostics.
- UI is not authoritative state; coordinator host and isolated UVC workers persist critical state.
- Startup reconciles database, filesystem, devices, transfers, and incomplete transactions.

## Readiness and capture

Readiness evaluates subject/protocol, source capabilities, preview, recorder, camera configuration, timing, calibration, storage, battery, thermal state, network, coordinator repository, and Windows power. Results are pass, warning, override required, blocking failure, or not assessed. Non-overridable failures cannot arm. Overrides require reason and persist.

All required sources arm before standard start. A future monotonic `START_AT` coordinates capture. Once recording starts, individual failure does not stop survivors. Normal and emergency stop, maximum-duration fallback, and actual first/final timestamp reporting are required.

## Transfer, completion, and cleanup

Automatic Android transfer is coordinator-pulled, resumable, authenticated, and post-capture. USB/MTP imports the identical finalized package. UVC packages use the same verifier locally. Completion requires protocol-required packages/trials to be verified and committed.

Android receives an idempotent receipt after commit and marks the package safe to delete. No silent automatic mobile cleanup or coordinator deletion exists in MVP.

## Quality and handoff

Quality review covers integrity, timing, discontinuities, profile, calibration, camera movement, exposure/focus, thermal, and overrides. Complete and quality are separate. The default handoff is pseudonymized and includes trial/source paths, timing/IMU, camera/calibration, clock models, quality, schemas, and hashes. Identified export is explicit and audited.

## Compatibility

- Primary: currently supported Windows 11 x64.
- Technical floor: Windows 10 22H2 x64 with lifecycle warning and named-hardware qualification.
- Android floor: Phase 0 decision.
- MVP sources: Android Camera2 and Windows UVC.
- Required scenarios: one Android, one UVC, two Android, mixed Android/UVC.

## Product quality targets

- Mandatory 1080p60 H.264 master; 1080p120 optional/unqualified.
- 720p15 minimum preview baseline.
- Thirty-minute Android/UVC soak tests.
- Provisional clock uncertainty ≤5 ms and first-frame alignment within one 60 fps interval, subject to evidence.
- Discovery target within 10 seconds on healthy tested LAN.
- Normal status/control target within one second.
- Preview latency target ≤500 ms on healthy tested LAN.
- No detected acquisition discontinuity concealed.

## Security, privacy, and compliance

Local-first, paired/authenticated control, encrypted transfer, no PII in discovery/Android/default logs, pseudonymized default export, explicit destructive actions, and append-oriented audit.

India/CDSCO is primary. Current official policy and applicable BIS/international standards must be reviewed before affected implementation and release. No approval, certification, device classification, clinical, or synchronization claim is authorized without defined evidence and qualified review.

## Training and support

Trained-operator documentation covers identity, protocols, camera placement, readiness, timing/calibration limitations, capture, recovery, quality, cleanup, export, privacy, and field operation. Ordinary use requires no command line, ADB, or network-administration knowledge. Local redacted support bundles exclude identity and master video by default.

## MVP release gates

Functional scenarios, integrity, timing evidence, recovery/fault tests, named hardware/OS matrix, security/privacy tests, representative trained-operator workflows, current documentation, traceability, risk controls, and applicable regulatory review must pass. Build or unit-test success alone is insufficient.

## Deferred

iOS, cloud, institutional identity, complex approval chains, automatic retention, public app stores, custom USB, hardware synchronization, default HEVC/ProRes/JPEG XS/raw, hardware trigger, 3D reconstruction, clinical claims, detailed UI, and direct HumTrack changes.


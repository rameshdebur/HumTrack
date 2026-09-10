# HumCapture Architecture Description

**Document ID:** HC-ARD-001  
**Version:** 0.1  
**Status:** Draft baseline from accepted design discussion  
**Date:** 2026-08-26

## 1. Objective and boundary

HumCapture is the bounded acquisition subsystem of HumTrack. It may inspect the wider HumTrack repository for UI and engineering principles but may modify only `src/HumCapture` without explicit user permission. It produces a verified package for downstream analysis; it does not implement HumTrack tracking, inference, biomechanics, or reporting.

## 2. Invariant

Nothing in preview, networking, UI, transfer, or downstream inference may compromise scientific master acquisition.

```text
Master capture and sensor timestamps
> timing and IMU metadata
> safe finalization
> control and health
> preview
> UI
> post-capture transfer
```

## 3. System context

```text
Android capture nodes ─┐
                      ├─ local network ─ Windows coordinator ─ verified repository
Windows UVC cameras ──┘                                      │
                                                             └─ versioned HumTrack handoff
```

MVP excludes iOS, cloud dependencies, hardware synchronization, custom USB transport, automatic 3D reconstruction, and direct HumTrack changes.

## 4. Capture hierarchy

```text
Subject
└─ Session (visit/protocol instance)
   └─ Trial (one continuous acquisition attempt)
      └─ Source recording (Android or UVC)
```

Required source packages complete a trial. Required trials complete a session. Retakes create new trial UUIDs and never overwrite originals.

## 5. Runtime components

### Android capture app

- Kotlin/Camera2 camera adapter.
- Capture controller and foreground service.
- Local H.264 master recorder.
- Per-frame timing writer.
- IMU writer for orientation/gravity/gyroscope/acceleration.
- Bounded H.264 preview encoder and RTP sender.
- mDNS advertiser and authenticated control server.
- Resumable transfer server and USB/MTP export.
- Package finalizer, local inventory, receipt, and cleanup handling.

### Windows coordinator

- Avalonia trained-operator UI.
- Headless coordinator host as session authority.
- Subject, protocol, session, and trial registry.
- Discovery, pairing, control, timing, and preview services.
- Transfer, integrity, repository, audit, and quality services.
- SQLite operational catalog plus self-contained filesystem packages.

### UVC workers

One isolated worker per configured source owns capture, timestamps, master writing, bounded local preview, health, and finalization. Generic UVC support initially targets the Windows camera stack. Timestamp provenance must remain explicit.

## 6. Source abstraction

Capture sources expose capability, configure, arm, scheduled start/stop, status, finalization, metadata, and package behavior. Supported MVP sources are Android Camera2 and coordinator-attached UVC. Protocols may require one or more sources; the coordinator never imposes two cameras globally.

Required MVP scenarios are one Android, one UVC, two Android, and mixed Android/UVC.

## 7. Master and preview

Mandatory master target is 1920×1080 at 60 fps using H.264. 1080p120 is optional and capability-gated. Exposure, focus, crop/zoom, orientation, and stabilization configuration are locked after arming where supported.

Android preview baseline is H.264 1280×720 at 15 fps over RTP/UDP. Preview queues are bounded and drop frames under pressure. UVC preview is local. Preview is never used to reconstruct or validate masters.

## 8. Camera and IMU metadata

Record camera/device/lens identity, resolution, codec, sensor/crop geometry, focal length, focus, exposure, ISO, frame duration, stabilization, orientation, timestamp source, rolling-shutter information when reported, and calibration provenance.

Android IMU sidecar records time-stamped orientation/rotation vector, gravity, gyroscope, accelerometer, and linear acceleration where available. Device, camera-optical, gravity-aligned, and global coordinate frames must be defined. Magnetometer is optional and no absolute-heading claim is made.

## 9. Timing model

Preserve camera sensor, IMU, device monotonic, coordinator monotonic, and estimated global session time. Repeated two-way exchanges fit:

```text
T_global = a × T_device + b
```

Raw exchanges, RTT, accepted/rejected samples, fitted offset/drift, uncertainty, and model intervals are stored. Coordinated capture uses a future `START_AT`; wall clock is not the scientific timing basis.

Provisional targets are clock-fit uncertainty no greater than 5 ms and first-frame alignment within one 60 fps frame interval. These are software-derived and must be confirmed or revised by Phase 0; no hardware-synchronization claim is permitted.

## 10. Discovery, trust, and control

Android advertises `_humcapture._tcp` by mDNS. The coordinator browses, resolves,
and initiates pairing/control. Manual endpoint fallback remains available.
Discovery is untrusted. ADR-0013 and HC-IF-SEC-001 require an attended QR or
high-entropy manual bootstrap that binds both peer public keys, followed by
mutual TLS for control and transfer. One enrolled coordinator controls a phone.

MVP control uses versioned JSON over authenticated encrypted WebSocket. Large data never travels in control messages. Commands are acknowledged, state-validated, and idempotent where practical. Reconnection begins with actual-state reconciliation.

## 11. Session state and failure behavior

ADR-0009 separates coordinator workflow, source capture-attempt, package custody,
and observed health authorities. Commands request transitions; authoritative
events prove physical acquisition and durable commit. The UI is an observer.

ADR-0010 binds each planned session to an immutable content-hashed protocol
snapshot. The session progression is draft/planned/in-progress/completion-review/
complete, with recovery-required, cancelled-before-capture, and
closed-incomplete outcomes. Pre-capture plan changes create superseding snapshot
revisions; after any master sample, material protocol change requires incomplete
closure and a new session.

ADR-0011 preserves JSON monotonic instants as canonical unsigned 64-bit decimal
ticks with explicit clock identity and frequency. Mapped session time also
identifies its clock model and uncertainty. A new boot/clock epoch requires a
new capture attempt; UTC remains audit/display evidence rather than the
scientific ordering clock. Content-addressed JSON records hash their RFC 8785
canonical UTF-8 representation with their own content-hash property omitted,
so equivalent serialization order does not change identity.

The accepted trial progression is draft/configuring/preflight/ready/arming/armed/
start-scheduled/recording/stopping/finalizing/collecting/verifying/review-required/
complete, with cancelled, recovery-required, and closed-incomplete outcomes.
Source attempts and packages have separate lifecycles through finalization,
commit receipt, and safe-to-delete. `docs/interfaces/CONTROL_STATE_MACHINE.md`
version 1.2.0 and its executable session/source schema slices are the accepted
engineering interface baseline. HC-IF-SEC-001 version 1.0.0 binds control WSS
and transfer HTTPS to attended enrollment, mutual certificates, active trust,
peer role/identity, resource authorization, and replay controls. Binary
timing/IMU formats and application implementation remain separately gated.

All protocol-required sources must be ready before standard start. Once recording begins, one source failure does not stop surviving sources. Android network/coordinator/preview loss does not stop local master recording. Maximum duration and local emergency stop provide fallback.

## 12. Protocol-driven capture

Reusable, versioned protocols define purpose, fixed or flexible source count, source roles, master/preview profiles, timing/calibration rules, required metadata, maximum duration, trial plan, overrides, and completion. Approved versions are immutable. A session stores a complete snapshot.

## 13. Subject and repository

Subject folders use immutable UUIDs. Full name is required identity but is not a folder identifier and remains coordinator-local. Session packages carry subject UUID/code rather than full identity by default.

```text
DATA_ROOT/subjects/SUBJECT_UUID/sessions/SESSION_UUID/packages/PACKAGE_UUID/
```

Incoming data uses staging, validation, verification, and transactional commit.
ADR-0016 requires repository-local same-volume staging and a recoverable journal
across atomic package rename, SQLite catalog, custody, verification, and commit
state. Only reconciled agreement becomes `COMMITTED` or permits a receipt.
Identical reimports are idempotent; conflicting identities/hashes are
quarantined. Runtime data lives outside the source checkout. Required subject
name/demographics remain Coordinator-local while UUID is the authoritative
folder identity. ADR-0017 and HC-IF-REP-001 define the shallow UUID namespace:
`repository.json`, `catalog/`, `staging/`, `quarantine/`, and `subjects/`.
Trial/source/attempt identities remain in the package manifest and catalog, not
directory depth. Package contents and internal relative paths remain exactly as
verified. ADR-0018 makes SQLite the sole mutable workflow/PII authority while
immutable protocol-snapshot, verification, commit, receipt, quality,
completion, and handoff JSON records live under the session `records/`
namespace and are indexed by SQLite. Descriptor, catalog, journal and record
schemas remain later I0.4B work. ADR-0019 refines repository commit into
`STAGED_VERIFIED`, `COMMITTING`, `MOVED`, `CATALOGED`, and `COMMITTED`, with
explicit recovery/quarantine dispositions at crash boundaries; these are
internal durability states rather than additional operator workflow steps.

## 14. Transfer and recovery

Android finalizes immutable self-contained packages before exposing them. The coordinator pulls manifests/files using authenticated resumable HTTPS range requests, verifies required files, lengths, schemas, and SHA-256, then commits. UVC follows the same finalization/verification path without network transfer.

USB/MTP recovery imports the identical package through the common verifier. Network failure alone creates a recoverable workflow, not a recording failure. Completion receipts are sent only after durable commit. Android deletion remains explicit and user-confirmed.

## 15. Completion and quality

Recording stop, local finalization, transfer, verification, commit, trial completion, and session completion are distinct. Acquisition quality is assessed separately from workflow completion across integrity, timing, profile conformance, calibration, movement, thermal, and deviations.

Quality results do not establish clinical validity. Operators may accept protocol-defined overridable deviations with reasons; original evidence remains.

## 16. Security, privacy, and audit

The MVP is local-first and uses the signed-in Windows account as its only operator identity. No subject name/date of birth appears in discovery, Android packages, routine logs, or pseudonymized handoff. Control and transfer are authenticated and encrypted. Pairing, overrides, capture transitions, recovery, verification, export, backup, cleanup, and deletion are audited.

India/CDSCO is the primary regulatory jurisdiction. Final device classification, licensing, legal interpretation, or certification claims require qualified Indian regulatory review.

## 17. Retention, backup, and handoff

No automatic coordinator deletion occurs in MVP. Session deletion uses recoverable trash before deliberate permanent deletion. Backups are consistent and verified. Exports distinguish identified from default pseudonymized form.

HumCapture ends at a verified session package, quality report, common software timeline, and versioned handoff manifest. Direct HumTrack importer implementation is outside scope.

## 18. Platforms and compatibility

Primary coordinator target is a currently supported Windows 11 x64 release. Windows 10 22H2 x64 is the minimum technical compatibility floor with lifecycle warning and named-hardware qualification. Android floor and device list remain Phase 0 decisions.

## 19. Validation and delivery sequence

Governance precedes features. Capability probes precede permanent hardware assumptions. Contracts and simulator precede multi-component implementation. Single Android and single UVC acquisition precede discovery/control/transfer and multi-source synchronization. Release gates require named hardware, soak, failure, recovery, security, field, traceability, and applicable regulatory evidence.

## 20. Open decisions

See `docs/project/KNOWN_ISSUES.md`. No implementation may silently resolve evidence-dependent timing, device, UVC, hotspot, binary-format, compatibility, or CDSCO-classification questions.

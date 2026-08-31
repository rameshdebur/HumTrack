# HumCapture Project State

**Status:** Initial HumCapture Git baseline committed; Phase 0 P0.2 recovery probe remains incomplete and controlled release remains blocked  
**Tags:** GOV.1N | SCM | INITIAL-BASELINE | TRACEABILITY | P0.2J | EVIDENCE | SBOM  
**Last meaningful update:** 2026-08-31

## Objective

Build a local-first, trained-operator acquisition subsystem for HumTrack that captures scientific masters from Android Camera2 and Windows UVC sources, records timing/camera/IMU evidence, supports recovery and verification, and produces a versioned downstream handoff package.

## Boundary

HumCapture is bounded to `src/HumCapture`. It may inspect HumTrack conventions but may not modify or depend on existing HumTrack internals without explicit user permission. Direct HumTrack importer work is not authorized.

## Current stage

- ARD discussion: agreed.
- PRD discussion: agreed.
- MVP user stories: agreed.
- Roles and ownership: created.
- Scoped `AGENTS.md`: created.
- Governance documentation: created and coherence-audited; no application code created.
- Phase 0 hardware-capability probe specification and implementation plan: accepted.
- P0.1 exploratory evidence-contract implementation: accepted after six independent review passes across three authorized cycles; 55 automated tests, both validation CLIs, syntax checks, and dependency audit pass. The gate covers only the source evidence contract.
- P0.2A Windows camera API spike: source, builds, five self-tests, and named-device API inspection pass on the integrated HP camera and two Logitech C920 cameras. Managed/native surfaces agree on all HP formats; native Media Foundation exposes 119 additional H.264 signatures on each C920.
- ADR-0006 is accepted. The isolated P0.2B native Media Foundation diagnostic builds with zero warnings/errors, passes eight selection self-tests, rejects unavailable 1080p60 and ambiguous profiles without output, preserves presentation/QPC timing evidence, and finalizes readable H.264 MP4 files.
- Hardware probe execution: C920 B passed the short exact 1080p30 H.264 diagnostic at measured 29.92 fps. C920 A finalized readable media but delivered approximately 25.98 fps and then 24.00 fps in two runs; P0.2B acceptance is therefore blocked pending focused camera/control/profile/topology diagnosis. This is diagnostic evidence, not qualification.
- Application source implementation: not started and not yet authorized.

## Architecture summary

- Android capture node: Kotlin, Camera2, MediaCodec/MediaMuxer, IMU, local master, RTP preview, control and resumable transfer services.
- Windows coordinator: .NET/Avalonia, headless coordinator host, SQLite operational catalog, subject/session/trial repository, transfer/verification/quality services.
- UVC capture: coordinator-local isolated workers, initially through the Windows camera stack.
- Capture hierarchy: Subject → Session → Trial → Source recording.
- Supported MVP source modes: one Android, one UVC, two Android, and mixed Android/UVC.
- Discovery: Android advertises through mDNS; coordinator browses and initiates authenticated control. Manual endpoint/QR fallback.
- Scientific masters remain local until finalization; preview is disposable.
- Session completion requires all protocol-required trials and source packages to be verified and committed.

## Important contracts to create before feature implementation

- Control/state-machine AsyncAPI and message schemas.
- Transfer OpenAPI and resumable range behavior.
- Manifest, completion-receipt, quality, and handoff JSON schemas.
- Timing and IMU binary formats with test vectors.
- Repository transaction and compatibility rules.

## Regulatory and policy position

- Primary jurisdiction: India/CDSCO.
- Engineering baseline is standards-aligned, but no medical-device, certification, clinical-accuracy, or CDSCO approval claim is authorized.
- Final classification and legal interpretation require a qualified Indian regulatory professional.

## Known limitations and open evidence

- Minimum Android version and qualified devices are not yet measured.
- 1080p120 is optional and unqualified.
- Software timing targets are provisional and not hardware-validated.
- UVC timestamp/control behavior and multi-camera USB limits require probes.
- The HP camera reports at most 1280×720 at 30 fps; both Logitech C920 cameras report native H.264 up to 1920×1080 at 30 fps. None can qualify the normative 1080p60 UVC procedure.
- One C920 did not sustain the requested 30 fps in two short runs, and the MP4 nominal rate contradicted measured cadence. Negotiated and container rates must not substitute for measured source-timestamp evidence.
- The documented C920 activity indicators were not observed during P0.2B and
  were not part of its evidence. HC-P0-UVC-003 has now passed on both named C920
  configurations with two repeatable physical off/on/off observations each,
  correlated with exact Windows identity, different physical units, fresh
  frames, and source release. It is explicitly not LED-based timing
  synchronization, and cameras without an indicator remain `NOT_APPLICABLE`.
- During P0.2C, C920 A measured approximately 24 fps and then 30 fps in two
  nominally identical 30-second runs. This confirms cadence variability and
  does not unblock P0.2B. C920 B measured approximately 30 fps twice.
- P0.2D opened and finalized both C920s concurrently, but full decode found
  repeatable H.264 corruption in C920 A while C920 B remained clean. P0.2E
  started B first and A second; corruption still followed A, ruling against the
  first-started stream as the supported cause. Concurrent use remains blocked
  pending physical port-swap/topology diagnosis.
- P0.2F swapped the two physical camera connections. In two repeats, corruption
  moved to physical C920 B on downstream USB path 2 while C920 A became clean on
  path 3. The problem follows the connection path, not either camera body/fixed
  cable. Both paths share the observed `PCI(1400)#USBROOT(0)#USB(1)` root; an
  alternate port/controller test was required.
- P0.2G moved Camera B to direct root-port branch `USBROOT(0)#USB(3)` while both
  cameras remained on controller `PCI(1400)`. The prior H.264 corruption was not
  reproduced, but Camera B delivered approximately 15 fps both concurrently and
  alone under automatic exposure; a well-lit retry improved but did not pass.
- P0.2H temporarily held Camera B exposure at `-5`. Camera B passed alone, then
  both C920s passed two concurrent 30-second runs at approximately 30 fps with
  complete error-free decode and no timestamp regressions. Camera B was restored
  to automatic exposure and independently confirmed. This is a conditional
  short-run result, not qualification.
- The reusable failure-isolation and reconciliation sequence from P0.2B–H is
  recorded in `docs/project/UVC_TROUBLESHOOTING_LEARNINGS.md`.
- P0.2I passed a proportionate 10-minute concurrent stability run on the exact
  P0.2H configuration. Each camera produced and fully decoded 18,002 frames at
  approximately 30.0002 fps with zero decode errors and timestamp regressions.
  Camera B automatic exposure was restored and independently confirmed. This is
  not the normative 30-minute 1080p60 qualification soak.
- P0.2J is in progress. A live disconnect of exact Camera A produced raw HRESULT
  `0xC00D3EA2`; 265/265 received frames finalized and fully decoded, the ordinary
  verifier rejected false completion, and Windows confirmed the exact interface
  absent. Same-identity reconnect and a separate post-reconnect capture remain open.
- ADR-0007 is accepted. A local, non-temporary, non-cloud evidence vault now
  uses versioned release/receipt schemas, conflict-safe import, SHA-256 inventories,
  and verification. Twelve automated control tests pass. The surviving P0.2J run is
  retained as `HC-EV-5becff40f4271131c8b43641` and verifies. This is controlled
  engineering evidence, not a validated eQMS or regulatory dossier.
- HumCapture is tracked on the isolated `codex/humcapture-baseline` review
  branch from initial engineering baseline commit
  `fc3dbd02aa6b73e7ca42bb084229c627b859d006`, based directly on
  `origin/master` at `67c47f4ba5c29c46c3bdbcfc72133a0492a8ac12`. Earlier snapshot
  `HC-ENG-20260831T163246Z-7d17f5afce36` remains explicitly engineering-only;
  the new source baseline does not retroactively make it a controlled release
  or permit regulatory use.
- ADR-0008 and SBOM policy HC-GOV-SBOM-001 are accepted. The deterministic
  CycloneDX 1.7 generator covers current Node.js, .NET and native Windows probe
  manifests plus framework/build dependencies; four project tests, including
  fail-closed future-manifest discovery, pass. The
  15-component/16-node SBOM passed official CycloneDX CLI 0.33.1 validation and
  is hash-bound into the engineering snapshot and controlled P0.2J evidence.
- HC-SOP-SW-004 is active for engineering work. It establishes tool-neutral,
  risk-based change classification, material AI-assistance declaration,
  human accountability, verification/review, configuration, and release
  controls. It rejects generic coverage/tolerance/CVE gates and vendor-specific
  tooling as regulatory mandates. It is a governance control, not evidence of
  IEC 62304, ISO 14971, CDSCO, QMS, or certification conformity.
- HC-GOV-003 / HC-CHG-20260831-001 accepted the first bounded Git baseline at
  `fc3dbd02aa6b73e7ca42bb084229c627b859d006`. Its 142 staged files were all
  under `src/HumCapture`; unrelated HumTrack changes, generated build/runtime
  output and raw evidence were excluded. The baseline remains engineering-only.
- Laptop-hotspot discovery, timing, and throughput require named-hardware validation.
- Exact binary formats, schema compatibility window, and offline USB completion-receipt mechanism remain open.
- Evidence backup/restore, retention approval, independent signing/review,
  access audit, and any required WORM/eQMS integration remain open.
- SBOM binary/runtime completeness, vulnerability/VEX, licence approval and
  supplier/maintainer review remain open.
- The software lifecycle/AI-assistance procedure has now been used for one
  tracked change and attributed review record. Remote branch protection,
  independent approval, CI evidence and a controlled release remain open.
- Detailed UI design is intentionally deferred.

## Immediate next priorities

1. Review and formally baseline the consolidated ARD, PRD, user stories, preliminary requirements, risks, and compliance applicability.
2. Obtain qualified India/CDSCO applicability and classification review before medically positioned claims or release.
3. Configure the approved remote protected-branch/review workflow and CI without
   disturbing unrelated HumTrack work. Define and verify evidence backup/restore
   and independent review before dossier use.
4. Preserve the reconciled exact topology/profile as conditional and test
   exposure policy across representative lighting, restart, and reconnect.
5. Run the disconnect/reconnect campaign; compare a genuinely
   different host controller if available. Do not reinstate the rejected path-2
   combination without fresh evidence.
6. Complete P0.2 evidence-package integration. Obtain 1080p60 UVC hardware for normative qualification before concurrent-camera qualification.
7. Complete the normative interface specifications required by the first application implementation phase.
8. Do not begin production application features until the governance gate explicitly permits the named phase.

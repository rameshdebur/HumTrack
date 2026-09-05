# HumCapture Project State

**Status:** I0.1A-I and I0.2A are accepted; session/protocol and transport-neutral source control contracts pass source-level conformance  
**Tags:** I0.2A | CONTROL | SOURCE-MESSAGES | WIRE-CONTRACT | MONOTONIC-TIME | RECONCILIATION | CUSTODY | QUALITY  
**Last meaningful update:** 2026-09-05

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
- P0.2K evidence integration: completed. Nine pre-integration P0.2A-J report files are hashed;
  both retained P0.2J runs verify in the controlled vault. Earlier P0.2 primary
  artifacts and complete normative P0.2J measurement/lifecycle records are not
  available and will not be reconstructed. P0.2 qualification remains open.
- I0.1/I0.2A coordinator control/state contract: decisions A-I and the
  source-message wire baseline were explicitly accepted and are consolidated
  in `HC-IF-CTRL-001` version `1.1.0`.
  ADR-0009 separates workflow, source-attempt, package-custody, and health
  authorities; ADR-0010 fixes immutable session protocol snapshots. The
  executable control slice contains 18 JSON Schema 2020-12 files, an AsyncAPI
  3.1.0 logical interface, valid/invalid fixtures, and 19 named conformance
  tests. All 31 tests in the existing evidence-control harness pass, including
  rejection of 553 session, 2,799 source-command, 241 source-event, and 299
  package-custody forbidden combinations. ADR-0011 fixes exact decimal-string
  uint64 monotonic ticks, clock/frequency/model identity, uncertainty, and RFC
  8785/SHA-256 content identity with tamper detection.
  Published AsyncAPI CLI 6.0.2 also accepts the document and referenced schemas
  in a one-off local check; it declares Node 24 and is therefore not added to the
  Node 22.12 CI dependency baseline.
  Transport/authentication binding, transfer endpoints, media/package
  manifests, timing/IMU binary streams, receipt signing, repository transaction
  implementation, and all application implementation remain open.

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

- Independent review of the control/state-machine AsyncAPI and message schemas.
- Transfer OpenAPI and resumable range behavior.
- Capture/package manifest and receipt-signing/offline-conveyance rules.
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
- P0.2J technical execution passed. A live disconnect of exact Camera A produced raw HRESULT
  `0xC00D3EA2`; 265/265 received frames finalized and fully decoded, the ordinary
  verifier rejected false completion, and Windows confirmed the exact interface
  absent. On reconnect, parent serial `0E1A0C0F` and exact interface
  `6&DBA5B52&2&0000` returned. Separate run
  `2D358446-A581-4DDA-92FA-92B5F71606A9` reached 30 seconds, finalized and fully
  decoded 898/898 frames at 29.9000 measured fps with zero decode errors and
  timestamp regressions. Independent review and production recovery remain open.
- ADR-0007 is accepted. A local, non-temporary, non-cloud evidence vault now
  uses versioned release/receipt schemas, conflict-safe import, SHA-256 inventories,
  and verification. Twelve automated control tests pass. The interrupted P0.2J
  run is retained as `HC-EV-5becff40f4271131c8b43641`; the separate successful
  post-reconnect run is `HC-EV-16e6576bbcf4f1896d64aac6`. Both verify. This is controlled
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
- HC-GOV-004 / HC-CHG-20260831-002 published the isolated branch
  `codex/humcapture-baseline` and opened
  `https://github.com/rameshdebur/HumTrack/pull/1` against `master`. The PR is
  open, non-draft and mergeable. GitHub currently reports no workflows, status
  checks, `master` protection or repository rulesets; independent review is
  also open. No repo-wide setting or path outside `src/HumCapture` was changed.
- HC-GOV-005 passed its engineering governance gate under explicit authorization for the root
  `.github/workflows/humcapture-ci.yml` path and `master` protection. The scoped
  check is named `HumCapture validation`; it covers existing automated tests,
  SBOM/hash/audit controls and managed/native non-hardware builds/self-tests.
  It does not perform camera acquisition and cannot supply HIL evidence.
- GitHub PR run `33421222644` passed on commit
  `8ff67d8b6104b729428f6866de75660f388b09c5`. `master` now requires the strict
  `HumCapture validation` check and pull-request flow, applies protection to
  administrators, requires conversation resolution and linear history, and
  blocks force pushes/deletion. Required approvals remain zero for the
  solo-maintainer engineering flow; independent human review remains an open
  controlled-release blocker.
- The current SBOM now includes the CI workflow, three commit-pinned GitHub
  Actions and the exact Windows SDK Chocolatey build package. It contains 20
  components/21 dependency nodes; six project tests and project validation
  pass. The prior 15-component SBOM remains immutable
  in the existing snapshot/evidence vault; the CI-aware SBOM is not
  retroactively release-bound.
- Laptop-hotspot discovery, timing, and throughput require named-hardware validation.
- Exact timing/IMU binary formats, transport/security binding, schema
  compatibility window, receipt signing, and offline USB receipt-conveyance
  mechanism remain open.
- Evidence backup/restore, retention approval, independent signing/review,
  access audit, and any required WORM/eQMS integration remain open.
- P0.2K confirms that P0.2A-I primary artifacts are unavailable. Only the two
  draft-review P0.2J runs remain independently machine-verifiable, and they do
  not form a complete normative 1080p60 evidence package.
- SBOM binary/runtime completeness, vulnerability/VEX, licence approval and
  supplier/maintainer review remain open.
- The software lifecycle/AI-assistance procedure has now been used for tracked
  changes and attributed review records. CI evidence and remote branch
  protection are verified; independent approval and a controlled release remain open.
- Detailed UI design is intentionally deferred.

## Immediate next priorities

1. Review and formally baseline the consolidated ARD, PRD, user stories, preliminary requirements, risks, and compliance applicability.
2. Obtain qualified India/CDSCO applicability and classification review before medically positioned claims or release.
3. Review PR #1 and obtain independent human approval before controlled release.
   Define and verify evidence backup/restore before dossier use.
4. Preserve the reconciled exact topology/profile as conditional and test
   exposure policy across representative lighting, restart, and reconnect.
5. Obtain independent review of the completed P0.2J bounded diagnostic. Compare
   a genuinely different host controller if available, and do not reinstate the
   rejected path-2 combination without fresh evidence.
6. Obtain 1080p60 UVC hardware and capture a complete prospective evidence
   package before normative or concurrent-camera qualification.
7. Independently review HC-IF-CTRL-001 version 1.1.0 and its verified
   session/source wire contracts; then baseline the transfer/resume boundary
   without beginning application features.
8. Do not begin production application features until the governance gate explicitly permits the named phase.

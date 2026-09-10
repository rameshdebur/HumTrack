# HumCapture Project State

**Status:** I0.1A-I, I0.2A, I0.3A-C, I0.4A and I0.4B-A/B1/B2/B3A are accepted; repository transaction states are locked  
**Tags:** I0.4B-B3A | REPOSITORY | JOURNAL | STATE-MACHINE | CRASH | RECOVERY | CONTRACT  
**Last meaningful update:** 2026-09-10

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
  source-message wire baseline were explicitly accepted in version `1.1.0`;
  I0.3B added the compatible WSS security binding in `HC-IF-CTRL-001` version
  `1.2.0`, and I0.3C advances it additively to `1.3.0`.
  ADR-0009 separates workflow, source-attempt, package-custody, and health
  authorities; ADR-0010 fixes immutable session protocol snapshots. The
  executable control slice now contains 23 JSON Schema 2020-12 files, an AsyncAPI
  3.1.0 logical interface, valid/invalid fixtures, and 29 named control/receipt
  conformance tests. The current evidence-control harness passes, including
  rejection of 553 session, 2,799 source-command, 241 source-event, and 299
  package-custody forbidden combinations. ADR-0011 fixes exact decimal-string
  uint64 monotonic ticks, clock/frequency/model identity, uncertainty, and RFC
  8785/SHA-256 content identity with tamper detection.
  Published AsyncAPI CLI 6.0.2 also accepts the document and referenced schemas
  in a one-off local check; it declares Node 24 and is therefore not added to the
  Node 22.12 CI dependency baseline.
  Runtime transport/authentication, timing/IMU binary streams, receipt signing,
  repository transaction implementation, and all application implementation
  remain open.
- I0.3A was accepted as `HC-IF-XFR-001` version `1.0.0`; I0.3B adds compatible
  version `1.1.0` for the security binding. ADR-0012 records
  coordinator pull, immutable directory publication, HTTPS ranges/strong
  validators, USB/MTP artifact-boundary recovery, staging, common verification,
  idempotency/quarantine, and commit/receipt linkage. Three JSON Schemas,
  OpenAPI 3.1.2, and HC-XFR-TEST-001–011 pass locally. Receipt signing,
  repository implementation, runtime, HIL, field, and independent review
  remain open.
- I0.3B is accepted as `HC-IF-SEC-001` version `1.0.0`. ADR-0013 records
  attended exact-fingerprint QR/high-entropy manual enrollment, Android
  Keystore and Windows DPAPI CurrentUser custody, mutual TLS, active
  trust/role/resource authorization, TLS 1.3 default, restricted logged Windows
  10 TLS 1.2 compatibility, replay/lockout, rotation/revocation/re-enrollment,
  and redacted audit. Control AsyncAPI is now version 1.3.0 and transfer OpenAPI is
  version 1.1.0. Four security schemas and HC-SEC-TEST-001–012 pass locally.
  Runtime credential/TLS implementation, hostile-network/penetration, HIL,
  field, independent and qualified regulatory review remain open.
  Verification report HC-VR-I0-3B-001 records 54/54 contract/evidence tests,
  55/55 capability regressions, 6/6 SBOM policy tests, both API validators,
  JSON parsing, diff checks, and explicit non-runtime limitations.
  Implementation commit `2af6761beed6e0aaaba5f0348ba3d991678b4b37` is
  remotely verified by successful HumCapture CI runs `34037481492` and
  `34037479975`.
- I0.3C is accepted as `HC-IF-RCP-001` version `1.0.0`; ADR-0014 keeps
  durable coordinator commit as acquisition completion authority and makes
  receipt acknowledgement a cleanup gate only. HC-IF-CTRL-001 version 1.3.0
  adds durable receipt acknowledgement, status query/reconciliation, explicit
  operator cleanup and truthful per-package result messages. One refined
  receipt plus five new JSON schemas and HC-RCP-TEST-001–010 cover immutable
  replay/conflict, lost acknowledgement, active-operation exclusion, manual
  USB/MTP fallback, partial remainder-only retry and record minimization.
  Local verification passes 64/64 evidence-control tests, 55/55 capability
  regressions, 6/6 SBOM policy tests, current SBOM validation, both production
  dependency audits, AsyncAPI CLI validation, JSON parsing, and diff checks.
  Implementation commit `1744443045295e741cf8db884da9006d337c7176` is
  remotely verified by successful HumCapture CI push run `34093701024` and PR
  run `34093704064`, including managed and native camera build/self-tests.
  Android/coordinator persistence/UI/deletion, restart/power, HIL, field,
  independent review and qualified regulatory review remain open.
- I0.4A is accepted as `HC-IF-TIM-001` version `1.0.0`; ADR-0015 records
  separate fixed-record little-endian frame/IMU scientific masters, native
  timestamp authority, explicit presence/provenance, CRC32C plus package
  SHA-256, segmented affine clock mappings, independent sensor sequences,
  measured cadence, frame/video association, camera/lens/orientation metadata,
  camera–IMU transforms and protocol-scoped quality gates. Fixed, variable and
  adaptive camera rate classes are supported; lack of adaptive-rate control is
  not a universal failure. Three JSON schemas, nine immutable binary vectors
  (six accepted and three rejected), decoded references and HC-TIM-TEST-001–012
  pass in the 76-test evidence-control suite.
  HC-IF-XFR-001 advances additively to version `1.2.0` for explicit timing
  profile/format declarations. Production Android/UVC writers, Coordinator
  ingestion, runtime/HIL/field and independent/regulatory review remain open.
  Implementation commit `ffdd9557d61b8d9aebc792cf217cf58f670fd0a4`
  is remotely verified by successful HumCapture CI push run `34324594435` and
  PR run `34324599337`, including managed and native camera build/self-tests.
- I0.4B-A is accepted in ADR-0016. HumCapture uses a Coordinator-owned data
  root outside the source checkout, a SQLite operational catalog, immutable
  filesystem packages, UUID-authoritative folder identity, same-volume staging,
  and a recoverable journal across filesystem/catalog/verification/custody
  state. Only reconciled `COMMITTED` state permits a receipt. I0.4B-B must still
  define exact paths, schemas, compatibility behavior and executable
  crash/restart tests; no repository application code is authorized by this
  decision. Decision commit `cd0214ae6e0950882ca714390899c5c9ca8089b5`
  passed HumCapture CI push run `34364850625` and PR run `34364851530`,
  including existing contracts, SBOM/audits, and managed/native camera
  build/self-tests.
- I0.4B-B1 is accepted in ADR-0017 and HC-IF-REP-001 version 1.0.0. The
  repository uses root descriptor/catalog/staging/quarantine/subjects areas and
  commits packages at
  `subjects/{subject_id}/sessions/{session_id}/packages/{package_id}/` using
  canonical lowercase UUIDs. Trial/source/attempt identities remain in the
  manifest/catalog to limit Windows path depth. Package contents remain exactly
  as verified; display/PII values never become path authority. B3 executable
  schemas and recovery tests remain open. Decision commit
  `6021c50b18257a3b2f778c1b94c0b2b56d498f77` passed HumCapture CI push run
  `34444103000` and PR run `34444106785`, including existing contracts,
  SBOM/audits, and managed/native camera build/self-tests.
- I0.4B-B2 is accepted in ADR-0018 and advances HC-IF-REP-001 additively to
  version 1.1.0. SQLite is the sole mutable workflow/subject-PII authority;
  verified packages and immutable protocol-snapshot, verification, commit,
  receipt, quality, completion and handoff records are filesystem evidence
  indexed by UUID/revision/hash/path. Disagreement enters recovery and blocks
  receipt/completion. The minimal `repository.json` version/feature descriptor
  contains no subject data or secrets; unsupported major/required features
  refuse mutation and opening never silently migrates history. B3 executable
  schemas and fault tests remain open. Decision commit
  `d9f656b05ed9ff4d88baf670293aa364d0281065` passed HumCapture CI push run
  `34445863047` and PR run `34445866678`, including contracts/controls, SBOM,
  dependency audit, and managed/native camera build/self-tests.
- I0.4B-B3A is accepted in ADR-0019 and advances HC-IF-REP-001 additively to
  version 1.2.0. Internal repository transactions use `STAGED_VERIFIED`,
  `COMMITTING`, `MOVED`, `CATALOGED`, and `COMMITTED`, with explicit
  `RECOVERY_REQUIRED` and `QUARANTINED` dispositions. Only evidence-reconciled
  `COMMITTED` permits receipt/completion; these durability states map to the
  existing custody contract and do not add routine operator workflow steps.
  B3B exact journal/reconciliation fields and B3C executable schemas/fault
  fixtures remain open. Decision commit
  `7db93a5a404f54bc7818f258f8187a8dc04cd43b` passed HumCapture CI push run
  `34474035867` and PR run `34474039424`, including contracts/controls, SBOM,
  dependency audit, and managed/native camera build/self-tests.

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

- Independent review of the control/state-machine, transfer, and security
  schemas/bindings.
- Receipt signing and any future trusted offline acknowledgement mechanism.
- Independent review and production implementation of the accepted timing/IMU contract.
- I0.4B-B3B/B3C exact journal/reconciliation fields, automatic/operator action
  policy, executable repository schemas and fault fixtures.

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
- Production timing/IMU codecs, broader future-version compatibility window,
  receipt signing, and any future trusted offline USB receipt-conveyance
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
7. Independently review HC-IF-CTRL-001 version 1.3.0, HC-IF-XFR-001 version
   1.2.0, HC-IF-SEC-001 version 1.0.0, HC-IF-RCP-001 version 1.0.0 and
   HC-IF-TIM-001 version 1.0.0 without beginning application features.
8. Do not begin production application features until the governance gate explicitly permits the named phase.

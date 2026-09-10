# HumCapture MVP User Stories

**Document ID:** HC-US-BASELINE-001  
**Version:** 0.1  
**Status:** Accepted story baseline; detailed UI stories deferred  
**Primary actor:** Trained operator using the signed-in Windows account

## Subject

- **HC-US-SUB-001:** Find an existing subject by code/name and distinguish likely duplicates.
- **HC-US-SUB-002:** Create a subject with code, name, date of birth, sex field, and height; generate immutable UUID; keep PII coordinator-local.
- **HC-US-SUB-003:** Warn on similar name/date of birth; never merge automatically; require reason to continue.
- **HC-US-SUB-004:** Correct identity/demographics without changing UUID/folder/history; audit changes.
- **HC-US-SUB-005:** Review previous sessions by protocol, status, trial count, and quality.
- **HC-US-SUB-006:** Block capture against an unsaved or failed subject record.

## Protocol

- **HC-US-PRO-001:** View approved/current/retired protocols and their requirements.
- **HC-US-PRO-002:** Create an editable draft with immutable protocol identity.
- **HC-US-PRO-003:** Define validated fixed or flexible source count.
- **HC-US-PRO-004:** Define required/optional trial types, repetitions, instructions, and duration.
- **HC-US-PRO-005:** Validate source/profile/timing/calibration/field/trial/override/completion rules before approval.
- **HC-US-PRO-006:** Explicitly approve and freeze a version under the Windows account.
- **HC-US-PRO-007:** Revise through a new version without changing history.
- **HC-US-PRO-008:** Clone with new identity and provenance.
- **HC-US-PRO-009:** Retire without deleting referenced history.
- **HC-US-PRO-010:** Preserve requirements, policy, standards, and compatibility context.

## Session and trial

- **HC-US-SES-001:** Create subject/protocol session with UUID, operator, and immutable protocol snapshot.
- **HC-US-SES-002:** Complete protocol-required session metadata.
- **HC-US-SES-003:** Apply fixed source count without operator change.
- **HC-US-SES-004:** Select an allowed flexible count and freeze it before arming.
- **HC-US-SES-005:** Review required/optional trial plan and progress.
- **HC-US-SES-006:** Add traceable trials only when the protocol permits.
- **HC-US-SES-007:** Resume draft/open/recovery session and reconcile current state.
- **HC-US-SES-008:** Change protocol only before any trial is armed/recorded.
- **HC-US-SES-009:** Abort unused session with reason rather than recording failure.
- **HC-US-SES-010:** Reuse setup between trials but rerun volatile readiness.
- **HC-US-SES-011:** Complete, recover, or close incomplete with accurate outcome.

## Devices

- **HC-US-DEV-001–004:** Discover, pair, reconnect, and manually connect Android without bypassing authentication.
- **HC-US-DEV-005:** Reject coordinator takeover during active work.
- **HC-US-DEV-006:** Assign friendly names without changing authoritative identity.
- **HC-US-DEV-007–008:** Enumerate/disambiguate UVC devices and record actual identity limits.
- **HC-US-DEV-009:** Detect incompatible software/schema before arming.
- **HC-US-DEV-010–011:** Assign source role/viewpoint and distinguish required/supplementary completion.
- **HC-US-DEV-012:** Unpair only when critical operations are inactive; preserve packages.
- **HC-US-DEV-013:** As a trained operator, I can verify a documented physical
  camera-use indicator against the exact selected device before approval, and I
  receive an actionable reconciliation or rejection result when it disagrees
  with actual camera use.
- **HC-US-DEV-014:** When my selected protocol requires multiple UVC cameras, I
  can preflight the exact cameras, drivers, USB topology, and profiles together,
  and HumCapture rejects or reconciles the combination if any stream is corrupt
  even when all cameras appear connected and frame counts look complete.

## Readiness

- **HC-US-RDY-001–002:** Apply requested/actual source configuration and camera/lens identity without silent substitution.
- **HC-US-RDY-003–004:** Inspect fresh identified previews and protocol positioning/orientation guidance.
- **HC-US-RDY-005:** Produce one explainable preflight across subject, protocol, sources, timing, storage, thermal, network, coordinator, and repository.
- **HC-US-RDY-006:** Block missing/incompatible source, recorder failure, insufficient storage, invalid identity, or unavailable repository.
- **HC-US-RDY-007:** Permit only protocol-defined overrides with reason and retained evidence.
- **HC-US-RDY-008–009:** Show storage sufficiency and synchronization evidence/thresholds.
- **HC-US-RDY-010:** Confirm complete arming summary.
- **HC-US-RDY-011:** Invalidate readiness when important conditions change.

## Active capture

- **HC-US-CAP-001–004:** Arm all required sources, schedule future start, show countdown, and report actual first timestamps/late failure.
- **HC-US-CAP-005:** Monitor master independently of preview.
- **HC-US-CAP-006–008:** Preserve Android master through network/preview failure and preserve survivors after source failure.
- **HC-US-CAP-009–011:** Normal stop, intentional emergency stop, and maximum-duration fallback with provenance.
- **HC-US-CAP-012:** No pause; interruption becomes finalized attempt and retake.
- **HC-US-CAP-013:** UI lifecycle does not own scientific capture.
- **HC-US-CAP-014:** Allow next trial after safe finalization while prior transfers remain pending and capacity passes.
- **HC-US-CAP-015:** Preserve every camera's native per-frame timing, source/video association and visible loss without constructing frames from nominal FPS.
- **HC-US-CAP-016:** Preserve available Android raw accelerometer/gyroscope and optional derived IMU streams with independent timestamps, sequences, units and cadence evidence.
- **HC-US-CAP-017:** Preserve reported camera/lens/orientation data and explicit unavailable reasons without guessed specifications.
- **HC-US-CAP-018:** Associate a camera and IMU only through explicit clocks/transforms/validity so downstream horizon and motion analytics can retain uncertainty.
- **HC-US-CAP-019:** Use fixed-, variable- or adaptive-rate cameras when they meet the selected protocol; absence of adaptive control alone is not failure.

## Transfer and recovery

- **HC-US-XFR-001–002:** Finalize immutable Android/UVC packages with master, sidecars, manifest, and hashes.
- **HC-US-XFR-003–005:** Queue, pause, resume, restart, and prioritize acquisition over transfer.
- **HC-US-XFR-006–007:** Verify schema/identity/files/length/hash/structure and transactionally commit to the correct location.
- **HC-US-XFR-008:** USB/MTP recovery uses the identical common verifier without ADB.
- **HC-US-XFR-009–010:** Identical duplicate is idempotent; conflicting package is quarantined without overwrite.
- **HC-US-XFR-011–012:** Reconcile restart and retry/quarantine hash failure while preserving phone original.
- **HC-US-XFR-013:** Send replayable completion receipt only after durable commit.
- **HC-US-XFR-014:** Network failure alone produces recovery-required, not recording failure.
- **HC-US-XFR-015:** Treat durable coordinator commit as package/session completion even when receipt acknowledgement or cleanup is pending.
- **HC-US-XFR-016:** Match and durably acknowledge one exact receipt on Android without deleting the package.
- **HC-US-XFR-017:** Reconcile a lost acknowledgement and replay the identical receipt without recommit or receipt regeneration.
- **HC-US-XFR-018:** Show the trained operator only cleanup-eligible packages with device, count, size, and capture/commit dates before one explicit confirmation.
- **HC-US-XFR-019:** Report deleted, partial, missing, and rejected packages truthfully and retry only reconciled remaining files.
- **HC-US-XFR-020:** Offer an informed manual cleanup list after USB/MTP completion without claiming automatic Android deletion authority.
- **HC-US-XFR-021:** Commit a verified package through a recoverable journal so
  a restart or power interruption cannot create false completion or a premature
  cleanup receipt.
- **HC-US-XFR-022:** Reconcile filesystem, catalog, verification, custody, and
  journal state at startup and present unresolved disagreement as recovery
  rather than silently repairing or overwriting scientific data.
- **HC-US-XFR-023:** Keep required subject name/demographics available in the
  Coordinator while using immutable UUIDs and hashes for repository paths,
  packages, receipts, logs, and pseudonymized handoff.
- **HC-US-XFR-024:** Locate a committed package under its stable subject and
  session UUIDs while retaining the package exactly as verified and preventing
  display-name changes, unsafe paths, or conflicts from renaming or
  overwriting it.
- **HC-US-XFR-025:** Recover and verify immutable session milestones from
  individually hashed records without treating them as a second mutable
  Coordinator database.
- **HC-US-XFR-026:** Detect disagreement between a milestone file and its
  catalog index, block affected completion/cleanup, and present a specific
  recovery state without silently choosing or recreating either record.
- **HC-US-XFR-027:** Inspect a newer or older repository safely without silent
  migration or modification when its required version/features are unsupported.
- **HC-US-XFR-028:** Resume after a crash at any repository durability boundary
  without guessing that a package is committed or losing the verified package.
- **HC-US-XFR-029:** See a simple **Saving**, **Needs attention**,
  **Quarantined**, or **Completed** outcome while diagnostic records retain the
  exact internal transaction state and evidence.
- **HC-US-XFR-030:** Permit receipt and cleanup only after repository
  reconciliation proves the package, catalog, journal, verification, and
  immutable commit evidence agree.

## Quality and completion

- **HC-US-QA-001–003:** Review versioned protocol conformance, warning intervals, and retained deviations.
- **HC-US-QA-004–006:** Retake, supersede, and exclude without overwrite/deletion.
- **HC-US-QA-007–009:** Show protocol progress, complete only satisfied sessions, and block false completion.
- **HC-US-QA-010:** Close incomplete with reason and without successful protocol claim.
- **HC-US-QA-011:** Reopen deliberately with audit and preserved history.
- **HC-US-QA-012:** Preserve assessment rules, inputs, version, and reassessment history.

## Cross-cutting MVP

- **HC-US-MVP-001:** Conduct qualified laptop-hotspot field capture with discovery/manual fallback and recovery.
- **HC-US-MVP-002:** Clear only verified mobile data through informed confirmation.
- **HC-US-MVP-003:** Export verified pseudonymized-by-default handoff without altering authority.
- **HC-US-MVP-004:** Verify/backup repository and use recoverable trash before permanent deletion.
- **HC-US-MVP-005:** Provide actionable local diagnostics and redacted support bundle.

## Acceptance principle

Detailed scenarios shall be converted into executable or manual verification cases before implementation. Event capture, source inspection, or a broad passing suite is insufficient when the story requires visible workflow, semantic data change, restart, hardware, field, or regulatory evidence.

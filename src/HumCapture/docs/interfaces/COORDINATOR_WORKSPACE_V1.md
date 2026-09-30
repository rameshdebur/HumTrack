# HC-IF-UIW-001 version 1.0.0

C16A baseline, 2026-09-30. ADR-0040. Synthetic workspace only.

## Approved screens and acceptance

- Session: synthetic subject code/name/DOB/controlled sex/height in cm, immutable
  UUID; create or explicitly resume session; existing versioned fixture protocol;
  show destination; validate before Cameras. No protocol authoring.
- Cameras: protocol-defined required slots; unique simulated source per slot;
  requested versus supported profile; readiness reason; fixed rate is supported.
  Unavailable optional IMU/lens information is not inferred or a global blocker.
- Capture: locked session/attempt identity; start/stop idempotence, elapsed
  simulated time, synthetic activity, preview health separate from capture.
  No pause. Source loss retains interrupted attempt, finalization failure retains
  evidence. Navigation/close while active requires an explicit safe choice.
- Results: finalization, verification, storage and protocol result distinct.
  Simulator finalization never becomes actual verification/commit. Unsupported
  real processing actions explain their unavailability. Existing repository
  evidence may be inspected separately and is labelled historical.
- History: bounded pages, resume immutable session, new attempt for retake.
  Reopening never manufactures success from a stale active state.

## Persistence and service

SQLite application_id 1212372819, user_version 1, synthetic-workspace.sqlite3.
Sessions store synthetic subject and immutable selected protocol. Attempts store
configuration, state, start/end UTC (display-only), preview condition and reason.
Events are append-only; current attempt update plus event append is transactional.
State names: Ready, Recording, Finalizing, Finalized, Interrupted, FinalizationFailed.
Finalized means synthetic finalization only. Verification/commit remain NotAssessed.
No scientific timestamps are produced.
An independent Processing field records NotRun, VerificationFailed, StorageFailed
or SimulatedOnly, with a processing-attempt count. The first injected processing
fault fails; an explicit retry advances the simulation exercise on the same
recording attempt and retains the failure event. SimulatedOnly does not create
media, verification, custody or completion records. A two-source loss scenario
keeps the surviving synthetic source active and ultimately retains an incomplete
attempt. Source/profile fixtures are not approved production protocols.

The service accepts list/reconcile/create-session/prepare/start/stop/finalize,
preview-loss/source-loss/simulate-processing. Request fields are Command,
SessionId, AttemptId, Subject, Protocol, Sources, Scenario, Page, SchemaVersion.
Session history uses 20-row pages; selected-session attempt history currently
shows the latest 20, selected-attempt event history the latest 50 (limits labelled
in the UI). Older rows remain retained; expanded browsing is later work.
inspect-repository is separate: root is an existing repository and AttemptId is
the journal pagination cursor; existing Open/ListStartupTransactions APIs inspect
20 historical rows without changing custody. No simulation workspace is opened
or initialized by this command. Read-only evidence is never fresh verification.

Service requests are strict JSON with version 1.0.0, command, identifiers and
command-specific fields. Root is an absolute local designated simulation folder.
Output is a versioned response containing workspace snapshot or a refusal.
One process holds the workspace lock for the entire command. No automatic
migration, repository creation, network listener or authentication system.

## Shared UI requirements

Keyboard reachable controls, visible focus, text states, scalable/scrollable
layout, one primary action per screen. Background calls show honest indeterminate
progress; failed refresh replaces old readiness with Status unavailable.
Synthetic identity and generated outcomes remain visibly marked throughout.

## Acceptance mapping

C16-01 identity/protocol binding; C16-02 required slots/unique sources;
C16-03 fixed rate and unsupported profiles; C16-04 duplicate start/stop;
C16-05 preview independent; C16-06 source/finalization failures retained;
C16-07 restart interruption; C16-08 new attempt immutable history;
C16-09 no false verified/committed/completed; C16-10 isolation/version refusal;
C16-11 real historical evidence distinct; C16-12 keyboard/layout smoke.

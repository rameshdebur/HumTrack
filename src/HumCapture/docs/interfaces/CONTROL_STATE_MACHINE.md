# HumCapture Coordinator Control and State Contract

**Document ID:** HC-IF-CTRL-001  
**Version:** 0.1-draft  
**Status:** Controlled draft of accepted I0.1A-H decisions; session/protocol lifecycle pending explicit approval  
**Date:** 2026-09-05

## 1. Scope

This contract defines coordinator control semantics shared by Android capture
nodes and coordinator-local UVC workers. It covers authority, trial and source
states, command acknowledgement/idempotency, coordinated start/stop, readiness,
package custody, quality, retakes, and trial completion.

It does not yet baseline the session/protocol execution lifecycle, wire-format
schemas, transport security details, transfer endpoints, timing/IMU binary
formats, repository layout, or application implementation.

## 2. Normative language and invariants

`SHALL`, `SHALL NOT`, `SHOULD`, and `MAY` express requirements in this draft.

- Scientific master acquisition and native timestamps outrank control, health,
  preview, UI, and post-capture transfer.
- The coordinator controls intent; the source performing acquisition reports
  acquisition facts; repository services report custody facts.
- UI state is never authoritative.
- Network or preview failure alone does not terminate an Android master.
- Commands request transitions and never declare their own success.
- Recording requires an accepted scientific-master sample.
- Completion requires every protocol-required package to be durably committed.
- Missing, unknown, partial, or conflicting evidence is never inferred as pass.

## 3. State authorities

| Domain | Authority | Examples |
|---|---|---|
| Session/trial workflow | Coordinator host | Draft, ready, recording, recovery required, complete, closed incomplete |
| Source capture attempt | Android capture service or UVC worker | Configuring, armed, recording, finalizing, finalized |
| Package custody | Transfer/verifier/repository services | Local, collecting, staged, verified, committed, quarantined |
| Connection and health | Observed component status | Online, disconnected, preview degraded, storage warning |

Conflicts are resolved by the authority for the affected domain. No component
may overwrite another domain's fact merely to make a composite workflow appear
successful.

## 4. Stable identities

Control records SHALL carry applicable stable UUIDs explicitly:

- session, trial, source, capture-attempt, package, command, message, start-plan,
  stop-plan, readiness-snapshot, assessment, commit, and receipt IDs;
- immutable protocol snapshot and contract versions; and
- coordinator/device identity and source boot epoch where applicable.

Identity SHALL NOT be inferred from the active UI screen, friendly name, socket,
or arrival order. A reconnect after interrupted capture creates a new attempt;
a retake creates a new trial.

## 5. Coordinator trial workflow

The accepted trial workflow is:

```text
DRAFT -> CONFIGURING -> PREFLIGHT -> READY -> ARMING -> ARMED
-> START_SCHEDULED -> RECORDING -> STOPPING -> FINALIZING
-> COLLECTING -> VERIFYING -> REVIEW_REQUIRED -> COMPLETE
```

| State | Entry fact |
|---|---|
| `DRAFT` | Trial identity exists and allowed assignments/metadata remain editable. |
| `CONFIGURING` | Exact source configurations are being applied. |
| `PREFLIGHT` | Named readiness checks are executing. |
| `READY` | Required checks passed or have valid protocol-authorized deviations. |
| `ARMING` | Arming was requested but all required sources have not confirmed it. |
| `ARMED` | Every required source independently confirmed recorder readiness. |
| `START_SCHEDULED` | Every required source accepted the same committed future start plan. |
| `RECORDING` | At least one source reported its first accepted master sample. |
| `STOPPING` | A normal, emergency, maximum-duration, or failure-driven stop is active. |
| `FINALIZING` | At least one required attempt is still closing artifacts. |
| `COLLECTING` | Required finalized packages have not all reached coordinator staging. |
| `VERIFYING` | Required packages are under verification/transactional commit. |
| `REVIEW_REQUIRED` | Package custody is resolved and quality/deviation review is required. |
| `COMPLETE` | Every trial completion predicate in section 13 is true. |

Exceptional workflow outcomes are:

- `CANCELLED`: terminal pre-capture cancellation with reason and no acquired
  scientific-master sample;
- `RECOVERY_REQUIRED`: non-terminal unresolved state requiring reconciliation,
  collection, verification, or operator recovery; and
- `CLOSED_INCOMPLETE`: deliberate terminal closure without a successful
  protocol-trial claim.

A generic trial-level `FAILED` SHALL NOT erase the specific source, custody,
health, or quality failure. A retake is a new trial rather than a reset of the
existing trial.

## 6. Source capture-attempt lifecycle

Android and UVC implementations share this logical model:

```text
CREATED -> CONFIGURING -> CONFIGURED -> CHECKING_READINESS -> READY
-> ARMING -> ARMED -> START_SCHEDULED -> STARTING -> RECORDING
-> STOPPING -> FINALIZING -> FINALIZED_COMPLETE
```

Exceptional terminal results are:

- `CANCELLED_BEFORE_CAPTURE`: no scientific-master sample was accepted;
- `FINALIZED_INCOMPLETE`: samples exist and a trustworthy partial package was
  finalized, but the attempt cannot be treated as normally complete; and
- `FINALIZATION_FAILED`: capture began but no structurally valid finalized
  source package could be produced.

Finalization SHALL record a stop reason such as normal operator stop, protocol
duration, maximum duration, coordinator/local emergency stop, device loss,
recorder error, storage exhaustion, power/thermal shutdown, process recovery,
or `UNKNOWN`. `UNKNOWN` creates a blocking deviation.

Control/preview disconnection is an orthogonal observation and SHALL NOT change
Android capture state. Physical UVC loss affects its attempt. Reconnecting never
continues the interrupted attempt.

Camera/lens/profile/control/orientation/IMU configuration is locked from
`ARMING`. A material configuration change after any master sample requires a new
attempt ID.

## 7. Commands, acknowledgements, and idempotency

Minimum commands are:

```text
CONFIGURE, CHECK_READINESS, ARM, DISARM, PREPARE_START, COMMIT_START,
CANCEL_START_PLAN, STOP_AT, EMERGENCY_STOP, RECONCILE_STATE, GET_STATE
```

An applicable command envelope contains contract/message/command identity,
coordinator/session/trial/source/attempt identity, command type, expected source
state, coordinator issue observation, command deadline or scheduled session
time, and command-specific payload.

Command handling produces one acknowledgement:

```text
ACCEPTED, REJECTED, ALREADY_APPLIED
```

Acknowledgement SHALL be followed by authoritative state/result events. A
coordinator timeout is `RESULT_UNKNOWN`, not failure. It requires reconciliation
or repetition of the same command ID.

- Same command ID and canonically identical payload returns the persisted
  acknowledgement/result without repeating the operation.
- Same command ID with different payload is `COMMAND_ID_CONFLICT` and audited.
- A new command ID requesting an invalid/redundant transition is rejected
  against actual state.
- Critical command intent/results persist through reconnect and coordinator/UI
  restart at least until session closure and resolved package custody.

Error responses contain stable code, safe summary, actual state, expected
states, related IDs, and retry class: `DO_NOT_RETRY`, `RETRY_SAME_COMMAND`,
`RECONCILE_FIRST`, or `OPERATOR_ACTION_REQUIRED`.

## 8. Events and restart reconciliation

Source events contain event ID/type, source boot ID, monotonically increasing
per-boot event sequence, source monotonic timestamp, prior/resulting state, and
related command ID. Critical transitions are persisted before announcement.

On reconnection or restart, the coordinator requests a snapshot containing
identity/version compatibility, boot ID, active IDs, actual capture state,
last event sequence, critical command results, actual configuration,
recorder/finalization state, finalized-package inventory, warnings, maximum
duration, and clock-continuity information.

Authority examples:

- source `FINALIZED_COMPLETE` outranks stale coordinator `RECORDING`;
- source `RECORDING` prevents duplicate start despite stale coordinator `ARMED`;
- repository absence outranks a source assertion that a package is committed;
- a committed repository package permits replay of the same lost receipt; and
- restart with partial artifacts enters recovery/finalization, never inferred
  uninterrupted recording.

Coordinator authority expiration prevents takeover but SHALL NOT automatically
stop an active Android master. Maximum duration and local emergency stop bound
disconnected capture.

## 9. Coordinated start and stop

All protocol-required sources SHALL be `ARMED` before standard multi-source
start. The coordinator creates an immutable start plan with source attempts,
session start time, per-source clock-model identity/translation/uncertainty,
decision deadline, and duration safeguards.

`PREPARE_START` validates/reserves the plan without starting. After every
required source reports prepared, `COMMIT_START` persists the future local
schedule. Sources execute locally at their translated monotonic time without a
network message at the start instant.

The protocol does not claim atomic distributed or hardware-synchronized start.
If commit delivery is uncertain, the coordinator attempts cancellation and
reconciles all sources; any acquired samples create immutable attempts.

Actual start is proven by `FIRST_MASTER_SAMPLE`, which records attempt/start-plan
identity, first sequence, native timestamp/provenance, device monotonic time,
mapped session time/model/uncertainty, actual profile, and late/discontinuity
status. Scheduled acceptance is not timing evidence.

Normal stop uses a near-future `STOP_AT` across active sources. Fixed-duration
protocols commit the local stop before capture. Flexible duration still requires
a maximum local duration. Emergency stop is immediate, attributed, and normally
produces an incomplete attempt; it does not wait for synchronized stop.

Required sources block prepare/start. Supplementary sources do not block unless
the immutable protocol snapshot explicitly requires otherwise.

## 10. Readiness and invalidation

Each versioned check returns `PASS`, `WARNING`, `OVERRIDE_REQUIRED`,
`BLOCKING_FAILURE`, or `NOT_ASSESSED`. Aggregate dispositions are `BLOCKED`,
`READY_WITH_OVERRIDES_REQUIRED`, `READY_WITH_WARNINGS`, `READY`, or
`READY_WITH_AUTHORIZED_DEVIATIONS`.

Mandatory groups cover workflow identity, exact source identity/version,
requested/actual configuration, recorder, bounded storage, timing, positioning
and preview, calibration/metadata, Android health, named UVC configuration,
network/recovery, coordinator, and repository readiness.

An override SHALL be explicitly permitted by the immutable protocol, preserve
the failed/observed evidence and threshold, include a meaningful reason and
signed-in Windows account, be scoped to named trial/source attempts, and remain
unexpired. It authorizes proceeding; it never changes a measurement to pass.

Non-overridable conditions include wrong/ambiguous identity, missing required
source, incompatible contract, failed recorder, insufficient bounded storage,
unavailable protocol-required profile/timing, corrupt required stream, rejected
UVC topology, invalid workflow association, unresolved conflicting trial,
unsafe repository/audit state, unenforceable maximum duration, or unresolved
camera-ownership/indicator mismatch.

Readiness records check/rule version, scope, requested/observed values,
threshold, evidence, measurement/validity interval, invalidation triggers, and
override policy. Material changes invalidate affected readiness: protocol/source
assignment, source reboot/reconnect, driver/USB/profile/control/orientation,
clock model, storage/thermal/battery/recorder, repository, coordinator restart,
camera ownership, calibration, or override validity.

Before recording, invalidation blocks start until rechecked. After recording
begins, it becomes visible health/quality evidence unless continuing is unsafe
or cannot preserve valid evidence.

## 11. Package custody

Package custody is:

```text
BUILDING -> FINALIZED_LOCAL -> COLLECTION_PENDING -> COLLECTING -> STAGED
-> VERIFYING -> VERIFIED -> COMMITTING -> COMMITTED -> RECEIPT_PENDING
-> RECEIPT_ACKNOWLEDGED -> SAFE_TO_DELETE -> DELETED_FROM_SOURCE
```

Exceptional conditions include `COLLECTION_INTERRUPTED`, `VERIFICATION_FAILED`,
`QUARANTINED`, `COMMIT_RECOVERY_REQUIRED`, and `RECEIPT_STATUS_UNKNOWN`.

Finalized packages are immutable and contain versioned identities, scientific
master, required timing/camera/IMU/events/finalization records, and complete
file length/SHA-256 inventory. Network resume and USB/MTP recovery move the
identical package through the same staging and verification boundary.

Verification precedes commit and checks schema/version, identities/protocol
role, required/unsafe paths, lengths/hashes, conflicts, master structure and
complete decode, timing/events, metadata, actual profile, and finalization.
Identical reimport is idempotent; same identity with different content is
quarantined without overwrite.

Commit is a recoverable coordinator transaction across controlled staging,
authoritative subject/session/trial destination, catalog, commit journal, and
verification record. Only `COMMITTED` counts toward completion.

A hash-bound idempotent receipt is created only after durable commit. Receipt
loss produces `RECEIPT_STATUS_UNKNOWN` and replay after repository verification,
not a second commit. Only a valid receipt for the exact package/artifact set
makes Android data `SAFE_TO_DELETE`.

MVP cleanup is never silent. One informed trained-operator confirmation may be
made through the coordinator; Android revalidates the receipt before deletion.
If remote deletion is unavailable, Android guides local deletion and clearly
separates safe, incomplete, pending, and unknown packages.

## 12. Quality, retakes, and supersession

Quality assessments are immutable/versioned interpretations over committed
artifact hashes. Per-package outcomes are `NOT_ASSESSED`, `ASSESSING`, `PASS`,
`PASS_WITH_WARNINGS`, `DEVIATION_REVIEW_REQUIRED`, `FAIL_RETAKE_REQUIRED`, or
`NOT_APPLICABLE`.

Dimensions remain separate: identity, integrity/decode, profile/measured
cadence, timing, finalization, calibration, camera metadata, IMU, continuity,
operator view, device health, and recovery provenance.

Wrong association, hash conflict, corrupt/unreadable required master, missing
required artifact, unsupported unsafe schema, unproven finalization, missing
required source, silent substitution, fabricated mandatory timing, or incomplete
capture presented as complete are not overridable.

Reviewable deviations require protocol permission and retained reason/evidence.
A trial is one continuous coordinated acquisition set. Required footage from
different times SHALL NOT be silently combined. A retake creates a new trial
and source attempts. Exclusion never deletes data; a required source cannot be
excluded merely to create completion. Supersession links immutable old/new
trials without cycles or overwrite.

## 13. Trial completion and closure

A trial may become `COMPLETE` only when:

1. it references one immutable approved protocol snapshot;
2. workflow/source/package identities are valid;
3. every required source role has one accepted attempt;
4. every accepted required package is `COMMITTED`;
5. no required package is quarantined, unknown, or recovery-pending;
6. required integrity/full-decode checks pass;
7. required timing/metadata/IMU/calibration rules pass or have authorized
   protocol deviations;
8. all blocking quality findings are resolved;
9. overrides/deviations preserve evidence, reason, scope, and operator;
10. retake/exclusion/supersession relationships are consistent;
11. trained-operator trial review is complete; and
12. an immutable completion record is durably persisted.

`CLOSED_INCOMPLETE` requires reason, failed/missing requirements, package state,
remaining recovery options, operator, and timestamp, and SHALL state that the
protocol trial was not successfully completed.

Reassessment appends a new assessment and preserves the prior one. Completed
workflow is not silently changed; deliberate reopening records reason/operator,
prior completion, affected records, and produces a newly versioned handoff when
applicable.

## 14. Compatibility and evidence requirements

- The eventual AsyncAPI/JSON Schemas SHALL use stable IDs and versions.
- Unknown required features fail safely; optional fields follow explicit rules.
- Historical finalized packages and events are never silently migrated.
- Conformance fixtures SHALL cover valid lifecycles, every forbidden transition,
  duplicate/conflicting commands, lost acknowledgement/result, restart/new boot
  epoch, disconnect/reconnect, partial finalization, transfer resume/USB import,
  verification conflict, receipt replay, readiness invalidation, override, and
  false-completion rejection.
- Simulator/source-contract acceptance is not runtime, HIL, field, clinical, or
  regulatory evidence.

## 15. Open approval boundary

The session-level protocol execution lifecycle remains intentionally undefined
in this draft. It must decide how immutable protocol snapshots instantiate trial
slots/repetitions, how additional and retake trials fill those slots, how
sessions close incomplete or reopen, and when a versioned session-completion
record and handoff may be created.

Executable control schemas and transition fixtures SHALL NOT be baselined until
that decision is explicitly approved and this combined contract is reviewed for
coherence.


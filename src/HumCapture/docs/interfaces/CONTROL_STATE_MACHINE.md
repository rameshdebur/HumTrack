# HumCapture Coordinator Control and State Contract

**Document ID:** HC-IF-CTRL-001  
**Version:** 1.3.0  
**Status:** Accepted engineering interface baseline; independent review and application implementation remain open  
**Date:** 2026-09-05

## 1. Scope

This contract defines coordinator control semantics shared by Android capture
nodes and coordinator-local UVC workers. It covers authority, trial and source
states, command acknowledgement/idempotency, coordinated start/stop, readiness,
package custody, quality, retakes, trial completion, and session/protocol
execution.

Versioned JSON Schemas, AsyncAPI operations, and conformance fixtures accompany
this baseline for session/protocol and source-control records. AsyncAPI binds
control to mutually authenticated WSS under `HC-IF-SEC-001`. Transfer endpoints,
media/package manifests, timing/IMU binary formats, repository transactions,
complete repository layout, and application implementation remain separate
controlled work.

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
| Session/trial workflow | Coordinator host | Planned, in progress, completion review, recovery required, complete, closed incomplete |
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
- immutable protocol snapshot ID, revision, content hash, and contract versions;
- session-completion and handoff-manifest IDs and revisions; and
- coordinator/device identity and source boot epoch where applicable.

Identity SHALL NOT be inferred from the active UI screen, friendly name, socket,
or arrival order. A reconnect after interrupted capture creates a new attempt;
a retake creates a new trial.

### 4.1 Wire time and numeric precision

JSON control records represent each native monotonic instant as `clock_id`,
canonical unsigned-decimal-string `ticks`, and integer `ticks_per_second`.
Mapped session instants additionally identify `session_clock_id`,
`clock_model_id`, and `uncertainty_ns`. Decimal strings preserve exact unsigned
64-bit values across Android/JVM, .NET, native, and JavaScript consumers.

Every new source boot creates a new boot and clock identity. An attempt cannot
continue across a changed boot/clock epoch. UTC timestamps support audit and
display only; they SHALL NOT order scientific samples, prove scheduled start,
or replace native/mapped monotonic evidence. See ADR-0011.

A JSON record's `*_content_sha256` is the lowercase SHA-256 of the RFC 8785
canonical UTF-8 JSON after omitting that record's own content-hash property.
Hashes that bind referenced records remain included. Consumers fail closed on
a content-hash mismatch.

## 5. Session and protocol execution lifecycle

Protocols are reusable approved versioned definitions. Creating a session
selects one approved protocol version and creates a session-owned protocol
snapshot containing the complete rules and the operator's protocol-permitted
choices. The normal session lifecycle is:

```text
DRAFT -> PLANNED -> IN_PROGRESS -> COMPLETION_REVIEW -> COMPLETE
```

| State | Entry fact |
|---|---|
| `DRAFT` | Session identity exists; protocol/source/trial planning remains editable and no plan claims to be frozen. |
| `PLANNED` | A complete content-hashed protocol snapshot and trial-slot plan are durably recorded. |
| `IN_PROGRESS` | At least one planned trial is active or has acquired a scientific-master sample. |
| `COMPLETION_REVIEW` | Acquisition is not active and the coordinator is evaluating all required slots, packages, quality, deviations, and supersession decisions. |
| `COMPLETE` | Every session completion predicate below is true and immutable completion/handoff records exist. |

Exceptional outcomes are:

- `RECOVERY_REQUIRED`: non-terminal unresolved acquisition, reconciliation,
  transfer, verification, custody, or review work;
- `CLOSED_INCOMPLETE`: terminal deliberate closure with a reason and explicit
  list of unsatisfied requirements, without a successful protocol claim; and
- `CANCELLED`: terminal cancellation before any scientific-master sample, with
  operator and reason.

The session protocol snapshot SHALL include approved protocol identity/version
and content hash, snapshot ID/revision/content hash, fixed or flexible source
count policy, selected source count and roles, trial-slot definitions,
additional-trial and retake rules, duration limits, override rules, and
completion predicates.

Before any master sample exists, a trained operator MAY revise planning by
creating a new immutable snapshot revision that explicitly supersedes the prior
revision. Once any session trial has accepted a master sample, protocol snapshot
identity and content SHALL NOT change. A material change then requires
`CLOSED_INCOMPLETE` followed by creation of a new session.

Each planned trial slot is `UNFILLED`, `IN_PROGRESS`, `SATISFIED`, or
`UNSATISFIED`. Exactly one accepted `COMPLETE` trial satisfies a required slot.
Retakes create new trials; superseded and excluded trials remain immutable.
Additional trials do not satisfy a required slot unless explicitly assigned
under the snapshot's rules.

A session may enter `COMPLETE` only when:

1. every required trial slot is `SATISFIED` by exactly one accepted complete
   trial;
2. every accepted required package is durably `COMMITTED`;
3. no required acquisition, package, custody, recovery, or result is active,
   pending, conflicting, quarantined, or unknown;
4. retake, exclusion, deviation, override, and supersession decisions are
   resolved and retained;
5. required session-level quality review is complete;
6. the signed-in trained operator explicitly finalizes the session; and
7. immutable versioned session-completion and handoff-manifest records are
   durably persisted and mutually reference the same session/snapshot/content.

Network transfer failure alone never creates terminal failure. Automatic HTTPS
or manual USB/MTP collection moves the same finalized package through the same
verification/commit predicates; the session remains `IN_PROGRESS` or
`RECOVERY_REQUIRED` until custody is resolved.

Deliberate reopening of `COMPLETE` requires an idempotent command, reason,
operator, and reference to the prior completion/handoff versions. It returns the
session to `COMPLETION_REVIEW`; prior records remain immutable and a subsequent
completion creates higher record revisions.

## 6. Coordinator trial workflow

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
| `COMPLETE` | Every trial completion predicate in section 14 is true. |

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

## 7. Source capture-attempt lifecycle

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

## 8. Commands, acknowledgements, and idempotency

Minimum source commands are:

```text
CONFIGURE, CHECK_READINESS, ARM, DISARM, PREPARE_START, COMMIT_START,
CANCEL_START_PLAN, STOP_AT, EMERGENCY_STOP, RECONCILE_STATE, GET_STATE
```

Minimum session commands are:

```text
PLAN_SESSION, REVISE_SESSION_PLAN, BEGIN_SESSION, ENTER_COMPLETION_REVIEW,
COMPLETE_SESSION, CLOSE_SESSION_INCOMPLETE, CANCEL_SESSION,
REOPEN_SESSION_FOR_REVIEW, RECONCILE_SESSION
```

An applicable command envelope contains contract/message/command identity,
coordinator/session/trial/source/attempt identity, command type, expected source
state, coordinator issue observation, command deadline or scheduled session
time, and command-specific payload.

The I0.2A source envelope binds one exact coordinator, session, trial, logical
source, capture attempt, expected source state, operator, and command payload.
Configuration, readiness, and start-plan commands carry content identity rather
than silently embedding or substituting mutable configuration.

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

## 9. Events and restart reconciliation

Source events contain event ID/type, source boot ID, monotonically increasing
per-boot event sequence, source monotonic timestamp, prior/resulting state, and
related command ID. Critical transitions are persisted before announcement.

`event_sequence` is scoped to one source boot and increases exactly once for
each authoritative event. Event monotonic time cannot regress within that boot.
`RECORDING` requires a `FIRST_MASTER_SAMPLE` event whose evidence is identical
to the authoritative source snapshot; acknowledgement or scheduled time alone
cannot create `RECORDING`.

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

## 10. Coordinated start and stop

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

## 11. Readiness and invalidation

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

## 12. Package custody

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
loss produces `RECEIPT_STATUS_UNKNOWN` and exact status reconciliation/replay,
not a second commit. Durable Android acknowledgement of the exact receipt makes
data eligible for an explicit transition to `SAFE_TO_DELETE`; it does not
delete files or determine session completion.

MVP cleanup is never silent. One informed trained-operator confirmation may be
made through the coordinator; Android revalidates the receipt before deletion.
If remote deletion is unavailable, Android guides local deletion and clearly
separates safe, incomplete, pending, and unknown packages.

## 13. Quality, retakes, and supersession

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

## 14. Trial completion and closure

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

## 15. Compatibility and evidence requirements

- AsyncAPI and JSON Schemas SHALL use stable IDs and versions.
- Unknown required features fail safely; optional fields follow explicit rules.
- Historical finalized packages and events are never silently migrated.
- Conformance fixtures SHALL cover valid lifecycles, every forbidden transition,
  duplicate/conflicting commands, lost acknowledgement/result, restart/new boot
  epoch, disconnect/reconnect, partial finalization, transfer resume/USB import,
  verification conflict, receipt replay, readiness invalidation, override, and
  false-completion rejection.
- Simulator/source-contract acceptance is not runtime, HIL, field, clinical, or
  regulatory evidence.

## 16. Baseline and deferred implementation boundary

I0.1A-I, I0.2A, and I0.3B-C are approved as engineering contract baselines.
HC-IF-CTRL-001 version 1.3.0 adds receipt acknowledgement, status reconciliation,
operator cleanup, and truthful cleanup-result records over the mutually
authenticated WSS binding introduced in 1.2.0. These extend the version 1.1.0
source command/acknowledgement,
configuration, state/event, start-plan, readiness, custody/receipt, and quality
records to the existing session/protocol slice. JSON Schema 2020-12 artifacts,
AsyncAPI 3.1.0 operations, and conformance fixtures provide an
implementation-independent oracle.

This baseline does not authorize production coordinator, Android, UVC,
repository, transfer, or UI feature implementation. Runtime credential stores,
TLS stacks, transfer services, capture/media, timing/IMU binary streams,
repository transaction implementation, receipt signing/trusted offline receipt conveyance, and application
features require their named work items and reviews. Passing schema/conformance
tests is software evidence only, not
runtime, hardware, field, clinical, regulatory, or release evidence.

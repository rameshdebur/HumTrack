# HumCapture Preliminary Risk Register

C12 control update (2026-09-12), HC-RISK-022/030/031: host admission reads a
bounded strict request, supplies the current Windows identity, preserves exact
verifier bytes and invokes existing package/evidence checks. It does not create
PASS evidence or independently establish supplied decoder/verifier claims.
HC-REP-RUNTIME-118–125 cover replay, current account, pipeline, malformed/duplicate
requests, oversized input, package/verifier tamper, unavailable staging/root
and host guard. Collection and production verifier implementation remain open.
Ctrl+C defers completion of this one admission; signal/abrupt-loss testing remains
open. No receipt/cleanup or medical conformity claim.

C11 control update (2026-09-12), HC-RISK-022/030/031: explicit process-staged
uses C3's fresh evidence validation and durable move, stops at MOVED, and skips
other states without reporting a verified state. Interrupted movement requires
startup reconciliation before a new normal attempt. Shared host exclusion and
bounded iteration remain in force. HC-REP-RUNTIME-110–117 cover processing,
repeat skip, interrupted intent/move, tamper refusal, pagination and access/cancel
controls. No receipt/cleanup authorization; Ctrl+C mid-operation and power loss
remain unverified. The C3 two-transition move is one normal processing operation.

C10 control update (2026-09-12), HC-RISK-022/030/031: automatic cataloging verifies
exact moved evidence and absent catalog/commit authority before atomically saving
catalog, six observations, reconciliation and transition. Unexpected commit
material is retained and rejected. Later finalization verifies recovered history
and observations; replay after final commit verifies final evidence too.
HC-REP-RUNTIME-103–109 pass, including rollback and synthetic history corruption.
The corruption test deliberately bypasses the append-only trigger only in its
temporary fixture. One-action-per-pass and all receipt/cleanup limits remain.

C9 control update (2026-09-12), HC-RISK-022/030/031: executable startup requires
an explicit existing root, emits controlled output/exit codes and never creates
missing repositories. A named same-session mutex refuses overlapping cooperating
hosts. HC-REP-RUNTIME-096–102 exercise real child processes including recovery,
failure preservation and mutex contention. Direct library writers, alternate
path aliases and other Windows sessions are not covered by the guard.
Ctrl+C signal delivery, abandoned-mutex recovery and crash/power-loss behavior
are not tested in C9. No capture-readiness, session completion or cleanup claim.

C8 control update (2026-09-12), HC-RISK-022/030/031: RunStartupPass separates
page completion, item recovery failures and package state. It uses the current
Windows identity and a bounded page, cancels only between transactions, retains
failed material and does not force STAGED_VERIFIED/MOVED to completion.
HC-REP-RUNTIME-088–095 verify the entry point; no executable startup integration
or capture-readiness gate is claimed. Callers must aggregate failures across
pages and retry failed identities; the cursor is not durable recovery evidence.
Only successful reconciliation observations are currently journaled. Failure
presentation/history integration and mid-pass cancellation injection remain open.

C7 control update (2026-09-12), HC-RISK-022/030/031: startup discovery is not
verification. Later-state recovery revalidates all retained package authorities,
refuses missing committed records, preserves orphan bytes/audit fields, and
rejects malformed or ambiguous orphan records without deleting material.
HC-REP-RUNTIME-077–087 cover these controls, interrupted database finalization,
read-only compatibility and stale replay. Commit-directory corruption can
conservatively block other recovery in that session; operator investigation
is required. Operator disposition tooling and host startup orchestration remain
open. Tests do not establish abrupt power-loss or concurrent-writer safety.

C6 control update (2026-09-12), HC-RISK-022/030/031: final commit requires
exact package, verification, catalog, immutable record, index, and journal
agreement. Index/reconciliation/observations/final transition commit atomically.
HC-REP-RUNTIME-064–076 cover completion, replay, interrupted publication,
missing/altered evidence, incompatible state, conflicting identities, read-only
mode, and isolation from startup replay. Failure preserves material and returns
a recovery error; persistent operator disposition remains open. Caller request
retention is required for retry. No receipt, source deletion, HIL or power-loss
qualification is conferred.

C5 control update (2026-09-12), HC-RISK-022/030/031: publication revalidates the
destination and immutable verification evidence, then atomically writes the
catalog row, contiguous transition and current state. HC-REP-RUNTIME-057–063
verify exact replay, rollback and refusal of unsafe publication conditions.
CATALOGED does not authorize receipt, completion or source cleanup. Independent
review, cross-process exclusion and abrupt power-loss evidence remain open.

All entries are `Open / not implemented / not verified` unless later evidence says otherwise. Qualitative ratings are deferred until intended use and risk method are professionally reviewed.

| Risk ID | Hazard or hazardous situation | Principal controls | Requirement links |
|---|---|---|---|
| HC-RISK-001 | Capture associated with wrong subject/session/trial | UUIDs, confirmation, manifest identity, no manual destination, audit | HC-DATA-REQ-001/003, HC-SEC-REQ-005 |
| HC-RISK-002 | Incomplete capture presented as complete | Required-source/trial state, verification before commit, recovery state | HC-DATA-REQ-002/004, HC-COORD-REQ-004 |
| HC-RISK-003 | Silent frame/timestamp/metadata loss | Sequence/timestamps, discontinuity detection, quality report | HC-TIME-REQ-005, HC-AND-REQ-001 |
| HC-RISK-004 | Incorrect temporal alignment misleads downstream analysis | Raw exchanges, uncertainty, provenance, thresholds/deviation | HC-TIME-REQ-001–005 |
| HC-RISK-005 | Wrong lens/calibration/camera movement invalidates geometry | Physical lens identity, profile matching, IMU movement, explicit uncalibrated state | HC-SYS-REQ-003, future calibration requirements |
| HC-RISK-006 | Preview/UI/network disrupts scientific master | Separate bounded paths, foreground/worker capture, resource priority | HC-SYS-REQ-002/005, HC-AND-REQ-002 |
| HC-RISK-007 | Premature Android deletion causes data loss | Commit receipt, safe/incomplete separation, explicit confirmation | HC-AND-REQ-005, HC-DATA-REQ-002/004 |
| HC-RISK-008 | Storage, thermal, battery, or USB contention interrupts capture | Preflight, max duration, monitoring, soak/capability tests | Future phase-specific requirements |
| HC-RISK-009 | Unauthorized coordinator/device or replayed command | Pairing, pinned trust, authenticated control/transfer, replay protection | HC-SEC-REQ-001/003/004 |
| HC-RISK-010 | Malicious/corrupt package overwrites valid data | Path validation, identity/hash, idempotency, quarantine, transactional commit | HC-DATA-REQ-001–003 |
| HC-RISK-011 | PII exposed through discovery, phone, logs, or export | Data minimization, coordinator-local identity, redaction, explicit export | HC-SEC-REQ-002/004/005 |
| HC-RISK-012 | Operator misinterprets preview, completion, quality, or override | Guided workflow, distinct states, reasons, training, accessibility | HC-USE-REQ-001–004, HC-COORD-REQ-004 |
| HC-RISK-013 | Unsupported OS/device/driver presented as qualified | Capability/version negotiation, compatibility matrix, named evidence | HC-COMPAT-REQ-001–003 |
| HC-RISK-014 | Unsupported medical/certification claim | Claims register, evidence linkage, CDSCO gate, qualified review | HC-REG-REQ-001–004 |
| HC-RISK-015 | A documented camera in-use indicator does not illuminate during acquisition or remains illuminated after release, misleading the operator about camera/privacy state | Exact physical identity correlation, API-authoritative state, off/on/off qualification observation, other-process check, reconciliation or named-configuration rejection | HC-UVC-REQ-004, HC-USE-REQ-002/004 |
| HC-RISK-016 | Multiple required UVC sources open and report complete frame counts while one stream is corrupted by driver, USB, profile, or concurrency interaction | Protocol-scoped concurrent preflight, named topology/driver evidence, per-stream complete decode, no silent substitution, reject/reconcile combination, soak | HC-SYS-REQ-003, HC-UVC-REQ-005, HC-TIME-REQ-005 |
| HC-RISK-017 | Primary verification evidence is lost, altered, selectively retained, or cannot be bound to the tested source/build | Controlled non-temporary vault, complete hash inventory, conflict rejection, release identity, retention/backup, independent review and future signature/eQMS control | HC-DATA-REQ-001/003, HC-REG-REQ-003/004 |
| HC-RISK-018 | A vulnerable, malicious, unmaintained, unlicensed, substituted, or unidentified software component enters a build or remains undiscoverable during response | Locked dependencies, CycloneDX SBOM per release, component hashes/identifiers, dependency review, ecosystem audit, supplier/licence review, vulnerability monitoring and VEX/patch process | HC-SEC-REQ-006, HC-COMPAT-REQ-002/003, HC-REG-REQ-003/004 |
| HC-RISK-019 | Incorrect, insecure, fabricated, or insufficiently verified AI/automated output enters a controlled baseline or creates misleading evidence | Treat generated output as untrusted draft, material-use declaration, requirement/risk traceability, independent test oracle, human review, proportionate independent review, privacy/secret restrictions, exact release identity | HC-SEC-REQ-004/006, HC-REG-REQ-003/004; HC-SOP-SW-004 |
| HC-RISK-020 | An in-progress or historical session is silently reinterpreted under a changed protocol, source count, trial plan, or completion rule | Immutable content-hashed session snapshot, audited pre-capture supersession, post-capture change rejection, new session for material change, immutable completion/handoff revisions | HC-COORD-REQ-005–011, HC-DATA-REQ-006 |
| HC-RISK-021 | Monotonic timestamps lose integer precision, cross boot/clock epochs, regress, or are replaced by UTC/arrival time, causing incorrect source state or temporal interpretation | Canonical decimal-string uint64 ticks, explicit clock identity/frequency/model/uncertainty, boot-scoped sequence, first-sample authority, range/continuity/restart rejection | HC-TIME-REQ-001–007, HC-COORD-REQ-013–015 |
| HC-RISK-022 | Interrupted, stale, or method-divergent transfer combines incompatible bytes, falsely verifies a package, or commits partial data | Immutable manifest, strong validator/If-Range, transport and stored-artifact digests, exact ranges, staging boundary, common verifier, artifact-boundary USB restart, identity-conflict quarantine, receipt only after durable commit | HC-COORD-REQ-012, HC-DATA-REQ-001–004/007–012, HC-SEC-REQ-003/005 |
| HC-RISK-023 | Rogue discovery, endpoint substitution, or bootstrap interception enrolls an unauthorized coordinator/device | Attended exact-fingerprint bootstrap, 128-bit secret/nonce, transcript binding, expiry, single use, lockout, generic errors | HC-SEC-REQ-001/007/011 |
| HC-RISK-024 | Weak/downgraded transport, replay, or certificate-only authorization permits unauthorized control or package access | Mutual TLS, TLS 1.3 default, restricted logged Win10 profile, no early data, active trust/role/UUID/resource binding, command/request idempotency | HC-SEC-REQ-003/009/010/014 |
| HC-RISK-025 | Lost, expired, revoked, or silently replaced key material leaves unauthorized persistent access or blocks safe recovery | Platform-protected keys, finite certificate lifetime, revisioned rotation/revocation/unpair, explicit re-enrollment, audit, no bypass | HC-SEC-REQ-008/012/013 |
| HC-RISK-026 | Security retries/logging disclose subject or credential data or starve scientific acquisition | Redacted audit schema, no subject/secret discovery records, bounded proof attempts, acquisition priority, offline recovery | HC-SEC-REQ-002/004/011/013/014 |
| HC-RISK-027 | Lost, replayed, regenerated, or conflicting receipt state either falsely blocks a completed acquisition or authorizes cleanup of the wrong Android package | Commit remains completion authority; immutable exact receipt; durable matched acknowledgement; status query; identical replay; conflict recovery with source retention | HC-COORD-REQ-016, HC-DATA-REQ-013/014 |
| HC-RISK-028 | Operator cleanup, concurrent activity, or partial deletion removes uncommitted data or falsely reports complete source removal | Eligible-only display; explicit selection/confirmation; immediate Android recheck; active-operation block; exact package/hash/path binding; truthful per-package remainder; manual USB fallback | HC-AND-REQ-005/006, HC-DATA-REQ-015/016, HC-USE-REQ-003 |
| HC-RISK-029 | Binary layout, clock, cadence, frame/video association, coordinate transform, or unavailable camera/IMU metadata is misinterpreted and produces misleading downstream timing or motion analysis | HC-IF-TIM-001 magic/version/size/presence/CRC/SHA controls; independent sequence/clock domains; explicit provenance/mapping/uncertainty; protocol-scoped quality; immutable golden vectors and fail-closed validator | HC-TIME-REQ-008–014, HC-AND-REQ-001, HC-SYS-REQ-003 |
| HC-RISK-030 | Crash, power loss, cross-volume copy, unsafe/deep/display-name path, or disagreement between filesystem, catalog, verification, custody, and journal state falsely marks a package committed, loses it, misassociates or overwrites another package, exposes PII, or authorizes premature source cleanup | Same-volume staging; verified immutable package; durable recoverable commit journal; explicit durability-boundary states; current plus append-only transition/reconciliation rows; shallow canonical UUID namespace; opaque package envelope; path/link rejection; atomic rename; evidence-based bounded recovery; conflict quarantine; no force commit; receipt only after reconciled `COMMITTED` state | HC-DATA-REQ-002/003/011/013/017–024/030–038, HC-COORD-REQ-003 |
| HC-RISK-031 | Mutable JSON/catalog duplication, missing or mismatched milestone indexes, lost subject PII, or silent repository migration creates competing authority, false history, unreadable sessions, or unsafe completion | SQLite-only mutable/PII authority; immutable content-hashed milestone files; exact index binding; disagreement recovery; no silent migration; coordinated repository/catalog backup required before release | HC-DATA-REQ-025–029, HC-COMPAT-REQ-002–004, HC-COORD-REQ-003 |

## Current Phase 0 evidence notes

- **HC-RISK-003 / HC-RISK-013, 2026-08-31:** The P0.2B diagnostic kept
  requested, negotiated, nominal encoded, and measured rates separate. One
  named C920 delivered approximately 26 fps and then 24 fps after exact 30/1
  negotiation, while the other delivered approximately 29.92 fps. The first
  camera remains unaccepted; no compatibility or qualification claim is made.
- **HC-RISK-015, 2026-08-31:** HC-P0-UVC-003 passed on both named C920
  configurations. Each selected camera produced two consistent off/on/off
  indicator cycles correlated to fresh frames and source release, while the
  non-selected unit remained off. This does not create a universal indicator
  requirement or a timing claim.
- **HC-RISK-016, 2026-08-31:** Two concurrent C920 runs opened and finalized
  both streams, but C920 A contained H.264 decode corruption both times while
  C920 B remained clean. The named concurrent configuration is not approved.
- **HC-RISK-016 follow-up:** Reverse startup order did not move the failure.
  Swapping physical cameras between downstream paths 2 and 3 moved corruption
  to the camera on path 2 in both repeats. The current connection combination
  is rejected; neither camera body/fixed cable is identified as the primary cause.
- **HC-RISK-003 / HC-RISK-008 / HC-RISK-016 reconciliation:** Moving Camera B
  to direct root-port branch `USBROOT(0)#USB(3)` removed the prior corruption in
  observed short runs, but automatic exposure produced approximately 15 fps
  both concurrently and alone. With Camera B temporarily held at exposure `-5`,
  it passed alone and both cameras passed two concurrent 30-second full-decode
  runs at approximately 30 fps. Automatic exposure was restored and independently
  confirmed. This is a conditional short diagnostic, not qualification; scene
  range, exposure policy, soak, recovery, and topology persistence remain open.
- **HC-RISK-003 / HC-RISK-008 / HC-RISK-016 stability follow-up:** The exact
  reconciled P0.2H configuration passed a 10-minute concurrent run with 18,002
  fully decoded frames per camera, approximately 30.0002 fps, no decode errors,
  and no timestamp regressions. All sampled power states were on AC and Camera B
  automatic exposure was independently confirmed after cleanup. The duration
  was deliberately proportionate to the MVP diagnostic; it is not the normative
  30-minute 1080p60 qualification soak; recovery remained open at that stage.
- **HC-RISK-002 / HC-RISK-003 / HC-RISK-008 / HC-RISK-012 recovery follow-up:**
  P0.2J live-disconnected exact Camera A during acquisition. Media Foundation
  returned raw HRESULT `0xC00D3EA2`; the probe finalized and fully decoded all
  265 received frames while marking requested duration false/read error. The
  ordinary verifier rejected false normal completion and Windows confirmed the
  exact interface absent. On 2026-09-01, the same parent serial and exact
  interface returned. A distinct 30-second run finalized and fully decoded
  898/898 frames at 29.9000 measured fps with zero timestamp regressions. This
  passes the bounded 1080p30 diagnostic but does not verify production recovery
  or normative 1080p60 qualification; independent review remains open.
- **HC-RISK-017 initial control, 2026-08-31:** ADR-0007 and evidence-control
  evidence receipt 1.0.0 and SBOM-aware release record 1.1.0 are implemented.
  Twelve automated tests cover successful
  import, idempotency, conflicting run identity, artifact/release tampering, and
  malformed/unexpected records, and vault-boundary rejection. P0.2J interrupted
  and post-reconnect evidence is retained and verifies as
  `HC-EV-5becff40f4271131c8b43641` and `HC-EV-16e6576bbcf4f1896d64aac6`.
  Residual risk remains high enough to block a
  dossier claim because backup/restore, access audit, independent approval,
  signature/WORM/eQMS controls, and an approved clean release are open.
- **HC-RISK-017 P0.2K integration, 2026-09-01:** The P0.2A-J reports were
  consolidated and hashed. Only the two P0.2J primary runs remain in the
  controlled vault and both verify; P0.2A-I primary artifacts are unavailable.
  The retained P0.2J runs also lack the complete normative measurement/lifecycle
  set and used diagnostic 1080p30 rather than required 1080p60. The historical
  reports remain engineering records, but a complete qualification package must
  be captured prospectively and independently reviewed.
- **HC-RISK-018 initial control, 2026-08-31:** ADR-0008, SBOM policy
  HC-GOV-SBOM-001 and a deterministic CycloneDX 1.7 generator now cover both
  npm lockfiles, the managed and native probes, target frameworks, Windows
  runtime APIs and build SDK. Four project tests, including future-manifest
  drift detection, pass. This inventory does not
  replace vulnerability, licence, maintainer/supplier, binary composition or
  runtime module analysis; those reviews remain open.
  The generated 15-component/16-node CycloneDX 1.7 SBOM passed project checks
  and official CycloneDX CLI 0.33.1 validation and is bound to the engineering
  snapshot. This reduces inventory uncertainty but does not close the risk.
- **HC-RISK-019 governance control, 2026-08-31:** HC-SOP-SW-004 defines a
  tool-neutral, risk-based workflow for material AI/automation use, human
  accountability, independent test oracles, review, traceability, and release.
  The control is documented but not operationally verified: HumCapture remains
  untracked, no protected review workflow or CI is approved, and no independent
  release review exists. The risk remains open.
- **HC-RISK-020 initial contract control, 2026-09-05:** ADR-0010 and
  `HC-IF-CTRL-001` version 1.0.0 define immutable session protocol snapshots.
  HC-CTRL-TEST-001–006 validate schema compilation/reference closure, a complete
  fixed-source lifecycle and handoff, 553 forbidden transition combinations,
  named invalid fixtures, post-capture snapshot-change rejection, false
  completion rejection, and flexible source-count bounds. Application/runtime,
  persistence, restart, independent-review, and field evidence remain open.
- **HC-RISK-021 initial contract control, 2026-09-05:** ADR-0011 and
  HC-IF-CTRL-001 version 1.1.0 define exact JSON monotonic instants and explicit
  mapped-time uncertainty. HC-CTRL-TEST-007–019 cover exact uint64 maximum and
  overflow/lexical rejection, clock identity, boot-epoch attempt replacement,
  event order, first-master-sample proof, command replay/conflict, and
  fail-closed source/event transition matrices, plus RFC 8785/SHA-256 content
  identity stability and tamper rejection. Timing implementation, binary
  stream format, runtime clock fitting, HIL synchronization, and field evidence
  remain open.
- **HC-RISK-022 initial contract control, 2026-09-05:** ADR-0012 and
  `HC-IF-XFR-001` version 1.0.0 define immutable manifest publication,
  coordinator-pulled HTTPS byte ranges under a stable strong validator,
  artifact-boundary USB/MTP recovery, staging isolation, common verification,
  idempotency/quarantine, and commit/receipt linkage. HC-XFR-TEST-001–011 pass
  source-level conformance. Runtime transport, storage durability, device loss,
  hostile-network, HIL, field, and independent review evidence remain open.
- **HC-RISK-023–026 initial contract control, 2026-09-06:** ADR-0013 and
  `HC-IF-SEC-001` version 1.0.0 define attended exact-fingerprint enrollment,
  platform-protected peer keys, mutual TLS, role/resource authorization,
  restricted Windows 10 compatibility, replay/lockout controls, revocation and
  recovery, and redacted audit. HC-SEC-TEST-001–012 pass source-level
  conformance. Runtime Keystore/DPAPI/TLS, hostile-network, penetration,
  restart/power, HIL, field, independent, and qualified regulatory evidence
  remain open.
- **HC-RISK-027–028 initial contract control, 2026-09-07:** ADR-0014 and
  `HC-IF-RCP-001` version 1.0.0 separate durable acquisition completion from
  cleanup eligibility; define immutable receipt replay/conflict recovery,
  matched durable acknowledgement, explicit operator cleanup, active-operation
  exclusion, truthful partial results, and manual USB/MTP fallback. HC-RCP-
  TEST-001–010 are source-level only. Runtime persistence/deletion, restart,
  HIL, field, independent, and qualified regulatory evidence remain open.
- **HC-RISK-030 architectural control, 2026-09-09:** ADR-0016 fixes the
  Coordinator-owned SQLite/filesystem authority model, same-volume staging,
  recoverable journal, atomic repository-boundary rename, post-operation
  reconciliation, conflict quarantine and receipt-after-commit ordering.
  Exact schemas, durability APIs, crash/power fault injection, runtime,
  backup/restore, field, independent and qualified regulatory evidence remain
  open.
- **HC-RISK-030 namespace refinement, 2026-09-10:** ADR-0017 and
  HC-IF-REP-001 version 1.0.0 fix a shallow canonical UUID repository hierarchy,
  preserve the exact verified package envelope, exclude display/PII values from
  paths, and reject unsafe/link-based substitution. Executable schemas and
  Windows path tests remain I0.4B-B3 work.
- **HC-RISK-031 architectural control, 2026-09-10:** ADR-0018 and
  HC-IF-REP-001 version 1.1.0 separate mutable SQLite authority from immutable
  package/milestone evidence, require exact content-hash indexing, fail to
  recovery on disagreement, and prohibit silent migration. Executable schemas,
  catalog reconstruction, backup/restore and runtime fault evidence remain open.
- **HC-RISK-030 transaction-state refinement, 2026-09-10:** ADR-0019 and
  HC-IF-REP-001 version 1.2.0 assign explicit states to verified staging,
  durable intent, atomic rename, catalog publication, full reconciliation,
  recovery and quarantine. Exact schemas, automatic/operator action policy,
  crash fixtures and runtime/power-loss evidence remain open.
- **HC-RISK-030 reconciliation-policy refinement, 2026-09-10:** ADR-0020 and
  HC-IF-REP-001 version 1.3.0 require append-only transition/reconciliation
  history, fact/result separation, bounded exact automatic continuation,
  operator-controlled conflict disposition, and no force commit. Executable
  schemas, DDL, crash fixtures and runtime/power-loss evidence remain open.
- **HC-RISK-030 / HC-RISK-031 executable contract control, 2026-09-10:**
  ADR-0021 and HC-REP-TEST-001–015 add closed repository schemas, executable
  constrained/append-only SQLite DDL, exact namespace and cross-record
  bindings, exhaustive transition rejection, complete observations, bounded
  recovery fixtures and post-reconciliation receipt gating. These are
  source-level controls; production filesystem/SQLite behavior, power-loss,
  backup/restore, independent and qualified regulatory evidence remain open.
- **HC-RISK-030 / HC-RISK-031 initialize/open implementation control,
  2026-09-10:** I0.4B-C1 and ADR-0022 add a locked production SQLite provider,
  real empty-root initialization, catalog creation from the accepted embedded
  DDL, flushed descriptor-last publication, reparse/hard-link rejection,
  integrity/metadata agreement and explicit no-write compatibility outcomes.
  HC-REP-RUNTIME-001–017 pass on the local Windows process/filesystem. Abrupt
  termination, OS/power-loss, removable storage, package commit/reconciliation,
  backup/restore, independent and qualified regulatory evidence remain open.
- **HC-RISK-022 / HC-RISK-030 / HC-RISK-031 verified-staging implementation
  control, 2026-09-10:** I0.4B-C2 rechecks canonical package identity, RFC 8785
  content identity, exact artifact bytes/inventory, path/link safety and a
  successful content-bound immutable verification record before admitting
  `STAGED_VERIFIED`. Verification-record publication is non-overwriting; the
  current row, initial append-only transition and verification index commit in
  one SQLite transaction. Exact replay is idempotent and conflicting reuse,
  destination material or index collision fails closed. HC-REP-RUNTIME-018–031
  pass locally. Collection transport, production media decoding, later commit/
  reconciliation states, concurrent external mutation, abrupt process/OS/power
  loss, independent and qualified regulatory evidence remain open.
- **HC-RISK-022 / HC-RISK-030 / HC-RISK-031 commit-intent and atomic-move
  implementation control, 2026-09-11:** I0.4B-C3 revalidates exact staged
  evidence before intent, proves same-volume placement, refuses destination
  overwrite, atomically journals `COMMITTING`, performs a write-through Windows
  directory rename, revalidates the destination and atomically journals
  `MOVED`. Exact replay is idempotent. Failures before the intent transaction
  roll back and retain staging; failures after intent retain the observable
  package locations and durable `COMMITTING` state for later reconciliation.
  HC-REP-RUNTIME-032–045 pass locally and exact-source CI passes. This is not a
  power-loss, cross-process exclusion, reconciliation, `COMMITTED`, receipt,
  cleanup, HIL, field, independent or qualified regulatory claim.
- **HC-RISK-022 / HC-RISK-030 / HC-RISK-031 startup-reconciliation
  implementation control, 2026-09-11:** I0.4B-C4 observes all six accepted
  repository authorities and automatically performs only exact `NO_ACTION`,
  `RETRY_FROM_STAGED` or `RESUME_AFTER_MOVE` outcomes within the implemented
  C2/C3 boundary. Reconciliation, observations and any contiguous state
  transition are committed atomically; exact replay is idempotent. Changed,
  dual-path, unsafe, unreadable or later-authority evidence fails closed and
  retains package material. HC-REP-RUNTIME-046–056 pass locally, including
  transactional rollback and successful C3 retry after reconciliation. This is
  not operator/quarantine implementation, cross-process exclusion, abrupt
  process/OS/power-loss, final commit, HIL, field, independent or qualified
  regulatory evidence.

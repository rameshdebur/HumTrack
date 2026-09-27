# HC-VR-I0-4B-C14XYZ-001: Bound assignment, failure history and recovery

Date: 2026-09-27. Approved C14X-Z; ADR-0038 / HC-IF-VWF-001.
AI-assisted implementation and verification. Independent human review open.
Only src/HumCapture modified. No subject recordings or physical cameras used.

## Objective and implemented boundary

Compare imported packages with independently recorded coordinator intent,
retain failed/unassessed attempts, and recover interrupted verification without
inventing success. Assignment includes subject/session/trial/slot/source/role,
capture attempt/boot/configuration and canonical protocol bytes. The internal
adapter requires already-fixed capture assignments; it is not session planning
or demographics UI. It accepts fixed or flexible single-camera protocols.

An explicitly initialized SQLite version-1 ledger owns immutable assignments
and append-only STARTED/FAILED/CANCELLED/INTERRUPTED/VERIFIED_READY/ADMITTED
events. Existing custody catalog remains authoritative for package admission.
Payload hashes, repository/version binding, exclusive Windows workflow lock
and existing account attribution are used. No new login/security subsystem.
Each failure has a reason and recovery action; available failed verification
records retain NOT_ASSESSED checks. New attempt IDs preserve earlier history.

READY contains exact verification bytes before admission. Explicit recovery
reopens a package lease, checks unchanged package/assignment/record evidence,
and repeats admission idempotently. A STARTED-only attempt becomes INTERRUPTED.
Terminal replay is historical evidence, not a fresh custody assessment.
No new package move, commit, receipt, take completion or source cleanup.

## Verification results

| Evidence level | Result | Retained output |
|---|---|---|
| Build/static | Release, zero warnings/errors | I0_4B_C14XYZ_BUILD_RESULTS.txt |
| Automated runtime | 210/210 passing | I0_4B_C14XYZ_RUNTIME_RESULTS.txt |
| Contract regression | 115/115 passing | I0_4B_C14XYZ_CONTRACT_RESULTS.txt |
| SBOM/register | 13/13; project format/inventory checks pass | I0_4B_C14XYZ_SBOM_RESULTS.txt |
| Runtime integration | Real pinned decoder, synthetic master, collection to staged admission | I0_4B_C14XYZ_REAL_RESULTS.txt |
| Hosted checks | Separately source-SHA-bound push/PR runs | GitHub HumCapture validation |
| HIL / field / clinical / regulatory | Not performed | No claim |

Runtime groups 202-210 cover immutable assignment/reassignment and session
conflicts; wrong subject/session/trial/source/attempt/boot/configuration/hash;
invalid role/slot; failed decoder record retention; exact terminal replay and
changed-request refusal; bounded history; append-only SQL refusal; distinct
retries; cancellation and interruption; READY and post-catalog crash boundaries;
version/repository/payload-hash refusal; busy workflow; missing-ledger refusal;
wrong protocol snapshot/source kind/count; one-source fixed/flexible policy;
READY cancellation and changed staging followed by exact-byte recovery.
Existing groups 001-201 passed unchanged, including shared input/decoder/timing
guards, local collection, staged admission, commit/reconciliation and host tests.

Restart tests inject exceptions at named persistence boundaries and construct
a new RepositoryService after handles close. They do not kill the OS/process
or prove power-loss resilience. The actual decoder case uses the retained
master3.mp4 synthetic fixture and pinned ffmpeg/ffprobe 9.0.1 binaries; no supplied
decoder-success callback exists. Source remains present and no final destination
is created. Survivor tests also hash source files before/after recovery.

## Reproduction and execution findings

From HumCapture root: build the Coordinator.Repository.SelfTest project in
Release with --no-restore, then run it with --no-build (all 210 tests).
Run --package-input for the focused package/workflow group. Actual-decoder test:
--bound-verification-real <absolute-pinned-bin-directory> <absolute-media-directory>.
Fixtures are isolated under evidence-vault/workflow-tests and removed by their
owner after each test. Contract suite: npm run test:contracts --prefix
tools/evidence-control. SBOM suite: npm test --prefix tools/sbom; project
validation and licence-register --check follow.

Initial focused execution exposed lock validation reopening its own exclusive
file. Corrected by inspecting the already-held Windows handle; busy exclusion
and all runtime tests pass. The first real-decoder invocation supplied a relative
binary path, correctly producing nonverified evidence. Repeating with the
absolute pinned directory passed. Neither failed run is counted as acceptance.

## Supply chain and release limits

SBOM 0.1.0-i0.4b-c14z; 37 components plus product, 38 closed dependency nodes.
Generated 2026-09-27T12:28:46Z. SHA-256:
`c99c651934a41c9148e5f6853f23145a422333c576e217fe56fa0862f9c8e891`.
No new dependency; published JsonSchema.Net remains 9.4.0. Licence register
coverage refreshed; legal approval, prices and advisory findings not reassessed.
Current checks are the project validator, not a newly run official CLI audit.
Decoder runtime_enabled and redistribution_approved remain false.

Architecture skill trade-off review selected explicit, separately versioned
persistence rather than changing the existing custody catalog silently. This
requires coordinated backup/restore qualification of both databases before
production activation. Full session lifecycle/plan revisions, operator UI and
host activation, broad old-API enforcement, actual process/power-loss acceptance,
HIL/field, independent QA and qualified Indian regulatory review remain open.
This is engineering evidence, not certification or a controlled product release.

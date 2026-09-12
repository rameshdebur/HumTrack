# HumCapture Windows Coordinator

This subtree contains the dedicated Windows Coordinator application and its
headless services. It borrows HumTrack's Windows engineering principles but
does not reference HumTrack application internals.

## Repository core

`src/HumCapture.Coordinator.Repository` contains the I0.4B-C1 initialize/open,
I0.4B-C2 verified-staging journal, I0.4B-C3 commit-intent/atomic-move, and
I0.4B-C4 bounded startup-reconciliation slices. It:

- targets Windows 10 version 2004 (build 19041) or later on .NET 10;
- pins and lock-resolves `Microsoft.Data.Sqlite` 10.0.12;
- embeds the accepted `repository-v1.sql` contract as the catalog source;
- initializes only an empty absolute data root;
- publishes `repository.json` only after the catalog is created, checked and
  flushed, using a flushed same-directory temporary file and non-overwriting
  move;
- rejects reparse points and hard-linked descriptor/catalog files;
- enables mutation only when descriptor versions/features, SQLite integrity
  and catalog metadata agree exactly; and
- returns explicit read-only inspection for unsupported versions/features
  without opening the catalog or performing migration;
- rechecks an already-collected staged package against its manifest bytes,
  artifact inventory, canonical content hashes and successful immutable
  verification record before journal admission;
- publishes the exact verification record without overwrite; and
- atomically inserts the initial `STAGED_VERIFIED` transaction, sequence-1
  transition and verification-record index, with exact-replay idempotency and
  fail-closed identity conflict handling;
- revalidates journal history, immutable verification evidence, exact staged
  bytes, canonical paths, volume identity and repository availability before
  crossing the repository boundary;
- atomically records `COMMITTING` intent before moving package data; and
- uses a same-volume, non-overwriting Windows write-through directory move,
  revalidates the exact destination package, then atomically advances the
  journal to `MOVED`;
- observes journal, staging, destination, catalog linkage, immutable
  verification record and commit record separately at startup; and
- atomically retains the six observations plus `NO_ACTION`,
  `RETRY_FROM_STAGED`, or `RESUME_AFTER_MOVE`, with exact replay and contiguous
  recovery transitions.

The C2 API is an admission boundary for a package already collected by the
future transfer/common-verifier service. C3 moves that package only through
the internal `MOVED` durability boundary. Neither API collects or decodes
media. C4 automatically acts only on exact, safe, supported evidence within
`STAGED_VERIFIED`, `COMMITTING`, and `MOVED`; every other observation fails
closed for trained-operator handling without moving or deleting material.
`MOVED` is not cataloged or committed and cannot authorize completion, a
receipt or source cleanup. The component does not yet implement subject
records, transfer, catalog/commit reconciliation, operator/quarantine actions,
receipts, backup/restore, UI, or migration.

## Verification

C6 adds `CompleteCatalogedPackage`, taking the original catalog publication
and retained finalization identities/timestamps. It publishes a versioned
immutable commit record, verifies its bytes and package evidence, then saves
the index, six observations, PRE_RECEIPT reconciliation and COMMITTED transition
atomically. Exact retry reuses a retained file after a database rollback.
Replay revalidates existing evidence and never recreates a missing committed
record. Exact public retry uses the retained request. C7 startup recovery can
instead reconstruct requests from retained catalog and transition history.

C7 adds `ListStartupTransactions(root, afterTransactionId, limit)` (default 100,
maximum 1000). A candidate's stored state is not fresh verification. Pass each
candidate to `ReconcileStartupTransaction`: CATALOGED requires transition and
operation IDs and can finalize with STARTUP provenance; COMMITTED requires
null transition/operation IDs and records CONFIRM_IDEMPOTENT_COMMIT without
changing revision. Use a fresh reconciliation ID for a new inspection.
Exact reconciliation replay still checks current committed evidence.
An interrupted unindexed commit retains its original bytes and audit fields;
the new recovery audit records the current actor/time separately. Ambiguous,
malformed or conflicting records block recovery and are preserved. Missing
committed records are never recreated. C8/C9 add host startup orchestration and
C10 adds automatic MOVED-to-CATALOGED publication. Receipts remain separate work.

C8 supplies `RunStartupPass(root, afterTransactionId, maxTransactions,
cancellationToken)` as the host-callable startup entry point in this assembly.
It opens the repository, uses the current Windows identity, and runs one
bounded page (default 100, maximum 1000) before returning. Call before starting
capture, on the future host's worker context, not a UI or acquisition thread.
C9 now provides the executable below; a capture scheduler remains unimplemented.

Status is Completed, MoreWork, Cancelled or ReadOnlyInspection. Completed means
only that no further candidates were found after this cursor at the final check.
Always inspect RequiresOperatorAttention and every item; neither Completed nor
a successful reconciliation means session completion, receipt authorization or
permission to delete source data. STAGED_VERIFIED/MOVED results need normal
package workflow continuation. Failed items retain material and a controlled
error/next action; raw exceptions and absolute paths are not returned per item.

Continue MoreWork from LastProcessedTransactionId. Aggregate results across
pages, including failures; failed items also advance the cursor and must be
retried explicitly or on a later fresh pass. Start a new application startup
from a null cursor. This is not a cross-page snapshot or durable failure log.
Cancellation returns completed item results and is observed between transactions,
not mid-finalization. Waiting for the process-local gate and a running
verification are not immediately cancellable. There is no automatic retry loop.
Audit timestamps supplied to reconciliation identify the action request, not
measured verification elapsed time. Root/open failures propagate and must block
repository startup rather than being treated as an empty successful pass.

## Executable host (C9)

From the HumCapture directory, run against an existing repository:

```powershell
dotnet run --project apps/windows-coordinator/src/HumCapture.Coordinator.Host -c Release -- startup --root "D:\HumCaptureData" --limit 100
```

The example path must be replaced with the actual repository root. This command
can perform the approved recovery actions; it is not a read-only diagnostic.
It never initializes missing repositories, starts cameras, issues receipts or
deletes source data. It runs once and exits, without opening a visible UI.
The host requires the project's .NET 10 Windows runtime/build environment.

For structured output without build output, build first and invoke
`HumCapture.Coordinator.Host.exe` in its Release target-framework directory.
Pass `--after UUID` from last_processed_transaction_id to continue a page.
Restart a fresh scan from no cursor; retain prior page failures for investigation.
The single JSON output has schema_version 1.2.0 (C12). Root errors contain a controlled
code only. Per-item output has transaction ID, state, action, error and next action.
Completed describes scan exhaustion, not capture/session completion.

| Exit | Meaning |
|---|---|
| 0 | Completed scan with no item errors |
| 2 | Invalid arguments |
| 3 | Repository/startup failure |
| 4 | One or more items need operator investigation |
| 5 | More candidates remain |
| 6 | Inspection-only repository |
| 7 | Another cooperating host holds this root's same-session guard |
| 130 | Cancellation between transactions |

Cancellation wins over item errors; item errors win over MoreWork, so inspect
the output status as well as the exit code. Ctrl+C requests cancellation but
does not interrupt a finalization already in progress.
The named mutex is limited to cooperating hosts in one Windows session; it does
not protect against direct library writers, other sessions or path aliases.
Do not run concurrent writers through these other routes.
No installer, background service, capture-readiness gate or controlled release
is supplied. See ADR-0023 and HC-VR-I0-4B-C9-001.

## Automatic catalog recovery (C10)

The host now advances a directly verified MOVED package to CATALOGED using
COMPLETE_CATALOGING. Catalog row, six pre-action observations, STARTUP
reconciliation and state transition are atomic. A later fresh pass can finalize
the CATALOGED package, and another can confirm COMMITTED. One pass still performs
only one action per package; Completed remains scan exhaustion, not final commit.

Direct ReconcileStartupTransaction callers select MOVED catalog recovery by
supplying both result transition and operation IDs. Existing calls with neither
retain their observation-only NO_ACTION behavior. Exact request replay rechecks
the catalog, history and package; if already COMMITTED it also rechecks final
evidence. Normal PublishMovedPackage replay semantics remain unchanged.
Recovered STARTUP publication history is not relabelled NORMAL.

Unexpected matching commit files, catalog conflict, changed/missing evidence
and unsafe paths block recovery without overwrite or deletion. STAGED_VERIFIED
uses the explicit processing command below. No receipt or source cleanup is authorized.

## Normal staged processing (C11)

Use process-staged with the same --root, --limit and --after arguments as startup.
The library entry point is ProcessStagedPass. It initiates the existing durable
STAGED_VERIFIED -> COMMITTING -> MOVED operation, then stops for that package.
It does not catalog or commit automatically. Startup remains recovery-only:
it does not initiate movement of an intact STAGED_VERIFIED package.

process-staged skips other journal states with action SKIP_NOT_STAGED and null
state. A skip is not proof of validity or completion, even when the exit code is 0.
Successful movement reports MOVE_STAGED_PACKAGE, state MOVED and next action
RUN_STARTUP_TO_CONTINUE_CATALOGING. Failed movement preserves evidence and reports
RETAIN_AND_RUN_STARTUP_RECOVERY. Run startup to establish the actual state before
attempting normal processing again; no blind in-place move retry is performed.

Both commands share bounds, pagination, account attribution, same-session mutex,
cancellation-between-packages and exit codes. Page limits count all inspected
candidates, including skipped entries. Start a fresh pass without --after to
revisit earlier candidates; always aggregate per-item errors. Audit timestamps
describe requests, not measured acquisition or elapsed processing time.
The CLI output minor version is now 1.2.0; all previous fields remain.
No transfer ingestion, receipt, source deletion, capture readiness or UI is added.

## Admission of verified staging (C12)

Use admit-staged --root ABSOLUTE_ROOT --request ABSOLUTE_JSON. It invokes the
existing C2 admission boundary, stopping at STAGED_VERIFIED. The library API is
AdmitStagedRequest. It does not copy external packages, initialize a repository,
generate verifier records or rerun media decoding.

The request uses HC-IF-HOST-ADM-001 1.0.0, documented in
docs/interfaces/HOST_ADMISSION_CONTRACT.md. It carries stable admission/context
IDs, package hashes/length/count, a fixed UTC recorded_at and base64 of the exact
existing verifier-record bytes. The current Windows account is supplied by the
host; a request-provided actor is rejected. Keep this file outside the package
directory. The request and package bytes are not rewritten.

Unknown/duplicate fields, unsupported versions, oversized requests and unsafe
request links are refused. The existing verifier binding and package validation
must pass before admission. A supplied PASS flag is not independent verification.
Exit 0 returns Admitted plus state/revision/transaction identity and exact-replay
status; it is not acquisition completion. --limit/--after do not apply.
Ctrl+C is deferred until this single admission completes; then the host exits
with the actual result. Abrupt termination remains unqualified.

Use the same request/account for retry before movement. Afterwards use startup.
After successful admission, process-staged may move the package; subsequent
startup passes may catalog and commit it. No receipt/cleanup is authorized.

C5 adds `PublishMovedPackage`: it revalidates the final package and verification
record, then inserts the immutable package catalog row and advances the journal
from `MOVED` to `CATALOGED` in one SQLite transaction. Exact replay checks the
complete catalog binding and original transition. C6 implements normal final
commit, and C7 extends the C4 startup API to CATALOGED and COMMITTED.

Run the Coordinator repository self-tests directly:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/HumCapture.Coordinator.Repository.SelfTest.csproj --configuration Release
```

They are also part of `tools/evidence-control`'s default test command.

# Local folder collection

HC-IF-COL-LOCAL-001, version 1.0.0; C13 engineering baseline, 2026-09-15.
Ordinary-folder subset of HC-IF-XFR-001 1.2.0 and ADR-0026.

`collect-local --root ABSOLUTE_REPOSITORY --source ABSOLUTE_PACKAGE_FOLDER --attempt UUID`

All three options are required, unique and exclusive. Root must be initialized
and writable; source/root cannot overlap. An ordinary mounted folder is supported,
not a Windows Shell MTP object. Retry uses the same source and attempt UUID.
API: RepositoryService.CollectLocalFolder(root, source, attempt, cancellationToken).
The API uses the process-local root gate; the command also uses the existing
same-session mutex. Neither excludes arbitrary writers or other Windows sessions.

Manifest/schema/identity/inventory/length/hash checks precede copying. No verifier
record is consumed or produced. Originals are read-only; the manifest is held
against cooperating write/delete during collection. Files copy sequentially with
a 1 MiB buffer, flush, hash check and no-replace rename. Existing payload files
are rehashed before reuse. Only collector-owned partials restart from zero.
The original manifest is published last via rename; destination bytes are checked.

ADR-0026 defines staging layout. The existing checkpoint schema 1.0.0 is used,
with method COORDINATOR_LOCAL, never VERIFIED or artifact_verification_id.
STAGED is byte collection only. Retry reconstructs progress from files and keeps
attempt/restart counters. A lagging checkpoint is not trusted over actual bytes.

Manifest mismatch or different-content package ID in another collection is
refused with material retained for inspection. This is a hold, not an automated
quarantine-resolution UI. Any already-admitted package returns JournalConflict;
use repository recovery instead. No transaction/catalog/subject record is created.

Host envelope remains 1.2.0. Success: status COLLECTED_UNVERIFIED, copied_files,
reused_files. No source path, name or account is printed. Exit codes: 0 collected,
2 usage, 3 failure, 7 host contention, 130 cancellation. Failure retains evidence.
Cancellation checks run before work, at file boundaries and while copying;
source/destination hashing and short publications are synchronous/noninterruptible.

Excluded: direct MTP/HTTPS, salvaging newly damaged declared artifacts, full video
decode, timing/metadata scientific verification, automatic admission, receipt,
cleanup, UI, live-acquisition scheduling, field or physical power-loss acceptance.

# ADR-0038: Bound verification and retained attempt history

2026-09-27. Engineering design under user-approved C14X-Z.
Primary Architect/Coordinator-Repository engineer; affected Timing, Android/UVC,
QA, Risk and Release/SBOM owners. QA owns verification. Independent human review
remains open. Existing ARD/PRD/stories/SRS/governance and preliminary India
baseline apply; no intended-use, clinical or regulatory claims change.

Use an explicitly initialized version-1 SQLite workflow ledger at
catalog/verification-workflow.sqlite3. Do not silently migrate repository-v1.sql,
reuse transaction states for failed pre-admission attempts, or mirror mutable
state into JSON files. The existing catalog alone remains package custody
authority; this ledger owns immutable expected capture assignments and append-only
verification attempts. Initialization is an internal explicit operation, not
production activation. Both databases must be retained in coordinated backup;
backup/export adaptation is a release gate.

Assignments originate from coordinator intent, not from imported package fields.
Store an immutable assignment UUID, subject/session/trial/slot/source/role,
capture attempt/boot/configuration IDs, configuration hash, source kind and exact
protocol snapshot. Validate canonical snapshot/schema/hash, selected source-count
policy, unique roles/slots and role/source compatibility. One capture attempt
cannot be reassigned; corrections require a new capture attempt. This is not a
full session state machine, subject-demographics editor or proof of protocol's
clinical suitability. Windows account attribution is sufficient for this MVP.

One operation envelope binds stable admission IDs and audit time to an assignment.
Append STARTED before collecting/verifying; FAILED or CANCELLED retains reason
and next action. New attempts never overwrite prior events. Persist exact
VERIFIED_READY record bytes/hash before journal admission, then ADMITTED after
the existing catalog accepts them. FAILED verification records are retained too.
No common transaction spans the two databases: recover READY by revalidating
assignment, manifest, package lease and saved record, then idempotently repeating
admission. A STARTED-only attempt is INTERRUPTED, never inferred successful;
retry uses new attempt IDs. Exact terminal requests replay retained results.

Use a cooperative exclusive workflow lock across processes and SQLite FULL
synchronous transactions; concurrent callers receive busy rather than race a
recovery. No new privileged service, user authentication or automatic cleanup.
Storage failure propagates; do not claim a failure record was saved when its
write failed. A stale process lock is released by Windows after process exit.
Ledger version/repository/hash mismatch fails closed without migration.

All new workflow methods are internal. Existing legacy APIs remain available;
this is not a claim every production entry point now enforces session binding.
No UI activation, acquisition scheduling, master movement, receipt, take
completion or deletion. Tests cover recovery boundaries with synthetic input.

Trade-off: a separate opt-in ledger avoids a premature main-catalog migration,
but adds a coordinated-backup requirement and explicit two-store recovery.
Reconsider consolidation only with an approved catalog migration plan.

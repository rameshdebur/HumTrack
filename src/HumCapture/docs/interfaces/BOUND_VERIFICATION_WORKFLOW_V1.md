# HC-IF-VWF-001 version 1.0.0

Engineering baseline 2026-09-27, ADR-0038, approved C14X-Z scope.

Ledger: catalog/verification-workflow.sqlite3. Application/schema version 1,
bound to repository UUID. SQL schema is verification-workflow-v1.sql.
No automatic creation from read/run operations; initialization is explicit.
Unknown versions, repository mismatch, altered payload hash or unsafe paths
refuse use. No migration or repair is inferred.

Assignment JSON uses the versioned CaptureAssignment DTO with canonical
snake_case property names and schema_version "1.0.0". Exact canonical bytes
and their SHA-256 are stored with Windows account attribution. Protocol bytes
are retained as base64 in protocol_utf8 and independently schema/hash validated.
No subject name or demographic values are added. The assignment is immutable
and capture_attempt_id unique. Slot must exist in the snapshot; optional
additional-trial slot allocation and plan revisions are future workflow work.
The first assignment freezes subject and protocol bytes for that session.
Assignments in a trial share its slot and a consistent one-to-one source/role
mapping. Reconfiguration requires a new trial or session as appropriate.

Run request: assignment_id plus VerificationAdmissionRequest, in canonical
snake_case JSON, version 1.0.0. verification_record_id is the attempt identity.
Reusing it with different request bytes is a conflict. A retry is a new attempt
with new verification/admission operation identities. Audit time is stable per
attempt; recorded event time is actual UTC. Payloads are bounded to 32 MiB.

Event states: STARTED, VERIFIED_READY, ADMITTED, FAILED, CANCELLED, INTERRUPTED.
Every event includes reason_code, next_action and optional verification bytes,
their SHA-256, and admission snapshot. Events are append-only, sequence-ordered
and content-hashed. FAILED can retain an existing verification-record whose
required checks are NOT_ASSESSED. No failed/unassessed result becomes admission.
No raw exception text or source path is persisted in the reason code.
History reads are bounded/paginated and do not initialize storage.

Next actions distinguish RETRY_NEW_ATTEMPT, CORRECT_ASSIGNMENT_OR_SOURCE,
RECONCILE_REPOSITORY and CONTINUE_EXISTING_REPOSITORY_WORKFLOW. They are guidance,
not automatic actions, quality overrides, or deletion permission.

On restart, explicit recovery of STARTED appends INTERRUPTED. Recovery of
VERIFIED_READY validates retained bytes/hash/record ID/package identity and
reopens a stable source lease before repeating existing admission. A crash
after catalog admission but before ADMITTED is handled by exact admission replay.
Failed terminal attempts replay unchanged; a repaired environment is tested
only by a new attempt. Source files and earlier events remain untouched.
Terminal replay is historical evidence, not a fresh revalidation of present
custody, a completion receipt, or authorization to remove source data.

The bound workflow adds a coordinator-assignment UUID/content-hash reference
to the existing PACKAGE_IDENTITY check while leaving full session lifecycle/
clinical protocol authorization unclaimed. Slot and role come from coordinator
intent and are checked against the snapshot, not invented package fields. It reuses
published schemas, local collection and pinned decoding. Current source
count policy is validated without imposing two cameras universally.

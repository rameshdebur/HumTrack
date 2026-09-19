# Host Admission Request Contract

**ID:** HC-IF-HOST-ADM-001  
**Version:** 1.0.0  
**Status:** Accepted C12 engineering contract; independent review pending

This request bridges the host to existing HC-IF-REP-001 1.3.0 staged admission.
It is not a source package manifest, verifier implementation or transfer request.
The package must already exist at staging/{collection_attempt_id}/{package_id}
under the selected initialized repository. Keep the request outside that immutable
package directory. The host never copies or creates the source package.

UTF-8 JSON, 1–1,048,576 bytes, maximum nesting depth 8, no comments/trailing
commas, duplicate or unknown properties. Exactly two top-level properties:
schema_version (string, exactly 1.0.0) and registration (object).
All following registration fields are required:

| Fields | Type / meaning |
|---|---|
| transaction_id, transition_id, operation_id, record_index_entry_id | Nonempty UUID strings; stable admission identities |
| subject_id, session_id, trial_id, source_id, capture_attempt_id, collection_attempt_id, package_id | Nonempty UUID strings bound to package/collection context |
| package_content_sha256, artifact_set_sha256 | Existing package identity hashes, 64 lowercase hexadecimal characters |
| verification_record_id | Nonempty verifier-record UUID |
| verification_record_content_sha256 | SHA-256 of exact verifier-record bytes |
| package_byte_length | Canonical unsigned-decimal string, not JSON number |
| artifact_count | Positive integer, matching the manifest |
| verification_record_utf8 | Base64 string containing exact existing verification JSON bytes |
| recorded_at | Fixed, non-minimum UTC ISO timestamp (zero offset); retained for retry |

actor_windows_account is prohibited, including null: the host injects the
current Windows identity. Existing registration validation remains authoritative
for bounds and identity/hash rules. Null, omitted or malformed required fields
cannot authorize admission. Unknown keys are rejected, not ignored.

Base64 preserves verification-record bytes exactly, including whitespace. Do not
regenerate or edit a record merely to satisfy its declared hash. Admission validates
the supplied record's binding and required check results against the package;
it does not independently rerun video decoding, timing/metadata validation or
prove verifier provenance. A real verifier remains responsible for those results.

CLI: admit-staged --root ABSOLUTE_ROOT --request ABSOLUTE_JSON.
--limit and --after are not accepted here. The request is read-only and must be
a safe regular single-link file. Supported repository and path checks precede
admission. Outcome stops at STAGED_VERIFIED. No movement, catalog publication,
receipt, source cleanup or session-completion authorization.

Exact retry uses unchanged IDs, recorded_at, bytes and Windows account before
movement. After movement use startup recovery. Conflicts preserve evidence.
Host output version 1.2.0 is distinct from this input envelope version 1.0.0.
Exit 0: Admitted output with transaction_id, state, revision, was_already_present;
2: CLI usage; 3: request/repository failure; 7: cooperating host guard held.
No raw exception, request bytes, subject names or absolute paths in output.

Admission is synchronous for one package. Ctrl+C is deferred until that admission
finishes and the host exits normally, preserving its actual result; there is no
mid-admission cancellation guarantee. Abrupt termination is not qualified here.

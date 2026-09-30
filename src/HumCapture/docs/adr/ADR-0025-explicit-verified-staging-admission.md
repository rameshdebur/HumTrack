# ADR-0025 — Explicit verified-staging admission

**Status:** Accepted for user-authorized C12 engineering scope; independent review pending
**Date:** 2026-09-12

Expose the existing C2 admission API through admit-staged --root ABSOLUTE_ROOT
--request ABSOLUTE_JSON. It requires an already-collected staging package and
an existing verifier record. This does not implement collection, media decode,
or trusted verifier production. It does not manufacture PASS evidence.

Request envelope version 1.0.0 has exactly schema_version and registration.
Registration uses snake_case names of StagedVerifiedPackageRegistration except
actor_windows_account, which is prohibited and supplied from the current
Windows identity. recorded_at is required, UTC and stable for exact retry.
verification_record_utf8 is base64 of the exact existing verifier-record bytes,
preserving its hash; it is not a newly serialized verifier object.

Reject unknown/duplicate fields, unsupported envelope versions, missing required
fields, malformed base64, unsafe request-file links, nonabsolute paths and
requests above 1 MiB. Request JSON is separate from the immutable package.
The existing admission validator checks package identity, artifact set, lengths,
hashes and supplied verification binding. It does not rerun media/full-decode
checks merely because their supplied results say PASS.

Reuse the repository and cooperating-host guards. Publish verification evidence
and journal admission through the existing C2 implementation; stop at
STAGED_VERIFIED. Preserve source/request bytes and existing evidence. Stable
request IDs/time and the same Windows account enable exact retry before movement;
after movement use startup recovery, not readmission.

Host output advances additively to 1.2.0. Admission output contains version,
Admitted status, transaction_id, state, revision and was_already_present.
Existing command fields/exit codes remain unchanged. Exit 2 is CLI usage error,
3 request/repository failure, 7 guard contention, 0 successful admission.
Do not infer capture/session completion, receipts or cleanup eligibility.

The request contract is documented in HOST_ADMISSION_CONTRACT.md. No new
repository schema, dependency, data-root initialization or deployment surface.
Extends ADR-0023/0024; independent verifier, field and regulatory review remain.

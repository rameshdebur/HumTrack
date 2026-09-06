# HumCapture Transfer and Package Contract

**Contract ID:** HC-IF-XFR-001  
**Version:** 1.1.0  
**Status:** Accepted engineering baseline  
**Date:** 2026-09-05

## 1. Boundary and authority

This contract defines immutable finalized packages, coordinator collection,
restart checkpoints, common verification, commit linkage, and cleanup
eligibility. It does not implement application services.

The coordinator owns collection scheduling and is the HTTPS client. Android is
the finalized-package HTTPS server. USB/MTP is a manual conveyance path for the
same bytes. A UVC worker materializes the same package locally. Transfer is
always subordinate to scientific master acquisition.

## 2. Package publication

The package is a directory containing regular files and exactly one canonical
`package-manifest.json`. A source may expose only `FINALIZED_COMPLETE` or
`FINALIZED_INCOMPLETE` packages. It must close every artifact, determine its
length and SHA-256, construct the manifest, and atomically publish the manifest
last. Publication makes the artifact set immutable.

The manifest itself is excluded from `artifacts` to avoid recursive hashing.
Symbolic links, hard links, reparse points, absolute paths, traversal, Windows
device aliases, non-NFC paths, case-insensitive collisions, and trailing dot or
space segments fail `PATH_SAFETY`/`REGULAR_FILES` verification.

A complete Android package requires master video, frame timestamps, camera
metadata, capture events, finalization, IMU samples, and IMU metadata. Complete
UVC packages require the same non-IMU core and do not acquire a hidden IMU
requirement. An incomplete package records an explicit finalization reason and
must preserve at least capture events and the finalization record; missing or
damaged artifacts remain visible rather than preventing survivor collection.

Only pseudonymized `subject_id` is present. Subject name, date of birth, and
operator account are coordinator-local and must not appear in this source
package.

## 3. Identity and hashing

`package_content_sha256` is the lowercase SHA-256 of RFC 8785 canonical JSON
after removing only `package_content_sha256`. `artifact_set_sha256` is the
lowercase SHA-256 of UTF-8 inventory lines, sorted lexically as complete lines:

```text
relative_path TAB byte_length TAB sha256 LF
```

`package_byte_length` is the exact unsigned-64 sum of artifact lengths. JSON
wire values that can occupy the full unsigned-64 range are canonical decimal
strings. Each artifact repeats session, trial, source, and capture-attempt
identity to prevent cross-package substitution. Scientific master video, frame
timestamps, and IMU samples include explicit timing coverage.

## 4. HTTPS collection

`openapi/transfer-v1.openapi.json` is normative OpenAPI 3.1.2. The coordinator:

1. retrieves and validates the manifest;
2. creates or reconciles a staging checkpoint bound to package ID and content
   hash;
3. addresses artifacts by UUID, never by relative filesystem path;
4. obtains the strong ETag and full-representation digest;
5. requests missing bytes with `Range: bytes=<offset>-` and the stored strong
   ETag in `If-Range`;
6. accepts `206` for append only when the strong validator is identical and the
   `Content-Range` starts at the requested offset;
7. on `200` after a resume request, discards the old partial before writing the
   complete representation;
8. on changed validator, `416`, manifest mismatch, or invalid digest,
   reconciles and restarts that artifact without combining representations.

`Content-Digest` covers bytes carried in the HTTP message. `Repr-Digest` covers
the entire selected artifact representation. The manifest SHA-256 remains the
authoritative post-storage identity. Exact chunk size and concurrency are
implementation policy and may not back-pressure capture.

The OpenAPI global security requirement binds every operation to
`HC-IF-SEC-001` mutual TLS. Authentication also requires exact peer role,
identity, trust revision, certificate validity/revocation, TLS profile, and
package ownership authorization. `401`, `403`, and `429` are explicit generic
failure surfaces; no operation permits anonymous or plaintext access.

## 5. Checkpoints and restart

Checkpoints reside only in coordinator staging. They bind every manifest
artifact, expected length/hash, collection method, received ranges, staged byte
count, attempts, restarts, and verification identity. Ranges are sorted,
non-overlapping, non-empty, and bounded by the artifact length. `STAGED` and
`VERIFIED` require full coverage; `VERIFIED` also requires a verification ID.

For HTTPS, each entry persists the manifest-derived strong ETag. For USB/MTP
and coordinator-local collection, no HTTP validator is used: only a completely
received and verified artifact can be skipped, and an incomplete artifact
restarts from zero. Switching collection method creates a new custody collection
attempt rather than silently changing an existing one.

## 6. Common verification and commit

All collection methods use the same verifier and
`package-verification-record.schema.json`. A `VERIFIED` aggregate requires:

- manifest schema, package identity, path/regular-file safety, exact artifact
  set, length and SHA-256 checks (including IMU samples/metadata for Android;
  UVC does not acquire a hidden IMU requirement);
- master structure and complete decode;
- timing/events and metadata/profile checks;
- finalization checks; and
- a passing result for every required artifact.

Missing, failed, or not-assessed required evidence prevents `VERIFIED`. A
package ID already bound to the same content hash is idempotent. The same ID
with different content is quarantined. Partial or unverified data remains in
staging and cannot become the subject repository destination.

Only `VERIFIED` may enter repository commit. The custody record binds the
verification record and commit identity. A receipt binds the committed package
and artifact-set hashes and is issued only after durable transactional commit.
Receipt signing and exact transaction mechanics remain deferred.

## 7. Cleanup and degraded operation

Network interruption changes collection state, not capture finalization or
scientific data status. The trained operator may use USB/MTP fallback. Android
files remain protected until receipt acknowledgement. Cleanup then requires an
explicit informed action that distinguishes confirmed transferred packages from
incomplete/unconfirmed packages. If programmatic deletion is unavailable,
Android presents the same status for informed manual cleanup.

## 8. Compatibility and errors

Record schema version remains `1.0.0`; API version is `1.1.0` for the additive
security binding. Unknown required semantics, unsupported major
versions, and identity conflicts fail safely. Historical finalized packages are
not rewritten. HTTP errors use `application/problem+json` with stable `code`
and `retry_class`; machine behavior does not depend on free text.

## 9. Deferred decisions

Later controlled work: range sizing, parallelism, receipt signing/offline
acknowledgement, repository transaction implementation, retention/access
controls, and runtime/hostile-network/HIL/field validation.

## 10. Normative technical references

- OpenAPI Specification 3.1.2: https://spec.openapis.org/oas/v3.1.2.html
- RFC 9110 HTTP Semantics: https://www.rfc-editor.org/rfc/rfc9110.html
- RFC 9530 Digest Fields: https://www.rfc-editor.org/rfc/rfc9530.html
- RFC 9852, New Protocols Using TLS Must Require TLS 1.3:
  https://www.rfc-editor.org/rfc/rfc9852.html
- RFC 8785 JSON Canonicalization Scheme: https://www.rfc-editor.org/rfc/rfc8785.html
- RFC 6234 SHA algorithms: https://www.rfc-editor.org/rfc/rfc6234.html

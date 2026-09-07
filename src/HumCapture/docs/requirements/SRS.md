# HumCapture Software Requirements Baseline

**Document ID:** HC-SRS-001  
**Version:** 0.5  
**Status:** Preliminary; control, source-wire, transfer/package, and pairing/transport-security requirements are baselined; remaining interfaces are open

This document establishes requirement families and mandatory system constraints. Detailed atomic requirements and verification IDs are completed before each implementation phase.

## Requirement families

| Prefix | Domain |
|---|---|
| HC-SYS-REQ | System boundary and orchestration |
| HC-AND-REQ | Android capture node |
| HC-COORD-REQ | Windows coordinator |
| HC-UVC-REQ | UVC worker |
| HC-DATA-REQ | Package, repository, backup, export |
| HC-TIME-REQ | Timing, synchronization, provenance |
| HC-SEC-REQ | Security, privacy, identity, audit |
| HC-USE-REQ | Operator workflow and accessibility |
| HC-COMPAT-REQ | Platform/schema backward compatibility |
| HC-REG-REQ | India/CDSCO and applicable standards controls |

## Baselined constraints

- **HC-SYS-REQ-001:** HumCapture SHALL modify only `src/HumCapture` without explicit user authorization.
- **HC-SYS-REQ-002:** Preview, networking, UI, transfer, and inference SHALL NOT back-pressure scientific master acquisition.
- **HC-SYS-REQ-003:** The selected protocol SHALL determine required/flexible source count and trial completion.
- **HC-SYS-REQ-004:** The system SHALL support Android and UVC source abstractions for the defined MVP scenarios.
- **HC-SYS-REQ-005:** UI state SHALL NOT be the sole authoritative source of capture state.

- **HC-AND-REQ-001:** Android SHALL record the scientific master locally with per-frame timing and required metadata.
- **HC-AND-REQ-002:** Android SHALL continue master acquisition through preview, network, or coordinator loss until scheduled/max/local stop or unrecoverable recorder failure.
- **HC-AND-REQ-003:** Android SHALL record available orientation/gravity/gyro/acceleration with native timestamps and provenance.
- **HC-AND-REQ-004:** Android SHALL expose only finalized packages for transfer/recovery.
- **HC-AND-REQ-005:** Android SHALL delete no capture silently and SHALL distinguish safe, incomplete, and unknown states.
- **HC-AND-REQ-006:** Android SHALL durably match the exact coordinator receipt
  before acknowledging it and SHALL recheck that receipt, package identity, and
  inactive operation state immediately before any programmatic deletion.

- **HC-COORD-REQ-001:** The coordinator SHALL use the signed-in Windows account as the MVP operator identity.
- **HC-COORD-REQ-002:** The coordinator SHALL guide subject/protocol/session/trial/source/readiness/capture/recovery/quality workflows without command-line prerequisites.
- **HC-COORD-REQ-003:** The coordinator SHALL reconcile persisted, filesystem, device, transfer, and worker state after restart.
- **HC-COORD-REQ-004:** The coordinator SHALL distinguish master health, preview health, transfer state, completion, and quality.
- **HC-COORD-REQ-005:** Planning a session SHALL create a versioned,
  content-hashed, session-owned snapshot of the selected approved protocol,
  including source-count selection, source roles, trial slots, duration,
  override, retake, and completion rules.
- **HC-COORD-REQ-006:** Before any scientific-master sample exists, a plan
  change SHALL create an audited new snapshot revision that supersedes rather
  than overwrites the prior revision.
- **HC-COORD-REQ-007:** After any session trial accepts a scientific-master
  sample, the protocol snapshot identity and content SHALL NOT change; a
  material protocol change SHALL require incomplete closure and a new session.
- **HC-COORD-REQ-008:** The coordinator SHALL enforce the versioned session
  lifecycle and terminal/non-terminal outcomes defined by `HC-IF-CTRL-001`.
- **HC-COORD-REQ-009:** Each required trial slot SHALL be satisfied by exactly
  one accepted complete trial, while retaken, superseded, excluded, and
  additional trials remain immutable and traceable.
- **HC-COORD-REQ-010:** Session completion SHALL require all required slots and
  packages resolved, no active/unknown required work, completed quality review,
  explicit trained-operator finalization, and mutually consistent immutable
  completion and handoff records.
- **HC-COORD-REQ-011:** Reopening a complete session SHALL preserve prior
  completion/handoff records, require an attributed reason, return to completion
  review, and produce higher record revisions if completed again.
- **HC-COORD-REQ-012:** Network transfer failure alone SHALL NOT create terminal
  session failure; automatic and USB/MTP collection SHALL use the same package
  verification and commit predicates.
- **HC-COORD-REQ-013:** Source commands SHALL bind stable coordinator/session/
  trial/source/attempt identity and expected state; the same command ID with
  changed semantic content SHALL be rejected as a conflict.
- **HC-COORD-REQ-014:** A command acknowledgement SHALL report acceptance,
  rejection, or prior application without asserting the requested acquisition
  or repository outcome; authoritative events/records SHALL establish results.
- **HC-COORD-REQ-015:** Restart reconciliation SHALL consume the authority-owned
  source snapshot and SHALL NOT continue one capture attempt across a changed
  source boot or monotonic-clock epoch.
- **HC-COORD-REQ-016:** The coordinator SHALL treat durable package commit as
  completion authority, separately track receipt/cleanup status, and SHALL NOT
  reverse session completion solely because acknowledgement or cleanup fails.

- **HC-UVC-REQ-001:** Each active UVC source SHALL be isolated so worker failure does not terminate unrelated sources.
- **HC-UVC-REQ-002:** UVC timestamps SHALL identify sensor/device/host-sample/host-arrival provenance truthfully.
- **HC-UVC-REQ-003:** UVC packages SHALL follow the common finalization, verification, and commit contract.
- **HC-UVC-REQ-004:** For a UVC camera with a manufacturer-documented physical
  in-use indicator, qualification SHALL verify that the indicator corresponds
  to active fresh-frame acquisition and confirmed source release. A mismatch
  SHALL be reconciled, classified conditional, or rejected without silent
  waiver; the indicator SHALL NOT be used as capture timing evidence. A camera
  without an available/documented indicator SHALL be `NOT_APPLICABLE` and SHALL
  NOT be rejected solely for lacking one.
- **HC-UVC-REQ-005:** When and only when a selected protocol requires multiple
  concurrent UVC sources, readiness SHALL qualify the complete named
  camera/driver/USB-controller/profile combination and each finalized stream
  SHALL pass independent full-decode integrity. Failure of any required stream
  SHALL block, reconcile, or reject that combination without silent profile
  substitution or creating a universal multi-camera requirement.

- **HC-DATA-REQ-001:** Finalized packages SHALL contain versioned identity, manifest, required files, sizes, and SHA-256 hashes.
- **HC-DATA-REQ-002:** Verification SHALL precede transactional commit.
- **HC-DATA-REQ-003:** Identical reimport SHALL be idempotent; conflicting identity/hash SHALL quarantine without overwrite.
- **HC-DATA-REQ-004:** Workflow completion SHALL require all protocol-required packages/trials committed.
- **HC-DATA-REQ-005:** Default handoff SHALL be pseudonymized and SHALL preserve original authoritative data.
- **HC-DATA-REQ-006:** Session completion and handoff records SHALL mutually
  reference the same session, subject, protocol snapshot identity/content hash,
  and record revisions; handoff artifact paths SHALL be relative and SHALL NOT
  escape the controlled repository destination.
- **HC-DATA-REQ-007:** Package custody, commit receipt, and quality assessment
  records SHALL retain exact package/attempt/content identity; a receipt SHALL
  be created only after durable commit and a quality aggregate SHALL NOT hide
  blocking or unresolved dimensions.
- **HC-DATA-REQ-008:** A JSON record's `*_content_sha256` identity SHALL be the
  lowercase SHA-256 of its RFC 8785 canonical UTF-8 JSON with that record's own
  content-hash property omitted; referenced record hashes SHALL remain included.
- **HC-DATA-REQ-009:** Every finalized source package SHALL use the versioned
  `HC-IF-XFR-001` manifest and the same verifier for HTTPS, USB/MTP, and
  coordinator-local collection; the manifest SHALL be published only after all
  artifacts are closed, sized, and hashed.
- **HC-DATA-REQ-010:** HTTPS resume SHALL combine partial bytes only under an
  unchanged strong representation validator and matching byte range; a full
  response, changed validator, invalid digest, or unsatisfied range SHALL cause
  safe artifact reconciliation without appending incompatible bytes.
- **HC-DATA-REQ-011:** Partial collection SHALL remain outside the subject
  repository; only a complete passing verification followed by durable commit
  SHALL create a receipt.
- **HC-DATA-REQ-012:** USB/MTP recovery SHALL skip only completely verified
  artifacts and SHALL restart each incomplete artifact from byte zero.
- **HC-DATA-REQ-013:** One immutable revision-1 receipt SHALL bind the exact
  durable commit, coordinator, device, source/session/trial/attempt/package
  identities, package/artifact hashes, destination, schema version, and issue
  time; identical replay SHALL be idempotent and conflicting reuse SHALL enter
  recovery with source files retained.
- **HC-DATA-REQ-014:** Receipt acceptance SHALL mean the exact receipt is
  durably stored and matched on Android; it SHALL NOT mean deletion. Lost
  acknowledgement recovery SHALL query state and reuse the same receipt.
- **HC-DATA-REQ-015:** Programmatic cleanup SHALL require explicit trained-
  operator selection/confirmation plus exact durable receipt/acknowledgement
  evidence, and SHALL be blocked during capture, finalization, transfer, or
  recovery.
- **HC-DATA-REQ-016:** Cleanup SHALL record one truthful result per selected
  package; partial deletion SHALL list remaining artifacts and subsequent retry
  SHALL target only that reconciled remainder. USB/MTP-only completion SHALL
  support informed manual cleanup but SHALL NOT authorize automatic deletion.

- **HC-TIME-REQ-001:** Wall clock SHALL NOT be the primary scientific timing source.
- **HC-TIME-REQ-002:** Raw synchronization exchanges, RTT, fit, uncertainty, drift, and provenance SHALL be retained.
- **HC-TIME-REQ-003:** Future scheduled start SHALL be used for coordinated sources.
- **HC-TIME-REQ-004:** The product SHALL NOT claim hardware synchronization in the MVP.
- **HC-TIME-REQ-005:** Detected timing discontinuities SHALL remain visible in quality evidence.
- **HC-TIME-REQ-006:** JSON control records SHALL preserve native monotonic
  instants as a clock identity, canonical unsigned 64-bit decimal-string ticks,
  and explicit tick frequency; mapped session instants SHALL also identify the
  clock model and uncertainty.
- **HC-TIME-REQ-007:** UTC and host-arrival timestamps SHALL support audit and
  observation only and SHALL NOT replace or order scientific source timing.

- **HC-SEC-REQ-001:** Discovery SHALL NOT grant control; pairing/authentication SHALL establish trust.
- **HC-SEC-REQ-002:** Subject name/date of birth SHALL NOT appear in mDNS or Android source packages.
- **HC-SEC-REQ-003:** Control and transfer SHALL be authenticated; transfer SHALL be encrypted.
- **HC-SEC-REQ-004:** Secrets SHALL NOT be stored in ordinary logs or unprotected catalog fields.
- **HC-SEC-REQ-005:** Overrides, recovery, verification, export, cleanup, and deletion SHALL be auditable.
- **HC-SEC-REQ-006:** Every engineering snapshot and release SHALL have a
  versioned, machine-readable SBOM generated from resolved manifests and project
  dependencies, with explicit coverage/known unknowns, component identifiers,
  dependency relationships and validation evidence. A controlled release SHALL
  be blocked when its SBOM is missing, stale, invalid, or not hash-bound to the
  release record.
- **HC-SEC-REQ-007:** Android first pairing SHALL be locally attended and SHALL
  bind a single-use secret and nonce of at least 128 bits, an exact bootstrap
  SPKI SHA-256 fingerprint, stable peer identities, and a ten-minute-or-shorter
  window; mDNS, an endpoint, or a short numeric code alone SHALL NOT grant trust.
- **HC-SEC-REQ-008:** Android and coordinator private keys SHALL be generated and
  retained in platform-protected storage; Android key material SHALL be
  non-exportable and Windows material SHALL be protected for the current user,
  not machine scope.
- **HC-SEC-REQ-009:** Control WSS and transfer HTTPS SHALL require mutual X.509
  authentication plus active trust, peer-role/UUID, certificate, and target
  resource authorization under `HC-IF-SEC-001`.
- **HC-SEC-REQ-010:** TLS 1.3 SHALL be supported and preferred; the Windows 10
  TLS 1.2 profile SHALL require an explicit compatibility reason, ECDHE/ECDSA
  with AEAD, and no silent downgrade. TLS 1.1 or earlier, plaintext, anonymous
  suites, static RSA, CBC/RC4, and TLS 1.3 early data SHALL be rejected.
- **HC-SEC-REQ-011:** Pairing SHALL be single-use, fail closed after at most five
  failed proofs, apply increasing retry delay, and return non-enumerating errors.
- **HC-SEC-REQ-012:** Certificate rotation, revoke, unpair, credential loss, and
  re-enrollment SHALL be explicit, revisioned, and auditable; trust removal
  SHALL NOT occur during capture/finalization or delete preserved packages.
- **HC-SEC-REQ-013:** Security audit records SHALL exclude private keys,
  bootstrap secrets/proofs, subject identity/demographics, and master content.
- **HC-SEC-REQ-014:** Authentication/network failure SHALL preserve acquisition
  and permit later authenticated or USB/MTP collection without false completion.

- **HC-USE-REQ-001:** Normal capture SHALL be for a trained supervised operator.
- **HC-USE-REQ-002:** Blocking failures and allowed deviations SHALL be distinguishable and actionable.
- **HC-USE-REQ-003:** Destructive actions SHALL require explicit informed confirmation.
- **HC-USE-REQ-004:** Status SHALL NOT be conveyed by color alone and native accessibility behavior SHALL be supported.

- **HC-COMPAT-REQ-001:** Windows 11 x64 supported releases SHALL be primary; Windows 10 22H2 x64 SHALL be the technical floor with qualification/lifecycle warning.
- **HC-COMPAT-REQ-002:** Incompatible source/control/schema versions SHALL be detected before arming.
- **HC-COMPAT-REQ-003:** Newer unsupported package schemas SHALL be refused without modification.
- **HC-COMPAT-REQ-004:** Historical finalized packages SHALL NOT be silently migrated or rewritten.

- **HC-REG-REQ-001:** Current India/CDSCO applicability SHALL be checked before affected implementation and medically positioned release.
- **HC-REG-REQ-002:** Final CDSCO classification/legal interpretation SHALL require qualified Indian regulatory review.
- **HC-REG-REQ-003:** Claims SHALL NOT exceed linked verification and regulatory authorization.
- **HC-REG-REQ-004:** Applicable risk controls SHALL link to implementation and verification evidence.

## Verification status

HC-COORD-REQ-005–015, HC-DATA-REQ-006–008, HC-TIME-REQ-006/007, and
HC-SEC-REQ-007–014 have
executable schema/conformance verification in HC-CTRL-TEST-001–019 and
HC-SEC-TEST-001–012. This is
contract-source verification, not
application implementation, runtime integration, HIL, field, clinical,
regulatory, independent-review, or release evidence. All other implementation
statuses remain `Not implemented / Not verified` unless separately recorded.

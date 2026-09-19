# ADR-0007 — Controlled Evidence Vault and Release Identity

**Status:** Accepted  
**Date:** 2026-08-31

## Context

Phase 0 hardware probes produce media, measurements, logs, and result records
that are too large and too sensitive to treat as ordinary source files. Some
early evidence was held under the Windows temporary directory and could be
removed by routine cleanup. HumCapture is also currently an untracked subtree
of the parent repository, so the parent commit alone cannot identify the source
or executable used for a test.

Verification evidence intended for design history, audit, or a future CDSCO
submission must remain attributable to a test, configuration, operator, source
and executable identity, disposition, review state, and immutable artifact set.
It must not silently turn an exploratory run into qualification evidence.

## Decision drivers

- Preserve raw proof outside temporary/cache storage.
- Keep large media and subject data out of Git.
- Detect alteration, truncation, replacement, path escape, and conflicting
  reuse of a run identity.
- Support the MVP's signed-in Windows-account operating model.
- Keep Phase 0 evidence independent of future coordinator persistence.
- Distinguish an engineering snapshot from a controlled released build.
- Permit later migration to a qualified document/eQMS or archival system.

## Decision

HumCapture will use a configurable, local controlled evidence vault outside the
source checkout. The MVP default is `HumCapture Evidence Vault` directly under
the signed-in operator's local profile root, not a redirected Documents folder,
cloud-synchronized location, `%TEMP%`, or an application cache.

Evidence is imported by copying into a vault-local staging directory, hashing
every physical regular file, writing a versioned receipt, and renaming the
staged directory into its final content-derived identity. Existing evidence is
never overwritten. Reimporting the same run and exact content is idempotent;
reusing a source run identity with different content is rejected. Verification
recomputes hashes and reports any missing, additional, or changed file.

The vault records the Windows account, import time, test case, source run,
disposition, data classification, review state, release identity, file inventory,
and limitations. Imported artifacts are made read-only as a deterrent. SHA-256
provides tamper evidence, not prevention against a privileged or malicious user.

Every executable evidence campaign must reference a versioned release record.
A `CONTROLLED_RELEASE` requires a clean committed HumCapture tree. Until the
subsystem is placed under version control and a clean release is deliberately
created, tooling emits only `ENGINEERING_SNAPSHOT` records with
`regulatory_use_permitted: false`. A parent commit that does not track
HumCapture is not a valid HumCapture release identity.

Source-control contains the decision, policy, schemas, tool, tests, traceability,
and compact evidence references. Raw media and subject data remain in the vault.
The vault is not a substitute for backup, access control, digital signatures,
independent approval, or a validated electronic QMS.

## Alternatives considered

### Continue using temporary folders

Rejected. Temporary storage has no retention promise and routine cleanup can
destroy the primary proof while leaving a report that can no longer be verified.

### Commit raw evidence to Git

Rejected. Large media, device logs, and possible personal data are unsuitable
for the source repository and would create cloning, retention, and privacy risks.

### Require an external eQMS/DMS now

Deferred. It may be appropriate before regulatory release, but selecting and
validating a product is disproportionate to Phase 0. The versioned receipt and
exportable inventory provide a migration boundary.

### Local controlled vault plus compact Git records

Accepted. It addresses present retention and provenance failures with a bounded,
testable implementation while preserving a future archival integration path.

## Consequences

- Evidence in `%TEMP%` is non-authoritative until imported successfully.
- A report without a verifiable controlled receipt is documentary narrative,
  not retained primary proof.
- Vault backup, retention periods, access review, independent approval, and
  qualified regulatory review remain explicit open controls.
- A clean Git commit is necessary but not sufficient for a controlled release.
- Any future signing, WORM, eQMS, or coordinator repository integration must
  preserve receipt and release-record compatibility or receive a new ADR.

## Affected components and interfaces

- `tools/evidence-control/` — isolated Phase 0 vault and release tooling.
- `docs/governance/EVIDENCE_CONTROL_POLICY.md` — operating policy.
- `docs/verification/` — test reports, traceability, and compact references.
- Future coordinator repository — must consume or supersede this boundary
  explicitly; no hidden dependency is created.

## Supersedes / Superseded by

None.

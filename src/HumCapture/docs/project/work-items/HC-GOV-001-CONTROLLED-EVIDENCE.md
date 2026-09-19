# HC-GOV-001 — Controlled Evidence and Release Identity

**Tags:** GOV.1 | EVIDENCE | RETENTION | RELEASE-IDENTITY | TRACEABILITY | CDSCO-READINESS

## Goal

Prevent Phase 0 raw proof from being lost in temporary storage and bind every
future retained run to an explicit source/build identity without claiming
regulatory conformity.

## Owners and reviews

- Primary owner: verification infrastructure / QA.
- Affected owners: system architect, UVC owner, release owner, regulatory/risk reviewer.
- Interfaces: evidence receipt 1.0.0 and SBOM-aware release record 1.1.0.
- Architecture review: completed through accepted ADR-0007.
- Independent QA and qualified regulatory review: open.

## Acceptance criteria

- [x] Evidence vault is outside Git, temporary storage, cache, and redirected cloud folders.
- [x] Versioned executable schemas exist for release records and evidence receipts.
- [x] Import is staging-based, complete, content-addressed, and non-overwriting.
- [x] Exact duplicate import is idempotent and conflicting run content is rejected.
- [x] Artifact, receipt, and release-record tampering is detectable.
- [x] Untracked/dirty source cannot be classified as a controlled release.
- [x] Surviving P0.2J raw evidence is imported and verifies.
- [ ] HumCapture source is committed and a clean controlled build is approved.
- [ ] Backup/restore, retention schedule, access audit, and independent review are approved and tested.
- [ ] Appropriate signing/WORM/eQMS controls are selected if required by the regulatory route.

## Current result

Twelve automated evidence-control tests pass. P0.2J source run
`1858C775-7907-43B3-9A3C-59104285E826` is retained as
`HC-EV-5becff40f4271131c8b43641`, disposition `INCONCLUSIVE`, review state
`DRAFT`, under engineering snapshot `HC-ENG-20260831T163246Z-7d17f5afce36`.
The snapshot and retained run include the officially validated CycloneDX 1.7
SBOM hash and a controlled SBOM copy.

This closes the immediate temporary-retention failure for new evidence. It does
not close release, QMS, backup, independent-review, or CDSCO readiness gaps.

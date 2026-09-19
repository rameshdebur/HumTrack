# HC-I0.4B-A — Repository transaction architecture decision

**Tags:** I0.4B-A | REPOSITORY | SQLITE | FILESYSTEM | TRANSACTION | DURABILITY | RECOVERY | CONTRACT

## Goal

Lock the authority and recovery model for a durable HumCapture repository
commit before specifying the exact folder layout, schemas, compatibility rules,
or production implementation.

## Classification and ownership

- Change: architectural, cross-component persistence and data-integrity decision.
- Primary owner: System Architect / repository owner.
- Affected owners: Coordinator, transfer/verifier, Android cleanup, simulator/QA,
  backup/export, risk/regulatory and release.
- Verification owner: engineering QA for the later executable contract;
  independent human review remains pending.
- Allowed paths: `src/HumCapture` documentation only for this decision.
- Application implementation: not authorized.

## Approved decisions

- [x] Coordinator-owned configurable data root outside the source checkout.
- [x] SQLite operational catalog plus immutable filesystem packages.
- [x] Required Coordinator-local subject name; UUID is the authoritative folder key.
- [x] Staging and quarantine remain outside the committed subject hierarchy.
- [x] Commit staging and destination share one filesystem volume.
- [x] Recoverable journal coordinates filesystem, catalog and verification state.
- [x] `COMMITTED` requires reconciled agreement; receipt creation follows it.
- [x] Identical content is idempotent; conflicting identity quarantines without overwrite.
- [x] Repository failure affects custody/commit, not source capture/finalization facts.

## Deferred boundaries

I0.4B-B will define the exact repository paths, catalog/journal schemas,
compatibility/version behavior and executable crash-point fixtures. Durable
write APIs, SQLite configuration, runtime repository service, backup/restore,
retention, HIL/power-loss/field evidence, independent review, qualified
regulatory review and controlled release remain open.

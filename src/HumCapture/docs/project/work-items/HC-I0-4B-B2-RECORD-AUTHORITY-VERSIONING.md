# HC-I0.4B-B2 — Record authority, descriptor and versioning

**Tags:** I0.4B-B2 | REPOSITORY | SQLITE | RECORDS | DESCRIPTOR | VERSIONING | RECOVERY | CONTRACT

## Goal

Separate mutable Coordinator/catalog authority from immutable filesystem
evidence and define repository descriptor and compatibility behavior without
duplicating current state or altering committed source packages.

## Classification and ownership

- Change: architectural, cross-component persistence, privacy, recovery and
  compatibility contract.
- Primary owner: System Architect / repository owner.
- Affected owners: Coordinator subject/protocol/session services,
  transfer/verifier, receipt/cleanup, quality/handoff, audit, backup/export,
  simulator/QA and risk/regulatory.
- Verification owner: engineering QA for B3 executable schemas and fault
  fixtures; independent human review remains pending.
- Allowed paths: `src/HumCapture` documentation only.
- Application implementation: not authorized.

## Approved decisions

- [x] SQLite is sole mutable current-state and subject-PII authority.
- [x] Mutable current state is not mirrored into replaceable JSON.
- [x] Scientific packages and immutable milestones are filesystem evidence.
- [x] Session `records/` contains seven fixed milestone record kinds.
- [x] Milestone files are UUID-named, versioned, content-hashed and immutable.
- [x] SQLite indexes immutable milestone UUID/revision/hash/path/bindings.
- [x] Disagreement enters recovery and blocks receipt/completion.
- [x] `repository.json` has a minimal non-PII, non-secret, relative descriptor.
- [x] Unsupported major/required-feature versions refuse mutation.
- [x] Repository open never silently migrates historical data.

## Deferred boundaries

B3 defines executable descriptor, record-index, transaction-journal and
reconciliation schemas, state transitions and crash-point fixtures. SQLite
DDL/configuration, runtime publication/recovery, backup/restore, retention,
HIL/power/field evidence, independent review, qualified regulatory review and
controlled release remain open.

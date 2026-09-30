# HC-I0.4B-B3A — Repository transaction state machine

**Tags:** I0.4B-B3A | REPOSITORY | JOURNAL | STATE-MACHINE | CRASH | RECOVERY | CONTRACT

## Goal

Assign one truthful internal state to every repository durability boundary so
restart recovery can resume, block, or quarantine without guessing and without
adding routine operator complexity.

## Classification and ownership

- Change: architectural, cross-component persistence and recovery contract.
- Primary owner: System Architect / repository owner.
- Affected owners: transfer/verifier, Coordinator/custody, receipt/cleanup,
  immutable records, audit, simulator/QA, backup/export and risk/regulatory.
- Verification owner: engineering QA for subsequent executable schemas and
  crash fixtures; independent human review remains pending.
- Allowed paths: `src/HumCapture` documentation only.
- Application implementation: not authorized.

## Approved decisions

- [x] Normal internal order is `STAGED_VERIFIED` → `COMMITTING` → `MOVED` →
  `CATALOGED` → `COMMITTED` without skipped evidence boundaries.
- [x] `RECOVERY_REQUIRED` and `QUARANTINED` are explicit non-success paths.
- [x] Only reconciled `COMMITTED` permits receipt or acquisition completion.
- [x] `COMMITTED` and `QUARANTINED` are terminal for one transaction identity.
- [x] Recovery classification uses directly observed durable evidence, not
  intended state, final-folder presence alone, nominal status, or timestamps.
- [x] Internal states map to existing package-custody states and remain a
  simplified Saving/Needs-attention/Quarantined/Completed operator experience.

## Deferred boundary

B3B defines exact journal and reconciliation fields plus automatic versus
operator-controlled recovery actions. B3C defines executable descriptor,
record-index and journal schemas with fixtures. SQLite/runtime implementation,
power-loss/HIL/field evidence, independent review, qualified regulatory review
and controlled release remain open.

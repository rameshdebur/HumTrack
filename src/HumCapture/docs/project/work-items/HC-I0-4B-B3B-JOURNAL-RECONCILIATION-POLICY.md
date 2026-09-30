# HC-I0.4B-B3B — Journal and reconciliation policy

**Tags:** I0.4B-B3B | REPOSITORY | SQLITE | JOURNAL | HISTORY | RECONCILIATION | RECOVERY | CONTRACT

## Goal

Define the durable journal field groups, append-only history, observations, and
bounded automatic/operator actions required to recover repository transactions
without guessing, force commit, or routine operator burden.

## Classification and ownership

- Change: architectural, cross-component persistence/recovery interface.
- Primary owner: System Architect / repository owner.
- Affected owners: transfer/verifier, Coordinator/custody, receipt/cleanup,
  immutable records, audit, backup/export, simulator/QA and risk/regulatory.
- Verification owner: engineering QA for B3C schemas/fixtures; independent
  human review remains pending.
- Allowed paths: `src/HumCapture` documentation only.
- Application implementation: not authorized.

## Approved decisions

- [x] One current transaction row supports queries.
- [x] Append-only transitions retain order, trigger, actor, reason, operation,
  and reconciliation linkage.
- [x] Reconciliation retains observed facts separately from result/action.
- [x] Transition sequence/revision is order authority; UTC is audit context.
- [x] Identity/hash/path bindings become immutable at transaction creation.
- [x] Six exact non-destructive actions may run automatically and are recorded.
- [x] Conflicts, unsafe/unsupported evidence and uncertain durability require
  one of four controlled operator actions.
- [x] No force commit, overwrite, unique-copy deletion, or immutable-record
  manufacture is permitted.
- [x] Journal/reconciliation excludes subject PII, secrets, raw media and
  absolute paths.

## Deferred boundary

B3C defines executable JSON Schemas, SQLite schema representation, conformance
fixtures, and crash-point tests. Runtime repository implementation,
backup/restore, retention, power-loss/HIL/field evidence, independent review,
qualified regulatory review and controlled release remain open.

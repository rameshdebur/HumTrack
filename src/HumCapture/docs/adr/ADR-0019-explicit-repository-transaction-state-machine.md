# ADR-0019 — Explicit repository transaction state machine

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0016 requires a recoverable journal because SQLite and the filesystem do
not share one atomic transaction. ADR-0017 fixes the repository paths and
ADR-0018 separates mutable catalog authority from immutable package and
milestone evidence. A restart must distinguish each durable boundary without
treating destination-folder presence, intended state, or a partially updated
index as proof of commit.

The existing package-custody interface exposes `VERIFIED`, `COMMITTING`,
`COMMITTED`, `COMMIT_RECOVERY_REQUIRED`, and `QUARANTINED`. Repository recovery
needs more detail internally, but the trained operator does not need every
storage boundary presented as a workflow step.

## Decision

HumCapture shall use this internal repository-transaction state machine:

```text
STAGED_VERIFIED -> COMMITTING -> MOVED -> CATALOGED -> COMMITTED
         |              |          |          |
         +--------------+----------+----------+-> RECOVERY_REQUIRED
         |              |          |          |
         +--------------+----------+----------+-> QUARANTINED
```

- `STAGED_VERIFIED`: the exact immutable package exists only at its canonical
  staging path and has a successful, content-bound common verification record.
  It is safe to retry but is not committed.
- `COMMITTING`: commit preconditions were revalidated and the exact transaction
  intent was durably journaled before crossing the repository boundary.
- `MOVED`: the same-volume atomic rename placed the exact package at its final
  canonical destination. Catalog completion is not implied.
- `CATALOGED`: one SQLite transaction durably wrote the required package,
  custody, verification, commit, milestone-index, and journal linkages.
  Post-write reconciliation is still required.
- `COMMITTED`: reconciliation proves the final package, immutable verification
  and commit records, SQLite linkages, and journal all agree. This is the only
  repository transaction state that permits receipt creation and can satisfy
  acquisition completion.
- `RECOVERY_REQUIRED`: evidence is missing, conflicting, ambiguous, unsafe, or
  of uncertain durability. Receipt, completion, handoff, and source cleanup are
  blocked until controlled reconciliation produces a new durable result.
- `QUARANTINED`: conflicting or unsafe material is preserved under a new
  quarantine-record UUID and excluded from normal commit/completion. It is a
  non-success disposition, not an alternate form of commit.

`COMMITTED` and `QUARANTINED` are terminal for a transaction. A correction,
replacement, or retry that cannot be idempotently resumed uses a new
transaction identity and retains the prior history.

The normal forward order cannot be skipped. Any non-terminal state may enter
`RECOVERY_REQUIRED`. Reconciliation may classify the transaction at the
nearest state directly proven by durable evidence, including `COMMITTED`, or
may quarantine it. It shall record the observed evidence and action; it shall
not infer success from a requested state, folder presence alone, nominal
workflow status, or host-arrival time.

Recovery classification does not skip a durability boundary: it acknowledges
work that the retained evidence proves completed before the interruption.

The internal states map to the existing package-custody contract as follows:

| Repository transaction | Package custody |
|---|---|
| `STAGED_VERIFIED` | `VERIFIED` |
| `COMMITTING`, `MOVED`, `CATALOGED` | `COMMITTING` |
| `COMMITTED` | `COMMITTED` |
| `RECOVERY_REQUIRED` | `COMMIT_RECOVERY_REQUIRED` |
| `QUARANTINED` | `QUARANTINED` |

The Coordinator UI may collapse normal intermediate states to **Saving** and
shall present `RECOVERY_REQUIRED` as **Needs attention**, with a specific safe
next action. Internal state detail remains available in diagnostics and audit.

## Restart and crash classification

- Before durable `COMMITTING`, a verified staging package remains
  `STAGED_VERIFIED`; no in-progress commit is invented.
- A durable intent with the exact package still in staging and no destination
  may be returned to `STAGED_VERIFIED` for an idempotent retry.
- An exact final package with no staging package can prove at least `MOVED`, but
  cannot prove `CATALOGED` or `COMMITTED` without their required evidence.
- SQLite catalog linkage and its `CATALOGED` journal update occur in one SQLite
  transaction, so restart observes both or neither.
- `CATALOGED` advances to `COMMITTED` only after full reconciliation and
  durable publication/indexing of required immutable milestone records.
- Both staging and destination, a content mismatch, unsafe path/link, duplicate
  conflicting identity, missing required immutable record, or uncertain flush
  enters `RECOVERY_REQUIRED` or `QUARANTINED`; it is never overwritten.

Exact journal fields, record schemas, authorized automatic versus operator
reconciliation actions, and crash fixtures are subsequent I0.4B-B3 work. This
decision does not authorize application implementation.

## Alternatives considered

- **Keep only `COMMITTING` and `COMMITTED`:** rejected because a restart could
  not distinguish whether rename or catalog publication completed.
- **Use final-folder presence as `COMMITTED`:** rejected because it cannot prove
  catalog, verification, immutable-record, custody, or journal agreement.
- **Expose every internal state as an operator step:** rejected because it adds
  trained-operator burden without improving scientific evidence.
- **Roll back every interrupted transaction:** rejected because rename may
  already have durably crossed the repository boundary and destructive rollback
  could lose the only verified package.
- **Automatically choose whichever authority looks newest:** rejected because
  timestamps and intended states cannot resolve content or identity conflict.

## Rationale

The states correspond to actual durability boundaries and permit deterministic,
idempotent restart classification. They retain the simple MVP operator workflow
while ensuring that only fully reconciled evidence reaches the accepted
completion and cleanup boundary.

## Consequences

- Journal and reconciliation schemas must encode the exact state and the
  evidence proving each transition.
- Crash-point tests are required before and after every durable boundary.
- Recovery may safely resume or finish an unambiguous transaction, but every
  reconciliation result is retained and conflicts remain operator-visible.
- Application UI remains simple; diagnostic detail does not become routine
  workflow complexity.
- Runtime, power-loss, HIL, field, independent, and regulatory verification
  remain separate evidence levels.

## Affected components and interfaces

Repository and SQLite journal, transfer/verifier, package custody,
receipt/cleanup, immutable milestone publication, Coordinator recovery UI,
audit, backup/export, simulator/QA, risk and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0004, ADR-0009, ADR-0012, ADR-0014, and ADR-0016–0018; supersedes
none.

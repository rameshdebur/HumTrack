# HumCapture Windows Coordinator

This subtree contains the dedicated Windows Coordinator application and its
headless services. It borrows HumTrack's Windows engineering principles but
does not reference HumTrack application internals.

## Repository core

`src/HumCapture.Coordinator.Repository` contains the I0.4B-C1 initialize/open,
I0.4B-C2 verified-staging journal, I0.4B-C3 commit-intent/atomic-move, and
I0.4B-C4 bounded startup-reconciliation slices. It:

- targets Windows 10 version 2004 (build 19041) or later on .NET 10;
- pins and lock-resolves `Microsoft.Data.Sqlite` 10.0.12;
- embeds the accepted `repository-v1.sql` contract as the catalog source;
- initializes only an empty absolute data root;
- publishes `repository.json` only after the catalog is created, checked and
  flushed, using a flushed same-directory temporary file and non-overwriting
  move;
- rejects reparse points and hard-linked descriptor/catalog files;
- enables mutation only when descriptor versions/features, SQLite integrity
  and catalog metadata agree exactly; and
- returns explicit read-only inspection for unsupported versions/features
  without opening the catalog or performing migration;
- rechecks an already-collected staged package against its manifest bytes,
  artifact inventory, canonical content hashes and successful immutable
  verification record before journal admission;
- publishes the exact verification record without overwrite; and
- atomically inserts the initial `STAGED_VERIFIED` transaction, sequence-1
  transition and verification-record index, with exact-replay idempotency and
  fail-closed identity conflict handling;
- revalidates journal history, immutable verification evidence, exact staged
  bytes, canonical paths, volume identity and repository availability before
  crossing the repository boundary;
- atomically records `COMMITTING` intent before moving package data; and
- uses a same-volume, non-overwriting Windows write-through directory move,
  revalidates the exact destination package, then atomically advances the
  journal to `MOVED`;
- observes journal, staging, destination, catalog linkage, immutable
  verification record and commit record separately at startup; and
- atomically retains the six observations plus `NO_ACTION`,
  `RETRY_FROM_STAGED`, or `RESUME_AFTER_MOVE`, with exact replay and contiguous
  recovery transitions.

The C2 API is an admission boundary for a package already collected by the
future transfer/common-verifier service. C3 moves that package only through
the internal `MOVED` durability boundary. Neither API collects or decodes
media. C4 automatically acts only on exact, safe, supported evidence within
`STAGED_VERIFIED`, `COMMITTING`, and `MOVED`; every other observation fails
closed for trained-operator handling without moving or deleting material.
`MOVED` is not cataloged or committed and cannot authorize completion, a
receipt or source cleanup. The component does not yet implement subject
records, transfer, catalog/commit reconciliation, operator/quarantine actions,
receipts, backup/restore, UI, or migration.

## Verification

Run the Coordinator repository self-tests directly:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/HumCapture.Coordinator.Repository.SelfTest.csproj --configuration Release
```

They are also part of `tools/evidence-control`'s default test command.

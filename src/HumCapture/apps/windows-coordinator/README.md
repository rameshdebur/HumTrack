# HumCapture Windows Coordinator

This subtree contains the dedicated Windows Coordinator application and its
headless services. It borrows HumTrack's Windows engineering principles but
does not reference HumTrack application internals.

## Repository core

`src/HumCapture.Coordinator.Repository` contains the I0.4B-C1 initialize/open
and I0.4B-C2 verified-staging journal slices. It:

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
  fail-closed identity conflict handling.

The C2 API is an admission boundary for a package already collected by the
future transfer/common-verifier service. It does not itself collect or decode
media. The component does not yet implement subject records, transfer,
`COMMITTING` or later package commit states, reconciliation, quarantine moves,
receipts, backup/restore, UI, or migration.

## Verification

Run the Coordinator repository self-tests directly:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/HumCapture.Coordinator.Repository.SelfTest.csproj --configuration Release
```

They are also part of `tools/evidence-control`'s default test command.

# HumCapture Windows Coordinator

This subtree contains the dedicated Windows Coordinator application and its
headless services. It borrows HumTrack's Windows engineering principles but
does not reference HumTrack application internals.

## Repository core

`src/HumCapture.Coordinator.Repository` is the I0.4B-C1 repository
initialize/open slice. It:

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
  without opening the catalog or performing migration.

The component does not yet implement subject records, transfer, package
commit, reconciliation, receipts, backup/restore, UI, or migration.

## Verification

Run the Coordinator repository self-tests directly:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest/HumCapture.Coordinator.Repository.SelfTest.csproj --configuration Release
```

They are also part of `tools/evidence-control`'s default test command.

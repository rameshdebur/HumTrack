# ADR-0022 — Microsoft.Data.Sqlite for the Coordinator repository

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0016–0021 define SQLite as HumCapture's mutable Coordinator catalog and
baseline its executable DDL without selecting a production .NET provider.
I0.4B-C1 must create and verify that catalog on the supported Windows 10/11
Coordinator while remaining headless, independently testable and isolated
from HumTrack internals.

The provider must execute the accepted SQLite `STRICT` DDL, support read-only
and read-write/create modes, permit explicit transactions and integrity
checks, ship a known SQLite implementation, work with the .NET 10 Coordinator,
and have a maintainable dependency/SBOM surface.

## Decision drivers

- Direct ADO.NET access without introducing an object-relational mapper.
- A consistent bundled SQLite runtime rather than an untracked host library.
- Microsoft-maintained integration with current .NET.
- Exact NuGet locking and transitive SBOM visibility.
- Windows 10 build 19041 and later compatibility.

## Options considered

1. **Microsoft.Data.Sqlite with its default SQLitePCLRaw bundle:** selected.
   It is Microsoft's lightweight ADO.NET SQLite provider and its default
   package brings the `bundle_e_sqlite3` native runtime.
2. **Microsoft.Data.Sqlite.Core with a custom provider/bundle:** rejected for
   MVP because HumCapture has no requirement for a system or custom SQLite
   build and would own more initialization/compatibility choices.
3. **Entity Framework Core SQLite:** rejected because the accepted DDL is the
   contract and an ORM/migration model would add an unnecessary second schema
   authority.
4. **System.Data.SQLite or another community wrapper:** not selected because
   the Microsoft provider is sufficient and aligns directly with the current
   .NET stack with a smaller decision surface.

## Decision

The Windows Coordinator repository shall use `Microsoft.Data.Sqlite` version
`10.0.12`, resolved through committed NuGet lock files. The application targets
`net10.0-windows10.0.19041.0`. The default bundled SQLitePCLRaw/e_sqlite3
runtime is retained; no host SQLite or Entity Framework dependency is used.

The accepted `docs/interfaces/sqlite/repository-v1.sql` file is embedded at
build time and remains the schema authority. Opening a repository never uses
provider or ORM migrations. Unsupported versions/features yield descriptor-
only read-only inspection, while an exact supported repository must pass
SQLite `quick_check` and descriptor/catalog metadata equality before mutation
is authorized.

## Consequences

### Positive

- Coordinator code uses a small ADO.NET surface and the already-approved DDL.
- SQLite runtime resolution is consistent across supported Windows hosts.
- Package and transitive native dependencies are pinned and inventoried.
- A later UI can consume the headless repository service without becoming a
  storage authority.

### Negative and risks

- The application distributes native e_sqlite3 binaries through
  SQLitePCLRaw; architecture/RID packaging must be verified before release.
- Monthly provider servicing requires explicit dependency review, lock update,
  SBOM regeneration and regression tests.
- Source/process-level flush and atomic-move tests do not prove power-loss
  durability; destructive fault/power testing remains a later gate.
- Provider acceptance does not confer CDSCO, clinical or medical-device
  compliance.

## References

- Microsoft Learn, `Microsoft.Data.Sqlite` overview:
  <https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/>
- Microsoft Learn, custom SQLite versions and default bundle:
  <https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions>
- NuGet, `Microsoft.Data.Sqlite` 10.0.12:
  <https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12>

## Related decisions

Implements the provider decision left open by ADR-0021 and refines
ADR-0016–0020; supersedes none.

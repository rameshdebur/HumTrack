# HC-I0.4B-B3C — Executable repository contracts

**Tags:** I0.4B-B3C | REPOSITORY | JSON SCHEMA | SQLITE | DDL | RECONCILIATION | FAULT FIXTURES | CONTRACT

## Goal

Convert the accepted HC-IF-REP-001 version 1.3.0 repository policy into
machine-testable record, SQLite, lifecycle, reconciliation, and receipt gates
without starting application repository implementation.

## Classification and ownership

- Change: cross-component interface and verification infrastructure.
- Primary owner: System Architect / repository owner.
- Affected owners: Coordinator, transfer/verifier, receipt/cleanup, immutable
  records, audit/recovery UI, backup/export, QA, risk and release.
- Verification owner: engineering QA; independent human review remains pending.
- Allowed paths: `src/HumCapture` only.
- Dependency/SBOM impact: none; pinned Node 22.12 built-in experimental SQLite
  adapter is used only to execute DDL in the test process.
- Application implementation: not authorized.

## Delivered source contract

- [x] Eight JSON Schema 2020-12 documents, including shared definitions.
- [x] Executable strict-table SQLite DDL with constraints and immutable/history
  triggers.
- [x] One complete lifecycle, eight crash/reconciliation cases and three named
  invalid fixtures.
- [x] HC-REP-TEST-001–015 for schema, compatibility, DDL, namespace,
  lifecycle, exhaustive transitions, immutable bindings, replay,
  observations, recovery actions, receipt gating, minimization and exact
  catalog/commit/index agreement.
- [x] No subject name/demographics, secrets, raw media or absolute path fields
  are admitted to repository journal/reconciliation records.

## Deferred boundary

Production .NET/SQLite provider and repository I/O, real filesystem safety and
atomicity, backup/restore, retention, runtime fault injection, power-loss/HIL,
field, independent and qualified regulatory evidence remain open.

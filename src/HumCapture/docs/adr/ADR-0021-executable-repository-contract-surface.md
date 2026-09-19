# ADR-0021 — Executable repository contract surface

**Status:** Accepted  
**Date:** 2026-09-10

## Context

ADR-0016–0020 establish repository authority, namespace, compatibility,
transaction states, journal history, and bounded reconciliation. Those prose
decisions are not sufficient to reject malformed records, skipped states,
unsafe paths, conflicting replay, incomplete observations, or an impermissible
automatic repair before application implementation begins.

Some rules apply to one serialized record and fit JSON Schema. Others depend on
ordered history or agreement among SQLite rows and immutable filesystem records
and cannot be expressed truthfully in a single JSON Schema document. The
SQLite representation must also be executable without adding a production
database library or prematurely implementing repository I/O.

## Decision

HC-IF-REP-001 version 1.3.0 is realized by three complementary source-level
contract surfaces:

1. JSON Schema 2020-12 validates repository descriptors, current transaction
   rows, append-only transitions/reconciliations, package catalog entries,
   immutable milestone indexes, and immutable commit records.
2. `repository-v1.sql` is executable SQLite DDL for the logical catalog,
   constraints, uniqueness, foreign keys, and immutable/append-only triggers.
3. HC-REP-TEST-001–015 validate cross-record identity, exact namespace
   templates, compatibility, complete authority observations, transition
   ordering, operation replay, bounded automatic/operator action predicates,
   crash-point fixtures, receipt eligibility, and data minimization.

The DDL is executed in an in-memory SQLite database during tests through the
pinned Node.js 22.12 experimental `node:sqlite` adapter. That adapter is test
infrastructure only and does not select or constrain the production .NET
SQLite provider.

The JSON schemas are closed to unknown fields. The conformance layer handles
semantic rules that are inherently relational. Passing these tests authorizes
neither application repository implementation nor a durability claim.

## Alternatives considered

- **Schemas only:** rejected because they cannot prove ordered history,
  operation replay identity, cross-record agreement, or automatic-action
  predicates.
- **DDL inspected as text:** rejected because syntactically invalid or
  non-enforcing SQL could appear complete.
- **Add a third-party Node SQLite package:** rejected because the pinned Node
  runtime can execute the DDL without expanding production dependencies/SBOM.
- **Build the production repository now:** rejected because B3C is the
  contract gate, not application implementation.
- **Encode all relationships in database triggers:** rejected for this slice
  because filesystem observations and immutable JSON hashes are external to
  SQLite; the application must still execute the accepted reconciliation
  algorithm and transaction boundary.

## Rationale

The split keeps each enforcement mechanism honest: schemas reject malformed
records, SQLite proves the catalog representation and local invariants are
executable, and conformance tests cover relationships across authorities. It
also keeps the MVP bounded and avoids coupling the future Windows Coordinator
to verification tooling.

## Consequences

- Production repository code must consume or implement the same schemas,
  transition matrix, identity bindings, and recovery predicates.
- Current-row update plus transition insertion remains one application-owned
  SQLite transaction and must receive runtime crash/fault verification.
- The experimental Node adapter warning is acceptable only in contract tests;
  production provider selection remains open.
- Backup/restore, real filesystem link/reparse inspection, atomic rename and
  flush behavior, power-loss, HIL, field, independent, and regulatory evidence
  remain open.

## Affected components and interfaces

HC-IF-REP-001, Windows Coordinator repository, transfer/common verifier,
receipt/cleanup, immutable records, audit/recovery UI, QA/fault injection,
backup/export, risk, and release evidence.

## Supersedes / Superseded by

Implements the executable contract gate established by ADR-0016–0020;
supersedes none.

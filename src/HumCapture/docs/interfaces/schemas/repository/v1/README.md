# HC-IF-REP-001 executable schemas — version 1

These JSON Schema 2020-12 documents define the source-level repository
descriptor, current transaction, append-only transition/reconciliation,
package-catalog, immutable-record index, and immutable commit-record contracts
for HC-IF-REP-001 version 1.3.0.

The schemas constrain individual serialized representations. Cross-record
identity, sequence, path-template, transition, observation-completeness,
idempotency, and automatic-action rules are additionally enforced by the
repository conformance suite. The SQLite DDL representation is
`docs/interfaces/sqlite/repository-v1.sql`.

This directory does not implement repository I/O or prove filesystem
durability, crash recovery, power-loss behavior, hardware behavior, field
workflow, or regulatory compliance.

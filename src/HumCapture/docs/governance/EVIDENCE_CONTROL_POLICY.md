# HumCapture Controlled Evidence Policy

**Policy ID:** HC-GOV-EVIDENCE-001  
**Revision:** 1.0  
**Effective date:** 2026-08-31  
**Status:** Active for Phase 0 engineering evidence

## Purpose and scope

This policy controls HumCapture Phase 0 test evidence from creation through
review and retention. It applies to raw media, measurements, logs, result files,
receipts, release records, and verification reports. It does not assert CDSCO,
ISO, IEC, clinical, or medical-device conformity.

## Roles

- **Test operator:** executes the approved test, records deviations, and imports
  the complete raw run without selective deletion.
- **Verification owner:** checks the receipt, hashes, expected/actual results,
  disposition, and traceability.
- **Independent reviewer:** approves or rejects the evidence independently of
  test execution when a gate requires independence.
- **Regulatory/risk reviewer:** confirms applicability, claims, and dossier use;
  this requires an appropriately qualified person for regulatory conclusions.
- **Release owner:** creates a clean, committed release identity and approves its
  intended verification use.

For the MVP, the signed-in Windows account identifies the acting operator. This
is attribution, not strong authentication or electronic signature.

## Required records

Every retained run shall have:

1. A unique source run ID and controlled evidence ID.
2. Test/report IDs and traceability to requirements and risks.
3. The exact configuration, procedure revision, expected and actual results.
4. Raw error values, deviations, limitations, and a disposition.
5. A release record identifying source and executed binaries.
6. A complete relative-path, byte-length, and SHA-256 inventory.
7. Operator/import timestamps and a review state.
8. Data classification and retention class.

`INCONCLUSIVE`, `FAIL`, and interrupted runs are retained when they inform risk,
recovery, or design decisions. They are never relabelled as `PASS`.

## Storage and handling

- The evidence vault must be outside the source checkout and outside temporary
  or cache directories. It must not use a cloud-redirected profile folder unless
  that transfer and its privacy/security controls are explicitly approved.
- Import uses vault-local staging and conflict-safe finalization. No existing
  controlled evidence may be overwritten.
- Evidence files become read-only after import. Hash verification is required
  before review, use at a gate, export, or restoration from backup.
- Source paths are provenance only; the controlled vault copy is authoritative.
- Subject names and direct identifiers are prohibited in Phase 0 hardware-probe
  evidence. Future subject evidence requires the approved privacy/data contract.
- Raw evidence is not committed to Git. Git may retain redacted summaries,
  receipts/inventory references, schemas, and test reports.

## Release identity rule

`ENGINEERING_SNAPSHOT` is the only permitted classification when HumCapture is
untracked, dirty, or not at an approved version. It must set
`regulatory_use_permitted` to `false`.

`CONTROLLED_RELEASE` requires, at minimum, a clean committed HumCapture tree,
an approved version, reproducible source inventory, executed-binary hashes,
passed release checks, release-owner approval, and the applicable independent
reviews. The current tool enforces only the clean/committed technical precondition;
project governance must enforce the remaining approvals.

## Review states and changes

Evidence enters as `DRAFT`. Review changes are separate signed/attributed review
records in a future increment; the raw receipt and artifacts are not edited.
Correction creates a new superseding record with a reason and retains the prior
record. Conversation text cannot approve or alter evidence by itself.

## Retention, backup, and export

Phase 0 uses retention class `PROJECT_LIFETIME_PENDING_POLICY`. No evidence may
be destroyed until an approved retention schedule, backup verification, legal/
regulatory hold process, and deletion authorization exist. The local vault is a
single-copy risk until backup and restore are verified.

An export must include the release record, receipt, full artifact inventory,
hashes, report, review records, and a successful verification result.

## Known control limitations

The MVP vault is hash-verifiable and conflict-safe, but it is not digitally
signed, access-controlled beyond the Windows account, independently time-stamped,
WORM-backed, backed up by this tool, or validated as an electronic QMS. Those
controls remain prerequisites for any future regulatory dossier claim.

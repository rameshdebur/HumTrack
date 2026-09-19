# HC-I0.3C — Receipt Acknowledgement and Operator Cleanup Contract

**Tags:** I0.3C | RECEIPT | ACKNOWLEDGEMENT | OPERATOR-CLEANUP | RECOVERY | CONTRACT

## Goal

Baseline the post-commit receipt, durable Android acknowledgement, lost-message
reconciliation, explicit cleanup, partial-delete recovery, and USB/MTP manual
fallback without turning cleanup into acquisition completion.

## Classification and ownership

- Change: cross-component interface, persistence/lifecycle architecture,
  trained-operator workflow, privacy, risk, and regulatory-adjacent contract.
- Primary owner: System Architect.
- Affected owners: Android, coordinator, transfer/repository, simulator/QA,
  security/privacy, regulatory/risk, and trained-operator workflow.
- Verification owner: QA/simulator owner after executable baseline.
- Paths: HumCapture documentation and `tools/evidence-control/` only.
- Application implementation: not authorized.

## Approved decisions

- [x] Durable coordinator commit completes acquisition; receipt/cleanup do not.
- [x] Online receipt uses existing mutual TLS without an MVP signature.
- [x] One immutable receipt binds exact commit/device/package/hash/destination.
- [x] Android durably matches and stores before acceptance; acknowledgement is
  cleanup eligibility, never proof of deletion.
- [x] Lost acknowledgement uses exact status query and identical replay.
- [x] Conflicts enter recovery and retain files; no receipt regeneration.
- [x] Operator explicitly selects eligible packages and confirms once.
- [x] Android rechecks durable evidence and blocks cleanup during active work.
- [x] Per-package outcomes distinguish deleted, partial, missing, and unsafe.
- [x] Partial retry targets only reconciled remaining files.
- [x] USB/MTP completion supports informed manual cleanup but no automatic
  Android deletion in MVP.
- [x] No background/scheduled cleanup; unpair never deletes; history persists.

## Executable scope

`HC-IF-RCP-001` version 1.0.0; HC-IF-CTRL-001 version 1.3.0; five new
acknowledgement/status/cleanup schemas plus the refined commit receipt;
semantic conformance helpers; HC-RCP-TEST-001–010.

## Deferred boundaries

Android/coordinator persistence and UI; repository transactions; deletion and
filesystem APIs; restart/power-loss; trusted offline receipt conveyance;
receipt signing; HIL/field/usability; independent review; controlled release;
qualified regulatory/clinical decisions.

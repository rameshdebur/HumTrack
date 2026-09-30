# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260911-008  
**Title:** Implement I0.4B-C3 durable commit intent and atomic package move  
**State:** Implementation remotely verified; independent review pending  
**Change owner:** Signed-in project owner / Coordinator repository owner  
**Component owner:** Software Engineer with Coordinator/repository assignment  
**Verification owner:** Engineering QA  
**Independent reviewer:** Pending  
**Created:** 2026-09-11  
**Last reviewed:** 2026-09-11

## Objective and boundary

- Advance one exactly verified package from `STAGED_VERIFIED` through durable
  `COMMITTING` intent and an atomic same-volume rename to `MOVED`.
- Preserve a recoverable observable state at every tested failure boundary.
- Modify only `src/HumCapture`.
- Exclude collection/media decode, startup reconciliation, quarantine,
  `CATALOGED`, `COMMITTED`, receipts, cleanup, backup/restore, UI and Android.

## Classification

- Local Coordinator persistence implementation affecting immutable package
  custody and recovery risk controls.
- Primary owner: Coordinator/repository software engineer.
- Affected owners: System Architect, transfer/verifier owner, QA, Release/SBOM
  owner and Regulatory/Risk reviewer.
- ADR-0016–0022 and HC-IF-REP-001 1.3.0 already authorize the exact model; no
  architecture, interface, schema, intended-use or regulatory-claim change.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017–038 | Durable intent/move subset implemented; reconciliation and final commit remain open |
| User stories | HC-US-XFR-021–034 | Repository-local package movement and retained recovery state |
| Risks/controls | HC-RISK-001/002/017/019/022/030/031 | Exact evidence, same-volume/no-overwrite move, atomic transition history, fail-closed recovery |
| Interfaces | HC-IF-XFR-001 1.2.0; HC-IF-REP-001 1.3.0 | Consumed without contract change |
| ADRs | ADR-0016–0022 | Direct implementation; no superseding decision |
| Claims | India/CDSCO baseline unchanged | No approval, certification, clinical or power-loss claim |

## AI and automation declaration

- Material AI/automation contribution: substantial implementation, testing and
  controlled-record drafting; accountable human and independent review remain
  required.
- No dependency, subject data, secret, raw media or external HumTrack
  dependency was added.

## Implementation and configuration identity

- Branch: `codex/humcapture-baseline` in the isolated HumCapture worktree.
- Implementation commit: `0b715d34d4b854eec7e4ebefd0a73e0d83e70f80`.
- Interface/schema change: none.
- SBOM: product version `0.1.0-i0.4b-c3`, 31 components/32 nodes, SHA-256
  `58161eca97eaa69c14ebfb47d66f7f814c63f241f4bca94b43beba35545f0dfd`.

## Verification plan and results

| Verification | Requirement/risk | Result |
|---|---|---|
| HC-REP-RUNTIME-032–034 | Normal intent/move, contiguous evidence and replay | 3/3 pass |
| HC-REP-RUNTIME-035–045 | Mutation, collision, rollback, interrupted-state, compatibility and history failures | 11/11 pass |
| C1/C2 regression HC-REP-RUNTIME-001–031 | Initialize/open and verified staging | 31/31 pass |
| HC-REP-TEST/HC-XFR and full contracts | Contract regression | 91/91 pass |
| Build/analyzers | Repository coding baseline | Release build: 0 warnings, 0 errors |
| Capability and camera probes | Unrelated regression | 55/55, managed 7/7, native 8/8 pass |
| Dependency/SBOM | Supply-chain regression | No findings; SBOM 6/6 plus project/official validation pass |
| Exact-source remote CI | Reproducible branch/PR verification | Push `34595856604` and PR `34595859162` pass at `0b715d34d4b854eec7e4ebefd0a73e0d83e70f80` |

Not verified: common-verifier media decode, live transfer integration,
cross-process writer exclusion, abrupt process/OS/power interruption,
reconciliation/quarantine, catalog/final commit, backup/restore, HIL, field,
independent review, qualified regulatory review or release packaging.

## Decision and approvals

- Current disposition: engineering implementation passed exact-commit CI and
  is ready for independent review; not a controlled release.
- Human change/verification approval, independent review, release ownership and
  Regulatory/Risk attribution remain pending.

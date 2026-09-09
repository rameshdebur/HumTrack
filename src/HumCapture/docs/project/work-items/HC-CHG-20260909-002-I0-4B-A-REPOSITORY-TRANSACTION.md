# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260909-002  
**Title:** Lock I0.4B-A repository transaction architecture  
**State:** Accepted architecture baseline; executable contract and independent review pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and repository owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-09  
**Last reviewed:** 2026-09-09

## Objective and boundary

- Objective: record the explicitly approved I0.4B-A repository authority,
  transaction, durability and recovery decision.
- Allowed paths: `src/HumCapture` documentation only.
- Excluded: exact repository paths/schemas; SQLite configuration; production
  repository, transfer, Coordinator or Android code; backup/retention;
  runtime/power/HIL/field work; certification, regulatory or release claims.
- Expected evidence: coherent architecture, requirement, story, risk,
  traceability and project-state records plus documentation regressions.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Data integrity/privacy
- [ ] Scientific timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

Architecture review is required and recorded by ADR-0016. Primary owner is the
System Architect/repository owner. Coordinator, transfer/verifier, Android
cleanup, QA, backup/export, risk/regulatory and release owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-COORD-REQ-003; HC-DATA-REQ-002/003/011/013/017–020 | Recoverable durable commit and reconciliation |
| User stories | HC-US-SUB-002/004; HC-US-XFR-006–013/021–023 | UUID identity, no overwrite, restart recovery and receipt ordering |
| Risks/controls | HC-RISK-001/002/007/010/022/030 | Prevent false commit, loss, overwrite and premature cleanup |
| Interfaces/schemas | HC-IF-XFR-001 1.2.0; HC-IF-RCP-001 1.0.0 | Existing verification/receipt boundaries retained; exact repository schema deferred |
| ADRs | ADR-0004/0009/0012/0014/0016 | SQLite/filesystem authority and recoverable journal |
| Compatibility | Exact window deferred to I0.4B-B | Historical finalized packages remain immutable |
| Claims/regulatory | India/CDSCO position unchanged | Engineering architecture evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted ADR-0016 and the linked requirements, stories, risk,
  traceability and state changes following the project owner's explicit lock.
- Accountable human owner: signed-in project owner; independent review pending.
- Sources/assumptions checked: accepted HumCapture architecture, transfer,
  custody, receipt, timing and project-state records.
- New dependency/licence/privacy/security impact: none; documentation only.
- Durable output is promoted into versioned repository artifacts, not
  conversation history.

## Configuration identity

- Source baseline/branch: `aa9e9808e9f3cf75a1f41466dd67d6b95d886553` on `codex/humcapture-baseline`.
- Decision commit: assigned when this record is committed; exact SHA and CI are
  recorded in the final verification update.
- Dependency/SBOM change: none; no dependency or build manifest changed.
- Interface/schema change: none; I0.4B-B remains the executable contract phase.
- Backward compatibility: historical packages are not rewritten or renamed.

## Verification plan and results

| Verification | Expected | Current result |
|---|---|---|
| Path boundary | Only `src/HumCapture` changed | Pass |
| ADR structure | Context, options, decision, consequences and related ADRs present | Pass |
| Traceability | Architecture, SRS, stories, risk and state agree | Pass |
| Markdown/diff integrity | No whitespace errors or generated artifacts | Pass |
| Existing automated suites | No regression | Pass locally; exact-SHA CI pending |

Evidence levels achieved:

- [x] Architecture/source documentation recorded
- [x] Static/document consistency checks passed
- [x] Existing automated regression verified locally
- [ ] Executable repository contract verified
- [ ] Runtime integration verified
- [ ] Power-loss/hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

## Decision and approvals

- Decision: I0.4B-A architecture accepted; I0.4B-B remains required before
  repository feature implementation.
- Permitted classification: engineering architecture baseline only.
- Change/verification owner: signed-in project owner, 2026-09-09.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

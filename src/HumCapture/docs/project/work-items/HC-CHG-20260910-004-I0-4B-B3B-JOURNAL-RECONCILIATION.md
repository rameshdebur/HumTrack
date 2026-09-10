# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-004  
**Title:** Baseline I0.4B-B3B journal and reconciliation policy  
**State:** Accepted architecture/interface baseline; executable schemas and independent review pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and repository owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-10  
**Last reviewed:** 2026-09-10

## Objective and boundary

- Objective: record the accepted journal rows/fields, observation contract and
  automatic versus operator recovery-action boundary.
- Allowed paths: `src/HumCapture` documentation only.
- Excluded: executable schemas/DDL, runtime recovery, backup/retention,
  HIL/field and regulatory claims.
- Expected evidence: coherent ADR/interface, requirements, stories, risk,
  traceability and project state plus existing automated regressions.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Recovery/completion safety
- [ ] Scientific timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

ADR-0020 records architecture review. Repository/Coordinator are primary;
transfer/verifier, custody, receipt/cleanup, immutable records, audit,
backup/export, QA, risk/regulatory and release owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017–038 | Durable ordered journal and bounded recovery |
| User stories | HC-US-XFR-021–034 | Exact automatic resume and informed conflict handling |
| Risks/controls | HC-RISK-001/002/007/010/011/020/022/030/031 | Prevent false repair, commit, overwrite and cleanup |
| Interfaces | HC-IF-REP-001 1.3.0; HC-IF-CTRL-001 1.3.0 custody mapping | Additive journal/action semantics; custody unchanged |
| ADRs | ADR-0009/0014/0016/0018–0020 | SQLite journal and recovery authority |
| Claims/regulatory | India/CDSCO position unchanged | Engineering architecture/interface evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted ADR-0020 and linked lifecycle records after explicit
  project-owner acceptance of the proposed journal/action model.
- Accountable human owner: signed-in project owner; independent review pending.
- Existing package, verification, custody and repository contracts were
  inspected to reuse canonical identities and avoid duplicate authority.
- Dependency/licence impact: none; documentation only.

## Configuration identity

- Source baseline/branch: `20f5fb5d5b672d66ed66c3fe95955a4d47cab30c` on `codex/humcapture-baseline`.
- Decision commit: assigned on commit and recorded in a verification update.
- Interface change: HC-IF-REP-001 advances additively from 1.2.0 to 1.3.0.
- Backward compatibility: repository paths, packages, milestones and external
  custody states are unchanged; executable compatibility remains B3C work.

## Verification disposition

- Architecture/interface/source documentation: recorded.
- Existing automated regression: passed locally — capability 55/55,
  evidence-control 76/76, SBOM 6/6, 73 tracked JSON documents parsed, SBOM
  graph valid at 20 components/21 dependency nodes with hash
  `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`,
  and both dependency audits reported zero findings.
- Executable journal/reconciliation behavior: deferred to B3C.
- Runtime, power-loss/HIL, field, independent and regulatory evidence: open.

## Decision and approvals

- Decision: I0.4B-B3B accepted; B3C remains required before repository feature
  implementation.
- Permitted classification: engineering architecture/interface baseline only.
- Change/verification owner: signed-in project owner, 2026-09-10.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

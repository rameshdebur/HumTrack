# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-003  
**Title:** Baseline I0.4B-B3A repository transaction states  
**State:** Accepted architecture/interface baseline; executable schemas and independent review pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and repository owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-10  
**Last reviewed:** 2026-09-10

## Objective and boundary

- Objective: record the accepted internal durability states, transition order,
  custody mapping, terminal states and restart classification constraints.
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

ADR-0019 records architecture review. Repository/Coordinator are primary;
transfer/verifier, custody, receipt/cleanup, immutable records, audit,
backup/export, QA, risk/regulatory and release owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017–033 | Explicit durability states and evidence-based recovery |
| User stories | HC-US-XFR-021–030 | Crash recovery, simple UI and safe receipt boundary |
| Risks/controls | HC-RISK-001/002/007/010/011/020/022/030/031 | Prevent false commit, overwrite and unsafe cleanup |
| Interfaces | HC-IF-REP-001 1.2.0; HC-IF-CTRL-001 1.3.0 custody mapping | Additive internal states; external custody unchanged |
| ADRs | ADR-0004/0009/0012/0014/0016–0019 | Recoverable filesystem/catalog transaction |
| Claims/regulatory | India/CDSCO position unchanged | Engineering architecture/interface evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted ADR-0019 and linked lifecycle records after explicit
  project-owner acceptance of the proposed state model.
- Accountable human owner: signed-in project owner; independent review pending.
- Existing custody and repository contracts were inspected to keep internal
  durability states separate from operator-facing workflow state.
- Dependency/licence impact: none; documentation only.

## Configuration identity

- Source baseline/branch: `eb714162d10c56d2a3659832fb83dde3cfe1a86a` on `codex/humcapture-baseline`.
- Decision commit: assigned on commit and recorded in a verification update.
- Interface change: HC-IF-REP-001 advances additively from 1.1.0 to 1.2.0.
- Backward compatibility: repository paths and external package-custody states
  are unchanged; executable compatibility behavior remains B3C work.

## Verification disposition

- Architecture/interface/source documentation: recorded.
- Existing automated regression: passed locally — capability 55/55,
  evidence-control 76/76, SBOM 6/6, 73 tracked JSON documents parsed, SBOM
  graph valid at 20 components/21 dependency nodes with hash
  `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`,
  and both dependency audits reported zero findings.
- Executable state/journal/reconciliation behavior: deferred to B3B/B3C.
- Runtime, power-loss/HIL, field, independent and regulatory evidence: open.

## Decision and approvals

- Decision: I0.4B-B3A accepted; B3B/B3C remain required before repository
  feature implementation.
- Permitted classification: engineering architecture/interface baseline only.
- Change/verification owner: signed-in project owner, 2026-09-10.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

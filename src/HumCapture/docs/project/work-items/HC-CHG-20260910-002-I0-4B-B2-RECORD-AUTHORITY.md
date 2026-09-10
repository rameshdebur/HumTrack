# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-002  
**Title:** Baseline I0.4B-B2 record authority and versioning  
**State:** Accepted architecture/interface baseline; executable schemas and independent review pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and repository owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-10  
**Last reviewed:** 2026-09-10

## Objective and boundary

- Objective: record the accepted split between SQLite operational authority and
  immutable filesystem evidence, plus descriptor/version behavior.
- Allowed paths: `src/HumCapture` documentation only.
- Excluded: executable schemas/DDL, runtime publication/recovery,
  backup/retention, HIL/field and regulatory claims.
- Expected evidence: coherent ADR/interface, requirements, stories, risk,
  traceability and project state plus existing automated regressions.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Privacy/compatibility/recovery
- [ ] Scientific timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

ADR-0018 records architecture review. Repository/Coordinator are primary;
subject/protocol/session, transfer/verifier, receipt/cleanup, quality/handoff,
audit, backup/export, QA, risk/regulatory and release owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017–029; HC-COMPAT-REQ-002–004 | Split authority, immutable records and fail-closed version behavior |
| User stories | HC-US-SUB-002–006; HC-US-SES-001/009; HC-US-XFR-021–027 | PII/current state, milestone evidence and recovery |
| Risks/controls | HC-RISK-001/002/007/010/011/020/022/030/031 | Prevent split authority, false reconstruction and unsafe migration |
| Interfaces | HC-IF-REP-001 1.1.0; existing immutable control/transfer records | Additive record/descriptor/compatibility semantics |
| ADRs | ADR-0004/0009/0010/0014/0016–0018 | Operational versus immutable authority |
| Claims/regulatory | India/CDSCO position unchanged | Engineering architecture/interface evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted ADR-0018, the additive interface and linked lifecycle
  records after explicit project-owner acceptance.
- Accountable human owner: signed-in project owner; independent review pending.
- Existing schemas/contracts were inspected to reuse immutable record identity
  and avoid a competing mutable JSON database.
- Dependency/licence impact: none; documentation only.

## Configuration identity

- Source baseline/branch: `a27fab999670ac9652fc8256ac3fe1bb6d2b3eb6` on `codex/humcapture-baseline`.
- Decision commit: `d9f656b05ed9ff4d88baf670293aa364d0281065`.
- Interface change: HC-IF-REP-001 advances additively from 1.0.0 to 1.1.0.
- Backward compatibility: 1.0.0 namespace remains valid; package and milestone
  content is not silently migrated.

## Verification disposition

- Architecture/interface/source documentation: recorded.
- Existing automated regression: passed locally — capability 55/55,
  evidence-control 76/76, SBOM 6/6, 73 tracked JSON documents parsed, SBOM
  graph valid at 20 components/21 dependency nodes with hash
  `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`,
  and both dependency audits reported zero findings.
- Exact decision-commit CI: HumCapture CI push run `34445863047` and PR run
  `34445866678` passed contracts/controls, SBOM policy/hash, runtime dependency
  audit, and managed/native camera-probe build/self-tests.
- Executable descriptor/catalog/journal behavior: deferred to B3.
- Runtime, power-loss/HIL, field, independent and regulatory evidence: open.

## Decision and approvals

- Decision: I0.4B-B2 accepted; B3 remains required before repository feature
  implementation.
- Permitted classification: engineering architecture/interface baseline only.
- Change/verification owner: signed-in project owner, 2026-09-10.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

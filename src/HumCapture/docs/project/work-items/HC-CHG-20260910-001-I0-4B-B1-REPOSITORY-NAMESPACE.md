# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-001  
**Title:** Baseline I0.4B-B1 repository namespace  
**State:** Accepted namespace baseline; executable schemas and independent review pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and repository owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-10  
**Last reviewed:** 2026-09-10

## Objective and boundary

- Objective: record the explicitly approved shallow UUID repository namespace
  and exact-package-envelope rule.
- Allowed paths: `src/HumCapture` documentation only.
- Excluded: application code, executable schemas, database/journal design,
  compatibility window, backup/retention, HIL/field and regulatory claims.
- Expected evidence: architecture/interface, requirement, story, risk,
  traceability and project-state consistency plus existing regressions.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Privacy/compatibility
- [ ] Scientific timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

ADR-0017 records architecture review. Repository is primary; Coordinator,
transfer/verifier, Android/UVC, recovery, backup/export, handoff, QA,
risk/regulatory and release owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017–024 | Stable shallow paths, exact package, PII exclusion and Windows safety |
| User stories | HC-US-SUB-002/004; HC-US-XFR-021–024 | Stable identity and understandable recovery paths |
| Risks/controls | HC-RISK-001/010/011/022/030 | Prevent misassociation, overwrite, disclosure and path substitution |
| Interfaces | HC-IF-REP-001 1.0.0; HC-IF-XFR-001 1.2.0 | New repository namespace; package bytes/paths unchanged |
| ADRs | ADR-0004/0012/0014/0016/0017 | Shallow UUID hierarchy and opaque package envelope |
| Claims/regulatory | India/CDSCO position unchanged | Engineering architecture/interface evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the decision, interface and linked lifecycle records
  after explicit project-owner acceptance.
- Accountable human owner: signed-in project owner; independent review pending.
- Existing package fixtures and accepted contracts were inspected so the
  illustrative media subfolders were not made a conflicting rewrite rule.
- Dependency/licence impact: none; documentation only.

## Configuration identity

- Source baseline/branch: `9d14de4973be9b1ca0d357341e71eb09b1e54cac` on `codex/humcapture-baseline`.
- Decision commit: assigned on commit and recorded in a verification update.
- Interface change: HC-IF-REP-001 version 1.0.0 namespace baseline.
- Backward compatibility: existing package internal paths remain unchanged.

## Verification disposition

- Architecture/interface/source documentation: recorded.
- Existing automated regression: 55/55 capability, 76/76 contract/evidence,
  and 6/6 SBOM-policy tests pass locally; retained SBOM/hash and both dependency
  audits pass.
- Executable namespace/schema behavior: deferred to B2/B3.
- Runtime, power-loss/HIL, field, independent and regulatory evidence: open.

## Decision and approvals

- Decision: I0.4B-B1 accepted; B2/B3 remain required before repository feature
  implementation.
- Permitted classification: engineering architecture/interface baseline only.
- Change/verification owner: signed-in project owner, 2026-09-10.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260905-003  
**Title:** Baseline I0.3A transfer and immutable package contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-05  
**Last reviewed:** 2026-09-05

## Objective and boundary

- Objective: implement the explicitly approved package, collection-resume,
  common-verification, and commit-linkage contract slice.
- Allowed paths: `src/HumCapture` only.
- Excluded: HumTrack internals, application/runtime features, authentication/TLS
  mechanics, repository implementation, receipt signing, HIL/field work,
  classification, certification, clinical and release claims.

## Classification and owners

- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Privacy/security-adjacent
- [ ] Application implementation
- [ ] Security transport implementation
- [ ] Regulatory/intended-use/claims change

Primary owner is the System Architect. Affected owners are Android,
coordinator, UVC, transfer/repository, simulator/QA, security/privacy, and
regulatory/risk. ADR-0012 records the accepted alternatives and consequences.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| User stories | HC-US-XFR-001–014 | Finalize, collect, recover, verify, commit, receipt and cleanup |
| Requirements | HC-AND-REQ-004/005; HC-COORD-REQ-003/004/012; HC-DATA-REQ-001–004/007–012; HC-SEC-REQ-002/003/005 | Exact package/resume/verification predicates |
| Risks | HC-RISK-001/002/007/009–011/022 | Reject incompatible partials, false verification, wrong identity and premature deletion |
| Interface | HC-IF-XFR-001 1.0.0 | New OpenAPI 3.1.2 and three record schemas |
| ADRs | ADR-0002, ADR-0004, ADR-0009, ADR-0011, ADR-0012 | Refines verified package boundary |
| Claims | India/CDSCO position unchanged | Engineering contract evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the ADR, contract, schemas, OpenAPI, conformance logic,
  tests, traceability, risk/compliance refresh, and review record under explicit
  project-owner approval.
- Accountable human owner: signed-in project owner; independent review pending.
- Dependency/licence impact: none. Existing locked Node/Ajv test surface reused.
- Privacy test data: synthetic UUIDs and account only; no subject PII.

## Implementation and configuration identity

- Source baseline: `d29fffe4c420a62a84967380b8084c7898504088` on
  `codex/humcapture-baseline`.
- Change commit: commit containing this record on the same branch.
- Interface: HC-IF-XFR-001/OpenAPI/schema version 1.0.0.
- Dependency/SBOM change: none; manifests and runtime build surfaces unchanged.

## Verification plan and current results

| Test | Behavior/oracle | Current result | Limitation |
|---|---|---|---|
| HC-XFR-TEST-001/002 | Three schemas compile; OpenAPI pull/range/digest/security-deferral structure | Pass locally | Source only |
| HC-XFR-TEST-003/004 | Canonical identity, artifact roles/timing/length and path safety | Pass locally | No filesystem verifier yet |
| HC-XFR-TEST-005/006 | Exact range/checkpoint and validator-change/full/416 recovery | Pass locally | Synthetic HTTP outcomes |
| HC-XFR-TEST-007 | USB/MTP/local common format and artifact-boundary restart | Pass locally | No device/MTP runtime |
| HC-XFR-TEST-008/009 | False verification and identity conflict reject | Pass locally | No repository concurrency |
| HC-XFR-TEST-010/011 | Verified commit/receipt binding and digest/ETag encoding | Pass locally | No durable commit or network |

Evidence levels:

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Local verification on 2026-09-05:

- evidence-control: 42/42 tests pass, including HC-XFR-TEST-001–011;
- capability-evidence regression: 55/55 tests pass;
- dependency audits: zero vulnerabilities in both locked Node surfaces;
- SBOM: 6/6 policy tests, project validation, and detached SHA-256 readback pass
  at `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`;
- Redocly CLI 2.51.2 validates the OpenAPI document under recommended rules,
  excluding only `security-defined` because the concrete mandatory security
  scheme is the explicitly deferred I0.3B decision, and the advisory
  `info-license` rule;
- 55 tracked JSON documents parse and `git diff --check` passes.

Final diff, commit scope, and exact-SHA remote CI are recorded in the handoff.

## Review decision

- Engineering design: explicitly approved by project owner.
- Engineering baseline: approved by project owner; pending commit/CI confirmation.
- Independent review: pending.
- Controlled release: blocked.
- Permitted claim: schema/conformance source evidence only.

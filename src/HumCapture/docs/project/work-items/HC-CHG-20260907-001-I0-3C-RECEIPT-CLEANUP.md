# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260907-001  
**Title:** Baseline I0.3C receipt acknowledgement and operator cleanup contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-07  
**Last reviewed:** 2026-09-07

## Objective and boundary

- Objective: implement the explicitly approved post-commit receipt,
  acknowledgement, reconciliation, cleanup and fallback contract slice.
- Allowed paths: `src/HumCapture` only.
- Excluded: HumTrack internals, application/runtime deletion, new security
  mechanisms, clinical/classification/certification and release claims.

## Classification and owners

- [x] Cross-component/interface and persistence lifecycle
- [x] Destructive trained-operator workflow
- [x] Privacy/regulatory/risk-adjacent
- [ ] Application or repository implementation
- [ ] Intended-use, classification, or claims change

ADR-0014 records the decision and alternatives. Architecture is primary;
Android, coordinator, transfer/repository, QA, privacy, risk/regulatory and
trained-operator workflow are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| User stories | HC-US-XFR-013–020; HC-US-MVP-002 | Complete independently of cleanup; recover and clear safely |
| Requirements | HC-AND-REQ-005/006; HC-COORD-REQ-016; HC-DATA-REQ-013–016 | Exact durable receipt and explicit truthful cleanup |
| Risks | HC-RISK-007/027/028 | Premature deletion, conflict/loss and false cleanup status |
| Interfaces | HC-IF-RCP-001 1.0.0; HC-IF-CTRL-001 1.3.0 | Six receipt/cleanup schemas and five WSS messages |
| ADR | ADR-0014 | Commit receipts control cleanup, not completion |
| Claims | India/CDSCO position unchanged | Engineering contract evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the ADR, contract, schemas, conformance logic, tests,
  traceability, risk/privacy/applicability updates and this review record under
  explicit project-owner approval.
- Accountable human owner: signed-in project owner; independent review pending.
- Dependency/SBOM impact: none; existing locked Node/Ajv surface reused.
- Privacy test data: synthetic UUIDs/hashes only; no subject PII.

## Verification plan and evidence levels

| Test | Behavior/oracle | Result target | Limitation |
|---|---|---|---|
| HC-RCP-TEST-001 | Six schemas and AsyncAPI bindings | Pass | Source only |
| HC-RCP-TEST-002–005 | Post-commit immutability, conflict, ack and loss recovery | Pass | Synthetic records |
| HC-RCP-TEST-006–007 | Explicit/idle/exact cleanup and offline-manual boundary | Pass | No UI/device |
| HC-RCP-TEST-008–010 | Truthful partial result/retry, minimization and completion separation | Pass | No filesystem deletion |

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Detailed current commands/results and limitations are recorded in
`docs/verification/I0_3C_RECEIPT_CLEANUP_CONTRACT_REPORT.md`.

## Review decision

- Engineering design: explicitly approved by project owner.
- Engineering baseline: pending final local and exact-SHA CI verification.
- Independent review: pending.
- Controlled release: blocked.
- Permitted claim: contract-source evidence only after verification passes.

Local verification on 2026-09-07:

- evidence-control: 64/64 tests pass, including HC-RCP-TEST-001–010;
- capability-evidence regression: 55/55 tests pass;
- SBOM policy: 6/6 tests and current 20-component/21-node inventory validation pass;
- both production dependency audits report zero vulnerabilities;
- AsyncAPI CLI 6.0.2 accepts the document and referenced schemas;
- 64 non-generated JSON documents parse and `git diff --check` passes.

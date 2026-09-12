# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260906-001  
**Title:** Baseline I0.3B pairing and transport-security contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-06  
**Last reviewed:** 2026-09-06

## Objective and boundary

- Objective: implement the explicitly approved pairing, mutual-TLS,
  authorization, lifecycle, recovery, and audit contract slice.
- Allowed paths: `src/HumCapture` only.
- Excluded: HumTrack internals, application/runtime features, production PKI or
  credential storage, penetration/HIL/field work, classification, certification,
  clinical and release claims.

## Classification and owners

- [x] Cross-component/interface
- [x] Security architecture and compatibility
- [x] Privacy/regulatory/risk-adjacent
- [ ] Application or transport implementation
- [ ] Regulatory/intended-use/claims change

ADR-0013 records the security decision and alternatives. Architecture is the
primary owner; Android, coordinator, transfer/repository, QA, security/privacy,
regulatory/risk, and trained-operator workflow are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| User stories | HC-US-DEV-001–005/009/012; HC-US-XFR-001–014 | Pair, reconnect, collect, recover and unpair without bypass |
| Requirements | HC-SEC-REQ-001–005/007–014; HC-COMPAT-REQ-001–004 | Exact trust, transport, recovery and privacy predicates |
| Risks | HC-RISK-009/011/013/023–026 | MITM, unauthorized control, downgrade, replay, credential loss and disclosure |
| Interfaces | HC-IF-SEC-001 1.0.0; HC-IF-CTRL-001 1.2.0; HC-IF-XFR-001 1.1.0 | New schemas and mTLS WSS/HTTPS bindings |
| ADR | ADR-0013 | Attended enrollment and mutual TLS |
| Claims | India/CDSCO position unchanged | Engineering contract evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the ADR, contract, schemas, bindings, conformance logic,
  tests, traceability, risk/compliance refresh, and review record under explicit
  project-owner approval.
- Accountable human owner: signed-in project owner; independent review pending.
- Dependency/SBOM impact: none; existing locked Node/Ajv surface reused.
- Privacy test data: synthetic UUIDs/account/fingerprints only; no subject PII.

## Verification plan and current results

| Test | Behavior/oracle | Current result | Limitation |
|---|---|---|---|
| HC-SEC-TEST-001–003 | Four schemas and concrete mutual-TLS OpenAPI/AsyncAPI binding | Pass locally | Source only |
| HC-SEC-TEST-004–006 | Bootstrap entropy/expiry/fingerprint, single-use/lockout, transcript/lifetime | Pass locally | Synthetic records |
| HC-SEC-TEST-007–010 | Trust lifecycle, resource authorization, TLS downgrade/early-data, revoked/lost/expired rejection | Pass locally | No real TLS/credential store |
| HC-SEC-TEST-011–012 | Audit redaction and discovery/persistence minimization | Pass locally | No runtime logs/network capture |

Evidence levels:

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Local verification on 2026-09-06:

- evidence-control: 54/54 tests pass, including HC-SEC-TEST-001–012;
- capability-evidence regression: 55/55 tests pass;
- SBOM policy: 6/6 tests pass; dependency manifests did not change;
- production dependency audits: zero vulnerabilities reported;
- Redocly CLI 2.51.2 validates OpenAPI 3.1.2 under recommended rules with only
  advisory `info-license` excluded;
- AsyncAPI CLI 6.0.2 validates the control document and references;
- 59 non-dependency JSON documents parse and `git diff --check` passes.
- implementation commit `2af6761beed6e0aaaba5f0348ba3d991678b4b37` is
  pushed; exact-SHA HumCapture CI runs `34037481492` and `34037479975` pass.

Detailed evidence and explicit limitations are recorded in
`docs/verification/I0_3B_SECURITY_CONTRACT_REPORT.md`.

## Review decision

- Engineering design: explicitly approved by project owner.
- Engineering baseline: approved by project owner; source verification and
  exact-SHA CI confirmed for the implementation commit.
- Independent review: pending.
- Controlled release: blocked.
- Permitted claim: contract-source evidence only after verification passes.

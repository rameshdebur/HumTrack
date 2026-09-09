# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260909-001  
**Title:** Baseline I0.4A timing, IMU and camera metadata contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect and timing owner  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-09  
**Last reviewed:** 2026-09-09

## Objective and boundary

- Objective: implement the explicitly approved HC-IF-TIM-001 source-level
  contract, schemas, reference codec, vectors and conformance evidence.
- Allowed paths: `src/HumCapture` only.
- Excluded: HumTrack internals; Android, UVC or Coordinator application code;
  production capture/recovery; analytics/UI; HIL/field work; clinical,
  certification, regulatory or release claims.
- Expected evidence: source, static and automated contract behavior.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [ ] Security/privacy/supply chain
- [x] Scientific acquisition/timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

Architecture review is required and recorded by ADR-0015. Primary owner is the
System Architect/timing owner. Android, UVC, Coordinator, repository/transfer,
QA, risk/regulatory and release/SBOM owners are affected.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-TIME-REQ-001–014; HC-AND-REQ-001; HC-SYS-REQ-003 | Native timing, binary integrity, metadata, mappings and protocol gates |
| User stories | HC-US-RDY-008–009; HC-US-CAP-001–019; HC-US-QA-001–012 | Capture, inspect, recover and assess timing/IMU evidence |
| Risks/controls | HC-RISK-003–005/012/013/021/029 | Prevent silent loss, false cadence/sync and metadata/transform confusion |
| Interfaces/schemas | HC-IF-TIM-001 1.0.0; HC-IF-XFR-001 1.2.0 | New binary/metadata profile; additive package declarations |
| ADRs | ADR-0002/0003/0005/0009/0011/0012/0015 | Fixed binary native evidence and explicit mappings |
| Compatibility | Unknown major/minor required semantics fail closed; historical packages unchanged | No silent migration or nominal-rate synthesis |
| Claims/regulatory | India/CDSCO position unchanged | Engineering contract evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the contract, ADR, requirements/risk traceability,
  schemas, reference codec/generator, fixtures, tests and review records under
  explicit project-owner decisions A–K.
- Accountable human owner: signed-in project owner; independent review pending.
- Sources/assumptions checked: existing accepted contracts/ADRs and official
  Android/Windows timing API documentation during refinement.
- New dependency/licence/privacy/security impact: none; existing locked Node/Ajv
  test surface reused; fixtures are synthetic and contain no subject data.
- Durable output is promoted into versioned repository artifacts, not conversation history.

## Implementation and configuration identity

- Source baseline/branch: `1a0cd0b9ab2e7d274aa3a8303e2a917a8401a4b8` on `codex/humcapture-baseline`.
- Commit: pending.
- Build/toolchain: Node.js built-in binary/crypto APIs plus locked Ajv surface.
- Dependency/SBOM change: no dependency or build manifest changed; current SBOM must be regression-validated.
- Interface/schema change: HC-IF-TIM-001 1.0.0; HC-IF-XFR-001 1.2.0 additive profile fields.
- Backward compatibility: old packages remain historical; new profile is explicit and fail-closed.

## Verification plan and results

| Verification ID | Requirement/risk | Method and oracle | Expected | Actual | Evidence | Disposition |
|---|---|---|---|---|---|---|
| HC-TIM-TEST-001 | Metadata schemas | Ajv 2020-12 compile and canonical fixtures | Pass | Pass | automated test | Accepted source evidence |
| HC-TIM-TEST-002–004 | Binary identity/round-trip | Golden SHA/length/count and byte-exact codec | Pass | Pass | automated tests | Accepted source evidence |
| HC-TIM-TEST-005–007 | Integrity/recovery/version | Independent corruption/truncation/version mutations | Reject/recover exactly | Pass | automated tests | Accepted source evidence |
| HC-TIM-TEST-008–009 | Cadence/clock model | Synthetic gaps/regression and mapping mutations | Detect/reject | Pass | automated tests | Accepted source evidence |
| HC-TIM-TEST-010–012 | Capability/quality/association/package | Fixed-rate, unavailable metadata, protocol and frame/profile mutations | Defined accept/degrade/reject | Pass | automated tests | Accepted source evidence |

Evidence levels achieved:

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Unverified: Android/UVC production output, Coordinator ingestion, real clock
fits, live frame association, capture recovery, representative devices,
analytics, field workflow and independent/regulatory review.

## Security and supply-chain disposition

- Secret scanning: staged text diff checked for common credential/private-key patterns; no matches.
- Static security analysis: bounded parser mutation tests, syntax checks and fail-closed reserved-field handling pass; broader product analysis is deferred with runtime.
- Dependency/vulnerability review: both locked production Node audits report zero vulnerabilities; no manifest changed.
- Licence/supplier/maintenance review: unchanged dependency surface.
- SBOM: current 20-component/21-node CycloneDX inventory validates and its retained SHA-256 matches; no regeneration required because no dependency/build manifest changed.
- Release effect: independent review and runtime/HIL/field/regulatory evidence remain blockers.

## Review

- Complete diff inspected: pending final review.
- Traceability: implemented; final consistency check pending.
- Test oracle: independent golden hash, corruption and semantic mutations implemented.
- Documentation/project state: pending final refresh.
- Independence limitation: authoring and local verification use the same AI-assisted workstream; human independent review pending.

## Decision and approvals

- Decision: In review.
- Permitted classification: Engineering contract snapshot only.
- Release blockers: commit/CI, independent review, runtime/HIL/field and qualified regulatory review as applicable.
- Change/verification owner: signed-in project owner, 2026-09-09, project owner/system architect.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

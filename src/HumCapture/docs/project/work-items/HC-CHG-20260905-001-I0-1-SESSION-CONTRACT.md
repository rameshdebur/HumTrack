# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260905-001  
**Title:** Baseline I0.1 session/protocol control contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-05  
**Last reviewed:** 2026-09-05

## Objective and boundary

- Objective: record explicitly approved I0.1I semantics and create the
  executable session/protocol slice of HC-IF-CTRL-001.
- Allowed paths: `src/HumCapture` only.
- Explicit exclusions: HumTrack internals, root CI changes, application feature
  code, transport/authentication bindings, transfer/media/timing binary formats,
  and regulatory/release claims.
- Expected evidence: source/static and automated contract behavior only.

## Classification

- [ ] Local implementation
- [x] Cross-component/interface
- [x] Architectural/persistence
- [ ] Security/privacy/supply chain
- [ ] Scientific acquisition/timing/analytics
- [ ] Regulatory/intended-use/claims
- [ ] Exploratory/diagnostic

Architecture approval was supplied explicitly for immutable post-capture
protocol binding and new-session handling of material changes. ADR-0010 records
the rationale. Independent review remains open.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-COORD-REQ-005–012; HC-DATA-REQ-006 | Added atomic session/snapshot/completion controls |
| User stories | HC-US-SES-001–009; HC-US-QA-007–010 | Existing accepted stories made executable for the session slice |
| Risks/controls | HC-RISK-001/002/007/010/012/013/020 | Added HC-RISK-020 and immutable-snapshot controls |
| Interfaces/schemas | HC-IF-CTRL-001 1.0.0; control v1 AsyncAPI and JSON Schemas | First executable session/protocol interface version |
| ADRs | ADR-0004, ADR-0009, ADR-0010 | ADR-0010 records the newly accepted decision |
| Compatibility promises | HC-COMPAT-REQ-002–004 | Exact v1 version; unknown fields/versions fail closed; no historical rewrite |
| Claims/regulatory baseline | CDSCO baseline and claim prohibitions unchanged | Engineering evidence only; no conformity or approval claim |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- Tool and contribution: OpenAI Codex drafted the ADR, interface text, schemas,
  fixtures, conformance oracle, tests, traceability, and change record under the
  project owner's explicit decisions and bounded authorization.
- Accountable human owner: signed-in project owner; independent review pending.
- Sources checked: accepted project requirements/risks and official AsyncAPI
  3.1.0 and JSON Schema 2020-12 specifications.
- New dependency/licence/privacy/security impact: no dependency or subject-data
  fixture added; existing locked Ajv tooling executes the schemas.
- Durable information: all material rules and limitations are recorded in the
  ADR, interface, requirements, risk, fixtures, tests, and project state.

## Implementation and configuration identity

- Source baseline/branch: `811821ac4079a5bfdab6019dcf504075af203da9` on
  `codex/humcapture-baseline`.
- Change commit: the commit containing this record on the same review branch.
- Toolchain: Node.js 22.12.0 in CI; schemas use JSON Schema 2020-12; logical API
  document uses AsyncAPI 3.1.0.
- Dependency/SBOM change: none; dependency manifests are unchanged. Existing
  SBOM policy and retained-hash validation remain required.
- Interface change: HC-IF-CTRL-001 `0.1-draft` to `1.0.0`; first executable
  session/protocol schema version `1.0.0`.
- Backward compatibility: no runtime consumer exists. Unsupported versions and
  unknown fields reject; future incompatible change requires a new version.

## Verification plan and results

| Verification ID | Requirement/risk | Method and oracle | Expected result | Current result | Evidence/disposition |
|---|---|---|---|---|---|
| HC-CTRL-TEST-001 | HC-COMPAT-REQ-002/003 | Strict Ajv 2020-12 compile | All eight schemas compile | Pass locally | Automated source evidence |
| HC-CTRL-TEST-002 | Interface closure | Parse/resolve refs plus one-off published AsyncAPI CLI 6.0.2 validation | Version/refs exact; official tool accepts document and references | Pass locally | CLI declared Node 24 but executed successfully on project Node 22.12; not added to baseline |
| HC-CTRL-TEST-003 | HC-COORD-REQ-005–011; HC-DATA-REQ-006 | Synthetic valid lifecycle, pre-capture revision, reopen, completion/handoff bundle | All schema and cross-record predicates pass | Pass locally | Runtime/persistence not tested |
| HC-CTRL-TEST-004 | HC-COORD-REQ-008; HC-RISK-002/020 | Enumerate state × command × resulting-state space against accepted matrix | Every unlisted combination rejects | Pass locally: 553 rejected | Allowed paths separately tested where material |
| HC-CTRL-TEST-005 | HC-RISK-001/002/010/020 | Named invalid/malformed fixtures | Correct semantic code or schema rejection | Pass locally | Includes post-capture change and false completion |
| HC-CTRL-TEST-006 | HC-SYS-REQ-003; HC-COORD-REQ-005 | Fixed/flexible boundary oracle | Inclusive valid range; mismatch/inverted/out-of-range reject | Pass locally | UI selection not tested |

Evidence levels achieved at authoring time:

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Local regression verification also passed 55/55 capability/evidence tests and
6/6 SBOM-tool tests. Both locked npm surfaces reported zero vulnerabilities at
the configured audit threshold. The retained CycloneDX 1.7 document validated
with 20 components and 21 dependency nodes, and its detached SHA-256 matched.
Remote exact-commit CI is recorded in the PR check history. Independent review,
runtime/persistence/restart behavior,
remaining control-message slices, and every higher evidence level remain open.

## Security and supply-chain disposition

- Synthetic UUIDs/accounts only; no subject data, credentials, or secrets.
- No dependency manifest changed; existing locked Ajv/Ajv-formats are reused.
- Existing CycloneDX SBOM and SHA-256 validation must pass unchanged.
- Published AsyncAPI CLI 6.0.2 was used only as a one-off validator; its
  transient packages and engine/deprecation warnings are not production or
  project dependencies.
- This is not a vulnerability, licence, supplier, or release approval.

## Review and decision

- Complete diff and scope inspection: passed locally; changes are confined to
  `src/HumCapture`.
- Requirements/risk/interface traceability: implemented; independent review pending.
- Oracle/failure sensitivity: positive, boundary, cross-record, malformed, and
  exhaustive forbidden-transition cases included.
- Decision: engineering interface baseline approved by project owner; controlled
  release and independent approval remain blocked.
- Permitted classification: engineering source/automated evidence only.

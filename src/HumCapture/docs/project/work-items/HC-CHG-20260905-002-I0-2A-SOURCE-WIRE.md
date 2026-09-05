# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260905-002  
**Title:** Baseline I0.2A source-message wire contract  
**State:** In review  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Signed-in project owner / System Architect  
**Verification owner:** Signed-in project owner / Engineering verification  
**Independent reviewer:** Pending; required before controlled release  
**Created:** 2026-09-05  
**Last reviewed:** 2026-09-05

## Objective and boundary

- Objective: implement the explicitly approved transport-neutral source,
  readiness, custody, receipt, quality, and monotonic-time contract slice.
- Allowed paths: `src/HumCapture` only.
- Excluded: HumTrack internals, root workflow/settings, application features,
  transport/authentication binding, transfer/media/binary formats, receipt
  signing, hardware, clinical/regulatory, and release claims.

## Classification and owners

- [x] Cross-component/interface
- [x] Architectural/persistence
- [x] Scientific timing representation
- [ ] Application implementation
- [ ] Security transport implementation
- [ ] Regulatory/intended-use/claims change

Primary owner is the System Architect. Affected owners are Android,
coordinator, UVC, timing, transfer/repository, simulator/QA, security, and
regulatory/risk. Architecture review is required and ADR-0011 records the
accepted representation and trade-offs.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-COORD-REQ-013–015; HC-DATA-REQ-007/008; HC-TIME-REQ-006/007 | Added exact wire, content identity, and authority constraints |
| Risks/controls | HC-RISK-001–004/007/009/010/012/013/021 | Added precision/clock-epoch control and executable negative evidence |
| Interface | HC-IF-CTRL-001 1.1.0; control-v1 AsyncAPI/schemas | Additive aggregate-interface revision; new record schemas start at 1.0.0 |
| ADRs | ADR-0005, ADR-0009, ADR-0010, ADR-0011 | Exact monotonic-time representation accepted |
| Compatibility | HC-COMPAT-REQ-002–004 | Strict versions/properties; historical records not rewritten |
| Claims | India/CDSCO position unchanged | Engineering contract evidence only |

## AI and automation declaration

- Material AI/automation contribution: Substantial.
- OpenAI Codex drafted the ADR, schemas, AsyncAPI expansion, conformance oracle,
  fixtures, tests, traceability, and change record under explicit approval.
- Accountable human owner: signed-in project owner; independent review pending.
- New dependency/licence/privacy impact: none; synthetic identifiers only and
  the existing locked Ajv validation surface is reused.

## Implementation and configuration identity

- Source baseline: `bd5a69147e2d98e633068f9da0cc7e3ae25e58a5` on
  `codex/humcapture-baseline`.
- Change commit: commit containing this record on the same branch.
- Interface revision: HC-IF-CTRL-001/AsyncAPI `1.1.0`; individual source record
  schemas `1.0.0`; JSON Schema 2020-12.
- Dependency/SBOM change: none; manifests/build surfaces unchanged. Existing
  CycloneDX/hash checks remain required.

## Verification plan and current results

| Test | Behavior/oracle | Current result | Evidence limitation |
|---|---|---|---|
| HC-CTRL-TEST-001/002/016 | All 18 schemas compile; session and source AsyncAPI refs/operations close | Pass locally | Format/source only |
| HC-CTRL-TEST-007 | Valid source/config/start/readiness/custody/receipt/quality bundle | Pass locally | No runtime persistence |
| HC-CTRL-TEST-008 | Exact uint64 maximum; overflow/noncanonical rejection | Pass locally | No platform serializer yet |
| HC-CTRL-TEST-009 | Same-ID replay versus semantic conflict | Pass locally | No network retry runtime |
| HC-CTRL-TEST-010/011 | First-sample authority; event order; new boot/clock requires new attempt | Pass locally | Synthetic clocks/restarts |
| HC-CTRL-TEST-012 | Blocking readiness and unauthorized override rejection | Pass locally | No named hardware readiness |
| HC-CTRL-TEST-013 | 299 forbidden custody paths; receipt only after exact commit | Pass locally | Repository not implemented |
| HC-CTRL-TEST-014 | Quality false-pass rejection | Pass locally | Protocol thresholds not implemented |
| HC-CTRL-TEST-015 | Named negative fixture inventory | Pass locally | Fixture registry only |
| HC-CTRL-TEST-017/018 | 2,799 source-command and 241 source-event combinations reject | Pass locally | Accepted paths selectively exercised |
| HC-CTRL-TEST-019 | RFC 8785/SHA-256 identity, member-order stability, and tamper rejection | Pass locally | Cross-platform serializers not implemented |

Evidence levels:

- [x] Source implemented
- [x] Build/static checks passed
- [x] Automated behavior verified
- [ ] Runtime integration verified
- [ ] Hardware-in-the-loop verified
- [ ] Field workflow verified
- [ ] Regulatory or clinical review completed

Local verification on 2026-09-05:

- evidence-control: 31/31 tests pass;
- capability-evidence regression: 55/55 tests plus valid run/campaign pass;
- dependency audits: zero vulnerabilities in both locked Node surfaces;
- SBOM: 6/6 policy tests, project validation, and detached SHA-256 readback pass
  at `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`;
- published `@asyncapi/cli` 6.0.2 accepts the AsyncAPI document and all
  referenced schemas; its metrics submission warning is non-validation output;
- JavaScript syntax, 149 JSON files, whitespace, and `src/HumCapture`-only
  scope checks pass.

Exact-SHA remote CI, final diff, and commit scope are recorded before
completion. Independent design/QA review and every higher evidence level remain
open.

## Review decision

- Engineering baseline: approved by project owner.
- Independent review: pending.
- Controlled release: blocked.
- Permitted claim: schema/conformance source evidence only.

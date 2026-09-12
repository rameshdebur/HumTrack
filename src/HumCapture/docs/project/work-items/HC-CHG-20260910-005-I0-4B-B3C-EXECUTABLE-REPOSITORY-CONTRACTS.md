# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-005  
**Title:** Baseline I0.4B-B3C executable repository contracts  
**State:** Accepted source contract; independent review and runtime implementation pending  
**Change owner:** Signed-in project owner / System Architect  
**Component owner:** Repository owner  
**Verification owner:** Engineering QA  
**Date:** 2026-09-10

## Change classification and scope

- Cross-component interface and verification-infrastructure change.
- Paths are restricted to `src/HumCapture`.
- Adds schemas, DDL, fixtures and conformance code/tests; no Coordinator,
  repository, transfer, Android, UVC, or UI runtime feature is implemented.
- Architecture review is required and recorded by ADR-0021.
- Dependency and release-SBOM inventory are unchanged.

## Traceability

- Stories: HC-US-XFR-021–034.
- Requirements: HC-COORD-REQ-003; HC-DATA-REQ-002/003/011/013/017–038;
  HC-COMPAT-REQ-002–004.
- Risks: HC-RISK-001/002/007/010/011/020/022/030/031.
- Interfaces/decisions: HC-IF-REP-001 1.3.0; ADR-0016–0021.
- Verification: HC-REP-TEST-001–015 plus existing HumCapture regression suite.

## Configuration identity

- Source baseline/branch: `ef0b39ec37ac0560c079bc0976cd6c6059d18d82` on
  `codex/humcapture-baseline`.
- Decision/implementation commit:
  `de2c49134ff775c3bddaeed935c1322ebcde0ecf`.
- Compatibility: no existing repository exists in production; the descriptor
  fails closed for unsupported major versions or unknown required features.

## Verification disposition

- HC-REP-TEST-001–015: 15/15 passed, including in-memory execution of the
  SQLite DDL on pinned Node.js 22.12.0.
- Full evidence/control suite: 91/91 passed.
- Phase 0 capability regression: 55/55 passed.
- SBOM policy tests: 6/6 passed; official project validator reports 20
  components/21 dependency nodes and retained SHA-256
  `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`.
- Both production dependency audits: zero findings.
- All 86 tracked/new JSON files parsed; diff whitespace check passed.
- Exact decision-commit CI: HumCapture CI push run `34502020209` and PR run
  `34502026587` passed for `de2c49134ff775c3bddaeed935c1322ebcde0ecf`.
- Runtime integration, real filesystem crash/power-loss, HIL, field,
  independent and regulatory evidence: open.

## Decision and approvals

- Decision: B3C executable contract slice accepted; application repository
  implementation remains separately gated.
- Permitted classification: engineering source contract and automated
  conformance evidence only.
- Change/verification owner: signed-in project owner, 2026-09-10.
- Independent reviewer/release owner/regulatory-risk attribution: pending.

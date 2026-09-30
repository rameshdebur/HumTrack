# HumCapture Change and Review Record

**Change ID:** HC-CHG-20260910-006  
**Title:** Implement I0.4B-C1 repository initialize and open  
**State:** Implementation remotely verified; independent review pending  
**Change owner:** Signed-in project owner / Coordinator repository owner  
**Component owner:** Software Engineer with Coordinator/repository assignment  
**Verification owner:** Engineering QA  
**Independent reviewer:** Pending  
**Created:** 2026-09-10  
**Last reviewed:** 2026-09-10

## Objective and boundary

- Implement the first production Coordinator repository slice from the
  accepted HC-IF-REP-001 version 1.3.0 descriptor and SQLite DDL.
- Modified paths remain within `src/HumCapture`: Windows Coordinator source and
  tests, ADR/governance/verification records, CI-invoked test configuration,
  and SBOM tooling/artifact.
- Excludes subjects/protocols, package movement/commit/reconciliation,
  transfer, receipts/cleanup, backup/restore, migration, UI and Android/UVC.
- Expected evidence: source, clean build/static checks, automated behavior and
  local Windows process/runtime integration.

## Classification

- Local Coordinator implementation plus architectural/persistence and software
  supply-chain change.
- Primary owner: Coordinator/repository software engineer.
- Affected owners: System Architect, QA, Release/SBOM owner and Regulatory/Risk
  reviewer.
- Architecture review: ADR-0022 records the production provider choice.
- Interface impact: no schema or HC-IF-REP-001 version change; implementation
  consumes the accepted DDL and descriptor.

## Traceability

| Type | IDs or links | Impact/disposition |
|---|---|---|
| Requirements | HC-DATA-REQ-017/018/021/024/025/028/029; HC-COMPAT-REQ-002–004 | Initialize/open subset implemented; commit/backup remain open |
| User stories | HC-US-XFR-023/024/027 | UUID/provenance descriptor and safe unsupported inspection |
| Risks/controls | HC-RISK-030/031 | Fail-closed initialization, link checks, catalog agreement, no silent migration |
| Interfaces | HC-IF-REP-001 1.3.0 and repository-v1.sql | Consumed without contract change |
| ADRs | ADR-0016–0022 | Provider decision and implementation alignment |
| Compatibility | Windows 10 build 19041+, exact version/features for mutation | Older/newer/unknown-required repositories are read-only |
| Claims | India/CDSCO baseline unchanged | No approval/compliance claim |

## AI and automation declaration

- Material AI/automation contribution: Substantial implementation and test
  drafting; accountable human review remains required.
- Official Microsoft Learn and NuGet records were checked for provider role,
  default bundle, current stable version and target compatibility.
- New dependency: `Microsoft.Data.Sqlite` 10.0.12 with locked transitive
  SQLitePCLRaw 2.1.12 packages; SBOM and advisory checks updated.
- No subject data, secrets, raw media or external HumTrack dependency added.

## Implementation and configuration identity

- Branch: `codex/humcapture-baseline` in the isolated HumCapture worktree.
- Implementation commit: `7594e4ee2e1670ad7457c169d0a9833f5ec74bbc`.
- Toolchain: .NET SDK 10.0.401; Microsoft.NETCore.App 10.0.12; Windows host
  10.0.26200; Node.js contract harness.
- Interface/schema change: none.
- Backward compatibility: exact 1.3.0 repository opens for mutation; unsupported
  major, older/newer versions and unknown required features are not changed and
  return descriptor-only read-only inspection.

## Verification plan and results

| Verification | Requirement/risk | Result |
|---|---|---|
| HC-REP-RUNTIME-001–006 | Safe initialize, namespace, no overwrite | 6/6 pass |
| HC-REP-RUNTIME-007–010 | No silent migration/unknown capability mutation | 4/4 pass |
| HC-REP-RUNTIME-011–017 | Missing/corrupt/mismatched catalog, publication, hard-link and lock boundaries | 7/7 pass |
| Build/analyzers | Repository coding baseline | Release build: 0 warnings, 0 errors |
| HC-REP-TEST-001–015 and full contracts | Contract regression | 91/91 pass |
| NuGet advisory/deprecation query | Supply-chain screening | No findings from current configured sources |
| HC-GOV-002 SBOM | Dependency inventory and format | 6/6 tests; project and official validation pass; 31 components/32 nodes |
| Exact-source remote CI | Reproducible branch/PR verification | Push `34509901600` and PR `34509907387` passed for implementation SHA `7594e4ee2e1670ad7457c169d0a9833f5ec74bbc` |

Evidence levels achieved: source implemented; build/static checks passed;
automated behavior verified; local Windows process/runtime integration verified.

Not verified: abrupt process kill during initialization, OS crash/power loss,
filesystem/device removal, backup/restore, package commit/reconciliation, HIL,
field workflow, independent review, qualified regulatory review, or release
packaging/RID coverage.

SBOM: `sbom/humcapture.cdx.json`, product version `0.1.0-i0.4b-c1`, SHA-256
`d650ce9089e8bff62f26843c0629aebe71ae537bb317109a1c0c3c1bf9d67552`.

## Decision and approvals

- Current disposition: engineering implementation ready for independent review;
  not a controlled release.
- Human change/verification approval, independent review, release ownership and
  Regulatory/Risk attribution remain pending.

# HumCapture Regulatory Evidence Readiness Matrix

**Document ID:** HC-VER-READINESS-001  
**Revision:** 1.3  
**Date:** 2026-09-01  
**Status:** Gap assessment; not a conformity or certification statement

This matrix distinguishes existing engineering records from the additional
controls normally needed before evidence could support an India/CDSCO design
history or submission. Final applicability and sufficiency require a qualified
Indian regulatory professional and the applicable licence/submission route.

| Evidence/control area | Current retained evidence | Readiness | Required before dossier or release claim |
|---|---|---|---|
| Intended use and claims | Controlled draft and explicit claim prohibitions | Partial | Qualified applicability/classification and claims approval |
| Architecture and design decisions | ARD plus accepted ADRs 0001–0014; I0.1A-I, I0.2A and I0.3A-C are engineering interface baselines | Partial | Independent whole-contract/design review, remaining interface decisions, and formal release baseline approval |
| Product requirements and user stories | PRD, SRS, stories, HC-IF-CTRL-001 1.3.0, HC-IF-XFR-001 1.1.0, HC-IF-SEC-001 1.0.0 and HC-IF-RCP-001 1.0.0 | Partial | Approved complete interface set, application implementation, and change-control evidence |
| Bidirectional traceability | Preliminary links plus HC-CTRL-TEST-001–019 from session/source requirements and risks 020/021 to executable contract tests | Partial | Complete requirement → risk/control → implementation → result linkage and independent review |
| Automated control-contract V&V | Twenty-three JSON Schemas, AsyncAPI reference closure, session/source/receipt fixtures, exact uint64/clock/restart/replay/readiness/receipt/cleanup/quality checks, and 3,892 total forbidden session/source/event/custody combinations pass | Engineering only | Application persistence/deletion, runtime/restart/platform verification, independent QA approval and retained released-build results |
| Risk management | Preliminary plan/register with UVC findings | Partial | Approved method, ratings, benefit-risk/residual-risk review, production/post-market linkage |
| Verification protocols | Phase 0 specification and work items | Partial | Approved pre-execution protocols, acceptance criteria, deviations and independent approval |
| Automated evidence-contract V&V | P0.1 schemas/validator with 55 passing tests | Engineering only | Released-build execution, independent QA approval and controlled retained outputs |
| Hardware test results | P0.2A-J reports consolidated and hashed by P0.2K; named diagnostic configurations and limitations recorded | Partial | Complete controlled raw evidence for every claimed run, reviewer approval, normative hardware/profile testing |
| Raw evidence retention | Both P0.2J runs verify as `HC-EV-5becff40f4271131c8b43641` and `HC-EV-16e6576bbcf4f1896d64aac6`; P0.2A-I primary artifacts are unavailable | Partial | Prospective complete evidence capture; verified backup/restore and approved retention schedule |
| Build/source identification | Retrospective engineering snapshot `HC-ENG-20260831T163246Z-7d17f5afce36` identifies P0.2J; subsequent source is tracked on a protected review branch with CI | Blocked for release | Clean approved reproducible release identity and release-owner/QA approvals; do not relabel the retrospective snapshot |
| Evidence integrity | SHA-256 inventories, conflict rejection, read-only files, schema validation | Partial | Independent anchor/signature or validated eQMS/WORM control, access audit and trusted timestamps as applicable |
| Operator attribution | Signed-in Windows account | MVP only | Role control, training records, stronger authentication/signature controls if required |
| Test equipment/configuration | Host, driver, camera identity, profile, binary hashes in P0.2J | Partial | Calibration/verification status where applicable and controlled configuration records for every test |
| Software lifecycle/QMS | Governance documents and change rules | Partial | Implemented and audited QMS procedures across lifecycle, suppliers, CAPA, complaints and release |
| Cybersecurity/privacy | Preliminary requirements, Windows-account MVP, non-subject Phase 0 data | Early | Threat/risk evidence, security V&V, access/backup controls, approved subject-data handling |
| Software supply chain / SBOM | CycloneDX 1.7 source-manifest/build-context inventory with hashes, licence status, relationships and known unknowns; project and official validation plus snapshot/vault binding pass | Partial | Vulnerability/VEX, licence and supplier review, binary/runtime coverage and ongoing monitoring |
| Clinical/performance evidence | None claimed | Not started | Only if required by intended use/classification; approved study/evaluation and qualified review |
| Regulatory approval/certification | None claimed | Not started | Applicable CDSCO process, qualified review, submission/licence/approval evidence |

## Current conclusion

HumCapture now has a controlled engineering-evidence foundation and two retained
P0.2J raw runs. P0.2K documents that earlier P0.2 primary artifacts are not
recoverable and that the retained runs are not a complete normative package.
It does **not** yet have a CDSCO-ready dossier, controlled released
build, complete design history file, validated electronic QMS, clinical evidence,
or regulatory approval. Reports for runs whose raw artifacts were deleted or
were never imported remain useful engineering records but are not independently
re-verifiable primary proof.

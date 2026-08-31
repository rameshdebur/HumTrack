# HumCapture Regulatory Evidence Readiness Matrix

**Document ID:** HC-VER-READINESS-001  
**Revision:** 1.0  
**Date:** 2026-08-31  
**Status:** Gap assessment; not a conformity or certification statement

This matrix distinguishes existing engineering records from the additional
controls normally needed before evidence could support an India/CDSCO design
history or submission. Final applicability and sufficiency require a qualified
Indian regulatory professional and the applicable licence/submission route.

| Evidence/control area | Current retained evidence | Readiness | Required before dossier or release claim |
|---|---|---|---|
| Intended use and claims | Controlled draft and explicit claim prohibitions | Partial | Qualified applicability/classification and claims approval |
| Architecture and design decisions | ARD plus accepted ADRs 0001–0007 | Partial | Formal baseline approval and design-review records |
| Product requirements and user stories | PRD, SRS, stories, interfaces-to-create list | Partial | Approved versions, complete interface specifications, change control |
| Bidirectional traceability | Preliminary requirement/risk/test links | Partial | Atomic test IDs and complete requirement → risk/control → implementation → result linkage |
| Risk management | Preliminary plan/register with UVC findings | Partial | Approved method, ratings, benefit-risk/residual-risk review, production/post-market linkage |
| Verification protocols | Phase 0 specification and work items | Partial | Approved pre-execution protocols, acceptance criteria, deviations and independent approval |
| Automated evidence-contract V&V | P0.1 schemas/validator with 55 passing tests | Engineering only | Released-build execution, independent QA approval and controlled retained outputs |
| Hardware test results | P0.2 reports and named-configuration findings | Partial | Complete controlled raw evidence for every claimed run, reviewer approval, normative hardware/profile testing |
| Raw evidence retention | P0.2J disconnect artifacts imported as `HC-EV-5becff40f4271131c8b43641` | Partial | Import/recover earlier runs where possible; verified backup/restore and approved retention schedule |
| Build/source identification | Engineering snapshot `HC-ENG-20260831T163246Z-7d17f5afce36` with retrospective source inventory, exact probe-source/executable hashes and validated SBOM | Blocked for release | Track HumCapture in Git; clean approved version; reproducible build and release-owner/QA approvals |
| Evidence integrity | SHA-256 inventories, conflict rejection, read-only files, schema validation | Partial | Independent anchor/signature or validated eQMS/WORM control, access audit and trusted timestamps as applicable |
| Operator attribution | Signed-in Windows account | MVP only | Role control, training records, stronger authentication/signature controls if required |
| Test equipment/configuration | Host, driver, camera identity, profile, binary hashes in P0.2J | Partial | Calibration/verification status where applicable and controlled configuration records for every test |
| Software lifecycle/QMS | Governance documents and change rules | Partial | Implemented and audited QMS procedures across lifecycle, suppliers, CAPA, complaints and release |
| Cybersecurity/privacy | Preliminary requirements, Windows-account MVP, non-subject Phase 0 data | Early | Threat/risk evidence, security V&V, access/backup controls, approved subject-data handling |
| Software supply chain / SBOM | CycloneDX 1.7 source-manifest/build-context inventory with hashes, licence status, relationships and known unknowns; project and official validation plus snapshot/vault binding pass | Partial | Vulnerability/VEX, licence and supplier review, binary/runtime coverage and ongoing monitoring |
| Clinical/performance evidence | None claimed | Not started | Only if required by intended use/classification; approved study/evaluation and qualified review |
| Regulatory approval/certification | None claimed | Not started | Applicable CDSCO process, qualified review, submission/licence/approval evidence |

## Current conclusion

HumCapture now has a controlled engineering-evidence foundation and one retained
P0.2J raw run. It does **not** yet have a CDSCO-ready dossier, controlled released
build, complete design history file, validated electronic QMS, clinical evidence,
or regulatory approval. Reports for runs whose raw artifacts were deleted or
were never imported remain useful engineering records but are not independently
re-verifiable primary proof.

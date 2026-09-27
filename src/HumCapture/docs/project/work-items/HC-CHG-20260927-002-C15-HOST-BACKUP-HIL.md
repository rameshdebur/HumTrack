# C15A-C: approved engineering integration and HIL-readiness audit

User approved implementation and requested an audit toward supervised human
testing after the batch. Cross-component/interface/persistence work, governed
by ADR-0039 and HC-IF-EHB-001 before implementation. Primary Coordinator/
Repository engineer; affected Architect, QA, UVC, Risk and Release/SBOM owners.
Existing baselined ARD/PRD/stories/SRS, roles, governance, India/CDSCO preliminary
baseline and risk/interface plans apply; no regulatory applicability change.

Accept: actual host assignment/run/history/recovery; paired-ledger backup and
non-overwriting restore; changed/partial/busy refusal; real child-process kill
and recovery; no false completion or deletion. Retain results, traceability and
SBOM updates. Audit current attached hardware and runnable capture-to-package
path honestly: diagnostic probes are not the production application. No clinical
participants, unapproved camera controls or certification claim.

Implemented and locally verified: clean Release build; 215 runtime, 115 contract,
13 SBOM/register tests; actual host plus pinned synthetic-media decoding through
existing final commit, backup, restore and committed reconciliation. Durable
report HC-VR-I0-4B-C15ABC-001 contains raw results. HIL audit HC-AUD-C15-HIL-001
finds conditional single-C920 diagnostic readiness, but no end-to-end production
camera package bridge or operator UI. No fresh capture was performed. Source
review, requirements/risk/traceability and SBOM/register updated; no dependency
added. Scoped Git commit/push and hosted checks remain source-SHA-bound.

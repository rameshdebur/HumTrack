# C9 Executable Host Change Record

**ID:** HC-CHG-20260912-014  
**Date:** 2026-09-12  
**State:** Local engineering verification passed; CI and independent review pending

User authorized the next executable-host step. Primary owner: Coordinator
engineer. Affected: Architect, repository engineer, QA, Risk/Regulatory and
Release/SBOM. Verification owner: Engineering QA. Paths: apps/windows-coordinator,
tools/sbom, sbom and affected HumCapture documentation only.

Classification: architectural process entry point and versioned CLI output.
Architecture review and acceptance are recorded in ADR-0023. Reuses accepted
design/requirements/governance, HC-IF-REP-001 1.3.0, HC-DATA-REQ-033–038,
HC-US-XFR-021–034 and HC-RISK-022/030/031. Preliminary India baseline remains
applicable; no intended-use, subject-data, scientific timing or medical claim
change. No controlled release or qualified regulatory approval.

Framework-dependent .NET 10 Windows executable, bounded one-pass startup,
structured JSON 1.0.0, controlled exit codes, Ctrl+C cancellation request,
same-session cooperating-host mutex. No data-root initialization or new
repository persistence format. Existing locked third-party dependencies only;
SBOM generator now covers host manifest, lock and dependency graph edges.
Self-test references host so existing CI builds and tests the real process.
No root HumTrack workflow modification.

AI contribution: implementation, tests, architecture review and documentation.
Independent human review remains required. HC-VR-I0-4B-C9-001 records evidence
and limitations.

# ADR-0040: Coordinator guided workspace, simulation first

2026-09-30. Accepted design under the user's C16A-C approval and successive
screen/acceptance approvals. Engineering baseline, not release approval.

## Ownership and review

Cross-component UI/host/simulator change. Primary: Coordinator engineer.
Affected: Architect, UX/accessibility, repository, QA, release/SBOM and risk.
Architecture and UX review are recorded here as agent design review, not
independent human acceptance. QA verifies software evidence separately from HIL.

## Decision and alternatives

Use the existing ARD's Avalonia UI with Fluent styling, independently authored
inside HumCapture. Retain version 11.3.12 used by HumTrack as a bounded initial
baseline; no HumTrack assembly/reference. Do not add commercial developer tools.
Use Avalonia.Win32 plus Skia rather than Avalonia.Desktop: the latter's unused
Linux Tmds.DBus.Protocol 0.21.2 failed NuGet advisory restore (GHSA-xrw6-gwf8-vvr9).
The Windows-only graph restores without that package; warnings are not suppressed.
Avalonia.BuildServices is retained in the dependency inventory but all assets
are excluded (no build stats task). Headless is engineering UI-test tooling.
Choose the approved guided workspace over a modal wizard (repeated take/recovery
backtracking) and a free dashboard (less obvious prerequisites).

The approved flow is Session -> Cameras -> Capture -> Results, with persistent
subject/session/protocol context and SIMULATION labelling. All capture sources,
frame activity, preview and capture outcomes in C16 are synthetic. Never create
a scientific package, VERIFIED record, completion receipt or production commit
from simulator output. Repository inspection is explicitly separate and reads
existing evidence; unsupported capture-to-package/commit operations are disabled.

A short-lived headless service entry point in the same executable owns SQLite
workspace transitions. The desktop invokes it asynchronously as a separate
process and observes snapshots. SQLite transactions and an exclusive workspace
guard serialize commands. Reopening reconciles synthetic active states to
interrupted, never resumed recording or completed. This is NOT a production
capture worker or proof of continued acquisition after a UI crash.

Workspace schema 1 is isolated from repository schemas; no existing repository
migration. Store synthetic subject/session/attempt data and append-only history
in SQLite. Use an explicitly designated empty simulation directory, rejecting
production repository descriptors. Tests use ignored evidence-vault directories
inside HumCapture. Production deployment data placement remains unchanged.

## Assumptions and non-goals

One signed-in Windows operator, one workspace window, small local simulation
workloads, paged history. Background service calls keep the UI responsive.
Required baseline demographics remain represented with synthetic values only.
No real PII, physical device enumeration/capture, Android, protocol authoring,
automatic cleanup, decoder activation, clinical claims, or HIL this batch.
No new CDSCO applicability or claim; preliminary HC-REG-BASE-IND-001 remains
qualified-review pending, not revalidated legal advice.

## Risks and mitigations

HC-RISK-001: context always visible; immutable session/attempt binding.
HC-RISK-006: service authority separate; no physical capture path in UI.
HC-RISK-020: session protocol fixed; retries create new attempt IDs.
HC-RISK-030/031: SQLite transactions, fail-closed version checks, no synthetic
custody claims or production repository initialization.
No completion claim without real required package and session evidence.

## Verification and revisit

Exercise duplicate commands, invalid readiness, fixed-rate compatibility,
preview/source/finalization failures, restart and bounded historical inspection.
Build and headless tests do not establish native accessibility or HIL acceptance.
Revisit the service transport/lifecycle before attaching actual acquisition.

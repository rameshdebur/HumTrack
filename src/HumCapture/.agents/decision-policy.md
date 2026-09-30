# Decision Policy

## Decision classes

- **Implementation detail:** Record in code/tests when local and reversible.
- **Interface decision:** Update the versioned specification and compatibility tests.
- **Architecture decision:** Create or supersede an ADR.
- **Operational decision:** Update project state or known issues.
- **Regulatory/risk decision:** Update the controlled applicability, risk, claims, and evidence records.
- **Exploratory result:** Record as evidence or an open question; do not present it as accepted architecture.

## ADR threshold

Create an ADR when a decision changes a subsystem boundary, cross-component contract, persistence model, security boundary, deployment topology, scientific guarantee, ownership model, or difficult-to-reverse dependency.

Accepted ADRs are historical records. If a decision changes, create a new ADR, mark the old record superseded, and link both.

## Authority

- The Architect accepts architecture decisions.
- The Regulatory/Risk Reviewer assesses applicable policy, risks, and claims.
- A qualified Indian regulatory professional must approve final CDSCO classification or legal interpretation.
- The user must authorize any change outside `src/HumCapture`.


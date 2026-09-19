# HumCapture Component Ownership

Modification authority follows the task's delegated responsibility and the component ownership below. Any agent may read the HumTrack repository. No agent may modify outside `src/HumCapture` without explicit user permission.

## Cross-component change record

Before a cross-component change, record:

- Primary owner.
- Affected owners.
- Paths and public interfaces affected.
- Architecture-review requirement.
- Regulatory, risk, security, or privacy impact.
- Verification owner and evidence required.

## Components

### Architecture and ADRs

- **Primary role:** System Architect
- **Paths:** `docs/architecture/`, `docs/adr/`
- **Public interfaces:** Architecture boundaries and accepted decisions
- **Upstream dependencies:** PRD, requirements, risks, regulatory baseline
- **Downstream dependents:** All implementation components
- **Constraints:** Accepted decisions are superseded through new ADRs, not silently rewritten

### Interface contracts

- **Primary role:** System Architect
- **Affected roles:** Android, coordinator, UVC, simulator, QA
- **Paths:** `docs/interfaces/`, future contract/schema source paths
- **Public interfaces:** Control, transfer, timing, IMU, manifest, receipt, quality, and handoff schemas
- **Upstream dependencies:** Architecture, requirements, risk controls
- **Downstream dependents:** All runtime components and integration tests
- **Constraints:** Versioned, executable where practical, and reviewed before incompatible change

### Android capture application

- **Primary role:** Software Engineer with Android assignment
- **Paths:** `apps/android-capture/`
- **Public interfaces:** Discovery advertisement, control server, preview stream, finalized package, transfer server, completion receipt handling
- **Upstream dependencies:** Android platform APIs, interface contracts, capture protocol
- **Downstream dependents:** Coordinator, repository, downstream package consumers
- **Constraints:** Network, preview, and UI failure cannot compromise scientific master acquisition

### Windows coordinator

- **Primary role:** Software Engineer with coordinator assignment
- **Paths:** `apps/windows-coordinator/`
- **Public interfaces:** Operator workflows and local coordinator-host API
- **Upstream dependencies:** Contracts, subject/protocol requirements, Windows account and platform services
- **Downstream dependents:** Android nodes, UVC workers, repository, operator
- **Constraints:** UI is not the authoritative state machine and must not block capture workers

### UVC capture workers

- **Primary role:** Software Engineer; Windows/UVC specialist when required
- **Paths:** `services/uvc/`
- **Public interfaces:** Capability, configure, arm, start/stop, preview, health, final package
- **Upstream dependencies:** Media Foundation/device drivers, contracts
- **Downstream dependents:** Coordinator and repository
- **Constraints:** Timestamp provenance remains explicit; worker failure is isolated per source

### Timing and synchronization

- **Primary role:** System Architect; timing/performance specialist for implementation
- **Paths:** Future timing contract and implementation paths
- **Public interfaces:** Clock exchanges, fitted models, per-sample global-time mapping, uncertainty
- **Upstream dependencies:** Device and coordinator monotonic clocks
- **Downstream dependents:** Capture coordination, quality assessment, downstream analytics
- **Constraints:** No wall-clock primary timing and no hardware-synchronization claim in MVP

### Transfer and session repository

- **Primary role:** Software Engineer with repository assignment
- **Affected roles:** Architect, Security Reviewer, Regulatory/Risk Reviewer, QA
- **Paths:** Future transfer, persistence, repository, and schema paths
- **Public interfaces:** Resumable HTTPS transfer, USB recovery import, manifests, commit, receipts, export
- **Upstream dependencies:** Identity, protocol, package contracts, Windows storage
- **Downstream dependents:** Quality assessment, cleanup, backup, HumTrack handoff
- **Constraints:** Finalized masters are immutable; commit follows verification; conflicts never overwrite

### Simulator and verification infrastructure

- **Primary role:** Software Engineer or QA as delegated
- **Paths:** `tests/`, future simulator paths
- **Public interfaces:** Contract test vectors, virtual clock, fault injection, hardware evidence
- **Upstream dependencies:** Requirements, contracts, state machines
- **Downstream dependents:** Every component and release gate
- **Constraints:** Synthetic identities/media only in source control; evidence levels reported separately

### User interfaces

- **Primary role:** Software Engineer for the owning application
- **Required specialist:** UX/accessibility specialist during detailed design and review
- **Paths:** Android and Windows UI paths within their owning components
- **Public interfaces:** Trained-operator workflows and accessibility behavior
- **Upstream dependencies:** PRD, user stories, risk controls, HumTrack visual principles
- **Downstream dependents:** Operator performance and error recovery
- **Constraints:** Borrow principles, not HumTrack implementation; no external HumTrack modification

### Risk, compliance, and claims

- **Primary role:** Regulatory and Risk Reviewer
- **Paths:** `docs/compliance/`, `docs/risk/`
- **Public interfaces:** Intended use, applicability, risks, controls, claims, release gates
- **Upstream dependencies:** Current CDSCO/India sources, BIS/international standards, qualified professional review
- **Downstream dependents:** Architecture, requirements, implementation, verification, release
- **Constraints:** Agents cannot confer regulatory approval or invent controlled standard text

### Software supply chain and SBOM

- **Primary role:** Release owner / verification infrastructure engineer
- **Affected roles:** Component engineers, Security Reviewer, QA, Regulatory and Risk Reviewer
- **Paths:** `tools/sbom/`, `sbom/`, `docs/governance/SBOM_POLICY.md`, release-record schema/tooling
- **Public interfaces:** CycloneDX SBOM, SBOM hash/validation fields in release record
- **Upstream dependencies:** Locked package manifests, project files, toolchain/runtime declarations and release version
- **Downstream dependents:** Release gate, vulnerability/VEX response, licence/supplier review, evidence vault and regulatory assessment
- **Constraints:** Regenerate on dependency/build-surface change; explicit known unknowns; SBOM inventory never implies vulnerability, licence, clinical or regulatory approval

### Software lifecycle, configuration, and change control

- **Primary role:** Orchestrator / Release owner
- **Affected roles:** Component owner, Architect, QA, Security Reviewer, Regulatory and Risk Reviewer
- **Paths:** `docs/governance/`, change/review records, future CI and release-control tooling
- **Public interfaces:** Change classification, traceability, AI-assistance declaration, review, verification, configuration baseline, and release gate
- **Upstream dependencies:** Requirements, risks, interfaces, ADRs, standards applicability, component ownership
- **Downstream dependents:** All implementation, verification, evidence, SBOM, and release activities
- **Constraints:** Tool-neutral and risk-based; AI cannot approve its own output; untracked/dirty work cannot become a controlled release

### Project state and handoffs

- **Primary role:** Orchestrator
- **Paths:** `docs/project/`, `.agents/`
- **Public interfaces:** Current project truth, workflow, handoffs, ownership
- **Upstream dependencies:** All component owners and reviewers
- **Downstream dependents:** Future conversations and task planning
- **Constraints:** Concise current state, not a diary; repository artifacts outrank conversational memory

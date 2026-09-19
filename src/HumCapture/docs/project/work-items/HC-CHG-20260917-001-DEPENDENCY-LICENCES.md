# Live dependency licence and cost register

Date: 2026-09-17. User requested a living register covering current HumCapture
packages, licences, potential charges and legal implications before further
application implementation. JsonSchema.Net is selected in principle, not yet
installed; commercial applicability, acceptance/payment and distribution remain
unresolved. No purchase, terms acceptance or runtime activation authorized here.

Classification: local governance and verification tooling; no acquisition,
persistence or public interface change. Primary Release/SBOM engineer; affected
component owners, legal/risk reviewer and QA. Verification owner engineering QA.
Existing SBOM policy and preliminary India baseline apply; no CDSCO, clinical,
legal clearance or changed intended-use claim. Legal conclusions require owner/
qualified counsel review. AI records publisher declarations, not legal approvals.

Deliver a generated readable register backed by reviewed JSON entries and current
SBOM identities. Cover direct/transitive components, first-party licensing gaps,
development/platform tooling, selected-not-installed candidates, costs and actions.
Tests shall detect missing/version-changed entries and stale generated documents;
check current source manifests against retained SBOM through existing generation.
No remote licence/pricing polling implied: refresh on changes and release review.
Keep all modifications under src/HumCapture; existing CI invokes tools/sbom tests
so no external workflow write is needed.

Implemented: generated HC-GOV-LIC-001 register and JSON review data; policy 1.1;
five new consistency tests integrated into the existing SBOM test command.
All 12 local SBOM/register tests pass. Evidence and unresolved review items:
[HC-VR-LIC-001](../../verification/DEPENDENCY_LICENSE_REGISTER_REPORT.md).
This completes the engineering register, not commercial/legal approval.

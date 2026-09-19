# C14R next-sprint implementation

User requested implementation of the next sprint. Scope: ADR-0035 leased
package-to-pinned-decoder integration, internal only. Cross-component design
review is recorded in ADR-0035 before source changes. Existing baselined
requirements and governance apply; no changed regulatory applicability/claim.
Software engineer owns repository code, QA owns evidence, Release owns SBOM.
AI-assisted implementation/review is not independent human approval.

Acceptance: preserve existing synchronous observation path and its unassessed
provenance; actual pinned worker succeeds only with matching admitted bytes;
decode failure cannot return package evidence; missing master remains partial;
cancellation/failure releases leases. Run build, regression, real synthetic
integration and update traceability, risk, SBOM and project state. C14S/T and
production activation are not included in this single-sprint completion.

Implementation and focused local evidence: HC-VR-I0-4B-C14R-001. Release build
clean; 12 focused runtime groups, four real synthetic-media cases, 111 contract
and 13 SBOM/register tests pass. Full hosted regression remains SHA-bound.
Independent human review and controlled release remain open.

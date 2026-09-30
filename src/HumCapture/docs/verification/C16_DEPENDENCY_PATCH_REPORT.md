# C16 dependency-audit follow-up

2026-09-30. Initial UI source cbdfabbc724a5bee7f44b276d46e547bc9b9089c passed
hosted contract/runtime and SBOM stages but push36657167077 failed the existing
Node audit. No desktop source, acquisition behavior or parent workflow changed.

Cause: existing fast-uri3.1.6 transitive Ajv dependency matched high advisories:
- https://github.com/advisories/GHSA-qw65-cvwx-89v3
- https://github.com/advisories/GHSA-58mr-gqgx-xq4g

Both scoped tools now override fast-uri to patched3.1.7, retaining Ajv8.20.0.
The exact package licence is BSD-3-Clause as before; full installed LICENSE
inspected, notices still required, no new mandatory copyright fee identified.
This does not provide legal/distribution or security approval.

Affected validation rerun:
- Probe evidence tool: 55/55; npm audit reports zero vulnerabilities.
- Evidence contracts: 115/115; npm audit reports zero vulnerabilities.
- SBOM/register: 13/13.

Raw proof: C16_PATCH_PHASE0_RESULTS.txt, C16_PATCH_CONTRACT_RESULTS.txt,
C16_PATCH_SBOM_RESULTS.txt. Existing217 .NET/UI results remain applicable to
unchanged .NET source, and hosted exact-SHA checks rerun the full pipeline.

Current SBOM 0.1.0-i0.4b-c16c.1 has56 components plus product and57 graph nodes.
SHA-256 c21b6a580fa5cd760028a4cb66d35912f42bd61702b277bffdaf01d33e72764a.
Earlier c16c SBOM hash/results remain historical in C16_UI_VERIFICATION_REPORT.
No audit gate suppression. HIL remains deferred.

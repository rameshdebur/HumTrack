# Dependency licence register verification

Report: HC-VR-LIC-001. Date: 2026-09-17.
Change: HC-CHG-20260917-001. Control: HC-GOV-SBOM-001 revision 1.1;
HC-GOV-LIC-001. Risk linkage: HC-RISK-018.
Verification owner: engineering QA. Legal/commercial approval: NOT established.

## Scope and baseline

HumCapture only. Register records 33 retained SBOM components plus product,
one selected-not-installed candidate and 13 supplemental tooling/composition
items. Source/lockfile inventory is not a complete shipping-binary audit.
No package installed, commercial agreement accepted, acquisition code changed,
camera accessed, or runtime decoder activated in this change.

Source baseline before this change: 0acc42526875cf7a1e040b9dc46e71931739f657.
Retained SBOM version: 0.1.0-i0.4b-c14e; generated 2026-09-16T13:31:01.932Z.
Unchanged SBOM SHA-256:
`4669d7f3f619e5ce1543aa8b76081168c2ae675fc845b76223fff5240c305f53`.
This governance update does not represent a new application release.

## Local verification

Commands from HumCapture, using installed Node and npm.cmd:

```text
node tools/sbom/src/licence-register.js --write
npm.cmd --prefix tools/sbom test
node tools/sbom/src/licence-register.js --check
git diff --check
```

Result: 12 tests passed, zero failed (five new register tests and seven existing
SBOM tests). Register check: 34 identities, one candidate. Diff whitespace check
passed. Raw evidence: [test results](DEPENDENCY_LICENSE_REGISTER_RESULTS.txt).

| Objective | Evidence / outcome |
|---|---|
| Cover every exact SBOM identity and keep rendered document current | Passing coverage/render comparison |
| Reject unreviewed additions, removals, upgrades and duplicates | Passing negative tests |
| Require costs, obligations, evidence and review state | Passing missing-field/profile tests |
| Reconcile selected candidate when installed | Passing synthetic-candidate transition rejection |
| Detect stale SBOM against source manifests | Fresh generation equals retained SBOM; register validates |
| Preserve existing inventory controls | Seven SBOM tests pass, including graph closure, deterministic output and manifest/pin controls |

The existing scoped CI invokes this same SBOM test command; no out-of-scope
workflow changes are needed. Hosted results for this change are not asserted by
this local report. Previous application baseline CI is recorded in project state.

## Review evidence and limitations

Publisher review date is recorded in the register. Exact NuGet versions were
checked against publisher nuspec licence declarations; npm lockfile and pinned
Action licence references are retained. Publisher legal/pricing links accompany
profiles. These references are declarations and review inputs, not a retained
complete licence/notice bundle or legal opinion. Pricing must be refreshed before
commercial acceptance; no total cost estimate or entitlement claim is made.

Remaining work is explicitly tracked: JsonSchema.Net commercial applicability,
FFmpeg redistribution and embedded codecs/libraries, exact native SQLite revision,
local and hosted tool composition/entitlements, historical PATH binary provenance,
and first-party producer/licensing decisions. A qualified reviewer must resolve
legal interpretation where required. No CDSCO or certification claim follows.

Evidence levels: source/tooling implemented; automated behaviour verified locally.
Application runtime, hardware, field workflow, regulatory/clinical review and
release legal clearance were not performed by this change.

# HumCapture Dependency, Licence and Cost Register

HC-GOV-LIC-001 | Living engineering register | NOT legal/distribution approval

Publisher/metadata review date: 2026-09-17. Owner: Release/SBOM owner; package owner updates; commercial/legal owner approves terms.

Coverage: 37 SBOM components plus HumCapture product; 0 selected-not-installed candidate(s); 13 supplemental tooling/composition items.
SBOM baseline: 0.1.0-i0.4b-c14h, generated 2026-09-17T12:00:00.000Z.

Scope is src/HumCapture, not the wider HumTrack repository or all software installed on this machine. Direct and transitive package identities follow the SBOM dependency graph. This is source/lockfile coverage, NOT a complete binary-composition audit.

## Read this first

- Gyan FFmpeg 9.0.1 is engineering-only, runtime disabled, redistribution unapproved. GPL/build notices and codec patent questions remain open.
- The pinned Sonar analyzer declares LGPL-3.0-only. It is build tooling, not an intended application runtime component.
- No mandatory licence fee identified is NOT no legal obligations, no total cost, or clearance to distribute. Fees exclude tax/FX and optional support unless stated.
- No total project cost is calculated: organization size/revenue, Windows/tool entitlements, hosting allowances and patent applicability are unresolved. No CDSCO, certification or legal approval follows.

## Licence and charge profiles

Entries below link to these shared profiles. Summaries describe publisher terms and review tasks, not counsel's conclusions. All engineering declarations still need appropriate release review.

### MIT

Licence: MIT.

Potential charges: No mandatory copyright-licence fee stated; optional support/services are separate.

Obligations / implications: Retain copyright and permission notices with distributed copies/substantial portions. No warranty. Assess bundled third-party notices separately.

Sources: [1](https://opensource.org/license/mit).

### BSD-3-Clause

Licence: BSD-3-Clause.

Potential charges: No mandatory copyright-licence fee stated.

Obligations / implications: Retain copyright, conditions and disclaimer in source/binary distribution; do not imply endorsement by contributors.

Sources: [1](https://opensource.org/license/bsd-3-clause).

### Apache-2.0

Licence: Apache-2.0.

Potential charges: No mandatory copyright-licence fee stated; paid support is optional.

Obligations / implications: Supply licence; preserve relevant attribution/NOTICE; mark modified files. Patent grant has conditions; no trademark licence or warranty. Check package contents before redistribution.

Sources: [1](https://www.apache.org/licenses/LICENSE-2.0).

### LGPL-3.0-only

Licence: LGPL-3.0-only (pinned analyzer package declaration).

Potential charges: No mandatory copyright-licence fee identified; separate Sonar commercial services not included.

Obligations / implications: Build-only use is not the same as shipping this library. Do not assume analyzer use makes all application code LGPL. If distributing/modifying/linking the analyzer, review LGPL/GPL notice, source and relinking obligations with counsel.

Sources: [1](https://licenses.nuget.org/LGPL-3.0-only), [2](https://www.gnu.org/licenses/lgpl-3.0.html).

### Gyan-GPL

Licence: Publisher-declared GPLv3 build; embedded components not fully inventoried.

Potential charges: No decoder subscription identified. Codec patent/royalty exposure and compliance effort are separate, unresolved costs.

Obligations / implications: Distribution blocked pending matching source/build/configuration and embedded-library notice review. Running a separate process is not by itself legal clearance; application/decoder relationship needs review. Evaluate codec patents for intended territories/use. Do not relabel this build LGPL.

Sources: [1](https://www.gyan.dev/ffmpeg/builds/), [2](https://ffmpeg.org/legal.html).

### Windows

Licence: Microsoft proprietary product/SDK terms; exact entitlement review open.

Potential charges: Windows/toolchain entitlement or subscription may cost money; actual edition, agreement and amount unknown.

Obligations / implications: Verify licensed host and applicable SDK/redistributable terms; installed SDK/API availability is not redistribution permission. Do not ship arbitrary OS/SDK binaries.

Sources: [1](https://www.microsoft.com/useterms), [2](https://visualstudio.microsoft.com/license-terms/), [3](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/).

### Dotnet

Licence: .NET open-source components (MIT plus third-party notices).

Potential charges: Microsoft states no .NET licence charge, including commercial use; support and host licences separate.

Obligations / implications: Retain notices for redistributed runtime/SDK portions. Record actual deployed patch and deployment method; framework minimum is not a measured installed version.

Sources: [1](https://dotnet.microsoft.com/en-us/platform/free), [2](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [3](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT).

### First-party

Licence: HumCapture first-party licence/producer rights not baselined.

Potential charges: Not a third-party package fee; producer commercial terms and legal-review costs not determined.

Obligations / implications: Owner must establish producer identity, contributor rights and distribution licence before release. Repository visibility is not permission to apply an open-source licence.

Sources: [1](SBOM_POLICY.md).

### JsonSchema-binary

Licence: MIT source; published binary OSMF agreement requires separate consideration.

Potential charges: Publisher tiers checked 2026-09-17: USD 10/month (<20 employees), 25/month (20-100), 50/month (>100), conditional on applicability. Annual equivalents 120/300/600; tax/FX excluded. Not a quote or accepted purchase.

Obligations / implications: User authorized the published-package engineering route on 2026-09-17 after the terms decision. This is not evidence of payment or fee exemption. Owner must establish applicable maintenance entitlement for use and release. JsonSchema.Net, JsonPointer.Net and Json.More.Net contain the identical OSMF agreement (SHA-256 5c805ac94dfdb4a3be55547f04a2f4b9f1bb87d7e9ee6d6fe54b0e72093900c3). Retain MIT notices and agreement; review each resolved dependency. No purchase made.

Sources: [1](https://www.nuget.org/packages/JsonSchema.Net/9.4.0/License), [2](https://github.com/sponsors/gregsdennis).

### Node

Licence: MIT core plus bundled third-party licences.

Potential charges: No mandatory Node copyright-licence fee identified; hosting/support separate.

Obligations / implications: Preserve bundled notices if redistributed; track exact tool version. Node is development tooling here, not an approved Coordinator runtime.

Sources: [1](https://github.com/nodejs/node/blob/v22.12.0/LICENSE).

### npm

Licence: Artistic-2.0 CLI plus bundled licences; registry service terms separate.

Potential charges: CLI licence not a package-use fee; paid registry/organization features may incur charges if selected.

Obligations / implications: Retain applicable notices; distinguish local CLI licensing from registry account terms. Exact CLI dependency inventory not included in current application SBOM.

Sources: [1](https://github.com/npm/cli/blob/latest/LICENSE), [2](https://docs.npmjs.com/policies/terms).

### Git

Licence: Git GPLv2; Git for Windows distribution contains additional components.

Potential charges: No mandatory Git licence fee identified.

Obligations / implications: Development tool only; redistribution of Git/tool bundles requires their own notices/source review. Not an application runtime dependency.

Sources: [1](https://git-scm.com/about/free-and-open-source).

### GitHub-service

Licence: GitHub service terms; separate from MIT-licensed action code.

Potential charges: Runner minutes/storage/caches may cost money according to plan, repository visibility and runner type. Account entitlements and actual charges not inspected.

Obligations / implications: Review organization billing and data/service terms; do not confuse free action source with free hosted execution. No new paid service authorized.

Sources: [1](https://docs.github.com/en/billing/concepts/product-billing/github-actions), [2](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service).

### Chocolatey

Licence: Chocolatey CLI Apache-2.0; hosted service/business terms and installed package EULAs separate.

Potential charges: Community CLI versus optional business offerings; SDK package does not establish Microsoft entitlement. Actual CLI edition unknown.

Obligations / implications: Check installer scripts/package provenance and upstream software terms. Tooling only, not application redistribution.

Sources: [1](https://github.com/chocolatey/choco/blob/develop/LICENSE), [2](https://chocolatey.org/terms).

### SQLite-native

Licence: SQLite upstream public-domain dedication; wrapper packages separately Apache-2.0.

Potential charges: No upstream SQLite licence fee required; optional warranty-of-title/support may cost money.

Obligations / implications: Confirm exact embedded native SQLite revision and provenance; public-domain recognition/indemnity needs qualified review if relevant. Do not confuse NuGet bundle version with SQLite engine version.

Sources: [1](https://www.sqlite.org/copyright.html).

### Unknown

Licence: Unresolved exact binary/licence composition.

Potential charges: Unknown, not zero.

Obligations / implications: Identify exact source/version, terms and bundled components before distribution approval.

Sources: [1](SBOM_POLICY.md).

## Current SBOM inventory

Review states are recorded per row; a declaration is not legal approval. Versions and commit identities are exact SBOM identities, except explicitly host-resolved components. Node package scope means required by development tooling, not necessarily shipped in the Coordinator.

| Component / version | Use | Licence / cost profile | Evidence | Outstanding action |
|---|---|---|---|---|
| HumTrack/HumCapture **0.1.0-i0.4b-c14h** | First-party acquisition subsystem | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| windows-sdk-10-version-2004-all **10.0.19041.685** | Build-time SDK/installer | [Windows](#windows) | [1](https://www.microsoft.com/useterms) [2](https://visualstudio.microsoft.com/license-terms/) [3](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Check exact Microsoft entitlement/redistributable terms; host composition remains open. |
| FFmpeg Windows essentials archive **9.0.1** | Local engineering only; runtime disabled; redistribution not approved | [Gyan-GPL](#gyan-gpl) | [1](../../apps/windows-coordinator/decoder/decoder-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Close embedded-library, corresponding-source and distribution review before activation/shipping. |
| HumCapture/HumCapture CI **0.1.0-i0.4b-c14h** | Development/test/CI | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.Coordinator.Host **0.1.0** | First-party acquisition subsystem | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.Coordinator.Repository.SelfTest **0.1.0** | Development/test/CI | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.Coordinator.Repository **0.1.0** | First-party acquisition subsystem | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.ManagedCameraProbe **0.1.0-p0** | Exploratory/test tooling | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.MfCapture **0.1.0-p0** | Exploratory/test tooling | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture.MfEnumerator **0.1.0-p0** | Exploratory/test tooling | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/HumCapture SBOM Tool **0.1.0** | Development/test/CI | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| Microsoft.NETCore.App **10.0.0** | Host-resolved framework minimum; actual patch not inventoried | [Dotnet](#dotnet) | [1](https://dotnet.microsoft.com/en-us/platform/free) [2](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) [3](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Record deployed runtime patch and notices before release. |
| Microsoft.NETCore.App **8.0.0** | Host-resolved framework minimum; actual patch not inventoried | [Dotnet](#dotnet) | [1](https://dotnet.microsoft.com/en-us/platform/free) [2](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) [3](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Record deployed runtime patch and notices before release. |
| Microsoft Windows Media Foundation, COM/OLE and Shell runtime APIs **host-provided** | Host-provided runtime APIs | [Windows](#windows) | [1](https://www.microsoft.com/useterms) [2](https://visualstudio.microsoft.com/license-terms/) [3](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Check exact Microsoft entitlement/redistributable terms; host composition remains open. |
| Microsoft Windows SDK **10.0.19041.0** | Build-time SDK/installer | [Windows](#windows) | [1](https://www.microsoft.com/useterms) [2](https://visualstudio.microsoft.com/license-terms/) [3](https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Check exact Microsoft entitlement/redistributable terms; host composition remains open. |
| actions/checkout **v6.0.2** | CI action source; hosted execution billed separately | [MIT](#mit) | [1](https://github.com/actions/checkout/blob/de0fac2e4500dabe0009e67214ff5f5447ce83dd/LICENSE) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if redistributing action; review service costs separately. |
| actions/setup-dotnet **v5.2.0** | CI action source; hosted execution billed separately | [MIT](#mit) | [1](https://github.com/actions/setup-dotnet/blob/c2fa09f4bde5ebb9d1777cf28262a3eb3db3ced7/LICENSE) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if redistributing action; review service costs separately. |
| actions/setup-node **v7.0.0** | CI action source; hosted execution billed separately | [MIT](#mit) | [1](https://github.com/actions/setup-node/blob/820762786026740c76f36085b0efc47a31fe5020/LICENSE) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if redistributing action; review service costs separately. |
| HumCapture/@humcapture/evidence-control **0.1.0** | Development/test/CI | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| HumCapture/@humcapture/phase0-evidence **0.1.0** | Exploratory/test tooling | [First-party](#first-party) | [1](SBOM_POLICY.md) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Baseline producer/distribution licence before release; not covered by third-party permission. |
| ajv-formats **3.0.1** | Node development/contract-test dependency (not Coordinator runtime) | [MIT](#mit) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| ajv **8.20.0** | Node development/contract-test dependency (not Coordinator runtime) | [MIT](#mit) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| fast-deep-equal **3.1.3** | Node development/contract-test dependency (not Coordinator runtime) | [MIT](#mit) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| fast-uri **3.1.6** | Node development/contract-test dependency (not Coordinator runtime) | [BSD-3-Clause](#bsd-3-clause) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| json-schema-traverse **1.0.0** | Node development/contract-test dependency (not Coordinator runtime) | [MIT](#mit) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| require-from-string **2.0.2** | Node development/contract-test dependency (not Coordinator runtime) | [MIT](#mit) | [1](../../tools/evidence-control/package-lock.json) [2](../../tools/capability-probes/shared/package-lock.json) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain notices if tooling is distributed; declaration from locked npm metadata. |
| Humanizer.Core **3.0.10** | Coordinator internal metadata validation; resolved published package in locked build | [MIT](#mit) | [1](https://api.nuget.org/v3-flatcontainer/humanizer.core/3.0.10/humanizer.core.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain MIT notices at redistribution. |
| Json.More.Net **3.0.1** | Coordinator internal metadata validation; resolved published package in locked build | [JsonSchema-binary](#jsonschema-binary) | [1](https://api.nuget.org/v3-flatcontainer/json.more.net/3.0.1/json.more.net.nuspec) [2](https://www.nuget.org/packages/Json.More.Net/3.0.1/License) | USER_AUTHORIZED_ENGINEERING_PAYMENT_OR_EXEMPTION_NOT_EVIDENCED: Owner records applicable fee/support entitlement or exemption; retain licence/notice bundle before release. |
| JsonPointer.Net **7.0.2** | Coordinator internal metadata validation; resolved published package in locked build | [JsonSchema-binary](#jsonschema-binary) | [1](https://api.nuget.org/v3-flatcontainer/jsonpointer.net/7.0.2/jsonpointer.net.nuspec) [2](https://www.nuget.org/packages/JsonPointer.Net/7.0.2/License) | USER_AUTHORIZED_ENGINEERING_PAYMENT_OR_EXEMPTION_NOT_EVIDENCED: Owner records applicable fee/support entitlement or exemption; retain licence/notice bundle before release. |
| JsonSchema.Net **9.4.0** | Coordinator internal metadata validation; resolved published package in locked build | [JsonSchema-binary](#jsonschema-binary) | [1](https://api.nuget.org/v3-flatcontainer/jsonschema.net/9.4.0/jsonschema.net.nuspec) [2](https://www.nuget.org/packages/JsonSchema.Net/9.4.0/License) | USER_AUTHORIZED_ENGINEERING_PAYMENT_OR_EXEMPTION_NOT_EVIDENCED: Owner records applicable fee/support entitlement or exemption; retain licence/notice bundle before release. |
| Microsoft.CodeAnalysis.NetAnalyzers **8.0.0** | Build-time analyzer; not intended for shipping | [MIT](#mit) | [1](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.netanalyzers/8.0.0/microsoft.codeanalysis.netanalyzers.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| Microsoft.Data.Sqlite.Core **10.0.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [MIT](#mit) | [1](https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite.core/10.0.12/microsoft.data.sqlite.core.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| Microsoft.Data.Sqlite **10.0.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [MIT](#mit) | [1](https://api.nuget.org/v3-flatcontainer/microsoft.data.sqlite/10.0.12/microsoft.data.sqlite.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| SonarAnalyzer.CSharp **9.32.0.97167** | Build-time analyzer; not intended for shipping | [LGPL-3.0-only](#lgpl-30-only) | [1](https://api.nuget.org/v3-flatcontainer/sonaranalyzer.csharp/9.32.0.97167/sonaranalyzer.csharp.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| SQLitePCLRaw.bundle_e_sqlite3 **2.1.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [Apache-2.0](#apache-20) | [1](https://api.nuget.org/v3-flatcontainer/sqlitepclraw.bundle_e_sqlite3/2.1.12/sqlitepclraw.bundle_e_sqlite3.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| SQLitePCLRaw.core **2.1.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [Apache-2.0](#apache-20) | [1](https://api.nuget.org/v3-flatcontainer/sqlitepclraw.core/2.1.12/sqlitepclraw.core.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |
| SQLitePCLRaw.lib.e_sqlite3 **2.1.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [Apache-2.0](#apache-20) | [1](https://api.nuget.org/v3-flatcontainer/sqlitepclraw.lib.e_sqlite3/2.1.12/sqlitepclraw.lib.e_sqlite3.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Retain package notices; identify embedded SQLite version/source before release. |
| SQLitePCLRaw.provider.e_sqlite3 **2.1.12** | Coordinator runtime direct/transitive dependency; see SBOM graph | [Apache-2.0](#apache-20) | [1](https://api.nuget.org/v3-flatcontainer/sqlitepclraw.provider.e_sqlite3/2.1.12/sqlitepclraw.provider.e_sqlite3.nuspec) | DECLARATION_RECORDED_NOT_LEGAL_APPROVAL: Verify notices against resolved package archive before redistribution. |

## Selected but not installed

| Item / version | Use | Licence / costs | Status and next action |
|---|---|---|---|

## Supplemental tools and unresolved composition

| Item / version | Use | Licence / costs | Status and next action |
|---|---|---|---|
| Node.js — CI 22.12.0; local version not qualified here | CI/development | [Node](#node) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: CI workflow pins Node; no production JavaScript runtime authorized. |
| .NET SDK — CI 10.0.303; local build version separately recorded | Build tool | [Dotnet](#dotnet) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: CI workflow pin; SDK bundled components not fully inventoried by current SBOM. |
| Visual Studio / MSBuild / MSVC — CI windows-2025-vs2026; native toolset v145; exact installed build/edition unresolved | Native build toolchain | [Windows](#windows) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Check edition eligibility and compiler/runtime redistributables before release; do not infer Community eligibility. |
| npm CLI — CI bundled with Node; exact CLI version unresolved | Dependency installation/test tooling | [npm](#npm) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Record precise release-build CLI version and applicable terms. |
| PowerShell — CI pwsh / local shell; exact version unresolved | Build/test shell | [MIT](#mit) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: PowerShell 7 source is MIT; distinguish Windows PowerShell OS terms and bundled components before redistribution. |
| Git / Git for Windows — Exact local/runner version unresolved | Source-control tooling | [Git](#git) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Not shipped with HumCapture; version and bundled licence scan open. |
| GitHub CLI — Exact local version unresolved | Repository/CI management tool | [MIT](#mit) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Source MIT; GitHub service/billing terms separate. |
| GitHub Actions hosting — Plan, runner billing class and entitlements unresolved | External build service | [GitHub-service](#github-service) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Read-only cost assessment only; not permission to enable paid usage. |
| Chocolatey CLI — CI host-provided version/edition unresolved | Build installer | [Chocolatey](#chocolatey) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Pinned SDK installer is already in the SBOM; CLI/service separate. |
| CycloneDX CLI — 0.33.1 local validation tool; binary provenance review pending | SBOM format validation | [Apache-2.0](#apache-20) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Local tool path hc-cyclonedx-0331; not a shipped application library. |
| Embedded SQLite engine — Native revision unresolved inside SQLitePCLRaw.lib.e_sqlite3 2.1.12 | Indirect runtime binary | [SQLite-native](#sqlite-native) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Resolve native version and build provenance before binary-composition closure. |
| Historical PATH FFmpeg / ffprobe — Earlier probe evidence reports 7.1.1; current PATH identity not requalified | Exploratory probes only | [Unknown](#unknown) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Not the pinned 9.0.1 decoder; identify actual build/licence before any reuse. Never silently promote PATH binaries. |
| FFmpeg embedded libraries/codecs — Exact versions and complete notices unresolved | Embedded engineering decoder dependencies | [Gyan-GPL](#gyan-gpl) | INVENTORY_GAP_OR_TOOLING_NOT_LEGAL_APPROVAL: Archive identity does not enumerate embedded licences; detailed binary composition remains open. |

## Maintenance and approval

1. For every added, removed, upgraded, re-scoped or redistributed dependency: regenerate the SBOM, review exact package/version terms and transitive dependencies, then update sbom/dependency-licences.json in the same change.
2. Refresh prices/applicability sources when selecting/upgrading a package and before release. Preserve review dates and decisions in version control. This document does not poll prices or accept terms automatically.
3. Regenerate this document with `node tools/sbom/src/licence-register.js --write`. Do not hand-edit generated tables. Run `npm.cmd test --prefix tools/sbom`.
4. Existing CI runs these tests: missing/version-changed entries, changed manifests against retained SBOM, and stale rendered documentation fail. First-party version changes also require reconciliation. Machine checks establish inventory consistency, not legal correctness or completeness of embedded binaries.
5. Package owners propose updates; Release/SBOM owner maintains coverage; commercial owner confirms fees/entitlements; qualified counsel reviews unresolved legal/distribution questions. Record approval separately with reviewer, date, scope and evidence. AI does not approve its own licence analysis.
6. Before shipping: complete third-party notice/licence bundle, exact binary composition, applicable corresponding-source obligations, paid entitlement evidence where needed, and a release-specific review. Internal tool use and application redistribution remain separate.

Not yet complete: embedded FFmpeg libraries, native SQLite revision, runner/SDK bundled tools, historical PATH binaries, exact local tool versions and producer licensing. Android/iOS/NDI SDK packages are not represented as installed; new manifests must enter the SBOM and this register when introduced.

Authoritative inputs: [SBOM](../../sbom/humcapture.cdx.json), [review data](../../sbom/dependency-licences.json), [SBOM policy](SBOM_POLICY.md).

# HumCapture SBOM Tool

Generates a deterministic CycloneDX 1.7 SBOM from every current HumCapture
Node/NuGet package lock and managed/native project file, plus the scoped
repository CI workflow, its immutable GitHub Action commit pins, and exact
Chocolatey packages installed by that workflow. NuGet lock parsing includes
transitive packages and content hashes. The generator uses only Node.js
built-ins so it does not introduce another third-party dependency surface.

```powershell
npm.cmd test
node src/cli.js generate --repo ../.. --output ../../sbom/humcapture.cdx.json --version 0.1.0-p0.2j --timestamp 2026-08-31T00:00:00.000Z
node src/cli.js validate --input ../../sbom/humcapture.cdx.json
```

Release generation must supply the approved release timestamp rather than an
implicit current time. This makes regeneration and review deterministic.

Project validation checks HumCapture policy. Release validation must additionally
use the official CycloneDX CLI against specification version 1.7.

## Living licence and cost register

From the HumCapture root:

```powershell
node tools/sbom/src/licence-register.js --write
node tools/sbom/src/licence-register.js --check
npm.cmd test --prefix tools/sbom
```

Review/edit `sbom/dependency-licences.json`; Markdown is generated at
`docs/governance/DEPENDENCY_LICENSE_REGISTER.md`. Package/version changes require
reviewed entries and SBOM regeneration. Tests also regenerate the SBOM into a
temporary test directory to detect stale source-manifest coverage. Existing CI
invokes these tests without workflow changes. No new package dependency introduced.
Coverage checks are not legal approval or a price-monitoring service.

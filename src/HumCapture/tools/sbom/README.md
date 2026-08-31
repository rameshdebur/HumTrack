# HumCapture SBOM Tool

Generates a deterministic CycloneDX 1.7 SBOM from every current HumCapture
package lock and Windows probe project file. It uses only Node.js built-ins so
the SBOM generator does not introduce another third-party dependency surface.

```powershell
npm.cmd test
node src/cli.js generate --repo ../.. --output ../../sbom/humcapture.cdx.json --version 0.1.0-p0.2j --timestamp 2026-08-31T00:00:00.000Z
node src/cli.js validate --input ../../sbom/humcapture.cdx.json
```

Release generation must supply the approved release timestamp rather than an
implicit current time. This makes regeneration and review deterministic.

Project validation checks HumCapture policy. Release validation must additionally
use the official CycloneDX CLI against specification version 1.7.

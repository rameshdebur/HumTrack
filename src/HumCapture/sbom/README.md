# HumCapture SBOM

`humcapture.cdx.json` is the current generated CycloneDX 1.7 engineering SBOM.
Its adjacent `.sha256` file records the exact document hash.

Regenerate it through `tools/sbom`, validate it with both the project validator
and official CycloneDX CLI, and attach its hash to every release record. Do not
edit the generated JSON manually.

The current document is retrospective engineering inventory. It is not a signed
release SBOM, vulnerability report, licence approval, VEX document, or CDSCO
conformity statement.

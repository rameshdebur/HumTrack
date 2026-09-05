# HumCapture control schemas v1

These JSON Schema 2020-12 files are the executable transport-neutral control
slice of `HC-IF-CTRL-001` version `1.1.0`. Individual record schemas start at
schema version `1.0.0`. `../../../asyncapi/control-v1.asyncapi.json`
describes the logical message channels using AsyncAPI 3.1.0.

The schemas deliberately reject unknown properties and unsupported contract
versions. Cross-record rules that JSON Schema cannot express, including source
count bounds, immutable post-capture snapshot binding, unsigned 64-bit range,
clock/boot continuity, command replay, readiness aggregate truth, authoritative
source transitions, custody/receipt binding, quality false-pass rejection, and
completion/handoff consistency, are enforced by the non-production conformance
oracle and fixtures under `tools/evidence-control`.

For a record with a `*_content_sha256` identity, hash the UTF-8 bytes of its RFC
8785 canonical JSON after omitting that record's own content-hash property.
Encode SHA-256 as lowercase 64-character hexadecimal. References to another
record's hash remain in the content and therefore bind that dependency. See
ADR-0011 and HC-CTRL-TEST-019.

Transport/authentication binding, transfer endpoints, media/package manifests,
receipt signing, timing/IMU binary streams, and application implementation are
not defined by this slice.

## Specification references

- AsyncAPI Specification 3.1.0:
  `https://www.asyncapi.com/docs/reference/specification/v3.1.0`
- JSON Schema Core 2020-12:
  `https://json-schema.org/draft/2020-12/json-schema-core`

One-off validation with published `@asyncapi/cli` 6.0.2 passed for the AsyncAPI
document and referenced schemas. That CLI declared Node.js 24 while the current
HumCapture CI runtime is Node.js 22.12.0, so the successful run is retained as a
local format check and the CLI is not added to the project dependency baseline.

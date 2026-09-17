# Decision required — Coordinator metadata validation dependency

Status: JsonSchema.Net approach selected by user 2026-09-17; NOT INSTALLED.
Commercial terms/applicability, payment and distribution approval remain open.
User requested the living licence/cost register before implementation resumes;
see HC-CHG-20260917-001 and docs/governance/DEPENDENCY_LICENSE_REGISTER.md.
The following comparison is retained as decision history, not a new request to
choose the library again. Source-build versus publisher-binary entitlement is not
resolved by technical selection alone.

The next C14 integration step must validate the accepted draft-2020-12 timing,
camera, IMU, event and finalization schemas before semantic verification. Existing
Coordinator admission uses hand-written manifest/verification checks; Ajv is only
in development tooling. Neither a new .NET dependency nor deploying Node/Ajv as
part of the Coordinator follows automatically from this batch authority.

Preferred engineering approach: use a maintained .NET schema implementation with
locally embedded, versioned schemas and explicitly enabled required format checks.
Keep network retrieval disabled; compare against the existing Ajv positive/negative
fixtures. Preserve independent byte/identity/semantic validation and never treat
schema validity alone as scientific verification.

Candidate JsonSchema.Net 9.4.0 supports draft 2020-12 and lists JsonPointer.Net
>=7.0.2 for net10.0. Its actual NuGet nuspec requires licence acceptance and names
OSMFEULA.txt, not just the source repository's MIT licence. The published binary
agreement includes maintenance-fee terms for qualifying revenue-generating users.
Do not assume an exemption or accept these terms on the user's behalf. No package
has been installed or added to the SBOM; this is candidate research only.

Options requiring user direction:

1. Authorize this .NET dependency approach, with package terms reviewed/accepted by
   the owner before use, then pin/lock, inventory and test the exact dependencies.
2. Keep the MVP dependency-free and extend explicit C# profile validators, with
   differential tests against all controlled schemas. This avoids the new package
   but duplicates schema rules and adds maintenance work; it is not a general
   JSON Schema engine and must not be described as one.

Self-building MIT sources is another possible route, but adds a supplier/build
maintenance obligation; not silently selected to avoid the binary agreement.
Decoder distribution/licence review remains a separate existing open gate.

Primary sources (accessed 2026-09-16):
- https://docs.json-everything.net/schema/basics/
- https://www.nuget.org/packages/JsonSchema.Net/9.4.0
- https://api.nuget.org/v3-flatcontainer/jsonschema.net/9.4.0/jsonschema.net.nuspec
- https://www.nuget.org/packages/JsonSchema.Net/9.4.0/License

This records technical options and publisher terms, not legal advice or approval.

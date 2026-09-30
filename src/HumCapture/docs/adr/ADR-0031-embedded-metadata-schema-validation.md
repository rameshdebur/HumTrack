# ADR-0031 — Embedded metadata schema validation

2026-09-17. Accepted for user-authorized engineering; independent review open.
Primary Coordinator/Architect; affected timing, Android/UVC, QA, Risk, Release/SBOM.

Choose published JsonSchema.Net 9.4.0, exact locked dependencies, for the five
versioned timing/camera/IMU/event/finalization schemas and their common definitions.
Embed controlled schema bytes in the Coordinator assembly. Callers choose only
known artifact kinds, never schema URLs supplied by packages. Build a local
registry; disallow fetching missing references. Require format validation.
Keep schema checks internal and separate from canonical-byte, identity, hash,
binary, timing and decoder checks. Schema success cannot produce VERIFIED.

Alternative explicit C# validators duplicate schema rules; shipping Node/Ajv adds
an unnecessary runtime; self-building MIT sources adds supplier build maintenance.
The published package is simpler but brings its binary agreement and versioned
dependency obligations. User authorized this route, not a purchase or proven fee
exemption. Record actual transitive licences in the living register.

Use fixed local schemas and shared fixtures against development Ajv to reduce
engine divergence. Reject duplicate object keys, malformed/deep JSON and oversized
metadata before evaluation. A bounded internal metadata envelope is an engineering
resource limit, not a protocol duration or camera cadence requirement. Fail
explicitly instead of truncating. Revisit limits before field activation.

Implemented envelope: 16 MiB per JSON document, depth 64, serialized evaluations
per validator. Cancellation is checked around third-party evaluation, not a hard
interrupt inside it. Clone embedded schema elements because compiled schemas retain
their backing JSON data. No arbitrary instance-provided schema is loaded.

C14H checks frame/metadata identity, declared clock references/models and bounds,
counts, coded dimensions and exact presentation association. It does not validate
all provenance, codec aliases, lens/rotation changes, cadence summaries or IMU/event
semantics. Model-value recomputation remains false pending the quantization decision.
No selected/advertised FPS is used to synthesize evidence or reject cadence classes.

The architecture skill informed this narrow boundary and explicit trade-offs.
HC-DATA-REQ-001/002/009 and HC-RISK-018/022/030/031 apply. No contract version,
host deployment, acquisition behaviour or regulatory claim changes.

# ADR-0030 — Finalized timing evidence primitives

Date: 2026-09-16. Status: implementation within user-approved C14 batch;
independent review open. Primary Coordinator/timing engineer and Architect;
affected QA, Android/UVC, Risk and Release/SBOM.

Implement HC-IF-TIM-001 1.0 binary semantics directly in .NET using platform
BinaryPrimitives, canonical-order UUIDs, CRC32C and exact integers. No additional
runtime dependency or wire/persistence version change. Finalized readers reject
unknown layouts/flags, count/tail disagreement, corruption, hidden absent values,
invalid mapping presence and non-finite IMU components. Never repair acquisition
records. Keep independent sensor sequences and segment/native values unchanged.

Use bounded in-memory read-only records for this internal engineering stage:
100,000 source frames and 1,000,000 IMU samples. These are resource envelopes, not
camera cadence, capture duration or protocol acceptance rules. Exceeding them
fails explicitly. A streaming implementation is required before supported
protocols exceed these limits. File loading/hash/lease integration remains the
common verifier's responsibility; the byte-span parser itself opens no files.

Associate every decoded presentation index exactly once with an accepted source
frame or explicitly declared generated duplicate. Compare source encoded PTS
and container PTS by BigInteger rational cross-products, not floating point or
nominal FPS. The caller must establish schema/identity and the presentation clock
before invoking this primitive; native sensor timestamps are not that clock.
No cadence class is prohibited and anomalies are not repaired.

Generated duplicates refer only to source sequence in v1 camera metadata. Refuse
ambiguous references across restart segments. They have no declared expected PTS:
report AllPresentationTimesCompared=false rather than manufacture timing evidence.
Any later required timing check must account for this limitation explicitly.
No public profile extension is silently introduced.

Trade-off: small typed primitives are independently testable against existing
cross-language vectors, but do not validate metadata schemas, clock bindings,
geometry, cadence summaries, event/finalization semantics or package completion.
These remain integration work. The architecture skill informed this minimal
separation; no custom general-purpose schema engine or Node runtime is introduced.

Risk controls HC-RISK-022/030/031 and HC-DATA-REQ-001/002/009 apply. No host
activation, automatic commit, source cleanup, deployment or scientific/medical
claim. AI design/implementation/testing; independent human review remains open.

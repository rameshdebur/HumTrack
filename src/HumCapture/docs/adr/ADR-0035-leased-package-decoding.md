# ADR-0035: Pinned decoding inside package read leases

Accepted engineering design for the user-requested next sprint C14R, 2026-09-19.
Primary Coordinator/Repository engineer; affected Architect, QA, Timing and
Release/SBOM owners. Engineering verification owner: QA; independent human
review remains open. Existing approved ARD/PRD/SRS, roles, governance, preliminary
India baseline, ADR-0028/0033/0034 and XFR/TIM/ART contracts apply. Requirements
HC-DATA-REQ-001/002/009; risks HC-RISK-022/030/031. No intended-use change.

Add an internal asynchronous package entry point which retains the admitted
package lease while awaiting the existing pinned inspection worker. Only that
entry point may discharge DECODER_PROVENANCE after successful inspection and
matching the admitted master length/hash. Caller-injected observations retain
their unassessed provenance. Reuse the same metadata/evidence comparison code.

Return the worker's typed outcome on decode failure, timeout, cancellation or
cleanup failure, with no package result. A survivor package with no master can
return its partial evidence with no inspection and missing decode/provenance.
Invalid package/metadata remains an exception, never successful evidence.
The decoder timeout covers its queue/hash/process work, not the whole package
admission; caller cancellation applies across admission and comparisons.

Alternative: decode first and reopen the package is simpler but breaks the
stable byte-binding interval. A new service adds unnecessary MVP complexity.
Trade-off: existing package handles remain held while the decoder waits/runs;
bounded cancellation and disposal release them. No new process framework,
dependency or external interface. No host activation, journal writes,
verification record, VERIFIED, receipt or cleanup. C14S/T cadence/protocol and
clock continuity assessment remain distinct gates, not inferred from decode.

Verify pin failure, missing master, cancellation/release, untrusted observation
separation and real pinned synthetic MP4 package comparison. This is not camera,
field, legal or regulatory qualification; redistribution stays disabled.

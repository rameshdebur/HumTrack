# ADR-0026 — Local-folder collection into unverified staging

Status: Accepted for user-authorized C13 engineering; independent review pending.
Date: 2026-09-15

Primary owner: Coordinator/repository engineer. Affected owners: Architect,
QA, Risk and Release/SBOM. Verification owner: engineering QA.
Classification: additive local API and staging persistence integration.
Baseline: ADR-0012, HC-IF-XFR-001 1.2.0, HC-DATA-REQ-001–003/009–012,
HC-RISK-022/030/031. No regulatory applicability or dependency change.

Accept an absolute Windows folder outside the repository, a caller-stable
collection-attempt UUID and an initialized repository. Reuse manifest and byte
integrity checks, never verifier PASS claims. Copy sequentially with bounded
memory. Retain all originals. Recheck completed destination files before skipping;
restart only collector-owned partial files from zero. Conflicting final files
stop collection without overwrite. No repository transaction is admitted.

Use staging/{attempt}/{package}/ for the payload, collection-manifest.json and
collection-checkpoint.json beside it, and partials/{artifact UUID} for scratch.
The immutable collection manifest binds retries. Publish the payload manifest
last. The HC-IF-XFR-001 checkpoint uses COORDINATOR_LOCAL, never VERIFIED; file
length/hash validation permits byte-level reuse, not scientific verification.
Checkpoint replacement is atomic; recovery rechecks bytes instead of trusting
checkpoint assertions. Failed/cancelled calls preserve staging for retry.

Folder and repository trees may not overlap. Same-session host exclusion plus
the existing process-local repository gate serialize cooperating calls. This is
not protection against arbitrary external writers or physical power-loss proof.

Trade-off: validate source inventory and bytes before collection and validate
the destination afterwards; extra I/O is accepted for a simple MVP. A finalized
incomplete package with its declared survivors is supported, but a subsequently
damaged/missing declared artifact is refused, not silently dropped or repaired.
Direct MTP, HTTPS and a richer survivor-recovery workflow remain separate work.

Alternative automatic admission is rejected: independent media/timing/metadata
verification is not implemented. Collected packages cannot authorize completion,
receipts or source cleanup. Revisit scheduling/performance before live capture
integration; no master worker may be blocked by collection.

# ADR-0028 — Bounded decoder execution, dormant integration

Date: 2026-09-16. Status: user-authorized C14C engineering; independent review open.
Primary: Coordinator engineer/Architect. Affected: QA, Release/SBOM and risk reviewer.
Verification: engineering QA. Classification: internal execution boundary, no external
schema, persistence, intended-use or regulatory-claim change. ADR-0027 and the existing
baselined ARD/PRD/stories/interfaces/roles/governance and preliminary India baseline apply.

Implement internal .NET asynchronous subprocess execution in the existing Coordinator
assembly, not a new service or synchronous Node probe dependency. No host CLI wiring,
automatic activation, download or redistribution. Only the self-test assembly can
exercise the internal candidate entry point at this stage.

Use explicit pinned ffmpeg path, embedded lock identity, read-only file handles held
during hashing/execution, argument lists (no shell), software decode to null and
passthrough cadence. Restrict input to ordinary local MP4 files and the mov demuxer;
reject reparse paths. One decoder invocation per process. Deadline covers queue,
hashing and execution. Drain stdout/stderr concurrently with bounded byte retention;
cancel/timeout/overflow terminates the process tree and waits boundedly for cleanup.
Any incomplete cleanup is failure, never decode success. No scientific timing is
derived from this deadline, host timing or nominal FPS.

An unconfirmed parent cleanup blocks further worker calls in that process until
restart. Remove inherited FFREPORT from the child environment to avoid unsolicited
report-file writes. Cleanup can add up to five seconds after the execution deadline.

Alternatives: synchronous probe blocks its caller; new service/project adds MVP
complexity. Reuse of repository assembly is intentionally internal and separable later.
This is not an OS sandbox or hard CPU/RAM quota. Single decoder/thread settings and
bounded time/output limit exposure, but capture scheduling/admission remains a later
integration gate. Process.Kill(true) is best-effort descendant termination, not proof
of every descendant's exit. Known pinned FFmpeg is not expected to spawn children.
Revisit with Job Objects if descendant or hard-memory containment is required.

Output is a decode-execution result, never a package-verification record. Complete
manifest, stream-count, frame/timing/camera/events/finalization/IMU checks remain C14D.
runtime_enabled and redistribution_approved stay false. No requirement closure or
hardware/field/clinical/regulatory qualification follows.

References checked 2026-09-16:
- https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0
- https://ffmpeg.org/ffmpeg.html

Risks HC-RISK-022/030/031; requirements HC-DATA-REQ-001/002/009 and HC-UVC-REQ-005.
Tests must exercise real subprocess success, nonzero exit, stderr, simultaneous pipe
pressure, overflow, timeout, cancellation, missing executable, pin/path rejection,
and optional real pinned FFmpeg synthetic input. Host integration stays disabled.

# ADR-0023 — Bounded Coordinator startup executable

**Status:** Accepted for user-authorized C9 engineering scope; independent review pending
**Date:** 2026-09-12

The existing Coordinator repository and startup-pass API need a real process
entry point. Add a framework-dependent .NET 10 Windows console host referencing
only the bounded repository assembly. It runs one explicitly requested startup
pass, prints versioned JSON and exits; it is not a resident service or capture
scheduler. No automatic repository creation, UI, networking or camera activation.

Use the signed-in Windows identity through the existing API. A Local named
mutex derived from the normalized root prevents overlapping cooperating hosts
in the same Windows session. This is not global writer exclusion: direct library
callers, different sessions and alternate path aliases remain outside the guard.
No new repository file or persistence schema is introduced.

CLI: startup --root ABSOLUTE_PATH [--limit 1..1000] [--after UUID].
Output contract 1.0.0 contains pass status, cursor, attention flag and item
transaction/state/action/error/next-action fields, not raw exceptions, account
names, subject demographics or absolute paths. Completed is scan completion
only. Exit codes: 0 completed without item errors; 2 invalid arguments;
3 root/runtime error; 4 item errors; 5 more work; 6 read-only inspection;
7 another host owns the guard; 130 cancellation. Cancellation takes precedence,
then item errors, then pass status. MoreWork may therefore accompany exit 4.
Root failures do not become empty successful scans.

Ctrl+C requests cancellation between transactions; finalization is not aborted.
The host never emits receipt or cleanup authorization. The caller must retain
and aggregate page results, and explicitly retry failed identities.

Architecture review: this realizes the accepted headless Coordinator boundary
without importing HumTrack internals. Retain locked NuGet dependencies, add the
host to the SBOM and test it as a child process through existing CI tests.
No installer, background autostart, Windows service, binary signing or controlled
release is authorized. ADR-0020 remains recovery authority; supersedes none.

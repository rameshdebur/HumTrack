# ADR-0024 — Explicit staged-package processing command

**Status:** Accepted for user-authorized C11 engineering scope; independent review pending
**Date:** 2026-09-12

Extend ADR-0023 with process-staged, using the same --root, --limit and --after
arguments and the same-session cooperating-host mutex. It runs normal C3
STAGED_VERIFIED -> COMMITTING -> MOVED processing only for staged candidates,
with fresh UUIDs and current Windows account. The existing move implementation
revalidates evidence and enforces absence and same-volume atomic movement.

Other states produce SKIP_NOT_STAGED with null state, not a fresh evidence
verdict. The caller may run startup to reconcile them. Failed movement never
retries blindly; startup establishes the actual state before a later process
command. Repeating process-staged after a successful move skips that entry and
cannot repeat or overwrite the package. Current startup behavior is unchanged.

Both commands remain one bounded page per invocation, with cancellation between
packages. Completed means scan exhaustion. One staged processing operation
contains the two established durable C3 transitions, not cataloging or final
commit. Move snapshot is additive in the local result API.

Host output contract advances compatibly to 1.1.0: existing fields and exit codes
remain; action adds MOVE_STAGED_PACKAGE and SKIP_NOT_STAGED. Skipped items have
no verified state and next action REVIEW_STATE_OR_RUN_STARTUP. Move success has
next action RUN_STARTUP_TO_CONTINUE_CATALOGING. No receipt/cleanup authorization.

No new persistence schema, dependency, process topology, Windows permission
requirement or scientific timing guarantee. Audit timestamps are request audit
values, not measured operation duration. Extends ADR-0023; supersedes none.

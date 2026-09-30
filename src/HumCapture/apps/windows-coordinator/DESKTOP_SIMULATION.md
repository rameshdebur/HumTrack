# Coordinator simulation workspace

Engineering-only C16 UI. No camera API, network capture, real identity or
scientific media is used. Actual repository inspection is separate, read-only
and historical, not fresh package verification.

## Open

Run Start-Simulation.ps1 from this folder to build locked dependencies and open
the Windows application. After building, opening
src/HumCapture.Coordinator.Desktop/bin/Release/net10.0-windows10.0.19041.0/HumCapture.Coordinator.Desktop.exe
also opens the default workspace without command-line arguments.

Default data: HumCapture/evidence-vault/coordinator-ui-simulation (ignored,
synthetic-only). Existing data is retained, not reset on launch. One window per
workspace. No production repository is initialized.

## Try

1. Save a fictional subject/session under single-v1 or dual-v1 fixture protocol.
2. Assign distinct SIM-A/SIM-B sources and choose a test scenario.
3. Start, optionally inject a selected failure, then stop. Preview failure does
   not stop simulated recording; source loss preserves a surviving source.
4. Results separate finalization from actual verification/commit. The optional
   processing exercise fails verification/storage once, then permits retry on
   the same attempt. It never creates a real verified or committed package.
5. History resumes sessions; repository inspection reads existing journal pages.

Close while active asks whether to stop/finalize or remain open. Reopening after
abrupt termination marks active simulation interrupted and shows history.
No production worker is qualified by this behavior.

## Evidence limits

Runtime216 tests the service; runtime217 drives actual controls headlessly,
keyboard input/focus, active navigation guards and two window-size renders.
These run through the existing self-test runner without parent CI edits.
Native Windows display, screen reader/high contrast/scaling and trained-human
usability remain unverified. No clinical/release qualification.

Session pages contain 20 rows; latest 20 attempts and 50 events are displayed.
Older rows remain in SQLite. Full production subject/protocol registry,
capture/import/transfer/commit operation and Android remain separate work.

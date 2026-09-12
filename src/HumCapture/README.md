# HumCapture

HumCapture is the bounded motion-capture acquisition subsystem of HumTrack. It coordinates trained-operator capture from Android Camera2 nodes and Windows-connected UVC cameras, protects local scientific masters, records timing/camera/IMU evidence, recovers and verifies packages, and commits them into subject/session/trial storage for downstream analysis.

HumCapture may read the wider HumTrack repository for conventions but may modify only this directory unless the user explicitly authorizes a specific external change.

## Current stage

Governance and design are baselined. Phase 0 P0.1 evidence tooling is implemented
under `tools/capability-probes/shared`, and its independent source-contract gate
has passed. P0.2 Windows/UVC probe work is unblocked but not started; production
application implementation remains gated. Start with:

1. `AGENTS.md`
2. `docs/project/PROJECT_STATE.md`
3. `docs/architecture/ARD.md`
4. `docs/product/PRD.md`
5. `docs/requirements/USER_STORIES.md`
6. `docs/verification/PHASE_0_IMPLEMENTATION_PLAN.md`

## Core invariant

Preview, networking, UI, transfer, and downstream inference must never compromise scientific master acquisition.

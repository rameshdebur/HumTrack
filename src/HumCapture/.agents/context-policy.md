# Context Selection Policy

Load context in this order:

1. `AGENTS.md`.
2. `docs/project/PROJECT_STATE.md`.
3. `.agents/ownership.md` for affected components.
4. Relevant architecture and ADRs.
5. Relevant requirements and user stories.
6. Relevant India/CDSCO, risk, privacy, and claims records.
7. Relevant interfaces and schemas.
8. Current implementation and tests.
9. Targeted Git history when rationale remains unclear.
10. Additional agent memory only when necessary.

Examples:

- Android recorder work loads Android architecture, master/timing/IMU contracts, acquisition risks, Android code/tests, and named hardware evidence.
- Transfer recovery work loads package/transfer contracts, repository ADRs, identity/integrity risks, transfer code/tests, and recovery stories.
- Coordinator UI work loads relevant workflows, UX/accessibility risks, owning component code/tests, and HumTrack visual principles without modifying HumTrack.

Do not load unrelated components or historical conversations by default. Promote important discoveries into current repository artifacts.


# ADR-0036: Versioned, evidence-bound scientific conditions

Accepted by user 2026-09-22 for C14S-T, including the protocol extension and
missing-evidence distinction. Classification: cross-component interface and
scientific evidence change. Primary Architect/Coordinator engineer; affected
Timing, Android/UVC, QA, Release/SBOM and Risk owners. QA owns verification;
independent human review remains open. Existing ARD, PRD, user stories, SRS,
roles/governance, preliminary India baseline and XFR/TIM/ART plans apply.
No changed medical intended use, clinical claim or deployment authority.

Create opt-in protocol snapshot 1.1 with per-role scientific rules; do not mutate
v1 schemas or historical snapshots. Fixed metric names/operators and integer
thresholds avoid an expression language and canonical floating-point ambiguity.
Milli-Hz and microseconds are threshold units; compare rational measurements
without rounding. No universal thresholds, adaptive-rate or second-camera rule.

The internal evaluator accepts canonical snapshot bytes plus the coordinator's
source/role assignment projection. Bind snapshot ID/content hash and source ID
to the package. This checks consistency, not that an approval/account/assignment
is authentic; full coordinator protocol admission stays a later integration gate.
No supplied rules, no role rules, unsupported/missing measurements, and legacy
snapshots cannot become automatic scientific acceptance.

For 1.1 conditions: required FAIL -> REJECTED; otherwise required NOT_ASSESSED
-> REVIEW_REQUIRED; otherwise preferred FAIL or NOT_ASSESSED ->
DEGRADED_ACCEPTABLE; otherwise CONFORMANT. Empty or informational-only sets ->
REVIEW_REQUIRED (no acceptance requirement established). NOT_APPLICABLE requires
future explicit applicability evidence; it is not a bypass in this evaluator.
Old JavaScript helper behavior remains versioned legacy, not silently changed.

Compute source cadence only from native frame timestamps with declared frequency,
not advertised FPS, arrival or container PTS. Count gaps/duplicates/regressions;
never bridge a segment change or time/sequence regression. Overall rate remains
unassessed when discontinuous or duplicate native times prevent a single cadence.
Widths/heights used for rules come from decoded media, not negotiated claims.
Camera provenance/rate class/lens availability remain explicitly reported metadata.

Clock models cannot be reused across a segment/restart or native-time/sequence
regression within a logical stream. IMU lanes are independent by sensor ID;
interleaving does not establish regression. Also disallow the same model/clock
across different declared segments across streams. Unmapped records still mark
barriers. Validate observed declarations only, not undetectable physical restarts
or epoch changes absent from source evidence. Missing mapped evidence stays
unassessed; do not claim physical synchronization.

Alternative: generic expression engine or global FPS gate adds complexity or
incorrectly rejects camera classes. Bounded fixed rules are simpler but new
metrics require a versioned extension. No host activation, verification record,
commit, receipt or deletion. Risks HC-RISK-022/030/031 and timing evidence risks;
requirements HC-TIME-REQ-005/008/009/011/012/013 and HC-DATA-REQ-001/002/007/009.

# ADR-0029 — Hash-bound decoded frame evidence

Date: 2026-09-16. Status: user-authorized C14D engineering; independent review open.
Primary: Coordinator engineer/Architect. Affected: timing, QA, Release/SBOM, risk.
Verification: engineering QA. Existing C14 requirements/governance and preliminary
India baseline apply; no external schema, persistence, deployment or claims change.

Extend the internal C14C worker with pinned ffprobe inspection and ffmpeg full
decode under the same serial gate, deadline and read-only input/executable leases.
Return byte length/SHA-256, codec, video-stream index, header dimensions, exact
positive rational time base and decoded presentation-order frame PTS/dimensions.
Require exactly one non-attached-picture video stream and at least one frame.
Other media types are outside this video inspection, not claimed verified.

Keep signed int64 PTS exactly (including negative, duplicate and regressing values).
Never substitute best_effort_timestamp, nominal FPS, duration/frame-count estimates
or source sensor time. Ordering/geometry anomalies remain observable for later
scientific checks; successful extraction is not acquisition conformance.
Missing PTS, foreign stream indices, invalid dimensions/time base, malformed JSON
or duplicate properties fail closed. No array truncation or synthetic padding.

Use selected ffprobe JSON fields rather than a new native parser or streaming
sidecar format in this slice. The trade-off is a bounded engineering envelope:
16 MiB combined process output and 100,000 decoded frames. Oversized inputs return
failure/OutputLimit, not camera rejection or partial verified evidence. No capture
duration/FPS requirement follows. Revisit streaming evidence before deploying a
protocol whose supported captures can exceed this envelope. Existing decode-only
calls retain their 1-MiB diagnostic limit. This is not a hard process-RAM quota.

This slice produces evidence only. Source binary/clock/camera/IMU/event/finalization
association and package verification remain subsequent C14 work. No host command,
runtime activation, journal mutation or receipt. Risks HC-RISK-022/030/031;
requirements HC-DATA-REQ-001/002/009 and HC-IF-TIM-001 timing provenance apply.

Primary reference checked 2026-09-16: https://ffmpeg.org/ffprobe.html

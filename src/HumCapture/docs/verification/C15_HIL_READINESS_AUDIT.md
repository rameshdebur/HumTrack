# HC-AUD-C15-HIL-001: readiness for supervised operator testing

2026-09-27. Scope: actual current HumCapture checkout, executable host, local
Windows device enumeration and available capture probes. No clinical subject
testing, new recording, camera-control mutation or regulatory determination.
Human testing here means a trained operator observing an engineering bench test.

## Verdict

- GO for the engineering host workflow with synthetic finalized packages.
- CONDITIONAL GO for an isolated short C920 diagnostic capture after exact
  profile selection and operator scene/indicator check. This is probe HIL only.
- NOT YET READY for a single integrated operator workflow from physical camera
  start through scientific package, repository commit and completion UI.
- NOT READY for clinical/field claims, Android HIL or a two-C920 acceptance run.

## Current evidence and gaps

| Area | Observed state | Consequence / next step |
|---|---|---|
| Physical sources | PnP reports one HD Pro Webcam C920 and HP Wide Vision HD Camera, status OK | Start with one C920; no mandatory second camera |
| Media Foundation | C920 inspection succeeds, 574 native types; HP succeeds, 17 types | Advertised capability only, not measured cadence or fresh-frame evidence |
| C920 modes | H.264 1280x720 and 1920x1080 at advertised 30/1 are present | Prefer a short single-source 720p test; enumerate exact native type before launch |
| NDI | Four NDI Webcam Video virtual endpoints are enumerated | No fresh-frame/upstream-camera evidence; exclude from physical acceptance |
| UVC acquisition | MfCapture diagnostic executable exists; emits capture.mp4, frames.csv and capture-result.json | Diagnostic format does not directly satisfy finalized production package contract |
| Package producer | No production services/uvc source tree or camera-to-package bridge found in this checkout | Implement minimal coordinator-bound UVC packaging/finalization adapter; do not invent metadata |
| Coordinator | Engineering CLI now runs assignment, collection, verification, history/recovery, backup/restore | Usable by developer-assisted trained operator, not finished user UI |
| Repository | Existing commit pipeline plus new staged verification/maintenance paths | Test the real package through existing commit, then restart/recheck subject/session destination |
| Android | No apps/android-capture implementation tree in this checkout | Android HIL not ready; phone IMU/camera/discovery/transfer not inferred from Windows tests |
| UI | Host is a console application; subject/protocol/capture UI is not implemented here | Detailed operator UI design and implementation remain a separate gate |
| Decoder | Pinned binary passes synthetic host integration; production flag and redistribution approval remain false | Engineering use only; no shipping/licence clearance inferred |
| Recovery | Real child-process kill tests cover verification and copy boundaries | Not an OS/power-loss, unplugged USB drive or field qualification |

Device/mode evidence: I0_4B_C15_HARDWARE_ENUMERATION.txt. Enumeration may
briefly initialize a device to inspect formats; no master was recorded. Names,
port identities and advertised types may change after reconnection. No current
LED, camera framing, achieved FPS, thermal or USB-bandwidth claim is made.
The HP imaging/scanner PnP entry is not counted as a video source.

## Smallest next implementation batch: single-UVC vertical slice

1. Coordinator-owned engineering capture request: synthetic subject/session/
   trial, immutable protocol assignment, exact C920 identity/profile, bounded
   duration and explicit output under HumCapture-owned test storage.
2. Isolated worker/adapter preserves the original master and measured Media
   Foundation sample timestamps, frame associations, finalization events and
   hashes in the current package schemas. Mark unavailable lens/IMU fields
   explicitly; do not fabricate sensor timestamps, adaptive FPS or IMU data.
3. Use existing engineering host for collection/verification, then existing
   commit processing. Verify correct subject/session placement, original source
   preservation, complete record history, restart and isolated backup/restore.

Baseline that adapter's contract/risk controls before implementation. Do not
silently relabel an old diagnostic CSV as production scientific evidence.
This is a new acquisition implementation slice, not part of C15 maintenance.

## Supervised HIL procedure after the bridge is ready

- Operator confirms one exact C920, a non-sensitive test scene, enough space,
  no other camera application, and the chosen protocol/profile. Agent operates
  only the agreed engineering commands; operator controls physical placement.
- Begin with a 3-5 second capture, not a 30-minute soak. Observe optional in-use
  indicator: off before, continuously on during, off after where available.
  Indicator absence is NOT_APPLICABLE and never timing evidence.
- Acceptance: fresh decodable video, measured source cadence and timestamp
  provenance, matching frame count/associations, complete metadata or explicit
  unavailability, verified commit in correct subject/session, no source deletion.
- Repeat once; retain exact run IDs, source/profile, build/SBOM identity,
  hashes, logs and operator observations. Failed cadence follows protocol
  REQUIRED/PREFERRED policy: reconcile configuration or reject; never silently
  lower the requirement or force an adaptive-rate feature.
- Disconnect/reconnect and longer stability are later supervised steps; request
  the physical action at that point. Do not require two cameras unless protocol
  calls for them. No clinical participants, automatic deletion or power cycling.

Evidence remains engineering/HIL only. Independent QA, privacy/retention review,
qualified Indian regulatory review and release controls remain separate.

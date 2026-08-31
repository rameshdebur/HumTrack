# HC-P0-2J — UVC Disconnect/Reconnect Recovery Diagnostic

**Tags:** P0.2J | UVC | RECOVERY | DEVICE-LOSS | FINALIZATION | RECONNECT | MEDIA-INTEGRITY | HARDWARE

## Goal

Determine whether already-received samples can be finalized after an unexpected
live USB disconnect and whether the exact camera can be rediscovered and used to
create a separate clean post-reconnect artifact.

## Safety and evidence rules

- Test one exact camera at a time with a synthetic/non-subject scene.
- A disconnect is never represented as continuation of one uninterrupted master.
- The pre-disconnect and post-reconnect captures have different run IDs and files.
- A partial pre-disconnect artifact is accepted only when the raw read failure is
  recorded, sink finalization succeeds, and the entire partial file decodes.
- Ordinary short-capture verification rejects recovery-mode early termination.
- A failure to finalize or decode is retained as failure evidence, not hidden.

## Acceptance criteria

- [x] Probe mode can distinguish requested-duration completion from a terminal
      read error and preserves the raw HRESULT where available.
- [x] Ordinary short-capture verification rejects an early recovery termination.
- [x] Operator disconnects the selected exact camera during active fresh-frame delivery.
- [x] Device disappearance is observed and correlated with the terminal read event.
- [x] Already-received samples finalize and fully decode, or failure is recorded truthfully.
- [x] Pre-disconnect raw artifacts are imported into the controlled evidence
      vault and pass full hash verification.
- [ ] The same physical camera/identity is reconnected and rediscovered.
- [ ] A new post-reconnect capture fully finalizes and decodes.
- [ ] Exact lifecycle events and separate artifact identities are recorded.

## Status

hardware execution in progress — Camera A live disconnect produced recorded
HRESULT `0xC00D3EA2`; 265/265 partial frames finalized and fully decoded, and
ordinary verification rejected false completion. Reconnect/post-capture remains open.

Controlled evidence ID: `HC-EV-5becff40f4271131c8b43641`; engineering
snapshot: `HC-ENG-20260831T163246Z-7d17f5afce36`. Its CycloneDX 1.7 SBOM is
retained and hash-verified. Review remains `DRAFT`.

## Evidence boundary

This is a conditional 1080p30 recovery diagnostic. It cannot qualify normative
HC-P0-UVC-002 because the available C920s do not report the required 1080p60
profile.

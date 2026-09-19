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
- [x] The same physical camera/identity is reconnected and rediscovered.
- [x] A new post-reconnect capture fully finalizes and decodes.
- [x] Exact lifecycle events and separate artifact identities are recorded.

## Status

technical execution passed — Camera A live disconnect produced recorded HRESULT
`0xC00D3EA2`; 265/265 partial frames finalized and fully decoded, and ordinary
verification rejected false completion. The same parent serial `0E1A0C0F` and
exact interface returned. Separate run `2D358446-A581-4DDA-92FA-92B5F71606A9`
then reached its requested duration, finalized, and fully decoded 898/898 frames
at measured 29.90 fps with zero decode errors or timestamp regressions.

Controlled evidence IDs: interrupted run `HC-EV-5becff40f4271131c8b43641`
(`INCONCLUSIVE`) and post-reconnect run `HC-EV-16e6576bbcf4f1896d64aac6`
(`PASS`). Both bind to engineering snapshot
`HC-ENG-20260831T163246Z-7d17f5afce36`; the two-run vault verifies. Review
remains `DRAFT`, so independent closure remains open.

## Evidence boundary

This is a conditional 1080p30 recovery diagnostic. It cannot qualify normative
HC-P0-UVC-002 because the available C920s do not report the required 1080p60
profile.

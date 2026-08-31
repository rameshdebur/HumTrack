# HumCapture Architecture Views

## Context view

The trained operator uses the Windows coordinator to manage subjects, protocols, trials, Android nodes, and local UVC cameras. Android nodes keep authoritative masters locally through network failure. The coordinator verifies and commits all required packages before session completion. HumTrack is a downstream package consumer, not a runtime dependency.

## Runtime view

```text
Coordinator UI → Coordinator Host → Android control/transfer services
                            ├──────→ Preview receivers
                            ├──────→ UVC workers
                            └──────→ Catalog and filesystem repository
```

UI and preview are observers of authoritative state. Recorder/finalization workers own scientific data production.

## Deployment views

Preferred laboratory:

```text
Android nodes → dedicated Wi-Fi 6/6E AP → wired Windows coordinator
UVC cameras  → USB ────────────────────→ Windows coordinator
```

Supported field:

```text
Android nodes → Windows laptop hotspot/coordinator
UVC cameras  → same laptop USB
```

Field mode requires named-adapter validation, mDNS fallback, timing readiness, power/storage checks, and post-capture sequential transfer by default.

## Data-flow view

```text
Capture → local master/timing/IMU → finalize → transfer/local staging
→ schema/identity/hash verification → transactional commit
→ quality/common timeline → pseudonymized handoff
```

## Trust boundaries

- mDNS discovery is untrusted.
- Pairing establishes pinned device/coordinator trust.
- Session authorization is short-lived.
- Android stores no full subject identity.
- Coordinator subject identity and media remain local unless explicitly exported.


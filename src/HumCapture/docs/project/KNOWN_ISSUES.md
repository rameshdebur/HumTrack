# HumCapture Known Issues and Unknowns

No application implementation exists yet. These are design/evidence gaps, not diagnosed code defects.

| ID | Issue or unknown | Impact | Resolution gate |
|---|---|---|---|
| HC-KI-001 | Android device/version support is unqualified | MVP device floor unknown | Phase 0 |
| HC-KI-002 | 1080p120 sustainability and concurrent preview vary | Optional profile may be unavailable | Phase 0 |
| HC-KI-003 | Software timing thresholds are provisional | Synchronization claim remains limited | Phase 0 |
| HC-KI-004 | Generic UVC timestamp provenance varies | Cross-source timing quality may differ | Phase 0 |
| HC-KI-005 | Laptop hotspot multicast and throughput vary by adapter/driver | Field discovery/transfer reliability unknown | Phase 0 |
| HC-KI-006 | Timing and IMU binary formats are undecided | Cross-component implementation blocked | Interface design |
| HC-KI-007 | Offline return of a USB-recovery completion receipt is undecided | Phone may remain `STATUS_UNKNOWN` until reconnect | Interface/product refinement |
| HC-KI-008 | CDSCO device applicability/classification is not determined | Medical claims and licensing path blocked | Qualified regulatory review |
| HC-KI-009 | Detailed Android and coordinator UI designs are deferred | No UI implementation should start | UI design stage |
| HC-KI-010 | Direct HumTrack importer is outside authorized scope | Handoff stops at package contract | Separate user permission |
| HC-KI-011 | HP camera reports at most 720p30 and both attached Logitech C920 cameras report native H.264 at most 1080p30 | Available cameras cannot qualify the normative UVC 1080p60 procedure | External named 1080p60 UVC hardware campaign |
| HC-KI-012 | Exact native H.264 1080p30 cadence varied from approximately 15 to 30 fps under automatic exposure; temporary manual exposure `-5` produced clean short runs and a clean 10-minute concurrent stability run | Negotiated and MP4 nominal rates cannot substitute for measured cadence; exposure/scene policy is part of the named configuration and remains conditionally supported only | Define readiness cadence measurement and protocol exposure policy; test scene range, persistence, and recovery before qualification |
| HC-KI-014 | Concurrent corruption followed downstream USB path 2 after a physical-camera swap; moving that camera to direct root branch `USBROOT(0)#USB(3)` removed corruption through repeated short runs and one 10-minute concurrent stability run, though both cameras remain on controller `PCI(1400)` | The original port combination remains rejected; the reconciled named topology is conditional and cannot be generalized | Preserve exact topology/profile evidence; complete disconnect/reconnect and preferably different-controller comparison before qualification |
| HC-KI-015 | The controlled evidence vault is local and hash-verifiable but has no verified backup/restore, approved retention schedule, independent signature, WORM/eQMS control, or access audit | A local disk/account failure or privileged alteration can compromise dossier-grade retention | Approve and test retention, backup/restore, access, review, and archival controls before release/dossier use |
| HC-KI-016 | HumCapture is tracked on the protected `codex/humcapture-baseline` review branch, but that baseline is not merged or approved as a controlled release | The retrospective P0.2J snapshot cannot be relabelled and no approved released-build identity exists | Obtain independent review, merge through the protected workflow when authorized, and create a clean reproducible release identity with required approvals |
| HC-KI-017 | Current SBOM covers declared source manifests/build context but not binary composition, dynamically loaded modules, host Windows/.NET patch resolution, future Android dependencies, vulnerability/VEX status, or licence/supplier approval | Inventory cannot yet support a released-product supply-chain assurance claim | Add binary/runtime collection and future manifests; perform independent vulnerability, licence and supplier review per release |
| HC-KI-018 | Primary raw artifacts for P0.2A-I are no longer retained, and the two retained P0.2J runs lack the complete normative measurement/lifecycle set | Historical reports remain useful engineering records but cannot form a complete independently reprocessable P0.1-format qualification package | Capture future attempts prospectively into the complete registry-bound package; verify vault retention and independent review before qualification |

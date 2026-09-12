# HumCapture Component Map

| Component | Responsibility | Consumes | Produces |
|---|---|---|---|
| Android UI | Setup, status, emergency stop, recovery, cleanup | Local state and coordinator commands | Operator actions |
| Android capture service | Camera/IMU acquisition and local finalization | Protocol configuration and scheduled time | Master, timing, IMU, manifest, hashes |
| Android preview | Disposable monitoring | Capture frames | Bounded RTP stream |
| Android control/transfer | Discovery, authenticated commands, resumable files | Coordinator requests | State, capabilities, packages, receipts |
| Coordinator UI | Guided trained-operator workflow | Coordinator host state | Validated commands |
| Coordinator host | Authoritative session/trial state and orchestration | Protocols, source state, operator actions | Commands, audit, workflow state |
| Clock synchronizer | Offset/drift/uncertainty fitting | Raw exchanges and monotonic clocks | Clock models and timing quality |
| UVC worker | Isolated wired capture and finalization | UVC device and trial configuration | Master, timing, metadata, health |
| Transfer manager | Queue, resume, USB recovery | Finalized Android packages | Staged packages and progress |
| Integrity verifier | Schema/identity/length/hash checks | Staged/local packages | Verified or quarantined result |
| Repository manager | Transactional subject/session/trial commit | Verified packages | Durable self-contained repository |
| Quality assessor | Acquisition conformance | Protocol snapshot and recorded evidence | Versioned quality report |
| Simulator | Deterministic source/failure behavior | Contracts and virtual clock | Integration/fault evidence |


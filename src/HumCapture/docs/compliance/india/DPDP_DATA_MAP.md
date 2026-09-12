# India Privacy and Data Map

**Status:** Preliminary; current DPDP applicability and institutional duties require review

| Data | Location | Purpose | Default export | Notes |
|---|---|---|---|---|
| Full name, DOB, demographics | Coordinator subject registry | Identity and demographic context | Excluded from pseudonymized handoff | Never advertised or sent to Android by default |
| Subject UUID/code | Coordinator and source packages | Association and recovery | Included | UUID is authoritative folder key |
| Master video | Android until committed; coordinator repository | Motion acquisition | Included when selected | Potentially directly identifiable |
| Partial package artifacts/checkpoints | Coordinator staging until verification, retry, quarantine, or controlled disposal | Interrupted-transfer recovery | Never | Not a committed subject record; inherits source-artifact sensitivity |
| Package manifest/verification/receipt | Source package and coordinator custody/audit records | Integrity, traceability, recovery and cleanup status | Included as appropriate | Uses subject UUID; no name/DOB; receipt created only after durable commit |
| Receipt acknowledgement/status and cleanup history | Android protected app storage and coordinator catalog/audit | Prove cleanup eligibility, reconcile loss, and record truthful source removal | Excluded by default | Exact device/source/package/hash references only; no subject name/DOB, secret, token, or receipt signature in MVP; retention policy remains open |
| Timing/IMU/camera data | Source package/repository | Scientific context and quality | Included | Preserve provenance/units |
| Operator Windows identity | Audit records | Accountability | Excluded by default unless required | No app password database in MVP |
| Pairing private keys | Android Keystore; coordinator current-user protected credential store | Mutual authentication | Never | Non-exportable on Android; DPAPI CurrentUser on Windows; no ordinary logs |
| Pairing QR secret/nonce | Android protected ephemeral memory and attended display; coordinator memory during enrollment | One-time bootstrap proof | Never | At least 128 bits, ten-minute maximum, single-use, never persisted or logged |
| Public certificate fingerprints/trust state | Android and coordinator trust/audit records | Peer identity, authorization, revocation and recovery | Excluded by default | No subject identity or master content; lifecycle retention policy remains open |
| Diagnostic logs | Device/coordinator | Troubleshooting | Redacted support bundle | Exclude PII and masters by default |
| Quality/audit reports | Session repository/catalog | Conformance and provenance | Included as appropriate | Retention policy linked to session |

## Controls to specify

- Purpose and collection notice/consent responsibility.
- Access and Windows filesystem protections.
- Local retention, backup, trash, permanent deletion, and tombstones.
- Identified versus pseudonymized export confirmation.
- Incident and lost-device/laptop response.
- Diagnostic redaction.
- Organization/data-fiduciary/processor roles under current law.

No legal conclusion is made in this preliminary map.

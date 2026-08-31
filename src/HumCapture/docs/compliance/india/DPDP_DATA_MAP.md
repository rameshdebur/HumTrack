# India Privacy and Data Map

**Status:** Preliminary; current DPDP applicability and institutional duties require review

| Data | Location | Purpose | Default export | Notes |
|---|---|---|---|---|
| Full name, DOB, demographics | Coordinator subject registry | Identity and demographic context | Excluded from pseudonymized handoff | Never advertised or sent to Android by default |
| Subject UUID/code | Coordinator and source packages | Association and recovery | Included | UUID is authoritative folder key |
| Master video | Android until committed; coordinator repository | Motion acquisition | Included when selected | Potentially directly identifiable |
| Timing/IMU/camera data | Source package/repository | Scientific context and quality | Included | Preserve provenance/units |
| Operator Windows identity | Audit records | Accountability | Excluded by default unless required | No app password database in MVP |
| Pairing keys/tokens | Protected platform credential storage | Authentication | Never | No ordinary logs |
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


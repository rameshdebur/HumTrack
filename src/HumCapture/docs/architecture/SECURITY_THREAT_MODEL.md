# HumCapture I0.3B Security Threat Model

**Status:** Contract baseline; implementation assessment pending  
**Date:** 2026-09-06  
**Scope:** Android discovery/pairing/control/transfer and coordinator identity;
USB/MTP retains the common verifier but is outside network authentication.

## Protected assets and safety priorities

1. Scientific master bytes and native timing/IMU evidence.
2. Authority to configure, arm, start, stop, collect, acknowledge, or clean up.
3. Stable device/coordinator/package identity and audit integrity.
4. Private keys, one-time bootstrap material, and subject privacy.
5. Availability of capture and safe finalization under attack or network loss.

## Trust boundaries

- LAN, laptop hotspot, mDNS, IP address, port and friendly label: untrusted.
- Attended Android display and trained operator transcription/scan: bootstrap
  boundary, not a general authentication channel.
- Android Keystore and Windows DPAPI CurrentUser: credential-custody boundaries;
  runtime verification remains required.
- Enrolled mutual-TLS channel: authenticated transport boundary; every operation
  still requires role, identity, trust revision and object authorization.
- Coordinator repository commit: separate custody authority; TLS success cannot
  declare verification, commit, completion, or safe deletion.

## Threat/control/verification cases

| Threat | Control | Contract verification | Open evidence |
|---|---|---|---|
| Spoofed mDNS/manual endpoint | Exact bootstrap SPKI fingerprint and attended proof | HC-SEC-TEST-004/006 | Hostile-network runtime |
| Online secret guessing | 128-bit secret, ten-minute expiry, five attempts, delay/lockout | HC-SEC-TEST-004/005 | Process restart/power-cycle persistence |
| Enrollment replay/substitution | Single-use session/nonce and both-key transcript hash | HC-SEC-TEST-005/006 | Real handshake capture |
| Stolen/replayed certificate | Active finite trust, exact cert/SPKI/UUID/role binding and revocation | HC-SEC-TEST-007/008/010 | Keystore/DPAPI and revocation runtime |
| TLS downgrade/weak cipher/early-data replay | TLS 1.3 default; narrow logged Win10 TLS 1.2 profile; no 0-RTT | HC-SEC-TEST-009 | OS TLS matrix and packet inspection |
| Cross-device/package access | Per-request paired device/coordinator/resource authorization | HC-SEC-TEST-008 | API integration/fuzz/penetration |
| Secret/subject leakage in discovery/logs | Minimal discovery and closed redacted audit schema | HC-SEC-TEST-011/012 | Runtime log/network inspection |
| Security workload starves capture | Bounded attempts/retries; acquisition priority; USB recovery | Document review | Load/fault/HIL/field tests |
| Key loss or reinstall invites bypass | Explicit lost/revoked state and attended re-enrollment | HC-SEC-TEST-007/010 | Usability/recovery field test |
| Trust removal deletes/interrupts data | No removal during capture/finalization; never delete package | HC-SEC-TEST-007 | Runtime fault/recovery test |

## Abuse cases that must fail closed

- An mDNS result is treated as paired merely because its UUID or name matches.
- A six-digit code, IP allowlist, cookie, or bearer token replaces certificate
  enrollment.
- A certificate authenticates but its role, UUID, trust state, or target package
  is not checked.
- A TLS 1.3-capable pair silently negotiates the Windows 10 profile.
- A pairing secret, proof, private key, subject name/DOB, or master path appears
  in audit/support output.
- A Windows reinstall or Android app-data loss reuses old trust without attended
  enrollment.
- Security/network failure marks capture, transfer, commit, or cleanup complete.

## Residual risk and review gates

The contract reduces ambiguity but does not prove platform key isolation,
protocol implementation, resistance to a hostile LAN, certificate parsing,
side-channel resistance, rate-limit persistence, secure update, incident
response, or operator usability. Runtime threat-model review, dependency/SBOM
review, fuzzing, penetration testing, Windows 10/11 and Android compatibility,
power/restart, HIL, and field evidence are required before a release claim.
CDSCO applicability and cybersecurity expectations require qualified review.

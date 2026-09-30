# HC-I0.3B — Pairing and TLS Binding Contract

**Tags:** I0.3B | SECURITY | ATTENDED-PAIRING | MUTUAL-TLS | AUTHORIZATION | KEY-LIFECYCLE | RECOVERY | CONTRACT

## Goal

Baseline peer enrollment, credential custody, transport authentication,
authorization, replay resistance, lifecycle recovery, and redacted audit before
Android or coordinator application implementation.

## Classification and ownership

- Change: cross-component interface, security architecture, privacy, lifecycle,
  compatibility, risk, and regulatory-adjacent contract design.
- Primary owner: System Architect.
- Affected owners: Android, coordinator, transfer/repository, security/privacy,
  simulator/QA, regulatory/risk, and trained-operator workflow.
- Verification owner: QA/simulator owner after executable baseline.
- Paths: HumCapture documentation and `tools/evidence-control/` only.
- Application implementation: not authorized.

## Approved decisions

- [x] Discovery/manual endpoint data is untrusted and grants no authority.
- [x] Attended QR bootstrap is preferred; manual fallback carries the same
  high-entropy secret and checksum, not a short numeric credential.
- [x] Bootstrap binds exact certificate fingerprint, peer IDs/keys, secret,
  nonce, transcript, ten-minute maximum, single-use and five-failure lockout.
- [x] Android key is non-exportable in Keystore; coordinator CA/leaf material is
  protected by DPAPI CurrentUser under the signed-in Windows account.
- [x] Control WSS and transfer HTTPS require mutual certificates plus active
  trust, role/UUID, certificate, and resource authorization.
- [x] TLS 1.3 is required/default; restricted logged TLS 1.2 is only the Windows
  10 22H2 compatibility profile; no early data or silent downgrade.
- [x] Rotation, revoke, unpair, key loss and re-enrollment are explicit and
  audited; no trust removal during capture/finalization and no package deletion.
- [x] Security audit excludes secrets, proofs, private keys, subject data, and
  master content.

## Executable scope

`HC-IF-SEC-001` version 1.0.0, four JSON Schema 2020-12 records, security
bindings in HC-IF-CTRL-001 1.2.0 and HC-IF-XFR-001 1.1.0, semantic conformance
helpers, and HC-SEC-TEST-001–012.

## Deferred boundaries

Runtime Android Keystore/Windows DPAPI/CA/TLS services; UI; hostile-network and
penetration testing; power/restart; HIL/field; credential backup/escrow;
receipt signing/offline conveyance; independent review; controlled release;
qualified regulatory/clinical decisions.

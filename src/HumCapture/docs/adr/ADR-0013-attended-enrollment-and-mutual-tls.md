# ADR-0013 — Attended enrollment and mutual TLS

**Status:** Accepted  
**Date:** 2026-09-06

## Context

HumCapture discovers Android capture nodes on field networks that may be
untrusted, shared, intermittently connected, or provided by a Windows laptop
hotspot. Discovery cannot prove identity. Control can start or stop acquisition,
and transfer exposes identifiable scientific masters, so both peers require
cryptographic identity and least-privilege authorization. The MVP relies only
on the signed-in Windows account and must retain a Windows 10 22H2 compatibility
path without weakening the normal security profile.

## Decision

The operator opens a short attended enrollment window on an unpaired Android
node. Android generates a non-exportable P-256 signing key in Android Keystore
and presents a versioned QR bootstrap record containing device identity,
bootstrap endpoint, the bootstrap certificate SPKI SHA-256 fingerprint, a
single-use 128-bit-or-stronger secret, nonce, and ten-minute-or-shorter expiry.
Manual fallback transcribes the same high-entropy grouped value with a checksum;
a short numeric code alone never establishes trust.

The coordinator connects only to the exact advertised fingerprint, proves
possession of the one-time secret, and binds the enrollment transcript to both
peer identities, both public keys, the nonce, and contract version. It maintains
a coordinator-local certification authority and issues role-constrained leaf
certificates. Android retains its private key in Android Keystore. Coordinator
private material is protected for the current Windows user with DPAPI; machine
scope is forbidden.

After enrollment, control uses WSS and transfer uses HTTPS with mutual X.509
authentication. TLS 1.3 profile `HC-TLS-13-001` is required and preferred.
`HC-TLS-12-W10-001` is an explicit Windows 10 compatibility profile only: it
requires ECDHE, AEAD, secure or disabled renegotiation, and a recorded reason.
TLS 1.1 and below, static RSA key exchange, CBC/RC4, anonymous suites, plaintext,
and TLS 1.3 early data are forbidden. Discovery names, addresses, and friendly
labels never authorize a connection.

Trust is one coordinator to one Android enrollment in the MVP. Certificates and
trust records have finite validity and rotate before expiry. Unpair, revoke,
replacement, Android app-data loss, and coordinator key loss require explicit,
audited recovery; no credential is silently replaced. Revocation blocks new
control and transfer immediately but never deletes or corrupts a local master.

## Alternatives considered

- mDNS identity or address allowlists: rejected because discovery and network
  location are forgeable and unstable.
- Shared static password/API key: rejected because it is reusable, difficult to
  bind to a device key, and encourages unsafe logging or copying.
- Public-CA server TLS without client authentication: rejected because it does
  not establish the local coordinator's authority to control a capture node.
- Short six-digit pairing code as the sole secret: rejected because online
  guessing protection cannot provide adequate bootstrap entropy by itself.
- Hard-coded certificate pinning: rejected because lifecycle recovery and key
  rotation would require application updates or unsafe bypasses.
- Machine-wide Windows key protection: rejected because MVP authority belongs
  to the signed-in Windows account.

## Consequences

- First use is deliberately attended and produces auditable trust records.
- Loss of Windows DPAPI-bound material or Android app data requires re-enrollment;
  MVP does not export or escrow private keys.
- Implementations must validate certificate chain, role, peer ID, validity,
  revocation, TLS profile, and resource identity—not merely complete a TLS
  handshake.
- Clock plausibility becomes a readiness concern for certificate validation but
  never permits bypass or deletion of preserved capture data.
- This ADR defines a contract and conformance oracle only. Runtime key storage,
  transport, hostile-network, HIL, field, independent, and regulatory evidence
  remain open.

## Affected components and interfaces

Android identity/pairing/control/transfer, Windows coordinator identity and
current-user credential store, control WSS binding, transfer HTTPS binding,
audit, privacy, recovery, simulator/QA, risk, and regulatory evidence.

## Supersedes / Superseded by

Refines ADR-0003, ADR-0004, ADR-0009, and ADR-0012; supersedes none.

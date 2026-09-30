# HumCapture Pairing and Transport Security Contract

**Contract ID:** HC-IF-SEC-001  
**Version:** 1.0.0  
**Status:** Accepted engineering interface baseline; implementation and independent review remain open  
**Date:** 2026-09-06

## 1. Scope and invariants

This contract binds Android discovery, attended pairing, peer identity,
authorization, control WSS, and transfer HTTPS. It does not implement a PKI,
credential store, network service, or user interface.

- mDNS and manually entered endpoints are routing hints only.
- Only an explicitly paired coordinator may control or collect from a node.
- Security failure cannot erase, truncate, or falsely finalize a scientific
  master; capture and safe finalization retain priority.
- Subject name, date of birth, video, private keys, and reusable secrets SHALL
  NOT appear in discovery, pairing audit, or ordinary logs.
- Successful TLS authentication is necessary but not sufficient: authorization
  also binds role, coordinator/device IDs, trust revision, and target resource.

## 2. Identities and credential custody

`device_id` and `coordinator_id` are stable UUIDs. Android creates an ECDSA P-256
key in Android Keystore with non-exportable key material and records available
hardware-backing evidence without treating hardware backing as universally
required. The coordinator creates its local CA and leaf key under the signed-in
Windows account; private material SHALL use DPAPI CurrentUser protection and
SHALL NOT use LocalMachine scope. Private keys never cross the pairing channel.

Certificates SHALL bind the peer UUID, role (`CAPTURE_NODE` or `COORDINATOR`),
contract version, and appropriate client/server extended-key usage. A trust
record stores public certificate/SPKI fingerprints and lifecycle facts only.
Leaf certificates SHALL be valid for no more than 397 days and SHOULD rotate at
least 30 days before expiry with an auditable overlap. Implementations fail
closed on invalid time and offer an attended clock/re-enrollment recovery path.

## 3. Attended bootstrap

The unpaired Android node opens pairing only after local operator action. Its
QR record conforms to `pairing-bootstrap-qr.schema.json` and contains:

- version, pairing session/device IDs, endpoint, certificate SPKI fingerprint;
- a CSPRNG secret of at least 128 bits and a separate 128-bit-or-stronger nonce;
- issue/expiry UTC with a lifetime no longer than ten minutes; and
- a grouped manual code representing the same secret plus checksum.

The secret is single-use. The node allows at most five failed proofs in the
window, applies increasing delay, emits generic remote errors, and closes the
window on success, expiry, lockout, local cancellation, or app restart. The
secret SHALL exist only in protected ephemeral memory and the QR/manual display;
it SHALL NOT be persisted or logged.

The coordinator validates schema, time window, secret entropy, and exact
bootstrap certificate fingerprint before transmitting a secret proof. The
enrollment transcript binds the pairing session, nonce, both peer IDs, both SPKI
fingerprints, and contract version. A changed endpoint or discovery record does
not change this identity. A transcript mismatch or repeated pairing session ID
is rejected and audited.

## 4. Enrollment and trust lifecycle

```text
UNPAIRED -> BOOTSTRAP_ADVERTISED -> BOOTSTRAP_VERIFIED -> ENROLLING -> PAIRED
             | expired/cancelled/5 failures -> PAIRING_FAILED or LOCKED_OUT
PAIRED -> ROTATION_DUE -> PAIRED
PAIRED or ROTATION_DUE -> REVOKED -> UNPAIRED (explicit reset only)
```

Only these transitions are valid. IDs, CA fingerprint, original pairing time,
and certificate history are immutable; every change increments revision once.
An already paired node rejects bootstrap replacement until explicit unpair or
local factory trust reset. Unpair/revoke is forbidden during recording or
finalization and never deletes packages.

Coordinator reinstall, Windows-account change, DPAPI loss, Android app-data
loss, or Keystore invalidation cannot be repaired by bypassing verification.
The old trust record is marked `LOST` or `REVOKED`, preserved for audit, and a
new attended enrollment creates new peer keys. Compromise triggers immediate
revocation and blocks subsequent sessions.

## 5. Transport profiles

`HC-TLS-13-001` is the default and SHALL negotiate TLS 1.3 with mutual X.509
authentication. TLS 1.3 0-RTT/early data is disabled for control and transfer.

`HC-TLS-12-W10-001` MAY be selected only for a named Windows 10 22H2
compatibility record. It SHALL use ECDHE with ECDSA authentication and AES-GCM,
disable renegotiation or require secure renegotiation, and forbid static RSA,
CBC, RC4, compression, and anonymous suites. Negotiated version, profile,
compatibility reason, and peer certificate fingerprints are audited. A peer
that supports `HC-TLS-13-001` SHALL NOT silently downgrade.

Plain HTTP/WS and TLS 1.1 or earlier are rejected. Control is bound to WSS in
HC-IF-CTRL-001; package collection is bound to HTTPS in HC-IF-XFR-001. There is
no bearer-token, API-key, or cookie alternative in MVP.

## 6. Authorization and replay controls

After chain and signature validation, each request SHALL verify:

1. active non-expired, non-revoked trust revision;
2. exact peer certificate and SPKI fingerprints;
3. certificate role and peer UUID;
4. allowed TLS profile and no forbidden early data/downgrade; and
5. session/source/package identity belongs to that paired device and authorized
   coordinator.

Control retains command/message IDs, expected revisions, deadlines, and
idempotency from HC-IF-CTRL-001. Pairing nonces and secrets are single-use.
Transfer GET/HEAD requests are read-only but remain resource-authorized. Commit
receipt acknowledgement binds exact receipt ID, revision, package content hash,
and request ID. Duplicate identical requests are idempotent; changed content
under a reused ID is rejected and audited.

## 7. Audit, privacy, and availability

Pairing opened/closed, proof failure/lockout, enrollment, authentication or
authorization rejection, profile downgrade use, rotation, revoke, unpair, key
loss, and recovery are auditable. Records may include public fingerprints and
stable peer IDs, but never private keys, bootstrap secret/proof material,
subject identifiers, subject demographics, or master content. Remote errors do
not reveal whether a peer ID, certificate, or package exists.

Security work is bounded so network load, cryptographic retries, or logging
cannot starve master acquisition. Rate limits apply to bootstrap and failed
authentication, not local acquisition. A network or authentication failure
leaves finalized packages available for later authenticated collection or the
existing USB/MTP recovery path.

## 8. Versioning and compatibility

Unknown major versions fail closed. Compatible additive minor versions require
explicit capability negotiation before arming. Trust created under an older
contract is never silently migrated; migration or rotation creates a new
revision and audit event. Certificate algorithm/profile changes require a new
ADR and hostile-network regression plan.

## 9. Verification boundary

HC-SEC-TEST-001–012 validate schemas, bindings, entropy/expiry/single-use and
lockout, transcript identity, trust transitions, authorization, TLS profiles,
replay behavior, recovery, and redaction using synthetic records. They establish
contract-source behavior only. Runtime platform storage, real TLS stacks,
hostile network, power/restart, HIL, field workflow, penetration, independent,
clinical, and regulatory verification remain open.

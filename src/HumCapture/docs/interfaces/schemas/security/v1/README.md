# HC-IF-SEC-001 schemas

These JSON Schema 2020-12 records implement version `1.0.0` of the attended
pairing and transport-security contract. The bootstrap QR is ephemeral and must
never be persisted. Enrollment, trust, and audit records deliberately contain
only public fingerprints and redacted lifecycle facts; private key material,
bootstrap secrets/proofs, subject data, and master content are forbidden.

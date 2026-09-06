# I0.3B Pairing and Transport-Security Contract Verification Report

**Report ID:** HC-VR-I0-3B-001  
**Tags:** I0.3B | SECURITY | PAIRING | MUTUAL-TLS | AUTHORIZATION | RECOVERY | SOURCE-VERIFICATION  
**Date:** 2026-09-06  
**Baseline before change:** `c34bd7ac5cae972e150249b2bc8ac4245c7d7aee`  
**Environment:** Windows; signed-in account; Node.js contract harness

## Objective

Verify that the approved security architecture has versioned, executable,
fail-closed contracts for attended bootstrap, enrolled peer identity, mutual-TLS
bindings, authorization, replay/downgrade rejection, credential lifecycle,
recovery, and audit redaction without claiming runtime security.

## Configuration under test

- HC-IF-SEC-001 1.0.0.
- HC-IF-CTRL-001 / AsyncAPI 3.1.0 document version 1.2.0.
- HC-IF-XFR-001 / OpenAPI 3.1.2 document version 1.1.0.
- Four JSON Schema 2020-12 security records.
- Synthetic UUIDs, public hashes, and `TEST\operator`; no subject data.

## Results

| Test IDs | Objective | Outcome | Evidence / limitation |
|---|---|---|---|
| HC-SEC-TEST-001–003 | Compile schemas and prove concrete mTLS WSS/HTTPS bindings and security response surfaces | Pass | Local Node tests; Redocly and AsyncAPI validators pass |
| HC-SEC-TEST-004–006 | Reject weak/expired/plaintext/mismatched bootstrap, guessing/replay, transcript/key substitution, and excessive certificate lifetime | Pass | Deterministic contract oracle; no real QR, certificate, or handshake |
| HC-SEC-TEST-007–010 | Reject illegal trust transitions, in-capture removal, wrong role/certificate/resource, weak TLS/downgrade/early data, revoked/lost/expired trust | Pass | Deterministic contract oracle; no OS credential store or network |
| HC-SEC-TEST-011–012 | Reject sensitive audit fields and keep discovery/persistent trust free of subject/reusable-secret material | Pass | Schema/semantic source checks; no runtime log or packet inspection |

Regression and supporting checks:

- `npm.cmd test` in `tools/evidence-control`: 54/54 pass, including all 12
  HC-SEC cases and existing control/transfer/evidence-vault cases.
- `npm.cmd test` in `tools/capability-probes/shared`: 55/55 pass.
- `npm.cmd test` in `tools/sbom`: 6/6 pass.
- production dependency audits for both Node surfaces: zero known
  vulnerabilities reported at execution time.
- Redocly CLI 2.51.2 recommended lint: OpenAPI valid; only the advisory
  `info-license` rule was explicitly skipped.
- AsyncAPI CLI 6.0.2: document and referenced documents valid with no governance
  issue reported; CLI emitted only configuration/deprecation/telemetry warnings.
- 59 non-`node_modules` JSON files parse; `git diff --check` passes.

## Defects found and disposition

Initial local execution found an expected control-API version assertion and two
Ajv strict-schema construction issues. The assertion was advanced to 1.2.0;
conditional properties were made explicit; scalar audit detail types were
expressed without a strict union. A subsequent review also bound the manual
fallback code deterministically to the QR secret plus checksum and constrained
audit detail keys. The full suites then passed.

## Evidence classification and conclusion

- Source implemented: **Yes**.
- Build/static checks passed: **Yes**.
- Automated behavior verified: **Yes, contract level**.
- Runtime integration verified: **No**.
- Hardware-in-the-loop verified: **No**.
- Field workflow verified: **No**.
- Penetration/hostile-network verified: **No**.
- Independent review completed: **No**.
- Regulatory or clinical review completed: **No**.

Conclusion: the I0.3B contract baseline is suitable for version control and
independent review. It is not evidence that Android Keystore, Windows DPAPI, a
certificate authority, WSS/HTTPS services, or field security are implemented.

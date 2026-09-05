# HumCapture transfer schemas v1

These JSON Schema 2020-12 records implement `HC-IF-XFR-001` version `1.0.0`:

- `package-manifest.schema.json` identifies one immutable finalized package and
  every artifact it contains;
- `collection-checkpoint.schema.json` records coordinator staging progress and
  safe restart state; and
- `package-verification-record.schema.json` records the common verifier result
  used by HTTPS, USB/MTP, and coordinator-local UVC collection.

Schema validity is necessary but not sufficient. The semantic rules in
`TRANSFER_AND_PACKAGE_CONTRACT.md` and HC-XFR-TEST-001 onward are normative.
Historical finalized manifests are never silently rewritten.

# Phase 0 Evidence Format Notes

## Versioning

The initial evidence format is `1.0.0`. The validator accepts that exact version.
A producer must not relabel incompatible evidence, and a consumer must reject a
newer version until support is implemented and reviewed.

This is an exploratory probe-evidence contract. It is not the later production
capture-package contract.

## Null, absent, and unknown

- A required JSON property is always present.
- JSON `null` means the value was not available or not applicable where the schema permits it.
- `unknown` is used when a required categorical statement cannot be established, particularly timestamp provenance.
- An optional CSV measurement is empty when unavailable.
- A required CSV value is never empty.
- Zero is a measured numeric value and must not be used to mean unavailable.

## Numbers and units

- Raw integer nanosecond values in JSON are decimal strings so they remain exact beyond IEEE-754 safe-integer limits.
- Raw integer timestamps in CSV are base-10 integer text.
- Every timestamp value has a declared clock domain and unit.
- Clock mappings, derived values, and uncertainty never replace the original timestamp.
- Physical units are fixed by the CSV dictionaries; `sensor_native` requires the sensor metadata to define the platform unit and coordinate frame.

## Identity and privacy

- `run_id` is a UUID and is shared by the manifest, capability report, and measurement rows.
- `campaign_id` groups engineering runs; it is not a subject/session identifier.
- Stable test-device identifiers identify qualified hardware without using a subject identifier.
- Fixtures and source-controlled summaries use synthetic identities only.
- Probe evidence must not contain subject names, demographics, or participant recordings.

## Artifacts

- Artifact paths use forward-slash, normalized, relative paths.
- Absolute paths, drive-qualified paths, backslashes, parent traversal, symbolic links, duplicate entries, missing files, and unlisted files are rejected.
- Every artifact has a byte length and lowercase SHA-256 value.
- `run-manifest.json` and `hashes.sha256` are excluded from the artifact inventory to avoid self-referential hashes.
- A campaign index may record the SHA-256 of the completed run manifest.

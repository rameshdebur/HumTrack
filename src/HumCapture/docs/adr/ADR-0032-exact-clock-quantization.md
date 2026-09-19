# ADR-0032 — Exact decimal clock quantization

2026-09-17. User accepted the recommendation; engineering implementation authorized.
Primary Architect/timing/Coordinator; affected Android/UVC, QA, Risk and Release.
Independent review remains open. Architecture skill used for versioned compatibility.

Adopt HC-IF-TIM-001@1.1.0 with timing JSON schema_version 1.1.0 and explicit
mapping_policy EXACT_DECIMAL_NEAREST_TIES_EVEN. Keep all 1.0 schemas, binary
layouts, historical files and native timestamps unchanged. Camera/IMU JSON stays
1.0.0; only timing metadata changes. Caller must dispatch from the declared profile,
not infer a new rounding rule from legacy values or a passing approximate match.

Read the final serialized JSON numeric scale lexeme as an exact decimal rational.
Use BigInteger numerator/denominator arithmetic for scale * uint64 source + int64
offset. Check the unrounded result is in uint64 range, then round once to nearest
integer; an exact half goes to even. Reject mismatch; uncertainty is not an
arithmetic tolerance. Parse each model once, not per sample. No new dependency.

Numeric scale remains a JSON number to avoid a second competing scale field.
Producers must compute from its final serialized decimal representation, not an
earlier binary floating-point value. Consumers must retain numeric lexemes and
must not parse/reserialize scale through double before checking mapped ticks.
Formatting-equivalent decimal/exponent spellings have identical rational value.

Resource envelope: numeric scale text at most 128 characters and explicit base-10
exponent magnitude at most 128. This is a supported implementation envelope, not
permission to truncate or substitute scale. Outside it fails explicitly.

Alternatives: double/decimal arithmetic risks precision loss or overflow for large
ticks; per-producer rounding policy increases MVP complexity; silently upgrading
v1 changes scientific meaning. Exact integer arithmetic plus explicit profile is
small and testable. Revisit envelopes only with shared vectors and contract review.

1.0 mapped-value comparison remains NOT_ASSESSED. In 1.1 report compared sample
count; no mapped samples is not a fabricated PASS. Complete mapped comparisons
do not establish model calibration, uncertainty validity, synchronization accuracy,
full package verification, host activation or regulatory approval.

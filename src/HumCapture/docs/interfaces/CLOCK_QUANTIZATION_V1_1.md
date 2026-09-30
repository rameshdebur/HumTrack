# HC-IF-TIM-001 1.1.0 — Clock quantization supplement

Accepted by user 2026-09-17; ADR-0032. The 1.0.0 timing contract remains the base
for unchanged semantics. This opt-in profile replaces only timing JSON with
schemas/timing/v1.1/timing-metadata.schema.json. Binary format remains 1.0;
camera and IMU JSON remain 1.0.0. Declare exactly one supported timing profile
in the package manifest. TIMING_METADATA format_version must match 1.1.0.
Unknown/conflicting timing profiles fail closed; historical 1.0 is never upgraded.

Required mapping_policy: EXACT_DECIMAL_NEAREST_TIES_EVEN.

For each model, interpret the serialized numeric scale token as exact decimal
rational p/q (q positive); offset is exact signed int64. For a source uint64 s:

1. Compute n = p*s + offset*q with arbitrary precision integers.
2. Require 0 <= n <= UINT64_MAX*q BEFORE rounding.
3. Divide n by q into quotient a and remainder r.
4. If 2*r > q, add one. If 2*r == q, add one only when a is odd.
5. Compare the resulting uint64 exactly with the recorded mapped ticks.

No intermediate rounding and no uncertainty-based tolerance. Unit-scale IDENTITY
requires exact p=q, zero offset and existing epoch/frequency consistency. OFFSET
requires exact unit scale. Model validity, clock binding and uncertainty checks
remain separate. Native data is not changed by validation. Invalid/overflowing
results are refused, not clamped. No mapping is inferred for absent mapped time.

Examples: 1*0.5 -> 0; 3*0.5 -> 2; 5*0.5 -> 2; 7*0.5 -> 4.
Scale lexemes 0.5 and 5e-1 mean exactly the same rational. Producers compute from
the FINAL serialized scale token. Consumers must not round it through a floating-
point JSON representation; parse/re-serialization can change scientific meaning.

Current supported envelope: 128 scale characters and explicit exponent magnitude
<=128. Syntax is the JSON number grammar. Values outside the envelope are
unsupported, never approximated. Offset/native/mapped values must fit their
declared integer widths. Compare every present mapped sample; unavailable model
or missing/ambiguous reference cannot pass. Calibration and uncertainty accuracy
are not established by exact arithmetic consistency.

Cross-language vectors: tools/evidence-control/fixtures/timing/v1/clock-quantization-vectors.json.
Old v1 metadata remains readable but its mapped arithmetic is NOT_ASSESSED without
an explicitly supported policy. No rewriting of old finalized metadata or binaries.

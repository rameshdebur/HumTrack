# Decision required: affine clock mapping to integer ticks

2026-09-17. HC-IF-TIM-001 specifies target = scale * source + offset, but the
binary master stores mapped ticks as uint64 and v1 does not define quantization
of fractional results, precision of scale arithmetic, or comparison tolerance.
For example 3 ticks * 0.5 = 1.5: truncation yields 1, nearest-even yields 2.
Choosing silently would change scientific acceptance behaviour. A model's stated
uncertainty is not automatically an arithmetic mismatch tolerance.

C14H can check schema, clock/epoch references, validity bounds, uncertainty and
exact native video PTS associations without deciding this. It explicitly reports
MappedTimesRecomputed=false. It does not create VERIFIED or alter master bytes.

Recommended next contract refinement for owner/architecture approval:

- Native source timestamps remain authoritative and unchanged.
- Define affine scale as the exact decimal JSON value, compute without binary
  floating-point loss, then round to nearest integer tick with ties to even.
- Reject overflow/out-of-range values; do not use uncertainty to hide arithmetic
  disagreement. Keep model uncertainty as separate scientific evidence.
- Version the clarified producer/consumer profile and add shared boundary vectors.
  Preserve old packages; assess their mappings only under their recorded supported
  policy, otherwise report NOT_ASSESSED. Do not reinterpret old master bytes.

Alternative: explicitly support a producer-declared rounding policy. This improves
legacy flexibility but expands profile/schema/test scope beyond the simpler MVP.
No policy has been implemented or assumed by this batch. Independent human and
regulatory review remain distinct from engineering approval.

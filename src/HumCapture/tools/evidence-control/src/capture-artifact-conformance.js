import { createHash } from "node:crypto";
import { ContractConformanceError } from "./control-contract-conformance.js";
import { validatePackageManifest } from "./transfer-contract-conformance.js";

export const CAPTURE_ARTIFACT_PROFILE = "HC-IF-ART-001@1.0.0";
const identity = ["package_id", "session_id", "trial_id", "source_id", "source_boot_id", "capture_attempt_id", "source_kind"];
const fail = (code, text) => { throw new ContractConformanceError(code, text); };
const hash = (bytes) => createHash("sha256").update(bytes).digest("hex");

function canonical(value) {
  if (typeof value === "string" && !value.isWellFormed()) fail("CAPTURE_CANONICAL_INVALID", "Invalid Unicode surrogate in capture artifact.");
  if (Array.isArray(value)) return `[${value.map(canonical).join(",")}]`;
  if (value !== null && typeof value === "object") return `{${Object.keys(value).sort().map((key) => `${canonical(key)}:${canonical(value[key])}`).join(",")}}`;
  return JSON.stringify(value);
}

// Only the schema's JSON-safe integer/string values are permitted. No timestamp conversion.
export function encodeCaptureArtifact(value) { return Buffer.from(canonical(value), "utf8"); }

function decode(bytes, validate) {
  let value;
  try { value = JSON.parse(new TextDecoder("utf-8", { fatal: true }).decode(bytes)); }
  catch { fail("CAPTURE_JSON_INVALID", "Artifact is not valid UTF-8 JSON."); }
  if (!validate(value)) fail("CAPTURE_SCHEMA_INVALID", "Artifact does not satisfy its versioned schema.");
  if (!Buffer.from(bytes).equals(encodeCaptureArtifact(value))) fail("CAPTURE_CANONICAL_INVALID", "Artifact must be canonical JSON, without duplicate fields or ambiguous encoding.");
  return value;
}

/** Contract check only: not a media verifier, admission API or VERIFIED producer. */
export function validateCaptureArtifacts(manifest, archiveBytes, summaryBytes, { validateArchive, validateSummary }) {
  validatePackageManifest(manifest);
  if (!manifest.interface_profiles?.includes(CAPTURE_ARTIFACT_PROFILE)) fail("CAPTURE_PROFILE_UNSUPPORTED", "Historical packages require their own verifier; do not silently upgrade.");
  const archive = decode(archiveBytes, validateArchive);
  const summary = decode(summaryBytes, validateSummary);
  for (const key of identity) if (archive[key] !== manifest[key] || summary[key] !== manifest[key]) fail("CAPTURE_IDENTITY_MISMATCH", `${key} differs from manifest.`);
  const eventFiles = manifest.artifacts.filter((item) => item.role === "CAPTURE_EVENTS");
  const summaryFiles = manifest.artifacts.filter((item) => item.role === "FINALIZATION_RECORD");
  if (eventFiles.length !== 1 || summaryFiles.length !== 1) fail("CAPTURE_ARTIFACT_COUNT", "Profile requires exactly one archive and one finalization summary.");
  for (const [artifact, bytes] of [[eventFiles[0], archiveBytes], [summaryFiles[0], summaryBytes]]) {
    if (!artifact.required || artifact.media_type !== "application/json" || artifact.format_version !== "1.0.0"
      || artifact.byte_length !== String(bytes.length) || artifact.sha256 !== hash(bytes)) fail("CAPTURE_ARTIFACT_BINDING", "Required artifact/version/length/hash binding differs.");
  }
  if (summary.event_archive_artifact_id !== eventFiles[0].artifact_id || summary.event_archive_sha256 !== hash(archiveBytes)) fail("CAPTURE_ARCHIVE_BINDING", "Summary does not bind the exact archive.");
  if (summary.outcome !== manifest.finalization_outcome || summary.finalized_utc !== manifest.finalized_utc) fail("CAPTURE_FINALIZATION_MISMATCH", "Finalization outcome or audit time differs.");
  if (summary.outcome === "FINALIZED_INCOMPLETE" && summary.reason !== manifest.finalization_reason) fail("CAPTURE_FINALIZATION_MISMATCH", "Incomplete reason differs.");
  if (summary.outcome === "FINALIZED_COMPLETE" && archive.completeness !== "COMPLETE") fail("CAPTURE_FALSE_COMPLETION", "A partial archive cannot prove complete finalization.");
  const firstSamples = archive.events.filter((event) => event.event_type === "FIRST_MASTER_SAMPLE");
  if (firstSamples.length > 1 || (summary.outcome === "FINALIZED_COMPLETE" && firstSamples.length !== 1)) fail("CAPTURE_FIRST_SAMPLE_INVALID", "Complete finalization needs one first-master event; multiple first-master events need separate capture attempts.");
  const ids = new Set();
  let previous;
  let gaps = 0;
  for (const event of archive.events) {
    if (ids.has(event.event_id)) fail("CAPTURE_EVENT_DUPLICATE", "Duplicate event identity.");
    ids.add(event.event_id);
    const time = event.event_source_time;
    if (BigInt(time.ticks) > 18446744073709551615n) fail("CAPTURE_TIME_INVALID", "Source ticks exceed uint64.");
    if (event.event_type === "FIRST_MASTER_SAMPLE" && event.resulting_state !== "RECORDING") fail("CAPTURE_EVENT_INVALID", "First master sample must enter recording.");
    if (previous) {
      const delta = event.event_sequence - previous.event_sequence;
      if (delta <= 0 || event.resulting_source_revision < previous.resulting_source_revision
        || (event.prior_state !== event.resulting_state && event.resulting_source_revision <= previous.resulting_source_revision)) fail("CAPTURE_EVENT_ORDER", "Event sequence/revision regressed or duplicated across a state change.");
      if (time.clock_id !== previous.event_source_time.clock_id || time.ticks_per_second !== previous.event_source_time.ticks_per_second
        || BigInt(time.ticks) < BigInt(previous.event_source_time.ticks)) fail("CAPTURE_TIME_INVALID", "Source clock/frequency changed or ticks regressed within a boot-scoped archive.");
      if (delta !== 1) gaps++;
      if (archive.completeness === "COMPLETE" && (delta !== 1 || event.prior_state !== previous.resulting_state)) fail("CAPTURE_EVENT_GAP", "Complete archive has an event gap or broken state adjacency.");
    }
    previous = event;
  }
  const terminal = archive.events.at(-1);
  if (terminal.event_type !== "FINALIZATION_RESULT" || terminal.event_id !== summary.terminal_event_id
    || terminal.resulting_state !== summary.outcome || terminal.prior_state !== "FINALIZING"
    || terminal.reason !== summary.reason) fail("CAPTURE_TERMINAL_MISMATCH", "Last event does not prove the bound finalization result.");
  if (archive.events.slice(0, -1).some((event) => event.event_type === "FINALIZATION_RESULT")) fail("CAPTURE_TERMINAL_MISMATCH", "Multiple terminal finalizations in one package.");
  return { archiveCompleteness: archive.completeness, observedEventGaps: gaps, finalizationOutcome: summary.outcome };
}

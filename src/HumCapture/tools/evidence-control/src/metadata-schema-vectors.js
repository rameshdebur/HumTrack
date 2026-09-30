import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
export const vectorsPath = path.join(root, "fixtures/timing/v1/metadata-schema-vectors.json");
export async function buildMetadataVectors() {
  const cases = [];
  const id = n => `10000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
  const identity = { package_id: id(1), session_id: id(2), trial_id: id(3), source_id: id(4), source_boot_id: id(5), capture_attempt_id: id(6), source_kind: "UVC" };
  const archive = { schema_version: "1.0.0", ...identity, completeness: "COMPLETE", events: [{ event_id: id(20), event_sequence: 1,
    event_type: "FIRST_MASTER_SAMPLE", event_source_time: { clock_id: id(7), ticks: "1000", ticks_per_second: 10000000 },
    prior_state: "STARTING", resulting_state: "RECORDING", resulting_source_revision: 1 }] };
  const summary = { schema_version: "1.0.0", ...identity, outcome: "FINALIZED_COMPLETE", reason: "Normal stop.",
    terminal_event_id: id(23), event_archive_artifact_id: id(100), event_archive_sha256: "a".repeat(64), finalized_utc: "2026-09-17T10:00:00Z" };
  const sources = [
    ["Timing", "timing", "timing-metadata", "valid-timing-metadata.json"],
    ["Camera", "timing", "camera-metadata", "valid-camera-metadata.json"],
    ["Camera", "timing", "camera-metadata", "valid-android-camera-metadata.json"],
    ["Imu", "timing", "imu-metadata", "valid-imu-metadata.json"],
    ["CaptureEvents", "capture", "capture-event-archive", archive],
    ["Finalization", "capture", "finalization-summary", summary]
  ];
  for (const [kind, group, schema, source] of sources) {
    const data = typeof source === "string" ? JSON.parse(await readFile(path.join(root, "fixtures/timing/v1", source), "utf8")) : source;
    const schemaId = `https://humtrack.invalid/humcapture/${group}/v1/${schema}.schema.json`;
    const prefix = typeof source === "string" ? source : kind;
    const add = (suffix, expected, value) => cases.push({ name: `${prefix}:${suffix}`, kind, schemaId, expected, data: value });
    add("valid-structure-only", true, data);
    for (const [suffix, mutate] of [
      ["version", d => d.schema_version = "99.0.0"],
      ["unknown-field", d => d.unrecognized = true],
      ["missing-identity", d => delete d.capture_attempt_id],
      ["bad-uuid", d => d.capture_attempt_id = "invalid"],
      ["wrong-type", d => d.capture_attempt_id = 123]
    ]) { const changed = structuredClone(data); mutate(changed); add(suffix, false, changed); }
    add("null", false, null);
  }
  const wrongDate = structuredClone(summary); wrongDate.finalized_utc = "2026-99-88T25:61:61Z";
  cases.push({ name: "invalid-date-format", kind: "Finalization", schemaId: "https://humtrack.invalid/humcapture/capture/v1/finalization-summary.schema.json", expected: false, data: wrongDate });
  return { description: "Structural parity only; valid does not mean scientifically or semantically valid.", cases };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  await writeFile(vectorsPath, JSON.stringify(await buildMetadataVectors(), null, 2) + "\n");
}

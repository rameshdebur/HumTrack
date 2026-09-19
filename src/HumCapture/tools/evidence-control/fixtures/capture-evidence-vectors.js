import { writeFile } from "node:fs/promises";
import { pathToFileURL } from "node:url";
import { fixture } from "./capture-fixture.js";

export function captureVectors() {
  const vectors = [];
  const add = (id, accepted, options = {}, mutate = () => {}) => {
    const bundle = fixture(options); mutate(bundle);
    vectors.push({ id, accepted, manifest: bundle.manifest,
      archive: bundle.archiveBytes.toString("base64"), summary: bundle.summaryBytes.toString("base64") });
  };
  add("complete", true);
  add("incomplete-partial-gap", true, { complete: false, partial: true, editArchive: a => a.events.splice(1, 1) });
  add("false-complete", false, { partial: true });
  add("archive-identity", false, { editArchive: a => { a.source_id = "10000000-0000-4000-8000-000000000999"; } });
  add("summary-identity", false, { editSummary: s => { s.capture_attempt_id = "10000000-0000-4000-8000-000000000999"; } });
  add("event-gap", false, { editArchive: a => a.events.splice(1, 1) });
  add("event-duplicate", false, { editArchive: a => { a.events[1].event_id = a.events[0].event_id; } });
  add("sequence-duplicate", false, { editArchive: a => { a.events[1].event_sequence = 1; } });
  add("revision-stale", false, { editArchive: a => { a.events[1].resulting_source_revision = 1; } });
  add("ticks-overflow", false, { editArchive: a => { a.events[0].event_source_time.ticks = "18446744073709551616"; } });
  add("ticks-regressed", false, { editArchive: a => { a.events[1].event_source_time.ticks = "1"; } });
  add("frequency-changed", false, { editArchive: a => { a.events[1].event_source_time.ticks_per_second = 1; } });
  add("clock-id-text-case", false, { editArchive: a => {
    for (const e of a.events) e.event_source_time.clock_id = "a0000000-0000-4000-8000-000000000007";
    a.events[1].event_source_time.clock_id = "A0000000-0000-4000-8000-000000000007";
  } });
  add("state-adjacency", false, { editArchive: a => { a.events[1].prior_state = "STARTING"; } });
  add("first-master-missing", false, { editArchive: a => { a.events[0].event_type = "STATE_TRANSITION"; } });
  add("first-master-wrong-state", false, { editArchive: a => { a.events[0].resulting_state = "STARTING"; } });
  add("early-finalization", false, { editArchive: a => { a.events[1].event_type = "FINALIZATION_RESULT"; } });
  add("terminal-reason", false, { editArchive: a => { a.events.at(-1).reason = "Different recorded reason"; } });
  add("summary-hash", false, { editSummary: s => { s.event_archive_sha256 = "0".repeat(64); } });
  add("audit-time", false, { editSummary: s => { s.finalized_utc = "2026-09-16T10:00:00Z"; } });
  add("byte-tamper", false, {}, b => { b.archiveBytes = Buffer.concat([b.archiveBytes, Buffer.from(" ")]); });
  const reason = "Unicode हिंदी 😀 \u2028 \u000f \b \t \n \f \r \\\" preserved";
  add("unicode-canonical", true, { editArchive: a => { a.events.at(-1).reason = reason; }, editSummary: s => { s.reason = reason; } });
  return { version: 1, evidence: "synthetic contract vectors; not decoded media or hardware evidence", vectors };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (process.argv[2] !== "--write") throw new Error("Use --write to regenerate synthetic vectors.");
  await writeFile(new URL("./timing/v1/capture-evidence-vectors.json", import.meta.url), `${JSON.stringify(captureVectors(), null, 2)}\n`);
}

import { readFileSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { fixture } from "./capture-fixture.js";
import { encodeCaptureArtifact } from "../src/capture-artifact-conformance.js";
import { computeArtifactSetSha256, computePackageContentSha256 } from "../src/transfer-contract-conformance.js";
import { sha256, decodeFrameStream, decodeImuStream, encodeFrameStream, encodeImuStream } from "../src/timing-contract-conformance.js";

const read = name => readFileSync(new URL(`./timing/v1/${name}`, import.meta.url));
const json = name => JSON.parse(read(name));
const encode = value => encodeCaptureArtifact(value);

export function packageInputVectors() {
  const result = [];
  for (const [name, complete, android, exact] of [["uvc-legacy", true, false, false], ["android-exact", true, true, true], ["incomplete-survivors", false, false, false]]) {
    const b = fixture({ complete, partial: !complete });
    const manifest = b.manifest;
    const attempt = "10000000-0000-4000-8000-000000000001";
    manifest.capture_attempt_id = attempt; manifest.source_kind = android ? "ANDROID" : "UVC";
    manifest.interface_profiles = ["HC-IF-ART-001@1.0.0", ...(complete ? [`HC-IF-TIM-001@${exact ? "1.1.0" : "1.0.0"}`] : [])];
    const archive = JSON.parse(b.archiveBytes); const summary = JSON.parse(b.summaryBytes);
    for (const item of [archive, summary]) { item.capture_attempt_id = attempt; item.source_kind = manifest.source_kind; }
    const archiveBytes = encode(archive); summary.event_archive_sha256 = sha256(archiveBytes);
    const files = { "events/capture.json": archiveBytes, "metadata/finalization.json": encode(summary) };
    manifest.artifacts = manifest.artifacts.slice(0, 2);
    let number = 200;
    const add = (role, path, media, version, bytes, timed = false) => {
      files[path] = bytes;
      manifest.artifacts.push({ artifact_id: `10000000-0000-4000-8000-${String(number++).padStart(12, "0")}`, role, relative_path: path,
        media_type: media, format_version: version, required: true, session_id: manifest.session_id, trial_id: manifest.trial_id,
        source_id: manifest.source_id, capture_attempt_id: attempt, byte_length: String(bytes.length), sha256: sha256(bytes),
        ...(timed ? { timing_coverage: { clock_id: "20000000-0000-4000-8000-000000000002", ticks_per_second: 1000000000,
          first_ticks: "1000000000", last_ticks: "1066666666", record_count: "3", discontinuity_count: "0" } } : {}) });
    };
    if (complete) {
      const timing = json("valid-timing-metadata.json");
      timing.clocks.push({ ...timing.clocks[0], clock_id: "20000000-0000-4000-8000-000000000009", ticks_per_second: 90000, provenance: "ENCODER_PTS", authority: "PRESENTATION" });
      timing.streams[0].presentation_clock_id = timing.clocks.at(-1).clock_id;
      timing.streams[1].native_clock_id = timing.streams[0].native_clock_id;
      timing.clock_models[0].uncertainty_ns = 100000;
      let frames = read("valid-frame-timestamps.bin"); let imu = read("valid-imu-samples.bin");
      if (exact) {
        timing.schema_version = "1.1.0"; timing.mapping_policy = "EXACT_DECIMAL_NEAREST_TIES_EVEN"; timing.clock_models[0].scale = 1;
        const f = decodeFrameStream(frames); const i = decodeImuStream(imu);
        frames = encodeFrameStream({ ...f.header, records: f.records.map(r => ({ ...r, mappedSessionTicks: r.nativeTicks + 4000000000n })) });
        imu = encodeImuStream({ ...i.header, records: i.records.map(r => ({ ...r, mappedSessionTicks: r.nativeTicks + 4000000000n })) });
      }
      add("SCIENTIFIC_MASTER_VIDEO", "media/master.mp4", "video/mp4", "1.0.0", Buffer.from("SYNTHETIC TEST OBSERVATIONS; NOT DECODABLE VIDEO"), true);
      add("FRAME_TIMESTAMPS", "timing/frame-timestamps.bin", "application/vnd.humcapture.frame-timestamps", "1.0", frames, true);
      add("TIMING_METADATA", "metadata/timing-metadata.json", "application/json", exact ? "1.1.0" : "1.0.0", encode(timing));
      add("CAMERA_METADATA", "metadata/camera-metadata.json", "application/json", "1.0.0", encode(json("valid-camera-metadata.json")));
      if (android) {
        add("IMU_SAMPLES", "imu/imu-samples.bin", "application/vnd.humcapture.imu-samples", "1.0", imu, true);
        add("IMU_METADATA", "imu/imu-metadata.json", "application/json", "1.0.0", encode(json("valid-imu-metadata.json")));
      }
    }
    for (const a of manifest.artifacts) { a.capture_attempt_id = attempt; a.byte_length = String(files[a.relative_path].length); a.sha256 = sha256(files[a.relative_path]); }
    manifest.artifact_count = manifest.artifacts.length;
    manifest.package_byte_length = manifest.artifacts.reduce((n, a) => n + BigInt(a.byte_length), 0n).toString();
    manifest.artifact_set_sha256 = computeArtifactSetSha256(manifest.artifacts);
    manifest.package_content_sha256 = computePackageContentSha256(manifest);
    files["package-manifest.json"] = encode(manifest);
    result.push({ name, files: Object.fromEntries(Object.entries(files).map(([path, bytes]) => [path, bytes.toString("base64")])) });
  }
  return { evidence: "synthetic on-disk package inputs; no real decoder or camera evidence", vectors: result };
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (process.argv[2] !== "--write") throw new Error("Use --write to regenerate synthetic vectors.");
  writeFileSync(new URL("./timing/v1/package-input-vectors.json", import.meta.url), `${JSON.stringify(packageInputVectors(), null, 2)}\n`);
}

import { writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { packageInputVectors } from "./package-input-vectors.js";
import { buildCoverage } from "../src/timing-coverage-v1.1.js";
import { decodeFrameStream, decodeImuStream, encodeFrameStream, sha256 } from "../src/timing-contract-conformance.js";
import { canonicalJson, computeArtifactSetSha256, computePackageContentSha256 } from "../src/transfer-contract-conformance.js";

export function coverageVectors() {
  return { evidence: "synthetic manifest 1.1; no real camera evidence", vectors: packageInputVectors().vectors
    .filter(v => v.name !== "incomplete-survivors").map(v => {
      const files = Object.fromEntries(Object.entries(v.files).map(([p, b]) => [p, Buffer.from(b, "base64")]));
      const m = JSON.parse(files["package-manifest.json"]), timing = JSON.parse(files["metadata/timing-metadata.json"]);
      m.schema_version = "1.1.0";
      m.interface_profiles = ["HC-IF-ART-001@1.0.0", "HC-IF-TIM-001@1.1.0", "HC-IF-XFR-001@1.3.0"];
      timing.schema_version = "1.1.0"; timing.mapping_policy = "EXACT_DECIMAL_NEAREST_TIES_EVEN";
      timing.clock_models[0].scale = 1;
      const oldFrames = decodeFrameStream(files["timing/frame-timestamps.bin"]);
      files["timing/frame-timestamps.bin"] = encodeFrameStream({ ...oldFrames.header,
        records: oldFrames.records.map(s => ({ ...s, mappedSessionTicks: s.nativeTicks + 4000000000n })) });
      files["metadata/timing-metadata.json"] = Buffer.from(canonicalJson(timing));
      for (const a of m.artifacts) {
        if (a.role === "TIMING_METADATA") a.format_version = "1.1.0";
        if (a.timing_coverage) {
          const imu = a.role === "IMU_SAMPLES";
          const data = imu ? decodeImuStream(files[a.relative_path]) : decodeFrameStream(files["timing/frame-timestamps.bin"]);
          const stream = timing.streams.find(s => s.kind === (imu ? "IMU_SAMPLES" : "FRAME_TIMESTAMPS"));
          const clock = timing.clocks.find(c => c.clock_id === stream.native_clock_id);
          const records = a.role === "SCIENTIFIC_MASTER_VIDEO" ? data.records.filter(s => s.disposition === 1) : data.records;
          a.timing_coverage = buildCoverage(stream.stream_id, clock.clock_id, clock.ticks_per_second, records);
        }
        a.byte_length = String(files[a.relative_path].length); a.sha256 = sha256(files[a.relative_path]);
      }
      m.package_byte_length = m.artifacts.reduce((n, a) => n + BigInt(a.byte_length), 0n).toString();
      m.artifact_set_sha256 = computeArtifactSetSha256(m.artifacts); m.package_content_sha256 = computePackageContentSha256(m);
      files["package-manifest.json"] = Buffer.from(canonicalJson(m));
      return { name: v.name.startsWith("uvc") ? "uvc-covered" : "android-covered",
        files: Object.fromEntries(Object.entries(files).map(([p, b]) => [p, b.toString("base64")])) };
    }) };
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  if (process.argv[2] !== "--write") throw new Error("Use --write to regenerate synthetic coverage vectors.");
  writeFileSync(new URL("./timing/v1/coverage-package-vectors.json", import.meta.url), JSON.stringify(coverageVectors(), null, 2) + "\n");
}

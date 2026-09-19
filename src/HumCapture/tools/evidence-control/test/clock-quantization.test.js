import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { quantizeClock } from "../src/clock-quantization.js";
import { validateTimingPackageProfile } from "../src/timing-contract-conformance.js";

test("HC-CLK-001 exact quantization shared boundary vectors", async () => {
  const vectors = JSON.parse(await readFile(new URL("../fixtures/timing/v1/clock-quantization-vectors.json", import.meta.url)));
  for (const v of vectors.cases) {
    const run = () => quantizeClock(v.source, v.scale, v.offset);
    if (v.expected === null) assert.throws(run, undefined, v.name);
    else assert.equal(run(), v.expected, v.name);
  }
});

test("HC-CLK-002 explicit v1.1 schema and manifest dispatch preserve legacy", async () => {
  const ajv = new Ajv2020({ strict: true }); addFormats(ajv);
  const base = new URL("../../../docs/interfaces/schemas/timing/", import.meta.url);
  const old = ajv.compile(JSON.parse(await readFile(new URL("v1/timing-metadata.schema.json", base))));
  const current = ajv.compile(JSON.parse(await readFile(new URL("v1.1/timing-metadata.schema.json", base))));
  const legacy = JSON.parse(await readFile(new URL("../fixtures/timing/v1/valid-timing-metadata.json", import.meta.url)));
  const next = { ...legacy, schema_version: "1.1.0", mapping_policy: "EXACT_DECIMAL_NEAREST_TIES_EVEN" };
  assert.equal(old(legacy), true); assert.equal(current(legacy), false);
  assert.equal(old(next), false); assert.equal(current(next), true);
  assert.equal(current({ ...next, mapping_policy: "FLOOR" }), false);
  const manifest = { source_kind: "UVC", interface_profiles: ["HC-IF-TIM-001@1.1.0"], artifacts: [
    { role: "FRAME_TIMESTAMPS", media_type: "application/vnd.humcapture.frame-timestamps", format_version: "1.0", relative_path: "timing/frame-timestamps.bin" },
    { role: "TIMING_METADATA", media_type: "application/json", format_version: "1.1.0", relative_path: "metadata/timing-metadata.json" },
    { role: "CAMERA_METADATA", media_type: "application/json", format_version: "1.0.0", relative_path: "metadata/camera-metadata.json" }
  ] };
  assert.equal(validateTimingPackageProfile(manifest), true);
  for (const profiles of [["HC-IF-TIM-001@1.0.0", "HC-IF-TIM-001@1.1.0"], ["HC-IF-TIM-001@2.0.0"], []])
    assert.throws(() => validateTimingPackageProfile({ ...manifest, interface_profiles: profiles }));
  manifest.artifacts[1].format_version = "1.0.0";
  assert.throws(() => validateTimingPackageProfile(manifest));
});

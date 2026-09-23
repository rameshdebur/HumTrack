import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { coverageVectors } from "../fixtures/coverage-package-vectors.js";
import { buildCoverage } from "../src/timing-coverage-v1.1.js";
import { validatePackageManifest } from "../src/transfer-contract-conformance.js";
const json = p => JSON.parse(readFileSync(new URL(p, import.meta.url)));

test("HC-COV-001 versioned manifest schema and independently generated native vectors", () => {
  const ajv = new Ajv2020({ strict: true, allErrors: true }); addFormats(ajv);
  ajv.addSchema(json("../../../docs/interfaces/schemas/control/v1/common.schema.json"));
  const old = ajv.compile(json("../../../docs/interfaces/schemas/transfer/v1/package-manifest.schema.json"));
  const current = ajv.compile(json("../../../docs/interfaces/schemas/transfer/v1.1/package-manifest.schema.json"));
  const retained = json("../fixtures/timing/v1/coverage-package-vectors.json");
  assert.deepEqual(retained, coverageVectors());
  for (const v of retained.vectors) {
    const m = JSON.parse(Buffer.from(v.files["package-manifest.json"], "base64"));
    assert.equal(current(m), true, JSON.stringify(current.errors));
    assert.equal(old(m), false); assert.equal(validatePackageManifest(m), true);
    const broken = structuredClone(m);
    broken.artifacts.find(a => a.timing_coverage).timing_coverage.spans[0].lane_id = -1;
    assert.equal(current(broken), false);
    const wrongCount = structuredClone(m);
    wrongCount.artifacts.find(a => a.timing_coverage).timing_coverage.record_count = "4";
    assert.throws(() => validatePackageManifest(wrongCount), /span sum/);
  }
});
test("HC-COV-002 interleaved lanes, regressions, gaps and empty native streams", () => {
  const s = (sensorStreamId, sequence, nativeTicks, segmentId = 0) => ({ sensorStreamId, sequence: BigInt(sequence), nativeTicks: BigInt(nativeTicks), segmentId });
  const c = buildCoverage("stream", "clock", 1000,
    [s(1, 0, 10), s(2, 0, 1), s(1, 2, 20), s(1, 3, 2, 1), s(2, 1, 2), s(1, 0, 1, 1)]);
  assert.deepEqual(c.spans.map(s => [s.lane_id, s.run_index, s.record_count, s.sequence_gap_count]),
    [[1, 0, "2", "1"], [1, 1, "1", "0"], [1, 2, "1", "0"], [2, 0, "2", "0"]]);
  assert.deepEqual(buildCoverage("stream", "clock", 1000, []).spans, []);
});

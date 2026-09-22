import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { scientificVectors } from "../fixtures/scientific-vectors.js";
import { evaluateScientificConditionsV1_1 } from "../src/scientific-conditions-v1.1.js";

const json = path => JSON.parse(readFileSync(new URL(path, import.meta.url)));
test("HC-SCI-001 versioned protocol extension and legacy isolation", () => {
  const ajv = new Ajv2020({ strict: true, allErrors: true }); addFormats(ajv);
  const prefix = "../../../docs/interfaces/schemas/control/";
  const common = json(prefix + "v1/common.schema.json"); ajv.addSchema(common);
  const legacy = ajv.compile(json(prefix + "v1/protocol-snapshot.schema.json"));
  const current = ajv.compile(json(prefix + "v1.1/protocol-snapshot.schema.json"));
  const { protocol } = scientificVectors();
  assert.equal(current(protocol), true, JSON.stringify(current.errors));
  assert.equal(legacy(protocol), false);
  const old = structuredClone(protocol); old.schema_version = "1.0.0"; delete old.scientific_rules;
  assert.equal(legacy(old), true); assert.equal(current(old), false);
  for (const change of [p => p.scientific_rules[0].metric = "UNSUPPORTED", p => p.scientific_rules[0].threshold = 0.5,
    p => p.scientific_rules[0].level = "OPTIONAL", p => p.scientific_rules[0].operator = "EXPRESSION"]) {
    const value = structuredClone(protocol); change(value); assert.equal(current(value), false);
  }
});
test("HC-SCI-002 missing evidence differs from measured failure; shared cases current", () => {
  const retained = json("../fixtures/timing/v1/scientific-rules-vectors.json");
  assert.deepEqual(retained, scientificVectors());
  for (const vector of retained.cases) assert.equal(evaluateScientificConditionsV1_1(vector.conditions), vector.expected);
  assert.throws(() => evaluateScientificConditionsV1_1([{ level: "REQUIRED", outcome: "NOT_APPLICABLE" }]));
});

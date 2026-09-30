import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { buildMetadataVectors, vectorsPath } from "../src/metadata-schema-vectors.js";

test("HC-META-001 shared metadata vectors match controlled sources and Ajv", async () => {
  const vectors = JSON.parse(await readFile(vectorsPath, "utf8"));
  assert.deepEqual(vectors, await buildMetadataVectors());
  const ajv = new Ajv2020({ strict: true, allErrors: true }); addFormats(ajv);
  const base = new URL("../../../docs/interfaces/schemas/", import.meta.url);
  const names = ["control/v1/common", "timing/v1/timing-metadata", "timing/v1/camera-metadata", "timing/v1/imu-metadata", "capture/v1/capture-event-archive", "capture/v1/finalization-summary"];
  for (const name of names) ajv.addSchema(JSON.parse(await readFile(new URL(name + ".schema.json", base), "utf8")));
  for (const vector of vectors.cases) assert.equal(ajv.getSchema(vector.schemaId)(vector.data), vector.expected, vector.name);
});

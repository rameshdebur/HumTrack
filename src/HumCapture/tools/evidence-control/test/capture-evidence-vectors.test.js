import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { captureVectors } from "../fixtures/capture-evidence-vectors.js";
import { encodeCaptureArtifact, validateCaptureArtifacts } from "../src/capture-artifact-conformance.js";

test("HC-ART-PARITY-001 shared finalization vectors agree and stay current", async () => {
  const current = captureVectors();
  assert.deepEqual(JSON.parse(await readFile(new URL("../fixtures/timing/v1/capture-evidence-vectors.json", import.meta.url))), current);
  const ajv = new Ajv2020({ strict: true }); addFormats(ajv);
  const load = async p => JSON.parse(await readFile(new URL(`../../../docs/interfaces/schemas/${p}.schema.json`, import.meta.url)));
  ajv.addSchema(await load("control/v1/common"));
  const validators = { validateArchive: ajv.compile(await load("capture/v1/capture-event-archive")),
    validateSummary: ajv.compile(await load("capture/v1/finalization-summary")) };
  for (const v of current.vectors) {
    const run = () => validateCaptureArtifacts(v.manifest, Buffer.from(v.archive, "base64"), Buffer.from(v.summary, "base64"), validators);
    if (v.accepted) assert.doesNotThrow(run, v.id); else assert.throws(run, undefined, v.id);
  }
});

test("HC-ART-PARITY-002 invalid Unicode is not canonical capture evidence", () => {
  assert.throws(() => encodeCaptureArtifact({ reason: "bad \ud800" }), /Unicode/);
  assert.throws(() => encodeCaptureArtifact({ ["bad \udfff"]: "reason" }), /Unicode/);
});

import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import { packageInputVectors } from "../fixtures/package-input-vectors.js";
import { validatePackageManifest } from "../src/transfer-contract-conformance.js";
import { validateTimingPackageProfile } from "../src/timing-contract-conformance.js";
import { validateCaptureArtifacts } from "../src/capture-artifact-conformance.js";

test("HC-PKG-INPUT-001 shared file fixtures satisfy manifest timing and finalization contracts", async () => {
  const vectors = packageInputVectors();
  assert.deepEqual(JSON.parse(await readFile(new URL("../fixtures/timing/v1/package-input-vectors.json", import.meta.url))), vectors);
  const ajv = new Ajv2020({ strict: true }); addFormats(ajv);
  const load = async name => JSON.parse(await readFile(new URL(`../../../docs/interfaces/schemas/${name}.schema.json`, import.meta.url)));
  ajv.addSchema(await load("control/v1/common"));
  const schema = ajv.compile(await load("transfer/v1/package-manifest"));
  const validators = { validateArchive: ajv.compile(await load("capture/v1/capture-event-archive")), validateSummary: ajv.compile(await load("capture/v1/finalization-summary")) };
  for (const v of vectors.vectors) {
    const read = p => Buffer.from(v.files[p], "base64");
    const manifest = JSON.parse(read("package-manifest.json"));
    assert.ok(schema(manifest), JSON.stringify(schema.errors)); validatePackageManifest(manifest);
    if (manifest.finalization_outcome === "FINALIZED_COMPLETE") validateTimingPackageProfile(manifest);
    validateCaptureArtifacts(manifest, read("events/capture.json"), read("metadata/finalization.json"), validators);
  }
});

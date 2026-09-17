import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { generateSbom } from "../src/sbom.js";
import { documentPath, readInputs, renderRegister, repoRoot, validateRegister } from "../src/licence-register.js";

test("licence register covers every exact SBOM identity and rendered document is current", async () => {
  const { bom, register } = await readInputs();
  assert.equal(validateRegister(bom, register).length, bom.components.length + 1);
  assert.equal((await readFile(documentPath, "utf8")).replaceAll("\r\n", "\n"), renderRegister(bom, register));
});

test("licence coverage rejects added removed upgraded or duplicate dependencies", async () => {
  const { bom, register } = await readInputs();
  const added = structuredClone(bom);
  added.components.push({ name: "Unreviewed", "bom-ref": "pkg:npm/unreviewed@1.0.0" });
  assert.throws(() => validateRegister(added, register), /Missing\/version-changed/);
  const removed = structuredClone(bom); removed.components.pop();
  assert.throws(() => validateRegister(removed, register), /Stale\/unknown/);
  const upgraded = structuredClone(bom); upgraded.components[0]["bom-ref"] += "-new";
  assert.throws(() => validateRegister(upgraded, register), /Stale\/unknown/);
  const duplicated = structuredClone(register); duplicated.entries.push(duplicated.entries[0]);
  assert.throws(() => validateRegister(bom, duplicated), /Duplicate/);
});

test("licence register requires cost obligations evidence and explicit review state", async () => {
  const { bom, register } = await readInputs();
  for (const change of [r => r.entries[0].evidence = [], r => r.entries[0].review_status = "",
    r => r.profiles.MIT.cost = "", r => r.profiles.MIT.obligations = "", r => r.entries[0].profile = "missing"]) {
    const altered = structuredClone(register); change(altered);
    assert.throws(() => validateRegister(bom, altered), /Incomplete|Missing/);
  }
});

test("selected candidate cannot remain not-installed after entering the SBOM", async () => {
  const { bom, register } = await readInputs();
  const c = { ...register.entries[0], name: "SyntheticCandidate", version: "1.0.0" };
  register.candidates = [c];
  const ref = `pkg:nuget/${c.name}@${c.version}`;
  bom.components.push({ name: c.name, version: c.version, "bom-ref": ref });
  register.entries.push({ ...register.entries[0], ref });
  assert.throws(() => validateRegister(bom, register), /Candidate now inventoried/);
});

test("retained SBOM and licence baseline match current source manifests", async t => {
  const { bom, register } = await readInputs();
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-licence-check-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const outputPath = path.join(root, "fresh.json");
  await generateSbom({ repoRoot, outputPath, productVersion: bom.metadata.component.version, timestamp: bom.metadata.timestamp });
  const fresh = JSON.parse(await readFile(outputPath, "utf8"));
  assert.deepEqual(fresh, bom, "Source manifests changed: regenerate SBOM and review licence register.");
  validateRegister(fresh, register);
});

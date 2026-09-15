import assert from "node:assert/strict";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { discoverDependencyManifests, generateSbom, parseDecoderLock, parsePinnedChocolateyPackages, parsePinnedGitHubActions, validateSbomFile } from "../src/sbom.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = path.resolve(toolRoot, "..", "..");

async function outputFixture(t, name) {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-sbom-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  return path.join(root, name);
}

test("generates a valid CycloneDX 1.7 inventory with all declared surfaces", async (t) => {
  const output = await outputFixture(t, "humcapture.cdx.json");
  const generated = await generateSbom({ repoRoot, outputPath: output, productVersion: "0.1.0-test", timestamp: "2026-08-31T00:00:00.000Z" });
  assert.ok(generated.componentCount >= 20);
  const validated = await validateSbomFile(output);
  assert.equal(validated.componentCount, generated.componentCount);
  const bom = JSON.parse(await readFile(output, "utf8"));
  const decoder = bom.components.find((item) => item.name === "FFmpeg Windows essentials archive");
  assert.equal(decoder.scope, "excluded");
  assert.equal(decoder.version, "9.0.1");
  assert.equal(decoder.hashes[0].content, "fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9");
  const host = bom.components.find((item) => item.name === "HumCapture.Coordinator.Host");
  assert.ok(host);
  const repository = bom.components.find((item) => item.name === "HumCapture.Coordinator.Repository");
  assert.ok(bom.dependencies.find((item) => item.ref === host["bom-ref"]).dependsOn.includes(repository["bom-ref"]));
  assert.ok(bom.components.some((item) => item.name === "ajv" && item.version === "8.20.0"));
  assert.ok(bom.components.some((item) => item.name === "Microsoft.NETCore.App"));
  const sqlite = bom.components.find((item) => item.name === "Microsoft.Data.Sqlite" && item.version === "10.0.12");
  assert.equal(sqlite.scope, "required");
  assert.equal(sqlite.hashes[0].alg, "SHA-512");
  assert.ok(bom.components.some((item) => item.name === "SQLitePCLRaw.bundle_e_sqlite3" && item.version === "2.1.12"));
  assert.ok(bom.components.some((item) => item.name === "HumCapture.Coordinator.Repository"));
  assert.ok(bom.components.some((item) => item.name.includes("Windows Media Foundation")));
  assert.ok(bom.components.some((item) => item.name === "checkout" && item.group === "actions"));
  assert.ok(bom.components.some((item) => item.name === "setup-node" && item.group === "actions"));
  assert.ok(bom.components.some((item) => item.name === "setup-dotnet" && item.group === "actions"));
  assert.ok(bom.components.some((item) => item.name === "windows-sdk-10-version-2004-all" && item.version === "10.0.19041.685"));
});

test("decoder candidate requires immutable archive identity and cannot silently become enabled", async () => {
  assert.throws(() => parseDecoderLock("null"), /Decoder lock/);
  const baseline = JSON.parse(await readFile(path.join(repoRoot, "apps/windows-coordinator/decoder/decoder-lock.json"), "utf8"));
  assert.equal(parseDecoderLock(JSON.stringify(baseline)).scope, "excluded");
  const unobserved = { ...baseline, binary_verification: "NOT_DOWNLOADED_OR_VERIFIED" };
  delete unobserved.executable_sha256;
  assert.equal(parseDecoderLock(JSON.stringify(unobserved)).scope, "excluded");
  assert.ok(parseDecoderLock(JSON.stringify(baseline)).properties.some(p => p.name === "humcapture:executable-sha256"));
  for (const change of [
    { version: "latest" }, { archive_sha256: "" }, { runtime_enabled: true },
    { redistribution_approved: true }, { archive_url: "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" },
    { license_review: "APPROVED" }, { binary_verification: "QUALIFIED" },
    { executable_sha256: undefined }, { executable_sha256: {} },
    { executable_sha256: { ...baseline.executable_sha256, "ffmpeg.exe": "missing" } },
    { binary_verification: "NOT_DOWNLOADED_OR_VERIFIED" }
  ]) assert.throws(() => parseDecoderLock(JSON.stringify({ ...baseline, ...change })), /Decoder lock/);
});

test("generation is deterministic for fixed inputs version and timestamp", async (t) => {
  const first = await outputFixture(t, "first.cdx.json");
  const second = path.join(path.dirname(first), "second.cdx.json");
  const options = { repoRoot, productVersion: "0.1.0-test", timestamp: "2026-08-31T00:00:00.000Z" };
  await generateSbom({ ...options, outputPath: first });
  await generateSbom({ ...options, outputPath: second });
  assert.equal(await readFile(first, "utf8"), await readFile(second, "utf8"));
});

test("dependency graph has complete closure", async (t) => {
  const output = await outputFixture(t, "closure.cdx.json");
  await generateSbom({ repoRoot, outputPath: output, productVersion: "0.1.0-test", timestamp: "2026-08-31T00:00:00.000Z" });
  const bom = JSON.parse(await readFile(output, "utf8"));
  const refs = new Set([bom.metadata.component["bom-ref"], ...bom.components.map((item) => item["bom-ref"])]);
  assert.deepEqual(new Set(bom.dependencies.map((item) => item.ref)), refs);
});

test("manifest discovery detects a future Android dependency surface", async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-sbom-discovery-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await mkdir(path.join(root, "apps", "android"), { recursive: true });
  await writeFile(path.join(root, "apps", "android", "build.gradle.kts"), "plugins {}\n");
  assert.deepEqual(await discoverDependencyManifests(root), ["apps/android/build.gradle.kts"]);
});

test("GitHub Actions must use immutable commit pins", () => {
  assert.throws(
    () => parsePinnedGitHubActions("steps:\n  - uses: actions/checkout@v6\n"),
    /40-character commit SHA/
  );
  const actions = parsePinnedGitHubActions("steps:\n  - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2\n");
  assert.equal(actions[0].version, "v6.0.2");
  assert.equal(actions[0].properties.find((item) => item.name === "humcapture:commit-pin").value, "de0fac2e4500dabe0009e67214ff5f5447ce83dd");
});

test("Chocolatey build packages must use exact versions", () => {
  assert.throws(
    () => parsePinnedChocolateyPackages("steps:\n  run: choco install windows-sdk-10-version-2004-all --yes\n"),
    /exact --version/
  );
  const packages = parsePinnedChocolateyPackages("steps:\n  run: choco install windows-sdk-10-version-2004-all --version 10.0.19041.685 --yes\n");
  assert.equal(packages[0].version, "10.0.19041.685");
});

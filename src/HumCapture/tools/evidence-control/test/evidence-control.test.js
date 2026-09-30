import assert from "node:assert/strict";
import { chmod, cp, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { execFile } from "node:child_process";
import { createHash } from "node:crypto";
import { promisify } from "node:util";
import { ControlError, createReleaseRecord, importEvidence, initializeVault, verifyVault } from "../src/evidence-control.js";

const execFileAsync = promisify(execFile);

async function fixture(t) {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-evidence-control-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const source = path.join(root, "source");
  const vault = path.join(root, "vault");
  await mkdir(source);
  await writeFile(path.join(source, "capture-result.json"), "{\"complete\":false}\n");
  await writeFile(path.join(source, "frames.csv"), "frame,timestamp\n1,1\n");
  const releasePath = path.join(root, "release.json");
  await writeFile(releasePath, JSON.stringify({
    record_format_version: "1.1.0",
    release_id: "HC-ENG-20260831T000000Z-0123456789ab",
    classification: "ENGINEERING_SNAPSHOT",
    software_version: "test",
    created_utc: "2026-08-31T00:00:00.000Z",
    created_by_windows_account: "TEST\\operator",
    repository: { humcapture_path: "src/HumCapture", parent_commit_sha: "0".repeat(40), tracking_state: "UNTRACKED", worktree_state: "DIRTY" },
    source_inventory: { algorithm: "sorted-relative-path-size-sha256-v1", sha256: "0".repeat(64), file_count: 1, exclusions: ["bin"] },
    executed_artifacts: [],
    technical_release_preconditions_met: false,
    regulatory_use_permitted: false,
    limitations: ["Synthetic test fixture."]
  }));
  return { root, source, vault, releasePath };
}

function importOptions(item) {
  return { sourceDirectory: item.source, vaultRoot: item.vault, sourceRunId: "RUN-001", testCaseId: "HC-TEST-001", releaseRecordPath: item.releasePath, disposition: "INCONCLUSIVE", dataClassification: "SYNTHETIC_NON_SUBJECT", limitations: ["Synthetic test fixture."] };
}

test("imports and verifies a complete immutable evidence set", async (t) => {
  const item = await fixture(t);
  const imported = await importEvidence(importOptions(item));
  assert.equal(imported.imported, true);
  assert.equal((await verifyVault(item.vault)).verified_runs, 1);
  const receipt = JSON.parse(await readFile(path.join(imported.directory, "evidence-receipt.json"), "utf8"));
  assert.equal(receipt.source_run_id, "RUN-001");
  assert.equal(receipt.review_state, "DRAFT");
});

test("exact duplicate import is idempotent", async (t) => {
  const item = await fixture(t);
  await importEvidence(importOptions(item));
  assert.equal((await importEvidence(importOptions(item))).imported, false);
});

test("same source run id with different content is rejected", async (t) => {
  const item = await fixture(t);
  await importEvidence(importOptions(item));
  await writeFile(path.join(item.source, "frames.csv"), "frame,timestamp\n1,2\n");
  await assert.rejects(() => importEvidence(importOptions(item)), ControlError);
});

test("tampering is detected", async (t) => {
  const item = await fixture(t);
  const imported = await importEvidence(importOptions(item));
  const target = path.join(imported.directory, "artifacts", "frames.csv");
  await chmod(target, 0o666);
  await writeFile(target, "frame,timestamp\n1,999\n");
  await assert.rejects(() => verifyVault(item.vault), /Artifact-set hash differs|Artifact inventory differs/);
});

test("release-record tampering is detected", async (t) => {
  const item = await fixture(t);
  const imported = await importEvidence(importOptions(item));
  await writeFile(path.join(imported.directory, "release-record.json"), "{}\n");
  await assert.rejects(() => verifyVault(item.vault), /Release-record hash differs/);
});

test("malformed receipt is not silently skipped", async (t) => {
  const item = await fixture(t);
  const imported = await importEvidence(importOptions(item));
  await writeFile(path.join(imported.directory, "evidence-receipt.json"), "not-json\n");
  await assert.rejects(() => verifyVault(item.vault), /Cannot read controlled receipt/);
});

test("unexpected controlled-run files are rejected", async (t) => {
  const item = await fixture(t);
  const imported = await importEvidence(importOptions(item));
  await writeFile(path.join(imported.directory, "uncontrolled-note.txt"), "not part of the receipt\n");
  await assert.rejects(() => verifyVault(item.vault), /Unexpected controlled-run entries/);
});

test("retained release SBOM tampering is detected", async (t) => {
  const item = await fixture(t);
  const sbomText = `${JSON.stringify({ bomFormat: "CycloneDX", specVersion: "1.7", metadata: { timestamp: "2026-08-31T00:00:00.000Z" } }, null, 2)}\n`;
  const sbomPath = path.join(item.root, "release.cdx.json");
  await writeFile(sbomPath, sbomText);
  const release = JSON.parse(await readFile(item.releasePath, "utf8"));
  release.sbom = { format: "CycloneDX", spec_version: "1.7", path: sbomPath, byte_length: Buffer.byteLength(sbomText), sha256: createHash("sha256").update(sbomText).digest("hex"), generated_utc: "2026-08-31T00:00:00.000Z", project_validation_passed: true, official_validation_passed: true, official_validator: "test-validator" };
  await writeFile(item.releasePath, JSON.stringify(release));
  const imported = await importEvidence(importOptions(item));
  const retained = path.join(imported.directory, "sbom", "release-sbom.cdx.json");
  await chmod(retained, 0o666);
  await writeFile(retained, "{}\n");
  await assert.rejects(() => verifyVault(item.vault), /Retained SBOM differs/);
});

test("source inside vault is rejected", async (t) => {
  const item = await fixture(t);
  await initializeVault(item.vault);
  const source = path.join(item.vault, "uncontrolled");
  await mkdir(source);
  await cp(item.source, source, { recursive: true });
  await assert.rejects(() => importEvidence({ ...importOptions(item), sourceDirectory: source }), /inside the controlled vault/);
});

test("controlled release is rejected for a dirty source tree", { skip: process.platform !== "win32" }, async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-release-gate-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await execFileAsync("git.exe", ["-C", root, "init"]);
  await execFileAsync("git.exe", ["-C", root, "config", "user.name", "HumCapture Test"]);
  await execFileAsync("git.exe", ["-C", root, "config", "user.email", "test@invalid.local"]);
  await writeFile(path.join(root, "source.txt"), "baseline\n");
  await execFileAsync("git.exe", ["-C", root, "add", "source.txt"]);
  await execFileAsync("git.exe", ["-C", root, "commit", "-m", "test baseline"]);
  await writeFile(path.join(root, "source.txt"), "dirty\n");
  await assert.rejects(() => createReleaseRecord({ repoRoot: root, outputPath: path.join(root, "release.json"), softwareVersion: "test", requestedClassification: "CONTROLLED_RELEASE" }), /tracked and clean/);
});

test("engineering snapshot retains and hashes its CycloneDX SBOM", { skip: process.platform !== "win32" }, async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-sbom-release-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  const repo = path.join(root, "repo");
  await mkdir(repo);
  await execFileAsync("git.exe", ["-C", repo, "init"]);
  await execFileAsync("git.exe", ["-C", repo, "config", "user.name", "HumCapture Test"]);
  await execFileAsync("git.exe", ["-C", repo, "config", "user.email", "test@invalid.local"]);
  await writeFile(path.join(repo, "source.txt"), "baseline\n");
  await execFileAsync("git.exe", ["-C", repo, "add", "source.txt"]);
  await execFileAsync("git.exe", ["-C", repo, "commit", "-m", "test baseline"]);
  const sbomPath = path.join(root, "input.cdx.json");
  await writeFile(sbomPath, JSON.stringify({ bomFormat: "CycloneDX", specVersion: "1.7", metadata: { timestamp: "2026-08-31T00:00:00.000Z" } }));
  const outputPath = path.join(root, "releases", "release.json");
  const record = await createReleaseRecord({ repoRoot: repo, outputPath, softwareVersion: "test", sbomPath, sbomOfficiallyValidated: true, sbomOfficialValidator: "test-validator" });
  assert.equal(record.sbom.spec_version, "1.7");
  assert.equal(JSON.parse(await readFile(record.sbom.path, "utf8")).bomFormat, "CycloneDX");
});

test("controlled release rejects an SBOM without official validation", { skip: process.platform !== "win32" }, async (t) => {
  const root = await mkdtemp(path.join(os.tmpdir(), "hc-sbom-gate-test-"));
  t.after(() => rm(root, { recursive: true, force: true }));
  await execFileAsync("git.exe", ["-C", root, "init"]);
  await execFileAsync("git.exe", ["-C", root, "config", "user.name", "HumCapture Test"]);
  await execFileAsync("git.exe", ["-C", root, "config", "user.email", "test@invalid.local"]);
  await writeFile(path.join(root, "source.txt"), "baseline\n");
  await execFileAsync("git.exe", ["-C", root, "add", "source.txt"]);
  await execFileAsync("git.exe", ["-C", root, "commit", "-m", "test baseline"]);
  const sbomPath = path.join(path.dirname(root), `${path.basename(root)}-input.cdx.json`);
  await writeFile(sbomPath, JSON.stringify({ bomFormat: "CycloneDX", specVersion: "1.7", metadata: { timestamp: "2026-08-31T00:00:00.000Z" } }));
  t.after(() => rm(sbomPath, { force: true }));
  await assert.rejects(() => createReleaseRecord({ repoRoot: root, outputPath: path.join(path.dirname(root), `${path.basename(root)}-release.json`), softwareVersion: "test", requestedClassification: "CONTROLLED_RELEASE", sbomPath, sbomOfficiallyValidated: false }), /official_validation_passed/);
});

import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { chmod, copyFile, lstat, mkdir, readFile, readdir, realpath, rename, rm, stat, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";

const execFileAsync = promisify(execFile);
const excludedDirectoryNames = new Set([".git", "node_modules", "bin", "obj", "x64", "coverage"]);
const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
const schemaDirectory = path.resolve(moduleDirectory, "..", "schemas");

export class ControlError extends Error {}

async function validateDocument(document, schemaName) {
  const schema = JSON.parse(await readFile(path.join(schemaDirectory, schemaName), "utf8"));
  const ajv = new Ajv2020({ allErrors: true, strict: true });
  addFormats(ajv);
  const validate = ajv.compile(schema);
  if (!validate(document)) {
    throw new ControlError(`${schemaName} validation failed: ${(validate.errors ?? []).map((item) => `${item.instancePath || "/"} ${item.message}`).join("; ")}`);
  }
}

async function hashFile(filePath) {
  const before = await stat(filePath, { bigint: true });
  if (before.nlink > 1n) throw new ControlError(`Hard-linked file is not allowed: ${filePath}`);
  const digest = await new Promise((resolve, reject) => {
    const hash = createHash("sha256");
    const stream = createReadStream(filePath);
    stream.on("error", reject);
    stream.on("data", (chunk) => hash.update(chunk));
    stream.on("end", () => resolve(hash.digest("hex")));
  });
  const after = await stat(filePath, { bigint: true });
  if (before.size !== after.size || before.mtimeNs !== after.mtimeNs || before.ctimeNs !== after.ctimeNs) {
    throw new ControlError(`File changed while being hashed: ${filePath}`);
  }
  return { byte_length: Number(after.size), sha256: digest };
}

async function listPhysicalFiles(root, { exclusions = false } = {}, current = root) {
  const rootReal = await realpath(root);
  const currentReal = await realpath(current);
  if (currentReal !== rootReal && !currentReal.startsWith(`${rootReal}${path.sep}`)) throw new ControlError(`Path escapes root: ${current}`);
  const files = [];
  for (const entry of await readdir(current, { withFileTypes: true })) {
    if (exclusions && entry.isDirectory() && excludedDirectoryNames.has(entry.name)) continue;
    const absolute = path.join(current, entry.name);
    const item = await lstat(absolute);
    if (item.isSymbolicLink()) throw new ControlError(`Symbolic links are not allowed: ${absolute}`);
    if (item.isFile() && item.nlink > 1) throw new ControlError(`Hard-linked files are not allowed: ${absolute}`);
    if (entry.isDirectory()) files.push(...await listPhysicalFiles(root, { exclusions }, absolute));
    else if (entry.isFile()) files.push(path.relative(root, absolute).split(path.sep).join("/"));
  }
  return files.sort();
}

async function inventory(root, options = {}) {
  const artifacts = [];
  for (const relative_path of await listPhysicalFiles(root, options)) {
    artifacts.push({ relative_path, ...await hashFile(path.join(root, ...relative_path.split("/"))) });
  }
  if (artifacts.length === 0) throw new ControlError(`No files found: ${root}`);
  const canonical = artifacts.map((item) => `${item.relative_path}\t${item.byte_length}\t${item.sha256}`).join("\n") + "\n";
  return { artifacts, artifact_set_sha256: createHash("sha256").update(canonical).digest("hex") };
}

async function windowsAccount() {
  if (process.platform !== "win32") return os.userInfo().username;
  try {
    const { stdout } = await execFileAsync("whoami.exe", []);
    return stdout.trim();
  } catch {
    return `${os.hostname()}\\${os.userInfo().username}`;
  }
}

async function git(repoRoot, args) {
  const { stdout } = await execFileAsync("git.exe", ["-C", repoRoot, ...args]);
  return stdout.trim();
}

function utcStamp(date) {
  return date.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}

export async function createReleaseRecord({ repoRoot, outputPath, softwareVersion, requestedClassification = "ENGINEERING_SNAPSHOT", executedArtifacts = [], sbomPath = null, sbomOfficiallyValidated = false, sbomOfficialValidator = "not-run" }) {
  const absoluteRoot = path.resolve(repoRoot);
  const source = await inventory(absoluteRoot, { exclusions: true });
  const parentCommit = await git(absoluteRoot, ["rev-parse", "HEAD"]);
  const tracked = (await git(absoluteRoot, ["ls-files", "--", "."])).length > 0;
  const dirty = (await git(absoluteRoot, ["status", "--porcelain", "--", "."])).length > 0;
  if (requestedClassification === "CONTROLLED_RELEASE" && (!tracked || dirty)) {
    throw new ControlError("CONTROLLED_RELEASE requires HumCapture to be tracked and clean in Git.");
  }
  const classification = requestedClassification;
  const now = new Date();
  const prefix = classification === "CONTROLLED_RELEASE" ? "REL" : "ENG";
  const releaseId = `HC-${prefix}-${utcStamp(now)}-${source.artifact_set_sha256.slice(0, 12)}`;
  const binaries = [];
  for (const artifact of executedArtifacts) {
    const absolute = path.resolve(artifact.path);
    binaries.push({ role: artifact.role, path: absolute, ...await hashFile(absolute) });
  }
  let sbom;
  let sbomText;
  let retainedSbomPath;
  if (sbomPath) {
    const absoluteSbom = path.resolve(sbomPath);
    sbomText = await readFile(absoluteSbom, "utf8");
    const document = JSON.parse(sbomText);
    if (document.bomFormat !== "CycloneDX" || document.specVersion !== "1.7" || !document.metadata?.timestamp) {
      throw new ControlError("Release SBOM must be a project-validated CycloneDX 1.7 document with generation timestamp.");
    }
    retainedSbomPath = path.join(path.dirname(path.resolve(outputPath)), `${releaseId}.cdx.json`);
    sbom = {
      format: "CycloneDX",
      spec_version: "1.7",
      path: retainedSbomPath,
      ...await hashFile(absoluteSbom),
      generated_utc: document.metadata.timestamp,
      project_validation_passed: true,
      official_validation_passed: sbomOfficiallyValidated,
      official_validator: sbomOfficialValidator
    };
  }
  if (requestedClassification === "CONTROLLED_RELEASE" && !sbom) throw new ControlError("CONTROLLED_RELEASE requires an attached SBOM.");
  const limitations = classification === "ENGINEERING_SNAPSHOT"
    ? ["Engineering identity only; not a controlled regulatory release.", "The source inventory was created retrospectively and is not proof of a reproducible build input set.", ...(tracked ? [] : ["HumCapture is not tracked by the parent Git repository."]), ...(dirty ? ["HumCapture source tree is not clean."] : [])]
    : ["Technical release identity does not by itself constitute regulatory, clinical, or independent QA approval."];
  const record = {
    record_format_version: "1.1.0",
    release_id: releaseId,
    classification,
    software_version: softwareVersion,
    created_utc: now.toISOString(),
    created_by_windows_account: await windowsAccount(),
    repository: {
      humcapture_path: "src/HumCapture",
      parent_commit_sha: parentCommit,
      tracking_state: tracked ? "TRACKED" : "UNTRACKED",
      worktree_state: dirty ? "DIRTY" : "CLEAN"
    },
    source_inventory: {
      algorithm: "sorted-relative-path-size-sha256-v1",
      sha256: source.artifact_set_sha256,
      file_count: source.artifacts.length,
      exclusions: [...excludedDirectoryNames].sort()
    },
    executed_artifacts: binaries,
    ...(sbom ? { sbom } : {}),
    technical_release_preconditions_met: classification === "CONTROLLED_RELEASE",
    regulatory_use_permitted: false,
    limitations
  };
  await validateDocument(record, "release-record.schema.json");
  await mkdir(path.dirname(path.resolve(outputPath)), { recursive: true });
  if (sbomText) await writeFile(retainedSbomPath, sbomText, { encoding: "utf8", flag: "wx" });
  await writeFile(path.resolve(outputPath), `${JSON.stringify(record, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  return record;
}

export async function initializeVault(vaultRoot) {
  const root = path.resolve(vaultRoot);
  await mkdir(path.join(root, ".staging"), { recursive: true });
  await mkdir(path.join(root, "runs"), { recursive: true });
  await mkdir(path.join(root, "releases"), { recursive: true });
  const markerPath = path.join(root, "vault.json");
  try {
    await writeFile(markerPath, `${JSON.stringify({ vault_format_version: "1.0.0", vault_id: `HC-VAULT-${createHash("sha256").update(root).digest("hex").slice(0, 16)}`, created_utc: new Date().toISOString(), created_by_windows_account: await windowsAccount(), control_level: "LOCAL_HASH_VERIFIABLE_NOT_SIGNED_OR_WORM" }, null, 2)}\n`, { encoding: "utf8", flag: "wx" });
  } catch (error) {
    if (error.code !== "EEXIST") throw error;
  }
  const marker = JSON.parse(await readFile(markerPath, "utf8"));
  if (marker.vault_format_version !== "1.0.0" || marker.control_level !== "LOCAL_HASH_VERIFIABLE_NOT_SIGNED_OR_WORM") {
    throw new ControlError("Existing vault marker is invalid or unsupported.");
  }
  return root;
}

async function copyTree(sourceRoot, destinationRoot) {
  for (const relative of await listPhysicalFiles(sourceRoot)) {
    const source = path.join(sourceRoot, ...relative.split("/"));
    const destination = path.join(destinationRoot, "artifacts", ...relative.split("/"));
    await mkdir(path.dirname(destination), { recursive: true });
    await copyFile(source, destination);
  }
}

async function existingReceipts(vaultRoot) {
  const runs = path.join(vaultRoot, "runs");
  const receipts = [];
  for (const entry of await readdir(runs, { withFileTypes: true })) {
    const entryPath = path.join(runs, entry.name);
    const item = await lstat(entryPath);
    if (item.isSymbolicLink() || !entry.isDirectory()) throw new ControlError(`Unexpected non-directory entry in vault runs: ${entry.name}`);
    const receiptPath = path.join(runs, entry.name, "evidence-receipt.json");
    try {
      receipts.push({ path: receiptPath, record: JSON.parse(await readFile(receiptPath, "utf8")) });
    } catch (error) {
      throw new ControlError(`Cannot read controlled receipt ${receiptPath}: ${error.message}`);
    }
  }
  return receipts;
}

export async function importEvidence({ sourceDirectory, vaultRoot, sourceRunId, testCaseId, releaseRecordPath, disposition, dataClassification, limitations }) {
  const root = await initializeVault(vaultRoot);
  const source = path.resolve(sourceDirectory);
  const sourceReal = await realpath(source);
  const rootReal = await realpath(root);
  if (sourceReal === rootReal || sourceReal.startsWith(`${rootReal}${path.sep}`)) throw new ControlError("Source evidence cannot be inside the controlled vault.");
  const releaseText = await readFile(path.resolve(releaseRecordPath), "utf8");
  const release = JSON.parse(releaseText);
  await validateDocument(release, "release-record.schema.json");
  const normalizedReleaseText = `${JSON.stringify(release, null, 2)}\n`;
  const releaseRecordSha256 = createHash("sha256").update(normalizedReleaseText).digest("hex");
  const sourceInventory = await inventory(source);
  const evidenceId = `HC-EV-${sourceInventory.artifact_set_sha256.slice(0, 24)}`;
  const finalDirectory = path.join(root, "runs", evidenceId);
  for (const existing of await existingReceipts(root)) {
    if (existing.record.source_run_id === sourceRunId && existing.record.artifact_set_sha256 !== sourceInventory.artifact_set_sha256) {
      throw new ControlError(`Conflicting content already exists for source_run_id ${sourceRunId}.`);
    }
    if (existing.record.evidence_id === evidenceId) return { receipt: existing.record, directory: path.dirname(existing.path), imported: false };
  }
  const staging = path.join(root, ".staging", `${evidenceId}-${process.pid}-${Date.now()}`);
  await mkdir(staging, { recursive: false });
  try {
    await copyTree(source, staging);
    const copiedInventory = await inventory(path.join(staging, "artifacts"));
    if (copiedInventory.artifact_set_sha256 !== sourceInventory.artifact_set_sha256) throw new ControlError("Copied evidence differs from the source inventory.");
    const receipt = {
      record_format_version: "1.0.0",
      evidence_id: evidenceId,
      source_run_id: sourceRunId,
      test_case_id: testCaseId,
      release_id: release.release_id,
      release_record_sha256: releaseRecordSha256,
      imported_utc: new Date().toISOString(),
      imported_by_windows_account: await windowsAccount(),
      disposition,
      review_state: "DRAFT",
      data_classification: dataClassification,
      retention_class: "PROJECT_LIFETIME_PENDING_POLICY",
      source_provenance: source,
      artifacts: copiedInventory.artifacts,
      artifact_set_sha256: copiedInventory.artifact_set_sha256,
      limitations
    };
    await validateDocument(receipt, "evidence-receipt.schema.json");
    const receiptText = `${JSON.stringify(receipt, null, 2)}\n`;
    await writeFile(path.join(staging, "evidence-receipt.json"), receiptText, { encoding: "utf8", flag: "wx" });
    await writeFile(path.join(staging, "evidence-receipt.sha256"), `${createHash("sha256").update(receiptText).digest("hex")}  evidence-receipt.json\n`, { encoding: "utf8", flag: "wx" });
    await writeFile(path.join(staging, "release-record.json"), normalizedReleaseText, { encoding: "utf8", flag: "wx" });
    if (release.sbom) {
      const releaseSbomPath = path.resolve(release.sbom.path);
      const releaseSbomInfo = await hashFile(releaseSbomPath);
      if (releaseSbomInfo.sha256 !== release.sbom.sha256 || releaseSbomInfo.byte_length !== release.sbom.byte_length) throw new ControlError("Release SBOM differs from its release record.");
      await mkdir(path.join(staging, "sbom"));
      await copyFile(releaseSbomPath, path.join(staging, "sbom", "release-sbom.cdx.json"));
    }
    await rename(staging, finalDirectory);
    for (const artifact of copiedInventory.artifacts) await chmod(path.join(finalDirectory, "artifacts", ...artifact.relative_path.split("/")), 0o444);
    return { receipt, directory: finalDirectory, imported: true };
  } catch (error) {
    await rm(staging, { recursive: true, force: true });
    throw error;
  }
}

export async function verifyVault(vaultRoot) {
  const root = path.resolve(vaultRoot);
  const marker = JSON.parse(await readFile(path.join(root, "vault.json"), "utf8"));
  if (marker.vault_format_version !== "1.0.0") throw new ControlError("Unsupported or missing vault format.");
  const failures = [];
  let verified = 0;
  for (const item of await existingReceipts(root)) {
    try {
      await validateDocument(item.record, "evidence-receipt.schema.json");
      const directory = path.dirname(item.path);
      if (path.basename(directory) !== item.record.evidence_id) throw new ControlError("Run directory name differs from evidence ID.");
      const receiptText = await readFile(item.path, "utf8");
      const receiptIndex = (await readFile(path.join(directory, "evidence-receipt.sha256"), "utf8")).trim();
      const receiptHash = createHash("sha256").update(receiptText).digest("hex");
      if (receiptIndex !== `${receiptHash}  evidence-receipt.json`) throw new ControlError("Receipt hash index differs from receipt.");
      const releaseHash = (await hashFile(path.join(directory, "release-record.json"))).sha256;
      if (releaseHash !== item.record.release_record_sha256) throw new ControlError("Release-record hash differs from receipt.");
      const releaseRecord = JSON.parse(await readFile(path.join(directory, "release-record.json"), "utf8"));
      await validateDocument(releaseRecord, "release-record.schema.json");
      const allowedEntries = new Set(["artifacts", "evidence-receipt.json", "evidence-receipt.sha256", "release-record.json", ...(releaseRecord.sbom ? ["sbom"] : [])]);
      const unexpectedEntries = (await readdir(directory)).filter((name) => !allowedEntries.has(name));
      if (unexpectedEntries.length > 0) throw new ControlError(`Unexpected controlled-run entries: ${unexpectedEntries.join(", ")}`);
      if (releaseRecord.sbom) {
        const retainedSbom = await hashFile(path.join(directory, "sbom", "release-sbom.cdx.json"));
        if (retainedSbom.sha256 !== releaseRecord.sbom.sha256 || retainedSbom.byte_length !== releaseRecord.sbom.byte_length) throw new ControlError("Retained SBOM differs from release record.");
      }
      const actual = await inventory(path.join(path.dirname(item.path), "artifacts"));
      if (actual.artifact_set_sha256 !== item.record.artifact_set_sha256) throw new ControlError("Artifact-set hash differs from receipt.");
      if (JSON.stringify(actual.artifacts) !== JSON.stringify(item.record.artifacts)) throw new ControlError("Artifact inventory differs from receipt.");
      verified += 1;
    } catch (error) {
      failures.push(`${item.record.evidence_id ?? item.path}: ${error.message}`);
    }
  }
  if (failures.length > 0) throw new ControlError(`Vault verification failed:\n${failures.join("\n")}`);
  return { vault_id: marker.vault_id, verified_runs: verified };
}

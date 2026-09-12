import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { readFile, readdir, stat, lstat, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";

const moduleDirectory = path.dirname(fileURLToPath(import.meta.url));
const toolRoot = path.resolve(moduleDirectory, "..");
const schemaDirectory = path.join(toolRoot, "schemas");
const dictionaryDirectory = path.join(toolRoot, "csv-dictionaries");
const capabilityDictionaryPath = path.join(toolRoot, "capability-dictionary.json");
const procedureRegistryPath = path.join(toolRoot, "procedure-registry.json");
const excludedPackageFiles = new Set(["run-manifest.json", "hashes.sha256"]);
const measurementContracts = new Map([
  ["measurements/frames.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "frames" }],
  ["measurements/sensors.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "sensors" }],
  ["measurements/thermal-power-storage.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "thermal-power-storage" }],
  ["measurements/network.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "network" }],
  ["measurements/compatibility.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "compatibility" }],
  ["measurements/capture-events.csv", { media_type: "text/csv", role: "measurement", csv_dictionary: "capture-events" }]
]);

export class EvidenceError extends Error {
  constructor(message, details = []) {
    super(message);
    this.name = "EvidenceError";
    this.details = details;
  }
}

async function readJson(filePath) {
  try {
    return JSON.parse(await readFile(filePath, "utf8"));
  } catch (error) {
    throw new EvidenceError(`Cannot read JSON: ${filePath}`, [error.message]);
  }
}

async function createValidator() {
  const ajv = new Ajv2020({ allErrors: true, strict: true });
  addFormats(ajv);
  const names = ["common.schema.json", "run-manifest.schema.json", "capability-report.schema.json", "campaign-index.schema.json", "procedure-registry.schema.json"];
  for (const name of names) {
    ajv.addSchema(await readJson(path.join(schemaDirectory, name)));
  }
  return ajv;
}

function schemaErrors(validator) {
  return (validator.errors ?? []).map((error) => `${error.instancePath || "/"} ${error.message}`);
}

async function validateJsonDocument(document, schemaId, label) {
  const ajv = await createValidator();
  const validator = ajv.getSchema(schemaId);
  if (!validator(document)) {
    throw new EvidenceError(`${label} failed JSON Schema validation.`, schemaErrors(validator));
  }
}

async function loadProcedureRegistry() {
  const registry = await readJson(procedureRegistryPath);
  await validateJsonDocument(registry, "https://humcapture.local/schemas/phase-0/1.0.0/procedure-registry.schema.json", "procedure-registry.json");
  const definitions = new Map();
  for (const definition of registry.procedures) {
    if (definitions.has(definition.procedure_id)) throw new EvidenceError(`Duplicate procedure registry entry: ${definition.procedure_id}`);
    if (definition.kind === "capture" && definition.required_streams.length === 0) throw new EvidenceError(`Capture procedure has no required streams: ${definition.procedure_id}`);
    if (definition.kind !== "capture" && definition.required_streams.length !== 0) throw new EvidenceError(`Non-capture procedure declares streams: ${definition.procedure_id}`);
    if (definition.profile_policy === "exact" && definition.required_streams.some((stream) => [stream.width_px, stream.height_px, stream.fps, stream.codec].some((value) => value === null))) {
      throw new EvidenceError(`Exact procedure profile contains null values: ${definition.procedure_id}`);
    }
    definitions.set(definition.procedure_id, definition);
  }
  return { registry, definitions };
}

function normalizeRelativePath(relativePath) {
  if (typeof relativePath !== "string" || relativePath.length === 0) {
    throw new EvidenceError("Artifact path is empty.");
  }
  if (relativePath.includes("\\") || path.posix.isAbsolute(relativePath) || /^[A-Za-z]:/.test(relativePath)) {
    throw new EvidenceError(`Artifact path must be a portable relative path: ${relativePath}`);
  }
  const normalized = path.posix.normalize(relativePath);
  if (normalized !== relativePath || normalized === ".." || normalized.startsWith("../") || normalized.split("/").includes("..")) {
    throw new EvidenceError(`Artifact path escapes or is not normalized: ${relativePath}`);
  }
  return normalized;
}

function resolveInside(root, relativePath) {
  const normalized = normalizeRelativePath(relativePath);
  const resolvedRoot = path.resolve(root);
  const resolved = path.resolve(resolvedRoot, ...normalized.split("/"));
  if (resolved !== resolvedRoot && !resolved.startsWith(`${resolvedRoot}${path.sep}`)) {
    throw new EvidenceError(`Artifact path escapes package root: ${relativePath}`);
  }
  return resolved;
}

async function sha256File(filePath) {
  const before = await stat(filePath, { bigint: true });
  if (before.nlink > 1n) throw new EvidenceError(`Hard-linked files are not allowed in evidence packages: ${filePath}`);
  const digest = await new Promise((resolve, reject) => {
    const hash = createHash("sha256");
    const stream = createReadStream(filePath);
    stream.on("error", reject);
    stream.on("data", (chunk) => hash.update(chunk));
    stream.on("end", () => resolve(hash.digest("hex")));
  });
  const after = await stat(filePath, { bigint: true });
  if (after.nlink > 1n) throw new EvidenceError(`Hard-linked files are not allowed in evidence packages: ${filePath}`);
  if (before.dev !== after.dev || before.ino !== after.ino || before.size !== after.size || before.mtimeNs !== after.mtimeNs || before.ctimeNs !== after.ctimeNs) {
    throw new EvidenceError(`File changed while it was being hashed: ${filePath}`);
  }
  return digest;
}

function parseCsv(text) {
  const rows = [];
  let row = [];
  let field = "";
  let quoted = false;
  let afterQuote = false;
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index];
    if (quoted) {
      if (char === '"' && text[index + 1] === '"') {
        field += '"';
        index += 1;
      } else if (char === '"') {
        quoted = false;
        afterQuote = true;
      } else {
        field += char;
      }
    } else if (afterQuote) {
      if (char === ",") {
        row.push(field);
        field = "";
        afterQuote = false;
      } else if (char === "\n") {
        row.push(field.endsWith("\r") ? field.slice(0, -1) : field);
        rows.push(row);
        row = [];
        field = "";
        afterQuote = false;
      } else if (char === "\r" && text[index + 1] === "\n") {
        // The following LF terminates the row.
      } else {
        throw new EvidenceError("Malformed CSV character after closing quote.");
      }
    } else if (char === '"') {
      if (field.length !== 0) throw new EvidenceError("Malformed CSV quote.");
      quoted = true;
    } else if (char === ",") {
      row.push(field);
      field = "";
    } else if (char === "\n") {
      row.push(field.endsWith("\r") ? field.slice(0, -1) : field);
      rows.push(row);
      row = [];
      field = "";
    } else {
      field += char;
    }
  }
  if (quoted) throw new EvidenceError("CSV ends inside a quoted field.");
  if (field.length > 0 || row.length > 0) {
    row.push(field.endsWith("\r") ? field.slice(0, -1) : field);
    rows.push(row);
  }
  return rows.filter((candidate) => candidate.some((value) => value.length > 0));
}

function isIntegerInRange(value, minimum, maximum) {
  if (!/^-?[0-9]+$/.test(value)) return false;
  const parsed = BigInt(value);
  return parsed >= minimum && parsed <= maximum;
}

function validatePrimitive(value, type) {
  if (type === "string") return true;
  if (type === "uuid") return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  if (type === "boolean") return value === "true" || value === "false";
  if (type === "float64") return /^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$/.test(value) && Number.isFinite(Number(value));
  if (type === "int32") return isIntegerInRange(value, -2147483648n, 2147483647n);
  if (type === "uint32") return isIntegerInRange(value, 0n, 4294967295n);
  if (type === "int64") return isIntegerInRange(value, -9223372036854775808n, 9223372036854775807n);
  if (type === "uint64") return isIntegerInRange(value, 0n, 18446744073709551615n);
  if (type.startsWith("enum:")) return type.slice(5).split("|").includes(value);
  return false;
}

function numericValue(value, type) {
  if (["int32", "uint32", "int64", "uint64"].includes(type)) return BigInt(value);
  if (type === "float64") return Number(value);
  return null;
}

async function validateCsv(filePath, dictionaryName, expectedRunId) {
  const dictionary = await readJson(path.join(dictionaryDirectory, `${dictionaryName}.json`));
  if (dictionary.evidence_format_version !== "1.0.0") {
    throw new EvidenceError(`Unsupported CSV dictionary version: ${dictionary.evidence_format_version}`);
  }
  const rows = parseCsv(await readFile(filePath, "utf8"));
  if (rows.length === 0) throw new EvidenceError(`CSV is empty: ${filePath}`);
  const expectedHeader = dictionary.columns.map((column) => column.name);
  if (rows[0].length !== expectedHeader.length || rows[0].some((value, index) => value !== expectedHeader[index])) {
    throw new EvidenceError(`CSV header does not match ${dictionaryName} dictionary.`, [`Expected: ${expectedHeader.join(",")}`, `Actual: ${rows[0].join(",")}`]);
  }
  const errors = [];
  const rowObjects = [];
  rows.slice(1).forEach((values, rowIndex) => {
    if (values.length !== dictionary.columns.length) {
      errors.push(`row ${rowIndex + 2}: expected ${dictionary.columns.length} columns, found ${values.length}`);
      return;
    }
    dictionary.columns.forEach((column, columnIndex) => {
      const value = values[columnIndex];
      if (value === "") {
        if (column.required) errors.push(`row ${rowIndex + 2}, ${column.name}: required value is empty`);
      } else if (!validatePrimitive(value, column.type)) {
        errors.push(`row ${rowIndex + 2}, ${column.name}: invalid ${column.type} value '${value}'`);
      } else {
        const numeric = numericValue(value, column.type);
        if (numeric !== null && column.minimum !== undefined && numeric < (typeof numeric === "bigint" ? BigInt(column.minimum) : column.minimum)) {
          errors.push(`row ${rowIndex + 2}, ${column.name}: value '${value}' is below minimum ${column.minimum}`);
        }
        if (numeric !== null && column.maximum !== undefined && numeric > (typeof numeric === "bigint" ? BigInt(column.maximum) : column.maximum)) {
          errors.push(`row ${rowIndex + 2}, ${column.name}: value '${value}' is above maximum ${column.maximum}`);
        }
      }
    });
    const rowObject = Object.fromEntries(dictionary.columns.map((column, columnIndex) => [column.name, values[columnIndex]]));
    if (rowObject.run_id !== expectedRunId) errors.push(`row ${rowIndex + 2}, run_id: does not match manifest run_id`);
    for (const group of dictionary.presence_groups ?? []) {
      const presentCount = group.filter((name) => rowObject[name] !== "").length;
      if (presentCount !== 0 && presentCount !== group.length) errors.push(`row ${rowIndex + 2}: fields must be present together: ${group.join(", ")}`);
    }
    rowObjects.push(rowObject);
  });
  if (errors.length > 0) throw new EvidenceError(`CSV validation failed: ${filePath}`, errors);
  return rowObjects;
}

async function listPackageFiles(root, current = root) {
  const files = [];
  const canonicalRoot = await realpath(root);
  const canonicalCurrent = await realpath(current);
  if (canonicalCurrent !== canonicalRoot && !canonicalCurrent.startsWith(`${canonicalRoot}${path.sep}`)) {
    throw new EvidenceError(`Evidence directory resolves outside package root: ${current}`);
  }
  const entries = await readdir(current, { withFileTypes: true });
  for (const entry of entries) {
    const absolute = path.join(current, entry.name);
    const relative = path.relative(root, absolute).split(path.sep).join("/");
    const entryStat = await lstat(absolute);
    if (entryStat.isSymbolicLink()) throw new EvidenceError(`Symbolic links are not allowed in evidence packages: ${relative}`);
    if (entry.isFile() && entryStat.nlink > 1) throw new EvidenceError(`Hard-linked files are not allowed in evidence packages: ${relative}`);
    const canonicalEntry = await realpath(absolute);
    if (canonicalEntry !== canonicalRoot && !canonicalEntry.startsWith(`${canonicalRoot}${path.sep}`)) {
      throw new EvidenceError(`Evidence path resolves outside package root: ${relative}`);
    }
    if (entry.isDirectory()) files.push(...await listPackageFiles(root, absolute));
    else if (entry.isFile() && !excludedPackageFiles.has(relative)) files.push(relative);
  }
  return files.sort();
}

async function resolveExistingDirectoryInside(root, relativePath) {
  const lexical = resolveInside(root, relativePath);
  const rootReal = await realpath(root);
  const targetReal = await realpath(lexical);
  if (targetReal !== rootReal && !targetReal.startsWith(`${rootReal}${path.sep}`)) {
    throw new EvidenceError(`Evidence reference resolves outside campaign root: ${relativePath}`);
  }
  const targetStat = await lstat(lexical);
  if (targetStat.isSymbolicLink() || !targetStat.isDirectory()) {
    throw new EvidenceError(`Evidence reference is not a physical directory: ${relativePath}`);
  }
  return lexical;
}

function mediaTypeFor(relativePath) {
  const extension = path.posix.extname(relativePath).toLowerCase();
  return ({ ".json": "application/json", ".csv": "text/csv", ".md": "text/markdown", ".txt": "text/plain", ".log": "text/plain", ".mp4": "video/mp4", ".bin": "application/octet-stream" })[extension] ?? "application/octet-stream";
}

function artifactContractFor(relativePath) {
  if (relativePath === "capability-report.json") return { media_type: "application/json", role: "capability" };
  if (relativePath === "summary.md") return { media_type: "text/markdown", role: "summary" };
  if (measurementContracts.has(relativePath)) return measurementContracts.get(relativePath);
  if (relativePath.startsWith("measurements/")) throw new EvidenceError(`Unknown measurement artifact for evidence format 1.0.0: ${relativePath}`);
  if (relativePath.startsWith("logs/")) return { media_type: mediaTypeFor(relativePath), role: "log" };
  if (relativePath.startsWith("media/")) return { media_type: mediaTypeFor(relativePath), role: "media" };
  throw new EvidenceError(`Artifact path is not allowed by evidence format 1.0.0: ${relativePath}`);
}

function requiredArtifactPaths(procedureDefinition) {
  const required = new Set(["capability-report.json", "summary.md"]);
  for (const dictionaryName of procedureDefinition.required_measurements) required.add(`measurements/${dictionaryName}.csv`);
  return required;
}

export async function generateHashes(packageDirectory) {
  const root = path.resolve(packageDirectory);
  const manifestPath = path.join(root, "run-manifest.json");
  const manifest = await readJson(manifestPath);
  const files = await listPackageFiles(root);
  const artifacts = [];
  for (const relativePath of files) {
    const absolutePath = resolveInside(root, relativePath);
    const fileStat = await stat(absolutePath);
    const contract = artifactContractFor(relativePath);
    const artifact = {
      relative_path: relativePath,
      media_type: contract.media_type,
      role: contract.role,
      ...(contract.csv_dictionary ? { csv_dictionary: contract.csv_dictionary } : {}),
      byte_length: fileStat.size,
      sha256: await sha256File(absolutePath)
    };
    artifacts.push(artifact);
  }
  manifest.artifacts = artifacts;
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
  const hashLines = artifacts.map((artifact) => `${artifact.sha256}  ${artifact.relative_path}`);
  await writeFile(path.join(root, "hashes.sha256"), `${hashLines.join("\n")}\n`, "utf8");
  return artifacts;
}

function captureProfilesEqual(left, right) {
  if (left === null || right === null) return left === right;
  return ["width_px", "height_px", "fps", "codec", "pixel_format", "bitrate_bps"].every((key) => left[key] === right[key]);
}

function exactCaptureShape(left, right) {
  if (left === null || right === null) return false;
  return ["width_px", "height_px", "fps", "codec"].every((key) => left[key] === right[key])
    && (left.pixel_format === null || left.pixel_format === right.pixel_format);
}

function procedureFamily(procedureId) {
  const match = /^HC-P0-([A-Z]+)-[0-9]{3}$/.exec(procedureId);
  return match?.[1] ?? null;
}

function isCaptureFamily(family) {
  return family === "AND" || family === "UVC" || family === "SYN";
}

function requestedProfileMatchesRequirement(profile, requirement) {
  if (requirement.width_px === null) return true;
  return profile.width_px === requirement.width_px
    && profile.height_px === requirement.height_px
    && profile.fps === requirement.fps
    && profile.codec === requirement.codec;
}

function validateProcedureContract(manifest, capability, registry, definition) {
  if (manifest.procedure.registry_version !== registry.registry_version) throw new EvidenceError("Manifest procedure registry version is unsupported.");
  if (!definition.allowed_probe_platforms.includes(manifest.probe.platform)) {
    throw new EvidenceError(`Procedure probe platform is not allowed for ${definition.procedure_id}: ${manifest.probe.platform}`);
  }
  if (!definition.allowed_device_platforms.includes(capability.device.platform)) {
    throw new EvidenceError(`Procedure device platform is not allowed for ${definition.procedure_id}: ${capability.device.platform}`);
  }
  if (!definition.allowed_camera_stacks.includes(capability.device.camera_stack)) {
    throw new EvidenceError(`Procedure camera stack is not allowed for ${definition.procedure_id}: ${capability.device.camera_stack}`);
  }
  const qualifying = manifest.disposition === "PASS" || manifest.disposition === "CONDITIONAL";
  if (qualifying && BigInt(manifest.procedure.required_duration_ns) < BigInt(definition.minimum_qualification_duration_ns)) {
    throw new EvidenceError(`Procedure required duration is below the registry minimum for ${definition.procedure_id}.`);
  }
  const streams = manifest.configuration.streams;
  const streamCountInvalid = definition.profile_policy === "synthetic_any"
    ? streams.length < definition.required_streams.length
    : streams.length !== definition.required_streams.length;
  if (streamCountInvalid) {
    throw new EvidenceError(`Procedure profile has the wrong required stream count for ${definition.procedure_id}.`);
  }
  if (new Set(streams.map((stream) => stream.purpose)).size !== streams.length) throw new EvidenceError(`Procedure profile contains duplicate stream purposes for ${definition.procedure_id}.`);
  const seenPurposes = new Set();
  for (const requirement of definition.required_streams) {
    const matches = streams.filter((stream) => stream.purpose === requirement.purpose);
    if (matches.length !== 1 || seenPurposes.has(requirement.purpose)) {
      throw new EvidenceError(`Procedure profile is missing or duplicates required stream purpose '${requirement.purpose}' for ${definition.procedure_id}.`);
    }
    seenPurposes.add(requirement.purpose);
    if (definition.profile_policy === "exact" && !requestedProfileMatchesRequirement(matches[0].requested, requirement)) {
      throw new EvidenceError(`Requested stream does not match normative procedure profile for ${definition.procedure_id}/${requirement.purpose}.`);
    }
  }
  if (definition.profile_policy === "none" && streams.length !== 0) throw new EvidenceError(`${definition.procedure_id} must not declare capture streams.`);
}

function validateDispositionSemantics(manifest, capability, matchingProfiles) {
  const qualifying = manifest.disposition === "PASS" || manifest.disposition === "CONDITIONAL";
  if (!qualifying) return;
  const family = procedureFamily(manifest.procedure.procedure_id);
  if (manifest.procedure.mode !== "qualification") throw new EvidenceError(`${manifest.disposition} requires qualification mode; diagnostic runs cannot qualify hardware.`);
  const actualDuration = BigInt(manifest.procedure.actual_duration_ns);
  const requiredDuration = BigInt(manifest.procedure.required_duration_ns);
  if (actualDuration < requiredDuration) throw new EvidenceError(`${manifest.disposition} requires actual duration to meet required duration.`);
  if (!manifest.finalization.completed || !manifest.finalization.artifact_readable) {
    throw new EvidenceError(`${manifest.disposition} requires completed, readable finalization evidence.`);
  }
  const streams = manifest.configuration.streams;
  if (isCaptureFamily(family)) {
    if (streams.length === 0) throw new EvidenceError(`${manifest.disposition} capture evidence requires at least one stream profile.`);
    const expectedStatus = manifest.disposition === "PASS" ? "qualified" : "conditional";
    for (const stream of streams) {
      const { requested, reported, negotiated, measured } = stream;
      if ([reported, negotiated, measured].some((profile) => profile === null)) {
        throw new EvidenceError(`${manifest.disposition} requires reported, negotiated, and measured profiles for stream ${stream.stream_id}.`);
      }
      if (!exactCaptureShape(requested, reported)) throw new EvidenceError(`${manifest.disposition} reported profile does not match the requested candidate for stream ${stream.stream_id}.`);
      if (!exactCaptureShape(requested, negotiated)) throw new EvidenceError(`${manifest.disposition} cannot contain requested-to-negotiated capture substitution for stream ${stream.stream_id}.`);
      if (!exactCaptureShape(negotiated, measured)) throw new EvidenceError(`${manifest.disposition} cannot contain negotiated-to-measured capture substitution for stream ${stream.stream_id}.`);
      const matchingProfile = matchingProfiles.get(stream.stream_id);
      if (!matchingProfile) throw new EvidenceError(`${manifest.disposition} requires a matching capability-report profile for stream ${stream.stream_id}.`);
      if (matchingProfile.status !== expectedStatus) throw new EvidenceError(`${manifest.disposition} requires capability profile status '${expectedStatus}' for stream ${stream.stream_id}.`);
      if (matchingProfile.reported_supported !== true) throw new EvidenceError(`${manifest.disposition} requires reported_supported=true for stream ${stream.stream_id}.`);
    }
    if (!manifest.artifacts.some((artifact) => artifact.role === "media")) throw new EvidenceError(`${manifest.disposition} capture evidence requires a media artifact.`);
  } else {
    if (streams.length !== 0) throw new EvidenceError(`${family} evidence must not declare capture stream profiles.`);
    if (capability.capture_profiles.length !== 0) throw new EvidenceError(`${family} evidence must not contain capture profiles.`);
  }
  if (capability.capabilities.some((item) => item.status === "qualified") && manifest.disposition !== "PASS") {
    throw new EvidenceError("Qualified capability status requires PASS disposition.");
  }
}

function provenanceKey(channelType, channelId, timestampField) {
  return `${channelType}\u0000${channelId}\u0000${timestampField}`;
}

function validateTimestampProvenance(manifest, measurementRows) {
  const provenance = new Map();
  for (const entry of manifest.timestamp_provenance) {
    const key = provenanceKey(entry.channel_type, entry.channel_id, entry.timestamp_field);
    if (provenance.has(key)) throw new EvidenceError(`Duplicate timestamp provenance binding: ${entry.channel_type}/${entry.channel_id}/${entry.timestamp_field}`);
    provenance.set(key, entry);
  }
  const used = new Set();
  const requireBinding = (channelType, channelId, timestampField, domain, unit) => {
    const key = provenanceKey(channelType, channelId, timestampField);
    const entry = provenance.get(key);
    if (!entry) throw new EvidenceError(`Missing timestamp provenance binding: ${channelType}/${channelId}/${timestampField}`);
    if (entry.domain !== domain || entry.unit !== unit) {
      throw new EvidenceError(`Timestamp provenance differs from measurement: ${channelType}/${channelId}/${timestampField}`);
    }
    used.add(key);
  };
  for (const row of measurementRows.get("frames") ?? []) {
    requireBinding("stream", row.stream_id, "source_timestamp", row.source_timestamp_domain, row.source_timestamp_unit);
    if (row.presentation_timestamp !== "") requireBinding("stream", row.stream_id, "presentation_timestamp", "media_presentation", row.presentation_timestamp_unit);
    if (row.host_arrival_timestamp !== "") requireBinding("stream", row.stream_id, "host_arrival_timestamp", row.host_arrival_timestamp_domain, row.host_arrival_timestamp_unit);
  }
  for (const row of measurementRows.get("sensors") ?? []) {
    requireBinding("sensor", row.sensor_id, "timestamp", row.timestamp_domain, row.timestamp_unit);
  }
  for (const row of measurementRows.get("thermal-power-storage") ?? []) {
    requireBinding("system", "thermal-power-storage", "timestamp", row.timestamp_domain, row.timestamp_unit);
  }
  for (const row of measurementRows.get("network") ?? []) {
    requireBinding("network", "network", "timestamp", row.timestamp_domain, row.timestamp_unit);
  }
  for (const row of measurementRows.get("capture-events") ?? []) {
    requireBinding("system", "capture-events", "timestamp", row.timestamp_domain, row.timestamp_unit);
  }
  const unused = [...provenance.keys()].filter((key) => !used.has(key));
  if (unused.length > 0) throw new EvidenceError("Manifest contains timestamp provenance that is not bound to measurement rows.", unused);
}

const timestampUnitNs = new Map([
  ["ns", 1n],
  ["us", 1_000n],
  ["ms", 1_000_000n],
  ["s", 1_000_000_000n],
  ["ticks_100ns", 100n]
]);

function timestampAsNs(value, unit) {
  return BigInt(value) * timestampUnitNs.get(unit);
}

function validateOrderedChannelRows(rows, { channel, timestamp, domain, unit, discontinuity = null }) {
  const states = new Map();
  for (const row of rows) {
    const channelId = channel(row);
    const sequence = BigInt(row.sequence);
    const currentTimestamp = timestampAsNs(row[timestamp], row[unit]);
    const currentDomain = row[domain];
    const currentDiscontinuity = discontinuity === null ? "none" : row[discontinuity];
    const previous = states.get(channelId);
    if (previous) {
      if (sequence <= previous.sequence) throw new EvidenceError(`Measurement sequence is duplicate or out of order for ${channelId}.`);
      const domainChanged = currentDomain !== previous.domain || row[unit] !== previous.unit;
      if (domainChanged && currentDiscontinuity !== "source_change") {
        throw new EvidenceError(`Timestamp domain changed without source_change for ${channelId}.`);
      }
      if (!domainChanged && currentTimestamp < previous.timestamp && !["regression", "source_change"].includes(currentDiscontinuity)) {
        throw new EvidenceError(`Unmarked timestamp regression for ${channelId}.`);
      }
      if (!domainChanged && currentTimestamp === previous.timestamp && !["duplicate", "source_change"].includes(currentDiscontinuity)) {
        throw new EvidenceError(`Unmarked duplicate timestamp for ${channelId}.`);
      }
    }
    states.set(channelId, { sequence, timestamp: currentTimestamp, domain: currentDomain, unit: row[unit] });
  }
}

function validateMeasurementOrdering(measurementRows) {
  validateOrderedChannelRows(measurementRows.get("frames") ?? [], {
    channel: (row) => `stream/${row.stream_id}`,
    timestamp: "source_timestamp",
    domain: "source_timestamp_domain",
    unit: "source_timestamp_unit",
    discontinuity: "discontinuity"
  });
  validateOrderedChannelRows(measurementRows.get("sensors") ?? [], {
    channel: (row) => `sensor/${row.sensor_id}`,
    timestamp: "timestamp",
    domain: "timestamp_domain",
    unit: "timestamp_unit",
    discontinuity: "discontinuity"
  });
  validateOrderedChannelRows(measurementRows.get("thermal-power-storage") ?? [], {
    channel: () => "system/thermal-power-storage",
    timestamp: "timestamp",
    domain: "timestamp_domain",
    unit: "timestamp_unit"
  });
  validateOrderedChannelRows(measurementRows.get("network") ?? [], {
    channel: () => "network/network",
    timestamp: "timestamp",
    domain: "timestamp_domain",
    unit: "timestamp_unit"
  });
  validateOrderedChannelRows(measurementRows.get("capture-events") ?? [], {
    channel: () => "system/capture-events",
    timestamp: "timestamp",
    domain: "timestamp_domain",
    unit: "timestamp_unit"
  });
}

function frameCoverageNs(rows, framePeriodNs) {
  let coverage = 0n;
  let segmentStart = null;
  let previous = null;
  let previousDomain = null;
  let previousUnit = null;
  let segmentCount = 0n;
  for (const row of rows) {
    const current = timestampAsNs(row.source_timestamp, row.source_timestamp_unit);
    const startsSegment = segmentStart === null
      || row.source_timestamp_domain !== previousDomain
      || row.source_timestamp_unit !== previousUnit
      || current <= previous;
    if (startsSegment) {
      if (segmentStart !== null) coverage += previous - segmentStart;
      segmentStart = current;
      segmentCount += 1n;
    }
    previous = current;
    previousDomain = row.source_timestamp_domain;
    previousUnit = row.source_timestamp_unit;
  }
  if (segmentStart !== null) coverage += previous - segmentStart;
  return coverage + framePeriodNs * segmentCount;
}

function validateCaptureQualificationMeasurements(manifest, measurementRows) {
  const frames = measurementRows.get("frames") ?? [];
  if (frames.length === 0) throw new EvidenceError("Capture qualification requires non-empty frames measurement rows.");
  const streamContracts = new Map(manifest.configuration.streams.map((stream) => [stream.stream_id, stream]));
  const rowsByStream = new Map([...streamContracts.keys()].map((streamId) => [streamId, []]));
  for (const row of frames) {
    if (!streamContracts.has(row.stream_id)) throw new EvidenceError(`Frame evidence contains undeclared stream: ${row.stream_id}`);
    rowsByStream.get(row.stream_id).push(row);
  }
  const durationNs = BigInt(manifest.procedure.actual_duration_ns);
  for (const [streamId, rows] of rowsByStream) {
    if (rows.length === 0) throw new EvidenceError(`Capture qualification requires frame evidence for required stream: ${streamId}`);
    const measured = streamContracts.get(streamId).measured;
    const fps = measured.fps;
    const framePeriodNs = BigInt(Math.ceil(1_000_000_000 / fps));
    const minimumAccountedFrames = Math.max(1, Math.ceil(Number(durationNs) / 1_000_000_000 * fps) - 1);
    let accountedFrames = rows.length;
    let previousSequence = null;
    let previousTimestamp = null;
    let previousDomain = null;
    let previousUnit = null;
    for (const row of rows) {
      if (Number(row.width_px) !== measured.width_px || Number(row.height_px) !== measured.height_px) {
        throw new EvidenceError(`Frame dimensions differ from measured profile for ${streamId}.`, [`measured=${measured.width_px}x${measured.height_px}`, `row=${row.width_px}x${row.height_px}`]);
      }
      const sequence = BigInt(row.sequence);
      const droppedBefore = BigInt(row.dropped_before);
      if (previousSequence === null && droppedBefore !== 0n) throw new EvidenceError(`First frame cannot report dropped_before for ${streamId}.`);
      if (previousSequence !== null && sequence - previousSequence !== droppedBefore + 1n) {
        throw new EvidenceError(`Frame sequence gap is not exactly accounted by dropped_before for ${streamId}.`);
      }
      accountedFrames += Number(droppedBefore);
      if (manifest.disposition === "PASS" && (droppedBefore !== 0n || row.discontinuity !== "none")) {
        throw new EvidenceError(`PASS capture evidence cannot contain frame loss or discontinuity for ${streamId}.`);
      }
      const currentTimestamp = timestampAsNs(row.source_timestamp, row.source_timestamp_unit);
      if (previousTimestamp !== null && previousDomain === row.source_timestamp_domain && previousUnit === row.source_timestamp_unit
        && currentTimestamp - previousTimestamp > framePeriodNs * 2n && row.discontinuity === "none") {
        throw new EvidenceError(`Frame timestamp gap is not explicitly recorded for ${streamId}.`);
      }
      previousSequence = sequence;
      previousTimestamp = currentTimestamp;
      previousDomain = row.source_timestamp_domain;
      previousUnit = row.source_timestamp_unit;
    }
    if (accountedFrames < minimumAccountedFrames) {
      throw new EvidenceError(`Capture qualification frame accounting does not cover claimed duration for ${streamId}.`, [`accounted=${accountedFrames}`, `minimum=${minimumAccountedFrames}`]);
    }
    if (frameCoverageNs(rows, framePeriodNs) < durationNs) {
      throw new EvidenceError(`Capture qualification timestamp duration coverage is insufficient for ${streamId}.`);
    }
  }
}

function validateProcedureMeasurements(manifest, measurementRows, procedureDefinition) {
  const qualifying = manifest.disposition === "PASS" || manifest.disposition === "CONDITIONAL";
  if (!qualifying) return;
  const family = procedureFamily(manifest.procedure.procedure_id);
  const requiredMeasurements = [...requiredArtifactPaths(procedureDefinition)]
    .filter((relativePath) => relativePath.startsWith("measurements/"))
    .map((relativePath) => path.posix.basename(relativePath, ".csv"));
  for (const dictionaryName of requiredMeasurements) {
    if ((measurementRows.get(dictionaryName) ?? []).length === 0) {
      throw new EvidenceError(`${manifest.disposition} requires non-empty ${dictionaryName} measurement rows.`);
    }
  }
  if (isCaptureFamily(family)) {
    validateCaptureQualificationMeasurements(manifest, measurementRows);
    const systemRows = measurementRows.get("thermal-power-storage") ?? [];
    if (systemRows.length > 0) {
      const first = timestampAsNs(systemRows[0].timestamp, systemRows[0].timestamp_unit);
      const last = timestampAsNs(systemRows.at(-1).timestamp, systemRows.at(-1).timestamp_unit);
      if (last - first < BigInt(manifest.procedure.actual_duration_ns)) throw new EvidenceError("Thermal/power/storage measurement duration coverage is insufficient.");
    }
    if (procedureDefinition.required_sensor_types.length > 0) {
      const sensorRows = measurementRows.get("sensors") ?? [];
      for (const sensorType of procedureDefinition.required_sensor_types) {
        if (!sensorRows.some((row) => row.sensor_type === sensorType)) throw new EvidenceError(`${manifest.procedure.procedure_id} requires sensor type: ${sensorType}`);
      }
      const bySensor = new Map();
      for (const row of sensorRows) {
        if (!bySensor.has(row.sensor_id)) bySensor.set(row.sensor_id, []);
        bySensor.get(row.sensor_id).push(row);
      }
      if (bySensor.size === 0) throw new EvidenceError(`${manifest.procedure.procedure_id} requires sensor measurement rows.`);
      for (const [sensorId, rows] of bySensor) {
        const first = timestampAsNs(rows[0].timestamp, rows[0].timestamp_unit);
        const last = timestampAsNs(rows.at(-1).timestamp, rows.at(-1).timestamp_unit);
        if (last - first < BigInt(manifest.procedure.actual_duration_ns)) throw new EvidenceError(`Sensor measurement duration coverage is insufficient for ${sensorId}.`);
      }
    }
    if (manifest.procedure.procedure_id === "HC-P0-UVC-002") {
      const eventRows = measurementRows.get("capture-events") ?? [];
      const requiredEvents = ["capture_started", "device_disconnected", "pre_disconnect_finalized", "device_reconnected", "post_reconnect_capture_started", "post_reconnect_finalized"];
      if (eventRows.some((row) => row.status !== "ok")) throw new EvidenceError("HC-P0-UVC-002 qualifying evidence cannot contain failed or unknown capture events.");
      if (eventRows.some((row) => !manifest.configuration.streams.some((stream) => stream.stream_id === row.stream_id))) {
        throw new EvidenceError("HC-P0-UVC-002 capture event references an undeclared stream.");
      }
      for (const eventType of requiredEvents) {
        if (!eventRows.some((row) => row.event_type === eventType && row.status === "ok")) throw new EvidenceError(`HC-P0-UVC-002 is missing required capture event: ${eventType}`);
      }
      if (eventRows.length !== requiredEvents.length || eventRows.some((row, index) => row.event_type !== requiredEvents[index])) {
        throw new EvidenceError("HC-P0-UVC-002 capture events do not match the exact lifecycle state sequence.");
      }
      const lifecycleDuration = timestampAsNs(eventRows.at(-1).timestamp, eventRows.at(-1).timestamp_unit)
        - timestampAsNs(eventRows[0].timestamp, eventRows[0].timestamp_unit);
      if (lifecycleDuration !== BigInt(manifest.procedure.actual_duration_ns)) throw new EvidenceError("HC-P0-UVC-002 lifecycle event span does not equal the claimed procedure duration.");
      const mediaPaths = new Set(manifest.artifacts.filter((artifact) => artifact.role === "media").map((artifact) => artifact.relative_path));
      const finalizedPaths = eventRows
        .filter((row) => ["pre_disconnect_finalized", "post_reconnect_finalized"].includes(row.event_type))
        .map((row) => row.artifact_relative_path);
      if (new Set(finalizedPaths).size !== 2 || finalizedPaths.some((relativePath) => !mediaPaths.has(relativePath))) {
        throw new EvidenceError("HC-P0-UVC-002 requires two distinct finalized media artifacts referenced by capture events.");
      }
    }
    return;
  }
  if (family === "NET") {
    const rows = measurementRows.get("network") ?? [];
    if (rows.length === 0) throw new EvidenceError("Network qualification requires non-empty network measurement rows.");
    const allowed = manifest.disposition === "PASS" ? new Set(["ok"]) : new Set(["ok", "degraded"]);
    if (rows.some((row) => !allowed.has(row.status))) throw new EvidenceError(`${manifest.disposition} network evidence contains a disallowed observation status.`);
    const completed = (testType) => rows.filter((row) => row.test_type === testType && row.completion_verified === "true");
    if (completed("discovery").length < 1) throw new EvidenceError("Network qualification is missing required network observation: completed discovery.");
    const completedPreviews = completed("preview_load").filter((row) => row.throughput_bps !== "" && Number(row.throughput_bps) > 0);
    if (new Set(completedPreviews.map((row) => row.endpoint_id)).size < 2) {
      throw new EvidenceError("Network qualification is missing required network observation: two distinct completed preview loads.");
    }
    if (!completed("clock_exchange").some((row) => row.rtt_ms !== "")) throw new EvidenceError("Network qualification is missing required network observation: completed clock exchange.");
    if (!completed("transfer").some((row) => row.transfer_interrupted === "true" && row.byte_range_resume_verified === "true"
      && row.payload_bytes !== "" && BigInt(row.payload_bytes) > 0n && row.throughput_bps !== "" && Number(row.throughput_bps) > 0)) {
      throw new EvidenceError("Network qualification is missing required network observation: interrupted transfer with verified byte-range resume.");
    }
    if (manifest.procedure.procedure_id === "HC-P0-NET-002" && completed("manual_fallback").length < 1) {
      throw new EvidenceError("Network qualification is missing required network observation: completed manual fallback.");
    }
    const timestamps = rows.map((row) => timestampAsNs(row.timestamp, row.timestamp_unit));
    if (timestamps[timestamps.length - 1] - timestamps[0] < BigInt(manifest.procedure.actual_duration_ns)) {
      throw new EvidenceError("Network measurement duration coverage is insufficient.");
    }
    return;
  }
  if (family === "COMP") {
    const rows = measurementRows.get("compatibility") ?? [];
    if (rows.length === 0) throw new EvidenceError("Compatibility qualification requires non-empty compatibility measurement rows.");
    const allowed = manifest.disposition === "PASS" ? new Set(["ok"]) : new Set(["ok", "conditional"]);
    if (rows.some((row) => !allowed.has(row.status))) throw new EvidenceError(`${manifest.disposition} compatibility evidence contains a disallowed result status.`);
    const requiredFamilies = new Set(["uvc", "network"]);
    for (const probeFamily of requiredFamilies) {
      if (!rows.some((row) => row.probe_family === probeFamily)) throw new EvidenceError(`Compatibility qualification is missing required ${probeFamily} probe execution.`);
    }
    if (rows.some((row) => row.os_name !== "Windows 10" || row.os_build !== "19045" || row.architecture !== "x64")) {
      throw new EvidenceError("HC-P0-COMP-001 qualification requires Windows 10 22H2 build 19045 x64 evidence.");
    }
  }
}

function capabilityValueMatches(value, definition) {
  if (definition.value_type === "string") return typeof value === "string";
  if (definition.value_type === "number") return typeof value === "number" && Number.isFinite(value);
  if (definition.value_type === "boolean") return typeof value === "boolean";
  if (definition.value_type === "string_array") return Array.isArray(value) && value.every((item) => typeof item === "string");
  if (definition.value_type === "capture_format_array") {
    return Array.isArray(value) && value.length > 0 && value.every((item) => item !== null && typeof item === "object"
      && Number.isInteger(item.width_px) && item.width_px > 0
      && Number.isInteger(item.height_px) && item.height_px > 0
      && Number.isInteger(item.frame_rate_numerator) && item.frame_rate_numerator > 0
      && Number.isInteger(item.frame_rate_denominator) && item.frame_rate_denominator > 0
      && typeof item.media_format === "string" && item.media_format.length > 0
      && (typeof item.pixel_format === "string" || item.pixel_format === null));
  }
  if (definition.value_type === "enum") return typeof value === "string" && definition.allowed_values.includes(value);
  return false;
}

function alignsToRangeStep(value, range) {
  const steps = (value - range.minimum) / range.step;
  return Math.abs(steps - Math.round(steps)) <= 1e-9 * Math.max(1, Math.abs(steps));
}

async function validateCapabilityVocabulary(capability) {
  const dictionary = await readJson(capabilityDictionaryPath);
  if (dictionary.evidence_format_version !== "1.0.0") throw new EvidenceError(`Unsupported capability dictionary version: ${dictionary.evidence_format_version}`);
  const definitions = new Map(dictionary.capabilities.map((definition) => [definition.capability_id, definition]));
  const seen = new Set();
  for (const item of capability.capabilities) {
    const definition = definitions.get(item.capability_id);
    if (!definition) throw new EvidenceError(`Unknown capability_id for evidence format 1.0.0: ${item.capability_id}`);
    const key = `${item.source_id ?? ""}\u0000${item.capability_id}`;
    if (seen.has(key)) throw new EvidenceError(`Duplicate capability binding: ${item.capability_id}`);
    seen.add(key);
    if (item.category !== definition.category) throw new EvidenceError(`Capability category mismatch: ${item.capability_id}`);
    if (definition.source_required && (!item.source_id || item.source_id.length === 0)) throw new EvidenceError(`Capability requires source_id: ${item.capability_id}`);
    if (!definition.source_required && item.source_id !== null) throw new EvidenceError(`Device-wide capability requires source_id=null: ${item.capability_id}`);
    const unavailable = item.status === "unknown" || item.status === "unsupported";
    if (!(unavailable && item.value === null) && !capabilityValueMatches(item.value, definition)) throw new EvidenceError(`Capability value type or enum mismatch: ${item.capability_id}`);
    if (item.unit !== definition.unit) throw new EvidenceError(`Capability unit mismatch: ${item.capability_id}`);
    if (!definition.range_allowed && item.range !== null) throw new EvidenceError(`Capability range is not allowed: ${item.capability_id}`);
    if (definition.range_required && !unavailable && item.range === null) throw new EvidenceError(`Capability range is required: ${item.capability_id}`);
    if (item.range !== null && item.range.minimum > item.range.maximum) throw new EvidenceError(`Capability range minimum exceeds maximum: ${item.capability_id}`);
    if (!definition.default_allowed && item.default !== null) throw new EvidenceError(`Capability default is not allowed: ${item.capability_id}`);
    if (item.default !== null && !capabilityValueMatches(item.default, definition)) throw new EvidenceError(`Capability default type mismatch: ${item.capability_id}`);
    if (typeof item.default === "number" && item.range !== null && (item.default < item.range.minimum || item.default > item.range.maximum)) {
      throw new EvidenceError(`Capability default is outside its range: ${item.capability_id}`);
    }
    if (typeof item.value === "number" && item.range !== null && (item.value < item.range.minimum || item.value > item.range.maximum)) {
      throw new EvidenceError(`Capability value is outside its range: ${item.capability_id}`);
    }
    for (const [label, numeric] of [["value", item.value], ["default", item.default]]) {
      if (typeof numeric !== "number") continue;
      if (definition.integer && !Number.isInteger(numeric)) throw new EvidenceError(`Capability ${label} must be an integer: ${item.capability_id}`);
      if (definition.minimum !== undefined && numeric < definition.minimum) throw new EvidenceError(`Capability ${label} is below minimum: ${item.capability_id}`);
      if (definition.maximum !== undefined && numeric > definition.maximum) throw new EvidenceError(`Capability ${label} is above maximum: ${item.capability_id}`);
      if (item.range !== null && !alignsToRangeStep(numeric, item.range)) throw new EvidenceError(`Capability ${label} is not aligned to range step: ${item.capability_id}`);
    }
  }
}

async function validateCrossDocumentSemantics(manifest, capability) {
  await validateCapabilityVocabulary(capability);
  if (manifest.run_id !== capability.run_id) throw new EvidenceError("run_id differs between run manifest and capability report.");
  if (manifest.environment.hardware_id !== capability.device.stable_test_device_id) {
    throw new EvidenceError("Manifest hardware_id differs from capability stable_test_device_id.");
  }
  const captureProcedure = isCaptureFamily(procedureFamily(manifest.procedure.procedure_id));
  const matchingProfiles = new Map();
  if (captureProcedure) {
    if (capability.capture_profiles.length !== manifest.configuration.streams.length) {
      throw new EvidenceError("Capture evidence requires exact one-to-one equality between manifest streams and capability profiles; capture profile count differs.");
    }
    const seenStreams = new Set();
    for (const stream of manifest.configuration.streams) {
      if (seenStreams.has(stream.stream_id)) throw new EvidenceError(`Duplicate stream profile in run manifest: ${stream.stream_id}`);
      seenStreams.add(stream.stream_id);
      const matches = capability.capture_profiles.filter((profile) => profile.source_id === manifest.configuration.source_id
        && profile.stream_id === stream.stream_id
        && captureProfilesEqual(profile.requested, stream.requested));
      if (matches.length !== 1) throw new EvidenceError(`Capture evidence must have exactly one capability profile matching source_id, stream_id, and requested profile: ${stream.stream_id}`);
      const [matchingProfile] = matches;
      matchingProfiles.set(stream.stream_id, matchingProfile);
      if (!captureProfilesEqual(matchingProfile.negotiated, stream.negotiated) || !captureProfilesEqual(matchingProfile.measured, stream.measured)) {
        throw new EvidenceError(`Capability profile negotiated/measured values differ from the run manifest for stream ${stream.stream_id}.`);
      }
      if ((stream.reported !== null) !== (matchingProfile.reported_supported === true)) {
        throw new EvidenceError(`Capability reported_supported differs from manifest reported profile availability for stream ${stream.stream_id}.`);
      }
    }
  } else if (capability.capture_profiles.length !== 0) {
    throw new EvidenceError("Non-capture evidence must not contain capture profiles.");
  }
  const qualifiedItems = capability.capabilities.filter((item) => item.status === "qualified");
  if (manifest.disposition !== "PASS" && qualifiedItems.length > 0) throw new EvidenceError("Qualified capability items require PASS disposition.");
  for (const profile of capability.capture_profiles) {
    if (profile.status === "qualified" && manifest.disposition !== "PASS") throw new EvidenceError("Qualified capture profile requires PASS disposition.");
    if (profile.status === "conditional" && manifest.disposition !== "CONDITIONAL") throw new EvidenceError("Conditional capture profile requires CONDITIONAL disposition.");
  }
  validateDispositionSemantics(manifest, capability, matchingProfiles);
}

export async function validateEvidencePackage(packageDirectory) {
  const root = path.resolve(packageDirectory);
  const manifest = await readJson(path.join(root, "run-manifest.json"));
  await validateJsonDocument(manifest, "https://humcapture.local/schemas/phase-0/1.0.0/run-manifest.schema.json", "run-manifest.json");
  const { registry, definitions } = await loadProcedureRegistry();
  const procedureDefinition = definitions.get(manifest.procedure.procedure_id);
  if (!procedureDefinition) throw new EvidenceError(`Unsupported Phase 0 procedure_id: ${manifest.procedure.procedure_id}`);
  const capability = await readJson(path.join(root, "capability-report.json"));
  await validateJsonDocument(capability, "https://humcapture.local/schemas/phase-0/1.0.0/capability-report.schema.json", "capability-report.json");
  validateProcedureContract(manifest, capability, registry, procedureDefinition);
  if (BigInt(manifest.timing.end_monotonic_ns) - BigInt(manifest.timing.start_monotonic_ns) !== BigInt(manifest.timing.duration_ns)) {
    throw new EvidenceError("Manifest monotonic timing does not equal duration_ns.");
  }
  if (manifest.procedure.actual_duration_ns !== manifest.timing.duration_ns) {
    throw new EvidenceError("Procedure actual_duration_ns does not equal manifest timing duration_ns.");
  }
  await validateCrossDocumentSemantics(manifest, capability);
  const packageFiles = await listPackageFiles(root);
  const listedPaths = new Set();
  const measurementRows = new Map();
  for (const artifact of manifest.artifacts) {
    if (listedPaths.has(artifact.relative_path)) throw new EvidenceError(`Duplicate artifact path: ${artifact.relative_path}`);
    listedPaths.add(artifact.relative_path);
    const contract = artifactContractFor(artifact.relative_path);
    if (artifact.media_type !== contract.media_type || artifact.role !== contract.role || artifact.csv_dictionary !== contract.csv_dictionary) {
      throw new EvidenceError(`Artifact contract differs from path-defined evidence contract: ${artifact.relative_path}`);
    }
    const absolutePath = resolveInside(root, artifact.relative_path);
    let fileStat;
    try {
      fileStat = await stat(absolutePath);
    } catch {
      throw new EvidenceError(`Listed artifact is missing: ${artifact.relative_path}`);
    }
    if (!fileStat.isFile()) throw new EvidenceError(`Listed artifact is not a regular file: ${artifact.relative_path}`);
    if (fileStat.size !== artifact.byte_length) throw new EvidenceError(`Artifact byte length mismatch: ${artifact.relative_path}`);
    if (await sha256File(absolutePath) !== artifact.sha256) throw new EvidenceError(`Artifact SHA-256 mismatch: ${artifact.relative_path}`);
    if (contract.csv_dictionary) measurementRows.set(contract.csv_dictionary, await validateCsv(absolutePath, contract.csv_dictionary, manifest.run_id));
  }
  const unlisted = packageFiles.filter((relativePath) => !listedPaths.has(relativePath));
  if (unlisted.length > 0) throw new EvidenceError("Evidence package contains unlisted files.", unlisted);
  const missing = [...listedPaths].filter((relativePath) => !packageFiles.includes(relativePath));
  if (missing.length > 0) throw new EvidenceError("Evidence manifest lists missing files.", missing);
  const missingRequired = [...requiredArtifactPaths(procedureDefinition)].filter((relativePath) => !listedPaths.has(relativePath));
  if (missingRequired.length > 0) throw new EvidenceError("Evidence package is incomplete for its procedure.", missingRequired);
  if (!manifest.artifacts.some((artifact) => artifact.role === "log")) throw new EvidenceError("Evidence package requires at least one log artifact.");
  validateMeasurementOrdering(measurementRows);
  validateTimestampProvenance(manifest, measurementRows);
  validateProcedureMeasurements(manifest, measurementRows, procedureDefinition);
  const expectedIndex = manifest.artifacts.map((artifact) => `${artifact.sha256}  ${artifact.relative_path}`).join("\n") + "\n";
  let actualIndex;
  try {
    actualIndex = await readFile(path.join(root, "hashes.sha256"), "utf8");
  } catch {
    throw new EvidenceError("hashes.sha256 is missing.");
  }
  if (actualIndex.replaceAll("\r\n", "\n") !== expectedIndex) throw new EvidenceError("hashes.sha256 does not exactly match the manifest artifact inventory.");
  return {
    runId: manifest.run_id,
    procedureId: manifest.procedure.procedure_id,
    hardwareQualification: procedureDefinition.hardware_qualification,
    artifactCount: manifest.artifacts.length,
    disposition: manifest.disposition
  };
}

export async function validateCampaignIndex(filePath) {
  const resolvedIndexPath = path.resolve(filePath);
  const document = await readJson(resolvedIndexPath);
  await validateJsonDocument(document, "https://humcapture.local/schemas/phase-0/1.0.0/campaign-index.schema.json", "campaign index");
  const { definitions } = await loadProcedureRegistry();
  const seen = new Set();
  for (const run of document.runs) {
    if (seen.has(run.run_id)) throw new EvidenceError(`Campaign index contains duplicate run_id: ${run.run_id}`);
    seen.add(run.run_id);
    const evidenceDirectory = await resolveExistingDirectoryInside(path.dirname(resolvedIndexPath), run.evidence_reference);
    const manifestPath = path.join(evidenceDirectory, "run-manifest.json");
    const manifest = await readJson(manifestPath);
    await validateJsonDocument(manifest, "https://humcapture.local/schemas/phase-0/1.0.0/run-manifest.schema.json", `referenced manifest for ${run.run_id}`);
    const manifestHash = await sha256File(manifestPath);
    if (manifestHash !== run.manifest_sha256) throw new EvidenceError(`Campaign manifest SHA-256 mismatch for run_id: ${run.run_id}`);
    if (manifest.run_id !== run.run_id) throw new EvidenceError(`Campaign run_id differs from referenced manifest: ${run.run_id}`);
    if (manifest.campaign_id !== document.campaign_id) throw new EvidenceError(`Referenced manifest campaign_id differs from campaign index: ${run.run_id}`);
    if (manifest.procedure?.procedure_id !== run.procedure_id) throw new EvidenceError(`Campaign procedure_id differs from referenced manifest: ${run.run_id}`);
    if (manifest.disposition !== run.disposition) throw new EvidenceError(`Campaign disposition differs from referenced manifest: ${run.run_id}`);
    const procedureDefinition = definitions.get(manifest.procedure.procedure_id);
    if (!procedureDefinition) throw new EvidenceError(`Campaign references unsupported procedure_id: ${manifest.procedure.procedure_id}`);
    if (run.hardware_qualification !== procedureDefinition.hardware_qualification) throw new EvidenceError(`Campaign hardware_qualification differs from the procedure registry: ${run.run_id}`);
    if (!procedureDefinition.hardware_qualification && ["PASS", "CONDITIONAL"].includes(manifest.disposition)) {
      throw new EvidenceError(`Synthetic procedure outcomes cannot be indexed as hardware qualification: ${run.run_id}`);
    }
    await validateEvidencePackage(evidenceDirectory);
  }
  return { campaignId: document.campaign_id, runCount: document.runs.length };
}

import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { cp, link, mkdir, mkdtemp, readFile, rm, symlink, unlink, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { EvidenceError, generateHashes, validateCampaignIndex, validateEvidencePackage } from "../src/evidence.js";

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const toolRoot = path.resolve(testDirectory, "..");
const validFixture = path.join(toolRoot, "fixtures", "valid", "android-run");

async function temporaryFixture(t) {
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), "humcapture-p0-"));
  const packageDirectory = path.join(temporaryRoot, "run");
  await cp(validFixture, packageDirectory, { recursive: true });
  t.after(async () => rm(temporaryRoot, { recursive: true, force: true }));
  return packageDirectory;
}

async function temporaryCampaign(t) {
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), "humcapture-p0-campaign-"));
  await cp(path.join(toolRoot, "fixtures", "valid"), temporaryRoot, { recursive: true });
  t.after(async () => rm(temporaryRoot, { recursive: true, force: true }));
  return temporaryRoot;
}

async function updateJson(filePath, mutate) {
  const document = JSON.parse(await readFile(filePath, "utf8"));
  mutate(document);
  await writeFile(filePath, `${JSON.stringify(document, null, 2)}\n`);
}

async function fileHash(filePath) {
  return createHash("sha256").update(await readFile(filePath)).digest("hex");
}

async function prepareQualificationClaim(packageDirectory, durationNs = "2000000000") {
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  await updateJson(manifestPath, (manifest) => {
    manifest.procedure.mode = "qualification";
    manifest.procedure.required_duration_ns = durationNs;
    manifest.procedure.actual_duration_ns = durationNs;
    manifest.timing.end_monotonic_ns = (BigInt(manifest.timing.start_monotonic_ns) + BigInt(durationNs)).toString();
    manifest.timing.duration_ns = durationNs;
    manifest.disposition = "PASS";
    manifest.reviewer = "SYNTHETIC\\reviewer";
    manifest.limitations = [];
    manifest.deviations = [];
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => { report.capture_profiles[0].status = "qualified"; });
  await mkdir(path.join(packageDirectory, "media"), { recursive: true });
  await writeFile(path.join(packageDirectory, "media", "synthetic.bin"), "synthetic media marker\n");
  await generateHashes(packageDirectory);
}

async function writeDictionaryCsv(packageDirectory, dictionaryName, rows) {
  const dictionary = JSON.parse(await readFile(path.join(toolRoot, "csv-dictionaries", `${dictionaryName}.json`), "utf8"));
  const header = dictionary.columns.map((column) => column.name).join(",");
  await mkdir(path.join(packageDirectory, "measurements"), { recursive: true });
  await writeFile(path.join(packageDirectory, "measurements", `${dictionaryName}.csv`), `${header}\n${rows.join("\n")}\n`);
}

async function prepareNonCaptureQualification(packageDirectory, family, options = {}) {
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  const runId = JSON.parse(await readFile(manifestPath, "utf8")).run_id;
  await rm(path.join(packageDirectory, "measurements"), { recursive: true, force: true });
  await updateJson(manifestPath, (manifest) => {
    manifest.procedure.procedure_id = family === "NET" ? (options.procedureId ?? "HC-P0-NET-001") : `HC-P0-${family}-001`;
    manifest.procedure.mode = "qualification";
    manifest.probe.platform = family === "NET" ? "network" : "windows";
    manifest.configuration.source_id = family === "NET" ? "network" : "windows-host";
    manifest.configuration.streams = [];
    manifest.timestamp_provenance = family === "NET" ? [{
      channel_type: "network",
      channel_id: "network",
      timestamp_field: "timestamp",
      domain: "host_qpc",
      source: "host_sample",
      unit: "ns",
      frequency_hz: 1000000000,
      mapping_method: null,
      uncertainty_ns: null,
      notes: "Synthetic fixture only."
    }] : [];
    manifest.disposition = "PASS";
    manifest.reviewer = "SYNTHETIC\\reviewer";
    manifest.limitations = [];
    manifest.deviations = [];
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = family === "NET" ? "network" : "windows_host";
    report.device.camera_stack = "network_only";
    report.capabilities = [];
    report.capture_profiles = [];
  });
  if (family === "NET") {
    const discoveryRows = [
      `${runId},0,1000000000,host_qpc,ns,discovery,bidirectional,coordinator,64,1.0,0.1,0,1000000,false,false,true,ok,synthetic`,
      `${runId},1,3000000000,host_qpc,ns,discovery,bidirectional,camera-a,64,1.0,0.1,0,1000000,false,false,true,ok,synthetic`
    ];
    const completeRows = [
      discoveryRows[0],
      `${runId},1,1400000000,host_qpc,ns,preview_load,android_to_windows,camera-a,1000000,5.0,1.0,0,8000000,false,false,true,ok,synthetic`,
      `${runId},2,1800000000,host_qpc,ns,preview_load,android_to_windows,camera-b,1000000,5.0,1.0,0,8000000,false,false,true,ok,synthetic`,
      `${runId},3,2200000000,host_qpc,ns,clock_exchange,bidirectional,camera-a,64,2.0,0.2,0,1000000,false,false,true,ok,synthetic`,
      `${runId},4,3000000000,host_qpc,ns,transfer,android_to_windows,camera-a,50000000,10.0,2.0,0,40000000,true,true,true,ok,synthetic`
    ];
    if (options.procedureId === "HC-P0-NET-002" && !options.omitManualFallback) {
      completeRows.push(`${runId},5,3100000000,host_qpc,ns,manual_fallback,bidirectional,camera-a,64,2.0,0.2,0,1000000,false,false,true,ok,synthetic`);
    }
    await writeDictionaryCsv(packageDirectory, "network", options.discoveryOnly ? discoveryRows : completeRows);
  } else {
    await writeDictionaryCsv(packageDirectory, "compatibility", [
      `${runId},windows-host,Windows 10,19045,x64,uvc,humcapture-phase0-uvc-probe,0.1.0,ok,synthetic`,
      `${runId},windows-host,Windows 10,19045,x64,network,humcapture-phase0-network-probe,0.1.0,ok,synthetic`
    ]);
  }
  await generateHashes(packageDirectory);
}

async function prepareCompleteCaptureQualification(packageDirectory) {
  const durationNs = "1800000000000";
  await prepareQualificationClaim(packageDirectory, durationNs);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    for (const profileName of ["requested", "reported", "negotiated", "measured"]) manifest.configuration.streams[0][profileName].fps = 1;
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    for (const profileName of ["requested", "negotiated", "measured"]) report.capture_profiles[0][profileName].fps = 1;
  });
  const manifest = JSON.parse(await readFile(path.join(packageDirectory, "run-manifest.json"), "utf8"));
  const frames = [];
  for (let sequence = 0; sequence < 1800; sequence += 1) {
    const sourceTimestamp = 1_000_000_000n + BigInt(sequence) * 1_000_000_000n;
    const presentationTimestamp = BigInt(sequence) * 1_000_000n;
    const hostTimestamp = sourceTimestamp + 100_000n;
    frames.push(`${manifest.run_id},master,${sequence},${sourceTimestamp},sensor_monotonic,ns,${presentationTimestamp},us,${hostTimestamp},device_monotonic,ns,none,0,1920,1080,24000,0,synthetic`);
  }
  await writeDictionaryCsv(packageDirectory, "frames", frames);
  await writeDictionaryCsv(packageDirectory, "thermal-power-storage", [
    `${manifest.run_id},0,1000000000,device_monotonic,ns,synthetic_nominal,30.0,90.0,false,10000000000,1.2,synthetic`,
    `${manifest.run_id},1,1801000000000,device_monotonic,ns,synthetic_nominal,31.0,89.0,false,9000000000,1.3,synthetic`
  ]);
  await generateHashes(packageDirectory);
}

async function prepareUvcRecoveryQualification(packageDirectory) {
  const durationNs = "1000000000";
  await prepareQualificationClaim(packageDirectory, durationNs);
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  await updateJson(manifestPath, (manifest) => {
    manifest.procedure.procedure_id = "HC-P0-UVC-002";
    manifest.probe.platform = "windows";
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = "windows_uvc";
    report.device.camera_stack = "windows_media_capture";
  });
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  const frames = [];
  for (let sequence = 0; sequence < 60; sequence += 1) {
    const sourceTimestamp = 1_000_000_000n + BigInt(sequence) * 16_666_667n;
    const presentationTimestamp = BigInt(sequence) * 16_667n;
    const hostTimestamp = sourceTimestamp + 100_000n;
    frames.push(`${manifest.run_id},master,${sequence},${sourceTimestamp},sensor_monotonic,ns,${presentationTimestamp},us,${hostTimestamp},device_monotonic,ns,none,0,1920,1080,24000,0,synthetic`);
  }
  await writeDictionaryCsv(packageDirectory, "frames", frames);
  await writeDictionaryCsv(packageDirectory, "thermal-power-storage", [
    `${manifest.run_id},0,1000000000,device_monotonic,ns,synthetic_nominal,30.0,90.0,false,10000000000,1.2,synthetic`,
    `${manifest.run_id},1,2000000000,device_monotonic,ns,synthetic_nominal,31.0,89.0,false,9000000000,1.3,synthetic`
  ]);
  await generateHashes(packageDirectory);
}

async function expectEvidenceError(action, pattern) {
  await assert.rejects(action, (error) => error instanceof EvidenceError && pattern.test(`${error.message}\n${error.details.join("\n")}`));
}

test("valid synthetic evidence package passes", async () => {
  const result = await validateEvidencePackage(validFixture);
  assert.equal(result.runId, "018f6f5e-6b1a-7c23-8d45-0123456789ab");
  assert.equal(result.disposition, "INCONCLUSIVE");
  assert.ok(result.artifactCount >= 5);
});

test("campaign index passes schema and duplicate checks", async () => {
  const result = await validateCampaignIndex(path.join(toolRoot, "fixtures", "valid", "campaign-index.json"));
  assert.equal(result.campaignId, "synthetic-campaign-001");
  assert.equal(result.runCount, 1);
});

test("synthetic PASS cannot be indexed as hardware qualification", async (t) => {
  const campaignDirectory = await temporaryCampaign(t);
  const packageDirectory = path.join(campaignDirectory, "android-run");
  await prepareCompleteCaptureQualification(packageDirectory);
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  const indexPath = path.join(campaignDirectory, "campaign-index.json");
  await updateJson(indexPath, (index) => {
    index.runs[0].procedure_id = "HC-P0-SYN-001";
    index.runs[0].disposition = "PASS";
  });
  const manifestHash = await fileHash(manifestPath);
  await updateJson(indexPath, (index) => { index.runs[0].manifest_sha256 = manifestHash; });
  await expectEvidenceError(() => validateCampaignIndex(indexPath), /Synthetic procedure outcomes cannot be indexed as hardware qualification/);
});

test("campaign index rejects a stale manifest hash", async (t) => {
  const temporaryRoot = await temporaryCampaign(t);
  const indexPath = path.join(temporaryRoot, "campaign-index.json");
  const index = JSON.parse(await readFile(indexPath, "utf8"));
  index.runs[0].manifest_sha256 = "0000000000000000000000000000000000000000000000000000000000000000";
  await writeFile(indexPath, `${JSON.stringify(index, null, 2)}\n`);
  await expectEvidenceError(() => validateCampaignIndex(indexPath), /manifest SHA-256 mismatch/);
});

test("campaign hardware qualification flag must match the procedure registry", async (t) => {
  const campaignDirectory = await temporaryCampaign(t);
  const indexPath = path.join(campaignDirectory, "campaign-index.json");
  await updateJson(indexPath, (index) => { index.runs[0].hardware_qualification = true; });
  await expectEvidenceError(() => validateCampaignIndex(indexPath), /hardware_qualification differs/);
});

test("diagnostic evidence cannot claim PASS", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => { manifest.disposition = "PASS"; });
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /diagnostic|must be equal to one of the allowed values/);
});

test("qualification PASS cannot use a short soak", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareQualificationClaim(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /registry minimum|30-minute soak/);
});

test("capture qualification rejects a header-only frame measurement", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareQualificationClaim(packageDirectory, "1800000000000");
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const header = (await readFile(framesPath, "utf8")).split(/\r?\n/, 1)[0];
  await writeFile(framesPath, `${header}\n`);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.timestamp_provenance = manifest.timestamp_provenance.filter((entry) => entry.channel_type !== "stream");
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /requires non-empty frames|measurement rows/);
});

test("two frames cannot substantiate a 30-minute capture qualification", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareQualificationClaim(packageDirectory, "1800000000000");
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /frame accounting|duration coverage/);
});

test("complete frame accounting can substantiate a 30-minute capture qualification", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareCompleteCaptureQualification(packageDirectory);
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.disposition, "PASS");
});

test("AND-001 cannot qualify at a reduced synthetic frame rate", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareCompleteCaptureQualification(packageDirectory);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.procedure.procedure_id = "HC-P0-AND-001";
    manifest.probe.platform = "android";
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = "android";
    report.device.camera_stack = "android_camera2";
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /procedure profile|1080p60|fps/);
});

test("AND-002 requires both normative master and preview stream profiles", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.procedure.procedure_id = "HC-P0-AND-002";
    manifest.probe.platform = "android";
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = "android";
    report.device.camera_stack = "android_camera2";
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /required stream|preview|procedure profile/);
});

test("UVC procedures reject a non-Windows non-UVC platform", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => { manifest.procedure.procedure_id = "HC-P0-UVC-001"; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /probe platform|device platform|camera stack/);
});

test("UVC-001 accepts its normative profile on the Windows UVC platform", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.procedure.procedure_id = "HC-P0-UVC-001";
    manifest.probe.platform = "windows";
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = "windows_uvc";
    report.device.camera_stack = "windows_media_capture";
  });
  await generateHashes(packageDirectory);
  assert.equal((await validateEvidencePackage(packageDirectory)).disposition, "INCONCLUSIVE");
});

test("frame dimensions must match the qualified per-stream profile", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareCompleteCaptureQualification(packageDirectory);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replaceAll(",1920,1080,24000,", ",1,1,1,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /dimensions differ from measured profile/);
});

test("capture qualification requires non-empty full-duration system measurements", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareCompleteCaptureQualification(packageDirectory);
  const measurementPath = path.join(packageDirectory, "measurements", "thermal-power-storage.csv");
  const header = (await readFile(measurementPath, "utf8")).split(/\r?\n/, 1)[0];
  await writeFile(measurementPath, `${header}\n`);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.timestamp_provenance = manifest.timestamp_provenance.filter((entry) => entry.channel_id !== "thermal-power-storage");
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /requires non-empty thermal-power-storage measurement rows/);
});

test("UVC disconnect and reconnect requires lifecycle events and two finalized artifacts", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareUvcRecoveryQualification(packageDirectory);
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  const runId = JSON.parse(await readFile(manifestPath, "utf8")).run_id;
  await updateJson(manifestPath, (manifest) => {
    manifest.timestamp_provenance.push({
      channel_type: "system",
      channel_id: "capture-events",
      timestamp_field: "timestamp",
      domain: "host_qpc",
      source: "host_sample",
      unit: "ns",
      frequency_hz: 1000000000,
      mapping_method: null,
      uncertainty_ns: null,
      notes: "Synthetic fixture only."
    });
  });
  await writeFile(path.join(packageDirectory, "media", "synthetic-post.bin"), "synthetic post-reconnect media marker\n");
  const eventRows = [
    `${runId},0,1000000000,host_qpc,ns,master,capture_started,,ok,synthetic`,
    `${runId},1,1200000000,host_qpc,ns,master,device_disconnected,,ok,synthetic`,
    `${runId},2,1300000000,host_qpc,ns,master,pre_disconnect_finalized,media/synthetic.bin,ok,synthetic`,
    `${runId},3,1400000000,host_qpc,ns,master,device_reconnected,,ok,synthetic`,
    `${runId},4,1500000000,host_qpc,ns,master,post_reconnect_capture_started,,ok,synthetic`,
    `${runId},5,2000000000,host_qpc,ns,master,post_reconnect_finalized,media/synthetic-post.bin,ok,synthetic`
  ];
  await writeDictionaryCsv(packageDirectory, "capture-events", eventRows);
  await generateHashes(packageDirectory);
  assert.equal((await validateEvidencePackage(packageDirectory)).disposition, "PASS");

  await writeDictionaryCsv(packageDirectory, "capture-events", [
    ...eventRows,
    `${runId},6,2100000000,host_qpc,ns,master,device_disconnected,,ok,synthetic-extra-terminal-transition`
  ]);
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /exact lifecycle|illegal|unexpected|out of order/);

  await writeDictionaryCsv(packageDirectory, "capture-events", eventRows.filter((row) => !row.includes(",device_reconnected,")));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /missing required capture event: device_reconnected/);
});

test("concurrent master and preview streams use independent profiles", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareCompleteCaptureQualification(packageDirectory);
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  const runId = JSON.parse(await readFile(manifestPath, "utf8")).run_id;
  await updateJson(manifestPath, (manifest) => {
    const master = manifest.configuration.streams[0];
    const preview = structuredClone(master);
    preview.stream_id = "preview";
    preview.purpose = "preview";
    for (const profileName of ["requested", "reported", "negotiated", "measured"]) {
      preview[profileName].width_px = 1280;
      preview[profileName].height_px = 720;
      preview[profileName].fps = 0.5;
    }
    manifest.configuration.streams.push(preview);
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    const preview = structuredClone(report.capture_profiles[0]);
    preview.profile_id = "preview-720p0.5-h264";
    preview.stream_id = "preview";
    for (const profileName of ["requested", "negotiated", "measured"]) {
      preview[profileName].width_px = 1280;
      preview[profileName].height_px = 720;
      preview[profileName].fps = 0.5;
    }
    report.capture_profiles.push(preview);
  });
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  const previewRows = [];
  for (let sequence = 0; sequence < 900; sequence += 1) {
    const sourceTimestamp = 1_000_000_000n + BigInt(sequence) * 2_000_000_000n;
    previewRows.push(`${runId},preview,${sequence},${sourceTimestamp},sensor_monotonic,ns,,,${sourceTimestamp + 100_000n},device_monotonic,ns,none,0,1280,720,12000,0,synthetic`);
  }
  await writeFile(framesPath, `${frames.trimEnd()}\n${previewRows.join("\n")}\n`);
  await writeDictionaryCsv(packageDirectory, "sensors", [
    `${runId},rotation-vector-0,rotation_vector,0,1000000000,sensor_monotonic,ns,3,0.0,0.0,0.0,1.0,android_device,none,synthetic`,
    `${runId},rotation-vector-0,rotation_vector,1,1801000000000,sensor_monotonic,ns,3,0.0,0.0,0.0,1.0,android_device,none,synthetic`
  ]);
  await updateJson(manifestPath, (manifest) => {
    manifest.timestamp_provenance.push(
      { ...manifest.timestamp_provenance.find((entry) => entry.channel_id === "master" && entry.timestamp_field === "source_timestamp"), channel_id: "preview" },
      { ...manifest.timestamp_provenance.find((entry) => entry.channel_id === "master" && entry.timestamp_field === "host_arrival_timestamp"), channel_id: "preview" }
    );
  });
  await generateHashes(packageDirectory);
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.disposition, "PASS");
});

test("unmarked frame timestamp regression is rejected", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replace(",1016666667,sensor_monotonic,", ",999999999,sensor_monotonic,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /timestamp regression/);
});

test("duplicate frame sequence is rejected", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replace(",master,1,", ",master,0,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /sequence is duplicate or out of order/);
});

test("network qualification uses network-specific disposition semantics", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareNonCaptureQualification(packageDirectory, "NET");
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.disposition, "PASS");
});

test("compatibility qualification uses compatibility-specific disposition semantics", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareNonCaptureQualification(packageDirectory, "COMP");
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.disposition, "PASS");
});

test("compatibility qualification requires both UVC and network probe families", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareNonCaptureQualification(packageDirectory, "COMP");
  const measurementPath = path.join(packageDirectory, "measurements", "compatibility.csv");
  const rows = (await readFile(measurementPath, "utf8")).trimEnd().split(/\r?\n/);
  await writeFile(measurementPath, `${rows.slice(0, 2).join("\n")}\n`);
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /missing required network probe execution/);
});

test("network and compatibility qualification require measurement rows", async (t) => {
  for (const family of ["NET", "COMP"]) {
    const packageDirectory = await temporaryFixture(t);
    await prepareNonCaptureQualification(packageDirectory, family);
    const measurementPath = path.join(packageDirectory, "measurements", family === "NET" ? "network.csv" : "compatibility.csv");
    const header = (await readFile(measurementPath, "utf8")).split(/\r?\n/, 1)[0];
    await writeFile(measurementPath, `${header}\n`);
    if (family === "NET") {
      await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => { manifest.timestamp_provenance = []; });
    }
    await generateHashes(packageDirectory);
    await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /requires non-empty (network|compatibility) measurement rows/);
  }
});

test("discovery-only evidence cannot qualify a network procedure", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareNonCaptureQualification(packageDirectory, "NET", { discoveryOnly: true });
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /missing required network observation/);
});

test("laptop-hotspot qualification requires and accepts completed manual fallback", async (t) => {
  const missingPackage = await temporaryFixture(t);
  await prepareNonCaptureQualification(missingPackage, "NET", { procedureId: "HC-P0-NET-002", omitManualFallback: true });
  await expectEvidenceError(() => validateEvidencePackage(missingPackage), /completed manual fallback/);

  const completePackage = await temporaryFixture(t);
  await prepareNonCaptureQualification(completePackage, "NET", { procedureId: "HC-P0-NET-002" });
  const result = await validateEvidencePackage(completePackage);
  assert.equal(result.disposition, "PASS");
});

test("UVC vocabulary accepts reported formats, control values, and USB topology", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.capabilities.push(
      {
        capability_id: "camera.formats.reported",
        source_id: "camera.back.wide",
        category: "camera",
        status: "reported",
        provenance: "synthetic MediaFrameSource.SupportedFormats",
        value: [{ width_px: 1920, height_px: 1080, frame_rate_numerator: 30000, frame_rate_denominator: 1001, media_format: "MJPG", pixel_format: "MJPG" }],
        unit: null,
        range: null,
        default: null
      },
      {
        capability_id: "camera.controls.exposure.value",
        source_id: "camera.back.wide",
        category: "control",
        status: "reported",
        provenance: "synthetic exposure control",
        value: 10000,
        unit: "ticks_100ns",
        range: { minimum: 1, maximum: 1000000, step: 1 },
        default: 10000
      },
      {
        capability_id: "camera.usb.topology_path",
        source_id: "camera.back.wide",
        category: "usb",
        status: "reported",
        provenance: "synthetic PnP topology",
        value: ["PCIROOT(0)", "USBROOT(0)", "PORT(3)"],
        unit: null,
        range: null,
        default: null
      }
    );
    for (const capability of report.capabilities) capability.default ??= null;
  });
  await generateHashes(packageDirectory);
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.disposition, "INCONCLUSIVE");
});

test("UVC topology and control values enforce integer/range/step constraints", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.capabilities.push({
      capability_id: "camera.usb.port_number",
      source_id: "camera.back.wide",
      category: "usb",
      status: "reported",
      provenance: "synthetic PnP topology",
      value: -1.5,
      unit: "port",
      range: null,
      default: null
    });
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /integer|minimum/);

  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.capabilities.at(-1).value = 3;
    report.capabilities.push({
      capability_id: "camera.controls.exposure.value",
      source_id: "camera.back.wide",
      category: "control",
      status: "reported",
      provenance: "synthetic exposure control",
      value: 2,
      unit: "ticks_100ns",
      range: { minimum: 0, maximum: 12, step: 3 },
      default: 3
    });
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /not aligned to range step/);
});

test("qualification PASS cannot hide capture-profile substitution", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareQualificationClaim(packageDirectory, "1800000000000");
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.configuration.streams[0].negotiated.width_px = 640;
    manifest.configuration.streams[0].measured.width_px = 320;
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.capture_profiles[0].negotiated.width_px = 640;
    report.capture_profiles[0].measured.width_px = 320;
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /requested-to-negotiated capture substitution/);
});

test("qualification PASS cannot contradict the reported candidate profile", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await prepareQualificationClaim(packageDirectory, "1800000000000");
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => { manifest.configuration.streams[0].reported.width_px = 640; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /reported profile does not match/);
});

test("manifest and capability hardware identities must match", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => { report.device.stable_test_device_id = "DIFFERENT-DEVICE"; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /hardware_id differs/);
});

test("manifest and capability capture profiles must agree", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => { report.capture_profiles[0].negotiated.width_px = 640; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /negotiated\/measured values differ/);
});

test("capture evidence rejects every status of extra unbound capability profile", async (t) => {
  for (const status of ["qualified", "conditional", "measured"]) {
    const packageDirectory = await temporaryFixture(t);
    await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
      const extra = structuredClone(report.capture_profiles[0]);
      extra.profile_id = `unbound-1080p120-${status}`;
      extra.stream_id = `unbound-extra-${status}`;
      extra.requested.fps = 120;
      extra.negotiated.fps = 120;
      extra.measured.fps = 120;
      extra.status = status;
      report.capture_profiles.push(extra);
    });
    await generateHashes(packageDirectory);
    await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /one-to-one|extra|capture profile count/);
  }
});

test("non-capture evidence rejects capture profiles at every disposition", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.procedure.procedure_id = "HC-P0-NET-001";
    manifest.probe.platform = "network";
    manifest.configuration.source_id = "network";
    manifest.configuration.streams = [];
    manifest.timestamp_provenance = [];
  });
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => {
    report.device.platform = "network";
    report.device.camera_stack = "network_only";
  });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /Non-capture evidence must not contain capture profiles/);
});

test("procedure-required evidence cannot be removed and rehashed", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await rm(path.join(packageDirectory, "measurements"), { recursive: true, force: true });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /incomplete for its procedure/);
});

test("measurement row run_id must match manifest", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replaceAll("018f6f5e-6b1a-7c23-8d45-0123456789ab", "018f6f5e-6b1a-7c23-8d45-abcdefabcdef"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /does not match manifest run_id/);
});

test("known measurement path cannot bypass CSV validation by relabeling media type", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    const frames = manifest.artifacts.find((artifact) => artifact.relative_path === "measurements/frames.csv");
    frames.media_type = "application/octet-stream";
    delete frames.csv_dictionary;
  });
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /path-defined evidence contract/);
});

test("timestamp provenance must bind every measured timestamp", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => {
    manifest.timestamp_provenance = manifest.timestamp_provenance.filter((entry) => entry.timestamp_field !== "host_arrival_timestamp");
  });
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /Missing timestamp provenance binding/);
});

test("paired timestamp fields must be present together", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replace(",device_monotonic,ns,none", ",device_monotonic,,none"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /fields must be present together/);
});

test("CSV parser rejects characters after a closing quote", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replace(",synthetic\n", ",\"synthetic\"x\n"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /Malformed CSV character after closing quote/);
});

test("CSV floats reject hexadecimal syntax and physical-range violations", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const measurementsPath = path.join(packageDirectory, "measurements", "thermal-power-storage.csv");
  const original = await readFile(measurementsPath, "utf8");
  await writeFile(measurementsPath, original.replace(",30.0,90.0,", ",0x10,90.0,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /invalid float64/);
  await writeFile(measurementsPath, original.replace(",30.0,90.0,", ",30.0,1000,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /above maximum 100/);
});

test("qualified capability status cannot appear in inconclusive evidence", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "capability-report.json"), (report) => { report.capabilities[0].status = "qualified"; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /Qualified capability items require PASS/);
});

test("campaign validation detects a tampered referenced artifact", async (t) => {
  const campaignDirectory = await temporaryCampaign(t);
  await writeFile(path.join(campaignDirectory, "android-run", "summary.md"), "tampered after campaign indexing\n");
  await expectEvidenceError(() => validateCampaignIndex(path.join(campaignDirectory, "campaign-index.json")), /byte length mismatch|SHA-256 mismatch/);
});

test("campaign index rejects a referenced manifest from another campaign", async (t) => {
  const campaignDirectory = await temporaryCampaign(t);
  const manifestPath = path.join(campaignDirectory, "android-run", "run-manifest.json");
  await updateJson(manifestPath, (manifest) => { manifest.campaign_id = "different-campaign"; });
  const indexPath = path.join(campaignDirectory, "campaign-index.json");
  await updateJson(indexPath, (index) => { index.runs[0].manifest_sha256 = null; });
  const manifestHash = await fileHash(manifestPath);
  await updateJson(indexPath, (index) => { index.runs[0].manifest_sha256 = manifestHash; });
  await expectEvidenceError(() => validateCampaignIndex(indexPath), /campaign_id differs/);
});

test("campaign index rejects a directory junction escaping campaign root", { skip: process.platform !== "win32" }, async (t) => {
  const temporaryRoot = await mkdtemp(path.join(os.tmpdir(), "humcapture-p0-junction-"));
  t.after(async () => rm(temporaryRoot, { recursive: true, force: true }));
  const campaignDirectory = path.join(temporaryRoot, "campaign");
  const outsideDirectory = path.join(temporaryRoot, "outside-run");
  await mkdir(campaignDirectory);
  await cp(validFixture, outsideDirectory, { recursive: true });
  await cp(path.join(toolRoot, "fixtures", "valid", "campaign-index.json"), path.join(campaignDirectory, "campaign-index.json"));
  await symlink(outsideDirectory, path.join(campaignDirectory, "android-run"), "junction");
  await expectEvidenceError(() => validateCampaignIndex(path.join(campaignDirectory, "campaign-index.json")), /outside campaign root|physical directory/);
});

test("hard-linked evidence artifacts are rejected", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const externalPath = path.join(path.dirname(packageDirectory), "external-summary.md");
  await writeFile(externalPath, "hard-linked synthetic content\n");
  await unlink(path.join(packageDirectory, "summary.md"));
  await link(externalPath, path.join(packageDirectory, "summary.md"));
  await expectEvidenceError(() => generateHashes(packageDirectory), /Hard-linked files are not allowed/);
});

test("missing required manifest fields fail", async () => {
  await expectEvidenceError(
    () => validateEvidencePackage(path.join(toolRoot, "fixtures", "invalid", "missing-required")),
    /required property/
  );
});

test("newer unsupported evidence version fails", async () => {
  await expectEvidenceError(
    () => validateEvidencePackage(path.join(toolRoot, "fixtures", "invalid", "newer-version")),
    /must be equal to constant/
  );
});

test("unknown Phase 0 procedure identifiers are rejected", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await updateJson(path.join(packageDirectory, "run-manifest.json"), (manifest) => { manifest.procedure.procedure_id = "HC-P0-FAKE-001"; });
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /Unsupported Phase 0 procedure_id/);
});

test("path traversal fails before file access", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const manifestPath = path.join(packageDirectory, "run-manifest.json");
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  manifest.artifacts[0].relative_path = "../outside.txt";
  await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /pattern|relative_path|escapes/);
});

test("artifact hash mismatch fails", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await writeFile(path.join(packageDirectory, "summary.md"), "tampered synthetic summary\n");
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /byte length mismatch|SHA-256 mismatch/);
});

test("missing listed artifact fails", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await unlink(path.join(packageDirectory, "summary.md"));
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /missing/);
});

test("unlisted artifact fails", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await writeFile(path.join(packageDirectory, "unlisted.txt"), "synthetic\n");
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /unlisted files/);
});

test("invalid CSV primitive fails even when hashes are regenerated", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  const framesPath = path.join(packageDirectory, "measurements", "frames.csv");
  const frames = await readFile(framesPath, "utf8");
  await writeFile(framesPath, frames.replace(",1920,1080,", ",not-a-width,1080,"));
  await generateHashes(packageDirectory);
  await expectEvidenceError(() => validateEvidencePackage(packageDirectory), /invalid uint32/);
});

test("hash generator produces a self-consistent package", async (t) => {
  const packageDirectory = await temporaryFixture(t);
  await writeFile(path.join(packageDirectory, "logs", "additional.log"), "synthetic additional log\n");
  const artifacts = await generateHashes(packageDirectory);
  assert.ok(artifacts.some((artifact) => artifact.relative_path === "logs/additional.log"));
  const result = await validateEvidencePackage(packageDirectory);
  assert.equal(result.artifactCount, artifacts.length);
});

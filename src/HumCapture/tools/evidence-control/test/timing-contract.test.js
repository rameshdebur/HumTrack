import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";
import {
  TIMING_FORMAT, FRAME_DISPOSITION, FRAME_PRESENCE, IMU_PRESENCE, SENSOR_KIND,
  analyzeSequenceAndCadence, crc32c, decodeFrameStream, decodeImuStream,
  encodeFrameStream, encodeImuStream, evaluateProtocolConditions, sha256,
  validateClockMappings, validateFrameAssociation, validateImuMetadata, validateTimingPackageProfile
} from "../src/timing-contract-conformance.js";

const toolRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const humCaptureRoot = path.resolve(toolRoot, "..", "..");
const fixtureRoot = path.join(toolRoot, "fixtures", "timing", "v1");
const schemaRoot = path.join(humCaptureRoot, "docs", "interfaces", "schemas", "timing", "v1");
const captureId = "10000000-0000-4000-8000-000000000001";
const frameStreamId = "10000000-0000-4000-8000-000000000002";
const imuStreamId = "10000000-0000-4000-8000-000000000003";

async function json(file) { return JSON.parse(await readFile(file, "utf8")); }
function throwsCode(fn, code) { assert.throws(fn, (error) => error?.code === code); }
function rewriteHeaderCrc(bytes) { bytes.writeUInt32LE(crc32c(bytes.subarray(0, 60)), 60); }
function basicFrame(sequence, ticks, extra = {}) { return { sequence: BigInt(sequence), nativeTicks: BigInt(ticks), segmentId: 0, disposition: FRAME_DISPOSITION.ACCEPTED, ...extra }; }

test("HC-TIM-TEST-001 timing, IMU and camera metadata schemas compile and accept canonical fixtures", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  const files = (await readdir(schemaRoot)).filter((name) => name.endsWith(".schema.json")).sort();
  for (const file of files) ajv.addSchema(await json(path.join(schemaRoot, file)));
  for (const [schema, fixture] of [["timing-metadata", "valid-timing-metadata"], ["imu-metadata", "valid-imu-metadata"], ["camera-metadata", "valid-camera-metadata"]]) {
    const validate = ajv.getSchema(`https://humtrack.invalid/humcapture/timing/v1/${schema}.schema.json`);
    assert.equal(validate(await json(path.join(fixtureRoot, `${fixture}.json`))), true, JSON.stringify(validate.errors));
  }
  const cameraSchema = ajv.getSchema("https://humtrack.invalid/humcapture/timing/v1/camera-metadata.schema.json");
  assert.equal(cameraSchema(await json(path.join(fixtureRoot, "valid-android-camera-metadata.json"))), true, JSON.stringify(cameraSchema.errors));
  const imuMetadata = await json(path.join(fixtureRoot, "valid-imu-metadata.json")); assert.equal(validateImuMetadata(imuMetadata), true);
  const malformedTransform = structuredClone(imuMetadata); malformedTransform.camera_associations[0].imu_to_camera_rotation_row_major = [0, 0, 0, 0, 0, 0, 0, 0, 0];
  throwsCode(() => validateImuMetadata(malformedTransform), "TRANSFORM_INVALID");
  const withoutGyro = structuredClone(imuMetadata); withoutGyro.sensor_streams = withoutGyro.sensor_streams.filter((sensor) => sensor.sensor_kind !== "GYROSCOPE");
  assert.equal(validateImuMetadata(withoutGyro), true);
  assert.deepEqual(files, ["camera-metadata.schema.json", "imu-metadata.schema.json", "timing-metadata.schema.json"]);
});

test("HC-TIM-TEST-002 golden streams match immutable byte length, record count and SHA-256", async () => {
  const manifest = await json(path.join(fixtureRoot, "vector-manifest.json"));
  const decodedReference = await json(path.join(fixtureRoot, "decoded-reference.json"));
  assert.equal(manifest.vectors.length, 9); assert.equal(decodedReference.vectors.length, 6);
  for (const vector of manifest.vectors) {
    const bytes = await readFile(path.join(fixtureRoot, vector.file));
    assert.equal(bytes.length, vector.byte_length); assert.equal(sha256(bytes), vector.sha256);
    const decode = () => vector.kind === "FRAME" ? decodeFrameStream(bytes) : decodeImuStream(bytes);
    if (vector.expected === "ACCEPT") {
      const decoded = decode(); assert.equal(decoded.records.length, vector.record_count); assert.equal(decoded.header.finalized, true);
      const reference = decodedReference.vectors.find((item) => item.id === vector.id); assert.ok(reference); assert.equal(reference.records.length, vector.record_count);
    } else throwsCode(decode, vector.expected_code);
  }
});

test("HC-TIM-TEST-003 frame golden vector decodes and re-encodes byte exactly", async () => {
  const bytes = await readFile(path.join(fixtureRoot, "valid-frame-timestamps.bin"));
  const decoded = decodeFrameStream(bytes);
  assert.equal(decoded.header.streamId, frameStreamId); assert.equal(decoded.header.captureId, captureId);
  assert.equal(decoded.records[0].nativeTicks, 1_000_000_000n); assert.equal(decoded.records[2].videoFrameIndex, 2n);
  assert.equal(decoded.records[0].exposureDurationNs, 8_000_000n);
  assert.deepEqual(encodeFrameStream({ streamId: decoded.header.streamId, captureId: decoded.header.captureId, records: decoded.records }), bytes);
});

test("HC-TIM-TEST-004 IMU golden vector preserves independent sensors, units payload and optional-field presence", async () => {
  const bytes = await readFile(path.join(fixtureRoot, "valid-imu-samples.bin"));
  const decoded = decodeImuStream(bytes);
  assert.deepEqual(decoded.records.map((item) => item.sequence), [0n, 1n, 0n]);
  assert.deepEqual(decoded.records.map((item) => item.sensorKind), [SENSOR_KIND.ACCELEROMETER, SENSOR_KIND.ACCELEROMETER, SENSOR_KIND.GYROSCOPE]);
  assert.equal(decoded.records[0].w, undefined); assert.ok(Math.abs(decoded.records[0].z - 9.8) < 0.00001);
  assert.deepEqual(encodeImuStream({ streamId: decoded.header.streamId, captureId: decoded.header.captureId, records: decoded.records }), bytes);
});

test("HC-TIM-TEST-005 header and record corruption fail at the localized CRC boundary", async () => {
  assert.equal(crc32c(Buffer.from("123456789", "ascii")), 0xe3069283);
  const source = await readFile(path.join(fixtureRoot, "valid-frame-timestamps.bin"));
  const header = Buffer.from(source); header[20] ^= 1; throwsCode(() => decodeFrameStream(header), "HEADER_CRC_MISMATCH");
  const record = Buffer.from(source); record[TIMING_FORMAT.headerSize + 8] ^= 1; throwsCode(() => decodeFrameStream(record), "RECORD_CRC_MISMATCH");
  const reserved = Buffer.from(source); reserved.writeUInt32LE(1, TIMING_FORMAT.headerSize + 72); reserved.writeUInt32LE(crc32c(reserved.subarray(TIMING_FORMAT.headerSize, TIMING_FORMAT.headerSize + 92)), TIMING_FORMAT.headerSize + 92);
  throwsCode(() => decodeFrameStream(reserved), "FRAME_STATUS_UNSUPPORTED");
});

test("HC-TIM-TEST-006 only a non-finalized stream may recover complete records and discard a partial tail", () => {
  const bytes = encodeFrameStream({ streamId: frameStreamId, captureId, finalized: false, records: [basicFrame(0, 100), basicFrame(1, 200)] });
  const truncated = bytes.subarray(0, bytes.length - 7);
  throwsCode(() => decodeFrameStream(truncated), "STREAM_NOT_FINALIZED");
  const recovered = decodeFrameStream(truncated, { allowIncompleteTail: true });
  assert.equal(recovered.records.length, 1); assert.equal(recovered.discardedTailBytes, TIMING_FORMAT.frameRecordSize - 7);
  const finalized = Buffer.from(bytes); finalized.writeUInt32LE(TIMING_FORMAT.finalizedFlag, 16); rewriteHeaderCrc(finalized);
  throwsCode(() => decodeFrameStream(finalized.subarray(0, finalized.length - 7), { allowIncompleteTail: true }), "TRUNCATED_RECORD");
});

test("HC-TIM-TEST-007 unsupported versions and record layouts fail closed", async () => {
  const source = await readFile(path.join(fixtureRoot, "valid-frame-timestamps.bin"));
  const major = Buffer.from(source); major.writeUInt16LE(2, 8); rewriteHeaderCrc(major); throwsCode(() => decodeFrameStream(major), "MAJOR_VERSION_UNSUPPORTED");
  const minor = Buffer.from(source); minor.writeUInt16LE(1, 10); rewriteHeaderCrc(minor); throwsCode(() => decodeFrameStream(minor), "MINOR_VERSION_UNSUPPORTED");
  const layout = Buffer.from(source); layout.writeUInt16LE(88, 14); rewriteHeaderCrc(layout); throwsCode(() => decodeFrameStream(layout), "LAYOUT_UNSUPPORTED");
});

test("HC-TIM-TEST-008 measured cadence exposes variable cadence, sequence gaps, duplicates and timestamp regressions", async () => {
  const ordinary = [basicFrame(0, 0), basicFrame(1, 10_000_000), basicFrame(2, 20_000_000)];
  const metrics = analyzeSequenceAndCadence(ordinary, 1_000_000_000); assert.equal(metrics.observedHz, 100); assert.equal(metrics.gaps, 0);
  const variable = decodeFrameStream(await readFile(path.join(fixtureRoot, "valid-variable-cadence-frames.bin"))).records;
  const variableMetrics = analyzeSequenceAndCadence(variable, 1_000_000_000); assert.notEqual(variableMetrics.minIntervalSeconds, variableMetrics.maxIntervalSeconds);
  const segmented = decodeFrameStream(await readFile(path.join(fixtureRoot, "valid-segmented-restart-frames.bin"))).records;
  assert.deepEqual([...new Set(segmented.map((record) => record.segmentId))], [0, 1]);
  const anomalous = [basicFrame(0, 20), basicFrame(2, 30), basicFrame(2, 25)];
  const result = analyzeSequenceAndCadence(anomalous, 1_000_000_000); assert.equal(result.gaps, 1); assert.equal(result.duplicates, 1); assert.equal(result.regressions, 1);
});

test("HC-TIM-TEST-009 clock models bind known clocks, bounded uint64 ranges and explicit uncertainty", async () => {
  const metadata = await json(path.join(fixtureRoot, "valid-timing-metadata.json")); assert.equal(validateClockMappings(metadata), true);
  const unknown = structuredClone(metadata); unknown.clock_models[0].source_clock_id = "90000000-0000-4000-8000-000000000001";
  throwsCode(() => validateClockMappings(unknown), "CLOCK_MODEL_UNKNOWN_CLOCK");
  const reversed = structuredClone(metadata); reversed.clock_models[0].valid_from_ticks = "200"; reversed.clock_models[0].valid_through_ticks = "100";
  throwsCode(() => validateClockMappings(reversed), "CLOCK_MODEL_RANGE_INVALID");
  const overflow = structuredClone(metadata); overflow.clock_models[0].valid_through_ticks = "18446744073709551616";
  throwsCode(() => validateClockMappings(overflow), "UINT64_TEXT_INVALID");
});

test("HC-TIM-TEST-010 fixed-rate capability and unavailable lens data are schema-valid rather than universal failures", async () => {
  const ajv = new Ajv2020({ allErrors: true, strict: true }); addFormats(ajv);
  const schema = await json(path.join(schemaRoot, "camera-metadata.schema.json")); const validate = ajv.compile(schema);
  const camera = await json(path.join(fixtureRoot, "valid-camera-metadata.json"));
  assert.equal(camera.selected_mode.rate_control_class, "FIXED"); assert.equal(validate(camera), true, JSON.stringify(validate.errors));
  assert.ok(camera.unavailable_fields.some((item) => item.field === "lens.focal_length_mm" && item.reason === "NOT_EXPOSED"));
});

test("HC-TIM-TEST-011 protocol-required failures block/reject while preferred limitations degrade", () => {
  assert.deepEqual(evaluateProtocolConditions([{ level: "REQUIRED", outcome: "PASS" }, { level: "PREFERRED", outcome: "FAIL" }]), { preCapture: "READY_WITH_LIMITATIONS", postCapture: "DEGRADED_ACCEPTABLE" });
  assert.deepEqual(evaluateProtocolConditions([{ level: "REQUIRED", outcome: "FAIL" }]), { preCapture: "BLOCKED", postCapture: "REJECTED" });
  assert.deepEqual(evaluateProtocolConditions([{ level: "INFORMATIONAL", outcome: "FAIL" }]), { preCapture: "READY", postCapture: "CONFORMANT" });
});

test("HC-TIM-TEST-012 accepted source frames require unique complete video associations", async () => {
  const records = decodeFrameStream(await readFile(path.join(fixtureRoot, "valid-frame-timestamps.bin"))).records;
  assert.equal(validateFrameAssociation(records, 3), true);
  assert.equal(validateFrameAssociation(records, 4, [{ kind: "GENERATED_DUPLICATE", video_frame_index: "3", source_frame_sequence: "2", reason: "CFR encoder output" }]), true);
  const unmatched = structuredClone(records); unmatched[1].videoFrameIndex = undefined; unmatched[1].presence &= ~FRAME_PRESENCE.VIDEO_FRAME_INDEX;
  throwsCode(() => validateFrameAssociation(unmatched, 3), "ACCEPTED_FRAME_UNMATCHED");
  throwsCode(() => validateFrameAssociation(records, 2), "VIDEO_FRAME_COUNT_MISMATCH");

  const packageProfile = {
    source_kind: "ANDROID", interface_profiles: ["HC-IF-TIM-001@1.0.0"], artifacts: [
      { role: "FRAME_TIMESTAMPS", relative_path: "timing/frame-timestamps.bin", media_type: "application/vnd.humcapture.frame-timestamps", format_version: "1.0" },
      { role: "TIMING_METADATA", relative_path: "metadata/timing-metadata.json", media_type: "application/json", format_version: "1.0.0" },
      { role: "CAMERA_METADATA", relative_path: "metadata/camera-metadata.json", media_type: "application/json", format_version: "1.0.0" },
      { role: "IMU_SAMPLES", relative_path: "imu/imu-samples.bin", media_type: "application/vnd.humcapture.imu-samples", format_version: "1.0" },
      { role: "IMU_METADATA", relative_path: "imu/imu-metadata.json", media_type: "application/json", format_version: "1.0.0" }
    ]
  };
  assert.equal(validateTimingPackageProfile(packageProfile), true);
  const wrong = structuredClone(packageProfile); wrong.artifacts[0].media_type = "application/json";
  throwsCode(() => validateTimingPackageProfile(wrong), "TIMING_ARTIFACT_PROFILE_MISMATCH");
});

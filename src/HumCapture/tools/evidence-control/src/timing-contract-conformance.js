import { createHash } from "node:crypto";

export const TIMING_FORMAT = Object.freeze({
  major: 1,
  minor: 0,
  headerSize: 64,
  frameRecordSize: 96,
  imuRecordSize: 80,
  frameMagic: Buffer.from([0x48, 0x43, 0x54, 0x49, 0x4d, 0x45, 0x31, 0x00]),
  imuMagic: Buffer.from([0x48, 0x43, 0x49, 0x4d, 0x55, 0x31, 0x00, 0x00]),
  finalizedFlag: 0x00000001
});

export const FRAME_PRESENCE = Object.freeze({
  SOURCE_FRAME_NUMBER: 1 << 0,
  HOST_ARRIVAL_TICKS: 1 << 1,
  MAPPED_SESSION_TICKS: 1 << 2,
  ENCODED_PTS_TICKS: 1 << 3,
  VIDEO_FRAME_INDEX: 1 << 4,
  EXPOSURE_DURATION_NS: 1 << 5,
  ROLLING_SHUTTER_SKEW_NS: 1 << 6
});

export const IMU_PRESENCE = Object.freeze({
  HOST_ARRIVAL_TICKS: 1 << 0,
  MAPPED_SESSION_TICKS: 1 << 1,
  W_COMPONENT: 1 << 2,
  BATCH_ID: 1 << 3
});

export const FRAME_DISPOSITION = Object.freeze({
  ACCEPTED: 1,
  DROPPED_BEFORE_ENCODING: 2,
  REJECTED_CORRUPT: 3,
  ENCODER_FAILURE: 4,
  FINALIZATION_LOSS: 5,
  UNKNOWN: 255
});

export const SENSOR_KIND = Object.freeze({
  ACCELEROMETER: 1,
  GYROSCOPE: 2,
  ROTATION_VECTOR: 3,
  GRAVITY: 4,
  LINEAR_ACCELERATION: 5,
  MAGNETOMETER: 6
});

export class TimingConformanceError extends Error {
  constructor(code, message) {
    super(message);
    this.name = "TimingConformanceError";
    this.code = code;
  }
}

function reject(code, message) {
  throw new TimingConformanceError(code, message);
}

function assertU64(value, label) {
  if (typeof value !== "bigint" || value < 0n || value > 0xffffffffffffffffn) {
    reject("UINT64_INVALID", `${label} must be an unsigned 64-bit bigint.`);
  }
}

function assertI64(value, label) {
  if (typeof value !== "bigint" || value < -0x8000000000000000n || value > 0x7fffffffffffffffn) reject("INT64_INVALID", `${label} must be a signed 64-bit bigint.`);
}

function parseU64Text(value, label) {
  if (typeof value !== "string" || !/^(0|[1-9][0-9]{0,19})$/.test(value)) reject("UINT64_TEXT_INVALID", `${label} must be canonical unsigned decimal text.`);
  const parsed = BigInt(value);
  if (parsed > 0xffffffffffffffffn) reject("UINT64_TEXT_INVALID", `${label} exceeds uint64.`);
  return parsed;
}

function parseI64Text(value, label) {
  if (typeof value !== "string" || !/^(0|-?[1-9][0-9]{0,18})$/.test(value)) reject("INT64_TEXT_INVALID", `${label} must be canonical signed decimal text.`);
  const parsed = BigInt(value);
  if (parsed < -0x8000000000000000n || parsed > 0x7fffffffffffffffn) reject("INT64_TEXT_INVALID", `${label} exceeds int64.`);
  return parsed;
}

function uuidBytes(value) {
  if (typeof value !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)) {
    reject("UUID_INVALID", "UUID must use canonical hexadecimal text form.");
  }
  return Buffer.from(value.replaceAll("-", ""), "hex");
}

function uuidText(bytes) {
  const hex = bytes.toString("hex");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function crc32c(bytes) {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) crc = (crc >>> 1) ^ (0x82f63b78 & -(crc & 1));
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function encodeHeader({ kind, streamId, captureId, recordCount, finalized = true }) {
  assertU64(recordCount, "recordCount");
  const recordSize = kind === "FRAME" ? TIMING_FORMAT.frameRecordSize : TIMING_FORMAT.imuRecordSize;
  const magic = kind === "FRAME" ? TIMING_FORMAT.frameMagic : TIMING_FORMAT.imuMagic;
  const output = Buffer.alloc(TIMING_FORMAT.headerSize);
  magic.copy(output, 0);
  output.writeUInt16LE(TIMING_FORMAT.major, 8);
  output.writeUInt16LE(TIMING_FORMAT.minor, 10);
  output.writeUInt16LE(TIMING_FORMAT.headerSize, 12);
  output.writeUInt16LE(recordSize, 14);
  output.writeUInt32LE(finalized ? TIMING_FORMAT.finalizedFlag : 0, 16);
  uuidBytes(streamId).copy(output, 20);
  uuidBytes(captureId).copy(output, 36);
  output.writeBigUInt64LE(recordCount, 52);
  output.writeUInt32LE(crc32c(output.subarray(0, 60)), 60);
  return output;
}

function decodeHeader(bytes, expectedKind, { allowIncompleteTail = false } = {}) {
  if (!Buffer.isBuffer(bytes) || bytes.length < TIMING_FORMAT.headerSize) reject("HEADER_TRUNCATED", "Binary stream header is incomplete.");
  const expectedMagic = expectedKind === "FRAME" ? TIMING_FORMAT.frameMagic : TIMING_FORMAT.imuMagic;
  if (!bytes.subarray(0, 8).equals(expectedMagic)) reject("MAGIC_INVALID", `Not a ${expectedKind} timing stream.`);
  const major = bytes.readUInt16LE(8);
  const minor = bytes.readUInt16LE(10);
  if (major !== TIMING_FORMAT.major) reject("MAJOR_VERSION_UNSUPPORTED", `Unsupported timing binary major version ${major}.`);
  if (minor > TIMING_FORMAT.minor) reject("MINOR_VERSION_UNSUPPORTED", `Unsupported timing binary minor version ${minor}.`);
  const headerSize = bytes.readUInt16LE(12);
  const recordSize = bytes.readUInt16LE(14);
  const expectedRecordSize = expectedKind === "FRAME" ? TIMING_FORMAT.frameRecordSize : TIMING_FORMAT.imuRecordSize;
  if (headerSize !== TIMING_FORMAT.headerSize || recordSize !== expectedRecordSize) reject("LAYOUT_UNSUPPORTED", "Declared header or record size is unsupported.");
  if (bytes.readUInt32LE(60) !== crc32c(bytes.subarray(0, 60))) reject("HEADER_CRC_MISMATCH", "Header CRC32C does not match.");
  const flags = bytes.readUInt32LE(16);
  if ((flags & ~TIMING_FORMAT.finalizedFlag) !== 0) reject("HEADER_FLAGS_UNSUPPORTED", "Header contains unsupported flags.");
  const finalized = (flags & TIMING_FORMAT.finalizedFlag) !== 0;
  if (!finalized && !allowIncompleteTail) reject("STREAM_NOT_FINALIZED", "Stream is not marked finalized.");
  return {
    major, minor, headerSize, recordSize, flags, finalized,
    streamId: uuidText(bytes.subarray(20, 36)),
    captureId: uuidText(bytes.subarray(36, 52)),
    recordCount: bytes.readBigUInt64LE(52)
  };
}

function present(mask, bit) { return (mask & bit) !== 0; }
function optionalU64(record, offset, mask, bit) { return present(mask, bit) ? record.readBigUInt64LE(offset) : undefined; }
function optionalI64(record, offset, mask, bit) { return present(mask, bit) ? record.readBigInt64LE(offset) : undefined; }
function requireZeroWhenAbsent(record, offset, size, presence, bit, label) {
  if (!present(presence, bit) && record.subarray(offset, offset + size).some((byte) => byte !== 0)) reject("ABSENT_FIELD_NONZERO", `${label} has bytes but its presence bit is clear.`);
}

function encodeRecord(recordSize, writer) {
  const output = Buffer.alloc(recordSize);
  writer(output);
  output.writeUInt32LE(crc32c(output.subarray(0, recordSize - 4)), recordSize - 4);
  return output;
}

export function encodeFrameRecord(value) {
  for (const [key, required] of [["sequence", true], ["nativeTicks", true]]) if (required) assertU64(value[key], key);
  const presence = value.presence ?? 0;
  return encodeRecord(TIMING_FORMAT.frameRecordSize, (record) => {
    record.writeBigUInt64LE(value.sequence, 0);
    record.writeBigUInt64LE(value.nativeTicks, 8);
    for (const [field, offset, bit] of [
      ["sourceFrameNumber", 16, FRAME_PRESENCE.SOURCE_FRAME_NUMBER], ["hostArrivalTicks", 24, FRAME_PRESENCE.HOST_ARRIVAL_TICKS],
      ["mappedSessionTicks", 32, FRAME_PRESENCE.MAPPED_SESSION_TICKS],
      ["videoFrameIndex", 48, FRAME_PRESENCE.VIDEO_FRAME_INDEX], ["exposureDurationNs", 76, FRAME_PRESENCE.EXPOSURE_DURATION_NS],
      ["rollingShutterSkewNs", 84, FRAME_PRESENCE.ROLLING_SHUTTER_SKEW_NS]
    ]) {
      if (present(presence, bit)) { assertU64(value[field], field); record.writeBigUInt64LE(value[field], offset); }
    }
    if (present(presence, FRAME_PRESENCE.ENCODED_PTS_TICKS)) { assertI64(value.encodedPtsTicks, "encodedPtsTicks"); record.writeBigInt64LE(value.encodedPtsTicks, 40); }
    record.writeUInt32LE(value.clockModelId ?? 0, 56);
    record.writeUInt32LE(value.uncertaintyNs ?? 0, 60);
    record.writeUInt32LE(value.segmentId ?? 0, 64);
    record.writeUInt16LE(value.disposition ?? FRAME_DISPOSITION.ACCEPTED, 68);
    record.writeUInt16LE(presence, 70);
    record.writeUInt32LE(value.statusFlags ?? 0, 72);
  });
}

export function encodeImuRecord(value) {
  assertU64(value.sequence, "sequence"); assertU64(value.nativeTicks, "nativeTicks");
  const presence = value.presence ?? 0;
  const components = [value.x, value.y, value.z, value.w ?? 0];
  if (components.slice(0, 3).some((item) => !Number.isFinite(item)) || (present(presence, IMU_PRESENCE.W_COMPONENT) && !Number.isFinite(components[3]))) {
    reject("IMU_VALUE_INVALID", "Present IMU components must be finite.");
  }
  return encodeRecord(TIMING_FORMAT.imuRecordSize, (record) => {
    record.writeBigUInt64LE(value.sequence, 0); record.writeBigUInt64LE(value.nativeTicks, 8);
    if (present(presence, IMU_PRESENCE.HOST_ARRIVAL_TICKS)) { assertU64(value.hostArrivalTicks, "hostArrivalTicks"); record.writeBigUInt64LE(value.hostArrivalTicks, 16); }
    if (present(presence, IMU_PRESENCE.MAPPED_SESSION_TICKS)) { assertU64(value.mappedSessionTicks, "mappedSessionTicks"); record.writeBigUInt64LE(value.mappedSessionTicks, 24); }
    components.forEach((component, index) => record.writeFloatLE(component, 32 + index * 4));
    record.writeUInt32LE(value.clockModelId ?? 0, 48); record.writeUInt32LE(value.uncertaintyNs ?? 0, 52);
    record.writeUInt32LE(value.segmentId ?? 0, 56); record.writeUInt16LE(value.sensorStreamId, 60);
    record.writeUInt8(value.sensorKind, 62); record.writeInt8(value.accuracy ?? -1, 63);
    record.writeUInt16LE(presence, 64); record.writeUInt16LE(value.statusFlags ?? 0, 66);
    if (present(presence, IMU_PRESENCE.BATCH_ID)) record.writeUInt32LE(value.batchId, 68);
  });
}

function verifyRecordCrc(record) {
  const stored = record.readUInt32LE(record.length - 4);
  if (stored !== crc32c(record.subarray(0, record.length - 4))) reject("RECORD_CRC_MISMATCH", "Record CRC32C does not match.");
}

function decodeFrameRecord(record) {
  verifyRecordCrc(record);
  const presence = record.readUInt16LE(70);
  if ((presence & ~0x007f) !== 0) reject("FRAME_PRESENCE_UNSUPPORTED", "Frame record contains unsupported presence bits.");
  if (record.readUInt32LE(72) !== 0) reject("FRAME_STATUS_UNSUPPORTED", "Frame status bits are reserved in v1.0.");
  for (const [offset, size, bit, label] of [[16, 8, FRAME_PRESENCE.SOURCE_FRAME_NUMBER, "sourceFrameNumber"], [24, 8, FRAME_PRESENCE.HOST_ARRIVAL_TICKS, "hostArrivalTicks"], [32, 8, FRAME_PRESENCE.MAPPED_SESSION_TICKS, "mappedSessionTicks"], [40, 8, FRAME_PRESENCE.ENCODED_PTS_TICKS, "encodedPtsTicks"], [48, 8, FRAME_PRESENCE.VIDEO_FRAME_INDEX, "videoFrameIndex"], [76, 8, FRAME_PRESENCE.EXPOSURE_DURATION_NS, "exposureDurationNs"], [84, 8, FRAME_PRESENCE.ROLLING_SHUTTER_SKEW_NS, "rollingShutterSkewNs"]]) requireZeroWhenAbsent(record, offset, size, presence, bit, label);
  const disposition = record.readUInt16LE(68);
  if (![...Object.values(FRAME_DISPOSITION)].includes(disposition)) reject("FRAME_DISPOSITION_UNSUPPORTED", "Frame disposition is unsupported.");
  const clockModelId = record.readUInt32LE(56); const uncertaintyNs = record.readUInt32LE(60);
  if (!present(presence, FRAME_PRESENCE.MAPPED_SESSION_TICKS) && (clockModelId !== 0 || uncertaintyNs !== 0)) reject("FRAME_MAPPING_WITHOUT_TIME", "Clock model or uncertainty appears without mapped session time.");
  if (present(presence, FRAME_PRESENCE.MAPPED_SESSION_TICKS) && clockModelId === 0) reject("FRAME_MAPPING_MODEL_MISSING", "Mapped session time requires a clock model ID.");
  return {
    sequence: record.readBigUInt64LE(0), nativeTicks: record.readBigUInt64LE(8),
    sourceFrameNumber: optionalU64(record, 16, presence, FRAME_PRESENCE.SOURCE_FRAME_NUMBER),
    hostArrivalTicks: optionalU64(record, 24, presence, FRAME_PRESENCE.HOST_ARRIVAL_TICKS),
    mappedSessionTicks: optionalU64(record, 32, presence, FRAME_PRESENCE.MAPPED_SESSION_TICKS),
    encodedPtsTicks: optionalI64(record, 40, presence, FRAME_PRESENCE.ENCODED_PTS_TICKS),
    videoFrameIndex: optionalU64(record, 48, presence, FRAME_PRESENCE.VIDEO_FRAME_INDEX),
    clockModelId, uncertaintyNs, segmentId: record.readUInt32LE(64),
    disposition, presence, statusFlags: record.readUInt32LE(72),
    exposureDurationNs: optionalU64(record, 76, presence, FRAME_PRESENCE.EXPOSURE_DURATION_NS),
    rollingShutterSkewNs: optionalU64(record, 84, presence, FRAME_PRESENCE.ROLLING_SHUTTER_SKEW_NS)
  };
}

function decodeImuRecord(record) {
  verifyRecordCrc(record);
  const presence = record.readUInt16LE(64);
  if ((presence & ~0x000f) !== 0) reject("IMU_PRESENCE_UNSUPPORTED", "IMU record contains unsupported presence bits.");
  if (record.readUInt16LE(66) !== 0 || record.readUInt32LE(72) !== 0) reject("IMU_RESERVED_NONZERO", "IMU status/reserved bytes must be zero in v1.0.");
  for (const [offset, size, bit, label] of [[16, 8, IMU_PRESENCE.HOST_ARRIVAL_TICKS, "hostArrivalTicks"], [24, 8, IMU_PRESENCE.MAPPED_SESSION_TICKS, "mappedSessionTicks"], [44, 4, IMU_PRESENCE.W_COMPONENT, "w"], [68, 4, IMU_PRESENCE.BATCH_ID, "batchId"]]) requireZeroWhenAbsent(record, offset, size, presence, bit, label);
  const sensorStreamId = record.readUInt16LE(60); const sensorKind = record.readUInt8(62); const accuracy = record.readInt8(63);
  if (sensorStreamId === 0 || !Object.values(SENSOR_KIND).includes(sensorKind)) reject("IMU_SENSOR_IDENTITY_INVALID", "IMU sensor stream ID and kind must be known and nonzero.");
  if (accuracy < -1 || accuracy > 3) reject("IMU_ACCURACY_INVALID", "IMU accuracy must be -1 or an Android accuracy value 0-3.");
  const clockModelId = record.readUInt32LE(48); const uncertaintyNs = record.readUInt32LE(52);
  if (!present(presence, IMU_PRESENCE.MAPPED_SESSION_TICKS) && (clockModelId !== 0 || uncertaintyNs !== 0)) reject("IMU_MAPPING_WITHOUT_TIME", "Clock model or uncertainty appears without mapped session time.");
  if (present(presence, IMU_PRESENCE.MAPPED_SESSION_TICKS) && clockModelId === 0) reject("IMU_MAPPING_MODEL_MISSING", "Mapped session time requires a clock model ID.");
  const value = {
    sequence: record.readBigUInt64LE(0), nativeTicks: record.readBigUInt64LE(8),
    hostArrivalTicks: optionalU64(record, 16, presence, IMU_PRESENCE.HOST_ARRIVAL_TICKS),
    mappedSessionTicks: optionalU64(record, 24, presence, IMU_PRESENCE.MAPPED_SESSION_TICKS),
    x: record.readFloatLE(32), y: record.readFloatLE(36), z: record.readFloatLE(40),
    w: present(presence, IMU_PRESENCE.W_COMPONENT) ? record.readFloatLE(44) : undefined,
    clockModelId, uncertaintyNs, segmentId: record.readUInt32LE(56),
    sensorStreamId, sensorKind, accuracy,
    presence, statusFlags: record.readUInt16LE(66), batchId: present(presence, IMU_PRESENCE.BATCH_ID) ? record.readUInt32LE(68) : undefined
  };
  if ([value.x, value.y, value.z, ...(value.w === undefined ? [] : [value.w])].some((item) => !Number.isFinite(item))) reject("IMU_VALUE_INVALID", "Present IMU components must be finite.");
  return value;
}

function decodeStream(bytes, kind, options = {}) {
  const header = decodeHeader(bytes, kind, options);
  const payloadLength = bytes.length - header.headerSize;
  const fullRecordCount = Math.floor(payloadLength / header.recordSize);
  const tailBytes = payloadLength % header.recordSize;
  if (tailBytes && !(options.allowIncompleteTail && !header.finalized)) reject("TRUNCATED_RECORD", "Binary stream ends with a partial record.");
  if (header.finalized && header.recordCount !== BigInt(fullRecordCount)) reject("RECORD_COUNT_MISMATCH", "Finalized header record count differs from complete records.");
  const records = [];
  for (let index = 0; index < fullRecordCount; index += 1) {
    const start = header.headerSize + index * header.recordSize;
    const record = bytes.subarray(start, start + header.recordSize);
    records.push(kind === "FRAME" ? decodeFrameRecord(record) : decodeImuRecord(record));
  }
  return { header, records, discardedTailBytes: tailBytes };
}

export function encodeFrameStream({ streamId, captureId, records, finalized = true }) {
  return Buffer.concat([encodeHeader({ kind: "FRAME", streamId, captureId, recordCount: BigInt(records.length), finalized }), ...records.map(encodeFrameRecord)]);
}

export function encodeImuStream({ streamId, captureId, records, finalized = true }) {
  return Buffer.concat([encodeHeader({ kind: "IMU", streamId, captureId, recordCount: BigInt(records.length), finalized }), ...records.map(encodeImuRecord)]);
}

export function decodeFrameStream(bytes, options) { return decodeStream(bytes, "FRAME", options); }
export function decodeImuStream(bytes, options) { return decodeStream(bytes, "IMU", options); }

export function sha256(bytes) { return createHash("sha256").update(bytes).digest("hex"); }

export function analyzeSequenceAndCadence(records, ticksPerSecond) {
  if (!Number.isSafeInteger(ticksPerSecond) || ticksPerSecond <= 0) reject("TICK_RATE_INVALID", "ticksPerSecond must be a positive safe integer.");
  let gaps = 0; let duplicates = 0; let regressions = 0; const intervals = [];
  for (let index = 1; index < records.length; index += 1) {
    const sequenceDelta = records[index].sequence - records[index - 1].sequence;
    if (sequenceDelta === 0n) duplicates += 1; else if (sequenceDelta < 0n) regressions += 1; else if (sequenceDelta > 1n) gaps += Number(sequenceDelta - 1n);
    const tickDelta = records[index].nativeTicks - records[index - 1].nativeTicks;
    if (tickDelta < 0n) regressions += 1; else intervals.push(Number(tickDelta) / ticksPerSecond);
  }
  const ordered = intervals.toSorted((left, right) => left - right);
  const percentile = (fraction) => ordered.length ? ordered[Math.min(ordered.length - 1, Math.floor(fraction * (ordered.length - 1)))] : undefined;
  const duration = records.length > 1 ? Number(records.at(-1).nativeTicks - records[0].nativeTicks) / ticksPerSecond : 0;
  return { recordCount: records.length, gaps, duplicates, regressions, observedHz: duration > 0 ? (records.length - 1) / duration : undefined,
    minIntervalSeconds: ordered[0], medianIntervalSeconds: percentile(0.5), p95IntervalSeconds: percentile(0.95), maxIntervalSeconds: ordered.at(-1) };
}

export function validateClockMappings(metadata) {
  const clockIds = metadata.clocks.map((clock) => clock.clock_id); const clocks = new Set(clockIds);
  if (clocks.size !== clockIds.length) reject("CLOCK_DUPLICATE", "Clock IDs must be unique.");
  const clockById = new Map(metadata.clocks.map((clock) => [clock.clock_id, clock]));
  const modelIds = new Set();
  for (const model of metadata.clock_models) {
    if (modelIds.has(model.model_id)) reject("CLOCK_MODEL_DUPLICATE", "Clock model IDs must be unique.");
    modelIds.add(model.model_id);
    if (!clocks.has(model.source_clock_id) || !clocks.has(model.target_clock_id)) reject("CLOCK_MODEL_UNKNOWN_CLOCK", "Clock model references an unknown clock.");
    if (parseU64Text(model.valid_from_ticks, "valid_from_ticks") > parseU64Text(model.valid_through_ticks, "valid_through_ticks")) reject("CLOCK_MODEL_RANGE_INVALID", "Clock model validity range is reversed.");
    parseI64Text(model.offset_ticks, "offset_ticks");
    if (model.kind === "IDENTITY") {
      const source = clockById.get(model.source_clock_id); const target = clockById.get(model.target_clock_id);
      if (source.epoch_id !== target.epoch_id || source.ticks_per_second !== target.ticks_per_second || model.scale !== 1 || model.offset_ticks !== "0") reject("IDENTITY_MODEL_UNPROVEN", "Identity mapping requires matching epoch/frequency and exact unit scale/zero offset.");
    }
    if (model.quality === "VALID" && (!Number.isFinite(model.scale) || !Number.isSafeInteger(model.uncertainty_ns))) reject("CLOCK_MODEL_VALUE_INVALID", "A valid clock model needs finite scale and uncertainty.");
  }
  return true;
}


export function validateImuMetadata(metadata) {
  const streamIds = metadata.sensor_streams.map((sensor) => sensor.sensor_stream_id);
  if (new Set(streamIds).size !== streamIds.length) reject("SENSOR_STREAM_DUPLICATE", "Sensor stream numeric IDs must be unique.");
  for (const association of metadata.camera_associations ?? []) {
    if (association.valid_through_segment !== undefined && association.valid_through_segment < association.valid_from_segment) reject("TRANSFORM_RANGE_INVALID", "Camera-IMU association segment range is reversed.");
    const m = association.imu_to_camera_rotation_row_major;
    if (!Array.isArray(m) || m.length !== 9 || m.some((value) => !Number.isFinite(value))) reject("TRANSFORM_INVALID", "Camera-IMU rotation must contain nine finite values.");
    const row = (index) => m.slice(index * 3, index * 3 + 3);
    const dot = (left, right) => left.reduce((sum, value, index) => sum + value * right[index], 0);
    for (let index = 0; index < 3; index += 1) {
      if (Math.abs(dot(row(index), row(index)) - 1) > 1e-4) reject("TRANSFORM_INVALID", "Camera-IMU rotation rows must have unit length.");
      for (let other = index + 1; other < 3; other += 1) if (Math.abs(dot(row(index), row(other))) > 1e-4) reject("TRANSFORM_INVALID", "Camera-IMU rotation rows must be orthogonal.");
    }
    const determinant = m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
    if (Math.abs(determinant - 1) > 1e-4) reject("TRANSFORM_INVALID", "Camera-IMU rotation must be right-handed with determinant +1.");
  }
  return true;
}

export function validateFrameAssociation(records, decodedVideoFrameCount, transformations = []) {
  if (!Number.isSafeInteger(decodedVideoFrameCount) || decodedVideoFrameCount < 0) reject("VIDEO_FRAME_COUNT_INVALID", "Decoded video frame count must be a non-negative safe integer.");
  const accepted = records.filter((record) => record.disposition === FRAME_DISPOSITION.ACCEPTED);
  const indices = accepted.map((record) => record.videoFrameIndex);
  if (indices.some((value) => value === undefined)) reject("ACCEPTED_FRAME_UNMATCHED", "Every accepted source frame needs a presentation-order video index.");
  if (new Set(indices.map(String)).size !== indices.length) reject("VIDEO_FRAME_INDEX_DUPLICATE", "Accepted source frames cannot share a video index without an explicit generated-frame transformation.");
  for (const event of transformations) {
    if (event.kind !== "GENERATED_DUPLICATE" || typeof event.reason !== "string" || !event.reason.trim()) reject("VIDEO_TRANSFORM_UNSUPPORTED", "Video transformation must be a reasoned generated duplicate in v1.0.");
    if (!accepted.some((record) => record.sequence === parseU64Text(event.source_frame_sequence, "source_frame_sequence"))) reject("VIDEO_TRANSFORM_SOURCE_UNKNOWN", "Generated duplicate references a non-accepted source frame.");
  }
  const generated = transformations;
  const generatedIndices = generated.map((event) => parseU64Text(event.video_frame_index, "video_frame_index"));
  if (new Set([...indices, ...generatedIndices].map(String)).size !== indices.length + generatedIndices.length) reject("VIDEO_FRAME_INDEX_DUPLICATE", "Source and generated video indices must be unique.");
  if (accepted.length + generated.length !== decodedVideoFrameCount) reject("VIDEO_FRAME_COUNT_MISMATCH", "Accepted source frames plus declared generated frames differ from complete decoded video-frame count.");
  return true;
}

export function evaluateProtocolConditions(conditions) {
  for (const condition of conditions) {
    if (!["REQUIRED", "PREFERRED", "INFORMATIONAL"].includes(condition.level) || !["PASS", "FAIL", "NOT_ASSESSED", "NOT_APPLICABLE"].includes(condition.outcome)) reject("PROTOCOL_CONDITION_UNSUPPORTED", "Protocol condition level or outcome is unsupported.");
  }
  const failedRequired = conditions.filter((item) => item.level === "REQUIRED" && item.outcome !== "PASS");
  const failedPreferred = conditions.filter((item) => item.level === "PREFERRED" && item.outcome !== "PASS");
  return {
    preCapture: failedRequired.length ? "BLOCKED" : failedPreferred.length ? "READY_WITH_LIMITATIONS" : "READY",
    postCapture: failedRequired.length ? "REJECTED" : failedPreferred.length ? "DEGRADED_ACCEPTABLE" : "CONFORMANT"
  };
}

export function validateTimingPackageProfile(manifest) {
  if (!manifest.interface_profiles?.includes("HC-IF-TIM-001@1.0.0")) reject("TIMING_PROFILE_MISSING", "Package does not declare HC-IF-TIM-001@1.0.0.");
  const expected = [
    ["FRAME_TIMESTAMPS", "application/vnd.humcapture.frame-timestamps", "1.0", "timing/frame-timestamps.bin"],
    ["TIMING_METADATA", "application/json", "1.0.0", "metadata/timing-metadata.json"],
    ["CAMERA_METADATA", "application/json", "1.0.0", "metadata/camera-metadata.json"]
  ];
  if (manifest.source_kind === "ANDROID") expected.push(
    ["IMU_SAMPLES", "application/vnd.humcapture.imu-samples", "1.0", "imu/imu-samples.bin"],
    ["IMU_METADATA", "application/json", "1.0.0", "imu/imu-metadata.json"]
  );
  for (const [role, mediaType, version, relativePath] of expected) {
    const matches = manifest.artifacts.filter((candidate) => candidate.role === role);
    if (matches.length > 1) reject("TIMING_ARTIFACT_ROLE_DUPLICATE", `Timing profile has duplicate ${role} artifacts.`);
    const artifact = matches[0];
    if (!artifact) reject("TIMING_ARTIFACT_MISSING", `Required ${role} artifact is absent.`);
    if (artifact.media_type !== mediaType || artifact.format_version !== version || artifact.relative_path !== relativePath) reject("TIMING_ARTIFACT_PROFILE_MISMATCH", `${role} does not match its media type, version, or canonical path profile.`);
  }
  return true;
}

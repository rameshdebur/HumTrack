import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { crc32c, decodeFrameStream, decodeImuStream, encodeFrameStream, encodeImuStream, FRAME_DISPOSITION, FRAME_PRESENCE, IMU_PRESENCE, SENSOR_KIND, sha256 } from "./timing-contract-conformance.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "fixtures", "timing", "v1");
const captureId = "10000000-0000-4000-8000-000000000001";
const frameStreamId = "10000000-0000-4000-8000-000000000002";
const imuStreamId = "10000000-0000-4000-8000-000000000003";
const framePresence = Object.values(FRAME_PRESENCE).reduce((sum, bit) => sum | bit, 0);
const imuPresence = IMU_PRESENCE.HOST_ARRIVAL_TICKS | IMU_PRESENCE.MAPPED_SESSION_TICKS | IMU_PRESENCE.BATCH_ID;
const frames = [0n, 1n, 2n].map((sequence) => ({ sequence, nativeTicks: 1_000_000_000n + sequence * 33_333_333n,
  sourceFrameNumber: 100n + sequence, hostArrivalTicks: 2_000_000_000n + sequence * 33_400_000n,
  mappedSessionTicks: 5_000_000_000n + sequence * 33_333_340n, encodedPtsTicks: sequence * 3_000n,
  videoFrameIndex: sequence, clockModelId: 1, uncertaintyNs: 250_000, segmentId: 0,
  disposition: FRAME_DISPOSITION.ACCEPTED, presence: framePresence, exposureDurationNs: 8_000_000n, rollingShutterSkewNs: 12_000_000n }));
const imu = [
  { sequence: 0n, nativeTicks: 1_000_000_000n, hostArrivalTicks: 2_000_100_000n, mappedSessionTicks: 5_000_000_000n, x: 0.01, y: 0.02, z: 9.80, sensorStreamId: 1, sensorKind: SENSOR_KIND.ACCELEROMETER, accuracy: 3, presence: imuPresence, batchId: 1, clockModelId: 1, uncertaintyNs: 100_000, segmentId: 0 },
  { sequence: 1n, nativeTicks: 1_010_000_000n, hostArrivalTicks: 2_010_100_000n, mappedSessionTicks: 5_010_000_002n, x: 0.02, y: 0.03, z: 9.79, sensorStreamId: 1, sensorKind: SENSOR_KIND.ACCELEROMETER, accuracy: 3, presence: imuPresence, batchId: 1, clockModelId: 1, uncertaintyNs: 100_000, segmentId: 0 },
  { sequence: 0n, nativeTicks: 1_001_000_000n, hostArrivalTicks: 2_001_100_000n, mappedSessionTicks: 5_001_000_000n, x: 0.1, y: -0.2, z: 0.3, sensorStreamId: 2, sensorKind: SENSOR_KIND.GYROSCOPE, accuracy: 3, presence: imuPresence, batchId: 1, clockModelId: 1, uncertaintyNs: 100_000, segmentId: 0 }
];

const hostOnlyFrames = [0n, 1n].map((sequence) => ({ sequence, nativeTicks: 3_000_000n + sequence * 333_333n,
  hostArrivalTicks: 4_000_000n + sequence * 334_000n, videoFrameIndex: sequence, segmentId: 0,
  disposition: FRAME_DISPOSITION.ACCEPTED, presence: FRAME_PRESENCE.HOST_ARRIVAL_TICKS | FRAME_PRESENCE.VIDEO_FRAME_INDEX }));
const variableFrames = [0n, 1n, 2n, 3n].map((sequence) => ({ sequence,
  nativeTicks: [0n, 31_000_000n, 67_000_000n, 99_000_000n][Number(sequence)], videoFrameIndex: sequence,
  segmentId: 0, disposition: FRAME_DISPOSITION.ACCEPTED, presence: FRAME_PRESENCE.VIDEO_FRAME_INDEX }));
const segmentedFrames = [
  { sequence: 0n, nativeTicks: 100n, videoFrameIndex: 0n, segmentId: 0, disposition: FRAME_DISPOSITION.ACCEPTED, presence: FRAME_PRESENCE.VIDEO_FRAME_INDEX },
  { sequence: 1n, nativeTicks: 200n, videoFrameIndex: 1n, segmentId: 0, disposition: FRAME_DISPOSITION.ACCEPTED, presence: FRAME_PRESENCE.VIDEO_FRAME_INDEX },
  { sequence: 0n, nativeTicks: 50n, videoFrameIndex: 2n, segmentId: 1, disposition: FRAME_DISPOSITION.ACCEPTED, presence: FRAME_PRESENCE.VIDEO_FRAME_INDEX }
];
const noGyroImu = imu.filter((record) => record.sensorKind === SENSOR_KIND.ACCELEROMETER);

await mkdir(root, { recursive: true });
const frameBytes = encodeFrameStream({ streamId: frameStreamId, captureId, records: frames });
const imuBytes = encodeImuStream({ streamId: imuStreamId, captureId, records: imu });
const hostOnlyBytes = encodeFrameStream({ streamId: "10000000-0000-4000-8000-000000000004", captureId, records: hostOnlyFrames });
const variableBytes = encodeFrameStream({ streamId: "10000000-0000-4000-8000-000000000005", captureId, records: variableFrames });
const segmentedBytes = encodeFrameStream({ streamId: "10000000-0000-4000-8000-000000000006", captureId, records: segmentedFrames });
const noGyroBytes = encodeImuStream({ streamId: "10000000-0000-4000-8000-000000000007", captureId, records: noGyroImu });
const corruptRecordBytes = Buffer.from(frameBytes); corruptRecordBytes[64 + 8] ^= 1;
const truncatedBytes = frameBytes.subarray(0, frameBytes.length - 5);
const unsupportedMajorBytes = Buffer.from(frameBytes); unsupportedMajorBytes.writeUInt16LE(2, 8); unsupportedMajorBytes.writeUInt32LE(crc32c(unsupportedMajorBytes.subarray(0, 60)), 60);
const files = new Map([
  ["valid-frame-timestamps.bin", frameBytes], ["valid-imu-samples.bin", imuBytes],
  ["valid-host-timestamp-frames.bin", hostOnlyBytes], ["valid-variable-cadence-frames.bin", variableBytes],
  ["valid-segmented-restart-frames.bin", segmentedBytes], ["valid-imu-without-gyro.bin", noGyroBytes],
  ["invalid-corrupt-frame-record.bin", corruptRecordBytes], ["invalid-truncated-finalized-frame.bin", truncatedBytes],
  ["invalid-unsupported-major-frame.bin", unsupportedMajorBytes]
]);
for (const [name, bytes] of files) await writeFile(path.join(root, name), bytes);
const manifest = {
  schema_version: "1.0.0",
  vectors: [
    { id: "HC-TIM-VEC-001", file: "valid-frame-timestamps.bin", kind: "FRAME", byte_length: frameBytes.length, record_count: 3, sha256: sha256(frameBytes), expected: "ACCEPT" },
    { id: "HC-TIM-VEC-002", file: "valid-imu-samples.bin", kind: "IMU", byte_length: imuBytes.length, record_count: 3, sha256: sha256(imuBytes), expected: "ACCEPT" },
    { id: "HC-TIM-VEC-003", file: "valid-host-timestamp-frames.bin", kind: "FRAME", byte_length: hostOnlyBytes.length, record_count: 2, sha256: sha256(hostOnlyBytes), expected: "ACCEPT", note: "UVC host provenance only; metadata must not relabel it as sensor time" },
    { id: "HC-TIM-VEC-004", file: "valid-variable-cadence-frames.bin", kind: "FRAME", byte_length: variableBytes.length, record_count: 4, sha256: sha256(variableBytes), expected: "ACCEPT" },
    { id: "HC-TIM-VEC-005", file: "valid-segmented-restart-frames.bin", kind: "FRAME", byte_length: segmentedBytes.length, record_count: 3, sha256: sha256(segmentedBytes), expected: "ACCEPT", note: "Sequence reset is valid only in a new segment" },
    { id: "HC-TIM-VEC-006", file: "valid-imu-without-gyro.bin", kind: "IMU", byte_length: noGyroBytes.length, record_count: 2, sha256: sha256(noGyroBytes), expected: "ACCEPT", note: "Protocol decides whether missing gyroscope is blocking" },
    { id: "HC-TIM-VEC-007", file: "invalid-corrupt-frame-record.bin", kind: "FRAME", byte_length: corruptRecordBytes.length, sha256: sha256(corruptRecordBytes), expected: "REJECT", expected_code: "RECORD_CRC_MISMATCH" },
    { id: "HC-TIM-VEC-008", file: "invalid-truncated-finalized-frame.bin", kind: "FRAME", byte_length: truncatedBytes.length, sha256: sha256(truncatedBytes), expected: "REJECT", expected_code: "TRUNCATED_RECORD" },
    { id: "HC-TIM-VEC-009", file: "invalid-unsupported-major-frame.bin", kind: "FRAME", byte_length: unsupportedMajorBytes.length, sha256: sha256(unsupportedMajorBytes), expected: "REJECT", expected_code: "MAJOR_VERSION_UNSUPPORTED" }
  ]
};
await writeFile(path.join(root, "vector-manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
const decodedReference = {
  schema_version: "1.0.0",
  vectors: [
    ["HC-TIM-VEC-001", decodeFrameStream(frameBytes)], ["HC-TIM-VEC-002", decodeImuStream(imuBytes)],
    ["HC-TIM-VEC-003", decodeFrameStream(hostOnlyBytes)], ["HC-TIM-VEC-004", decodeFrameStream(variableBytes)],
    ["HC-TIM-VEC-005", decodeFrameStream(segmentedBytes)], ["HC-TIM-VEC-006", decodeImuStream(noGyroBytes)]
  ].map(([id, decoded]) => ({ id, header: decoded.header, records: decoded.records }))
};
await writeFile(path.join(root, "decoded-reference.json"), `${JSON.stringify(decodedReference, (_, value) => typeof value === "bigint" ? value.toString() : value, 2)}\n`);

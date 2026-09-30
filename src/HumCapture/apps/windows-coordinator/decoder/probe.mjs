// Engineering-only candidate qualification. Not a Coordinator package verifier.
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, lstatSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';

export function checkedProcess(executable, args, { timeout = 30000, maxBuffer = 1048576 } = {}) {
  if (!path.isAbsolute(executable)) throw new Error('Explicit executable path required');
  const result = spawnSync(executable, args, {
    shell: false, windowsHide: true, encoding: 'utf8', timeout, maxBuffer,
    stdio: ['ignore', 'pipe', 'pipe'], killSignal: 'SIGKILL'
  });
  return { status: result.status, error: result.error?.code ?? null,
    signal: result.signal, stdout: result.stdout ?? '', stderr: result.stderr ?? '' };
}

export function cleanExit(result) {
  return result.error === null && result.status === 0 && result.signal === null
    && result.stderr.trim() === '';
}

export function assertHash(bytes, expected) {
  const actual = createHash('sha256').update(bytes).digest('hex');
  if (!/^[a-f0-9]{64}$/.test(expected ?? '') || actual !== expected) {
    throw new Error('Candidate SHA-256 mismatch; execution forbidden');
  }
  return actual;
}

export function decodeArguments(media) {
  return ['-hide_banner', '-nostdin', '-v', 'error', '-xerror',
    '-abort_on', 'empty_output', '-hwaccel', 'none', '-threads', '1',
    '-protocol_whitelist', 'file', '-i', media, '-map', '0:v:0',
    '-an', '-sn', '-dn', '-fps_mode', 'passthrough', '-f', 'null', 'NUL'];
}

function main() {
  if (process.platform !== 'win32' || process.argv.length !== 4) {
    throw new Error('Usage on Windows: node probe.mjs ABS_ARCHIVE ABS_EXTRACTED_BIN_DIRECTORY');
  }
  const here = path.dirname(fileURLToPath(import.meta.url));
  const lock = JSON.parse(readFileSync(path.join(here, 'decoder-lock.json'), 'utf8'));
  const [archive, bin] = process.argv.slice(2);
  if (![archive, bin].every(path.isAbsolute)) throw new Error('Absolute input paths required');
  const archiveHash = assertHash(readFileSync(archive), lock.archive_sha256);
  if (lock.runtime_enabled !== false || lock.redistribution_approved !== false) {
    throw new Error('This probe does not authorize runtime or redistribution');
  }
  const executables = {};
  const observed = {};
  for (const name of ['ffmpeg.exe', 'ffprobe.exe']) {
    const executable = path.join(bin, name);
    if (!lstatSync(executable).isFile() || lstatSync(executable).isSymbolicLink()) {
      throw new Error('Ordinary executable required');
    }
    const sha256 = assertHash(readFileSync(executable), lock.executable_sha256?.[name]);
    const version = checkedProcess(executable, ['-version']);
    if (!cleanExit(version) || !version.stdout.startsWith(`${name.slice(0, -4)} version ${lock.version}-essentials_build-www.gyan.dev `)) {
      throw new Error('Candidate version check failed');
    }
    executables[name] = executable;
    observed[name] = { sha256, version: version.stdout };
  }
  const vault = path.resolve(here, '../../../evidence-vault/decoder-c14b');
  mkdirSync(vault, { recursive: true });
  const run = mkdtempSync(path.join(vault, 'probe-'));
  const video = path.join(run, 'synthetic.mp4');
  const ffmpeg = executables['ffmpeg.exe'];
  const ffprobe = executables['ffprobe.exe'];
  const results = [];
  function record(id, passed, evidence) { results.push({ id, passed, evidence }); }
  const generation = checkedProcess(ffmpeg, ['-hide_banner', '-nostdin', '-v', 'error',
    '-f', 'lavfi', '-i', 'testsrc2=size=160x120:rate=15', '-frames:v', '30',
    '-c:v', 'libx264', '-threads', '1', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', video]);
  record('HC-DEC-TEST-001', cleanExit(generation), generation);
  if (!cleanExit(generation)) throw new Error('Synthetic media generation failed');
  const original = readFileSync(video);
  const originalHash = createHash('sha256').update(original).digest('hex');
  const inspection = checkedProcess(ffprobe, ['-v', 'error', '-protocol_whitelist', 'file',
    '-count_frames', '-select_streams', 'v:0', '-show_entries',
    'stream=codec_name,width,height,nb_read_frames', '-of', 'json', video]);
  let streams = [];
  try { streams = JSON.parse(inspection.stdout).streams; } catch { /* Fails below. */ }
  record('HC-DEC-TEST-002', cleanExit(inspection) && streams?.length === 1
    && streams[0].nb_read_frames === '30' && streams[0].width === 160
    && streams[0].height === 120 && streams[0].codec_name === 'h264', inspection);
  const decoded = checkedProcess(ffmpeg, decodeArguments(video));
  record('HC-DEC-TEST-003', cleanExit(decoded), decoded);
  const truncated = path.join(run, 'truncated.mp4');
  writeFileSync(truncated, original.subarray(0, Math.floor(original.length / 2)), { flag: 'wx' });
  const truncation = checkedProcess(ffmpeg, decodeArguments(truncated));
  record('HC-DEC-TEST-004', truncation.error === null && truncation.status !== null
    && truncation.status !== 0 && truncation.stderr.trim() !== '', truncation);
  const corrupt = path.join(run, 'corrupt.mp4');
  writeFileSync(corrupt, Buffer.alloc(4096, 0xa5), { flag: 'wx' });
  const corruption = checkedProcess(ffmpeg, decodeArguments(corrupt));
  record('HC-DEC-TEST-005', corruption.error === null && corruption.status !== null
    && corruption.status !== 0 && corruption.stderr.trim() !== '', corruption);
  record('HC-DEC-TEST-006', assertHash(readFileSync(video), originalHash) === originalHash,
    { original_sha256: originalHash, byte_length: original.length });
  const variableVideo = path.join(run, 'synthetic-variable.mp4');
  const variableGeneration = checkedProcess(ffmpeg, ['-hide_banner', '-nostdin', '-v', 'error',
    '-f', 'lavfi', '-i', 'testsrc2=size=160x120:rate=15:duration=2',
    '-vf', 'select=not(eq(mod(n\\,3)\\,1))', '-fps_mode', 'passthrough',
    '-c:v', 'libx264', '-threads', '1', '-pix_fmt', 'yuv420p', variableVideo]);
  const variableInspection = checkedProcess(ffprobe, ['-v', 'error', '-protocol_whitelist', 'file',
    '-select_streams', 'v:0', '-show_frames', '-show_entries', 'frame=pts', '-of', 'json', variableVideo]);
  let timestamps = [];
  try { timestamps = JSON.parse(variableInspection.stdout).frames.map(f => f.pts); } catch { /* Fails below. */ }
  const deltas = timestamps.slice(1).map((value, i) => value - timestamps[i]);
  const variableDecode = checkedProcess(ffmpeg, decodeArguments(variableVideo));
  record('HC-DEC-TEST-007', cleanExit(variableGeneration) && cleanExit(variableInspection)
    && cleanExit(variableDecode) && timestamps.length === 20
    && deltas.every(delta => Number.isSafeInteger(delta) && delta > 0)
    && new Set(deltas).size === 2,
    { generation: variableGeneration, inspection: variableInspection, decode: variableDecode });
  const report = { schema_version: '1.0.0', kind: 'ENGINEERING_DECODER_PROBE',
    created_utc: new Date().toISOString(), archive_sha256: archiveHash,
    probe_source_sha256: createHash('sha256').update(readFileSync(fileURLToPath(import.meta.url))).digest('hex'),
    lock_sha256: createHash('sha256').update(readFileSync(path.join(here, 'decoder-lock.json'))).digest('hex'),
    environment: { platform: process.platform, architecture: process.arch, os_release: os.release(), node: process.version },
    binaries: observed, results, passed: results.every(r => r.passed),
    package_verification: 'NOT_ASSESSED', runtime_enabled: false,
    limitations: ['Synthetic fixed and variable H.264/MP4 only', 'No hardware or field qualification',
      'No package timing/IMU/metadata verification', 'No production worker cancellation qualification',
      'Embedded library, vulnerability and licence reviews remain open'] };
  const reportPath = path.join(run, 'probe-report.json');
  writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
  console.log(JSON.stringify({ report: reportPath, passed: report.passed, results: results.map(({ id, passed }) => ({ id, passed })) }));
  if (!report.passed) process.exitCode = 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();

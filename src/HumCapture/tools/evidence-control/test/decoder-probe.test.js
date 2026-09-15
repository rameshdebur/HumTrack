import test from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { assertHash, checkedProcess, cleanExit, decodeArguments } from '../../../apps/windows-coordinator/decoder/probe.mjs';

test('HC-DEC-GUARD-001 rejects absent or mismatched binary/archive hashes', () => {
  const bytes = Buffer.from('synthetic-not-an-executable');
  const hash = createHash('sha256').update(bytes).digest('hex');
  assert.equal(assertHash(bytes, hash), hash);
  for (const bad of [undefined, '', '0'.repeat(64), hash.toUpperCase()]) {
    assert.throws(() => assertHash(bytes, bad), /execution forbidden/);
  }
});

test('HC-DEC-GUARD-002 subprocess success requires clean exit and no diagnostics', () => {
  const success = { error: null, status: 0, signal: null, stderr: '' };
  assert.equal(cleanExit(success), true);
  for (const change of [{ status: 1 }, { status: null }, { error: 'ETIMEDOUT' },
    { signal: 'SIGKILL' }, { stderr: 'decoder corruption' }]) {
    assert.equal(cleanExit({ ...success, ...change }), false);
  }
  assert.throws(() => checkedProcess('ffmpeg.exe', []), /Explicit executable/);
});

test('HC-DEC-GUARD-003 probe subprocess timeout and output overflow cannot pass', () => {
  const timeout = checkedProcess(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { timeout: 200 });
  assert.equal(timeout.error, 'ETIMEDOUT');
  assert.equal(cleanExit(timeout), false);
  const overflow = checkedProcess(process.execPath, ['-e', 'process.stdout.write("x".repeat(100000))'], { maxBuffer: 1024 });
  assert.equal(overflow.error, 'ENOBUFS');
  assert.equal(cleanExit(overflow), false);
});

test('HC-DEC-GUARD-004 software decode uses passthrough and no nominal cadence override', () => {
  const args = decodeArguments('C:\\synthetic video.mp4');
  assert.equal(args[args.indexOf('-i') + 1], 'C:\\synthetic video.mp4');
  assert.equal(args[args.indexOf('-fps_mode') + 1], 'passthrough');
  assert.equal(args[args.indexOf('-hwaccel') + 1], 'none');
  assert.equal(args[args.indexOf('-protocol_whitelist') + 1], 'file');
  assert.ok(!args.includes('-r'));
});

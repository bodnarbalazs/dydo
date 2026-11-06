const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const suites = ['statechart-config-oracle.test.cjs', 'guard-windows-selection.test.cjs'];

function run(t, { ci = '', pwsh = 'missing', platform = 'linux' } = {}) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-pwsh-prerequisite-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const env = { ...process.env, CI: ci, PWSH: path.join(dir, 'missing-pwsh') };
  if (pwsh === 'broken') env.PWSH = process.execPath;
  delete env.NODE_TEST_CONTEXT;
  const preload = path.join(dir, 'pwsh.cjs');
  fs.writeFileSync(preload, `Object.defineProperty(process, 'platform', { value: ${JSON.stringify(platform)} });\n`);
  if (pwsh === 'script-failure') {
    // Replace only the external PowerShell boundary: startup succeeds, script execution fails.
    fs.appendFileSync(preload, `const cp = require('node:child_process');
const original = cp.spawnSync;
cp.spawnSync = (command, args, options) => command === process.env.PWSH
  ? { status: args.includes('-File') ? 42 : 0, stdout: '', stderr: 'fixture script defect' }
  : original(command, args, options);
`);
  }
  const result = spawnSync(process.execPath, ['--require', preload, '--test', '--test-reporter=tap',
    ...suites.map(file => path.join(__dirname, file))], { env, encoding: 'utf8', timeout: 30000 });
  assert.ifError(result.error);
  return result;
}

test('local missing PowerShell explicitly skips only the 24 dependent cases', t => {
  const result = run(t);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /# pass 3\b/);
  assert.match(result.stdout, /# fail 0\b/);
  assert.match(result.stdout, /# skipped 24\b/);
  assert.equal((result.stdout.match(/# SKIP PowerShell prerequisite unavailable: pwsh was not found/g) || []).length, 24);
});

test('CI rejects missing PowerShell instead of accepting skipped proof', t => {
  const result = run(t, { ci: 'true' });
  assert.notEqual(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /PowerShell prerequisite unavailable/);
  assert.match(result.stdout, /# skipped 0\b/);
});

test('Windows rejects missing PowerShell even outside CI', t => {
  const result = run(t, { platform: 'win32' });
  assert.notEqual(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /PowerShell prerequisite unavailable/);
  assert.match(result.stdout, /# skipped 0\b/);
});

test('available PowerShell still executes the existing assertions and exposes script defects', t => {
  const result = run(t, { ci: 'true', pwsh: 'script-failure' });
  assert.notEqual(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /fixture script defect/);
  assert.match(result.stdout, /# skipped 0\b/);
  assert.match(result.stdout, /# fail [1-9]/);
});

test('an executable that fails PowerShell startup is a failure, not a local skip', t => {
  const result = run(t, { pwsh: 'broken' });
  assert.notEqual(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /# skipped 0\b/);
});

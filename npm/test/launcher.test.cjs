const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { after, test } = require('node:test');
const { spawnSync } = require('node:child_process');

const packageRoot = path.resolve(__dirname, '..');
const fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-launcher-'));

after(() => fs.rmSync(fixtureRoot, { recursive: true, force: true }));

function prepareFixture() {
  fs.rmSync(fixtureRoot, { recursive: true, force: true });
  fs.mkdirSync(fixtureRoot, { recursive: true });
  fs.cpSync(path.join(packageRoot, 'bin'), path.join(fixtureRoot, 'bin'), { recursive: true });
  fs.cpSync(path.join(packageRoot, 'lib'), path.join(fixtureRoot, 'lib'), { recursive: true });
}

function runLauncher(...args) {
  return spawnSync(process.execPath, [path.join(fixtureRoot, 'bin', 'dydo.cjs'), ...args], {
    encoding: 'utf8',
    timeout: 5000
  });
}

function installNodeFixture() {
  const platform = require(path.join(fixtureRoot, 'lib', 'platform'));
  const paths = require(path.join(fixtureRoot, 'lib', 'paths'));
  const binaryPath = paths.getBinaryPath(platform.getPlatformInfo());
  fs.mkdirSync(path.dirname(binaryPath), { recursive: true });
  fs.copyFileSync(process.execPath, binaryPath);
}

test('reports a missing native binary with the documented exit status', () => {
  prepareFixture();

  const result = runLauncher('--version');

  assert.equal(result.status, 2);
  assert.match(result.stderr, /Error: dydo binary not found\./);
});

test('preserves Node 22 synchronous spawn failure for an invalid Windows executable', { skip: process.platform !== 'win32' }, () => {
  prepareFixture();
  const platform = require(path.join(fixtureRoot, 'lib', 'platform'));
  const paths = require(path.join(fixtureRoot, 'lib', 'paths'));
  const binaryPath = paths.getBinaryPath(platform.getPlatformInfo());
  fs.mkdirSync(path.dirname(binaryPath), { recursive: true });
  fs.writeFileSync(binaryPath, 'not a Windows executable');

  const result = runLauncher('measure');

  assert.equal(result.status, 1);
  assert.match(result.stderr, /spawn UNKNOWN/);
  assert.doesNotMatch(result.stderr, /Error launching dydo:/);
});

for (const status of [0, 23, 77]) {
  test(`forwards representative arguments and streams from a native exit ${status}`, () => {
    prepareFixture();
    installNodeFixture();
    const expectedArgs = ['--flag', 'two words', '', 'x"y', 'a&b', 'C:\\path with spaces\\file', 'é', '--'];
    const script = `process.stdout.write('OUT:${status}\\n'); process.stderr.write('ERR:${status}\\n'); process.stdout.write(JSON.stringify(process.argv.slice(2))); process.exit(Number(process.argv[1]));`;

    const result = runLauncher('-e', script, '--', String(status), ...expectedArgs);

    assert.equal(result.status, status);
    assert.equal(result.stdout, `OUT:${status}\n${JSON.stringify(expectedArgs)}`);
    assert.equal(result.stderr, `ERR:${status}\n`);
  });
}

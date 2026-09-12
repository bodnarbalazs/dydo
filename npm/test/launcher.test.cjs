const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { after, test } = require('node:test');
const childProcess = require('node:child_process');
const { spawnSync } = childProcess;

const packageRoot = path.resolve(__dirname, '..');
const launcherPath = require.resolve('../bin/dydo.cjs');
const platformModule = require('../lib/platform');
const pathsModule = require('../lib/paths');
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

function reloadCanonicalLauncher() {
  delete require.cache[launcherPath];
  require(launcherPath);
}

test('canonical launcher reports unsupported and missing-binary failures', (t) => {
  const errors = [];
  const exits = [];
  const stopped = new Error('exit');
  t.mock.method(console, 'error', (...args) => errors.push(args.join(' ')));
  t.mock.method(process, 'exit', (code) => { exits.push(code); throw stopped; });
  t.mock.method(platformModule, 'getPlatformInfo', () => ({ supported: false, error: 'unsupported fixture' }));

  assert.throws(reloadCanonicalLauncher, error => error === stopped);

  platformModule.getPlatformInfo = () => ({ supported: true, binaryName: 'dydo-fixture' });
  t.mock.method(pathsModule, 'getBinaryPath', () => 'missing-fixture');
  t.mock.method(pathsModule, 'isBinaryInstalled', () => false);
  assert.throws(reloadCanonicalLauncher, error => error === stopped);
  assert.deepEqual(exits, [2, 2]);
  assert.match(errors.join('\n'), /unsupported fixture/);
  assert.match(errors.join('\n'), /binary not found/);
});

test('canonical launcher preserves child error, code, and signal exits', (t) => {
  const children = [];
  const exits = [];
  const errors = [];
  const stopped = new Error('exit');
  let stopOnExit = false;
  t.mock.method(platformModule, 'getPlatformInfo', () => ({ supported: true, binaryName: 'dydo-fixture' }));
  t.mock.method(pathsModule, 'getBinaryPath', () => 'binary-fixture');
  t.mock.method(pathsModule, 'isBinaryInstalled', () => true);
  t.mock.method(childProcess, 'spawn', () => {
    const child = new (require('node:events').EventEmitter)();
    children.push(child);
    return child;
  });
  t.mock.method(process, 'exit', code => {
    exits.push(code);
    if (stopOnExit) throw stopped;
  });
  t.mock.method(console, 'error', (...args) => errors.push(args.join(' ')));

  reloadCanonicalLauncher();
  stopOnExit = true;
  assert.throws(() => children[0].emit('error', new Error('launch fixture')), error => error === stopped);
  stopOnExit = false;
  reloadCanonicalLauncher();
  stopOnExit = true;
  assert.throws(() => children[1].emit('exit', 23, null), error => error === stopped);
  stopOnExit = false;
  reloadCanonicalLauncher();
  stopOnExit = true;
  assert.throws(() => children[2].emit('exit', null, 'SIGTERM'), error => error === stopped);
  stopOnExit = false;
  reloadCanonicalLauncher();
  stopOnExit = true;
  assert.throws(() => children[3].emit('exit', null, null), error => error === stopped);

  assert.deepEqual(exits, [2, 23, 1, 0]);
  assert.match(errors.join('\n'), /launch fixture/);
});

test('canonical platform and path helpers retain supported, unsupported, and marker behavior', (t) => {
  let platform = 'win32';
  let architecture = 'x64';
  let markerExists = true;
  const writes = [];
  t.mock.method(os, 'platform', () => platform);
  t.mock.method(os, 'arch', () => architecture);
  t.mock.method(fs, 'existsSync', () => markerExists);
  t.mock.method(fs, 'readFileSync', () => ' 3.0.0-beta.3 \n');
  t.mock.method(fs, 'mkdirSync', (...args) => writes.push(['mkdir', ...args]));
  t.mock.method(fs, 'writeFileSync', (...args) => writes.push(['write', ...args]));

  const supported = platformModule.getPlatformInfo();
  assert.deepEqual(supported, {
    supported: true, platform: 'win32', arch: 'x64', rid: 'win-x64',
    binaryName: 'dydo.exe', archiveExt: '.zip'
  });
  assert.match(platformModule.getDownloadUrl('3.0.0', supported), /v3\.0\.0\/dydo-win-x64\.zip$/);
  platform = 'plan9';
  architecture = 'mips';
  assert.deepEqual(platformModule.getPlatformInfo(), {
    supported: false, platform: 'plan9', arch: 'mips',
    error: 'Unsupported platform: plan9-mips. Supported: win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64'
  });

  assert.equal(pathsModule.isBinaryInstalled(supported), true);
  assert.equal(pathsModule.getInstalledVersion(), '3.0.0-beta.3');
  markerExists = false;
  assert.equal(pathsModule.getInstalledVersion(), null);
  pathsModule.setInstalledVersion('4.0.0');
  assert.equal(writes.length, 2);
  assert.equal(writes[0][0], 'mkdir');
  assert.deepEqual(writes[1].slice(-1), ['4.0.0']);
});

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

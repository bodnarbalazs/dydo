const assert = require('node:assert/strict');
const { afterEach, test } = require('node:test');

const installPath = require.resolve('../install');
const platform = require('../lib/platform');
const download = require('../lib/download');
const paths = require('../lib/paths');

const originals = {
  getPlatformInfo: platform.getPlatformInfo,
  downloadBinary: download.downloadBinary,
  getInstalledVersion: paths.getInstalledVersion,
  isBinaryInstalled: paths.isBinaryInstalled,
  getBinaryDir: paths.getBinaryDir,
  setInstalledVersion: paths.setInstalledVersion,
  exit: process.exit,
  error: console.error,
  log: console.log
};

afterEach(function restoreMocks() {
  Object.assign(platform, { getPlatformInfo: originals.getPlatformInfo });
  Object.assign(download, { downloadBinary: originals.downloadBinary });
  Object.assign(paths, {
    getInstalledVersion: originals.getInstalledVersion,
    isBinaryInstalled: originals.isBinaryInstalled,
    getBinaryDir: originals.getBinaryDir,
    setInstalledVersion: originals.setInstalledVersion
  });
  process.exit = originals.exit;
  console.error = originals.error;
  console.log = originals.log;
  delete require.cache[installPath];
});

function loadInstall(overrides = {}) {
  Object.assign(platform, overrides.platform);
  Object.assign(download, overrides.download);
  Object.assign(paths, overrides.paths);
  delete require.cache[installPath];
  return require(installPath).install;
}

test('an unsupported platform warns without failing npm installation', async function unsupportedPlatformTest() {
  const errors = [];
  const exits = [];
  console.error = function recordError(...args) { errors.push(args.join(' ')); };
  process.exit = function exitProcess(code) {
    exits.push(code);
    throw new Error('process exited');
  };
  const install = loadInstall({
    platform: { getPlatformInfo() {
      return { supported: false, error: 'unsupported fixture' };
    } }
  });

  await assert.rejects(install(), /process exited/);

  assert.deepEqual(exits, [0]);
  assert.match(errors.join('\n'), /unsupported fixture/);
});

test('an installed current binary skips the download', async function currentBinaryTest() {
  let downloads = 0;
  const logs = [];
  console.log = function recordLog(...args) { logs.push(args.join(' ')); };
  const install = loadInstall({
    platform: { getPlatformInfo() {
      return { supported: true, binaryName: 'dydo' };
    } },
    download: { async downloadBinary() { downloads += 1; } },
    paths: {
      getInstalledVersion() { return '3.0.0-beta.3'; },
      isBinaryInstalled() { return true; }
    }
  });

  await install();

  assert.equal(downloads, 0);
  assert.match(logs.join('\n'), /already installed/);
});

test('a successful download records the installed version', async function successfulDownloadTest() {
  const calls = [];
  const install = loadInstall({
    platform: { getPlatformInfo() {
      return { supported: true, binaryName: 'dydo' };
    } },
    download: { async downloadBinary(...args) {
      calls.push(['download', ...args]);
    } },
    paths: {
      getInstalledVersion() { return null; },
      isBinaryInstalled() { return false; },
      getBinaryDir() { return 'native-fixture'; },
      setInstalledVersion(version) { calls.push(['version', version]); }
    }
  });

  await install();

  assert.deepEqual(calls, [
    ['download', '3.0.0-beta.3', { supported: true, binaryName: 'dydo' }, 'native-fixture'],
    ['version', '3.0.0-beta.3']
  ]);
});

test('a failed download reports recovery instructions and exits one', async function failedDownloadTest() {
  const errors = [];
  const exits = [];
  console.error = function recordError(...args) { errors.push(args.join(' ')); };
  process.exit = function recordExit(code) { exits.push(code); };
  const install = loadInstall({
    platform: { getPlatformInfo() {
      return { supported: true, binaryName: 'dydo' };
    } },
    download: { async downloadBinary() {
      throw new Error('fixture failure');
    } },
    paths: {
      getInstalledVersion() { return null; },
      isBinaryInstalled() { return false; },
      getBinaryDir() { return 'native-fixture'; }
    }
  });

  await install();

  assert.deepEqual(exits, [1]);
  assert.match(errors.join('\n'), /fixture failure/);
  assert.match(errors.join('\n'), /releases\/tag\/v3\.0\.0-beta\.3/);
});

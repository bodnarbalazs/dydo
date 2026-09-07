const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const packageRoot = path.resolve(__dirname, '..');
const manifest = JSON.parse(fs.readFileSync(path.join(packageRoot, 'package.json'), 'utf8'));
const forwarded = ['--flag', 'two words', 'árvíz 日本語', '', 'embedded"quote',
  '& | < > ; $() %PATH%', 'C:\\plain\\path', '--', 'literal'];

function withPackage(run) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-launcher-'));
  let cleanupConfirmed = true;
  try {
    fs.mkdirSync(path.join(root, 'bin'));
    fs.copyFileSync(path.join(packageRoot, manifest.bin.dydo), path.join(root, manifest.bin.dydo));
    fs.cpSync(path.join(packageRoot, 'lib'), path.join(root, 'lib'), { recursive: true });
    fs.copyFileSync(path.join(packageRoot, 'package.json'), path.join(root, 'package.json'));
    fs.mkdirSync(path.join(root, 'native'));
    const platform = require(path.join(root, 'lib/platform')).getPlatformInfo();
    assert.equal(platform.supported, true, platform.error);
    const binary = require(path.join(root, 'lib/paths')).getBinaryPath(platform);
    const fixture = path.join(root, 'fixture.cjs');
    fs.writeFileSync(fixture, "process.stdout.write(JSON.stringify(process.argv.slice(2)));\n" +
      "process.stderr.write('fixture stderr\\n');\nprocess.exit(Number(process.env.DYDO_FIXTURE_EXIT));\n");
    run({ root, binary, fixture, invoke(args, status) {
      const env = { ...process.env, DYDO_FIXTURE_EXIT: String(status) };
      const before = { ...env };
      const result = spawnSync(process.execPath, [path.join(root, manifest.bin.dydo), ...args], {
        env, encoding: 'utf8', timeout: 15000,
      });
      if (result.error || result.signal) cleanupConfirmed = false;
      assert.deepEqual(env, before);
      assert.equal(result.error, undefined);
      assert.equal(result.signal, null);
      return result;
    } });
  } finally {
    if (cleanupConfirmed) {
      const resolved = fs.realpathSync(root);
      assert.equal(path.dirname(resolved), fs.realpathSync(os.tmpdir()));
      assert.ok(path.basename(resolved).startsWith('dydo-launcher-'));
      fs.rmSync(resolved, { recursive: true, force: true });
    } else {
      process.stderr.write(`Unconfirmed child cleanup; retained fixture: ${root}\n`);
    }
  }
}

test('public dydo bin selects the packaged CommonJS launcher', () => {
  assert.equal(manifest.bin.dydo, 'bin/dydo.cjs');
  assert.ok(fs.statSync(path.join(packageRoot, manifest.bin.dydo)).isFile());
  assert.equal(fs.existsSync(path.join(packageRoot, 'bin/dydo')), false);
});

for (const status of [0, 23, 77]) {
  test(`launcher preserves argument boundaries, streams and exit ${status}`, () => {
    withPackage(({ binary, fixture, invoke }) => {
      fs.copyFileSync(process.execPath, binary);
      fs.chmodSync(binary, 0o755);
      const result = invoke([fixture, ...forwarded], status);
      assert.equal(result.status, status);
      assert.equal(result.stdout, JSON.stringify(forwarded));
      assert.equal(result.stderr, 'fixture stderr\n');
    });
  });
}

test('missing native binary retains the installation diagnostic and exit 2', () => {
  withPackage(({ invoke }) => {
    const result = invoke([], 0);
    assert.equal(result.status, 2, JSON.stringify(result));
    assert.equal(result.stdout, '');
    assert.equal(result.stderr, 'Error: dydo binary not found.\n' +
      'Try reinstalling: npm install -g dydo\n' +
      'Or install via .NET: dotnet tool install -g dydo\n');
  });
});

test('unlaunchable native binary retains the spawn diagnostic and exit 2', () => {
  withPackage(({ binary, invoke }) => {
    fs.writeFileSync(binary, 'not an executable\n', { mode: 0o600 });
    const result = invoke([], 0);
    assert.equal(result.status, 2, JSON.stringify(result));
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /^Error launching dydo: .+\n$/);
  });
});

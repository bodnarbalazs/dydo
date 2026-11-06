const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');

function pwshTestOptions() {
  const result = spawnSync(process.env.PWSH || 'pwsh',
    ['-NoProfile', '-NonInteractive', '-Command', 'exit 0'], { encoding: 'utf8', timeout: 10000 });
  if (result.error?.code === 'ENOENT') {
    const reason = 'PowerShell prerequisite unavailable: pwsh was not found';
    assert.ok(process.platform !== 'win32' && !process.env.CI, `${reason}; required on Windows and CI`);
    return { skip: reason };
  }
  assert.ifError(result.error);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  return {};
}

module.exports = { pwshTestOptions };

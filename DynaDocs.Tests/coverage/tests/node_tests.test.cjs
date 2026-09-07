const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { discover } = require('../node_tests.cjs');

test('discovery is recursive, exact, sorted, and excludes ordinary helpers', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-node-tests-'));
  fs.mkdirSync(path.join(root, 'nested'));
  for (const name of ['z.test.cjs', 'nested/a.test.mjs', 'nested/helper.cjs']) {
    fs.writeFileSync(path.join(root, name), '');
  }
  assert.deepEqual(discover(root).map(file => path.relative(root, file).replaceAll('\\', '/')),
    ['nested/a.test.mjs', 'z.test.cjs']);
  fs.rmSync(root, { recursive: true });
});

test('missing Node tests fail closed', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-node-tests-'));
  assert.throws(() => discover(root), /No maintained Node test files/);
  fs.rmSync(root, { recursive: true });
});

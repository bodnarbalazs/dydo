const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { discover, maintainedTests } = require('../node_tests.cjs');

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

test('maintained test command includes coverage and npm lifecycle suites', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-node-project-'));
  fs.mkdirSync(path.join(root, 'DynaDocs.Tests', 'coverage', 'tests'), { recursive: true });
  fs.mkdirSync(path.join(root, 'npm', 'test'), { recursive: true });
  fs.mkdirSync(path.join(root, 'vendor', 'copied-worktree'), { recursive: true });
  fs.writeFileSync(path.join(root, 'DynaDocs.Tests', 'coverage', 'tests', 'collector.test.cjs'), '');
  fs.writeFileSync(path.join(root, 'npm', 'test', 'lifecycle.test.cjs'), '');
  fs.writeFileSync(path.join(root, 'vendor', 'copied-worktree', 'foreign.test.cjs'), '');

  assert.deepEqual(maintainedTests(root).map(file => path.relative(root, file).replaceAll('\\', '/')), [
    'DynaDocs.Tests/coverage/tests/collector.test.cjs',
    'npm/test/lifecycle.test.cjs'
  ]);
  fs.rmSync(root, { recursive: true });
});

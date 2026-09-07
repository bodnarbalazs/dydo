const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

test('real c8 campaign keeps child-only, never-imported, and same-line callable identities', () => {
  const tools = path.resolve(__dirname, '..');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-js-coverage-'));
  fs.writeFileSync(path.join(root, 'common.cjs'),
    'const left=()=>1,right=()=>2; module.exports={left,right};\n');
  fs.writeFileSync(path.join(root, 'child.mjs'),
    'export function childOnly(){return 3}\nchildOnly();\n');
  fs.writeFileSync(path.join(root, 'never.cjs'),
    'module.exports=function never(){return 4};\n');
  fs.writeFileSync(path.join(root, 'ordinary.test.cjs'),
    "const {test}=require('node:test');const {spawnSync}=require('node:child_process');"
    + "const {left}=require('./common.cjs');test('ordinary',()=>{left();"
    + "const r=spawnSync(process.execPath,['child.mjs']);if(r.status)throw Error('child');});\n");
  const output = path.join(root, 'evidence');
  const env = { ...process.env }; delete env.NODE_V8_COVERAGE; delete env.NODE_TEST_CONTEXT;
  const result = spawnSync(process.execPath, [path.join(tools, 'javascript_coverage.cjs'),
    '--root', root, '--output', output,
    '--targets-json', '["common.cjs","child.mjs","never.cjs"]',
    '--command-json', '["{node}","--test","ordinary.test.cjs"]'],
    { cwd: root, encoding: 'utf8', env });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const joined = JSON.parse(fs.readFileSync(path.join(output, 'joined.json')));
  const modules = Object.fromEntries(joined.modules.map(row => [row.path, row]));
  assert.equal(modules['common.cjs'].methods.find(row => row.id.startsWith('left:')).covered, 1);
  assert.equal(modules['common.cjs'].methods.find(row => row.id.startsWith('right:')).covered, 0);
  assert.equal(modules['child.mjs'].methods[0].covered, 1);
  assert.equal(modules['never.cjs'].methods[0].covered, 0);
  assert.equal(new Set(modules['common.cjs'].methods.map(row => row.id)).size, 2);
  fs.rmSync(root, { recursive: true });
});

test('ambiguous coverage source identities fail closed', () => {
  const { canonicalRows } = require('../javascript_coverage.cjs');
  assert.throws(() => canonicalRows('C:/repo', {
    'C:/repo/a.cjs': { path: 'C:/repo/a.cjs' }, 'C:\\repo\\a.cjs': { path: 'C:\\repo\\a.cjs' },
  }), /ambiguous/i);
});

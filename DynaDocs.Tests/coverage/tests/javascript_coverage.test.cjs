const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { spawnSync } = require('node:child_process');

test('real c8 campaign keeps child-only, never-imported, and same-line callable identities', () => {
  const tools = path.resolve(__dirname, '..');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-js-coverage-'));
  fs.writeFileSync(path.join(root, 'common.cjs'),
    'function outer(items){return items.filter(item=>false).map(item=>item)}\n'
    + 'const left=()=>1,right=()=>2; module.exports={outer,left,right};\n');
  fs.writeFileSync(path.join(root, 'child.mjs'),
    'export function childOnly(){return 3}\nchildOnly();\n');
  fs.writeFileSync(path.join(root, 'never.cjs'),
    'module.exports=function never(){return 4};\n');
  fs.writeFileSync(path.join(root, 'ordinary.test.cjs'),
    "const {test}=require('node:test');const {spawnSync}=require('node:child_process');"
    + "const {outer,left}=require('./common.cjs');test('ordinary',()=>{outer([1]);left();"
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
  const outer = modules['common.cjs'].methods.find(row => row.id.startsWith('outer:'));
  const callbacks = modules['common.cjs'].methods.filter(row => row.id.startsWith('<anonymous>:1:'));
  assert.ok(outer.execution_count > 0);
  assert.equal(callbacks.length, 2);
  assert.equal(new Set(callbacks.map(row => row.id)).size, 2);
  assert.ok(callbacks.some(row => row.execution_count > 0));
  assert.ok(callbacks.some(row => row.execution_count === 0));
  assert.equal(modules['common.cjs'].methods.find(row => row.id.startsWith('left:')).covered, 1);
  assert.equal(modules['common.cjs'].methods.find(row => row.id.startsWith('right:')).covered, 0);
  assert.equal(modules['child.mjs'].methods[0].covered, 1);
  assert.equal(modules['never.cjs'].methods[0].covered, 0);
  const sameLine = modules['common.cjs'].methods.filter(row => row.line === 2);
  assert.equal(new Set(sameLine.map(row => row.id)).size, 2);
  fs.rmSync(root, { recursive: true });
});

test('ambiguous coverage source identities fail closed', () => {
  const { canonicalRows } = require('../javascript_coverage.cjs');
  assert.throws(() => canonicalRows('C:/repo', {
    'C:/repo/a.cjs': { path: 'C:/repo/a.cjs' }, 'C:\\repo\\a.cjs': { path: 'C:\\repo\\a.cjs' },
  }), /ambiguous/i);
});

test('duplicate native V8 script identities in one raw artifact fail closed', () => {
  const { rawFunctions } = require('../javascript_coverage.cjs');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-js-raw-'));
  const output = path.join(root, 'evidence');
  const target = path.join(root, 'target.cjs');
  fs.mkdirSync(path.join(output, 'tmp'), { recursive: true });
  fs.writeFileSync(target, 'module.exports=1;\n');
  const script = { url: pathToFileURL(target).href, functions: [] };
  fs.writeFileSync(path.join(output, 'tmp', 'duplicate.json'), JSON.stringify({ result: [script, script] }));
  assert.throws(() => rawFunctions(root, output, ['target.cjs']), /ambiguous/i);
  fs.rmSync(root, { recursive: true });
});

test('native V8 callable counts sum across inventories with an absent row contributing zero', () => {
  const { analyze } = require('../js_metrics.cjs');
  const { join } = require('../javascript_coverage.cjs');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd96-js-join-'));
  const output = path.join(root, 'evidence');
  const target = path.join(root, 'target.cjs');
  const source = 'function run(){}\n';
  const metric = analyze(source).methods[0];
  const script = count => ({ url: pathToFileURL(target).href, functions: count === null ? [] : [{ ranges: [{
    startOffset: metric.start, endOffset: metric.end, count,
  }] }] });
  fs.mkdirSync(path.join(output, 'tmp'), { recursive: true });
  fs.writeFileSync(target, source);
  fs.writeFileSync(path.join(output, 'coverage-final.json'), JSON.stringify({ [target]: {
    path: target, all: false, statementMap: {}, s: {}, branchMap: {}, b: {},
    fnMap: { 0: { name: 'run', loc: { start: { line: metric.line, column: metric.column },
      end: { line: metric.end_line, column: metric.end_column } } } }, f: { 0: 3 },
  } }));
  fs.writeFileSync(path.join(output, 'tmp', 'one.json'), JSON.stringify({ result: [script(1)] }));
  fs.writeFileSync(path.join(output, 'tmp', 'two.json'), JSON.stringify({ result: [script(2)] }));
  fs.writeFileSync(path.join(output, 'tmp', 'absent.json'), JSON.stringify({ result: [script(null)] }));
  assert.equal(join(root, output, ['target.cjs']).modules[0].methods[0].execution_count, 3);
  fs.rmSync(root, { recursive: true });
});

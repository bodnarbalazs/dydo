const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { spawnSync } = require('node:child_process');
const vm = require('node:vm');

test('TypeScript instrumentation preserves pragma-like literals and neutralizes only comments', () => {
  const { typescriptSource } = require('../javascript_coverage.cjs');
  const source = '/* istanbul ignore file */\n'
    + 'const plain: string = "istanbul ignore next";\n'
    + 'const template = `c8 ignore next ${"v8 ignore next"}`;\n'
    + 'const pattern = /istanbul ignore next/;\n'
    + '// istanbul ignore next\n'
    + 'function lineComment() { return plain; }\n'
    + '/* istanbul ignore next */\n'
    + 'function blockComment() { return template; }\n'
    + 'result = [lineComment(), blockComment(), pattern.source];\n';
  const compiled = typescriptSource(source, '/repo/literals.ts');
  const context = {};
  vm.runInNewContext(compiled.code, context);
  assert.deepEqual(Array.from(context.result),
    ['istanbul ignore next', 'c8 ignore next v8 ignore next', 'istanbul ignore next']);
  assert.equal(Object.keys(compiled.coverage.fnMap).length, 2);
  assert.deepEqual(Object.values(compiled.coverage.fnMap).map(row => row.loc.start.line), [6, 8]);
});

test('TypeScript source counters retain branches, erased types, empty and never-entered callables', () => {
  const { typescriptSource } = require('../javascript_coverage.cjs');
  const source = 'type Value = number;\nexport function choose(flag: boolean) { if (flag) return 1; return 0; }\n'
    + 'export const empty = () => {};\n';
  const first = typescriptSource(source, '/repo/source.ts');
  const second = typescriptSource(source, '/other/source.ts');
  for (const key of ['statementMap', 'fnMap', 'branchMap']) assert.deepEqual(first.coverage[key], second.coverage[key]);
  assert.equal(Object.keys(first.coverage.fnMap).length, 2);
  assert.equal(Object.keys(first.coverage.branchMap).length, 1);
  assert.ok(Object.values(first.coverage.s).every(value => value === 0));
  assert.ok(Object.values(first.coverage.statementMap).every(span => span.start.line > 1));
  const ignored = typescriptSource('/* istanbul ignore next */\nexport function kept() { return 1; }', '/repo/kept.ts');
  assert.equal(Object.keys(ignored.coverage.fnMap).length, 1);
});

test('TypeScript campaign records child-only execution and the source denominator keeps unloaded modules', t => {
  const { typescriptDenominator } = require('../javascript_coverage.cjs');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-ts-coverage-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.writeFileSync(path.join(root, 'child.ts'), 'export function native(value: number) { return value + 1; }\nnative(1);\n');
  fs.writeFileSync(path.join(root, 'unloaded.ts'), 'export function missing() { return 0; }\n');
  fs.writeFileSync(path.join(root, 'test.cjs'),
    "const {spawnSync}=require('node:child_process'); const r=spawnSync(process.execPath,['child.ts'],{encoding:'utf8'});"
    + "if(r.status || r.stderr) throw Error(r.stderr || 'child failed');");
  const output = path.join(root, 'reports');
  const config = path.join(root, 'config.json');
  const targets = ['child.ts', 'unloaded.ts'];
  fs.writeFileSync(config, JSON.stringify({ root, cwd: root, output, targets }));
  const run = spawnSync(process.execPath, [path.resolve(__dirname, '../javascript_coverage.cjs'),
    '--typescript-run', config, 'test.cjs'], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stdout + run.stderr);
  const reports = fs.readdirSync(output).map(file => JSON.parse(fs.readFileSync(path.join(output, file))));
  const child = reports.flatMap(report => Object.values(report)).find(row => row.path.endsWith('child.ts'));
  assert.ok(child);
  assert.equal(child.f['0'], 1);
  const source = typescriptDenominator(root, targets);
  assert.equal(source['unloaded.ts'].f['0'], 0);
  assert.deepEqual(child.statementMap, source['child.ts'].statementMap);
  assert.deepEqual(child.fnMap, source['child.ts'].fnMap);
  assert.deepEqual(child.branchMap, source['child.ts'].branchMap);
});

function interruptedCampaign(root, prefix) {
  const output = path.join(root, 'reports');
  fs.writeFileSync(path.join(root, 'kept.ts'), 'export function kept() { return 1; }\nkept();\n');
  fs.writeFileSync(path.join(root, 'interrupted.ts'), 'export function lost() { return 2; }\nlost();\n');
  fs.writeFileSync(path.join(root, 'unloaded.ts'), 'export function unloaded() { return 3; }\n');
  fs.writeFileSync(path.join(root, 'interrupted.cjs'),
    "const fs = require('node:fs'); const path = require('node:path');\n"
    + "import('./interrupted.ts').then(() => { const write = fs.writeFileSync;\n"
    + 'fs.writeFileSync = (file, ...args) => {\n'
    + `if (path.dirname(String(file)) !== ${JSON.stringify(output)}) return write(file, ...args);\n`
    + `write(file, ${JSON.stringify(prefix)}); process.kill(process.pid, 'SIGKILL');\n`
    + '}; });\n');
  fs.writeFileSync(path.join(root, 'suite.cjs'),
    "const assert = require('node:assert/strict'); const {spawnSync} = require('node:child_process');\n"
    + "assert.equal(spawnSync(process.execPath, ['kept.ts']).status, 0);\n"
    + "const interrupted = spawnSync(process.execPath, ['interrupted.cjs']);\n"
    + 'assert.notEqual(interrupted.status, 0);\n');
  const config = path.join(root, 'config.json');
  fs.writeFileSync(config, JSON.stringify({ root, cwd: root, output,
    targets: ['kept.ts', 'interrupted.ts', 'unloaded.ts'] }));
  const run = spawnSync(process.execPath, [path.resolve(__dirname, '../javascript_coverage.cjs'),
    '--typescript-run', config, 'suite.cjs'], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stdout + run.stderr);
  return output;
}

for (const prefix of ['', '{']) {
  test(`TypeScript interrupted exit publishes no ${prefix ? 'partial' : 'empty'} JSON report`, t => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-ts-interrupted-'));
    t.after(() => fs.rmSync(root, { recursive: true, force: true }));
    const output = interruptedCampaign(root, prefix);
    const files = fs.readdirSync(output);
    const reports = files.filter(file => file.endsWith('.json'))
      .map(file => JSON.parse(fs.readFileSync(path.join(output, file), 'utf8')));
    const rows = reports.flatMap(report => Object.values(report));
    assert.deepEqual(rows.map(row => path.basename(row.path)), ['kept.ts']);
    assert.equal(rows[0].f['0'], 1);
    const pending = files.filter(file => file.endsWith('.pending'));
    assert.equal(pending.length, 1);
    assert.equal(fs.readFileSync(path.join(output, pending[0]), 'utf8'), prefix);
  });
}

test('missing c8 is an unavailable measurement with exit 2', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd171-js-no-c8-'));
  const tools = path.join(root, 'tools');
  fs.mkdirSync(tools);
  fs.copyFileSync(path.resolve(__dirname, '..', 'javascript_coverage.cjs'),
    path.join(tools, 'javascript_coverage.cjs'));
  fs.copyFileSync(path.resolve(__dirname, '..', 'js_metrics.cjs'),
    path.join(tools, 'js_metrics.cjs'));
  fs.copyFileSync(path.resolve(__dirname, '..', 'package.json'), path.join(tools, 'package.json'));
  fs.writeFileSync(path.join(root, 'target.cjs'), 'module.exports=1;\n');
  const result = spawnSync(process.execPath, [path.join(tools, 'javascript_coverage.cjs'),
    '--root', root, '--output', path.join(root, 'evidence'),
    '--targets-json', '["target.cjs"]', '--command-json', '["{node}","target.cjs"]'],
    { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 2, result.stdout + result.stderr);
  assert.match(result.stderr, /c8.*unavailable/i);
  fs.rmSync(root, { recursive: true });
});

test('available c8 preserves a failing suite exit 1', () => {
  const tools = path.resolve(__dirname, '..');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd171-js-failing-suite-'));
  fs.writeFileSync(path.join(root, 'target.cjs'), 'module.exports=1;\n');
  const result = spawnSync(process.execPath, [path.join(tools, 'javascript_coverage.cjs'),
    '--root', root, '--output', path.join(root, 'evidence'),
    '--targets-json', '["target.cjs"]',
    '--command-json', '["{node}","-e","process.exit(1)"]'],
    { cwd: root, encoding: 'utf8' });
  assert.equal(result.status, 1, result.stdout + result.stderr);
  assert.doesNotMatch(result.stderr, /unavailable/i);
  fs.rmSync(root, { recursive: true });
});

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

test('real c8 campaign joins every object, class and standalone callable shape', () => {
  const tools = path.resolve(__dirname, '..');
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dyd216-js-shapes-'));
  fs.writeFileSync(path.join(root, 'shapes.cjs'),
    'const items = [1, 2];\n'
    + 'const probe = {\n'
    + '  mark: () => items.length,\n'
    + '  shorthand() { return items.map(item => item); },\n'
    + '  plain: function () { return items; },\n'
    + '  get size() { return items.length; },\n'
    + '};\n'
    + 'class Gauge {\n'
    + '  static level() { return 1; }\n'
    + '  read() { return 2; }\n'
    + '}\n'
    + 'function top() { return probe.shorthand(); }\n'
    + 'module.exports = { probe, Gauge, top };\n');
  fs.writeFileSync(path.join(root, 'shapes.test.cjs'),
    "const {test}=require('node:test');const {probe,Gauge,top}=require('./shapes.cjs');"
    + "test('shapes',()=>{probe.mark();void probe.size;Gauge.level();top();});\n");
  const output = path.join(root, 'evidence');
  const env = { ...process.env }; delete env.NODE_V8_COVERAGE; delete env.NODE_TEST_CONTEXT;
  const result = spawnSync(process.execPath, [path.join(tools, 'javascript_coverage.cjs'),
    '--root', root, '--output', output, '--targets-json', '["shapes.cjs"]',
    '--command-json', '["{node}","--test","shapes.test.cjs"]'],
    { cwd: root, encoding: 'utf8', env });
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const joined = JSON.parse(fs.readFileSync(path.join(output, 'joined.json')));
  const covered = Object.fromEntries(joined.modules[0].methods.map(row => [row.id, row.covered]));
  assert.deepEqual(covered, {
    'mark:3:2': 1, 'shorthand:4:2': 1, '<anonymous>:4:33': 1, 'plain:5:2': 0,
    'size:6:2': 1, 'level:9:2': 1, 'read:10:2': 0, 'top:12:0': 1,
  });
  fs.rmSync(root, { recursive: true });
});

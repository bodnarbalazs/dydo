const { test } = require('node:test');
const assert = require('node:assert/strict');
const { analyze } = require('../js_metrics.cjs');

test('logical module metrics retain top-level branches without borrowing nested bodies', () => {
  const { moduleMetrics } = require('../js_metrics.cjs');
  const source = 'import fs from "node:fs"; export const value = flag ? 1 : 2; export function unused(x) { if(x) return 1; return 0; }';
  assert.deepEqual(moduleMetrics(source, 'module'), { cc: 2, cognitive: 1 });
  assert.deepEqual(moduleMetrics('#!/usr/bin/env node\nif (flag) run(); else stop();', 'commonjs'), { cc: 2, cognitive: 2 });
  assert.deepEqual(moduleMetrics('export default function(){if(flag)return 1;}\nconst value=flag?1:2;', 'module'), { cc: 2, cognitive: 1 });
});

test('flat switches use official cognitive score without a cyclomatic cap', () => {
  const rows = analyze('function choose(x) { switch(x) { case 1: return 1; case 2: return 2; case 3: return 3; default: return 0; } }').methods;
  assert.equal(rows.length, 1);
  assert.equal(rows[0].cognitive, 1);
  assert.equal(rows[0].cc, 4);
});

test('callbacks carry their own control flow and distinct positions', () => {
  const rows = analyze('function outer(x) { if(x) return () => { if(x) return 1; return 0; }; return () => 0; }').methods;
  assert.deepEqual(rows.map(row => row.cognitive), [1, 1, 0]);
  assert.equal(new Set(rows.map(row => row.id)).size, 3);
});

test('constructor parameter exemption does not hide ordinary method parameters', () => {
  const rows = analyze('class A { constructor(a,b,c,d,e,f,g,h) {} method(a,b,c,d,e,f,g,h) {} }').methods;
  assert.deepEqual(rows.map(row => row.parameters), [8, 8]);
  assert.deepEqual(rows.map(row => row.constructor), [true, false]);
});

test('dead parameter and nested ternary are real diagnostics', () => {
  const result = analyze('function f(unused) { return a ? (b ? 1 : 2) : 3; }');
  assert.ok(result.diagnostics.some(row => row.ruleId === 'no-unused-vars'));
  assert.ok(result.diagnostics.some(row => row.ruleId === 'no-nested-ternary'));
});

test('parse failures cannot become empty successful metrics', () => {
  assert.throws(() => analyze('function ( {'), /parse/i);
});

test('object shorthand methods retain their own diagnostic source locations', () => {
  const rows = analyze('function outer() { return { create() { return { visit(node) { if(node) return 1; return 0; } }; } }; }').methods;
  assert.deepEqual(rows.map(row => row.cc), [1, 1, 2]);
  assert.deepEqual(rows.map(row => row.cognitive), [0, 0, 1]);
});

test('explicit ESM parsing preserves exported function metrics', () => {
  const rows = analyze('export function choose(flag) { return flag ? 1 : 2; }', 'module').methods;
  assert.equal(rows[0].cc, 2);
  assert.equal(rows[0].cognitive, 1);
});

test('object arrow properties record at their key like ESLint diagnostics do', () => {
  const source = 'function make(items) { return { mark: () => items.length, pick: (x) => (x ? 1 : 2), stop: () => items.pop() }; }';
  const rows = analyze(source).methods;
  assert.deepEqual(rows.map(row => row.id),
    ['make:1:0', ...['mark', 'pick', 'stop'].map(key => `${key}:1:${source.indexOf(`${key}:`)}`)]);
  assert.deepEqual(rows.map(row => row.cc), [1, 1, 2, 1]);
  assert.deepEqual(rows.map(row => row.cognitive), [0, 0, 1, 0]);
});

test('a function used as a computed key is refused, never silently mis-joined', () => {
  // ESLint reports both the key function and the property value at the same head location, so no
  // metric can be attributed to either; refusing beats guessing which function a message belongs to.
  assert.throws(() => analyze('function outer() { return { [function key() { return 1; }]: (x) => (x ? 1 : 2) }; }'), /ambiguous/);
});

test('a class field holding a function is refused for an unjoinable location, not a duplicate', () => {
  // ESLint scores a field initializer twice — once at the field head, once as 'Class field
  // initializer' — against our single function row, so neither message can be attributed.
  for (const source of ['class A { f = (x) => (x ? 1 : 2); }', 'class A { g = function gg(x) { return x ? 1 : 2; }; }']) {
    assert.throws(() => analyze(source), /^Error: Missing or ambiguous JavaScript metric location 1:11$/);
  }
});

test('member rows carry the diagnostic head and the runtime literal as separate anchors', () => {
  // V8 measures the function literal, ESLint reports at the member head, and the two only coincide
  // for a concise method that is not static; javascript_coverage.cjs joins on start/end. The last
  // member pins that V8 skips one leading `static` token even when `static` is the method's name.
  const source = 'const o = { mark: () => 1, shorthand() { return 2; }, get size() { return 3; } };\n'
    + 'class K { static level() { return 4; } read() { return 5; } static() { return 6; } }';
  const rows = analyze(source).methods;
  assert.deepEqual(rows.map(row => [row.id, source.slice(row.start, row.end)]), [
    ['mark:1:12', '() => 1'],
    ['shorthand:1:27', 'shorthand() { return 2; }'],
    ['size:1:54', 'get size() { return 3; }'],
    ['level:2:10', 'level() { return 4; }'],
    ['read:2:39', 'read() { return 5; }'],
    ['static:2:60', '() { return 6; }'],
  ]);
});

test('TypeScript and TSX use the official metrics and score a component apart from its callbacks', () => {
  const source = 'export function Panel({ items }: { items: string[] }) {\n'
    + '  if (items.length === 0) return null;\n'
    + '  return <ul onClick={(event: MouseEvent) => { if (event.shiftKey) { for (const x of items) { if (x) log(x); } } }}>{items}</ul>;\n'
    + '}\n';
  const rows = analyze(source, 'tsx').methods;
  assert.deepEqual(rows.map(row => [row.id, row.cc, row.cognitive, row.parameters]),
    [['Panel:1:7', 2, 1, 1], ['<anonymous>:3:22', 4, 6, 1]]);
  assert.deepEqual(analyze('export const pick = (flag: boolean): number => (flag ? 1 : 2);', 'typescript')
    .methods.map(row => [row.cc, row.cognitive]), [[2, 1]]);
});

test('TypeScript leaves unused bindings to its compiler yet still refuses nested ternaries', () => {
  const result = analyze('type Unused = number;\nexport function f(a: number, unused: Unused) { return a ? (a > 1 ? 1 : 2) : 3; }', 'typescript');
  assert.deepEqual(result.diagnostics.map(row => row.ruleId), ['no-nested-ternary']);
});

test('a module the compiler erases entirely has no runtime statement', () => {
  assert.equal(analyze('import type { A } from "./a";\nexport interface B { a: A }\nexport type C = B;\ntype D = C;', 'typescript').runtime, false);
  assert.equal(analyze('export interface B { a: number }\nexport const b: B = { a: 1 };', 'typescript').runtime, true);
  assert.equal(analyze('module.exports = {};').runtime, true);
});

test('a TypeScript class field initializer is its own callable row; a function field is still refused', () => {
  const rows = analyze('class Tree {\n  private readonly parent = new Map<string, string>();\n  depth = flag ? 1 : 2;\n}', 'typescript').methods;
  assert.deepEqual(rows.map(row => [row.id, row.cc, row.cognitive, row.parameters, row.constructor]),
    [['parent:2:28', 1, 0, 0, false], ['depth:3:10', 2, 0, 0, false]]);
  assert.throws(() => analyze('class A { f = (x: number) => (x ? 1 : 2); }', 'typescript'), /Missing or ambiguous/);
  assert.throws(() => analyze('class A { parent = new Map(); }'), /Missing or ambiguous/);
});

test('TypeScript module metrics score top-level statements without borrowing function bodies', () => {
  const { moduleMetrics } = require('../js_metrics.cjs');
  const source = 'import type { A } from "./a";\nexport interface B { a: A }\nconst root = find();\n'
    + 'if (root === null) throw new Error("none");\nexport function f(x: number) { if (x) return 1; return 0; }';
  assert.deepEqual(moduleMetrics(source, 'typescript'), { cc: 2, cognitive: 1 });
  assert.throws(() => analyze('const a = 1;', 'python'), /Unknown JavaScript source type/);
});

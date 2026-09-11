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

'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { fileURLToPath } = require('node:url');
const { spawnSync } = require('node:child_process');
const { analyze, moduleMetrics } = require('./js_metrics.cjs');

function canonical(root, candidate) {
  const value = candidate.startsWith('file:') ? fileURLToPath(candidate) : candidate;
  return path.resolve(root, value).replaceAll('\\', '/').toLowerCase();
}

function canonicalRows(root, report) {
  const rows = new Map();
  for (const [key, value] of Object.entries(report)) {
    const identity = canonical(root, key);
    if (rows.has(identity) || canonical(root, value.path) !== identity) {
      throw new Error(`Missing or ambiguous c8 source identity: ${key}`);
    }
    rows.set(identity, value);
  }
  return rows;
}

function samePoint(left, right) {
  return left.line === right.line && left.column === right.column;
}

function rawFunctions(root, output, targets) {
  const allowed = new Set(targets.map(target => canonical(root, target)));
  const scripts = new Map();
  const tmp = path.join(output, 'tmp');
  for (const name of fs.readdirSync(tmp).filter(name => name.endsWith('.json')).sort()) {
    const data = JSON.parse(fs.readFileSync(path.join(tmp, name), 'utf8'));
    if (!Array.isArray(data.result)) throw new Error('Invalid native V8 coverage result');
    const seen = new Set();
    for (const script of data.result) {
      if (!script.url || !script.url.startsWith('file:')) continue;
      const identity = canonical(root, script.url);
      if (!allowed.has(identity)) continue;
      if (!Array.isArray(script.functions)) throw new Error('Invalid native V8 function inventory');
      if (seen.has(identity)) throw new Error(`Ambiguous native V8 source identity: ${script.url}`);
      seen.add(identity);
      const rows = scripts.get(identity) || [];
      rows.push(script.functions);
      scripts.set(identity, rows);
    }
  }
  return scripts;
}

function nativeCallableCount(inventories, metric, relative) {
  let count = 0;
  let found = false;
  for (const inventory of inventories) {
    const raw = inventory.filter(fn => Array.isArray(fn.ranges) && fn.ranges.length
      && fn.ranges[0].startOffset === metric.start && fn.ranges[0].endOffset === metric.end);
    if (raw.length > 1) throw new Error(`Ambiguous native V8 callable join: ${relative}:${metric.id}`);
    if (raw.length === 1) {
      const value = raw[0].ranges[0].count;
      if (!Number.isInteger(value) || value < 0) throw new Error('Invalid native V8 function counter');
      count += value;
      found = true;
    }
  }
  if (count === 0 && !found) throw new Error(`Missing native V8 callable join: ${relative}:${metric.id}`);
  return count;
}

function istanbulCallableCounts(coverage, metric, relative) {
  const functions = Object.entries(coverage.fnMap || {}).filter(([, fn]) => samePoint(fn.loc.start, { line: metric.line, column: metric.column })
    && samePoint(fn.loc.end, { line: metric.end_line, column: metric.end_column }));
  const branches = Object.entries(coverage.branchMap || {}).filter(([, branch]) => branch.locations?.length === 1
    && samePoint(branch.locations[0].start, { line: metric.line, column: metric.column })
    && samePoint(branch.locations[0].end, { line: metric.end_line, column: metric.end_column }));
  if (functions.length > 1 || branches.length > 1) throw new Error(`Ambiguous Istanbul callable join: ${relative}:${metric.id}`);
  return [functions.length ? coverage.f[functions[0][0]] : null,
    branches.length ? coverage.b[branches[0][0]][0] : null];
}

function validateIstanbulCounts(counts, nativeCount, relative, metric) {
  for (const value of counts.filter(value => value !== null)) {
    if (!Number.isInteger(value) || value < 0 || value !== nativeCount) {
      throw new Error(`Disagreeing Istanbul callable join: ${relative}:${metric.id}`);
    }
  }
}

function callableExecutionCount(relative, identity, coverage, native, metric) {
  if (coverage.all === true) {
    const functions = Object.entries(coverage.fnMap || {});
    if (functions.length !== 1 || functions[0][1].name !== '(empty-report)'
        || Object.values(coverage.f).some(value => value !== 0)) {
      throw new Error(`Invalid c8 --all zero-hit witness: ${relative}`);
    }
    return 0;
  }
  const nativeCount = nativeCallableCount(native.get(identity) || [], metric, relative);
  validateIstanbulCounts(istanbulCallableCounts(coverage, metric, relative),
    nativeCount, relative, metric);
  return nativeCount;
}

function measuredCallable(relative, identity, coverage, native, metric) {
  const count = callableExecutionCount(relative, identity, coverage, native, metric);
  return { ...metric, covered: Number(count > 0), total: 1, execution_count: count };
}

function join(root, output, targets) {
  root = path.resolve(root); output = path.resolve(output);
  const report = JSON.parse(fs.readFileSync(path.join(output, 'coverage-final.json'), 'utf8'));
  const rows = canonicalRows(root, report);
  const native = rawFunctions(root, output, targets);
  const modules = [];
  const expected = new Set(targets.map(target => canonical(root, target)));
  if (rows.size !== expected.size || [...expected].some(identity => !rows.has(identity))) {
    throw new Error('Incomplete c8 target source inventory');
  }
  for (const relative of targets) {
    const absolute = path.resolve(root, relative);
    const identity = canonical(root, absolute);
    const source = fs.readFileSync(absolute, 'utf8');
    const coverage = rows.get(identity);
    const kind = relative.endsWith('.mjs') ? 'module' : 'commonjs';
    const metrics = analyze(source, kind).methods;
    const methods = metrics.map(metric => measuredCallable(relative, identity, coverage, native, metric));
    const lines = {};
    for (const [key, span] of Object.entries(coverage.statementMap || {})) {
      const hits = coverage.s[key];
      if (!Number.isInteger(hits) || hits < 0) throw new Error('Invalid Istanbul statement counter');
      const line = String(span.start.line);
      lines[line] = Math.max(lines[line] || 0, hits);
    }
    const branches = {};
    for (const [key, branch] of Object.entries(coverage.branchMap || {})) {
      const hits = coverage.b[key];
      if (!Array.isArray(hits) || hits.length !== branch.locations.length) throw new Error('Invalid Istanbul branch inventory');
      hits.forEach((count, index) => {
        if (!Number.isInteger(count) || count < 0) throw new Error('Invalid Istanbul branch counter');
        branches[`${key}:${index}`] = count;
      });
    }
    modules.push({ path: relative.replaceAll('\\', '/'), language: 'javascript',
      executable: Object.keys(lines).length > 0, declarative: Object.keys(lines).length === 0,
      lines, branches, methods, module: moduleMetrics(source, kind) });
  }
  return { schema: 1, modules };
}

function campaign(root, output, targets, command) {
  root = path.resolve(root); output = path.resolve(output);
  if (fs.existsSync(output) || !targets.length || !command.length) throw new Error('Invalid JavaScript campaign inputs');
  for (const target of targets) {
    const absolute = path.resolve(root, target);
    if (!absolute.startsWith(root + path.sep) || !fs.statSync(absolute).isFile()) throw new Error(`Invalid JavaScript target: ${target}`);
  }
  const c8 = path.join(__dirname, 'node_modules', 'c8', 'bin', 'c8.js');
  const argv = [c8, '--all', '--exclude-after-remap=false', '--reports-dir', output,
    '--temp-directory', path.join(output, 'tmp'), '--reporter=json', '--reporter=lcov'];
  for (const target of targets) argv.push('--include', target.replaceAll('\\', '/'));
  argv.push('--', ...command.map(item => item === '{node}' ? process.execPath : item));
  const result = spawnSync(process.execPath, argv, { cwd: root, stdio: 'inherit', env: process.env });
  if (result.error) throw result.error;
  if (result.status !== 0) return Number.isInteger(result.status) ? result.status : 130;
  fs.writeFileSync(path.join(output, 'joined.json'), JSON.stringify(join(root, output, targets), null, 2) + '\n');
  return 0;
}

function main(args = process.argv.slice(2)) {
  const values = {};
  for (let index = 0; index < args.length; index += 2) values[args[index]] = args[index + 1];
  if (!values['--root'] || !values['--output'] || !values['--targets-json'] || !values['--command-json']) return 2;
  return campaign(values['--root'], values['--output'], JSON.parse(values['--targets-json']), JSON.parse(values['--command-json']));
}

module.exports = { canonicalRows, rawFunctions, join };
if (require.main === module) {
  try { process.exitCode = main(); } catch (error) { console.error(error.message); process.exitCode = 2; }
}

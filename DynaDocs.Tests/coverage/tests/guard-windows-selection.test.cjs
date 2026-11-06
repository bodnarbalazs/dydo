const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');
const { parse } = createRequire(require.resolve('knip'))('yaml');
const { pwshTestOptions } = require('../pwsh_prerequisite.cjs');

const pwsh = pwshTestOptions();

const root = path.resolve(__dirname, '../../..');
const job = parse(fs.readFileSync(path.join(root, '.github/workflows/ci.yml'), 'utf8')).jobs['guard-windows'];
const run = job.steps.find(step => step.id === 'guard-tests');
const guard = job.steps.find(step => step.name === 'Require every selected guard test to pass');
const bodyClass = 'DynaDocs.Tests.Commands.GuardPrReadyBodyFileTests';
const method = 'LinkedDirectoryLeavingTheTree_IsNull';
const required = `${bodyClass}.${method}`;
const prReadyClass = 'DynaDocs.Tests.Integration.GuardPrReadyTests';
const classes = [
  'DynaDocs.Tests.Services.DangerousPatternCultureTests',
  'DynaDocs.Tests.Services.DangerousPatternOracleTests',
  'DynaDocs.Tests.Services.DangerousPatternBudgetTests',
  'DynaDocs.Tests.Services.BashCommandAnalyzerTests',
  'DynaDocs.Tests.Integration.GuardSecurityTests',
  'DynaDocs.Tests.Integration.GuardMergeRouteTests',
  prReadyClass,
];

function execute(t, script, report, outcome = 'success') {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-guard-selection-'));
  t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
  const trx = path.join(dir, 'guard.trx');
  if (report !== undefined) fs.writeFileSync(trx, report);
  const file = path.join(dir, 'step.ps1');
  fs.writeFileSync(file, `$ErrorActionPreference = 'Stop'\n${script}`);
  const result = spawnSync(process.env.PWSH || 'pwsh', ['-NoProfile', '-NonInteractive', '-File', file], {
    cwd: root, encoding: 'utf8', timeout: 30000,
    env: { ...process.env, ...job.env, GUARD_TRX: trx, TEST_OUTCOME: outcome },
  });
  assert.ifError(result.error);
  return result;
}

test('Windows selects the exact directory-link regression alongside the seven guard classes', pwsh, t => {
  assert.equal(job['runs-on'], 'windows-latest');
  for (const item of [job, run, guard]) assert.equal(item['continue-on-error'], undefined);
  assert.equal(job.if, undefined);
  assert.equal(run.if, undefined);
  assert.equal(guard.if, '${{ !cancelled() }}');
  assert.equal(guard.env.TEST_OUTCOME, '${{ steps.guard-tests.outcome }}');
  assert.ok(job.steps.indexOf(run) < job.steps.indexOf(guard));
  // Capture the arguments at the external dotnet boundary; execute the workflow's filter logic.
  const result = execute(t, `function dotnet { 'ARGV:' + (ConvertTo-Json -Compress -InputObject @($args)) }\n${run.run}`);
  assert.equal(result.status, 0, result.stdout + result.stderr);
  const args = JSON.parse(result.stdout.split(/\r?\n/).find(line => line.startsWith('ARGV:')).slice(5));
  assert.equal(args[0], 'test');
  assert.deepEqual(args[args.indexOf('--filter') + 1].split('|'), [
    ...classes.map(name => `FullyQualifiedName~${name}.`), `FullyQualifiedName=${required}`,
  ]);
});

function report(rows, total = rows.length) {
  const definitions = rows.map((row, i) => `<UnitTest id="${i}"><TestMethod className="${row.className}" name="${row.name}" /></UnitTest>`).join('');
  const results = rows.map((row, i) => `<UnitTestResult testId="${i}" testName="${row.display || `${row.className}.${row.name}`}" outcome="${row.outcome || 'Passed'}" />`).join('');
  const passed = rows.filter(row => !row.outcome || row.outcome === 'Passed').length;
  return `<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><TestDefinitions>${definitions}</TestDefinitions><Results>${results}</Results><ResultSummary><Counters total="${total}" passed="${passed}" /></ResultSummary></TestRun>`;
}

for (const fault of [null, 'missing', 'sibling', 'wrong-class', 'skipped', 'failed', 'missing-class', 'skipped-class', 'missing-pr-ready-class', 'skipped-pr-ready-class', 'counters', 'test-outcome', 'no-trx']) {
  test(`Windows TRX check ${fault ? `rejects ${fault}` : 'accepts every required test passing'}`, pwsh, t => {
    const rows = classes.map(className => ({ className, name: 'ExistingTest' }));
    if (fault === 'missing-pr-ready-class') rows.splice(classes.indexOf(prReadyClass), 1);
    if (fault === 'skipped-pr-ready-class') rows[classes.indexOf(prReadyClass)].outcome = 'NotExecuted';
    const exact = { className: bodyClass, name: method };
    if (fault !== 'missing') rows.push(exact);
    if (fault === 'sibling') { exact.name += '_Sibling'; exact.display = required; }
    if (fault === 'wrong-class') { exact.className += 'Other'; exact.display = required; }
    if (fault === 'skipped') exact.outcome = 'NotExecuted';
    if (fault === 'failed') exact.outcome = 'Failed';
    if (fault === 'missing-class') rows.shift();
    if (fault === 'skipped-class') rows[0].outcome = 'NotExecuted';
    const xml = fault === 'no-trx' ? undefined : report(rows, rows.length + (fault === 'counters' ? 1 : 0));
    const result = execute(t, guard.run, xml, fault === 'test-outcome' ? 'failure' : 'success');
    assert.equal(result.status, fault ? 1 : 0, result.stdout + result.stderr);
    if (['missing', 'sibling', 'wrong-class'].includes(fault)) {
      assert.ok(result.stdout.includes(`${required} produced zero results`), result.stdout);
    }
  });
}

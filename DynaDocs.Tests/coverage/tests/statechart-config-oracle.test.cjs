const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createRequire } = require('node:module');
const { spawnSync } = require('node:child_process');
const { createHash } = require('node:crypto');
const { parse } = createRequire(require.resolve('knip'))('yaml');

const root = path.resolve(__dirname, '../../..');
const workflow = parse(fs.readFileSync(path.join(root, '.github/workflows/ci.yml'), 'utf8'));

function oracleSteps() {
  const job = workflow.jobs['statechart-config-oracle'];
  assert.ok(job, 'CI must run the config oracle in a required job');
  assert.equal(job.if, undefined);
  assert.equal(job['continue-on-error'], undefined);
  assert.equal(job['runs-on'], 'ubuntu-24.04');
  const steps = ['oracle', 'require-oracle'].map(id => job.steps.find(step => step.id === id));
  for (const step of steps) {
    assert.ok(step, 'The oracle run and its non-skip guard must both exist');
    assert.equal(step.if, undefined);
    assert.equal(step['continue-on-error'], undefined);
  }
  assert.ok(job.steps.indexOf(steps[0]) < job.steps.indexOf(steps[1]));
  const provision = job.steps.find(step => step.id === 'statechart');
  assert.ok(provision && !provision.if && !provision['continue-on-error']);
  assert.ok(job.steps.indexOf(provision) < job.steps.indexOf(steps[0]));
  assert.equal(steps[0].env.JAVA, '${{ steps.statechart.outputs.java }}');
  assert.equal(steps[0].env.TLA2TOOLS, '${{ steps.statechart.outputs.tla2tools }}');
  return steps;
}

test('CI runs the oracle and requires its result on every push/PR trigger', () => {
  oracleSteps();
  assert.ok(Object.hasOwn(workflow.on, 'pull_request'));
  assert.equal(workflow.on.pull_request, null);
  assert.equal(workflow.on.push.paths, undefined);
  assert.equal(workflow.on.push['paths-ignore'], undefined);
});

// Execute the workflow's commands on real node:test files. In particular, an empty file must
// not turn into the synthetic passing file-test that node --test would report.
for (const [name, source, expected] of [
  ['passed', "import { test } from 'node:test'; test('oracle', () => {});", 0],
  ['skipped', "import { test } from 'node:test'; test('oracle', { skip: 'no JDK' }, () => {});", 1],
  ['empty', '', 1],
  ['failed', "import { test } from 'node:test'; test('oracle', () => { throw Error('mismatch'); });", 1],
]) {
  test(`the CI commands ${expected ? 'reject' : 'accept'} a ${name} oracle`, { skip: process.platform !== 'linux' }, t => {
    const [run, guard] = oracleSteps();
    const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-config-oracle-ci-'));
    t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
    const cwd = path.join(dir, run['working-directory']);
    fs.mkdirSync(path.join(cwd, 'test'), { recursive: true });
    fs.writeFileSync(path.join(cwd, 'test/config-fuzz.test.ts'), source);
    const script = path.join(dir, 'run.sh');
    fs.writeFileSync(script, run.run.replaceAll('${{ runner.temp }}', dir));
    const env = { ...process.env };
    delete env.NODE_TEST_CONTEXT; // A nested test runner otherwise suppresses the requested reporters.
    const result = spawnSync('bash', ['--noprofile', '--norc', '-e', '-o', 'pipefail', script],
      { cwd, env, encoding: 'utf8' });
    assert.ifError(result.error);
    if (name === 'failed') {
      assert.notEqual(result.status, 0, result.stdout + result.stderr);
      return;
    }
    assert.equal(result.status, 0, result.stdout + result.stderr);
    const guardScript = path.join(dir, 'guard.ps1');
    fs.writeFileSync(guardScript, guard.run.replaceAll('${{ runner.temp }}', dir));
    const guarded = spawnSync(process.env.PWSH || 'pwsh', ['-NoProfile', '-NonInteractive', '-File', guardScript],
      { cwd: root, encoding: 'utf8' });
    assert.ifError(guarded.error);
    assert.equal(guarded.status === 0 ? 0 : 1, expected, guarded.stdout + guarded.stderr);
    if (name === 'skipped') assert.match(guarded.stderr, /A test skipped instead of running/);
    if (name === 'empty') assert.match(guarded.stderr, /No test ran/);
  });
}

// These are shipped inputs. CI-only prerequisites must not invalidate their sealed identities.
for (const [file, expected] of [
  ['Resources/Statechart/dependency-lock.json', '3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab'],
  ['skills/engineering/statechart/PROVENANCE.md', '94bd4882414f1b3f116b5c04089f823147d45995d9895915a29662bb90d525f8'],
]) {
  test(`CI preserves sealed input ${file}`, () => {
    assert.equal(createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex'), expected);
  });
}

// Only the external JDK executable and TLC download are fixtures. A JDK download is forbidden.
for (const fault of [null, 'version', 'compiler', 'missing', 'java-exit', 'tlc']) {
  test(`installed runner JDK ${fault ? `rejects ${fault}` : 'publishes verified paths without downloading Java'}`,
    { skip: process.platform !== 'linux' }, t => {
      const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-oracle-provision-'));
      t.after(() => fs.rmSync(dir, { recursive: true, force: true }));
      fs.mkdirSync(path.join(dir, 'Resources/Statechart'), { recursive: true });
      const java = path.join(dir, 'installed-jdk/bin/java');
      fs.mkdirSync(path.dirname(java), { recursive: true });
      const version = fault === 'version' ? '21.0.12+7' : '21.0.13+9';
      if (fault !== 'missing') fs.writeFileSync(java, `#!/bin/sh
case "$1" in
  --version) echo 'openjdk ${version.split('+')[0]} LTS'
    echo 'OpenJDK Runtime Environment Temurin-${version} (build ${version}-LTS)' ;;
  --list-modules) echo 'java.base@21.0.13'
    ${fault === 'compiler' ? ':' : "echo 'jdk.compiler@21.0.13'"} ;;
  *) exit 99 ;;
esac
exit ${fault === 'java-exit' ? 1 : 0}
`, { mode: 0o755 });
      const jar = path.join(dir, 'tlc.jar');
      fs.writeFileSync(jar, 'fixture TLC');
      const tlc = { url: new URL(`file://${jar}`).href,
        sha256: createHash('sha256').update(fs.readFileSync(jar)).digest('hex') };
      if (fault === 'tlc') tlc.sha256 = '0'.repeat(64);
      fs.writeFileSync(path.join(dir, 'Resources/Statechart/dependency-lock.json'), JSON.stringify({ tlc }));
      const provision = workflow.jobs['statechart-config-oracle'].steps.find(step => step.id === 'statechart');
      assert.equal(provision.run, './.github/workflows/provision-statechart-oracle.ps1');
      const script = path.join(dir, 'provision.ps1');
      fs.writeFileSync(script, `$ErrorActionPreference = 'Stop'
function Invoke-WebRequest($Uri, $OutFile) {
  if ($Uri -ne '${tlc.url}') { throw 'Only the locked TLC download is authorized.' }
  Copy-Item ([Uri]$Uri).LocalPath $OutFile
}
& '${root}/${provision.run.replace(/^\.\//, '')}' -JavaHome '${path.dirname(path.dirname(java))}'
`);
      const output = path.join(dir, 'output');
      const result = spawnSync(process.env.PWSH || 'pwsh', ['-NoProfile', '-NonInteractive', '-File', script],
        { cwd: dir, env: { ...process.env, RUNNER_TEMP: dir, GITHUB_OUTPUT: output }, encoding: 'utf8' });
      assert.ifError(result.error);
      if (fault) {
        assert.notEqual(result.status, 0, result.stdout + result.stderr);
        const errors = { version: /must be Temurin 21\.0\.13\+9/, compiler: /jdk.compiler/,
          missing: /is missing/, 'java-exit': /version check failed/, tlc: /does not match its pinned SHA-256/ };
        assert.match(result.stderr, errors[fault]);
        assert.equal(fs.existsSync(output), false);
      } else {
        assert.equal(result.status, 0, result.stdout + result.stderr);
        const paths = Object.fromEntries(fs.readFileSync(output, 'utf8').trim().split('\n').map(line => line.split('=')));
        assert.equal(paths.java, java);
        assert.equal(fs.readFileSync(paths.tla2tools, 'utf8'), 'fixture TLC');
      }
    });
}

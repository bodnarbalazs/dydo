import assert from 'node:assert/strict';
import { copyFile, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';

const root = resolve(import.meta.dirname, '../../..');
const runner = process.env.FACADE_RUNNER || join(root, 'DynaDocs.Tests', 'coverage', 'gap_check.py');

test('configured Node adapter preserves literal argv, streams, and raw child exit 17', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'facade-node-'));
  try {
    await copyFile(runner, join(directory, 'gap_check.py'));
    const fixture = join(directory, 'fixture.mjs');
    await writeFile(fixture, [
      "import { writeFileSync } from 'node:fs'",
      "writeFileSync('received.json', JSON.stringify(process.argv.slice(2)))",
      "console.log('NODE_STDOUT_SENTINEL')",
      "console.error('NODE_STDERR_SENTINEL')",
      'process.exit(17)',
    ].join('\n'));
    const unavailable = { state: 'unavailable', reason: 'unused' };
    const data = {
      schema: 1, artifactRoot: 'results',
      stacks: [{
        name: 'node', kind: 'node', cwd: '.',
        isolation: { requirement: 'in-place', evidence: { state: 'verified', kind: 'direct' } },
        capabilities: {
          test: { state: 'configured', command: { kind: 'argv', argv: [process.execPath, fixture] }, artifacts: [] },
          static: unavailable, coverage: unavailable, mutation: unavailable,
        },
      }],
    };
    await writeFile(join(directory, 'gap_check.json'), JSON.stringify(data));
    // The facade supplies its current interpreter; standalone callers may configure PYTHON.
    const python = process.env.PYTHON || (process.platform === 'win32' ? 'py' : 'python3');
    const result = spawnSync(python, ['gap_check.py', 'test', '--stack', 'node', '--', '--', 'árvíztűrő tükörfúrógép'], { cwd: directory, encoding: 'utf8', timeout: 30000 });
    assert.equal(result.status, 1, result.stderr);
    assert.match(result.stdout, /NODE_STDOUT_SENTINEL/);
    assert.match(result.stderr, /NODE_STDERR_SENTINEL/);
    assert.deepEqual(JSON.parse(await readFile(join(directory, 'received.json'), 'utf8')), ['--', 'árvíztűrő tükörfúrógép']);
    const resultPath = result.stdout.split(/\r?\n/).find(line => line.startsWith('Result: ')).slice(8);
    const payload = JSON.parse(await readFile(resultPath, 'utf8'));
    assert.equal(payload.aggregateExit, 1);
    assert.equal(payload.results.length, 1);
    assert.equal(payload.results[0].state, 'failed');
    assert.equal(payload.results[0].resultExit, 1);
    assert.equal(payload.results[0].childExit, 17);
    assert.deepEqual(payload.results[0].argv, [process.execPath, fixture, '--', 'árvíztűrő tükörfúrógép']);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

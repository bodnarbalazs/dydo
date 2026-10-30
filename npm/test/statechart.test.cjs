const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { PassThrough } = require('node:stream');
const childProcess = require('node:child_process');
const { test } = require('node:test');
const platform = require('../lib/platform');
const paths = require('../lib/paths');

test('only statechart gets a hidden native child and shim-owned EOF channel', (t) => {
  const launches = [];
  const argv = process.argv;
  t.after(() => { process.argv = argv; });
  t.mock.method(platform, 'getPlatformInfo', () => ({ supported: true }));
  t.mock.method(paths, 'getBinaryPath', () => 'owned-native-fixture');
  t.mock.method(paths, 'isBinaryInstalled', () => true);
  t.mock.method(childProcess, 'spawn', (file, args, options) => {
    const child = new EventEmitter();
    child.stdin = new PassThrough();
    launches.push({ file, args, options, child });
    return child;
  });
  for (const command of ['statechart', 'check']) {
    process.argv = [argv[0], 'dydo.cjs', command, 'two words'];
    const launcher = require.resolve('../bin/dydo.cjs');
    delete require.cache[launcher];
    require(launcher);
  }
  const [chart, ordinary] = launches;
  assert.deepEqual(chart.args, ['statechart', 'two words']);
  assert.deepEqual(chart.options.stdio, ['pipe', 'inherit', 'inherit']);
  assert.equal(chart.options.windowsHide, true);
  assert.equal(chart.options.env.DYDO_STATECHART_NPM_LIFETIME, '1');
  assert.equal(chart.child.stdin.writableEnded, false);
  chart.child.stdin.emit('error', new Error('native already exited'));
  assert.equal(ordinary.options.stdio, 'inherit');
  assert.equal(ordinary.options.windowsHide, false);
  assert.equal(ordinary.options.env, undefined);
});

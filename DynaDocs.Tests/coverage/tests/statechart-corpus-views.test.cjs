'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

function view(name) {
  return import(pathToFileURL(path.resolve(__dirname, '../../..',
    'skills/engineering/statechart/scripts/test/corpus', name, 'view.mjs')).href);
}

test('CounterView folds odd states but preserves distinct even counter states', async () => {
  const { state } = await view('CounterView');
  assert.deepEqual([0, 1, 2, 3, 4].map(x => state({ x })), ['0', 'odd', '2', 'odd', '4']);
});

test('Login keeps hierarchy and labels for sign-in retries and locked-out state', async () => {
  const { state } = await view('Login');
  assert.deepEqual(state({ main: 'Trying to sign in.Checking first try' }), ['Trying to sign in', 'Checking first try']);
  assert.deepEqual(state({ main: 'Trying to sign in.Wrong password, 1 try left' }), ['Trying to sign in', 'Wrong password, 1 try left']);
  assert.deepEqual(state({ main: 'Locked out' }), ['Locked out']);
});

for (const scenario of ['LoginGuarded', 'LoginParallel']) {
  test(`${scenario} preserves independent session and retry regions`, async () => {
    const { state } = await view(scenario);
    assert.deepEqual(state({ session: 'Signed out', wrongTries: 0, ignored: 'hidden' }), { Session: 'Signed out', Tries: 0 });
    assert.deepEqual(state({ session: 'Locked out', wrongTries: 3 }), { Session: 'Locked out', Tries: 3 });
  });
}

for (const scenario of ['MediaPlayer', 'MediaPlayerFixed', 'MediaPlayerProps']) {
  test(`${scenario} preserves parallel regions and readable action labels`, async () => {
    const { state, step } = await view(scenario);
    assert.deepEqual(state({ playback: 'Stopped', volume: 'Normal volume', ignored: 1 }), { Playback: 'Stopped', Volume: 'Normal volume' });
    assert.deepEqual(state({ playback: 'Paused', volume: 'Muted' }), { Playback: 'Paused', Volume: 'Muted' });
    assert.equal(step('YouPressPlay'), 'you press play');
    assert.equal(step('ACallComesIn'), 'a call comes in');
    assert.equal(step('HTTPServer2Ready'), 'http server 2ready');
  });
}

for (const scenario of ['TwoPhase', 'TwoPhaseFixed']) {
  test(`${scenario} keeps manager and participant states without exposing protocol messages`, async () => {
    const { state } = await view(scenario);
    assert.deepEqual(state({ tmState: 'init', rmState: { r1: 'working', r2: 'prepared' }, msgs: [] }),
      { Manager: 'init', r1: 'working', r2: 'prepared' });
    assert.deepEqual(state({ tmState: 'done', rmState: { r1: 'committed', r2: 'aborted' }, msgs: ['Commit'] }),
      { Manager: 'done', r1: 'committed', r2: 'aborted' });
  });
}

'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');

// The statechart CLI loads each view by path; these tests import it by its literal path, so the
// static graph sees every export the assertions read and still reports one that nothing reads.

function regions(state) {
  assert.deepEqual(state({ session: 'Signed out', wrongTries: 0, ignored: 'hidden' }), { Session: 'Signed out', Tries: 0 });
  assert.deepEqual(state({ session: 'Locked out', wrongTries: 3 }), { Session: 'Locked out', Tries: 3 });
}

function player(state, step) {
  assert.deepEqual(state({ playback: 'Stopped', volume: 'Normal volume', ignored: 1 }), { Playback: 'Stopped', Volume: 'Normal volume' });
  assert.deepEqual(state({ playback: 'Paused', volume: 'Muted' }), { Playback: 'Paused', Volume: 'Muted' });
  assert.equal(step('YouPressPlay'), 'you press play');
  assert.equal(step('ACallComesIn'), 'a call comes in');
  assert.equal(step('HTTPServer2Ready'), 'http server 2ready');
}

function protocol(state) {
  assert.deepEqual(state({ tmState: 'init', rmState: { r1: 'working', r2: 'prepared' }, msgs: [] }),
    { Manager: 'init', r1: 'working', r2: 'prepared' });
  assert.deepEqual(state({ tmState: 'done', rmState: { r1: 'committed', r2: 'aborted' }, msgs: ['Commit'] }),
    { Manager: 'done', r1: 'committed', r2: 'aborted' });
}

test('CounterView folds odd states but preserves distinct even counter states', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/CounterView/view.mjs');
  assert.deepEqual([0, 1, 2, 3, 4].map(x => state({ x })), ['0', 'odd', '2', 'odd', '4']);
});

test('Login keeps hierarchy and labels for sign-in retries and locked-out state', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/Login/view.mjs');
  assert.deepEqual(state({ main: 'Trying to sign in.Checking first try' }), ['Trying to sign in', 'Checking first try']);
  assert.deepEqual(state({ main: 'Trying to sign in.Wrong password, 1 try left' }), ['Trying to sign in', 'Wrong password, 1 try left']);
  assert.deepEqual(state({ main: 'Locked out' }), ['Locked out']);
});

test('LoginGuarded preserves independent session and retry regions', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/LoginGuarded/view.mjs');
  regions(state);
});

test('LoginParallel preserves independent session and retry regions', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/LoginParallel/view.mjs');
  regions(state);
});

test('MediaPlayer preserves parallel regions and readable action labels', async () => {
  const { state, step } = await import('../../../skills/engineering/statechart/scripts/test/corpus/MediaPlayer/view.mjs');
  player(state, step);
});

test('MediaPlayerFixed preserves parallel regions and readable action labels', async () => {
  const { state, step } = await import('../../../skills/engineering/statechart/scripts/test/corpus/MediaPlayerFixed/view.mjs');
  player(state, step);
});

test('MediaPlayerProps preserves parallel regions and readable action labels', async () => {
  const { state, step } = await import('../../../skills/engineering/statechart/scripts/test/corpus/MediaPlayerProps/view.mjs');
  player(state, step);
});

test('TwoPhase keeps manager and participant states without exposing protocol messages', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/TwoPhase/view.mjs');
  protocol(state);
});

test('TwoPhaseFixed keeps manager and participant states without exposing protocol messages', async () => {
  const { state } = await import('../../../skills/engineering/statechart/scripts/test/corpus/TwoPhaseFixed/view.mjs');
  protocol(state);
});

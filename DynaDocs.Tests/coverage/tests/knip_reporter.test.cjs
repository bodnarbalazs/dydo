const { test } = require('node:test');
const assert = require('node:assert/strict');

test('native Knip reporter rejects corrupt counters before producing policy output', async () => {
  const { default: report } = await import('../knip_reporter.mjs');
  await assert.rejects(() => report({ counters: { total: -1 }, includedWorkspaceDirs: ['package'] }), /counters/);
});

test('native Knip reporter rejects missing or duplicate workspace identities', async () => {
  const { default: report } = await import('../knip_reporter.mjs');
  for (const includedWorkspaceDirs of [[], ['package', 'package']]) {
    await assert.rejects(() => report({ counters: { total: 1 }, includedWorkspaceDirs }), /workspace/);
  }
});


/* Preserve the native issue report beside its workspace and file counters. */
import nativeJson from './node_modules/knip/dist/reporters/json.js';

export default async function report(options) {
  const { counters, includedWorkspaceDirs } = options;
  if (!counters || !Object.values(counters).every(value => Number.isInteger(value) && value >= 0)) throw new Error('Invalid native Knip counters');
  if (!Array.isArray(includedWorkspaceDirs) || !includedWorkspaceDirs.length || new Set(includedWorkspaceDirs).size !== includedWorkspaceDirs.length) throw new Error('Invalid native Knip workspace inventory');
  await nativeJson(options);
  process.stdout.write(JSON.stringify({ kind: 'measurement', counters, includedWorkspaceDirs }) + '\n');
}


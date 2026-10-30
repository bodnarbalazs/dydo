// Fail before launching a visible child; inherited by the real worker and fixtures.
const cp = require('node:child_process');
const fs = require('node:fs');
for (const method of ['spawn', 'spawnSync', 'execFile', 'execFileSync']) {
  const original = cp[method];
  cp[method] = function(file, args, options, ...rest) {
    const hidden = options?.windowsHide === true;
    fs.appendFileSync(process.env.STATECHART_SPAWN_AUDIT,
      JSON.stringify({ pid: process.pid, method, file, hidden }) + '\n');
    if (!hidden) throw new Error(`Visible console launch refused: ${method} ${file}`);
    return original.call(this, file, args, options, ...rest);
  };
}
require('node:module').syncBuiltinESMExports();

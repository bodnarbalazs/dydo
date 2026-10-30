// This integration-only preload changes console visibility, not setup/test assertions.
const cp = require('node:child_process');
const fs = require('node:fs');
const original = cp.spawnSync;
cp.spawnSync = function(file, args, options) {
  const hidden = { ...options, windowsHide: true };
  fs.appendFileSync(process.env.DYD415_SETUP_AUDIT,
    JSON.stringify({ pid: process.pid, file, args, windowsHide: hidden.windowsHide }) + '\n');
  return original.call(this, file, args, hidden);
};

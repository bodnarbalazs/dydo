'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

function discover(root) {
  const files = [];
  function visit(folder) {
    for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
      const candidate = path.join(folder, entry.name);
      if (entry.isDirectory()) visit(candidate);
      else if (/\.test\.(?:c|m)?js$/u.test(entry.name)) files.push(path.resolve(candidate));
    }
  }
  visit(path.resolve(root));
  files.sort((left, right) => left.localeCompare(right, 'en'));
  if (!files.length) throw new Error(`No maintained Node test files under ${root}`);
  return files;
}

function main(args = process.argv.slice(2)) {
  const tests = discover(path.join(__dirname, 'tests'));
  const result = spawnSync(process.execPath, ['--test', ...args, ...tests], {
    cwd: path.resolve(__dirname, '..', '..'), stdio: 'inherit', env: process.env,
  });
  if (result.error) throw result.error;
  return Number.isInteger(result.status) ? result.status : 130;
}

module.exports = { discover, main };
if (require.main === module) process.exitCode = main();

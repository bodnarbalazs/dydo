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

function maintainedTests(root) {
  const files = [
    ...discover(path.join(root, 'DynaDocs.Tests', 'coverage', 'tests')),
    ...discover(path.join(root, 'npm', 'test')),
  ];
  files.sort((left, right) => left.localeCompare(right, 'en'));
  return files;
}

function main(args = process.argv.slice(2)) {
  const root = path.resolve(__dirname, '..', '..');
  const tests = maintainedTests(root);
  const result = spawnSync(process.execPath, ['--test', ...args, ...tests], {
    cwd: root, stdio: 'inherit', env: process.env,
  });
  if (result.error) throw result.error;
  return Number.isInteger(result.status) ? result.status : 130;
}

module.exports = { discover, maintainedTests };
if (require.main === module) process.exitCode = main();

#!/usr/bin/env node

const { spawn } = require('child_process');
const { getPlatformInfo } = require('../lib/platform');
const { getBinaryPath, isBinaryInstalled } = require('../lib/paths');

function main() {
  const platformInfo = getPlatformInfo();

  if (!platformInfo.supported) {
    console.error(`Error: ${platformInfo.error}`);
    console.error('Install via .NET instead: dotnet tool install -g dydo');
    process.exit(2);
  }

  const binaryPath = getBinaryPath(platformInfo);

  if (!isBinaryInstalled(platformInfo)) {
    console.error('Error: dydo binary not found.');
    console.error('Try reinstalling: npm install -g dydo');
    console.error('Or install via .NET: dotnet tool install -g dydo');
    process.exit(2);
  }

  const statechart = process.argv[2] === 'statechart';
  const child = spawn(binaryPath, process.argv.slice(2), {
    stdio: statechart ? ['pipe', 'inherit', 'inherit'] : 'inherit',
    windowsHide: statechart,
    ...(statechart ? { env: { ...process.env, DYDO_STATECHART_NPM_LIFETIME: '1' } } : {})
  });
  // Only this shim owns the writer. Kernel pipe EOF survives abrupt shim death.
  if (statechart) child.stdin.on('error', () => {});

  child.on('error', (err) => {
    console.error(`Error launching dydo: ${err.message}`);
    process.exit(2);
  });

  child.on('exit', (code, signal) => {
    if (signal) {
      process.exit(1);
    }
    process.exit(code ?? 0);
  });
}

main();

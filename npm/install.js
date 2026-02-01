#!/usr/bin/env node

const { getPlatformInfo } = require('./lib/platform');
const { downloadBinary } = require('./lib/download');
const { getBinaryDir, isBinaryInstalled, getInstalledVersion, setInstalledVersion } = require('./lib/paths');
const pkg = require('./package.json');

async function install() {
  // Check platform support
  const platformInfo = getPlatformInfo();

  if (!platformInfo.supported) {
    console.error(`\n[dydo] ${platformInfo.error}`);
    console.error('[dydo] You can still use dydo if you have .NET installed:');
    console.error('       dotnet tool install -g DynaDocs');
    process.exit(0); // Don't fail npm install, just warn
  }

  // Check if already installed with correct version
  const installedVersion = getInstalledVersion();
  if (installedVersion === pkg.version && isBinaryInstalled(platformInfo)) {
    console.log(`[dydo] Binary already installed (v${pkg.version})`);
    return;
  }

  try {
    await downloadBinary(pkg.version, platformInfo, getBinaryDir());
    setInstalledVersion(pkg.version);
  } catch (error) {
    console.error(`\n[dydo] Failed to download binary: ${error.message}`);
    console.error('[dydo] You can manually download from:');
    console.error(`       https://github.com/bodnarbalazs/dydo/releases/tag/v${pkg.version}`);
    console.error('[dydo] Or install via .NET:');
    console.error('       dotnet tool install -g DynaDocs');
    process.exit(1);
  }
}

// Only run during npm install, not when required as a module
if (require.main === module) {
  install().catch((err) => {
    console.error('[dydo] Installation error:', err);
    process.exit(1);
  });
}

module.exports = { install };

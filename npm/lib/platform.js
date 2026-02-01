const os = require('os');

// Map Node.js platform/arch to .NET RID (Runtime Identifier)
const PLATFORM_MAPPING = {
  'win32-x64': {
    rid: 'win-x64',
    binaryName: 'dydo.exe',
    archiveExt: '.zip'
  },
  'linux-x64': {
    rid: 'linux-x64',
    binaryName: 'dydo',
    archiveExt: '.tar.gz'
  },
  'darwin-x64': {
    rid: 'osx-x64',
    binaryName: 'dydo',
    archiveExt: '.tar.gz'
  },
  'darwin-arm64': {
    rid: 'osx-arm64',
    binaryName: 'dydo',
    archiveExt: '.tar.gz'
  }
};

function getPlatformInfo() {
  const platform = os.platform();
  const arch = os.arch();
  const key = `${platform}-${arch}`;

  const info = PLATFORM_MAPPING[key];
  if (!info) {
    return {
      supported: false,
      platform,
      arch,
      error: `Unsupported platform: ${platform}-${arch}. ` +
             `Supported: win-x64, linux-x64, osx-x64, osx-arm64`
    };
  }

  return {
    supported: true,
    platform,
    arch,
    ...info
  };
}

function getDownloadUrl(version, platformInfo) {
  const baseUrl = 'https://github.com/bodnarbalazs/dydo/releases/download';
  const assetName = `dydo-${platformInfo.rid}${platformInfo.archiveExt}`;
  return `${baseUrl}/v${version}/${assetName}`;
}

function getChecksumsUrl(version) {
  const baseUrl = 'https://github.com/bodnarbalazs/dydo/releases/download';
  return `${baseUrl}/v${version}/checksums.txt`;
}

module.exports = { getPlatformInfo, getDownloadUrl, getChecksumsUrl, PLATFORM_MAPPING };

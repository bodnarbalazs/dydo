const path = require('path');
const fs = require('fs');

// Binary is stored within the npm package directory
function getInstallDir() {
  return path.dirname(__dirname);
}

function getBinaryDir() {
  return path.join(getInstallDir(), 'native');
}

function getBinaryPath(platformInfo) {
  return path.join(getBinaryDir(), platformInfo.binaryName);
}

function isBinaryInstalled(platformInfo) {
  const binaryPath = getBinaryPath(platformInfo);
  return fs.existsSync(binaryPath);
}

// Marker file to track installed version
function getVersionMarkerPath() {
  return path.join(getBinaryDir(), '.version');
}

function getInstalledVersion() {
  const markerPath = getVersionMarkerPath();
  if (fs.existsSync(markerPath)) {
    return fs.readFileSync(markerPath, 'utf8').trim();
  }
  return null;
}

function setInstalledVersion(version) {
  const dir = getBinaryDir();
  fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(getVersionMarkerPath(), version);
}

module.exports = {
  getInstallDir,
  getBinaryDir,
  getBinaryPath,
  isBinaryInstalled,
  getInstalledVersion,
  setInstalledVersion
};

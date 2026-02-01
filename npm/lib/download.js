const https = require('https');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

async function downloadFile(url, destPath) {
  return new Promise((resolve, reject) => {
    const follow = (url, redirectCount = 0) => {
      if (redirectCount > 5) {
        reject(new Error('Too many redirects'));
        return;
      }

      https.get(url, (response) => {
        // Handle redirects (GitHub releases use them)
        if (response.statusCode === 302 || response.statusCode === 301) {
          follow(response.headers.location, redirectCount + 1);
          return;
        }

        if (response.statusCode !== 200) {
          reject(new Error(`Download failed: HTTP ${response.statusCode}`));
          return;
        }

        const file = fs.createWriteStream(destPath);
        response.pipe(file);
        file.on('finish', () => {
          file.close();
          resolve();
        });
        file.on('error', (err) => {
          fs.unlink(destPath, () => {});
          reject(err);
        });
      }).on('error', reject);
    };

    follow(url);
  });
}

async function extractArchive(archivePath, destDir, platformInfo) {
  fs.mkdirSync(destDir, { recursive: true });

  if (platformInfo.archiveExt === '.zip') {
    // Windows: use PowerShell to extract
    if (process.platform === 'win32') {
      execSync(
        `powershell -Command "Expand-Archive -Path '${archivePath}' -DestinationPath '${destDir}' -Force"`,
        { stdio: 'pipe' }
      );
    } else {
      execSync(`unzip -o "${archivePath}" -d "${destDir}"`, { stdio: 'pipe' });
    }
  } else {
    // tar.gz: use tar command
    execSync(`tar -xzf "${archivePath}" -C "${destDir}"`, { stdio: 'pipe' });
  }
}

async function downloadBinary(version, platformInfo, installDir) {
  const { getDownloadUrl } = require('./platform');
  const url = getDownloadUrl(version, platformInfo);

  const archivePath = path.join(installDir, `dydo${platformInfo.archiveExt}`);

  console.log(`[dydo] Downloading v${version} for ${platformInfo.rid}...`);

  await downloadFile(url, archivePath);
  console.log('[dydo] Download complete, extracting...');

  await extractArchive(archivePath, installDir, platformInfo);

  // Clean up archive
  fs.unlinkSync(archivePath);

  // Set executable permission on Unix systems
  if (process.platform !== 'win32') {
    const binaryPath = path.join(installDir, platformInfo.binaryName);
    fs.chmodSync(binaryPath, 0o755);
  }

  console.log('[dydo] Installation complete.');
  return path.join(installDir, platformInfo.binaryName);
}

module.exports = { downloadFile, extractArchive, downloadBinary };

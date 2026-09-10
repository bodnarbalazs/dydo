const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const fs = require('node:fs');
const https = require('node:https');
const os = require('node:os');
const path = require('node:path');
const { PassThrough } = require('node:stream');
const { after, test } = require('node:test');

const { downloadFile, extractArchive } = require('../lib/download');
const fixtureRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'dydo-download-'));

after(() => fs.rmSync(fixtureRoot, { recursive: true, force: true }));

function response(statusCode, body = '', headers = {}) {
  const stream = new PassThrough();
  stream.statusCode = statusCode;
  stream.headers = headers;
  process.nextTick(() => stream.end(body));
  return stream;
}

test('downloadFile follows a redirect and writes the response body', async (t) => {
  const urls = [];
  t.mock.method(https, 'get', (url, callback) => {
    urls.push(url);
    callback(url.endsWith('/redirect')
      ? response(302, '', { location: 'https://fixture.invalid/asset' })
      : response(200, 'archive fixture'));
    return new EventEmitter();
  });
  const destination = path.join(fixtureRoot, 'download.bin');

  await downloadFile('https://fixture.invalid/redirect', destination);

  assert.deepEqual(urls, ['https://fixture.invalid/redirect', 'https://fixture.invalid/asset']);
  assert.equal(fs.readFileSync(destination, 'utf8'), 'archive fixture');
});

test('downloadFile rejects a non-success response without creating an archive', async (t) => {
  t.mock.method(https, 'get', (_url, callback) => {
    callback(response(404));
    return new EventEmitter();
  });
  const destination = path.join(fixtureRoot, 'missing.bin');

  await assert.rejects(
    downloadFile('https://fixture.invalid/missing', destination),
    /Download failed: HTTP 404/
  );

  assert.equal(fs.existsSync(destination), false);
});

test('extractArchive expands a tar archive into the destination', async () => {
  const source = path.join(fixtureRoot, 'source');
  const destination = path.join(fixtureRoot, 'extracted');
  const archive = path.join(fixtureRoot, 'fixture.tar.gz');
  fs.mkdirSync(source, { recursive: true });
  fs.writeFileSync(path.join(source, 'dydo'), 'binary fixture');
  require('node:child_process').execFileSync('tar', ['-czf', archive, '-C', source, 'dydo']);

  await extractArchive(archive, destination, { archiveExt: '.tar.gz' });

  assert.equal(fs.readFileSync(path.join(destination, 'dydo'), 'utf8'), 'binary fixture');
});

const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const fs = require('node:fs');
const https = require('node:https');
const os = require('node:os');
const path = require('node:path');
const { PassThrough } = require('node:stream');
const { after, test } = require('node:test');

const platform = require('../lib/platform');
const { downloadFile, extractArchive, downloadBinary } = require('../lib/download');
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
  t.mock.method(https, 'get', function missingResponse(...args) {
    const callback = args[1];
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

test('downloadFile removes a partial archive when the file stream fails', async (t) => {
  const file = new EventEmitter();
  const removed = [];
  t.mock.method(fs, 'createWriteStream', () => file);
  t.mock.method(fs, 'unlink', (candidate, callback) => {
    removed.push(candidate);
    callback();
  });
  t.mock.method(https, 'get', (url, callback) => {
    assert.equal(url, 'https://fixture.invalid/partial');
    const incoming = new EventEmitter();
    incoming.statusCode = 200;
    incoming.headers = {};
    incoming.pipe = () => process.nextTick(() => file.emit('error', new Error('disk full')));
    callback(incoming);
    return new EventEmitter();
  });
  const destination = path.join(fixtureRoot, 'partial.bin');

  await assert.rejects(downloadFile('https://fixture.invalid/partial', destination), /disk full/);

  assert.deepEqual(removed, [destination]);
});

test('downloadFile rejects redirect loops and request errors', async (t) => {
  let requests = 0;
  t.mock.method(https, 'get', (url, callback) => {
    assert.equal(url, 'https://fixture.invalid/loop');
    requests += 1;
    callback(response(302, '', { location: 'https://fixture.invalid/loop' }));
    return new EventEmitter();
  });
  await assert.rejects(
    downloadFile('https://fixture.invalid/loop', path.join(fixtureRoot, 'loop.bin')),
    /Too many redirects/
  );
  assert.equal(requests, 6);

  https.get = () => {
    const request = new EventEmitter();
    process.nextTick(() => request.emit('error', new Error('network fixture')));
    return request;
  };
  await assert.rejects(
    downloadFile('https://fixture.invalid/error', path.join(fixtureRoot, 'error.bin')),
    /network fixture/
  );
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

test('extractArchive expands a zip archive into the destination', async () => {
  const source = path.join(fixtureRoot, 'zip-source');
  const destination = path.join(fixtureRoot, 'zip-extracted');
  const archive = path.join(fixtureRoot, 'fixture.zip');
  fs.mkdirSync(source, { recursive: true });
  fs.writeFileSync(path.join(source, 'dydo.exe'), 'binary fixture');
  require('node:child_process').execFileSync('tar', ['-a', '-cf', archive, '-C', source, 'dydo.exe']);

  await extractArchive(archive, destination, { archiveExt: '.zip' });

  assert.equal(fs.readFileSync(path.join(destination, 'dydo.exe'), 'utf8'), 'binary fixture');
});

test('downloadBinary downloads, extracts, cleans up, and returns the native path', async (t) => {
  const source = path.join(fixtureRoot, 'binary-source');
  const archive = path.join(fixtureRoot, 'binary-source.tar.gz');
  const destination = path.join(fixtureRoot, 'binary-install');
  fs.mkdirSync(source, { recursive: true });
  fs.writeFileSync(path.join(source, 'dydo'), 'binary fixture');
  require('node:child_process').execFileSync('tar', ['-czf', archive, '-C', source, 'dydo']);
  const body = fs.readFileSync(archive);
  t.mock.method(platform, 'getDownloadUrl', () => 'https://fixture.invalid/binary');
  t.mock.method(https, 'get', (url, callback) => {
    assert.equal(url, 'https://fixture.invalid/binary');
    callback(response(200, body));
    return new EventEmitter();
  });

  const binary = await downloadBinary('3.0.0', {
    rid: 'linux-x64', archiveExt: '.tar.gz', binaryName: 'dydo'
  }, destination);

  assert.equal(binary, path.join(destination, 'dydo'));
  assert.equal(fs.readFileSync(binary, 'utf8'), 'binary fixture');
  assert.equal(fs.existsSync(path.join(destination, 'dydo.tar.gz')), false);
});

"""Proposed one-shot acquisition of commissioned Node DATA; never extract or execute it."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import ssl
import subprocess
import sys
import tempfile
import time
import urllib.request
import urllib.error

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
PACKET = BASE / 'b3-node-source'
OUTPUT = BASE / 'b3-node-20261031a'
ARCHIVE = 'node-v22.23.2-linux-x64.tar.gz'
URL = 'https://nodejs.org/dist/v22.23.2/' + ARCHIVE
CHECKSUM_URL = 'https://nodejs.org/dist/v22.23.2/SHASUMS256.txt'
EXPECTED = 'b294a556e639d64338823920e5866c21c02741742d2e1529ee1a225c1ec9252a'
OWNED_TRANSFER = None
DRAIN_UNCERTAIN = False
LC_PINS = {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'}


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    with (OUTPUT / name).open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        fp.close()
        raise RuntimeError('Redirect prohibited; retain response and stop without retry')


def checksum_line(raw):
    text = raw.decode('ascii', errors='strict')
    matches = re.findall(r'^([0-9a-f]{64})  ' + re.escape(ARCHIVE) + r'$', text, re.M)
    if matches != [EXPECTED]:
        raise ValueError('Official checksum line missing, duplicate or different from commissioned SHA256')
    return EXPECTED + '  ' + ARCHIVE


def fetch(url, path, maximum, deadline, opener):
    if url not in [URL, CHECKSUM_URL]:
        raise ValueError('Only exact commissioned HTTPS URLs permitted')
    started = time.monotonic()
    if started >= deadline:
        raise TimeoutError('Acquisition time budget exhausted before request')
    request = urllib.request.Request(url, headers={'User-Agent': 'DYD415-source-pinned-data-acquisition', 'Accept-Encoding': 'identity'})
    save(path.name + '.request.json', dict(url=url, method='GET', maximumBytes=maximum,
        startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), redirectsAllowed=False, retries=0))
    # open xb before requesting: existing outputs cannot cause an extra request.
    with path.open('xb') as destination:
        try:
            response = opener.open(request, timeout=min(15, deadline - started))
        except urllib.error.HTTPError as error:
            try:
                save(path.name + '.http-error.json', dict(url=url, status=error.code, headers=list(error.headers.items())))
            finally:
                error.close()
            raise
        with response:
            if response.status != 200 or response.geturl() != url:
                raise ValueError('HTTP status/URL differs; no retry or redirect')
            if response.headers.get('Content-Encoding', 'identity').lower() != 'identity':
                raise ValueError('Unexpected transport encoding')
            length = response.headers.get('Content-Length')
            if length is not None and (not length.isdecimal() or int(length) > maximum):
                raise ValueError('Invalid or excessive Content-Length')
            save(path.name + '.response.json', dict(url=url, status=response.status,
                headers=list(response.headers.items()), maximumBytes=maximum,
                requestTimeoutSeconds=15, totalDeadlineSeconds=180))
            total, digest = 0, hashlib.sha256()
            while True:
                if time.monotonic() >= deadline:
                    raise TimeoutError('Acquisition total time bound exceeded')
                block = response.read1(min(65536, maximum - total + 1))
                if not block:
                    break
                total += len(block)
                if total > maximum:
                    raise ValueError('Acquisition byte bound exceeded')
                destination.write(block)
                digest.update(block)
            if not total or (length is not None and total != int(length)):
                raise ValueError('Empty or truncated response')
    facts = dict(url=url, path=str(path), bytes=total, sha256=digest.hexdigest(), seconds=time.monotonic()-started)
    save(path.name + '.result.json', facts)
    return facts


def validate_host():
    if os.name != 'nt' or sys.platform != 'win32':
        raise RuntimeError('Commissioned Windows host required')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve():
        raise RuntimeError('Redirected LOCALAPPDATA; refuse before booking')
    if Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected TEMP; refuse before booking')


def verify_inputs():
    validate_host()
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
            creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() != '6e3fe7e8018130af6b8586ef0cf40c1474740864':
        raise RuntimeError('HEAD differs from retained draft')
    pin = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    if release['state'] != 'RELEASED_B3_NODE_DATA' or release['sourcePinSha256'] != sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Exact captain acquisition release required')
    if sha(Path(__file__)) != pin['driverSha256'] or sha(TEMP / 'dyd415-rmi-b3-node-launch-20261031.py') != pin['launcherSha256']:
        raise RuntimeError('TEMP counterpart identity differs')
    for row in pin['sources'] + json.loads((PACKET / 'preserved-inputs.json').read_bytes()):
        if sha(ROOT / row['path']) != row['sha256']:
            raise RuntimeError('Source/custody differs: ' + row['path'])
    for path, digest in LC_PINS.items():
        if sha(LC / path) != digest:
            raise RuntimeError('LC input differs: ' + path)


def transfer(scratch):
    # Disable implicit proxy/auth handlers; only the exact public HTTPS requests below.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), NoRedirect(),
        urllib.request.HTTPSHandler(context=ssl.create_default_context()))
    deadline = time.monotonic() + 180
    witness = scratch / 'SHASUMS256.txt'
    checksum = fetch(CHECKSUM_URL, witness, 1048576, deadline, opener)
    line = checksum_line(witness.read_bytes())
    with (OUTPUT / 'SHASUMS256.txt').open('xb') as stream:
        stream.write(witness.read_bytes())
    save('checksum-provenance.json', dict(response=checksum, exactLine=line, expected=EXPECTED))
    archive = fetch(URL, scratch / ARCHIVE, 134217728, deadline, opener)
    if archive['sha256'] != EXPECTED:
        raise ValueError('Archive hash differs; retain as untrusted DATA, no extraction/install/retry')
    save('acquisition.json', dict(archive=archive, checksum=checksum, checksumLine=line,
        archiveVerified=True, requests=2, redirects=0, retries=0, extracted=False, installed=False,
        executed=False, globalChanges=False, runtimeVersionClaim=False))


def drain_transfer():
    global DRAIN_UNCERTAIN
    if DRAIN_UNCERTAIN:
        raise RuntimeError('Prior owned transfer drain uncertain; retain TEMP lock without retry')
    try:
        if OWNED_TRANSFER is not None:
            if OWNED_TRANSFER.poll() is None:
                OWNED_TRANSFER.kill()
            OWNED_TRANSFER.wait(timeout=15)
    except BaseException:
        DRAIN_UNCERTAIN = True
        raise
    return True


def acquire():
    global OWNED_TRANSFER
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-node-', dir=TEMP))
    save('scratch.json', dict(path=str(scratch), retainedAsData=True))
    context = dict(parentPid=os.getpid(), ticket=os.environ['LC_SUITE_SLOT_TICKET'],
        output=str(OUTPUT), scratch=str(scratch), sourcePinSha256=sha(PACKET / 'source-pin.json'),
        lockSha256=sha(TEMP / 'dydo-fullsuite.lock'))
    with (scratch / 'context.json').open('x', encoding='utf-8') as stream:
        json.dump(context, stream, indent=2)
    argv = [sys.executable, '-B', str(PACKET / 'transfer.py'), '--context', str(scratch / 'context.json')]
    save('transfer.argv.json', dict(argv=argv, hidden=True, hardWaitSeconds=210, ownedKillWaitSeconds=15,
        ticket=context['ticket'], startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    with (OUTPUT / 'transfer.stdout').open('xb') as stdout, (OUTPUT / 'transfer.stderr').open('xb') as stderr:
        OWNED_TRANSFER = subprocess.Popen(argv, cwd=ROOT, stdout=stdout, stderr=stderr,
            creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            save('transfer-owned.json', dict(pid=OWNED_TRANSFER.pid, parentPid=os.getpid(),
                source=row_identity(PACKET / 'transfer.py'), stableHandle='Python Popen owned process handle; child source starts no children'))
            code = OWNED_TRANSFER.wait(timeout=210)
        finally:
            drain_transfer()
            save('transfer.result.json', dict(exit=OWNED_TRANSFER.returncode, drained=True))
    if code != 0:
        raise RuntimeError('One-shot transfer failed; retain stdout/stderr/partial DATA, no retry')
    receipt = json.loads((OUTPUT / 'acquisition.json').read_bytes())
    archive = scratch / ARCHIVE
    if not receipt['archiveVerified'] or receipt['archive']['path'] != str(archive) or sha(archive) != EXPECTED:
        raise RuntimeError('Drained transfer did not leave the exact commissioned archive DATA')
    save('verified-data.json', dict(archive=row_identity(archive),
        checksumLine=checksum_line((scratch / 'SHASUMS256.txt').read_bytes()),
        extracted=False, installed=False, executed=False))


def row_identity(path):
    return dict(path=str(path), bytes=path.stat().st_size, sha256=sha(path))


def main():
    verify_inputs()
    if not OUTPUT.is_dir() or (OUTPUT / 'preflight.json').exists():
        raise RuntimeError('Acquisition output absent or already used')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    live = suite_slot.live()
    if live.directory.resolve() != Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve() or live.prefix != 'LC-Suite-Slot' or live.variable != 'LC_SUITE_SLOT_TICKET':
        raise RuntimeError('Wrong default live LC namespace')
    save('preflight.json', dict(namespace=str(live.directory), localAppData=os.environ.get('LOCALAPPDATA'),
        temp=str(tempfile.gettempdir()), sourcePinSha256=sha(PACKET / 'source-pin.json'), lcPins=LC_PINS))
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__))])
    lock, failure = TEMP / 'dydo-fullsuite.lock', None
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
            namespace=str(live.directory), bookingOwned=booking.owned, inherited=booking.inherited,
            startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        save('admission.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock_bytes = (json.dumps(owner, sort_keys=True) + '\n').encode()
            with lock.open('xb') as stream:
                stream.write(lock_bytes)
            try:
                acquire()
            except BaseException as error:
                failure = error
                save('stop.json', dict(type=type(error).__name__, message=str(error), retries=0, partialDataRetained=True))
            finally:
                # Never release the owned lock if the transfer's process handle cannot be drained.
                drain_transfer()
                save('owned-cleanup.json', dict(transferAbsentOrDrained=True, parentFileContextsClosed=True,
                    scratchRetained=True, childrenStarted=0 if OWNED_TRANSFER is None else 1,
                    containersStarted=0, extracted=False, executed=False))
                if lock.read_bytes() != lock_bytes:
                    raise RuntimeError('Owned TEMP lock identity uncertain; retain and return')
                lock.unlink()
        save('secondary-released.json', dict(heavyReleased=True, tempLockReleased=not lock.exists()))
    save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited))
    verify_inputs()
    save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True, productAndPayloadUnchanged=True))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

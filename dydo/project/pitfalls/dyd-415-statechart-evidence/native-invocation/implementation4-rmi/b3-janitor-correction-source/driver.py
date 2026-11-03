"""Twelve finite Java-free POSIX janitor correction regressions; source-only until separately released."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import uuid

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
PACKET = BASE / 'b3-janitor-correction-source'
OUTPUT = BASE / 'b3-janitor-correction-20261031a'
_shared = BASE / 'b3-source/driver.py'
if hashlib.sha256(_shared.read_bytes()).hexdigest() != '24f0afe8f437cef9dc0fc8a3d5cf98540f39af0bf6f45e129f12c2e53549c100':
    raise RuntimeError('Inspected containment bytes differ before import')
_spec = importlib.util.spec_from_file_location('_node_contract_containment', _shared)
common = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(common)
common.OUTPUT = OUTPUT


def validate_host():
    if os.name != 'nt' or sys.platform != 'win32':
        raise RuntimeError('Commissioned Windows host required')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve() or Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected host namespace; refuse before booking')


def verify_inputs():
    validate_host()
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() != '6e3fe7e8018130af6b8586ef0cf40c1474740864':
        raise RuntimeError('HEAD differs from retained draft')
    pin = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    if release['state'] != 'RELEASED_B3_JANITOR_CORRECTION' or release['sourcePinSha256'] != common.sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Exact captain source release required')
    if common.sha(Path(__file__)) != pin['driverSha256'] or common.sha(TEMP / 'dyd415-rmi-b3-janitor-correction-launch-20261031.py') != pin['launcherSha256']:
        raise RuntimeError('TEMP counterpart identity differs')
    for row in pin['sources']:
        path = ROOT / row['path']
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Current source/custody differs: ' + row['path'])
    mapping = json.loads((PACKET / 'historical-applicability.json').read_bytes())['beforeSnapshots']
    for row in json.loads((PACKET / 'preserved-inputs.json').read_bytes()):
        path = ROOT / mapping.get(row['path'], row['path'])
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Historical bytes differ: ' + row['path'])
    manifest = json.loads((PACKET / 'input-manifest.json').read_bytes())
    for row in manifest['retainedInputs']:
        path = Path(row['source'])
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Retained Node/checker DATA differs before booking')
    for name, expected in common.LC_PINS.items():
        if common.sha(LC / name) != expected:
            raise RuntimeError('LC bytes differ: ' + name)


VOLUME = None


def execute(nonce, children):
    global VOLUME
    VOLUME = 'dyd415-rmi-b3-janitor-correction-' + nonce + '-node'
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-janitor-correction-', dir=TEMP))
    common.save('scratch.json', dict(path=str(scratch)))
    source, work, output = [scratch / name for name in ['input', 'work', 'output']]
    for path in [source / 'repo', work / 'tmp', output]:
        path.mkdir(parents=True)
    manifest = json.loads((PACKET / 'input-manifest.json').read_bytes())
    copied = []
    for row in manifest['files']:
        original, target = ROOT / row['path'], source / 'repo' / row['path']
        if common.sha(original) != row['sha256'] or original.stat().st_size != row['bytes']:
            raise RuntimeError('Retained source differs before owned copy')
        target.parent.mkdir(parents=True, exist_ok=True)
        with original.open('rb') as reader, target.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        if common.sha(target) != row['sha256']:
            raise RuntimeError('Owned source copy differs')
        copied.append(row)
    for row in manifest['retainedInputs']:
        original, target = Path(row['source']), source / row['destination']
        if common.sha(original) != row['sha256'] or original.stat().st_size != row['bytes']:
            raise RuntimeError('Retained DATA input differs')
        with original.open('rb') as reader, target.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        if common.sha(target) != row['sha256']:
            raise RuntimeError('Owned DATA copy differs')
    for original, target in [(PACKET / 'worker.py', source / 'worker.py'),
            (PACKET / 'input-manifest.json', source / 'input-manifest.json'),
            (ROOT / manifest['nodeMemberManifest']['path'], source / 'node-members.json')]:
        with original.open('rb') as reader, target.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        if common.sha(target) != common.sha(original):
            raise RuntimeError('Worker/manifest copy differs')
    common.save('copied-inputs.json', dict(files=copied, workerSha256=common.sha(source / 'worker.py'),
        manifestSha256=common.sha(source / 'input-manifest.json'), nodeMemberSha256=common.sha(source / 'node-members.json')))
    image = json.loads(common.run('image-inspect', ['docker', 'image', 'inspect', common.IMAGE]))[0]
    if image['Id'] != common.IMAGE.split('@')[1] or image['Os'] != 'linux' or image['Architecture'] != 'amd64':
        raise RuntimeError('Cached owned Linux image differs')
    common.run('volume-create', ['docker', 'volume', 'create', '--label', 'com.dydo.proof-owner=' + nonce, VOLUME])
    view = json.loads(common.run('volume-inspect', ['docker', 'volume', 'inspect', VOLUME]))[0]
    if view['Name'] != VOLUME or view['Labels']['com.dydo.proof-owner'] != nonce:
        raise RuntimeError('Owned volume identity differs')
    mounts = ['type=bind,source=' + str(source) + ',target=/input,readonly',
        'type=bind,source=' + str(work) + ',target=/work', 'type=bind,source=' + str(output) + ',target=/output']
    common.container('dyd415-rmi-b3-janitor-correction-' + nonce + '-extract', nonce,
        mounts + ['type=volume,source=' + VOLUME + ',target=/toolchain'],
        ['-B', '/input/worker.py', '--extract'], children)
    common.container('dyd415-rmi-b3-janitor-correction-' + nonce + '-finite', nonce,
        mounts + ['type=volume,source=' + VOLUME + ',target=/toolchain,readonly'],
        ['-B', '/input/worker.py'], children)
    common.save('summary.json', json.loads((output / 'completed.json').read_bytes()))


def cleanup(children, nonce):
    if VOLUME is None:
        common.save('teardown.json', dict(cleanupConfirmed=not children, noMaterialResourcesCreated=not children))
        return not children
    return common.cleanup(children, VOLUME, nonce)


def main():
    verify_inputs()
    if not OUTPUT.is_dir() or (OUTPUT / 'preflight.json').exists():
        raise RuntimeError('Envelope output absent or already used')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    live = suite_slot.live()
    if live.directory.resolve() != Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve() or live.prefix != 'LC-Suite-Slot' or live.variable != 'LC_SUITE_SLOT_TICKET':
        raise RuntimeError('Wrong default live LC namespace')
    common.save('preflight.json', dict(namespace=str(live.directory), localAppData=os.environ.get('LOCALAPPDATA'),
        temp=str(tempfile.gettempdir()), sourcePinSha256=common.sha(PACKET / 'source-pin.json'), lcPins=common.LC_PINS))
    nonce = uuid.uuid4().hex[:16]
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__))])
    lock, children, failure, cleaned = TEMP / 'dydo-fullsuite.lock', [], None, False
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'], nonce=nonce,
            namespace=str(live.directory), bookingOwned=booking.owned, inherited=booking.inherited,
            startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        common.save('admission.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock_bytes = (json.dumps(owner, sort_keys=True) + '\n').encode()
            with lock.open('xb') as stream:
                stream.write(lock_bytes)
            try:
                execute(nonce, children)
            except BaseException as error:
                failure = error
                common.save('stop.json', dict(type=type(error).__name__, message=str(error)))
            finally:
                try:
                    cleaned = cleanup(children, nonce)
                except BaseException as error:
                    common.save('teardown-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                try:
                    common.retain_work()
                except BaseException as error:
                    common.save('retain-work-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                if cleaned and lock.read_bytes() == lock_bytes:
                    lock.unlink()
        common.save('secondary-released.json', dict(heavyReleased=True, tempLockReleased=not lock.exists()))
    common.save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited, cleanupConfirmed=cleaned))
    verify_inputs()
    common.save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True,
        admittedCorrectedSourceUnchanged=True, payloadUnchanged=True))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

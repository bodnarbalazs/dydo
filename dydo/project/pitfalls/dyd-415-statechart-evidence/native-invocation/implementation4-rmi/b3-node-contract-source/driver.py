"""Finite nine-contract envelope using admitted LC and B3 containment; source-only until released."""
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
PACKET = BASE / 'b3-node-contract-source'
OUTPUT = BASE / 'b3-node-contract-20261031a'
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
    if release['state'] != 'RELEASED_B3_NODE_CONTRACTS' or release['sourcePinSha256'] != common.sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Exact captain source release required')
    if common.sha(Path(__file__)) != pin['driverSha256'] or common.sha(TEMP / 'dyd415-rmi-b3-node-contract-launch-20261031.py') != pin['launcherSha256']:
        raise RuntimeError('TEMP counterpart identity differs')
    for row in pin['sources'] + json.loads((PACKET / 'preserved-inputs.json').read_bytes()):
        path = ROOT / row['path']
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Source/custody differs: ' + row['path'])
    for name, expected in common.LC_PINS.items():
        if common.sha(LC / name) != expected:
            raise RuntimeError('LC bytes differ: ' + name)


def execute(nonce, children):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-node-contract-', dir=TEMP))
    common.save('scratch.json', dict(path=str(scratch)))
    source, work, output = [scratch / name for name in ['input', 'work', 'output']]
    for path in [source / 'repo', work / 'tmp', output]:
        path.mkdir(parents=True)
    manifest = json.loads((PACKET / 'input-manifest.json').read_bytes())
    copies = []
    for row in manifest['files']:
        original, target = ROOT / row['path'], source / 'repo' / row['path']
        if common.sha(original) != row['sha256']:
            raise RuntimeError('Input differs before copy')
        target.parent.mkdir(parents=True, exist_ok=True)
        with original.open('rb') as reader, target.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        if common.sha(target) != row['sha256']:
            raise RuntimeError('Copied input differs')
        copies.append(dict(source=str(original), destination=str(target), sha256=common.sha(target), bytes=target.stat().st_size))
    shutil.copy2(PACKET / 'worker.py', source / 'worker.py')
    shutil.copy2(PACKET / 'input-manifest.json', source / 'input-manifest.json')
    if common.sha(source / 'worker.py') != common.sha(PACKET / 'worker.py') or common.sha(source / 'input-manifest.json') != common.sha(PACKET / 'input-manifest.json'):
        raise RuntimeError('Worker or manifest copy differs')
    common.save('copied-inputs.json', dict(files=copies, workerSha256=common.sha(source / 'worker.py'), manifestSha256=common.sha(source / 'input-manifest.json')))
    image = json.loads(common.run('image-inspect', ['docker', 'image', 'inspect', common.IMAGE]))[0]
    if image['Id'] != common.IMAGE.split('@')[1] or image['Os'] != 'linux' or image['Architecture'] != 'amd64':
        raise RuntimeError('Cached Linux image differs')
    common.container('dyd415-rmi-b3-node-contract-' + nonce + '-finite', nonce,
        ['type=bind,source=' + str(source) + ',target=/input,readonly',
         'type=bind,source=' + str(work) + ',target=/work',
         'type=bind,source=' + str(output) + ',target=/output'],
        ['-B', '/input/worker.py'], children)
    result = json.loads((output / 'completed.json').read_bytes())
    if result['cases'] != 9 or result['passed'] != 9 or result['skipped'] != 0 or result['names'] != sorted(manifest['names']):
        raise RuntimeError('Finite nine-contract completion differs')
    common.save('summary.json', result)


def cleanup(children, nonce):
    confirmed = True
    for index, (name, identity) in enumerate(children):
        if identity is None:
            common.save('cleanup-uncertain.json', dict(name=name, reason='No returned owned ID; retain TEMP lock'))
            confirmed = False
            continue
        prefix = 'cleanup-' + str(index)
        view = json.loads(common.run(prefix + '-inspect', ['docker', 'inspect', identity]))[0]
        if view['Id'] != identity or view['Name'] != '/' + name or view['Config']['Labels']['com.dydo.proof-owner'] != nonce:
            raise RuntimeError('Owned container identity uncertain; no foreign cleanup')
        if view['State']['Running']:
            common.run(prefix + '-stop', ['docker', 'stop', '--time', '5', identity])
        common.run(prefix + '-remove', ['docker', 'rm', identity])
        remaining = common.run(prefix + '-absence', ['docker', 'ps', '-a', '--no-trunc', '--filter', 'id=' + identity, '--format', '{{.ID}}'])
        confirmed = confirmed and not remaining.strip()
    common.save('teardown.json', dict(cleanupConfirmed=confirmed, ownedContainerIds=[identity for _, identity in children],
        volumesCreated=0, scratchRetained=True, desktopJava=False))
    return confirmed


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
    common.save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True, productAndPayloadUnchanged=True))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

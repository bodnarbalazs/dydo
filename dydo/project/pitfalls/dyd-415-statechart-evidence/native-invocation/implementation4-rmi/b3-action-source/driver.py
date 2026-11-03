"""Finite successor admission; reuse inspected B3 containment and owned cleanup unchanged."""
import argparse
import datetime
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import uuid

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
PACKET = BASE / 'b3-action-source'
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
_original = BASE / 'b3-source/driver.py' if os.name == 'nt' else Path(__file__).resolve().parent.parent / 'b3-source/driver.py'
if hashlib.sha256(_original.read_bytes()).hexdigest() != '24f0afe8f437cef9dc0fc8a3d5cf98540f39af0bf6f45e129f12c2e53549c100':
    raise RuntimeError('Inspected containment source differs before import')
_spec = importlib.util.spec_from_file_location('_b3_action_containment', _original)
common = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(common)
OUTPUT = None


def validate_host():
    if os.name != 'nt' or sys.platform != 'win32':
        raise RuntimeError('Commissioned Windows host required')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve():
        raise RuntimeError('Redirected LOCALAPPDATA; refuse before booking')
    if Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected TEMP; refuse before booking')


def verify_inputs(group):
    validate_host()
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
            creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() != '6e3fe7e8018130af6b8586ef0cf40c1474740864':
        raise RuntimeError('HEAD differs from retained draft')
    pin = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    if release['state'] != 'RELEASED_B3_ACTION' or group not in release['groups'] or release['sourcePinSha256'] != common.sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Captain release for exact successor/group required')
    if common.sha(Path(__file__)) != pin['driverSha256'] or common.sha(TEMP / 'dyd415-rmi-b3-action-launch-20261031.py') != pin['launcherSha256']:
        raise RuntimeError('TEMP counterpart identity differs')
    inputs = json.loads((PACKET / 'inputs.json').read_bytes())
    for row in pin['sources'] + json.loads((PACKET / 'preserved-inputs.json').read_bytes()) + inputs['snapshotFiles']:
        if common.sha(ROOT / row['path']) != row['sha256']:
            raise RuntimeError('Source/custody differs: ' + row['path'])
    for row in inputs['retainedInputs']:
        if common.sha(Path(row['source'])) != row['sha256']:
            raise RuntimeError('Retained input differs: ' + row['source'])
    for path, digest in common.LC_PINS.items():
        if common.sha(LC / path) != digest:
            raise RuntimeError('LC input differs: ' + path)
    if group == 'bootstrap':
        prior = BASE / 'b3-action-20261031a-contracts'
        if json.loads((prior / 'preflight.json').read_bytes())['sourcePinSha256'] != common.sha(PACKET / 'source-pin.json'):
            raise RuntimeError('Focused contracts used a different source pin')
        if json.loads((prior / 'retained/output/contracts.json').read_bytes()) != dict(cases=12, skipped=0, java=False, node=False):
            raise RuntimeError('Focused twelve real contracts required before bootstrap')
        for name, field in [('teardown.json', 'cleanupConfirmed'), ('post-custody.json', 'sourceAndHistoricalPinsMatch')]:
            if not json.loads((prior / name).read_bytes())[field]:
                raise RuntimeError('Contract cleanup/custody prerequisite missing')
        released = json.loads((prior / 'released.json').read_bytes())
        if not (released['fifoReleased'] or released['inheritedReturned']):
            raise RuntimeError('Contract queue prerequisite missing')
        if json.loads((prior / 'secondary-released.json').read_bytes()) != dict(heavyReleased=True, tempLockReleased=True):
            raise RuntimeError('Contract secondary lock prerequisite missing')


def execute(group, nonce, children, volume):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-action-', dir=TEMP))
    common.save('scratch.json', dict(path=str(scratch)))
    source, work, artifacts = [scratch / name for name in ['input', 'work', 'output']]
    for path in [source, work / 'tmp', artifacts]:
        path.mkdir(parents=True)
    inputs = json.loads((PACKET / 'inputs.json').read_bytes())
    for row in inputs['retainedInputs']:
        if row['destination'] == 'jdk.tar.gz':
            continue
        target = source / row['destination']
        target.parent.mkdir(parents=True, exist_ok=True)
        with target.open('xb') as destination, Path(row['source']).open('rb') as original:
            shutil.copyfileobj(original, destination)
        if common.sha(target) != row['sha256']:
            raise RuntimeError('Input copy identity differs')
    inventory = []
    with tarfile.open(source / 'site.tar', 'x') as bundle:
        for row in inputs['snapshotFiles']:
            raw = (ROOT / row['path']).read_bytes()
            info = tarfile.TarInfo('repo/' + row['path'])
            info.size, info.mode, info.mtime = len(raw), row['mode'], 0
            bundle.addfile(info, io.BytesIO(raw))
            inventory.append(dict(member=info.name, sha256=row['sha256'], mode=row['mode']))
    (source / 'snapshot.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    shutil.copy2(PACKET / 'worker.py', source / 'worker.py')
    common.save('snapshot.json', dict(archiveSha256=common.sha(source / 'site.tar'), files=inventory))
    image = json.loads(common.run('image-inspect', ['docker', 'image', 'inspect', common.IMAGE]))[0]
    if image['Id'] != common.IMAGE.split('@')[1] or image['Os'] != 'linux' or image['Architecture'] != 'amd64':
        raise RuntimeError('Cached Linux image identity differs')
    if common.reuse_jdk(source / 'jdk.tar.gz') != 'jdk-21.0.8+9':
        raise RuntimeError('Retained JDK archive root differs')
    mounts = ['type=bind,source=' + str(source) + ',target=/input,readonly',
              'type=bind,source=' + str(work) + ',target=/work',
              'type=bind,source=' + str(artifacts) + ',target=/output']
    common.run('volume-create', ['docker', 'volume', 'create', '--label', 'com.dydo.proof-owner=' + nonce, volume])
    view = json.loads(common.run('volume-inspect', ['docker', 'volume', 'inspect', volume]))[0]
    if view['Name'] != volume or view['Labels']['com.dydo.proof-owner'] != nonce:
        raise RuntimeError('Owned volume identity differs')
    common.container('dyd415-rmi-b3-action-' + nonce + '-extract', nonce,
        [*mounts, 'type=volume,source=' + volume + ',target=/toolchain'],
        ['-B', '/input/worker.py', '--group', group, '--extract'], children)
    common.container('dyd415-rmi-b3-action-' + nonce + '-finite', nonce,
        [*mounts, 'type=volume,source=' + volume + ',target=/toolchain,readonly'],
        ['-B', '/input/worker.py', '--group', group], children)
    if json.loads((artifacts / 'group-completed.json').read_bytes()) != dict(group=group, completed=True):
        raise RuntimeError('Finite stage incomplete')
    common.save('summary.json', dict(group=group, completed=True, javaInvocations=1 if group == 'bootstrap' else 0,
        healthyEvidenceReexecuted=False, productionTsNativeProof=False, windows=False, javaMetrics='UNAVAILABLE'))


def main():
    global OUTPUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--group', choices=['contracts', 'bootstrap'], required=True)
    group = parser.parse_args().group
    OUTPUT = BASE / ('b3-action-20261031a-' + group)
    common.OUTPUT = OUTPUT
    verify_inputs(group)
    if not OUTPUT.is_dir() or (OUTPUT / 'preflight.json').exists():
        raise RuntimeError('Stage output absent or already executed')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    live = suite_slot.live()
    if live.directory.resolve() != Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve() or live.prefix != 'LC-Suite-Slot' or live.variable != 'LC_SUITE_SLOT_TICKET':
        raise RuntimeError('Wrong default live LC namespace')
    common.save('preflight.json', dict(group=group, namespace=str(live.directory), localAppData=os.environ.get('LOCALAPPDATA'),
        temp=str(tempfile.gettempdir()), sourcePinSha256=common.sha(PACKET / 'source-pin.json'), lcPins=common.LC_PINS))
    nonce = uuid.uuid4().hex[:16]
    volume = 'dyd415-rmi-b3-action-' + nonce + '-jdk'
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__)), '--group', group])
    children, failure, cleaned = [], None, False
    lock = TEMP / 'dydo-fullsuite.lock'
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
                execute(group, nonce, children, volume)
            except BaseException as error:
                failure = error
                common.save('stop.json', dict(type=type(error).__name__, message=str(error)))
            finally:
                try:
                    cleaned = common.cleanup(children, volume, nonce)
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
    verify_inputs(group)
    common.save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True, productAndPayloadUnchanged=True))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

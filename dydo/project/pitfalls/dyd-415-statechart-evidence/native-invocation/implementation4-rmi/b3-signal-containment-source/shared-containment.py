"""Bounded B3 external file driver: live FIFO, Heavy, TEMP lock, hidden owned Docker children."""
import argparse
import io
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time
import uuid

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
OUTPUT = None
GROUP = None
IMAGE = 'python@sha256:6a9220edda062e9223ea0784740a535987299420b96fbfd520b89bbd3e772485'
JDK_SHA = 'f2dc5418092c43003db8f9005c4a286e1c0104fea96ccdd49e8ebd037cac9219'
RETAINED_JDK = TEMP / 'dyd415-rmi-b1-825xlni3/input/jdk.tar.gz'
TLC = TEMP / 'dyd415-implementation2-rjdqq05t/deps/7bdbd8727178911b1b9501285d21c16edbc86ef87b7884433088fb4509c280ad/win-x64/tlc/tla2tools.jar'
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-source'
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


def run(name, argv, timeout=180, expected=0):
    print(name + ': starting', flush=True)
    save(name + '.argv.json', dict(argv=list(map(str, argv)), cwd=str(ROOT), hidden=True,
        ticket=os.environ.get('LC_SUITE_SLOT_TICKET'), startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    started = time.monotonic()
    with (OUTPUT / (name + '.stdout')).open('xb') as stdout, (OUTPUT / (name + '.stderr')).open('xb') as stderr:
        try:
            result = subprocess.run(list(map(str, argv)), cwd=ROOT, stdout=stdout, stderr=stderr,
                timeout=timeout, creationflags=subprocess.CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired) as error:
            save(name + '.result.json', dict(error=type(error).__name__, message=str(error)))
            raise
    save(name + '.result.json', dict(exit=result.returncode, expected=expected, seconds=time.monotonic() - started))
    print(name + ': exit ' + str(result.returncode), flush=True)
    if expected is not None and result.returncode != expected:
        raise RuntimeError(name + ' failed; first failure retained, no automatic retry')
    return (OUTPUT / (name + '.stdout')).read_text(errors='replace')



def container(name, nonce, mounts, argv, children, entry='python'):
    args = ['docker', 'create', '--name', name, '--label', 'com.dydo.proof-owner=' + nonce,
        '--pull=never', '--network', 'none', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--pids-limit', '128', '--memory', '1g', '--cpus', '2', '--stop-timeout', '5',
        '--read-only', '--env', 'PYTHONDONTWRITEBYTECODE=1', '--env', 'TMPDIR=/work/tmp',
        '--entrypoint', entry, '--workdir', '/input']
    for mount in mounts:
        args += ['--mount', mount]
    args += [IMAGE, *argv]
    children.append((name, None))
    identity = run(name + '-create', args).strip()
    assert len(identity) == 64 and all(char in '0123456789abcdef' for char in identity)
    children[-1] = (name, identity)
    raw = run(name + '-inspect', ['docker', 'inspect', identity])
    view = json.loads(raw)[0]
    assert view['Id'] == identity and view['Name'] == '/' + name
    assert view['Config']['Labels']['com.dydo.proof-owner'] == nonce
    assert view['Image'] == IMAGE.split('@')[1]
    config = view['HostConfig']
    assert config['NetworkMode'] == 'none' and config['ReadonlyRootfs']
    assert config['CapDrop'] == ['ALL'] and 'no-new-privileges' in config['SecurityOpt']
    assert not config['Privileged'] and config['PidsLimit'] == 128 and config['Memory'] == 1073741824
    assert config['PidMode'] != 'host' and config['IpcMode'] != 'host' and not config['Devices']
    assert config['NanoCpus'] == 2000000000 and not config['PortBindings']
    expected_mounts = {'/input', '/work', '/output'}
    if any('target=/toolchain' in mount for mount in mounts): expected_mounts.add('/toolchain')
    assert {row['Destination'] for row in view['Mounts']} == expected_mounts
    for row in view['Mounts']:
        if row['Destination'] == '/input': assert not row['RW']
        if row['Destination'] == '/toolchain' and not name.endswith('-extract'): assert not row['RW']
    run(name + '-start', ['docker', 'start', '--attach', identity], timeout=1200)
    final = json.loads(run(name + '-state', ['docker', 'inspect', identity]))[0]
    assert final['State']['Status'] == 'exited' and final['State']['ExitCode'] == 0 and not final['State']['OOMKilled']
    return identity


def reuse_jdk(archive):
    assert sha(RETAINED_JDK) == JDK_SHA, 'Retained JDK changed; no network permitted'
    with RETAINED_JDK.open('rb') as source, archive.open('xb') as destination:
        shutil.copyfileobj(source, destination)
    assert sha(archive) == JDK_SHA
    save('jdk-reuse.json', dict(source=str(RETAINED_JDK), destination=str(archive), sha256=sha(archive),
        bytes=archive.stat().st_size, downloaded=False, executedOnHost=False))
    roots, count, total = set(), 0, 0
    with tarfile.open(archive, 'r:gz') as bundle:
        for member in bundle:
            path = PurePosixPath(member.name)
            assert not path.is_absolute() and '..' not in path.parts and path.parts
            roots.add(path.parts[0]); count += 1; total += member.size
            assert count <= 40000 and total <= 2 * 1024 ** 3
            assert member.isdir() or member.isfile() or member.issym() or member.islnk()
            if member.issym() or member.islnk():
                assert not PurePosixPath(member.linkname).is_absolute()
    assert len(roots) == 1
    top = next(iter(roots))
    save('jdk-archive-inventory.json', dict(top=top, entries=count, expandedBytes=total, extractedOnHost=False))
    return top



def cleanup(children, volume, nonce):
    confirmed = True
    for index, (name, identity) in enumerate(reversed(children)):
        if identity is None:
            save('cleanup-' + str(index) + '-uncertain.json', dict(name=name, removed=False,
                reason='No returned container ID; retain ownership lock and return uncertainty'))
            confirmed = False
            continue
        view = json.loads(run('cleanup-' + str(index) + '-inspect', ['docker', 'inspect', identity]))[0]
        assert view['Id'] == identity and view['Config']['Labels']['com.dydo.proof-owner'] == nonce
        if view['State']['Running']:
            run('cleanup-' + str(index) + '-stop', ['docker', 'stop', '--time', '5', identity])
        run('cleanup-' + str(index) + '-remove', ['docker', 'rm', identity])
        absent = run('cleanup-' + str(index) + '-absence', ['docker', 'ps', '-a', '--no-trunc', '--filter', 'id=' + identity, '--format', '{{.ID}}'])
        confirmed = confirmed and not absent.strip()
    raw = run('cleanup-volume-inspect', ['docker', 'volume', 'inspect', volume], expected=None)
    if raw.strip():
        view = json.loads(raw)[0]
        assert view['Name'] == volume and view['Labels']['com.dydo.proof-owner'] == nonce
        run('cleanup-volume-remove', ['docker', 'volume', 'rm', volume])
    remaining = run('cleanup-volume-absence', ['docker', 'volume', 'ls', '--filter', 'name=' + volume, '--format', '{{.Name}}'])
    confirmed = confirmed and volume not in remaining.splitlines()
    save('teardown.json', dict(cleanupConfirmed=confirmed, ownedContainerIds=[identity for _, identity in children], volume=volume,
        tempScratchRetained=True, desktopJavaLaunched=False))
    return confirmed



def verify_inputs():
    assert os.name == 'nt' and sys.platform == 'win32', 'B3 requires commissioned Windows host'
    assert Path(os.environ.get('LOCALAPPDATA', '')).resolve() == Path('C:/Users/User/AppData/Local').resolve(), 'Redirected LOCALAPPDATA; refuse before booking'
    assert Path(tempfile.gettempdir()).resolve() == TEMP.resolve(), 'Redirected TEMP; refuse before booking'
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() == '6e3fe7e8018130af6b8586ef0cf40c1474740864'
    declaration = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    assert GROUP in release['groups'] and release['state'] == 'RELEASED_B3' and release['sourcePinSha256'] == sha(PACKET / 'source-pin.json'), 'Captain has not released exact B3 source bytes'
    assert declaration['driverSha256'] == sha(Path(__file__))
    assert declaration['launcherSha256'] == sha(TEMP / 'dyd415-rmi-b3-launch-20261031.py')
    for row in declaration['sources']:
        assert sha(ROOT / row['path']) == row['sha256'], 'B3 source changed: ' + row['path']
    for row in json.loads((PACKET / 'preserved-inputs.json').read_bytes()):
        assert sha(ROOT / row['path']) == row['sha256'], 'Retained evidence/source changed: ' + row['path']
    for row in json.loads((PACKET / 'inputs.json').read_bytes())['retainedInputs']:
        assert sha(Path(row['source'])) == row['sha256'], 'Runtime input changed: ' + row['source']
    if GROUP in ['B', 'C']:
        raise RuntimeError('Pinned retained Linux Node archive unavailable; B/C require a new dependency pin before release')
    for path, digest in LC_PINS.items():
        assert sha(LC / path) == digest, 'Commissioned LC bytes changed: ' + path
    assert sha(RETAINED_JDK) == JDK_SHA, 'Retained archive changed; no download/substitution'
    assert sha(TLC) == '936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88'


def execute(nonce, children, volume):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-', dir=TEMP))
    save('scratch.json', dict(path=str(scratch)))
    source, work, artifacts = [scratch / name for name in ['input', 'work', 'output']]
    for path in [source, work / 'tmp', artifacts]:
        path.mkdir(parents=True)
    inputs = json.loads((PACKET / 'inputs.json').read_bytes())
    for row in inputs['retainedInputs']:
        if row['destination'] == 'jdk.tar.gz':
            continue
        target = source / row['destination']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(row['source'], target)
        assert sha(target) == row['sha256']
    inventory = []
    with tarfile.open(source / 'site.tar', 'x') as bundle:
        for row in inputs['snapshotFiles']:
            original = ROOT / row['path']
            assert sha(original) == row['sha256']
            raw = original.read_bytes()
            info = tarfile.TarInfo('repo/' + row['path'])
            info.size, info.mode, info.mtime = len(raw), row['mode'], 0
            bundle.addfile(info, io.BytesIO(raw))
            inventory.append(dict(member=info.name, sha256=row['sha256'], mode=row['mode']))
    (source / 'snapshot.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    for name in ['worker.py', 'observe.py']:
        shutil.copy2(PACKET / name, source / name)
    save('snapshot.json', dict(archiveSha256=sha(source / 'site.tar'), files=inventory))
    image = json.loads(run('image-inspect', ['docker', 'image', 'inspect', IMAGE]))[0]
    assert image['Id'] == IMAGE.split('@')[1] and image['Os'] == 'linux' and image['Architecture'] == 'amd64'
    top = reuse_jdk(source / 'jdk.tar.gz')
    assert top == 'jdk-21.0.8+9'
    mounts = ['type=bind,source=' + str(source) + ',target=/input,readonly',
              'type=bind,source=' + str(work) + ',target=/work',
              'type=bind,source=' + str(artifacts) + ',target=/output']
    run('volume-create', ['docker', 'volume', 'create', '--label', 'com.dydo.proof-owner=' + nonce, volume])
    view = json.loads(run('volume-inspect', ['docker', 'volume', 'inspect', volume]))[0]
    assert view['Name'] == volume and view['Labels']['com.dydo.proof-owner'] == nonce
    container('dyd415-rmi-b3-' + nonce + '-extract', nonce,
              [*mounts, 'type=volume,source=' + volume + ',target=/toolchain'],
              ['-B', '/input/worker.py', '--group', GROUP, '--extract'], children, entry='python')
    container('dyd415-rmi-b3-' + nonce + '-finite', nonce,
              [*mounts, 'type=volume,source=' + volume + ',target=/toolchain,readonly'],
              ['-B', '/input/worker.py', '--group', GROUP], children)
    result = json.loads((artifacts / 'group-completed.json').read_bytes())
    assert result == dict(group=GROUP, completed=True)
    save('summary.json', dict(stage='B3', group=GROUP, completed=True,
        productRecompiled=False, managedJre=False, installedReadiness=False, windows=False,
        javaMetrics='UNAVAILABLE', noCrossGroupExpansion=True))


def retain_work():
    identity = OUTPUT / 'scratch.json'
    if not identity.is_file():
        return
    scratch = Path(json.loads(identity.read_bytes())['path'])
    assert scratch.parent.resolve() == TEMP.resolve() and scratch.name.startswith('dyd415-rmi-b3-')
    for directory in ['output', 'work']:
        base = scratch / directory
        for child in base.rglob('*'):
            if child.is_file():
                assert not child.is_symlink() and child.resolve().is_relative_to(base.resolve())
                target = OUTPUT / 'retained' / directory / child.relative_to(base)
                target.parent.mkdir(parents=True, exist_ok=True)
                with child.open('rb') as source, target.open('xb') as destination:
                    shutil.copyfileobj(source, destination)


def main():
    global GROUP, OUTPUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--group', choices=['A', 'B', 'C', 'D'], required=True)
    GROUP = parser.parse_args().group
    OUTPUT = ROOT / ('dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-20261031a-' + GROUP)
    verify_inputs()
    assert OUTPUT.is_dir() and not (OUTPUT / 'preflight.json').exists(), 'B3 output already executed'
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    live = suite_slot.live()
    preflight = dict(osName=os.name, platform=sys.platform, localAppData=os.environ.get('LOCALAPPDATA'),
        temp=os.environ.get('TEMP'), namespace=str(live.directory), prefix=live.prefix, variable=live.variable,
        inheritedTicket=os.environ.get('LC_SUITE_SLOT_TICKET'), driver=str(Path(__file__)),
        driverSha256=sha(Path(__file__)), launcherSha256=sha(TEMP / 'dyd415-rmi-b3-launch-20261031.py'),
        sourcePins=json.loads((PACKET / 'inputs.json').read_bytes()), lcPins=LC_PINS, observedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    save('preflight.json', preflight)
    assert os.name == 'nt' and sys.platform == 'win32', 'B3 requires the commissioned Windows host'
    assert Path(os.environ.get('LOCALAPPDATA', '')).resolve() == Path('C:/Users/User/AppData/Local').resolve(), 'Redirected LOCALAPPDATA; refuse before booking'
    assert live.directory.resolve() == Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve(), 'Wrong default live LC namespace; refuse before booking'
    assert live.prefix == 'LC-Suite-Slot' and live.variable == 'LC_SUITE_SLOT_TICKET', 'Wrong LC live API identity'
    assert Path(tempfile.gettempdir()).resolve() == TEMP.resolve(), 'Redirected TEMP; refuse before booking'
    nonce = uuid.uuid4().hex[:16]
    volume = 'dyd415-rmi-b3-' + nonce + '-jdk'
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__)), '--group', GROUP])
    children, failure, cleaned = [], None, False
    lock = TEMP / 'dydo-fullsuite.lock'
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
            namespace=str(suite_slot.live().directory), bookingOwned=booking.owned, inherited=booking.inherited,
            nonce=nonce, lcHashes=LC_PINS, noPycache=True, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        save('admission.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock_bytes = (json.dumps(owner, sort_keys=True) + '\n').encode()
            with lock.open('xb') as stream: stream.write(lock_bytes)
            try:
                execute(nonce, children, volume)
            except BaseException as error:
                failure = error
                save('stop.json', dict(type=type(error).__name__, message=str(error)))
            finally:
                try:
                    cleaned = cleanup(children, volume, nonce)
                except BaseException as error:
                    save('teardown-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                try:
                    retain_work()
                except BaseException as error:
                    save('retain-work-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                if cleaned and lock.read_bytes() == lock_bytes: lock.unlink()
        save('secondary-released.json', dict(heavyReleased=True, tempLockReleased=not lock.exists()))
    save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited, cleanupConfirmed=cleaned))
    verify_inputs()
    save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True, productAndPayloadUnchanged=True))
    if failure: raise failure


if __name__ == '__main__':
    main()

"""Bounded B1 external file driver: live FIFO, Heavy, TEMP lock, hidden owned Docker children."""
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
OUTPUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031c'
IMAGE = 'python@sha256:6a9220edda062e9223ea0784740a535987299420b96fbfd520b89bbd3e772485'
JDK_SHA = 'f2dc5418092c43003db8f9005c4a286e1c0104fea96ccdd49e8ebd037cac9219'
RETAINED_JDK = TEMP / 'dyd415-rmi-b1-825xlni3/input/jdk.tar.gz'
TLC = TEMP / 'dyd415-implementation2-rjdqq05t/deps/7bdbd8727178911b1b9501285d21c16edbc86ef87b7884433088fb4509c280ad/win-x64/tlc/tla2tools.jar'
JAVA_ROOT = 'skills/engineering/statechart/scripts/java/'
PINS = {
    JAVA_ROOT + 'src/dydo/statechart/LoopbackTlc.java': '0edeb8431ba300737f2710d5c5dd495d424362f5f64698651737299337f95dc2',
    JAVA_ROOT + 'src/dydo/statechart/LoopbackSockets.java': '56711ce74c3f7db93cc8ac1a928dfbe82de470dc566b730c06dc7bae43ee79a3',
    JAVA_ROOT + 'build.py': '82939903e3774c3f29d4651541d170b301c100d60da4f1fde1faec471b1f38ea',
    JAVA_ROOT + 'build-lock.json': 'ab5d4052b97eb06e43d050ded02bac16b47f87421c44e154228d974c3e86a028',
    'DynaDocs.Tests/coverage/tests/test_statechart_java_build.py': 'ac8f740244c802d6723ff902bbdd3749591606e5ab93242b7a1294f88d2ce7fe'}
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


def verify_inputs():
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() == '6e3fe7e8018130af6b8586ef0cf40c1474740864'
    source_pin = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/runtime-isolation/source-pin.json'
    assert sha(source_pin) == '86eb5aa93d437a8d73eba12f0fa4ccd525f8129f1b1bdb65f4b3aa38ae77c5b3'
    closure = json.loads(source_pin.read_bytes())
    for group in ['sources', 'currentDraftSourceCustody', 'payloadUnchanged', 'preservedB1b']:
        for row in closure[group]:
            assert sha(ROOT / row['path']) == row['sha256'], 'Inspected source/custody changed: ' + row['path']
    declaration = json.loads((OUTPUT / 'pre-execution-pin.json').read_bytes())
    assert declaration['driverSha256'] == sha(Path(__file__))
    assert declaration['launcherSha256'] == sha(TEMP / 'dyd415-rmi-b1c-launch-20261031.py')
    assert declaration['authoritySha256'] == sha(TEMP / 'dyd415-rmi-b1c-build-release-20261031.md')
    for row in json.loads((OUTPUT / 'preserved-inputs.json').read_bytes()):
        assert sha(ROOT / row['path']) == row['sha256'], 'Historical source/evidence changed: ' + row['path']
    assert sha(RETAINED_JDK) == JDK_SHA, 'Retained JDK missing or changed; no download/retry'
    for path, digest in PINS.items():
        assert sha(ROOT / path) == digest, 'B1 inspected build input changed: ' + path
    for path, digest in LC_PINS.items():
        assert sha(LC / path) == digest, 'Commissioned LC bytes changed: ' + path
    assert sha(TLC) == '936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88'


def container(name, nonce, mounts, argv, children, entry='python'):
    args = ['docker', 'create', '--name', name, '--label', 'com.dydo.proof-owner=' + nonce,
        '--pull=never', '--network', 'none', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--pids-limit', '128', '--memory', '1g', '--cpus', '2', '--stop-timeout', '5',
        '--read-only', '--env', 'PYTHONDONTWRITEBYTECODE=1', '--env', 'TMPDIR=/work/tmp',
        '--entrypoint', entry, '--workdir', '/input']
    for mount in mounts:
        args += ['--mount', mount]
    args += [IMAGE, *argv]
    identity = run(name + '-create', args).strip()
    assert len(identity) == 64 and all(char in '0123456789abcdef' for char in identity)
    children.append((name, identity))
    raw = run(name + '-inspect', ['docker', 'inspect', identity])
    view = json.loads(raw)[0]
    assert view['Id'] == identity and view['Name'] == '/' + name
    assert view['Config']['Labels']['com.dydo.proof-owner'] == nonce
    assert view['Image'] == IMAGE.split('@')[1]
    config = view['HostConfig']
    assert config['NetworkMode'] == 'none' and config['ReadonlyRootfs']
    assert config['CapDrop'] == ['ALL'] and 'no-new-privileges' in config['SecurityOpt']
    assert not config['Privileged'] and config['PidsLimit'] == 128 and config['Memory'] == 1073741824
    assert config['NanoCpus'] == 2000000000 and not config['PortBindings']
    expected_mounts = {'/input', '/work', '/output'}
    if any('target=/toolchain' in mount for mount in mounts): expected_mounts.add('/toolchain')
    assert {row['Destination'] for row in view['Mounts']} == expected_mounts
    for row in view['Mounts']:
        if row['Destination'] == '/input': assert not row['RW']
        if row['Destination'] == '/toolchain' and entry != 'tar': assert not row['RW']
    run(name + '-start', ['docker', 'start', '--attach', identity], timeout=480)
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


def execute(nonce, children, volume):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b1c-', dir=TEMP))
    save('scratch.json', dict(path=str(scratch)))
    source, work, artifacts = [scratch / name for name in ['input', 'work', 'output']]
    for path in [source, work / 'tmp', artifacts]: path.mkdir(parents=True)
    rows = []
    for name in PINS:
        target = source / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / name, target)
        assert sha(target) == PINS[name]
        rows.append(dict(path=name, sha256=sha(target), bytes=target.stat().st_size))
    shutil.copy2(TLC, source / 'tla2tools.jar')
    save('source-inputs.json', dict(scratch=str(scratch), files=rows,
        tlc=dict(path=str(TLC), sha256=sha(TLC)), driver=str(Path(__file__)), driverSha256=sha(Path(__file__)),
        launcher=str(TEMP / 'dyd415-rmi-b1c-launch-20261031.py'), launcherSha256=sha(TEMP / 'dyd415-rmi-b1c-launch-20261031.py')))
    image = json.loads(run('image-inspect', ['docker', 'image', 'inspect', IMAGE]))[0]
    assert image['Id'] == IMAGE.split('@')[1] and image['Os'] == 'linux' and image['Architecture'] == 'amd64'
    mounts = ['type=bind,source=' + str(source) + ',target=/input,readonly',
        'type=bind,source=' + str(work) + ',target=/work', 'type=bind,source=' + str(artifacts) + ',target=/output']
    test_name = 'dyd415-rmi-bootstrap-' + nonce + '-tests'
    container(test_name, nonce, mounts, ['-B', '-m', 'unittest', 'discover', '-v',
        '-s', '/input/DynaDocs.Tests/coverage/tests', '-p', 'test_statechart_java_build.py'], children)
    stream = (OUTPUT / (test_name + '-start.stderr')).read_text(errors='replace')
    assert 'Ran 21 tests' in stream and '\nOK\n' in stream and 'skipped=' not in stream
    assert sum(line.endswith(' ... ok') for line in stream.splitlines()) == 21
    save('build-helper-tests.json', dict(actualDiscovery=21, passed=21, skipped=0,
        inputSha256=PINS['DynaDocs.Tests/coverage/tests/test_statechart_java_build.py'], javaInvoked=False))
    archive = source / 'jdk.tar.gz'
    top = reuse_jdk(archive)
    run('volume-create', ['docker', 'volume', 'create', '--label', 'com.dydo.proof-owner=' + nonce, volume])
    view = json.loads(run('volume-inspect', ['docker', 'volume', 'inspect', volume]))[0]
    assert view['Name'] == volume and view['Labels']['com.dydo.proof-owner'] == nonce
    extraction = [*mounts, 'type=volume,source=' + volume + ',target=/toolchain']
    container('dyd415-rmi-bootstrap-' + nonce + '-extract', nonce, extraction,
        ['--extract', '--gzip', '--file=/input/jdk.tar.gz', '--directory=/toolchain', '--no-same-owner'], children, entry='tar')
    compiler_mounts = [*mounts, 'type=volume,source=' + volume + ',target=/toolchain,readonly']
    container('dyd415-rmi-bootstrap-' + nonce, nonce, compiler_mounts,
        ['-B', '/input/' + JAVA_ROOT + 'build.py', '--jdk', '/toolchain/' + top,
        '--jdk-archive', '/input/jdk.tar.gz', '--tlc', '/input/tla2tools.jar', '--work', '/work/build',
        '--output', '/output/statechart-tlc-bootstrap.jar', '--receipt', '/output/build-receipt.json'], children)
    receipt = json.loads((artifacts / 'build-receipt.json').read_bytes())
    assert receipt['artifactSha256'] == sha(artifacts / 'statechart-tlc-bootstrap.jar')
    assert receipt['twoFreshBuildsByteIdentical'] and receipt['runtimeProof'] is False
    for name in ['statechart-tlc-bootstrap.jar', 'build-receipt.json']:
        shutil.copy2(artifacts / name, OUTPUT / name)
    save('build-summary.json', dict(artifactSha256=receipt['artifactSha256'], receiptSha256=sha(OUTPUT / 'build-receipt.json'),
        sourceInputsSha256=sha(OUTPUT / 'source-inputs.json'), runtimeProof=False, canonicalLockUnchanged=True, payloadUnchanged=True))


def retain_work():
    identity = OUTPUT / 'scratch.json'
    if not identity.is_file(): return
    scratch = Path(json.loads(identity.read_bytes())['path'])
    assert scratch.parent.resolve() == TEMP.resolve() and scratch.name.startswith('dyd415-rmi-b1c-')
    build = scratch / 'work/build'
    for child in build.rglob('*'):
        if child.is_file():
            target = OUTPUT / 'build-work' / child.relative_to(build)
            target.parent.mkdir(parents=True, exist_ok=True)
            with child.open('rb') as source, target.open('xb') as destination:
                shutil.copyfileobj(source, destination)


def cleanup(children, volume, nonce):
    confirmed = True
    for index, (name, identity) in enumerate(reversed(children)):
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


def main():
    verify_inputs()
    assert OUTPUT.is_dir() and not (OUTPUT / 'preflight.json').exists(), 'B1c output already executed'
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    live = suite_slot.live()
    preflight = dict(osName=os.name, platform=sys.platform, localAppData=os.environ.get('LOCALAPPDATA'),
        temp=os.environ.get('TEMP'), namespace=str(live.directory), prefix=live.prefix, variable=live.variable,
        inheritedTicket=os.environ.get('LC_SUITE_SLOT_TICKET'), driver=str(Path(__file__)),
        driverSha256=sha(Path(__file__)), launcherSha256=sha(TEMP / 'dyd415-rmi-b1c-launch-20261031.py'),
        sourcePins=PINS, lcPins=LC_PINS, observedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    save('preflight.json', preflight)
    assert os.name == 'nt' and sys.platform == 'win32', 'B1c requires the commissioned Windows host'
    assert Path(os.environ.get('LOCALAPPDATA', '')).resolve() == Path('C:/Users/User/AppData/Local').resolve(), 'Redirected LOCALAPPDATA; refuse before booking'
    assert live.directory.resolve() == Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve(), 'Wrong default live LC namespace; refuse before booking'
    assert live.prefix == 'LC-Suite-Slot' and live.variable == 'LC_SUITE_SLOT_TICKET', 'Wrong LC live API identity'
    assert Path(tempfile.gettempdir()).resolve() == TEMP.resolve(), 'Redirected TEMP; refuse before booking'
    nonce = uuid.uuid4().hex[:16]
    volume = 'dyd415-rmi-bootstrap-' + nonce + '-jdk'
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__))])
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
                    retain_work()
                except BaseException as error:
                    save('retain-work-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                try:
                    cleaned = cleanup(children, volume, nonce)
                except BaseException as error:
                    save('teardown-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                if cleaned and lock.read_bytes() == lock_bytes: lock.unlink()
        save('secondary-released.json', dict(heavyReleased=True, tempLockReleased=not lock.exists()))
    save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited, cleanupConfirmed=cleaned))
    verify_inputs()
    save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True, productAndPayloadUnchanged=True))
    if failure: raise failure


if __name__ == '__main__':
    main()

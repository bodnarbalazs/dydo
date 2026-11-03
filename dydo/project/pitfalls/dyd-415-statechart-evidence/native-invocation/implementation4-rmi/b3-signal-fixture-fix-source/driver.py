"""One canonical affected signal test; source-only until captain inspection/release."""
import argparse
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
PACKET = BASE / 'b3-signal-fixture-fix-source'
OUTPUT = None
SIGNAL = None
_shared = BASE / 'b3-signal-containment-source/shared-containment.py'
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
    if release['state'] != 'RELEASED_B3_SIGNAL_FIXTURE_FIX' or SIGNAL not in release['signals'] or len(set(release['signals'])) != len(release['signals']) or not set(release['signals']).issubset({'SIGINT', 'SIGTERM'}) or release['sourcePinSha256'] != common.sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Exact captain source release required')
    if not release.get('captainInspectionRecord') or not release.get('dockerIdentity'):
        raise RuntimeError('Captain inspection and bound actual-host Docker identity required')
    if common.sha(Path(__file__)) != pin['driverSha256'] or common.sha(TEMP / 'dyd415-rmi-b3-signal-fixture-fix-launch-20261031.py') != pin['launcherSha256']:
        raise RuntimeError('TEMP counterpart identity differs')
    for row in pin['sources']:
        path = ROOT / row['path']
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Current source/custody differs: ' + row['path'])
    for layer in json.loads((PACKET / 'preservation-map.json').read_bytes())['layers']:
        for row in json.loads((ROOT / layer['manifest']).read_bytes()):
            path = ROOT / layer['beforeSnapshots'].get(row['path'], row['path'])
            if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
                raise RuntimeError('Preserved bytes differ: ' + row['path'])
    manifest = json.loads((PACKET / 'input-manifest.json').read_bytes())
    for row in manifest['retainedInputs']:
        path = Path(row['source'])
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Retained Node/checker DATA differs before booking')
    for name, expected in common.LC_PINS.items():
        if common.sha(LC / name) != expected:
            raise RuntimeError('LC bytes differ: ' + name)
    for row in pin['externalInputs']:
        path = Path(row['path'])
        if common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('External source/executable identity differs: ' + row['path'])
    for path in json.loads((PACKET / 'closure.json').read_bytes())['namespacePackageAbsences']:
        if Path(path).exists():
            raise RuntimeError('LC namespace import topology changed: ' + path)


# Narrow successor checks around the unchanged owned-container pattern.
DOCKER = Path('C:/Program Files/Docker/Docker/resources/bin/docker.exe')
EVIDENCE_ERRORS = []
_original_run = common.run


def safe_save(name, value):
    try:
        common.save(name, value)
    except BaseException as error:
        EVIDENCE_ERRORS.append(dict(record=name, error=type(error).__name__, message=str(error)))


def run(name, argv, timeout=20, expected=0):
    if argv[0] == 'docker':
        pinned = json.loads((PACKET / 'execution-release.json').read_bytes())['dockerIdentity']['contextName']
        # Select the already-current pinned context without changing config/environment.
        argv = [str(DOCKER), *([] if argv[1] == 'context' else ['--context', pinned]), *argv[1:]]
    return _original_run(name, argv, timeout=timeout, expected=expected)


common.run = run


def check_daemon(prefix):
    # Captain must bind an actual read-only host record before release, never infer an ID.
    expected = json.loads((PACKET / 'execution-release.json').read_bytes())['dockerIdentity']
    if any(key.startswith('DOCKER_') for key in os.environ):
        raise RuntimeError('Ambient Docker transport/context injection; do not change environment')
    if common.sha(DOCKER) != expected['executableSha256']:
        raise RuntimeError('Docker executable identity differs')
    context = run(prefix + '-context', ['docker', 'context', 'show']).strip()
    if context != expected['contextName']:
        raise RuntimeError('Default Docker context differs; no context switch permitted')
    view = json.loads(run(prefix + '-context-inspect', ['docker', 'context', 'inspect', context]))[0]
    endpoint = view['Endpoints']['docker']
    if endpoint != expected['dockerEndpoint'] or not endpoint['Host'].startswith('npipe:////./pipe/'):
        raise RuntimeError('Exact local Docker endpoint required; no remote/control transport')
    info = json.loads(run(prefix + '-daemon', ['docker', 'info', '--format', '{{json .}}']))
    for key, value in expected['daemonFields'].items():
        if info.get(key) != value:
            raise RuntimeError('Captain-bound daemon identity differs: ' + key)
    if set(expected['daemonFields']) != {'ID', 'Name', 'OSType', 'Architecture', 'ServerVersion', 'OperatingSystem', 'DockerRootDir'}:
        raise RuntimeError('Complete daemon identity fields required')
    if not all(expected['daemonFields'].values()):
        raise RuntimeError('No missing/synthetic daemon identity field accepted')
    if info['OSType'] != 'linux':
        raise RuntimeError('Linux daemon required')


def container(name, nonce, mounts, argv, children, entry='python'):
    check_daemon(name + '-before-create')
    args = ['docker', 'create', '--name', name, '--label', 'com.dydo.proof-owner=' + nonce,
        '--pull=never', '--network', 'none', '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
        '--pids-limit', '128', '--memory', '1g', '--cpus', '2', '--stop-timeout', '5',
        '--read-only', '--env', 'PYTHONDONTWRITEBYTECODE=1', '--env', 'TMPDIR=/work/tmp',
        '--entrypoint', entry, '--workdir', '/input']
    for mount in mounts:
        args += ['--mount', mount]
    args += [common.IMAGE, *argv]
    children.append((name, None))
    identity = run(name + '-create', args).strip()
    if len(identity) != 64 or any(char not in '0123456789abcdef' for char in identity):
        raise RuntimeError('Missing exact returned container ID; retain uncertainty')
    children[-1] = (name, identity)
    view = json.loads(run(name + '-inspect', ['docker', 'inspect', identity]))[0]
    if view['Id'] != identity or view['Name'] != '/' + name or view['Config']['Labels']['com.dydo.proof-owner'] != nonce:
        raise RuntimeError('Container ownership differs')
    if view['Image'] != common.IMAGE.split('@')[1] or view['Path'] != entry or view['Args'] != argv:
        raise RuntimeError('Exact cached image/entry/argv differs')
    config = view['HostConfig']
    if config['NetworkMode'] != 'none' or not config['ReadonlyRootfs'] or config['CapDrop'] != ['ALL']:
        raise RuntimeError('Containment network/root/capability differs')
    if config['SecurityOpt'] != ['no-new-privileges'] or config['Privileged'] or config.get('CapAdd'):
        raise RuntimeError('Privilege/security differs')
    if config['PidMode'] != '' or config['IpcMode'] != 'private' or config.get('UTSMode') or config.get('UsernsMode'):
        raise RuntimeError('Private PID/IPC/default namespace required, no container namespace sharing')
    if config['PidsLimit'] != 128 or config['Memory'] != 1073741824 or config['NanoCpus'] != 2000000000:
        raise RuntimeError('Resource bounds differ')
    if any(config.get(key) for key in ['Devices', 'DeviceRequests', 'DeviceCgroupRules', 'PortBindings', 'VolumesFrom', 'Links', 'ExtraHosts', 'Binds', 'Tmpfs', 'CgroupParent']):
        raise RuntimeError('Undeclared device/port/mount/host access')
    if config.get('Init') or config['AutoRemove'] or config['RestartPolicy']['Name'] != 'no':
        raise RuntimeError('No init/restart/auto-remove topology change permitted')
    if view['Config'].get('ExposedPorts') or view['Config'].get('Volumes'):
        raise RuntimeError('Undeclared image port/volume')
    expected_mounts = {}
    for mount in mounts:
        parts = dict(item.split('=', 1) for item in mount.split(',') if '=' in item)
        expected_mounts[parts['target']] = dict(kind=parts['type'], source=parts['source'], rw='readonly' not in mount.split(','))
    if set(expected_mounts) != {'/input', '/work', '/output', '/toolchain'} or len(view['Mounts']) != 4:
        raise RuntimeError('Only four exact owned mounts permitted')
    for row in view['Mounts']:
        expected = expected_mounts[row['Destination']]
        if row['Type'] != expected['kind'] or row['RW'] != expected['rw']:
            raise RuntimeError('Mount type/RW differs')
        if row['Type'] == 'bind' and Path(row['Source']).resolve() != Path(expected['source']).resolve():
            raise RuntimeError('Exact owned host bind source differs')
        if row['Type'] == 'volume' and row['Name'] != expected['source']:
            raise RuntimeError('Exact owned volume differs')
        if row.get('Propagation', '') not in ['', 'rprivate']:
            raise RuntimeError('Mount propagation differs')
    check_daemon(name + '-before-start')
    image = json.loads(run(name + '-image', ['docker', 'image', 'inspect', common.IMAGE]))[0]
    if image['Id'] != common.IMAGE.split('@')[1] or image['Os'] != 'linux' or image['Architecture'] != 'amd64':
        raise RuntimeError('Cached runtime identity differs before start')
    # Canonical case <25s of bounded waits; native30s; runner65+5s; attach120s emergency.
    run(name + '-start', ['docker', 'start', '--attach', identity], timeout=120)
    final = json.loads(run(name + '-state', ['docker', 'inspect', identity]))[0]
    if final['State']['Status'] != 'exited' or final['State']['ExitCode'] != 0 or final['State']['OOMKilled']:
        raise RuntimeError('Contained worker failed; product acceptance not inferred from cleanup')
    return identity


common.container = container


def emergency_cleanup(children, volume, nonce):
    # Same exact-ID inspect/stop/remove/absence flow, independent of evidence-write success.
    confirmed = True
    receipts = []
    def call(argv, allowed=(0,)):
        pinned = json.loads((PACKET / 'execution-release.json').read_bytes())['dockerIdentity']['contextName']
        command = [str(DOCKER), *([] if argv[0] == 'context' else ['--context', pinned]), *argv]
        receipt = dict(argv=command, timeoutSeconds=20, attempted=True)
        receipts.append(receipt)
        try:
            result = subprocess.run(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                timeout=20, creationflags=subprocess.CREATE_NO_WINDOW)
        except BaseException as error:
            receipt.update(error=type(error).__name__, message=str(error))
            raise
        if len(result.stdout) > 16 * 1024 * 1024 or len(result.stderr) > 16 * 1024 * 1024:
            raise RuntimeError('Containment response bound exceeded')
        receipt.update(exit=result.returncode,
            stdout=result.stdout.decode(errors='replace'), stderr=result.stderr.decode(errors='replace'))
        if result.returncode not in allowed:
            raise RuntimeError('Exact-owned containment command failed')
        return result.stdout.decode()
    # Recheck routing without log-dependent common.run. No daemon/context mutation or guessed target.
    try:
        expected = json.loads((PACKET / 'execution-release.json').read_bytes())['dockerIdentity']
        if any(key.startswith('DOCKER_') for key in os.environ) or common.sha(DOCKER) != expected['executableSha256']:
            raise RuntimeError('Cleanup transport/executable uncertainty')
        if call(['context', 'show']).strip() != expected['contextName']:
            raise RuntimeError('Cleanup default context uncertainty')
        endpoint = json.loads(call(['context', 'inspect', expected['contextName']]))[0]['Endpoints']['docker']
        if endpoint != expected['dockerEndpoint']:
            raise RuntimeError('Cleanup endpoint uncertainty')
        info = json.loads(call(['info', '--format', '{{json .}}']))
        if any(info.get(key) != value for key, value in expected['daemonFields'].items()):
            raise RuntimeError('Cleanup daemon identity uncertainty')
    except BaseException as error:
        safe_save('emergency-containment.json', dict(confirmed=False, error=str(error), receipts=receipts,
            productOutcome='NOT_INFERRED', tempOwnershipLockMustRemain=True))
        return False
    for name, identity in reversed(children):
        try:
            if identity is None:
                raise RuntimeError('No returned container ID; no name-based recovery/foreign cleanup')
            view = json.loads(call(['inspect', identity]))[0]
            if view['Id'] != identity or view['Name'] != '/' + name or view['Config']['Labels']['com.dydo.proof-owner'] != nonce:
                raise RuntimeError('Cleanup exact ownership mismatch')
            if view['State']['Running']:
                call(['stop', '--time', '5', identity])
            call(['rm', identity])
            if call(['ps', '-a', '--no-trunc', '--filter', 'id=' + identity, '--format', '{{.ID}}']).strip():
                raise RuntimeError('Owned container absence unconfirmed')
        except BaseException as error:
            confirmed = False
            receipts.append(dict(container=identity, name=name, uncertainty=str(error)))
    if volume is not None:
        try:
            names = call(['volume', 'ls', '--filter', 'name=' + volume, '--format', '{{.Name}}']).splitlines()
            if volume in names:
                view = json.loads(call(['volume', 'inspect', volume]))[0]
                if view['Name'] != volume or view['Labels']['com.dydo.proof-owner'] != nonce:
                    raise RuntimeError('Volume ownership differs')
                call(['volume', 'rm', volume])
            if volume in call(['volume', 'ls', '--filter', 'name=' + volume, '--format', '{{.Name}}']).splitlines():
                raise RuntimeError('Owned volume absence unconfirmed')
        except BaseException as error:
            confirmed = False
            receipts.append(dict(volume=volume, uncertainty=str(error)))
    safe_save('emergency-containment.json', dict(confirmed=confirmed, receipts=receipts,
        productOutcome='NOT_INFERRED', strictCleanCensusNotInferred=True,
        exactOwnedOnly=True, tempScratchRetained=True))
    return confirmed and not EVIDENCE_ERRORS


VOLUME = None


def execute(nonce, children):
    global VOLUME
    check_daemon('pre-resource')
    VOLUME = 'dyd415-rmi-b3-signal-fixture-fix-' + nonce + '-node'
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-signal-fixture-fix-', dir=TEMP))
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
            (ROOT / manifest['nodeMemberManifest']['path'], source / 'node-members.json'),
            (ROOT / manifest['foundation']['path'], source / 'foundation.py')]:
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
    common.container('dyd415-rmi-b3-signal-fixture-fix-' + nonce + '-extract', nonce,
        mounts + ['type=volume,source=' + VOLUME + ',target=/toolchain'],
        ['-B', '/input/worker.py', '--signal', SIGNAL, '--extract'], children)
    common.container('dyd415-rmi-b3-signal-fixture-fix-' + nonce + '-finite', nonce,
        mounts + ['type=volume,source=' + VOLUME + ',target=/toolchain,readonly'],
        ['-B', '/input/worker.py', '--signal', SIGNAL], children)
    common.save('summary.json', dict(product=json.loads((output / 'product-outcome.json').read_bytes()),
        worker=json.loads((output / 'completed.json').read_bytes()), infrastructureContainment='NOT_YET_ATTEMPTED'))


def cleanup(children, nonce):
    if VOLUME is None:
        common.save('teardown.json', dict(cleanupConfirmed=not children, noMaterialResourcesCreated=not children))
        return not children
    return emergency_cleanup(children, VOLUME, nonce)


def main():
    global SIGNAL, OUTPUT
    parser = argparse.ArgumentParser()
    parser.add_argument('--signal', choices=['SIGINT', 'SIGTERM'], required=True)
    SIGNAL = parser.parse_args().signal
    OUTPUT = BASE / ('b3-signal-fixture-fix-20261031a-' + SIGNAL)
    common.OUTPUT = OUTPUT
    verify_inputs()
    if not OUTPUT.is_dir() or (OUTPUT / 'preflight.json').exists():
        raise RuntimeError('Envelope output absent or already used')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner import machine_lock as lock_module
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    if Path(suite_slot.__file__).resolve() != (LC / 'tools/testing/runner/suite_slot.py').resolve() or Path(lock_module.__file__).resolve() != (LC / 'tools/testing/runner/machine_lock.py').resolve():
        raise RuntimeError('Actual LC import origin differs')
    live = suite_slot.live()
    if live.directory.resolve() != Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve() or live.prefix != 'LC-Suite-Slot' or live.variable != 'LC_SUITE_SLOT_TICKET':
        raise RuntimeError('Wrong default live LC namespace')
    common.save('preflight.json', dict(namespace=str(live.directory), localAppData=os.environ.get('LOCALAPPDATA'),
        temp=str(tempfile.gettempdir()), sourcePinSha256=common.sha(PACKET / 'source-pin.json'), lcPins=common.LC_PINS))
    nonce = uuid.uuid4().hex[:16]
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__)), '--signal', SIGNAL])
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
                safe_save('stop.json', dict(type=type(error).__name__, message=str(error),
                    productOutcome='FAIL_OR_INCONCLUSIVE', infrastructureContainment='PENDING'))
            finally:
                try:
                    cleaned = cleanup(children, nonce)
                except BaseException as error:
                    safe_save('teardown-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                try:
                    common.retain_work()
                except BaseException as error:
                    safe_save('retain-work-error.json', dict(type=type(error).__name__, message=str(error)))
                    failure = failure or error
                if cleaned and not EVIDENCE_ERRORS and lock.read_bytes() == lock_bytes:
                    lock.unlink()
        common.save('secondary-released.json', dict(heavyReleased=True, tempLockReleased=not lock.exists()))
    common.save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited, cleanupConfirmed=cleaned))
    verify_inputs()
    common.save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True,
        admittedCorrectedSourceUnchanged=True, payloadUnchanged=True))
    if EVIDENCE_ERRORS:
        raise RuntimeError('Evidence failure retained; no product/containment PASS: ' + repr(EVIDENCE_ERRORS))
    if not cleaned:
        raise RuntimeError('Exact-owned absence unconfirmed; ownership lock retained')
    if failure:
        raise failure


if __name__ == '__main__':
    main()

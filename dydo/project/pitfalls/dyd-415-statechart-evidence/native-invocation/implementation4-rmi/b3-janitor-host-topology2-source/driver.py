"""Two finite host choices; source only until exact captain inspection/release."""
import argparse
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import uuid

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
PACKET = BASE / 'b3-janitor-host-topology2-source'
PYTHON = Path('C:/Users/User/AppData/Local/Programs/Python/Python313/python.exe')
NODE = Path('C:/Users/User/AppData/Local/fnm_multishells/10620_1793288379755/node.exe')
OUTPUT = None
SELECTION = None
CHILD = None
CLEANED = True
EVIDENCE_ERRORS = []
topology = None
_shared = BASE / 'b3-signal-containment-source/shared-containment.py'
if hashlib.sha256(_shared.read_bytes()).hexdigest() != '24f0afe8f437cef9dc0fc8a3d5cf98540f39af0bf6f45e129f12c2e53549c100':
    raise RuntimeError('Inspected shared source differs before import')
_spec = importlib.util.spec_from_file_location('_topology2_retained_common', _shared)
common = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(common)


def validate_host():
    if os.name != 'nt' or sys.platform != 'win32' or Path(sys.executable).resolve() != PYTHON.resolve():
        raise RuntimeError('Exact commissioned Windows Python host required')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve() or Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected host namespace; refuse before booking')


def validate_release():
    pin = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    if release.get('state') != 'RELEASED_B3_JANITOR_HOST_TOPOLOGY2' or release.get('selection') != SELECTION or release.get('sourcePinSha256') != common.sha(PACKET / 'source-pin.json'):
        raise RuntimeError('One exact captain source/selector release required')
    if release.get('once') is not True or not release.get('captainInspectionRecord'):
        raise RuntimeError('Finite inspected once-only release required')
    if SELECTION == 'TYPECHECK' and (release.get('helper12GreenReceiptSha256') !=
            'f5790b2123c890a458b3146bab1fe17591acc83912aa023ce87410ea6da40515'
            or not re.fullmatch(r'[0-9a-f]{64}', release.get('topologyGreenReceiptSha256', ''))):
        raise RuntimeError('Applicable helper12 and new topology GREEN bindings required')
    if release.get('nativeArgv') != native_argv():
        raise RuntimeError('Release must bind the exact native command')
    if OUTPUT is not None and (OUTPUT / 'execution-release-at-run.json').exists() and (OUTPUT / 'execution-release-at-run.json').read_bytes() != (PACKET / 'execution-release.json').read_bytes():
        raise RuntimeError('Administrative release changed during this invocation')
    return pin


def check_rows(rows, aliases=None):
    for row in rows:
        path = ROOT / (aliases or {}).get(row['path'], row['path'])
        if not path.is_file() or path.is_symlink() or common.sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('Source/custody identity differs: ' + row['path'])


def verify_inputs():
    validate_host()
    pin = validate_release()
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, timeout=20,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()
    if head != '6e3fe7e8018130af6b8586ef0cf40c1474740864':
        raise RuntimeError('HEAD differs from retained draft')
    if common.sha(Path(__file__)) != pin['driverSha256'] or common.sha(PACKET / 'launcher.py') != pin['launcherSha256']:
        raise RuntimeError('Host counterpart identity differs')
    global topology
    required = [str((PACKET / name).relative_to(ROOT)).replace('\\', '/')
        for name in ['topology.py', 'topology-manifest.json', 'ownership-evidence.json']]
    if [row['path'] for row in pin['topologyInputs']] != required:
        raise RuntimeError('Exact topology data/module identities required')
    check_rows(pin['topologyInputs'])
    if topology is None:
        spec = importlib.util.spec_from_file_location('_dyd415_topology2', PACKET / 'topology.py')
        topology = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(topology)
    manifest, ownership = topology_inputs()
    topology.compiler_inventory(ROOT, manifest, ownership)
    topology.workspace_paths(ROOT, OUTPUT, manifest)
    check_rows(pin['sources'])
    check_rows(pin['externalInputs'])
    check_rows([pin['afterCustody']])
    preservation = json.loads((PACKET / 'preservation-map.json').read_bytes())
    for layer in preservation['layers']:
        data = json.loads((ROOT / layer['manifest']).read_bytes())
        check_rows(data[layer['rowsKey']] if layer.get('rowsKey') else data, layer['beforeSnapshots'])
    for name, expected in common.LC_PINS.items():
        if common.sha(LC / name) != expected:
            raise RuntimeError('LC input differs: ' + name)
    for path in json.loads((PACKET / 'closure.json').read_bytes())['namespacePackageAbsences']:
        if Path(path).exists():
            raise RuntimeError('LC namespace topology changed')


def safe_save(name, value):
    try:
        common.save(name, value)
    except BaseException as error:
        EVIDENCE_ERRORS.append(dict(record=name, error=type(error).__name__, message=str(error)))


def native_argv():
    if SELECTION == 'TOPOLOGY':
        return [PYTHON.as_posix(), '-I', '-B', '-m', 'unittest', 'discover', '-v',
            '-s', str(PACKET.relative_to(ROOT)).replace('\\', '/'), '-p', 'test_topology_contracts.py']
    if SELECTION == 'TYPECHECK':
        return [NODE.as_posix(), 'skills/engineering/statechart/scripts/node_modules/typescript/bin/tsc',
            '--noEmit', '--project', 'skills/engineering/statechart/scripts/tsconfig.json']
    raise RuntimeError('No generic native command selection')


def child_environment():
    env = os.environ.copy()
    if SELECTION == 'TYPECHECK':
        rejected = sorted(key for key in env if key.upper().startswith(
            ('NODE_', 'ELECTRON_', 'TS_NODE_', 'TSX_', 'BABEL_')) or
            key.upper() in {'OPENSSL_CONF', 'OPENSSL_MODULES', 'ICU_DATA'})
        if rejected:
            raise RuntimeError('Ambient compiler injection refused (names only): ' + repr(rejected))
        env['NODE_DISABLE_COMPILE_CACHE'] = '1'
    return env


def topology_inputs():
    return (json.loads((PACKET / 'topology-manifest.json').read_bytes()),
        json.loads((PACKET / 'ownership-evidence.json').read_bytes()))


def scripts_inventory():
    manifest, ownership = topology_inputs()
    return topology.scripts_inventory(ROOT, manifest, ownership)


def workspace_paths():
    manifest, _ = topology_inputs()
    return topology.workspace_paths(ROOT, OUTPUT, manifest)


def run_native():
    global CHILD, CLEANED
    argv = native_argv()
    env = child_environment()
    common.save('native.argv.json', dict(argv=argv, cwd=str(ROOT), hidden=True, deadlineSeconds=180,
        directReapSeconds=5, postCompletionLimitBytes=16 * 1024 * 1024,
        hardCap=False, job=False, networkNone=False, descendantContainment=False,
        childEnvDelta={'NODE_DISABLE_COMPILE_CACHE': '1'} if SELECTION == 'TYPECHECK' else {},
        ticket=os.environ.get('LC_SUITE_SLOT_TICKET')))
    failure = None
    before, paths_before = None, None
    if SELECTION == 'TYPECHECK':
        before, paths_before = scripts_inventory(), workspace_paths()
        expected_files = json.loads((PACKET / 'input-manifest.json').read_bytes())['compilerWholeScriptsBefore']
        actual_files = [{key: row[key] for key in ['path', 'bytes', 'sha256']}
            for row in before if row['kind'] == 'file']
        if sorted(actual_files, key=lambda row: row['path']) != sorted(expected_files, key=lambda row: row['path']):
            raise RuntimeError('Compiler complete input path/member closure differs before start')
        common.save('scripts-before.json', before)
        common.save('workspace-paths-before.json', paths_before)
    try:
        with (OUTPUT / 'native.stdout').open('xb') as stdout, (OUTPUT / 'native.stderr').open('xb') as stderr:
            CHILD = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=stdout, stderr=stderr,
                creationflags=subprocess.CREATE_NO_WINDOW)
            CLEANED = False
            common.save('native.owned.json', dict(pid=CHILD.pid, directPopenOwned=True, argv=argv))
            CHILD.wait(timeout=180)
            CLEANED = True
    except BaseException as error:
        failure = error
    finally:
        if CHILD is not None and not CLEANED:
            try:
                if CHILD.poll() is None:
                    CHILD.kill()
                CHILD.wait(timeout=5)
                CLEANED = True
            except BaseException as error:
                safe_save('native-drain-error.json', dict(error=type(error).__name__, message=str(error)))
                failure = failure or error
        sizes = {suffix: (OUTPUT / ('native.' + suffix)).stat().st_size
            for suffix in ['stdout', 'stderr'] if (OUTPUT / ('native.' + suffix)).is_file()}
        if any(size > 16 * 1024 * 1024 for size in sizes.values()):
            failure = failure or RuntimeError('Post-completion output bound exceeded; NOT a hard cap')
        safe_save('native.result.json', dict(exit=CHILD.returncode if CHILD else None,
            outputBytes=sizes, postCompletionLimitBytes=16 * 1024 * 1024,
            directChildReaped=CLEANED, error=type(failure).__name__ if failure else None,
            message=str(failure) if failure else None, timeoutRemainsFailure=isinstance(failure, subprocess.TimeoutExpired),
            descendantAbsenceNotClaimed=True))
        if SELECTION == 'TYPECHECK' and before is not None:
            try:
                after, paths_after = scripts_inventory(), workspace_paths()
                common.save('scripts-after.json', after)
                common.save('workspace-paths-after.json', paths_after)
                if before != after or paths_before != paths_after:
                    raise RuntimeError('Compiler input drift or unexpected emission; diagnostic FAIL')
            except BaseException as error:
                EVIDENCE_ERRORS.append(dict(record='compiler-custody', error=type(error).__name__, message=str(error)))
                safe_save('nonemission-error.json', dict(error=type(error).__name__, message=str(error)))
                failure = failure or error
    if failure:
        raise failure
    if not CLEANED or CHILD is None or CHILD.returncode != 0:
        raise RuntimeError('Native nonzero or uncertain cleanup; first failure retained')
    if SELECTION == 'TOPOLOGY':
        if (OUTPUT / 'native.stdout').stat().st_size:
            raise RuntimeError('Topology stdout must be empty')
        stderr = (OUTPUT / 'native.stderr').read_text(encoding='utf-8')
        names = json.loads((PACKET / 'test-inventory.json').read_bytes())['nativeIds']
        actual = []
        for line in stderr.splitlines():
            if ' ... ' in line:
                name, result = line.rsplit(' ... ', 1)
                if result != 'ok':
                    raise RuntimeError('Topology non-ok/skip retained')
                actual.append(name)
        if len(names) != 17 or sorted(actual) != sorted(names) or len(set(actual)) != 17:
            raise RuntimeError('Exact unique seventeen topology native IDs required')
        if len(re.findall(r'^Ran 17 tests in [^\r\n]+s$', stderr, re.M)) != 1 or len(re.findall(r'^OK$', stderr, re.M)) != 1:
            raise RuntimeError('Complete native Ran17/OK summary required')
        common.save('native.selection.json', dict(actual=actual, unique=17, skipped=0, stdoutBytes=0))
    common.save('summary.json', dict(selection=SELECTION, nativeExit=0, topologyCount=17 if SELECTION == 'TOPOLOGY' else None,
        diagnosticOnly=SELECTION == 'TYPECHECK', compilerNonEmissionChecked=SELECTION == 'TYPECHECK',
        newHostDispatchDirectAssurance='OWED', realJava=False, productExecution=False))


def retain_and_release(lock, lock_bytes, cleaned, failure):
    retained = False
    try:
        common.retain_work()
        rows = []
        for path in sorted(OUTPUT.rglob('*')):
            if path.is_symlink() or path.is_junction():
                raise RuntimeError('Evidence topology uncertainty')
            if path.is_file():
                rows.append(dict(path=path.relative_to(OUTPUT).as_posix(), bytes=path.stat().st_size, sha256=common.sha(path)))
        common.save('retention.json', dict(files=rows, evidenceReadable=True, directCleanup=cleaned,
            lockOwnerSha256=hashlib.sha256(lock_bytes).hexdigest(), releasePrepared=True))
        retained = True
    except BaseException as error:
        safe_save('retain-work-error.json', dict(type=type(error).__name__, message=str(error)))
        failure = failure or error
    if cleaned and retained and not EVIDENCE_ERRORS:
        try:
            if lock.read_bytes() != lock_bytes:
                raise RuntimeError('Exact owner bytes differ; no lock release')
            lock.unlink()
        except BaseException as error:
            safe_save('lock-release-error.json', dict(type=type(error).__name__, message=str(error)))
            failure = failure or error
    return failure


def main():
    global SELECTION, OUTPUT
    parser = argparse.ArgumentParser()
    selected = parser.add_mutually_exclusive_group(required=True)
    selected.add_argument('--topology', action='store_true')
    selected.add_argument('--typecheck', action='store_true')
    args = parser.parse_args()
    SELECTION = 'TOPOLOGY' if args.topology else 'TYPECHECK'
    OUTPUT = BASE / ('b3-janitor-host-topology2-20261031a-' + SELECTION)
    common.OUTPUT = OUTPUT
    verify_inputs()
    if not OUTPUT.is_dir() or (OUTPUT / 'preflight.json').exists():
        raise RuntimeError('Fresh launcher-owned output required')
    with (OUTPUT / 'execution-release-at-run.json').open('xb') as stream:
        stream.write((PACKET / 'execution-release.json').read_bytes())
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
    command = subprocess.list2cmdline([sys.executable, '-I', '-B', str(Path(__file__)), '--' + SELECTION.lower()])
    lock, lock_bytes, failure = TEMP / 'dydo-fullsuite.lock', None, None
    try:
        with suite_slot.book(command, worktree=ROOT) as booking:
            owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'], nonce=nonce,
                namespace=str(live.directory), bookingOwned=booking.owned, inherited=booking.inherited,
                startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
            common.save('admission.json', owner)
            with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
                candidate = (json.dumps(owner, sort_keys=True) + '\n').encode()
                with lock.open('xb') as stream:
                    lock_bytes = candidate
                    stream.write(lock_bytes)
                try:
                    verify_inputs()
                    run_native()
                except BaseException as error:
                    failure = error
                    safe_save('stop.json', dict(type=type(error).__name__, message=str(error), selection=SELECTION))
                finally:
                    try:
                        verify_inputs()
                        common.save('post-custody.json', dict(sourceAndHistoricalPinsMatch=True, headUnchanged=True))
                    except BaseException as error:
                        EVIDENCE_ERRORS.append(dict(record='post-custody', error=type(error).__name__, message=str(error)))
                        safe_save('post-custody-error.json', dict(type=type(error).__name__, message=str(error)))
                        failure = failure or error
            common.save('secondary-released.json', dict(heavyReleased=True, tempOwnershipStillHeld=lock_bytes is not None))
        common.save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited,
            directChildReaped=CLEANED, tempOwnershipStillHeld=lock_bytes is not None))
    except BaseException as error:
        failure = failure or error
        EVIDENCE_ERRORS.append(dict(record='admission-or-context-finalization', error=type(error).__name__, message=str(error)))
        safe_save('admission-or-finalization-error.json', dict(type=type(error).__name__, message=str(error)))
    finally:
        if lock_bytes is not None:
            # TEMP owner is deliberately released last, after context and evidence writes.
            safe_save('driver-verdict.json', dict(exit=1 if failure or EVIDENCE_ERRORS or not CLEANED else 0,
                failure=type(failure).__name__ if failure else None, message=str(failure) if failure else None,
                evidenceErrors=EVIDENCE_ERRORS, directChildReaped=CLEANED))
            failure = retain_and_release(lock, lock_bytes, CLEANED, failure)
    if failure:
        return 1
    if EVIDENCE_ERRORS or not CLEANED or lock_bytes is None:
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())

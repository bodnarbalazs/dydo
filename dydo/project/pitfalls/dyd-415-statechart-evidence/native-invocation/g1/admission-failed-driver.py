"""Bounded first native gate; default live FIFO, then Heavy, then the owned dydo lock."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
BASE = '3527ef642d9c0e1a4298cd13f22a53cf8a6a1a15'
EVIDENCE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation'
LC_HASHES = {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4',
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, value):
    with path.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def git(*arguments):
    return subprocess.check_output(['git', *arguments], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()


def source_paths():
    paths = [path for directory in ['Services/Statechart', 'DynaDocs.Tests/Statechart']
             for path in (ROOT / directory).glob('*.cs')]
    paths += list((EVIDENCE / 'g1/fixture').glob('*'))
    paths += [Path(__file__), EVIDENCE / 'g1_cases.py', ROOT / 'Directory.Build.props',
              ROOT / '.editorconfig', ROOT / 'DynaDocs.Tests/coverage/SonarLint.xml']
    return sorted(paths)


def snapshot(output):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-native-' + output.name + '-', dir=TEMP))
    source = scratch / 'source'
    identities = []
    for path in source_paths():
        relative = path.relative_to(ROOT)
        destination = source / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
        identities.append(dict(path=relative.as_posix(), sha256=sha(path)))
    save(output / 'inputs.json', dict(base=BASE, sourceHead=git('rev-parse', 'HEAD'),
        dirty=git('status', '--porcelain=v1', '--untracked-files=all'), scratch=str(scratch),
        files=identities, driverSha256=sha(Path(__file__))))
    return scratch, source


def environment(scratch):
    return dict(os.environ, DOTNET_CLI_HOME=str(scratch / 'dotnet-home'),
        DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
        DOTNET_NOLOGO='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1',
        NUGET_PACKAGES=str(scratch / 'nuget'), PYTHONDONTWRITEBYTECODE='1',
        TEMP=str(scratch / 'temp'), TMP=str(scratch / 'temp'), TMPDIR=str(scratch / 'temp'))


def run(output, name, command, cwd, env, timeout=120):
    started = time.monotonic()
    print(name + ': starting', flush=True)
    with (output / (name + '.stdout')).open('xb') as stdout:
        with (output / (name + '.stderr')).open('xb') as stderr:
            result = subprocess.run(command, cwd=cwd, env=env, stdout=stdout, stderr=stderr,
                creationflags=subprocess.CREATE_NO_WINDOW, timeout=timeout)
    save(output / (name + '.command.json'), dict(argv=command, cwd=str(cwd),
        exit=result.returncode, seconds=time.monotonic() - started, hidden=True,
        ticket=os.environ['LC_SUITE_SLOT_TICKET']))
    print(name + ': ' + str(result.returncode), flush=True)
    return result.returncode


def preflight(output, scratch, source, env):
    if run(output, 'dotnet-info', ['dotnet', '--info'], source, env):
        raise RuntimeError('dotnet-info failed; inspect retained output')
    vswhere = Path('C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe')
    compiler = dict(vswhereExists=vswhere.is_file(), clOnPath=shutil.which('cl.exe'))
    if vswhere.is_file():
        compiler['exit'] = run(output, 'native-toolchain', [str(vswhere), '-latest',
            '-products', '*', '-requires', 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64',
            '-property', 'installationPath'], source, env)
    docker = shutil.which('docker')
    available = False
    if docker:
        available = run(output, 'docker-server', [docker, 'version', '--format',
            '{{json .Server}}'], source, env, timeout=30) == 0
    save(output / 'preflight.json', dict(nativeToolchain=compiler,
        dockerExecutable=docker, dockerServerAvailable=available,
        runtimeArchivesInstalled=False, materialTestsExecuted=False,
        sourceSnapshot=str(source), scratch=str(scratch)))


def publish(output, scratch, source, env):
    fixture = source / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/g1/fixture/Fixture.csproj'
    command = ['dotnet', 'publish', str(fixture), '-c', 'Release', '-r', 'win-x64',
        '--self-contained', '-p:RepositoryRoot=' + str(source),
        '-o', str(scratch / 'publish'), '--warnaserror']
    if run(output, 'aot-publish', command, source, env, timeout=600):
        raise RuntimeError('NativeAOT publish failed; stop dependent work and inspect retained output')
    binary = scratch / 'publish/Fixture.exe'
    save(output / 'aot-binary.json', dict(path=str(binary), sha256=sha(binary),
        bytes=binary.stat().st_size, sourceInputs='inputs.json', nativeAot=True))
    command = [sys.executable, '-B', str(source /
        'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/g1_cases.py'),
        str(binary), str(scratch / 'cases'), str(output / 'native-outcomes.json')]
    if run(output, 'native-cases', command, source, env, timeout=120):
        raise RuntimeError('Native ownership counterexample failed; stop dependent work')


def validate_source():
    assert git('branch', '--show-current') == 'DYD-415-statechart'
    assert Path(git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    assert git('rev-parse', 'HEAD') == BASE
    changed = git('status', '--porcelain=v1', '--untracked-files=all').splitlines()
    prefixes = ('Services/Statechart/', 'DynaDocs.Tests/Statechart/',
                'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/')
    assert all(line[3:].replace('\\', '/').startswith(prefixes) for line in changed), changed
    old = json.loads((TEMP / 'dyd415-native-integration-after-20261030.json').read_bytes())
    assert all(sha(ROOT / name) == expected for name, expected in old['workingHashes'].items())
    for name, expected in LC_HASHES.items():
        assert sha(LC / name) == expected, 'Commissioned LC file changed: ' + name


def execute(mode, output, owner):
    sys.path.insert(0, str(ROOT / 'DynaDocs.Tests/coverage'))
    from run_tests import contain_descendants, end_descendants
    from windows_job import bindings, members
    job = contain_descendants()
    try:
        scratch, source = snapshot(output)
        env = environment(scratch)
        (scratch / 'temp').mkdir()
        {'preflight': preflight, 'g1': publish}[mode](output, scratch, source, env)
    finally:
        end_descendants(job)
        remaining = [pid for pid in members(bindings(), job) if pid != os.getpid()]
        save(output / 'teardown.json', dict(remainingOwnedProcesses=remaining,
            cleanupConfirmed=not remaining, scratchRetained=True, **owner))
        if remaining:
            raise RuntimeError('Owned descendant teardown unconfirmed; retain locks and evidence')


def main():
    mode = sys.argv[1]
    assert mode in ('preflight', 'g1')
    validate_source()
    output = EVIDENCE / 'g1' / mode
    output.mkdir(exist_ok=False)
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__)), mode])
    failure = None
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
            namespace=str(suite_slot.live().directory), owned=booking.owned,
            inherited=booking.inherited, lcReadOnly=True, noPycache=True, lcHashes=LC_HASHES,
            startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        save(output / 'admission.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            lock_bytes = (json.dumps(owner, sort_keys=True) + '\n').encode()
            with lock.open('xb') as stream:
                stream.write(lock_bytes)
            try:
                execute(mode, output, owner)
            except BaseException as error:
                failure = error
                save(output / 'stop.json', dict(type=type(error).__name__, reason=str(error)))
            finally:
                teardown = json.loads((output / 'teardown.json').read_bytes())
                if teardown['cleanupConfirmed'] and lock.read_bytes() == lock_bytes:
                    lock.unlink()
        save(output / 'secondary-released.json', dict(heavyReleased=True,
            dydoReleased=not lock.exists(), **owner))
    save(output / 'released.json', dict(fifoReleased=booking.owned,
        inheritedReturned=booking.inherited, **owner))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

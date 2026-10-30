"""TEMP supervisor: I1 RED, focused rejection proof, final docs/whitespace only."""
import hashlib
import importlib.util
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
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/Desktop/LC')
E = 'dydo/project/pitfalls/dyd-415-statechart-evidence/'
OUT = ROOT / E / 'integration3'
START = 'bc5f120b6da58670def58e6403e55feb7fcc0267'
MASTER = 'f3e0a161886621395f2dba47b689fba19f3bf3de'
POINTER = 'dydo/project/pitfalls/dyd-415-statechart-verification.md'
LC_HASHES = {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': '1a15e761a905a1df77c08dc9ed4c851b43da5310d22f3dec13247db1d7098e28',
}
os.environ.update(LOCALAPPDATA=str(TEMP.parent), TEMP=str(TEMP), TMP=str(TEMP),
    TMPDIR=str(TEMP), PYTHONDONTWRITEBYTECODE='1')
sys.path.insert(0, str(LC))
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(name, value):
    path = OUT / name
    assert not path.exists(), 'Never overwrite an attempt: ' + name
    path.write_bytes((json.dumps(value, indent=2) + '\n').encode())


def git(*args, cwd=ROOT):
    return subprocess.check_output(['git', *args], cwd=cwd,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().rstrip('\r\n')


def owned(path):
    return path == POINTER or path.startswith(E + 'integration3/')


def stage():
    assert all(owned(p[3:]) for p in git('status', '--porcelain=v1', '--untracked-files=all').splitlines())
    git('add', '--', POINTER, E + 'integration3')
    assert all(owned(p) for p in git('diff', '--name-only', START).splitlines())


def run(name, argv, env=None):
    assert not (OUT / (name + '.command.json')).exists(), 'No automatic retry'
    started = time.time()
    result = subprocess.run(list(map(str, argv)), cwd=ROOT, env=env,
        capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
    (OUT / (name + '.stdout')).write_bytes(result.stdout)
    (OUT / (name + '.stderr')).write_bytes(result.stderr)
    save(name + '.command.json', dict(argv=list(map(str, argv)), cwd=str(ROOT),
        sourceHead=git('rev-parse', 'HEAD'), exit=result.returncode,
        seconds=time.time() - started, hidden=True, ticket=os.environ['LC_SUITE_SLOT_TICKET']))
    print(name, result.returncode, flush=True)
    assert result.returncode == 0, name + ' failed; stop and inspect retained streams'


def retained():
    history = json.loads((OUT / 'historical-hashes.json').read_bytes())['files']
    assert all(sha(ROOT / r['path']) == r['sha256'] for r in history), 'Historical byte drift'
    rows = json.loads((OUT / 'dependencies.json').read_bytes())['files']
    for row in rows:
        path = ROOT / row['path'] if row['location'] == 'source' else (
            TEMP / 'dyd415-fix2' / row['path'] if row['location'] == 'metric' else Path(row['path']))
        assert path.is_file() and sha(path) == row['sha256'], 'Retained dependency: ' + row['path']
    assert git('show', '-s', '--format=%P', '590f2204').split() == [
        'b08ab4417aeb7fba61dd2e559379960a79b6b53d', MASTER]
    assert git('rev-parse', 'HEAD:skills/engineering/statechart') == '10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d'
    for p in ['skills/README.md', 'skills/engineering/README.md']:
        assert not git('diff', START, '--', p), 'Catalog drift'
    return dict(historicalFiles=len(history), dependenciesMatched=len(rows), rawBytesUnchanged=True,
        runtimeTree='10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d', catalogs='41/13 unchanged')


def snapshot(phase):
    stage()
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-integration3-' + phase + '-', dir=TEMP))
    source = scratch / 'source'
    source.mkdir()
    paths = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().split('\0')
    for path in filter(None, paths):
        dest = source / path
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes((ROOT / path).read_bytes())
    git('init', cwd=source)
    git('config', 'core.autocrlf', 'false', cwd=source)
    git('add', '-f', '-A', cwd=source)
    commit(source, '.')
    tools = {}
    metric = scratch / 'metric'
    rows = json.loads((OUT / 'dependencies.json').read_bytes())['files']
    for index, row in enumerate(rows):
        if row['location'] == 'source':
            continue
        original = TEMP / 'dyd415-fix2' / row['path'] if row['location'] == 'metric' else Path(row['path'])
        dest = metric / row['path'] if row['location'] == 'metric' else scratch / 'tools' / (str(index) + '.bin')
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, dest)
        if row['location'] == 'absolute':
            tools[row['path']] = str(dest)
    (scratch / 'tools-map.json').write_bytes((json.dumps(tools, indent=2) + '\n').encode())
    save(phase + '-input.json', dict(sourceHead=START, scratch=str(scratch),
        sourceSnapshotHead=git('rev-parse', 'HEAD', cwd=source),
        sourceSnapshotTree=git('rev-parse', 'HEAD^{tree}', cwd=source),
        copiedIndexPaths=len(list(filter(None, paths))),
        trackedSnapshotPaths=len(git('ls-files', cwd=source).splitlines()),
        measurementExecuted=False, recipeInputs={p.relative_to(OUT).as_posix(): sha(p)
            for p in OUT.rglob('*') if p.is_file() and (p.suffix == '.py' or p.name == 'dependencies.json')}))
    return scratch, source, metric, tools


def commit(source, path):
    git('add', '-f', '-A', '--', str(path), cwd=source)
    git('-c', 'user.name=DYD415 scratch proof', '-c', 'user.email=proof@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '-m', 'Owned clean I1 probe snapshot', cwd=source)


def historical_red():
    scratch, source, metric, tools = snapshot('red')
    module_path = ROOT / E / 'integration2/recipe/config.py'
    spec = importlib.util.spec_from_file_location('historical_config', module_path)
    old = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(old)
    rows = json.loads((OUT / 'dependencies.json').read_bytes())['files']
    row = next(r for r in rows if r['group'] == 'ts-producer')
    path = source / row['path']
    original = path.read_bytes()
    outcomes = []
    initial_tree = git('rev-parse', 'HEAD^{tree}', cwd=source)
    for mutation in ['changed', 'missing']:
        try:
            path.write_bytes(original + b'\nowned I1 RED\n') if mutation == 'changed' else path.unlink()
            commit(source, path)
            assert not git('status', '--porcelain=v1', cwd=source)
            result = old.preflight(source, scratch / 'work', scratch / 'out', TEMP, metric, tools)
            assert len(result['checked']) == 476 and result['measurementExecuted'] is False
            outcomes.append(dict(path=row['path'], mutation=mutation, historicalResult='accepted',
                head=result['head'], clean=True, expectedRejectionAssertion=False,
                red='I1 reproduced: historical preflight accepts clean relevant drift'))
        finally:
            path.write_bytes(original)
            commit(source, path)
    assert git('rev-parse', 'HEAD^{tree}', cwd=source) == initial_tree
    assert not git('status', '--porcelain=v1', cwd=source)
    save('historical-red.json', dict(outcomes=outcomes, observations=len(outcomes),
        predicateSha256=sha(module_path), scratch=str(scratch), restoredClean=True,
        initialAndFinalTree=initial_tree, measurementExecuted=False))


def probes():
    scratch, source, metric, tools = snapshot('probes')
    env = dict(os.environ, DYD415_PROBE_SOURCE=str(source), DYD415_PROBE_ROOT=str(scratch))
    run('recipe-tests', [sys.executable, OUT / 'recipe/test_recipe.py'], env)
    shutil.copy2(scratch / 'recipe-outcomes.json', OUT / 'recipe-outcomes.json')
    run('recipe-preflight', [sys.executable, OUT / 'recipe/driver.py', '--source', source,
        '--work', scratch / 'fresh-work', '--out', scratch / 'fresh-out', '--normal-temp', TEMP,
        '--metric', metric, '--lc', LC, '--tools-map', scratch / 'tools-map.json'])
    shutil.copy2(scratch / 'fresh-out/preflight.json', OUT / 'recipe-preflight.json')
    save('probe-restored.json', dict(sourceClean=not git('status', '--porcelain=v1', cwd=source),
        scratchHead=git('rev-parse', 'HEAD', cwd=source),
        scratchTree=git('rev-parse', 'HEAD^{tree}', cwd=source), scratch=str(scratch),
        measurementExecuted=False, **retained()))


def final_checks():
    save('document-input.json', dict(files={p: sha(ROOT / p) for p in [
        E + 'integration3/verification.md', POINTER]}))
    run('docs', [shutil.which('dydo.exe'), 'check'])
    stage()
    run('whitespace-correction', ['git', 'diff', '--check', START])
    stage()
    run('whitespace-ticket', ['git', 'diff', '--check', MASTER])
    save('final-identities.json', retained())


def main():
    phase = sys.argv[1]
    assert phase in ['red', 'probes', 'final']
    assert git('rev-parse', 'HEAD') == START
    assert git('branch', '--show-current') == 'DYD-415-statechart'
    assert Path(git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    assert all(sha(LC / p) == expected for p, expected in LC_HASHES.items()), 'Replacement queue/docs changed'
    command = subprocess.list2cmdline([sys.executable, __file__, phase])
    failure = None
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(phase=phase, pid=os.getpid(), command=command,
            owned=booking.owned, inherited=booking.inherited,
            ticket=os.environ['LC_SUITE_SLOT_TICKET'], namespace=str(suite_slot.live().directory),
            driverSha256=sha(Path(__file__)), sourceHead=START, lcRoot=str(LC), lcReadOnly=True,
            noPycache=True, lcHashes=LC_HASHES)
        save(phase + '-reservation.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            with lock.open('x') as stream:
                json.dump(owner, stream)
            save(phase + '-locks.json', owner)
            try:
                retained()
                {'red': historical_red, 'probes': probes, 'final': final_checks}[phase]()
            except BaseException as error:
                save(phase + '-stop.json', dict(type=type(error).__name__, message=str(error)))
                failure = error
            finally:
                lock.unlink()
        save(phase + '-secondary-released.json', dict(**owner, heavyReleased=True, dydoReleased=True))
    save(phase + '-released.json', dict(**owner, fifoReleased=booking.owned,
        inheritedReturned=booking.inherited, heavyReleased=True, dydoReleased=True))
    if phase == 'final' and failure is None:
        save('final-result.json', dict(gates={name: json.loads((OUT / (name + '.command.json')).read_bytes())['exit']
            for name in ['docs', 'whitespace-correction', 'whitespace-ticket']},
            documentInputsUnchanged=all(sha(ROOT / p) == expected for p, expected in
                json.loads((OUT / 'document-input.json').read_bytes())['files'].items()),
            measurementExecuted=False, independentReview='PENDING', dydo434='HOLD UNCHANGED', **retained()))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

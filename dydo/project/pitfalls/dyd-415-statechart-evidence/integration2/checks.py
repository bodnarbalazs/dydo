"""TEMP supervisor: bounded recipe probes, then final docs/whitespace only."""
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
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
E = 'dydo/project/pitfalls/dyd-415-statechart-evidence/'
OUT = ROOT / E / 'integration2'
START = 'a18c2fcdbb9054880f5fa1179fdcf5e6f3309ffe'
MASTER = 'f3e0a161886621395f2dba47b689fba19f3bf3de'
POINTER = 'dydo/project/pitfalls/dyd-415-statechart-verification.md'
os.environ.update(LOCALAPPDATA=str(TEMP.parent), TEMP=str(TEMP), TMP=str(TEMP), TMPDIR=str(TEMP))
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
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()


def owned(path):
    return path in ['skills/README.md', 'skills/engineering/README.md', POINTER,
        E + 'integration1/.gitattributes'] or path.startswith(E + 'integration2/')


def stage():
    git('add', '--', 'skills/README.md', 'skills/engineering/README.md', POINTER,
        E + 'integration1/.gitattributes', E + 'integration2')
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
        seconds=time.time() - started, hidden=True,
        ticket=os.environ['LC_SUITE_SLOT_TICKET']))
    print(name, result.returncode, flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)


def retained():
    rows = json.loads((OUT / 'historical-hashes.json').read_bytes())['files']
    assert all(sha(ROOT / row['path']) == row['sha256'] for row in rows)
    dependencies = json.loads((OUT / 'dependencies.json').read_bytes())['files']
    for row in dependencies:
        path = ROOT / row['path'] if row['location'] == 'source' else (
            TEMP / 'dyd415-fix2' / row['path'] if row['location'] == 'metric' else Path(row['path']))
        assert path.is_file() and sha(path) == row['sha256'], row['path']
    assert git('show', '-s', '--format=%P', '590f2204e541ddc1de66d58fadc91001ed3d2009').split() == [
        'b08ab4417aeb7fba61dd2e559379960a79b6b53d', MASTER]
    return dict(historicalFiles=len(rows), integration1Files=sum('/integration1/' in r['path'] for r in rows),
        rawBytesUnchanged=True, dependenciesMatched=len(dependencies),
        runtimeTree=git('rev-parse', 'HEAD:skills/engineering/statechart'))


def inventory():
    actual = {category: sorted(p.parent.name for p in (ROOT / 'skills' / category).rglob('SKILL.md'))
        for category in ['roles', 'engineering', 'productivity']}
    assert [len(actual[k]) for k in actual] == [9, 13, 19]
    catalogs = {p: (ROOT / p).read_text() for p in ['skills/README.md', 'skills/engineering/README.md']}
    assert 'The 41 canonical' in catalogs['skills/README.md']
    assert '9 + 13 + 19 = 41' in catalogs['skills/README.md']
    entries = [line.split('**')[1] for line in catalogs['skills/engineering/README.md'].splitlines() if line.startswith('- **')]
    assert entries == actual['engineering']
    projections = json.loads((ROOT / E / 'integration1/projections.json').read_bytes())
    for row in projections['projections']:
        path = ROOT / row['host'] / 'skills/statechart'
        assert path.resolve() == (ROOT / 'skills/engineering/statechart').resolve()
        assert sha(path / 'SKILL.md') == row['skillSha256']
        assert sha(path / 'agents/openai.yaml') == row['codexMetadataSha256']
    save('inventory.json', dict(skills=actual, total=41, engineeringEntries=entries,
        projectionsRetainedAndReadOnlyChecked=projections, setupRerun=False))


def probes():
    stage()
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-integration2-', dir=TEMP))
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
    git('add', '-A', cwd=source)
    git('-c', 'user.name=DYD415 scratch proof', '-c', 'user.email=proof@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '-m', 'Integrated candidate snapshot; owned probes only', cwd=source)
    manifest = json.loads((OUT / 'dependencies.json').read_bytes())
    tools = {}
    metric = scratch / 'metric'
    for index, row in enumerate(manifest['files']):
        if row['location'] == 'source':
            continue
        original = TEMP / 'dyd415-fix2' / row['path'] if row['location'] == 'metric' else Path(row['path'])
        assert original.is_file() and sha(original) == row['sha256'], 'Retained tool identity: ' + str(original)
        dest = metric / row['path'] if row['location'] == 'metric' else scratch / 'tools' / (str(index) + '.bin')
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, dest)
        if row['location'] == 'absolute':
            tools[row['path']] = str(dest)
    (scratch / 'tools-map.json').write_bytes((json.dumps(tools, indent=2) + '\n').encode())
    env = dict(os.environ, DYD415_PROBE_SOURCE=str(source), DYD415_PROBE_ROOT=str(scratch))
    save('probe-input.json', dict(sourceHead=START, sourceSnapshotHead=git('rev-parse', 'HEAD', cwd=source),
        sourceSnapshotTree=git('rev-parse', 'HEAD^{tree}', cwd=source),
        copiedFromCurrentIndexPaths=len(list(filter(None, paths))), scratch=str(scratch),
        allRelevantExpectationsFrom=manifest['reference'], measurementExecuted=False))
    run('recipe-tests', [sys.executable, OUT / 'recipe/test_recipe.py'], env)
    shutil.copy2(scratch / 'recipe-outcomes.json', OUT / 'recipe-outcomes.json')
    argv = [sys.executable, OUT / 'recipe/driver.py', '--source', source,
        '--work', scratch / 'fresh-work', '--out', scratch / 'fresh-out',
        '--normal-temp', TEMP, '--metric', metric, '--lc', LC, '--tools-map', scratch / 'tools-map.json']
    run('recipe-preflight', argv)
    shutil.copy2(scratch / 'fresh-out/preflight.json', OUT / 'recipe-preflight.json')
    save('probe-restored.json', dict(sourceClean=not git('status', '--porcelain=v1', cwd=source),
        ownedScratchRetained=str(scratch), measurementExecuted=False, **retained()))
    inventory()


def final_checks():
    inventory_paths = [E + 'integration2/verification.md', POINTER, 'skills/README.md', 'skills/engineering/README.md']
    save('document-input.json', dict(sourceHead=START,
        files={p: sha(ROOT / p) for p in inventory_paths}))
    run('docs', [shutil.which('dydo.exe'), 'check'])
    stage()
    run('whitespace-correction', ['git', 'diff', '--check', START])
    stage()
    run('whitespace-ticket', ['git', 'diff', '--check', MASTER])
    save('final-identities.json', retained())


def main():
    phase = sys.argv[1]
    assert phase in ['probes', 'final']
    assert git('rev-parse', 'HEAD') == START
    assert git('branch', '--show-current') == 'DYD-415-statechart'
    assert Path(git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    command = subprocess.list2cmdline([sys.executable, __file__, phase])
    failure = None
    initialized = False
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(phase=phase, pid=os.getpid(), command=command,
            owned=booking.owned, inherited=booking.inherited,
            ticket=os.environ['LC_SUITE_SLOT_TICKET'], namespace=str(suite_slot.live().directory),
            driverSha256=sha(Path(__file__)), sourceHead=START)
        save(phase + '-reservation.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            with lock.open('x') as stream:
                json.dump(owner, stream)
            initialized = True
            save(phase + '-locks.json', owner)
            try:
                retained()
                probes() if phase == 'probes' else final_checks()
            except BaseException as error:
                save(phase + '-stop.json', dict(type=type(error).__name__, message=str(error)))
                failure = error
            finally:
                lock.unlink()
        save(phase + '-secondary-released.json', dict(**owner, heavyReleased=True, dydoReleased=True))
    if initialized:
        save(phase + '-released.json', dict(**owner, fifoReleased=booking.owned,
            inheritedReturned=booking.inherited, heavyReleased=True, dydoReleased=True))
    if phase == 'final' and failure is None:
        names = ['docs', 'whitespace-correction', 'whitespace-ticket']
        save('final-result.json', dict(sourceHead=START, pinnedMaster=MASTER,
            gates={name: json.loads((OUT / (name + '.command.json')).read_bytes())['exit'] for name in names},
            substantiveDocumentsUnchanged=all(sha(ROOT / p) == expected for p, expected in
                json.loads((OUT / 'document-input.json').read_bytes())['files'].items()),
            measurementExecuted=False, independentReview='PENDING', dydo434='ACCEPTANCE HOLD UNCHANGED',
            **retained()))
    if failure:
        raise failure


if __name__ == '__main__':
    main()

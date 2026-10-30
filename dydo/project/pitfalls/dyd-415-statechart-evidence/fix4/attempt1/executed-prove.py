"""Normal-TEMP driver: six selected scenario runs and bounded recipe bookkeeping only."""
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time

sys.dont_write_bytecode = True
ROOT = Path(sys.argv[1]).resolve()
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix4'
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
HEAD = 'fb047aa7c41b97277aa75620677d221d882e4f5e'
os.environ.update(LOCALAPPDATA='C:\\Users\\User\\AppData\\Local', TEMP=str(TEMP), TMP=str(TEMP))
sys.path.insert(0, str(LC))
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY


def sha(data):
    return hashlib.sha256(data).hexdigest()


def save(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def run(name, argv, cwd=ROOT, env=None, allowed=(0,)):
    assert not (OUT / (name + '.command.json')).exists(), 'Fresh attempt required'
    started = time.time()
    result = subprocess.run(list(map(str, argv)), cwd=cwd, env=env, capture_output=True,
        creationflags=subprocess.CREATE_NO_WINDOW, timeout=180)
    (OUT / (name + '.stdout')).write_bytes(result.stdout)
    (OUT / (name + '.stderr')).write_bytes(result.stderr)
    environment = env or os.environ
    keys = ['LOCALAPPDATA', 'TEMP', 'TMP', 'TMPDIR', 'PATH', 'JAVA', 'TLA2TOOLS',
        'NODE_OPTIONS', 'STATECHART_BASH', 'STATECHART_PELE', 'STATECHART_EVIDENCE_DIR',
        'STATECHART_SPAWN_AUDIT', 'LC_SUITE_SLOT_TICKET']
    keys += [k for k in environment if k.startswith('DYD415_')]
    save(name + '.command.json', dict(argv=list(map(str, argv)), cwd=str(cwd),
        exit=result.returncode, hidden=True, seconds=time.time() - started,
        environment={k: environment[k] for k in keys if k in environment}))
    print(name, result.returncode, flush=True)
    assert result.returncode in allowed, name + ': inspect raw output'
    return result


@contextlib.contextmanager
def locks(label):
    with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
        lock = TEMP / 'dydo-fullsuite.lock'
        owner = dict(owner='DYD-415 fix4', pid=os.getpid(), phase=label,
            ticket=os.environ['LC_SUITE_SLOT_TICKET'], namespace=str(suite_slot.live().directory))
        with lock.open('x') as stream:
            json.dump(owner, stream)
        save(label + '-locks.json', owner)
        try:
            yield
        finally:
            lock.unlink()
    save(label + '-locks-released.json', dict(**owner, heavyReleased=True, dydoReleased=True))


def identity():
    paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', HEAD,
        'skills'], cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).decode().splitlines()
    return [dict(path=p, sha256=sha((ROOT / p).read_bytes())) for p in paths]


def snapshot(work):
    root = work / 'source'
    root.mkdir()
    archive = subprocess.check_output(['git', 'archive', '--format=tar', HEAD],
        cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)
    with tarfile.open(fileobj=io.BytesIO(archive)) as stream:
        stream.extractall(root, filter='data')
    run('scratch-git-init', ['git', 'init', root])
    common = run('git-common-dir', ['git', 'rev-parse', '--path-format=absolute',
        '--git-common-dir']).stdout.decode().strip()
    # Read-only object sharing; all refs, index, files and scratch commits are private.
    (root / '.git/objects/info/alternates').write_text(str(Path(common) / 'objects') + '\n')
    run('scratch-git-pin', ['git', 'update-ref', 'refs/heads/proof', HEAD], root)
    run('scratch-git-head', ['git', 'symbolic-ref', 'HEAD', 'refs/heads/proof'], root)
    run('scratch-git-index', ['git', 'reset', '--mixed', HEAD], root)
    save('snapshot.json', dict(source=HEAD, path=str(root), archiveSha256=sha(archive),
        method='git archive + private index/refs, read-only object alternate; no git worktree',
        files=identity()))
    return root


def scenarios(root, work):
    scratch = work / 'tmp'
    scratch.mkdir()
    env = dict(os.environ, JAVA='C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe',
        TLA2TOOLS=str(TEMP / 'dyd415-upstream/tla2tools.jar'), TEMP=str(scratch), TMP=str(scratch),
        TMPDIR=str(scratch), STATECHART_BASH='C:/Program Files/Git/bin/bash.exe',
        STATECHART_PELE=str(TEMP / 'dyd415-tools/node_modules/pele/dist/pele.js'),
        NODE_OPTIONS='--disable-warning=ExperimentalWarning --require=' + str(OUT / 'hidden-console.cjs'))
    env['PATH'] = str(Path(env['JAVA']).parent) + os.pathsep + env['PATH']
    run('scratch-setup', ['node', root / 'setup-skills.mjs'], root, dict(env,
        STATECHART_SPAWN_AUDIT=str(OUT / 'setup-launches.jsonl')))
    skill = root / 'skills/engineering/statechart'
    target = skill / 'scripts/test/acceptance.test.ts'
    original = target.read_bytes()
    assert original == (ROOT / target.relative_to(root)).read_bytes()
    (OUT / 'test-bytes').mkdir()
    (OUT / 'test-bytes/original.ts.exact').write_bytes(original)
    cases = [
        ('corrected', 'Correcting the model preserves the invariant', b'/3 states/', b'/4 states/'),
        ('budget', 'The chart budget preserves counterexamples',
            b'/shortest path has 3 steps/', b'/shortest path has 4 steps/'),
        ('hosts', 'Both hosts require user invocation',
            b'allow_implicit_invocation: false$', b'allow_implicit_invocation: true$'),
    ]
    ledger = []
    for key, name, before, after in cases:
        assert original.count(before) == 1
        mutant = original.replace(before, after)
        (OUT / ('test-bytes/' + key + '-mutated.ts.exact')).write_bytes(mutant)
        results = []
        try:
            for phase, data, code in [('red', mutant, 1), ('restored', original, 0)]:
                target.write_bytes(data)
                label = key + '-' + phase
                artifacts = OUT / (label + '-artifacts')
                audit = OUT / (label + '-launches.jsonl')
                result = run(label, ['node', '--test', '--test-name-pattern=^' + name + '$', target],
                    skill / 'scripts', dict(env, STATECHART_EVIDENCE_DIR=str(artifacts),
                    STATECHART_SPAWN_AUDIT=str(audit)), allowed=(code,))
                output = result.stdout.decode('utf-8', errors='replace')
                assert '# tests 1' in output and '# skipped 0' in output
                if phase == 'red':
                    assert 'ERR_ASSERTION' in output and 'operator: \'match\'' in output
                    assert 'Unwired' not in output and 'Visible console launch refused' not in output
                rows = [json.loads(line) for line in audit.read_text().splitlines()] if audit.exists() else []
                assert all(row['hidden'] for row in rows)
                results.append(dict(phase=phase, exit=code, tests=1, skipped=0,
                    testSha256=sha(data), output=label + '.stdout', hiddenLaunches=len(rows)))
        finally:
            target.write_bytes(original)
        (OUT / ('test-bytes/' + key + '-restored.ts.exact')).write_bytes(target.read_bytes())
        ledger.append(dict(scenario=name, changedExpectedValue=dict(before=before.decode(), after=after.decode()),
            beforeSha256=sha(original), mutatedSha256=sha(mutant), restoredSha256=sha(target.read_bytes()),
            originalGherkinAndRuntimeUnchanged=True, results=results))
    first = OUT.parent / 'fix1'
    reused = {name: sha((first / name).read_bytes()) for name in [
        'acceptance-mutation-red.json', 'acceptance-mutation-red.txt',
        'acceptance-mutation-restored.json', 'acceptance-mutation-restored.txt', 'windows-source.json']}
    save('scenario-ledger.json', dict(scenariosAccounted=4, newRedRestoredPairs=3,
        existingPair=dict(scenario='A broken model shows its shortest bug path',
            reference='../fix1/acceptance-mutation-{red,restored}.{json,txt}', hashes=reused,
            observed='Retained pair inspected and source identity rechecked; not rerun'),
        newPairs=ledger, fullChangedCodeMutation='UNAVAILABLE'))


def main():
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() == HEAD
    before = identity()
    save('source-before.json', before)
    command = subprocess.list2cmdline([sys.executable, __file__, str(ROOT)])
    with suite_slot.book(command, worktree=ROOT) as booking:
        save('reservation.json', dict(pid=os.getpid(), command=command, owned=booking.owned,
            inherited=booking.inherited, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
            namespace=str(suite_slot.live().directory), driverSha256=sha(Path(__file__).read_bytes())))
        with locks('proof'):
            work = Path(tempfile.mkdtemp(prefix='dyd415-fix4-', dir=TEMP))
            root = snapshot(work)
            scenarios(root, work)
            recipe = work / 'recipe'
            shutil.copytree(OUT / 'recipe', recipe)
            env = dict(os.environ, DYD415_SOURCE_ROOT=str(root), DYD415_NORMAL_TEMP=str(TEMP),
                DYD415_COVERLET_WORK=str(work / 'fresh-work'), DYD415_EVIDENCE_OUT=str(work / 'fresh-evidence'),
                DYD415_METRIC_ROOT=str(TEMP / 'dyd415-fix2'), DYD415_LC_ROOT=str(LC))
            run('recipe-tests', [sys.executable, recipe / 'test_recipe.py'], root, env)
        # The recipe inherits FIFO; it acquires/releases its own Heavy and dydo locks.
        run('recipe-preflight', [sys.executable, recipe / 'driver.py', 'preflight'], root, env)
        evidence = work / 'fresh-evidence'
        assert not (work / 'fresh-work').exists(), 'Preflight must not execute native measurement'
        shutil.copytree(evidence, OUT / 'recipe-preflight')
        with locks('gates'):
            run('docs', ['dydo.exe', 'check'])
            run('whitespace', ['git', 'diff', '--check'])
            after = identity()
            assert before == after
            save('source-after.json', after)
            run('historical-unchanged', ['git', 'diff', '--exit-code', HEAD, '--',
                'skills', 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1',
                'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2',
                'dydo/project/pitfalls/dyd-415-statechart-evidence/fix3'])
    save('released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited,
        heavyReleased=True, dydoLockReleased=True, scratch=str(work), sourceUnchanged=True))


if __name__ == '__main__':
    main()

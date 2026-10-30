"""One bounded combined discovery phase, then one final-document validation phase."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
EVIDENCE = 'dydo/project/pitfalls/dyd-415-statechart-evidence'
OUT = ROOT / EVIDENCE / 'integration1'
SOURCE = 'b08ab4417aeb7fba61dd2e559379960a79b6b53d'
MASTER = 'f3e0a161886621395f2dba47b689fba19f3bf3de'
BASE = '8553172dfaa7601d34f6b884c10350183f9faaa5'
RUNTIME = '0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6'
MERGE = '590f2204e541ddc1de66d58fadc91001ed3d2009'
os.environ.update(LOCALAPPDATA='C:/Users/User/AppData/Local', TEMP=str(TEMP), TMP=str(TEMP), TMPDIR=str(TEMP))
sys.path.insert(0, str(LC))
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY


def digest(data):
    return hashlib.sha256(data).hexdigest()


def save(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def git(*args):
    result = subprocess.run(['git', *args], cwd=ROOT, capture_output=True,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    result.check_returncode()
    return result.stdout.decode().strip()


def run(name, argv, env=None):
    assert not (OUT / (name + '.command.json')).exists(), 'No automatic retry: inspect retained attempt'
    started = time.time()
    result = subprocess.run(argv, cwd=ROOT, env=env, capture_output=True,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    (OUT / (name + '.stdout')).write_bytes(result.stdout)
    (OUT / (name + '.stderr')).write_bytes(result.stderr)
    save(name + '.command.json', dict(candidate=MERGE, argv=argv, cwd=str(ROOT),
         exit=result.returncode, seconds=time.time() - started, hidden=True,
         ticket=os.environ['LC_SUITE_SLOT_TICKET'],
         environment={key: (env or os.environ).get(key) for key in
                      ['LOCALAPPDATA', 'TEMP', 'TMP', 'TMPDIR', 'NODE_OPTIONS', 'DYD415_SETUP_AUDIT']}))
    print(name, result.returncode, flush=True)
    if result.returncode:
        raise SystemExit(result.returncode)
    return result.stdout.decode().strip()


def identity():
    upstream = git('diff', '--name-only', BASE, MASTER).splitlines()
    authored = git('diff', '--name-only', BASE, SOURCE).splitlines()
    assert not set(upstream) & set(authored)
    assert git('diff', '--name-only', SOURCE, MERGE).splitlines() == upstream
    imported = []
    for path in upstream:
        incoming = git('ls-tree', MASTER, '--', path)
        assert incoming == git('ls-tree', MERGE, '--', path)
        imported.append(dict(path=path, entry=incoming))
    scopes = ['skills/engineering/statechart', 'skills/engineering/create-formal-verification-skill',
              'skills/engineering/maintain-formal-verification-skill', 'THIRD-PARTY-NOTICES.md',
              'dydo/understand/control-flow.md', 'dydo/reference/dydo-glossary.md',
              'dydo/project/changelog/2026/2026-10-29/dyd-415-statechart.md', EVIDENCE]
    trees = []
    for path in scopes:
        before = git('rev-parse', SOURCE + ':' + path)
        after = git('rev-parse', MERGE + ':' + path)
        assert before == after
        trees.append(dict(path=path, source=before, merge=after))
    runtime_tree = git('rev-parse', RUNTIME + ':skills/engineering/statechart')
    assert runtime_tree == trees[0]['merge']
    old = json.loads((ROOT / EVIDENCE / 'fix4/source-before.json').read_bytes())
    changed = [row['path'] for row in old if digest((ROOT / row['path']).read_bytes()) != row['sha256']]
    scoped = [row for row in old if row['path'].startswith('skills/engineering/statechart/')]
    assert scoped and not any(p.startswith('skills/engineering/statechart/') for p in changed)
    discovery = []
    for path in ['setup-skills.mjs', 'DynaDocs.Tests/coverage/tests/setup-skills.test.cjs']:
        blobs = {pin: git('rev-parse', pin + ':' + path) for pin in [SOURCE, MASTER, MERGE]}
        assert len(set(blobs.values())) == 1
        discovery.append(dict(path=path, blobs=blobs, workingSha256=digest((ROOT / path).read_bytes())))
    save('identity.json', dict(candidate=MERGE, parents=[SOURCE, MASTER], base=BASE,
         root=str(ROOT), branch='DYD-415-statechart', cleanAtMerge=True,
         candidatePaths=len(authored), upstreamPaths=len(upstream), overlap=[], imported=imported,
         preserved=trees, runtimePin=RUNTIME, runtimeTree=runtime_tree,
         historicalRuntimeWorkingHashesMatched=len(scoped),
         historicalWholeSkillsManifestChanged=changed, discovery=discovery))


def discovery():
    identity()
    node = shutil.which('node')
    dydo = shutil.which('dydo.exe')
    assert node and dydo
    save('tools.json', dict(python=sys.executable, pythonVersion=sys.version,
         node=node, nodeSha256=digest(Path(node).read_bytes()),
         dydo=dydo, dydoSha256=digest(Path(dydo).read_bytes()),
         nodeVersion=run('node-version', [node, '--version']),
         dydoVersion=run('dydo-version', [dydo, '--version'])))
    tests = ROOT / 'DynaDocs.Tests/coverage/tests/setup-skills.test.cjs'
    save('selection.json', dict(path=str(tests), namedTests=tests.read_text().count("test('"),
         sha256=digest(tests.read_bytes()), intendedCommand=[node, '--test', str(tests.relative_to(ROOT))]))
    env = dict(os.environ, NODE_OPTIONS='--require=' + str(OUT / 'setup-hidden.cjs'),
               DYD415_SETUP_AUDIT=str(OUT / 'setup-launches.jsonl'))
    run('setup-tests', [node, '--test', str(tests.relative_to(ROOT))], env)
    launches = [json.loads(line) for line in (OUT / 'setup-launches.jsonl').read_text().splitlines()]
    assert launches and all(row['windowsHide'] for row in launches)
    run('setup', [node, 'setup-skills.mjs'])
    canonical = ROOT / 'skills/engineering/statechart'
    projections = []
    for host in ['.claude', '.agents']:
        projection = ROOT / host / 'skills/statechart'
        assert projection.resolve() == canonical.resolve()
        assert (projection / 'SKILL.md').read_bytes() == (canonical / 'SKILL.md').read_bytes()
        assert 'disable-model-invocation: true' in (projection / 'SKILL.md').read_text()
        assert 'allow_implicit_invocation: false' in (projection / 'agents/openai.yaml').read_text()
        projections.append(dict(host=host, resolved=str(projection.resolve()),
             skillSha256=digest((projection / 'SKILL.md').read_bytes()),
             codexMetadataSha256=digest((projection / 'agents/openai.yaml').read_bytes()), explicitOnly=True))
    save('projections.json', dict(candidate=MERGE, projections=projections,
         canonicalSkillCount=len(list((ROOT / 'skills').rglob('SKILL.md'))),
         auditedHiddenSetupChildren=len(launches)))


def main():
    phase = sys.argv[1]
    assert phase in ['discovery', 'docs']
    assert git('rev-parse', 'HEAD') == MERGE
    assert git('show', '-s', '--format=%P', MERGE).split() == [SOURCE, MASTER]
    assert git('branch', '--show-current') == 'DYD-415-statechart'
    command = subprocess.list2cmdline([sys.executable, __file__, phase])
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(phase=phase, pid=os.getpid(), command=command, owned=booking.owned,
             inherited=booking.inherited, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
             namespace=str(suite_slot.live().directory), candidate=MERGE,
             driverSha256=digest(Path(__file__).read_bytes()))
        save(phase + '-reservation.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            with lock.open('x') as stream:
                json.dump(owner, stream)
            save(phase + '-locks.json', owner)
            try:
                if phase == 'discovery':
                    discovery()
                else:
                    save('document-input.json', dict(candidate=MERGE, files={
                         path: digest((ROOT / path).read_bytes()) for path in
                         [EVIDENCE + '/integration1/verification.md',
                          'dydo/project/pitfalls/dyd-415-statechart-verification.md']}))
                    run('docs', [shutil.which('dydo.exe'), 'check'])
                    git('add', '--', EVIDENCE + '/integration1',
                        'dydo/project/pitfalls/dyd-415-statechart-verification.md')
                    run('whitespace', ['git', 'diff', '--check', MERGE])
            finally:
                lock.unlink()
        save(phase + '-secondary-released.json', dict(**owner, heavyReleased=True, dydoReleased=True))
    save(phase + '-released.json', dict(**owner, fifoReleased=booking.owned,
         inheritedReturned=booking.inherited, heavyReleased=True, dydoReleased=True))


if __name__ == '__main__':
    main()

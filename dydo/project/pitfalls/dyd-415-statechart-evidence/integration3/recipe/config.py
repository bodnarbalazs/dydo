"""Source-bound applicability preflight only; never executes native measurements."""
import hashlib
import json
from pathlib import Path
import subprocess

HERE = Path(__file__).resolve().parent
MANIFEST_PATH = HERE.parent / 'dependencies-complete.json'
MANIFEST = json.loads(MANIFEST_PATH.read_bytes())


def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()


def resolve(row, root, metric, tools):
    if row['location'] == 'source':
        return root / row['path']
    if row['location'] == 'metric':
        return metric / row['path']
    return Path(tools.get(row['path'], row['path']))


def identities(root, metric, tools):
    checked = []
    for row in MANIFEST['files']:
        path = resolve(row, root, metric, tools)
        label = row['group'] + ': ' + row['path']
        assert path.is_file(), 'Missing dependency ' + label
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        assert actual == row['sha256'], 'Changed dependency ' + label
        checked.append(dict(group=row['group'], path=str(path), sha256=actual))
    producer_closure(root)
    extraction = json.loads((root / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2/extraction.json').read_bytes())
    source = (root / extraction['source']).read_bytes()
    code = source[extraction['excerptByteOffset']:extraction['excerptByteOffset'] + extraction['excerptBytes']]
    assert hashlib.sha256(code).hexdigest() == extraction['excerptSha256'], 'Extraction mismatch'
    return checked


def producer_closure(root):
    """A declared class must cover every producer in the reviewed retained receipt."""
    receipt = 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1/producer-hashes.json'
    source = {r['path']: r for r in MANIFEST['files'] if r['location'] == 'source'}
    assert receipt in source, 'Reviewed producer manifest must be declared'
    for name, expected in json.loads((root / receipt).read_bytes()).items():
        path = 'DynaDocs.Tests/coverage/' + name
        row = source.get(path)
        assert row and row['group'] == 'ts-producer' and row['sha256'] == expected, (
            'Incomplete reviewed TS producer closure: ' + path)


def preflight(root, work, out, normal_temp, metric, tools=None):
    root, work, out, normal_temp, metric = map(Path, (root, work, out, normal_temp, metric))
    assert all(p.is_absolute() and p == p.resolve() for p in (root, work, out, normal_temp, metric)), 'Exact resolved paths required'
    assert Path(git(root, 'rev-parse', '--show-toplevel')).resolve() == root, 'Exact checkout root required'
    assert not git(root, 'status', '--porcelain=v1', '--untracked-files=all'), 'Source must be clean'
    assert normal_temp.is_dir() and metric.is_dir(), 'Missing input root'
    assert work.is_relative_to(normal_temp) and work != normal_temp, 'Work must be below normal TEMP'
    inputs = [root, metric, *[resolve(r, root, metric, tools or {}) for r in MANIFEST['files']
        if r['location'] == 'absolute']]
    for dest in (work, out):
        assert not dest.exists(), 'Fresh output required: ' + str(dest)
        assert all(not dest.is_relative_to(p) and not p.is_relative_to(dest) for p in inputs), 'Keep inputs separate'
    assert not out.is_relative_to(work) and not work.is_relative_to(out), 'Separate work and evidence'
    checked = identities(root, metric, tools or {})
    return dict(root=str(root), head=git(root, 'rev-parse', 'HEAD'), clean=True,
        runtimePin=MANIFEST['runtimePin'], runtimeTree=MANIFEST['runtimeTree'],
        reference=MANIFEST['reference'], checked=checked,
        dependencyManifestSha256=hashlib.sha256(MANIFEST_PATH.read_bytes()).hexdigest(),
        work=str(work), evidence=str(out), measurementExecuted=False,
        originalAddType='Separate retained integration layer',
        generatedExactCSharp='Retained Coverlet proof; not rerun', altcover='FAILED')

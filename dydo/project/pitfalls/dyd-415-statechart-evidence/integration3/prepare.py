"""Declare the I1 successor from reviewed receipts/Git bytes before recipe edits."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
E = 'dydo/project/pitfalls/dyd-415-statechart-evidence/'
OUT = ROOT / E / 'integration3'
REVIEWED = 'b08ab4417aeb7fba61dd2e559379960a79b6b53d'
START = 'bc5f120b6da58670def58e6403e55feb7fcc0267'

# These are retained report/identity inputs, not an installed-environment graph.
# Each edge is established by the named reviewed consumer's file reads/imports.
EDGES = {
    'fix1/driver.py': ['fix1/assurance.py', 'fix1/knip-audit.py',
        'fix1/static-summary.py', 'fix1/coverage.py', 'fix1/merge-coverage.cjs',
        'fix1/coverage-summary.py'],
    'fix1/assurance.py': ['fix1/metrics.cjs', 'fix1/knip.json', 'fix1/walked.json'],
    'fix1/metrics.cjs': ['source-manifest.json'],
    'fix1/coverage.py': ['fix1/walked.json', 'fix1/linux-v8.tar.gz',
        'fix1/coverage-windows/lcov.info', 'fix1/coverage-linux/lcov.info'],
    'fix1/merge-coverage.cjs': ['fix1/coverage-windows/coverage-final.json',
        'fix1/coverage-linux/coverage-final.json'],
    'fix1/coverage-summary.py': ['fix1/walked.json', 'fix1/coverage-final/lcov.info'],
    'fix1/static-summary.py': ['fix1/walked.json', 'source-manifest.json',
        'fix1/cycles.txt', 'fix1/typescript-strict-unused.txt', 'fix1/knip.txt',
        'fix1/knip-discovery.txt', 'fix1/clones/jscpd-report.json'],
    'fix1/knip-audit.py': ['fix1/knip-command.json'],
    'fix3/driver.py': ['fix3/baseline-results/baseline.trx',
        'fix3/instrumented-results/instrumented.trx', 'fix3/selection.json',
        'fix3/binary-before.json'],
    'fix3/finalize.py': ['fix3/runtime-before.json'],
    'fix3/report.py': ['fix3/native-before.stdout', 'fix3/native-instrumented.stdout',
        'fix3/extraction.json', 'fix3/coverage.json', 'fix3/coverage.opencover.xml',
        'fix3/roslyn-project.stdout', 'fix2/roslyn-project.stdout',
        'fix3/native-test-parity.json'],
    # The portable report consumes the same schema/names in a fresh output root.
    'fix4/recipe/report.py': ['fix3/native-before.stdout', 'fix3/native-instrumented.stdout',
        'fix3/extraction.json', 'fix3/coverage.json', 'fix3/coverage.opencover.xml',
        'fix3/roslyn-project.stdout', 'fix2/roslyn-project.stdout',
        'fix3/native-test-parity.json'],
}
PRODUCER_EDGES = {
    'js_metrics.cjs': ['fix1/driver.py:install', 'fix1/metrics.cjs:loadTool'],
    'typescript_join.py': ['fix1/coverage.py:join', 'fix1/coverage-summary.py:join'],
    'gate_inventory.py': ['fix1/static-summary.py:dependency_cycles'],
    'knip_reporter.mjs': ['fix1/assurance.py:reporter copy'],
    'package.json': ['fix1/driver.py:install'],
    'package-lock.json': ['fix1/driver.py:install'],
}


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def reference(path, revision=REVIEWED):
    return git('show', revision + ':' + path)


def save(name, value):
    path = OUT / name
    assert not path.exists(), 'Never overwrite declarations'
    path.write_bytes((json.dumps(value, indent=2) + '\n').encode())


def main():
    assert git('rev-parse', 'HEAD').decode().strip() == START
    previous = json.loads(reference(E + 'integration2/dependencies.json', START))
    assert previous['reference'] == REVIEWED and len(previous['files']) == 476
    rows = previous['files'].copy()
    producers = json.loads(reference(E + 'fix1/producer-hashes.json'))
    assert set(producers) == set(PRODUCER_EDGES), 'Review newly listed producers'
    for name, expected in producers.items():
        path = 'DynaDocs.Tests/coverage/' + name
        rows.append(dict(group='ts-producer', location='source', path=path,
            sha256=expected, provenance=E + 'fix1/producer-hashes.json@' + REVIEWED,
            consumers=PRODUCER_EDGES[name], identityLayer='reviewed Windows working bytes',
            reviewedGitBlob=git('rev-parse', REVIEWED + ':' + path).decode().strip(),
            canonicalBlobSha256=sha(reference(path))))
    direct = sorted({p for consumer, inputs in EDGES.items() for p in [consumer, *inputs]}
        | {'fix1/producer-hashes.json'})
    keys = {(r['location'], r['path']) for r in rows}
    added = []
    for name in direct:
        path = E + name
        if ('source', path) in keys:
            continue
        rows.append(dict(group='retained-input', location='source', path=path,
            sha256=sha(reference(path)), provenance=path + '@' + REVIEWED,
            identityLayer='reviewed retained Git bytes'))
        added.append(path)
        keys.add(('source', path))
    assert len({(r['location'], r['path']) for r in rows}) == len(rows)
    save('closure-declaration.json', dict(reference=REVIEWED, sourceHead=START,
        unchangedPreviousRows=476, previousManifestSha256=sha(reference(
            E + 'integration2/dependencies.json', START)),
        producerManifest=E + 'fix1/producer-hashes.json', producerEdges=PRODUCER_EDGES,
        retainedReadEdges=EDGES, addedRetainedInputs=added,
        declaredBeforeSuccessorRecipe=True,
        scope='Retained TS assurance report pipeline and exact-source native identity join; preflight only',
        exclusions='No platform/capture rerun, installed SDK/npm dependency graph, all-skills identity, or toolchain equivalence'))
    save('dependencies.json', dict(**{k: v for k, v in previous.items()
        if k not in ('files', 'classes')}, supersedes='integration2/dependencies.json@' + START,
        classes=sorted({r['group'] for r in rows}), files=rows))
    history = []
    for folder in ['integration1', 'integration2', 'fix1', 'fix2', 'fix3', 'fix4']:
        for path in git('ls-tree', '-r', '--name-only', START, '--', E + folder).decode().splitlines():
            raw = reference(path, START)
            assert (ROOT / path).read_bytes() == raw, path
            history.append(dict(path=path, bytes=len(raw), sha256=sha(raw)))
    save('historical-hashes.json', dict(reference=START, files=history))
    save('custody.json', dict(sourceHead=START, branch=git('branch', '--show-current').decode().strip(),
        root=git('rev-parse', '--show-toplevel').decode().strip(),
        base=REVIEWED, baseIsAncestor=subprocess.run(['git', 'merge-base', '--is-ancestor', REVIEWED, 'HEAD'],
            cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).returncode == 0,
        cleanBeforeFirstEdit=True, ownedPaths=[E + 'integration3/**',
            'dydo/project/pitfalls/dyd-415-statechart-verification.md'],
        mergeParents=git('show', '-s', '--format=%P', '590f2204').decode().strip().split()))
    print(json.dumps(dict(dependencies=len(rows), addedProducers=len(producers),
        addedRetainedInputs=len(added), historicalFiles=len(history))))


if __name__ == '__main__':
    main()

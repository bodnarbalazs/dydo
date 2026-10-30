"""Read-only source/retained-result audit; write only final fix4 receipts and manifest."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(sys.argv[1]).resolve()
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix4'


def read(name):
    return json.loads((OUT / name).read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().rstrip()


assert git('rev-parse', 'HEAD') == 'fb047aa7c41b97277aa75620677d221d882e4f5e'
before = read('source-before.json')
assert before == read('source-after.json')
assert all(sha(ROOT / row['path']) == row['sha256'] for row in before)
ledger = read('scenario-ledger.json')
assert ledger['scenariosAccounted'] == 4 and len(ledger['newPairs']) == 3
hidden = 0
excerpts = []
for case in ledger['newPairs']:
    assert case['beforeSha256'] == case['restoredSha256'] != case['mutatedSha256']
    assert [r['exit'] for r in case['results']] == [1, 0]
    for result in case['results']:
        assert result['tests'] == 1 and result['skipped'] == 0
        output = (OUT / result['output']).read_text()
        assert '# tests 1' in output and '# skipped 0' in output
        assert ('ERR_ASSERTION' in output) == (result['exit'] == 1)
        if result['exit'] == 1:
            excerpts.append(case['scenario'] + '\n' + '\n'.join(output.splitlines()[:70]))
        hidden += result['hiddenLaunches']
for name, expected in ledger['existingPair']['hashes'].items():
    assert sha(OUT.parent / 'fix1' / name) == expected
prior = json.loads((OUT.parent / 'fix1/windows-source.json').read_text())
assert all(sha(ROOT / path) == expected for path, expected in prior.items())
for name in ('recipe-tests', 'recipe-preflight', 'docs', 'whitespace', 'historical-unchanged'):
    assert read(name + '.command.json')['exit'] == 0
assert 'Ran 5 tests' in (OUT / 'recipe-tests.stderr').read_text()
legacy = json.loads((OUT / 'recipe-tests.stdout').read_text().strip())
assert legacy['status'] == [] and legacy['exit'] == 1 and 'AssertionError' in legacy['stderr']
preflight = read('recipe-preflight/preflight.json')
assert preflight['clean'] and preflight['runtimeMatchesPin']
assert preflight['measurementExecuted'] is False
assert not Path(preflight['work']).exists()
for name, expected in preflight['recipeModules'].items():
    assert sha(OUT / 'recipe' / name) == expected
assert (OUT / 'recipe/report.py').read_bytes() == (OUT.parent / 'fix3/report.py').read_bytes()
assert read('recipe-preflight/runtime-before.json') == read('recipe-preflight/runtime-after.json')
assert read('recipe-preflight/released.json')['inheritedReturned']
released = read('released.json')
assert released['fifoReleased'] and released['heavyReleased'] and released['dydoLockReleased']
assert not git('diff', '--name-only', '0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6', '--', 'skills')
status = git('status', '--porcelain=v1', '--untracked-files=all').splitlines()
allowed = 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix4/'
pointer = 'dydo/project/pitfalls/dyd-415-statechart-verification.md'
assert all(row[3:].startswith(allowed) or row[3:] == pointer for row in status)
attestation = dict(runtimeAndOriginalTestsUnchanged=True, sourceFilesChecked=len(before),
    previousWindowsSourceRowsChecked=len(prior), scenariosAccounted=4,
    newSemanticRedRestoredPairs=3, selectedRuns=6, hiddenLaunches=hidden,
    bookkeepingTests=5, legacyWriterPredicateExit=1, freshCleanExternalPreflight=True,
    portableMeasurementExecuted=False,
    docsExit=0, whitespaceExit=0, historicalFix1Fix2Fix3Unchanged=True,
    locksReleased=True, changedPathsOwned=True, powershellMetrics='UNAVAILABLE',
    fullChangedCodeMutation='UNAVAILABLE', readiness='BLOCKED: DYD-434 disposition',
    altcover='FAILED (historical)', coverlet='Supported historical fix3 campaign; not rerun',
    janitorNit='Recorded/deferred, source-based only', writer='code-writer/model: gpt-6.1-sol/high')
(OUT / 'final-attestation.json').write_text(json.dumps(attestation, indent=2) + '\n')
(OUT / 'assertion-excerpts.txt').write_text('\n\n'.join(excerpts) + '\n')
manifest = [dict(path=p.relative_to(OUT).as_posix(), bytes=p.stat().st_size, sha256=sha(p))
    for p in sorted(OUT.rglob('*')) if p.is_file() and p != OUT / 'proof-manifest.json']
(OUT / 'proof-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
print(json.dumps(dict(**attestation, manifestedFiles=len(manifest)), indent=2))

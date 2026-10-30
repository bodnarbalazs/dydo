"""Complete the reviewed gate_inventory import edge, preserving the first attempt."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/integration3'
REVIEWED = 'b08ab4417aeb7fba61dd2e559379960a79b6b53d'
PATH = 'DynaDocs.Tests/coverage/inventory.py'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    initial = OUT / 'initial-attempt'
    initial.mkdir(exist_ok=False)
    for path in sorted(OUT.iterdir()):
        if path.name in ['initial-attempt', 'complete.py']:
            continue
        if path.is_dir():
            shutil.copytree(path, initial / path.name)
        else:
            shutil.copy2(path, initial / path.name)
    raw = git('show', REVIEWED + ':' + PATH)
    assert b'\r' not in raw, 'Reference is canonical LF'
    windows = raw.replace(b'\n', b'\r\n')
    row = dict(group='retained-input', location='source', path=PATH,
        sha256=sha(windows), provenance=PATH + '@' + REVIEWED,
        reviewedGitBlob=git('rev-parse', REVIEWED + ':' + PATH).decode().strip(),
        canonicalBlobSha256=sha(raw),
        identityLayer='Reviewed canonical LF Git bytes rendered CRLF for Windows working input',
        consumers=['DynaDocs.Tests/coverage/gate_inventory.py:6 import'],
        terminalImports='stdlib only: hashlib,json,subprocess,pathlib')
    declaration = dict(reference=REVIEWED, addedRow=row,
        reason='Direct repository import required when retained static-summary.py loads dependency_cycles',
        initialAttempt='initial-attempt; all bytes preserved, 519-row result remains true at that scope',
        expectationFromCandidate=False, declaredBeforeRecipeEdits=True,
        bounds='No other gate_inventory inventory functions are called; do not add their unused input graphs')
    (OUT / 'closure-addendum.json').write_bytes((json.dumps(declaration, indent=2) + '\n').encode())
    manifest = json.loads((OUT / 'dependencies.json').read_bytes())
    manifest['files'].append(row)
    manifest['supersedesInitialDeclaration'] = 'dependencies.json (519 rows)'
    dest = OUT / 'dependencies-complete.json'
    assert not dest.exists()
    dest.write_bytes((json.dumps(manifest, indent=2) + '\n').encode())
    assert sha((ROOT / PATH).read_bytes()) == row['sha256'], 'Observed identity must match reference derivation'
    print(json.dumps(dict(dependencies=len(manifest['files']), added=row)))


if __name__ == '__main__':
    main()

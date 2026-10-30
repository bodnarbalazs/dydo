"""Cheap required integration checks, recorded against the correction source."""
import json
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
commands = {
    'setup': ['node', 'setup-skills.mjs'],
    'setup-tests': ['node', '--test', 'DynaDocs.Tests/coverage/tests/setup-skills.test.cjs'],
    'typecheck': ['node', 'C:/Users/User/AppData/Local/Temp/dyd415-tools/node_modules/typescript/bin/tsc', '-p', 'skills/engineering/statechart/scripts/tsconfig.json'],
    'docs': ['dydo.exe', 'check'],
    'diff-check': ['git', 'diff', '--check'],
    'staged-diff-check': ['git', 'diff', '--cached', '--check'],
    'base-diff-check': ['git', 'diff', '--check', '8553172dfaa7601d34f6b884c10350183f9faaa5'],
    'scoped-static': ['python', 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1/static-summary.py'],
    'coverage-gate': ['python', 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1/coverage-summary.py'],
}
verdict = 0
for name in sys.argv[1:]:
    args = commands[name]
    with (out / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run(args, cwd=root, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
    (out / (name + '-command.json')).write_text(json.dumps({'argv': args, 'cwd': str(root), 'exit': result.returncode}, indent=2) + '\n')
    print(name, result.returncode)
    verdict = max(verdict, result.returncode)
raise SystemExit(verdict)

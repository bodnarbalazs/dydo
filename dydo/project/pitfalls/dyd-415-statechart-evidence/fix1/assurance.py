"""Run pinned native tools against statechart, retaining unfiltered reports for review."""
import json
import os
from pathlib import Path
import subprocess
import shutil

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
TOOLS = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1/assurance/node_modules')
SCRIPTS = ROOT / 'skills/engineering/statechart/scripts'

def run(name, args, cwd=ROOT):
    with (OUT / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run([str(a) for a in args], cwd=cwd, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
    (OUT / (name + '-command.json')).write_text(json.dumps({'argv': [str(a) for a in args], 'cwd': str(cwd), 'exit': result.returncode}, indent=2) + '\n')
    print(name, result.returncode, flush=True)
    return result.returncode

run('metrics', ['node', OUT / 'metrics.cjs'])
rows = json.loads((OUT / 'walked.json').read_text())
files = ['skills/engineering/statechart/' + r['path'] for r in rows]
run('typescript-strict-unused', ['node', 'C:/Users/User/AppData/Local/Temp/dyd415-tools/node_modules/typescript/bin/tsc', '-p', SCRIPTS / 'tsconfig.json', '--noUnusedLocals', '--noUnusedParameters'])
run('cycles', ['node', TOOLS / 'dependency-cruiser/bin/dependency-cruise.mjs', '--no-config', '--output-type', 'json', '--do-not-follow', 'node_modules', '--ts-pre-compilation-deps', *files])
run('clones', ['node', TOOLS / 'jscpd/run-jscpd.js', '--absolute', '--format', 'typescript', '--no-gitignore', '--min-lines', '15', '--min-tokens', '100', '--max-lines', '2147483647', '--max-size', '1000000', '--mode', 'strict', '--reporters', 'json', '--output', OUT / 'clones', '--silent', '--no-tips', *files])
config = {'entry': ['statechart.ts', 'worker.ts', 'windows-cleanup.ts', 'test/*.test.ts', 'test/java.ts'], 'project': ['**/*.ts']}
(OUT / 'knip.json').write_text(json.dumps(config, indent=2) + '\n')
shutil.copy2(ROOT / 'DynaDocs.Tests/coverage/knip_reporter.mjs', TOOLS.parent / 'knip_reporter.mjs')
run('knip', ['node', TOOLS / 'knip/bin/knip.js', '--config', OUT / 'knip.json', '--no-gitignore', '--include-entry-exports', '--include', 'files,exports,nsExports,duplicates,unresolved', '--reporter', TOOLS.parent / 'knip_reporter.mjs'], SCRIPTS)
versions = {}
for name in ['c8', 'eslint', 'eslint-plugin-sonarjs', '@typescript-eslint/parser', 'dependency-cruiser', 'knip', 'jscpd']:
    versions[name] = json.loads((TOOLS / name / 'package.json').read_text(encoding='utf-8'))['version']
(OUT / 'assurance-versions.json').write_text(json.dumps(versions, indent=2) + '\n')

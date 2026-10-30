"""Convert one native platform at a time; merge Istanbul locations with merge-coverage.cjs."""
import json
from pathlib import Path
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[5]
OUT = Path(__file__).resolve().parent
TEMP = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1')
TOOLS = TEMP / 'assurance/node_modules'
windows_only = '--windows-only' in sys.argv
linux_only = '--linux-only' in sys.argv
assert windows_only != linux_only, 'Choose one platform; never merge raw V8 ranges across platforms'
label = 'windows' if windows_only else 'linux'
walked = json.loads((OUT / 'walked.json').read_text())
targets = ['skills/engineering/statechart/' + r['path'] for r in walked if '/test/' not in r['path']]
combined = TEMP / (label + '-final-normalized-v8')
combined.mkdir(exist_ok=True)
assert not list(combined.iterdir()), 'Use a fresh combined destination, never merge stale evidence'

def normalize(value):
    if isinstance(value, str):
        return value.replace('file:///source/', ROOT.as_uri() + '/').replace('/source/skills/', ROOT.as_posix() + '/skills/')
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if isinstance(value, dict):
        return {normalize(k): normalize(v) for k, v in value.items()}
    return value

def save(label, data):
    data = normalize(data)
    urls = { (ROOT / p).as_uri() for p in targets }
    data['result'] = [r for r in data['result'] if r['url'] in urls]
    data['source-map-cache'] = {k: v for k, v in data.get('source-map-cache', {}).items() if k in urls}
    if data['result']:
        (combined / label).write_text(json.dumps(data))

if not linux_only:
    for file in (TEMP / 'final-windows-v8').glob('*.json'):
        save('windows-' + file.name, json.loads(file.read_text()))
if not windows_only:
    with tarfile.open(OUT / 'linux-v8.tar.gz') as tar:
        for member in tar.getmembers():
            if member.isfile() and member.name.endswith('.json'):
                save('linux-' + Path(member.name).name, json.load(tar.extractfile(member)))

args = ['node', str(TOOLS / 'c8/bin/c8.js'), 'report', '--temp-directory', str(combined),
        '--reports-dir', str(OUT / ('coverage-' + label)), '--reporter=json', '--reporter=lcovonly', '--reporter=text',
        '--all', '--src', str(ROOT / 'skills/engineering/statechart/scripts'), '--extension=.ts']
for target in targets:
    args += ['--include', target]
with (OUT / ('coverage-' + label + '.txt')).open('w') as log:
    result = subprocess.run(args, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
(OUT / ('coverage-' + label + '-command.json')).write_text(json.dumps({'argv': args, 'cwd': str(ROOT), 'exit': result.returncode}, indent=2) + '\n')
assert result.returncode == 0
sys.path.insert(0, str(ROOT / 'DynaDocs.Tests/coverage'))
from typescript_join import join
rows = join(OUT / ('coverage-' + label) / 'lcov.info', ROOT, ROOT,
            targets, {'skills/engineering/statechart/' + row['path']: row for row in walked})
for row in rows:
    local = next(w for w in walked if row['path'].endswith(w['path']))
    original = {m['id']: m['vendorIdentical'] for m in local['methods']}
    for method in row['methods']:
        method['lineCoverage'] = method['covered'] / method['total']
        method['hcrap'] = method['cc'] ** 2 * (1 - method['lineCoverage']) ** 3 + method['cognitive']
        method['vendorIdentical'] = original.get(method['id'], False)
    for kind in ['lines', 'branches']:
        hits = list(row[kind].values())
        row[kind + 'Percent'] = 100 * sum(h > 0 for h in hits) / len(hits) if hits else 100
    print(row['path'], row['linesPercent'], row['branchesPercent'])
    print('method findings', [(m['id'], m['hcrap'], m['cognitive'], m['vendorIdentical']) for m in row['methods'] if m['hcrap'] > 20 or m['cognitive'] > 20])
(OUT / ('coverage-' + label + '-methods.json')).write_text(json.dumps(rows, indent=2) + '\n')
findings = []
for row in rows:
    if row['linesPercent'] < 80 or row['branchesPercent'] < 60:
        findings.append({'path': row['path'], 'gate': 'coverage', 'lines': row['linesPercent'], 'branches': row['branchesPercent']})
    for method in row['methods']:
        if not method['vendorIdentical'] and (method['hcrap'] > 20 or method['cognitive'] > 20 or method['parameters'] > 7):
            findings.append({'path': row['path'], 'gate': 'method', **method})
(OUT / ('coverage-' + label + '-result.json')).write_text(json.dumps({'partial': windows_only or linux_only, 'findings': findings,
    'nativeFiles': len(list(combined.glob('*.json'))), 'targets': targets}, indent=2) + '\n')

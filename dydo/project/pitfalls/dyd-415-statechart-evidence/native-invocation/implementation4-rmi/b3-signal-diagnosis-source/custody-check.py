"""Read-only custody/source audit. No writes, project imports, tests or runtime launches."""
import ast
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
PACKET = Path(__file__).resolve().parent
OLD = PACKET.parent / 'b3-janitor-correction-source'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def check(item, mapping=None):
    path = Path((mapping or {}).get(item['path'], item['path']))
    if not path.is_absolute():
        path = ROOT / path
    assert sha(path) == item['sha256'], str(path)
    if 'bytes' in item:
        assert path.stat().st_size == item['bytes'], str(path)


assert sha(OLD / 'source-pin.json') == '30bbe7be70c9482971410d1ddef6764481da0828237c5d1d3a6096f8ec73efce'
assert sha(PACKET.parent / 'b3-janitor-correction-20261031a/return-receipt.json') == 'cdedf5f1729f6f15e405c26235be0d22329de25204f54ad786d9651fd966754a'
pin = read(OLD / 'source-pin.json')
for item in pin['sources'] + pin['tempCounterparts'] + pin['externalInputs']:
    check(item)
historical = read(OLD / 'preserved-inputs.json')
mapping = read(OLD / 'historical-applicability.json')['beforeSnapshots']
assert len(mapping) == 2 and set(mapping) == set(pin['sourceScope'])
for item in historical:
    check(item, mapping)
before = read(PACKET / 'before-custody.json')
for item in before:
    assert sha(Path(item['Path'])) == item['Hash'].lower(), item['Path']
assert sha(PACKET / 'preserved-inputs.json') == sha(OLD / 'preserved-inputs.json')
assert sha(PACKET / 'historical-applicability.json') == sha(OLD / 'historical-applicability.json')
manifest = read(PACKET / 'input-manifest.json')
old_manifest = read(OLD / 'input-manifest.json')
assert manifest['files'][2:] == old_manifest['files']
for item in manifest['files']:
    check(item)
for path in PACKET.glob('*.py'):
    ast.parse(path.read_text(encoding='utf-8'), filename=str(path))
assert not (PACKET / 'execution-release.json').exists()
for name in ['SIGINT', 'SIGTERM']:
    assert not (PACKET.parent / ('b3-signal-diagnosis-20261031a-' + name)).exists()
for name in ['launch', 'driver', 'assemble']:
    assert not Path('C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-signal-diagnosis-' + name + '-20261031.py').exists()
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
    creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()
assert head == pin['head']
status = set(subprocess.check_output(['git', 'status', '--porcelain=v1', '--untracked-files=all'], cwd=ROOT,
    creationflags=subprocess.CREATE_NO_WINDOW).decode().splitlines())
original_status = set((PACKET / 'before-status.txt').read_text(encoding='utf-8-sig').splitlines())
assert original_status <= status
assert all(line.startswith('?? ' + PACKET.relative_to(ROOT).as_posix() + '/') for line in status - original_status)
sources = [dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size, sha256=sha(path))
    for path in sorted(PACKET.rglob('*')) if path.is_file() and path.name not in ['source-pin.json', 'after-custody.json']]
print(json.dumps(dict(state='SOURCE_ONLY_BLOCKED_CANCELLATION_CONTRACT', head=head,
    frozenSourceRows=len(pin['sources']), historicalRows=len(historical), beforeCustodyRows=len(before),
    mismatches=0, productionCanonicalTestsPayloadAndHistoricalBytesUnchanged=True,
    sources=sources, execution='NONE', importsOrDiscovery='NONE',
    driverSha256=sha(PACKET / 'driver.py'), launcherSha256=sha(PACKET / 'launcher.py'),
    sourceClosureComplete=False, safeExecutionEnvelopeProved=False,
    tempCounterparts='ALL_THREE_ABSENT', inputs=len(manifest['files']),
    scope='Two failed signal boundaries only'), indent=2))

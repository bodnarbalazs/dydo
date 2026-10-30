import difflib
import hashlib
import json
from pathlib import Path
import subprocess
import tarfile
from urllib.parse import unquote, urlparse
import xml.etree.ElementTree as ET

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-609d3912')
OUT = Path(__file__).parent
E = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
result = {}
for folder in ['fix1', 'fix2', 'fix3']:
    manifest = read(E / folder / 'proof-manifest.json')
    entries = manifest.items() if isinstance(manifest, dict) else [(r['path'], r['sha256']) for r in manifest]
    mismatches = [name for name, digest in entries if sha(E / folder / name) != digest]
    result[folder + 'Manifest'] = dict(files=len(manifest), mismatches=mismatches)

snap = read(E / 'fix1/snapshot.json')
result['snapshot'] = dict(files=len(snap['files']), mismatches=[p for p,r in snap['files'].items() if sha(ROOT/p) != r['sha256']])
windows = read(E / 'fix1/windows-source.json')
result['windowsSource'] = dict(files=len(windows), mismatches=[p for p,h in windows.items() if sha(ROOT/p) != h])
runtime = read(E / 'fix3/runtime-after.json')
result['runtime'] = dict(files=len(runtime), mismatches=[r['path'] for r in runtime if sha(ROOT/r['path']) != r['sha256']])

upstream = Path('C:/Users/User/AppData/Local/Temp/dyd415-upstream/plugins-1ba91fc25c0977741dd0e267849e6234b037ed0a/pstack/skills/statechart')
manifest = read(E / 'source-manifest.json')
rows = manifest['vendored_files']
result['upstream'] = dict(files=len(rows), mismatches=[r['path'] for r in rows if sha(upstream/r['path']) != r['upstream_sha256']],
    identical=sum((upstream/r['path']).read_bytes() == (ROOT/'skills/engineering/statechart'/r['path']).read_bytes() for r in rows))
for name in ['scripts/run.ts', 'scripts/statechart.ts']:
    delta = ''.join(difflib.unified_diff((upstream/name).read_text().splitlines(True),
        (ROOT/'skills/engineering/statechart'/name).read_text().splitlines(True),fromfile='upstream/'+name,tofile='candidate/'+name))
    (OUT / (Path(name).stem + '-upstream.diff')).write_text(delta, encoding='utf-8')

source = (ROOT/'skills/engineering/statechart/scripts/windows-job.ps1').read_bytes()
ex = read(E/'fix3/extraction.json')
excerpt = source[ex['excerptByteOffset']:ex['excerptByteOffset']+ex['excerptBytes']]
assert hashlib.sha256(source).hexdigest() == ex['sourceSha256']
assert hashlib.sha256(excerpt).hexdigest() == ex['excerptSha256']
assert excerpt == (E/'fix3/StatechartJob.cs.exact').read_bytes() == (E/'fix2/StatechartJob.cs.exact').read_bytes()
assert source[:ex['excerptByteOffset']].count(b'\n') + 1 == ex['sourceStartLine']
assert source[ex['excerptByteOffset']-3:ex['excerptByteOffset']] == b"@'\n"
assert source[ex['excerptByteOffset']+ex['excerptBytes']:].startswith(b"\n'@")
for name in ['AdapterTests.cs.template','Adapter.csproj.template','Adapter.Tests.csproj.template']:
    assert (E/'fix3'/name).read_bytes() == (E/'fix2'/name).read_bytes()
result['extraction'] = ex

before=read(E/'fix3/native-before.stdout'); during=read(E/'fix3/native-instrumented.stdout'); after=read(E/'fix3/native-restored.stdout')
assert before == after
native=before['methods'][0]; altered=next(m for m in during['methods'] if m['token']==native['token'])
fields=['path','line','column','end_line','end_column','checksum_algorithm','checksum']
points=lambda m:[{k:p[k] for k in fields} for p in m['points']]
assert points(native)==points(altered)
assert before['module_id']==during['module_id'] and before['sha256']!=during['sha256']
assert all(p['checksum']==ex['excerptSha256'] and p['checksum_algorithm']=='SHA256' for p in native['points'])
cov=read(E/'fix3/coverage.json'); assert list(cov)==['Adapter.dll']
doc,classes=next(iter(cov['Adapter.dll'].items())); assert doc in before['documents']
sig,counters=next(iter(classes['StatechartJob'].items())); assert sig==native['identity']
assert set(map(int,counters['Lines']))=={p['line'] for p in native['points']}
branches=counters['Branches']; lines=counters['Lines']
result['nativeCoverageRecount']=dict(linesCovered=sum(v>0 for v in lines.values()),linesTotal=len(lines),
    branchesCovered=sum(b['Hits']>0 for b in branches),branchesTotal=len(branches),token=native['token'])
result['executionSnapshots'] = {}
for receipt, snapshot in [('driver-identity.json','executed-setup-driver.py'), ('resume-driver-identity.json','executed-campaign-driver.py')]:
    result['executionSnapshots'][snapshot] = sha(E/'fix3'/snapshot) == read(E/'fix3'/receipt)['sha256']
execution = read(E/'fix3/finalize-inherited.json')
assert execution['sha256'] == sha(E/'fix3/finalize.py')
assert execution['reportSha256'] == sha(E/'fix3/report.py')
binary = read(E/'fix3/binary-before.json')
binary_dir = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix3/tests/bin/Debug/net10.0')
result['retainedRestoredBinaries'] = {name: sha(binary_dir/name) == digest for name,digest in binary.items()}
assert all(result['retainedRestoredBinaries'].values())
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for label in ['baseline','instrumented']:
    tree=ET.parse(E/f'fix3/{label}-results/{label}.trx')
    results=tree.findall('.//t:UnitTestResult',ns)
    assert len(results)==6 and all(r.attrib['outcome']=='Passed' for r in results)
    result[label+'Tests']=[r.attrib['testName'] for r in results]

walk=read(E/'fix1/walked.json')
bad=[]
for row in walk:
    local=(ROOT/'skills/engineering/statechart'/row['path']).read_text()
    original=(upstream/row['path']).read_text() if not row['authored'] else ''
    for m in row['methods']:
        if m['vendorIdentical']:
            text=local[m['start']:m['end']]
            if text not in original: bad.append([row['path'],m['id']])
result['staticVendorBodies']=dict(mismatches=bad,files=len(walk))
source_maps = {}
with tarfile.open(E/'fix1/normalized-v8.tar.gz') as archive:
    for member in archive.getmembers():
        if not member.isfile() or not member.name.endswith('.json'):
            continue
        data = json.load(archive.extractfile(member))
        for url, cache in data.get('source-map-cache', {}).items():
            name = url.split('/skills/engineering/statechart/')[-1]
            for text in cache.get('data', {}).get('sourcesContent', []):
                if text is not None:
                    expected = (ROOT/'skills/engineering/statechart'/name).read_text()
                    source_maps.setdefault(name, set()).add(text == expected)
result['coverageSourceMapContents'] = {name: sorted(values) for name, values in source_maps.items()}
native_modules = read(E/'fix1/coverage-final-methods.json')
result['typescriptRecount'] = []
for row in native_modules:
    lines = list(row['lines'].values()); branches = list(row['branches'].values())
    result['typescriptRecount'].append(dict(path=row['path'], linesCovered=sum(v>0 for v in lines), linesTotal=len(lines),
        branchesCovered=sum(v>0 for v in branches), branchesTotal=len(branches),
        maxMaintainedHcrap=max(m['cc']**2*(1-m['covered']/m['total'])**3+m['cognitive'] for m in row['methods'] if not m['vendorIdentical'])))
cmd=['git','diff','--check','8553172dfaa7601d34f6b884c10350183f9faaa5','fb047aa7c41b97277aa75620677d221d882e4f5e']
r=subprocess.run(cmd,cwd=ROOT,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
result['diffCheck']=dict(argv=cmd,exit=r.returncode,stdout=r.stdout.decode(),stderr=r.stderr.decode())
(OUT/'audit.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))

"""Account for native findings and unchanged vendor spans without discarding raw diagnostics."""
import hashlib
import json
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
sys.path.insert(0, str(root / 'DynaDocs.Tests/coverage'))
from gate_inventory import dependency_cycles

walked = json.loads((out / 'walked.json').read_text())
assert all(not row['suppressions'] for row in walked)
modules, diagnostics = [], []
for row in walked:
    local = [m for m in row['methods'] if not m['vendorIdentical']]
    modules.append({'path': row['path'], 'authored': row['authored'], 'methods': len(row['methods']),
                    'maintainedMethods': len(local), 'module': row['module'], 'upstreamModule': row['upstreamModule'],
                    'moduleVendorIdentical': row['moduleVendorIdentical'],
                    'maxMaintainedCognitive': max([m['cognitive'] for m in local] + [0]),
                    'maxMaintainedParameters': max([m['parameters'] for m in local] + [0])})
    for message in row['diagnostics']:
        owners = [m for m in row['methods'] if m['line'] <= message['line'] <= m['end_line']]
        owner = min(owners, key=lambda m: m['end'] - m['start']) if owners else None
        diagnostics.append({'path': row['path'], 'message': message,
                            'vendorIdenticalMethod': bool(owner and owner['vendorIdentical']), 'method': owner['id'] if owner else '<module>'})
native = json.loads((out / 'cycles.txt').read_text())
edges = [(m['source'], d['resolved']) for m in native['modules'] for d in m['dependencies'] if not d.get('coreModule') and not d.get('matchesDoNotFollow')]
cycles = dependency_cycles(edges)
unresolved = [(m['source'], d['module']) for m in native['modules'] for d in m['dependencies'] if d.get('couldNotResolve')]
manifest = json.loads((out.parent / 'source-manifest.json').read_text())
identical = {'skills/engineering/statechart/' + r['path']: r['upstream_sha256'] for r in manifest['vendored_files'] if r['upstream_sha256'] == r['candidate_sha256']}
unused = []
for text in (out / 'typescript-strict-unused.txt').read_text().splitlines():
    file = text.partition('(')[0]
    exact = file in identical and hashlib.sha256((root / file).read_bytes()).hexdigest() == identical[file]
    unused.append({'diagnostic': text, 'vendorIdenticalFile': exact})
knip, knip_measurement = [json.loads(line) for line in (out / 'knip.txt').read_text().splitlines() if line.strip()]
assert knip_measurement['kind'] == 'measurement'
expected = {p.as_posix() for p in (root / 'skills/engineering/statechart/scripts').rglob('*.ts') if 'node_modules' not in p.parts}
discovered = set(re.findall(r"'(C:/[^'\n]+\.(?:ts|mjs))'", (out / 'knip-discovery.txt').read_text()))
assert expected <= discovered
assert knip_measurement['counters']['processed'] == knip_measurement['counters']['total'] == len(discovered)
knip_measurement['discoveredSources'] = sorted(discovered)
knip_measurement['pluginAddedCorpusViews'] = sorted(discovered - expected)
for issue in knip['issues']:
    file = 'skills/engineering/statechart/scripts/' + issue['file']
    issue['vendorIdenticalFile'] = file in identical and hashlib.sha256((root / file).read_bytes()).hexdigest() == identical[file]
clones = json.loads((out / 'clones/jscpd-report.json').read_text())
summary = {'modules': modules, 'nativeDiagnostics': diagnostics, 'typescriptUnused': unused, 'knip': knip, 'knipMeasurement': knip_measurement,
           'cycles': cycles, 'unresolvedDependencies': unresolved, 'graphModules': len(native['modules']), 'graphEdges': len(edges),
           'clones': clones, 'thresholds': {'linePercent': 80, 'branchPercent': 60, 'hcrap': 20, 'cognitive': 20, 'parameters': 7, 'cloneLines': 15, 'cloneTokens': 100}}
(out / 'static-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
assert all(m['maxMaintainedCognitive'] <= 20 and m['maxMaintainedParameters'] <= 7
           and (m['moduleVendorIdentical'] or m['module']['cognitive'] <= 20) for m in modules)
assert all(d['vendorIdenticalMethod'] for d in diagnostics)
assert all(d['vendorIdenticalFile'] for d in unused)
assert all(d['vendorIdenticalFile'] for d in knip['issues'])
assert not cycles and not unresolved and not clones['duplicates']
print('Maintained static findings: 0. Full native findings retained with exact vendor identity witnesses.')
print('Modules:', len(modules), 'maintained methods:', sum(m['maintainedMethods'] for m in modules), 'graph:', len(native['modules']), 'nodes', len(edges), 'edges')

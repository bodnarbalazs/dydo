"""Apply unchanged DR048 thresholds to the final platform-union Istanbul evidence."""
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
sys.path.insert(0, str(root / 'DynaDocs.Tests/coverage'))
from typescript_join import join
walked = json.loads((out / 'walked.json').read_text())
targets = ['skills/engineering/statechart/' + r['path'] for r in walked if '/test/' not in r['path']]
methods = {'skills/engineering/statechart/' + r['path']: r for r in walked}
rows = join(out / 'coverage-final/lcov.info', root, root, targets, methods)
findings = []
for row in rows:
    original = {m['id']: m['vendorIdentical'] for m in methods[row['path']]['methods']}
    for method in row['methods']:
        method['lineCoverage'] = method['covered'] / method['total']
        method['hcrap'] = method['cc'] ** 2 * (1 - method['lineCoverage']) ** 3 + method['cognitive']
        method['vendorIdentical'] = original.get(method['id'], False)
        if not method['vendorIdentical'] and (method['hcrap'] > 20 or method['cognitive'] > 20 or method['parameters'] > 7):
            findings.append({'path': row['path'], 'method': method})
    for kind in ['lines', 'branches']:
        hits = list(row[kind].values())
        assert hits, (row['path'], kind, 'missing coverage counters')
        row[kind + 'Percent'] = 100 * sum(h > 0 for h in hits) / len(hits)
    if row['linesPercent'] < 80 or row['branchesPercent'] < 60:
        findings.append({'path': row['path'], 'gate': 'coverage'})
    row['maxMaintainedHcrap'] = max(m['hcrap'] for m in row['methods'] if not m['vendorIdentical'])
    print(row['path'], f"lines {row['linesPercent']:.2f}, branches {row['branchesPercent']:.2f}, HCRAP {row['maxMaintainedHcrap']:.4f}")
(out / 'coverage-final-methods.json').write_text(json.dumps(rows, indent=2) + '\n')
(out / 'coverage-final-result.json').write_text(json.dumps({'findings': findings, 'targets': targets, 'exit': 1 if findings else 0}, indent=2) + '\n')
raise SystemExit(1 if findings else 0)

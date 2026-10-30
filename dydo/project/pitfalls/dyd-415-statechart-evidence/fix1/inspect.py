"""Concise current measurements; raw reports remain beside this script."""
import json
from pathlib import Path

out = Path(__file__).resolve().parent
ps = out / 'powershell-coverage.json'
if ps.exists():
    data = json.loads(ps.read_text(encoding='utf-8-sig'))
    coverage = data['CodeCoverage']
    print('Pester:', data['TotalCount'], data['PassedCount'], data['FailedCount'])
    for file in coverage['AnalyzedFiles']:
        hit = [c for c in coverage['HitCommands'] if c['File'] == file]
        miss = [c for c in coverage['MissedCommands'] if c['File'] == file]
        print(file, 'commands', len(hit), '/', len(hit) + len(miss), 'misses', [(c['Line'], c['Command']) for c in miss])
for name in ['metrics.txt', 'typescript-strict-unused.txt', 'knip.txt', 'install.txt', 'docker-build.txt', 'linux-suite.txt', 'windows.txt']:
    file = out / name
    if file.exists():
        print('\n' + name)
        print('\n'.join(file.read_text(encoding='utf-8', errors='replace').splitlines()[-12:]))

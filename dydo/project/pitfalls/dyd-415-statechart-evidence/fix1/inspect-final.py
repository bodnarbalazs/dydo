"""Compact audit of the final source, native counters and launch interception."""
import json
from pathlib import Path

out = Path(__file__).resolve().parent
manifest = json.loads((out / 'source-manifest.json').read_text())
scope = json.loads((out / 'static-summary.json').read_text())
launches = [json.loads(line) for line in (out / 'windows-spawns.jsonl').read_text().splitlines()]
print('Identical vendor files:', sum(r['vendorIdentical'] for r in manifest['vendored_files']))
print('Clone totals:', scope['clones']['statistics']['total'])
print('Launch audit:', len(launches), 'observed;', sum(not r['hidden'] for r in launches), 'refused')
for name in ['windows.txt', 'linux-suite.txt', 'docs.txt']:
    print(name, '\n' + '\n'.join((out / name).read_text(encoding='utf-8').splitlines()[-10:]))

"""Summarize final raw results and retain native coverage counters for reproduction."""
import json
from pathlib import Path
import re
import shutil
import subprocess
import tarfile

out = Path(__file__).resolve().parent
root = out.parents[4]
temp = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1')
summary = {}
for name, file in [('windows', 'windows.txt'), ('linux', 'linux-suite.txt')]:
    text = (out / file).read_text()
    summary[name] = {key: float(re.findall(r'^# ' + key + r' ([\d.]+)$', text, re.M)[-1])
                     for key in ['tests', 'pass', 'fail', 'skipped', 'duration_ms']}
    assert summary[name]['fail'] == 0
summary['sourceArchive'] = json.loads((out / 'snapshot.json').read_text())['archive_sha256']
launches = [json.loads(line) for line in (out / 'windows-spawns.jsonl').read_text().splitlines()]
summary['consoleAudit'] = {'calls': len(launches), 'refused': sum(not r['hidden'] for r in launches)}
assert summary['consoleAudit']['refused'] == 0
methods = json.loads((out / 'coverage-final-methods.json').read_text())
summary['coverage'] = [{key: row[key] for key in ['path', 'linesPercent', 'branchesPercent', 'maxMaintainedHcrap']} for row in methods]
scope = json.loads((out / 'static-summary.json').read_text())
summary['static'] = {'files': len(scope['modules']), 'maintainedMethods': sum(m['maintainedMethods'] for m in scope['modules']),
                     'nodes': scope['graphModules'], 'edges': scope['graphEdges'], 'clones': len(scope['clones']['duplicates'])}
summary['coverageCounters'] = {label: len(list((temp / (label + '-final-normalized-v8')).glob('*.json')))
                               for label in ['windows', 'linux']}
(out / 'final-summary.json').write_text(json.dumps(summary, indent=2) + '\n')
old_archive = out / 'before-console/normalized-v8.tar.gz'
if not old_archive.exists():
    shutil.copy2(out / 'normalized-v8.tar.gz', old_archive)
with tarfile.open(out / 'normalized-v8.tar.gz', 'w:gz') as archive:
    for label in ['windows', 'linux']:
        archive.add(temp / (label + '-final-normalized-v8'), arcname=label)

doc = out / 'verification.md'
text = doc.read_text()
text = re.sub(r'Linux finished in [\d.]+ seconds', f"Linux finished in {summary['linux']['duration_ms'] / 1000:.3f} seconds", text)
text = text.replace('bits (mode 0775 in Git\'s archive and the actual run, recorded by `stat`).',
                    'bits (launcher 0775 and overlaid Java shim 0755, recorded by `stat`).')
start = text.index('| Maintained runtime module |')
end = text.index('\n\n', start)
table = ['| Maintained runtime module | Lines | Branches | Maximum maintained HCRAP |', '|---|---:|---:|---:|']
for row in methods:
    name = row['path'].split('/scripts/')[1]
    table.append(f"| `{name}` | {row['linesPercent']:.2f}% | {row['branchesPercent']:.2f}% | {row['maxMaintainedHcrap']:.4g} |")
text = text[:start] + '\n'.join(table) + text[end:]
text = re.sub(r'Linux produced 1,128 coverage files, 1,100 containing target counters; Windows\nproduced 193 files\.',
              f"The final captures contain {summary['coverageCounters']['linux']:,} Linux and "
              f"{summary['coverageCounters']['windows']:,} Windows files with target counters.", text)
doc.write_text(text, encoding='utf-8')
changed = out / 'changed-paths.txt'
changed.touch(exist_ok=True)
paths = set()
for args in [['git', 'diff', '--name-only', 'HEAD'], ['git', 'ls-files', '--others', '--exclude-standard']]:
    paths.update(subprocess.check_output(args, cwd=root, text=True, creationflags=subprocess.CREATE_NO_WINDOW).splitlines())
assert all(p.startswith(('skills/engineering/statechart/', 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1/'))
           or p == 'dydo/project/pitfalls/dyd-415-statechart-verification.md' for p in paths)
changed.write_text('\n'.join(sorted(paths)) + '\n')
print(json.dumps(summary, indent=2))

"""Bind proof, maintained scope and final source without rewriting the initial record."""
import hashlib
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
skill = root / 'skills/engineering/statechart'
initial = json.loads((out.parent / 'source-manifest.json').read_text())
files = []
for row in initial['vendored_files']:
    current = hashlib.sha256((skill / row['path']).read_bytes()).hexdigest()
    files.append({'path': row['path'], 'upstream_sha256': row['upstream_sha256'], 'candidate_sha256': current,
                  'vendorIdentical': current == row['upstream_sha256']})
added = [r['path'] for r in initial['added_files']]
added += subprocess.check_output(['git', 'diff', '--name-only', '--diff-filter=A', 'HEAD', '--', 'skills/engineering/statechart'], cwd=root, text=True).splitlines()
added += subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '--', 'skills/engineering/statechart'], cwd=root, text=True).splitlines()
added = sorted({p.removeprefix('skills/engineering/statechart/') for p in added})
record = {'parent': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(),
          'upstream_commit': initial['upstream_commit'], 'vendored_files': files,
          'added_files': [{'path': p, 'sha256': hashlib.sha256((skill / p).read_bytes()).hexdigest()} for p in added]}
(out / 'source-manifest.json').write_text(json.dumps(record, indent=2) + '\n')
tools = ['js_metrics.cjs', 'typescript_join.py', 'gate_inventory.py', 'knip_reporter.mjs', 'package.json', 'package-lock.json']
(out / 'producer-hashes.json').write_text(json.dumps({p: hashlib.sha256((root / 'DynaDocs.Tests/coverage' / p).read_bytes()).hexdigest() for p in tools}, indent=2) + '\n')
snapshot = json.loads((out / 'snapshot.json').read_text())
for name, row in snapshot['files'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == row['sha256'], name
windows = json.loads((out / 'windows-source.json').read_text())
for name, digest in windows.items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
proof = {p.relative_to(out).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
         for p in out.rglob('*') if p.is_file() and p.name != 'proof-manifest.json'}
(out / 'proof-manifest.json').write_text(json.dumps(proof, indent=2) + '\n')
print('Final runtime source matches both platform identities; proof hashes written.')

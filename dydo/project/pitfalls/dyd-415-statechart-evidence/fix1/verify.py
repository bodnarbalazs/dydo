"""Read-only verification of source/evidence hashes and the final committed tree."""
import hashlib
import json
from pathlib import Path
import subprocess

out = Path(__file__).resolve().parent
root = out.parents[4]
snapshot = json.loads((out / 'snapshot.json').read_text())
for name, row in snapshot['files'].items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == row['sha256'], name
windows = json.loads((out / 'windows-source.json').read_text())
for name, digest in windows.items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == digest, name
proof = json.loads((out / 'proof-manifest.json').read_text())
for name, digest in proof.items():
    assert hashlib.sha256((out / name).read_bytes()).hexdigest() == digest, name
clean = subprocess.check_output(['git', 'status', '--porcelain'], cwd=root, text=True,
                                creationflags=subprocess.CREATE_NO_WINDOW)
assert not clean, clean
candidate = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True,
                                    creationflags=subprocess.CREATE_NO_WINDOW).strip()
print(json.dumps({'candidate': candidate, 'snapshotFiles': len(snapshot['files']),
                  'windowsSources': len(windows), 'proofFiles': len(proof), 'clean': True}))

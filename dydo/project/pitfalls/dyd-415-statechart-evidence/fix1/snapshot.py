"""Immutable archive from HEAD plus disclosed owned runtime changes; preserve Git modes."""
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
temp = Path(os.environ['TEMP']) / 'dyd415-fix1'
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
archive = temp / 'source.tar'
subprocess.run(['git', 'archive', '--format=tar', '-o', str(archive), head], cwd=root, check=True)
changed = subprocess.check_output(['git', 'diff', '--name-only', 'HEAD', '--', 'skills/engineering/statechart'], cwd=root, text=True).splitlines()
added = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '--', 'skills/engineering/statechart'], cwd=root, text=True).splitlines()
with tarfile.open(archive, 'a') as tar:
    for name in changed + added:
        data = (root / name).read_bytes().replace(b'\r\n', b'\n')
        info = tarfile.TarInfo(name)
        info.size = len(data)
        mode = subprocess.check_output(['git', 'ls-files', '--stage', '--', name], cwd=root, text=True)
        info.mode = 0o755 if mode.startswith('100755') else 0o644
        tar.addfile(info, io.BytesIO(data))
with tarfile.open(archive) as tar:
    files = {m.name: {'sha256': hashlib.sha256(tar.extractfile(m).read()).hexdigest(), 'mode': oct(m.mode)}
             for m in tar.getmembers() if m.isfile() and m.name.startswith('skills/engineering/statechart/')}
record = {'head': head, 'overlay': changed + added, 'archive_sha256': hashlib.sha256(archive.read_bytes()).hexdigest(), 'files': files}
(out / 'snapshot.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({k: v for k, v in record.items() if k != 'files'}))

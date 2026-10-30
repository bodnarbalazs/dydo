"""Audit source identity and platform report monotonicity before accepting merged metrics."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[5]
out = Path(__file__).resolve().parent
snapshot = json.loads((out / 'snapshot.json').read_text())
for name, row in snapshot['files'].items():
    data = (root / name).read_bytes()
    if hashlib.sha256(data).hexdigest() != row['sha256']:
        print(name, 'CRLF', data.count(b'\r\n'), 'LF', data.count(b'\n'), 'canonical LF matches', hashlib.sha256(data.replace(b'\r\n', b'\n')).hexdigest() == row['sha256'])
temp = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1')
for folder in ['windows-normalized-v8', 'combined-normalized-v8']:
    for file in (temp / folder).glob('*.json'):
        if folder.startswith('combined') and not file.name.startswith('linux'):
            continue
        data = json.loads(file.read_text())
        cache = data.get('source-map-cache', {})
        found = next((v for k, v in cache.items() if k.endswith('/worker.ts')), None)
        if found:
            print(folder, 'cache', list(found), 'data', list(found.get('data', {})))
            contents = found.get('data', {}).get('sourcesContent', [])
            print('contents', [(len(s), s.count('\r\n'), s.count('\n')) for s in contents])
            break

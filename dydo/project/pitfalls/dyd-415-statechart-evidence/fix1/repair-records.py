"""Repair the metadata filename collision without rebuilding or changing the tested archive."""
import hashlib
import json
from pathlib import Path
import re
import tarfile

out = Path(__file__).resolve().parent
record = json.loads((out / 'snapshot.json').read_text())
if 'files' not in record:
    (out / 'snapshot-command.json').write_text(json.dumps(record, indent=2) + '\n')
    record = json.loads((out / 'snapshot.txt').read_text())
    archive = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1/source.tar')
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == record['archive_sha256']
    with tarfile.open(archive) as tar:
        record['files'] = {m.name: {'sha256': hashlib.sha256(tar.extractfile(m).read()).hexdigest(), 'mode': oct(m.mode)}
                           for m in tar.getmembers() if m.isfile() and m.name.startswith('skills/engineering/statechart/')}
    (out / 'snapshot.json').write_text(json.dumps(record, indent=2) + '\n')
    (out / 'snapshot-record-repair.json').write_text(json.dumps({
        'reason': 'final-driver command metadata used the snapshot artifact basename; command output retained the archive SHA',
        'archiveUnchanged': record['archive_sha256'], 'fileEntriesRecoveredFromArchive': len(record['files'])}, indent=2) + '\n')

doc = out / 'verification.md'
text = doc.read_text()
text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)',
              lambda m: m[0] if m[2].endswith('.md') else m[1] + ' (`' + m[2] + '`)', text)
doc.write_text(text, encoding='utf-8')

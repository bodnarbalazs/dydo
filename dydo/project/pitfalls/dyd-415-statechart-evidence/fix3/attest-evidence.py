"""Commit bookkeeping only: bind retained execution snapshots and refresh the manifest."""
import hashlib
import json
from pathlib import Path

out = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


for receipt, snapshot in [('driver-identity.json', 'executed-setup-driver.py'),
                          ('resume-driver-identity.json', 'executed-campaign-driver.py')]:
    assert json.loads((out / receipt).read_text())['sha256'] == digest(out / snapshot)
execution = json.loads((out / 'finalize-inherited.json').read_text())
assert execution['sha256'] == digest(out / 'finalize.py')
assert execution['reportSha256'] == digest(out / 'report.py')
identities = [json.loads((out / name).read_text()) for name in [
    'runtime-before.json', 'resume-runtime-before.json', 'runtime-after.json', 'finalize-runtime.json']]
assert all(value == identities[0] for value in identities)
assert json.loads((out / 'native-before.stdout').read_text()) == json.loads((out / 'native-restored.stdout').read_text())
for row in json.loads((out / 'input-identity.json').read_text()):
    assert digest(out / row['path']) == row['sha256'] == row['priorSha256']
cleanup = json.loads((out / 'sdk-path-cleanup.json').read_text())
assert cleanup['removedCount'] == 1 and cleanup['taskEntryAbsent'] and cleanup['otherEntriesPreserved']
attestation = dict(executionSnapshotsVerified=True, runtimeReceiptsEqual=True,
    restoredAssemblyPdbIdentityEqual=True, exactSourceAndSixCaseTemplateUnchanged=True,
    ownedSdkPathEntryRemoved=True, nativeTestsRerunForBookkeeping=False,
    scriptSha256=digest(Path(__file__)))
(out / 'final-attestation.json').write_text(json.dumps(attestation, indent=2) + '\n', encoding='utf-8')
manifest = [dict(path=p.relative_to(out).as_posix(), bytes=p.stat().st_size, sha256=digest(p))
    for p in sorted(out.rglob('*')) if p.is_file() and p.name != 'proof-manifest.json']
(out / 'proof-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print('Execution receipts and unchanged runtime verified; manifest files:', len(manifest))

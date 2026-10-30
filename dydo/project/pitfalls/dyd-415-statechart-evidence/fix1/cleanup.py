"""Inspect owned reservations and remove only this task's Docker tag; never prune shared resources."""
import json
import os
from pathlib import Path
import subprocess

out = Path(__file__).resolve().parent
records = [json.loads(p.read_text()) for p in out.glob('*-reservation.json')]
namespace = Path(os.environ['LOCALAPPDATA']) / 'lc-test/suite-slot'
tickets = [{'ticket': r['ticket'], 'retained': (namespace / (r['ticket'] + '.ticket')).exists()} for r in records]
assert all(not row['retained'] for row in tickets), tickets
lock = Path(os.environ['TEMP']) / 'dydo-fullsuite.lock'
owned_lock = lock.exists() and json.loads(lock.read_text()).get('owner', '').startswith('DYD-415 fix1')
assert not owned_lock, 'An owned dydo lock remains'
container = subprocess.run(['docker', 'ps', '-a', '--filter', 'name=^/dyd415-fix1-proof$', '--format', '{{.Names}}'], capture_output=True, text=True, check=True, creationflags=subprocess.CREATE_NO_WINDOW)
assert not container.stdout.strip(), container.stdout
image = subprocess.run(['docker', 'image', 'inspect', 'dyd415-fix1:proof'], capture_output=True, text=True, check=True, creationflags=subprocess.CREATE_NO_WINDOW)
(out / 'docker-image.json').write_text(image.stdout, encoding='utf-8')
removed = subprocess.run(['docker', 'image', 'rm', 'dyd415-fix1:proof'], capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
(out / 'docker-cleanup.txt').write_text(removed.stdout + removed.stderr, encoding='utf-8')
record = {'tickets': tickets, 'ownedDydoLockRetained': owned_lock, 'taskContainerPresent': False,
          'imageTagRemovalExit': removed.returncode,
          'retained': 'Task-local prerequisites, immutable source archive and raw counters remain in TEMP for review; no shared cache pruning.'}
(out / 'cleanup.json').write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2))
raise SystemExit(removed.returncode)

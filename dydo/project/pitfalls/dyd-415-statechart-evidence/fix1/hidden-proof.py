"""Run a TEMP copy: bounded real Windows checks, refusing unhidden launches before creation."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'
SCRIPTS = ROOT / 'skills/engineering/statechart/scripts'
sys.path.insert(0, 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY

env = dict(os.environ, JAVA='C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe',
           TLA2TOOLS=str(Path(os.environ['TEMP']) / 'dyd415-upstream/tla2tools.jar'),
           NODE_OPTIONS='--disable-warning=ExperimentalWarning --require=' + str(OUT / 'hidden-console.cjs'))

def run(name, files, pattern=None):
    audit = OUT / (name + '-spawns.jsonl')
    audit.write_text('')
    env['STATECHART_SPAWN_AUDIT'] = str(audit)
    args = ['node', '--test', '--test-concurrency=1']
    if pattern:
        args.append('--test-name-pattern=' + pattern)
    args += [str(SCRIPTS / 'test' / file) for file in files]
    start = datetime.datetime.now(datetime.timezone.utc).isoformat()
    with (OUT / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run(args, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT,
                                creationflags=subprocess.CREATE_NO_WINDOW)
    rows = [json.loads(line) for line in audit.read_text().splitlines()]
    (OUT / (name + '.json')).write_text(json.dumps({'argv': args, 'started': start,
        'exit': result.returncode, 'launches': len(rows), 'refused': sum(not r['hidden'] for r in rows)}, indent=2) + '\n')
    print(name, result.returncode, 'launches', len(rows), flush=True)
    return result.returncode

command = subprocess.list2cmdline([sys.executable, __file__])
with suite_slot.book(command, worktree=ROOT), machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
    env['LC_SUITE_SLOT_TICKET'] = os.environ['LC_SUITE_SLOT_TICKET']
    lock = Path(os.environ['TEMP']) / 'dydo-fullsuite.lock'
    owner = {'owner': 'DYD-415 fix1 hidden-console proof', 'pid': os.getpid(),
             'ticket': env['LC_SUITE_SLOT_TICKET'], 'command': command}
    with lock.open('x') as handle:
        json.dump(owner, handle)
    (OUT / 'hidden-reservation.json').write_text(json.dumps(owner, indent=2) + '\n')
    try:
        target = SCRIPTS / 'run.ts'
        corrected = target.read_bytes()
        try:
            target.write_bytes(corrected.replace(b', windowsHide: true', b''))
            red = run('hidden-red', ['windows.test.ts'], 'Windows resolves relative')
        finally:
            target.write_bytes(corrected)
        assert red == 1
        assert 'Visible console launch refused' in (OUT / 'hidden-red.txt').read_text()
        code = run('hidden-green', ['windows.test.ts', 'adaptation.test.ts'])
        identity = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in SCRIPTS.rglob('*') if p.is_file() and p.suffix in ['.ts', '.ps1'] and 'node_modules' not in p.parts}
        (OUT / 'hidden-source.json').write_text(json.dumps(identity, indent=2) + '\n')
    finally:
        lock.unlink()
print('Released owned dydo lock and LC FIFO.', flush=True)
raise SystemExit(code)

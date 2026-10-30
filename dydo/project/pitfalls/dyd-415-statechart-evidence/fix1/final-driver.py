"""Final source proof; run a normal TEMP copy with one Windows FIFO owner throughout."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import datetime

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'
TEMP = Path(os.environ['TEMP']) / 'dyd415-fix1'
SCRIPTS = ROOT / 'skills/engineering/statechart/scripts'
sys.path.insert(0, 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY

def run(name, args, cwd=ROOT, env=None):
    details = {'argv': [str(a) for a in args], 'cwd': str(cwd),
               'started': datetime.datetime.now(datetime.timezone.utc).isoformat()}
    with (OUT / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run(args, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT,
                                creationflags=subprocess.CREATE_NO_WINDOW)
    details['exit'] = result.returncode
    (OUT / (name + '.json')).write_text(json.dumps(details, indent=2) + '\n')
    print(name, result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(name + ' failed; inspect retained log')

command = subprocess.list2cmdline([sys.executable, __file__])
with suite_slot.book(command, worktree=ROOT), machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
    lock = Path(os.environ['TEMP']) / 'dydo-fullsuite.lock'
    owner = {'owner': 'DYD-415 fix1 final source proof', 'pid': os.getpid(),
             'ticket': os.environ['LC_SUITE_SLOT_TICKET'], 'command': command}
    with lock.open('x') as handle:
        json.dump(owner, handle)
    (OUT / 'final-reservation.json').write_text(json.dumps(owner, indent=2) + '\n')
    try:
        previous = OUT / 'before-console'
        previous.mkdir(exist_ok=True)
        for pattern in ['windows*.*', 'linux*.*', 'snapshot.json', 'static-summary.json', 'walked.json', 'coverage-final-*.json']:
            for file in OUT.glob(pattern):
                if file.is_file() and not (previous / file.name).exists():
                    shutil.copy2(file, previous / file.name)
        java = 'C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe'
        audit = OUT / 'windows-spawns.jsonl'
        audit.write_text('')
        env = dict(os.environ, JAVA=java, TLA2TOOLS=str(TEMP.parent / 'dyd415-upstream/tla2tools.jar'),
                   STATECHART_BASH='C:/Program Files/Git/bin/bash.exe',
                   STATECHART_PELE=str(TEMP.parent / 'dyd415-tools/node_modules/pele/dist/pele.js'),
                   XDG_CACHE_HOME=str(TEMP / 'render-cache'), npm_config_cache=str(TEMP / 'npm-cache'),
                   NODE_V8_COVERAGE=str(TEMP / 'final-windows-v8'), STATECHART_DIFFERENTIAL_SEED='415',
                   STATECHART_SPAWN_AUDIT=str(audit),
                   NODE_OPTIONS='--disable-warning=ExperimentalWarning --require=' + str(OUT / 'hidden-console.cjs'))
        env['PATH'] = str(Path(java).parent) + os.pathsep + env['PATH']
        identity = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in SCRIPTS.rglob('*') if p.is_file() and p.suffix in ['.ts', '.ps1'] and 'node_modules' not in p.parts}
        (OUT / 'windows-source.json').write_text(json.dumps(identity, indent=2) + '\n')
        package_command = json.loads((SCRIPTS / 'package.json').read_text())['scripts']['test']
        assert package_command == 'node --disable-warning=ExperimentalWarning --test --test-concurrency=1 test/*.test.ts'
        run('windows', ['node', '--disable-warning=ExperimentalWarning', '--test', '--test-concurrency=1', 'test/*.test.ts'], SCRIPTS, env)
        rows = [json.loads(line) for line in audit.read_text().splitlines()]
        assert rows and all(row['hidden'] for row in rows)
        run('snapshot-command', ['python', str(OUT / 'snapshot.py')])
        for name in ['linux.sh', 'filter-v8.cjs', 'Dockerfile']:
            shutil.copy2(OUT / name, TEMP / name)
        run('docker-build-final', ['docker', 'build', '--tag', 'dyd415-fix1:proof', str(TEMP)])
        run('linux', ['docker', 'run', '--rm', '--init', '--name', 'dyd415-fix1-proof', '--cpus', '2', '--memory', '4g',
                     '--mount', f'type=bind,source={TEMP},target=/input,readonly',
                     '--mount', f'type=bind,source={OUT},target=/evidence', 'dyd415-fix1:proof', 'bash', '/input/linux.sh'])
    finally:
        lock.unlink()
print('Released owned dydo lock and LC FIFO.', flush=True)

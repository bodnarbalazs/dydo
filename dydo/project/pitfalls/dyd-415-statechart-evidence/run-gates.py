import json
import os
from pathlib import Path
import subprocess
import sys
import datetime

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
EVIDENCE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence'
TOOLS = Path('C:/Users/User/AppData/Local/Temp/dyd415-tools')
SCRATCH = Path('C:/Users/User/AppData/Local/Temp/dyd415-test-temp')
SCRATCH.mkdir(exist_ok=True)
env = dict(os.environ, TLA2TOOLS='C:/Users/User/AppData/Local/Temp/dyd415-upstream/tla2tools.jar',
           JAVA='C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe',
           TEMP=str(SCRATCH), TMP=str(SCRATCH), TMPDIR=str(SCRATCH),
           STATECHART_BASH='C:/Program Files/Git/bin/bash.exe',
           STATECHART_PELE=str(TOOLS / 'node_modules/pele/dist/pele.js'),
           STATECHART_EVIDENCE_DIR=str(EVIDENCE),
           XDG_CACHE_HOME='C:/Users/User/AppData/Local/Temp/dyd415-render-cache',
           npm_config_cache='C:/Users/User/AppData/Local/Temp/dyd415-npm-cache',
           NODE_OPTIONS='--disable-warning=ExperimentalWarning',
           STATECHART_DIFFERENTIAL_SEED='415')

def run(name, args, cwd=ROOT):
    print(json.dumps({'gate': name, 'argv': args, 'cwd': str(cwd)}, ensure_ascii=True), flush=True)
    with (EVIDENCE / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run(args, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT)
    print(f'{name}: exit {result.returncode}', flush=True)
    return result.returncode

if sys.argv[1] == 'acceptance':
    raise SystemExit(run('acceptance', ['node', '--test', 'skills/engineering/statechart/scripts/test/acceptance.test.ts']))
if sys.argv[1] == 'windows':
    raise SystemExit(run('windows', ['node', '--test', 'skills/engineering/statechart/scripts/test/windows.test.ts']))
if sys.argv[1] == 'full':
    sys.path.insert(0, 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import machine_lock, HEAVY
    scripts = ROOT / 'skills/engineering/statechart/scripts'
    package = json.loads((scripts / 'package.json').read_text())
    selected = sorted(p.name for p in (scripts / 'test').glob('*.test.ts'))
    assert selected
    command = 'npm test => ' + package['scripts']['test']
    print(json.dumps({'selected_files': selected, 'resolved_command': command, 'candidate_base': '8553172dfaa7601d34f6b884c10350183f9faaa5'}), flush=True)
    with suite_slot.book(command, worktree=ROOT), machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
        env['LC_SUITE_SLOT_TICKET'] = os.environ['LC_SUITE_SLOT_TICKET']
        lock = Path('C:/Users/User/AppData/Local/Temp/dydo-fullsuite.lock')
        # Nonblocking exclusive creation after FIFO admission: no lock-order wait.
        with lock.open('x', encoding='utf-8') as handle:
            details = {'owner': 'DYD-415 code-writer/gpt-6-astra', 'pid': os.getpid(), 'ticket': env['LC_SUITE_SLOT_TICKET'], 'command': command, 'cwd': str(scripts), 'started': datetime.datetime.now(datetime.timezone.utc).isoformat()}
            json.dump(details, handle)
            handle.flush()
            (EVIDENCE / 'reservation.json').write_text(json.dumps(details, indent=2) + '\n')
        try:
            code = run('upstream-suite', ['npm.cmd', 'test'], scripts)
        finally:
            lock.unlink()
        raise SystemExit(code)
if sys.argv[1] == 'static':
    raise SystemExit(run('typescript', ['node', str(TOOLS / 'node_modules/typescript/bin/tsc'), '-p', 'skills/engineering/statechart/scripts/tsconfig.json']))

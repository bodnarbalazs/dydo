"""Task-local proof driver. Run a TEMP copy on Windows; keep the FIFO for every child."""
import contextlib
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'
TEMP = Path(os.environ['TEMP']) / 'dyd415-fix1'
TOOLS = Path(os.environ['TEMP']) / 'dyd415-tools'
SCRIPTS = ROOT / 'skills/engineering/statechart/scripts'
JAVA = 'C:/Program Files/Microsoft/jdk-11.0.16.101-hotspot/bin/java.exe'
TEMP.mkdir(exist_ok=True)
sys.path.insert(0, 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY

def run(name, args, cwd=ROOT, env=None):
    details = {'argv': args, 'cwd': str(cwd), 'started': datetime.datetime.now(datetime.timezone.utc).isoformat()}
    print(json.dumps({'step': name, **details}), flush=True)
    with (OUT / (name + '.txt')).open('w', encoding='utf-8') as log:
        result = subprocess.run(args, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
    details['exit'] = result.returncode
    (OUT / (name + '.json')).write_text(json.dumps(details, indent=2) + '\n')
    print(f'{name}: exit {result.returncode}', flush=True)
    return result.returncode

def environment():
    scratch = TEMP / 'scratch'
    scratch.mkdir(exist_ok=True)
    env = dict(os.environ, TLA2TOOLS=str(Path(os.environ['TEMP']) / 'dyd415-upstream/tla2tools.jar'),
               JAVA=JAVA, TEMP=str(scratch), TMP=str(scratch), TMPDIR=str(scratch),
               STATECHART_BASH='C:/Program Files/Git/bin/bash.exe',
               STATECHART_PELE=str(TOOLS / 'node_modules/pele/dist/pele.js'),
               XDG_CACHE_HOME=str(TEMP / 'render-cache'), npm_config_cache=str(TEMP / 'npm-cache'),
               NODE_OPTIONS='--disable-warning=ExperimentalWarning', STATECHART_DIFFERENTIAL_SEED='415')
    env['PATH'] = str(Path(JAVA).parent) + os.pathsep + env['PATH']
    return env

def action(mode):
    if mode == 'final-assurance':
        for name, args in [
            ('assurance-final', ['python', str(OUT / 'assurance.py')]),
            ('knip-audit-final', ['python', str(OUT / 'knip-audit.py')]),
            ('static-audit-final', ['python', str(OUT / 'static-summary.py')]),
            ('coverage-windows-final', ['python', str(OUT / 'coverage.py'), '--windows-only']),
            ('coverage-linux-final', ['python', str(OUT / 'coverage.py'), '--linux-only']),
            ('coverage-merge-final', ['node', str(OUT / 'merge-coverage.cjs')]),
            ('coverage-gate-final', ['python', str(OUT / 'coverage-summary.py')]),
        ]:
            code = run(name, args)
            if code:
                return code
        return 0
    if mode == 'install':
        assurance = TEMP / 'assurance'
        assurance.mkdir(exist_ok=True)
        for name in ['package.json', 'package-lock.json', 'js_metrics.cjs']:
            shutil.copy2(ROOT / 'DynaDocs.Tests/coverage' / name, assurance / name)
        return run('install', ['npm.cmd', 'ci', '--ignore-scripts', '--no-audit', '--no-fund'], assurance, environment())
    if mode == 'docker-build':
        shutil.copy2(OUT / 'Dockerfile', TEMP / 'Dockerfile')
        return run(mode, ['docker', 'build', '--tag', 'dyd415-fix1:proof', str(TEMP)])
    if mode in ['java-absolute', 'java-bare', 'java-unset']:
        env = environment()
        if mode.endswith('unset'):
            env.pop('JAVA')
        elif mode.endswith('bare'):
            env['JAVA'] = 'java'
        return run(mode, ['node', '--test', '--test-name-pattern=Windows resolves|Windows uses', str(SCRIPTS / 'test/windows.test.ts')], env=env)
    if mode == 'windows':
        env = environment()
        env['NODE_V8_COVERAGE'] = str(TEMP / 'windows-v8')
        identity = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                    for p in SCRIPTS.rglob('*') if p.is_file() and p.suffix in ['.ts', '.ps1'] and 'node_modules' not in p.parts}
        (OUT / 'windows-source.json').write_text(json.dumps(identity, indent=2) + '\n')
        return run(mode, ['npm.cmd', 'test'], SCRIPTS, env)
    if mode == 'java-matrix':
        results = [action('java-' + config) for config in ['absolute', 'bare', 'unset']]
        return max(results)
    if mode == 'java-proof':
        action('java-revert')
        return action('java-matrix')
    if mode == 'mutation':
        target = SCRIPTS / 'test/acceptance.test.ts'
        original = target.read_bytes()
        needle = b'The shortest path has 1 step:'
        assert original.count(needle) == 1
        try:
            target.write_bytes(original.replace(needle, b'The shortest path has 2 steps:'))
            code = run('acceptance-mutation-red', ['node', '--test', '--test-name-pattern=A broken model', str(target)], env=environment())
        finally:
            target.write_bytes(original)
        assert code == 1, 'The behavioral expected-value mutation must fail an assertion'
        return run('acceptance-mutation-restored', ['node', '--test', '--test-name-pattern=A broken model', str(target)], env=environment())
    if mode == 'java-revert':
        target = SCRIPTS / 'test/windows.test.ts'
        corrected = target.read_bytes()
        original = subprocess.check_output(['git', 'show', '53df22e:skills/engineering/statechart/scripts/test/windows.test.ts'], cwd=ROOT)
        env = environment()
        env.pop('JAVA')
        try:
            target.write_bytes(original)
            code = run('java-reverted-red', ['node', '--test', '--test-name-pattern=Windows resolves relative', str(target)], env=env)
        finally:
            target.write_bytes(corrected)
        assert code == 1, 'Restoring the reviewed test must reproduce the JAVA defect'
        return 0
    if mode == 'windows-proof':
        for step in ['java-revert', 'mutation', 'windows']:
            code = action(step)
            if code:
                return code
        return 0
    if mode == 'linux':
        return run(mode, ['docker', 'run', '--rm', '--init', '--name', 'dyd415-fix1-proof', '--cpus', '2', '--memory', '4g',
                         '--mount', f'type=bind,source={TEMP},target=/input,readonly',
                         '--mount', f'type=bind,source={OUT},target=/evidence',
                         'dyd415-fix1:proof', 'bash', '/input/linux.sh'])
    raise ValueError(mode)

mode = sys.argv[1]
command = subprocess.list2cmdline([sys.executable, __file__, mode])
with suite_slot.book(command, worktree=ROOT), machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
    lock = Path(os.environ['TEMP']) / 'dydo-fullsuite.lock'
    details = {'owner': 'DYD-415 fix1 code-writer/gpt-6-astra/high', 'pid': os.getpid(),
               'ticket': os.environ['LC_SUITE_SLOT_TICKET'], 'command': command}
    with lock.open('x', encoding='utf-8') as handle:
        json.dump(details, handle)
    try:
        (OUT / (mode + '-reservation.json')).write_text(json.dumps(details, indent=2) + '\n')
        code = action(mode)
    finally:
        lock.unlink()
print('Released owned dydo lock and LC FIFO.', flush=True)
raise SystemExit(code)

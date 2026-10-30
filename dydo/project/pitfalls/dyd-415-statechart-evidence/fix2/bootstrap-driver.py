"""Run a TEMP copy; all material children inherit one Windows LC reservation."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import urllib.request
import zipfile

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
EVIDENCE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2'
WORK = Path(os.environ['TEMP']) / 'dyd415-fix2'
PIN = '0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6'
sys.path.insert(0, 'C:/Users/User/.t3/worktrees/LC/t3-6a75af47')
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(name, value):
    (EVIDENCE / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def run(name, args, cwd=WORK, allowed=(0,), stdin=None):
    args = list(map(str, args))
    started = time.time()
    result = subprocess.run(args, cwd=cwd, input=stdin, capture_output=True,
                            creationflags=subprocess.CREATE_NO_WINDOW, timeout=600)
    (EVIDENCE / (name + '.stdout')).write_bytes(result.stdout)
    (EVIDENCE / (name + '.stderr')).write_bytes(result.stderr)
    save(name + '.command.json', dict(argv=args, cwd=str(cwd), exit=result.returncode,
                                    seconds=time.time() - started, hidden=True,
                                    ticket=os.environ['LC_SUITE_SLOT_TICKET']))
    print(name, result.returncode, flush=True)
    if result.returncode not in allowed:
        raise RuntimeError(name + ' failed; inspect retained evidence')
    return result.stdout


def runtime_identity():
    files = run('runtime-paths', ['git', 'ls-tree', '-r', '--name-only', PIN,
                                'skills/engineering/statechart/scripts'], ROOT).decode().splitlines()
    rows = []
    for path in files:
        if Path(path).suffix not in ('.ts', '.ps1'):
            continue
        original = subprocess.check_output(['git', 'show', PIN + ':' + path], cwd=ROOT,
                                           creationflags=subprocess.CREATE_NO_WINDOW)
        current = (ROOT / path).read_bytes()
        if current != original:
            raise ValueError('Runtime byte mismatch: ' + path)
        rows.append(dict(path=path, sha256=hashlib.sha256(current).hexdigest(), bytes=len(current)))
    return rows


def provision():
    run('dotnet-info', ['dotnet', '--info'])
    package = WORK / 'PSScriptAnalyzer.1.24.0.nupkg'
    url = 'https://www.powershellgallery.com/api/v2/package/PSScriptAnalyzer/1.24.0'
    if not package.exists():
        urllib.request.urlretrieve(url, package)
    save('pssa-package.json', dict(version='1.24.0', url=url, sha256=digest(package), bytes=package.stat().st_size))
    module = WORK / 'PSScriptAnalyzer' / '1.24.0'
    module.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(package) as archive:
        archive.extractall(module)
    tools = WORK / 'tools'
    if not (tools / 'altcover.exe').exists():
        run('altcover-install', ['dotnet', 'tool', 'install', 'altcover.global', '--version', '9.0.102', '--tool-path', tools])
    metrics = WORK / 'metrics'
    metrics.mkdir(exist_ok=True)
    for source in (ROOT / 'DynaDocs.Tests/coverage/metrics').glob('*'):
        if source.is_file():
            shutil.copy2(source, metrics / source.name)
    shutil.copy2(ROOT / 'Directory.Build.props', WORK / 'Directory.Build.props')
    shutil.copy2(ROOT / '.editorconfig', WORK / '.editorconfig')
    sonar = WORK / 'DynaDocs.Tests/coverage'
    sonar.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'DynaDocs.Tests/coverage/SonarLint.xml', sonar / 'SonarLint.xml')
    run('metrics-build', ['dotnet', 'build', metrics / 'GateMetrics.csproj', '-c', 'Release',
                          '-m:1', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'])


def main():
    mode = argparse.ArgumentParser()
    mode.add_argument('mode', choices=['provision', 'measure'])
    args = mode.parse_args()
    WORK.mkdir(exist_ok=True)
    os.environ.update(DOTNET_CLI_HOME=str(WORK / 'dotnet-home'), NUGET_PACKAGES=str(WORK / 'packages'),
                      DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
                      DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1')
    command = subprocess.list2cmdline([sys.executable, __file__, args.mode])
    with suite_slot.book(command, worktree=ROOT), machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
        lock = Path(os.environ['TEMP']) / 'dydo-fullsuite.lock'
        owner = dict(owner='DYD-415 fix2 ' + args.mode, pid=os.getpid(),
                     ticket=os.environ['LC_SUITE_SLOT_TICKET'], command=command)
        with lock.open('x') as handle:
            json.dump(owner, handle)
        save(args.mode + '-reservation.json', owner)
        try:
            before = runtime_identity()
            save('runtime-before.json', before)
            if args.mode == 'provision':
                provision()
            else:
                sys.path.insert(0, str(EVIDENCE))
                import measure
                measure.execute(sys.modules[__name__])
            after = runtime_identity()
            assert before == after
            save('runtime-after.json', after)
        finally:
            lock.unlink()
    save(args.mode + '-released.json', dict(**owner, fifoReleased=True, heavyReleased=True, dydoLockReleased=True))


if __name__ == '__main__':
    main()

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

sys.dont_write_bytecode = True

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
EVIDENCE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2'
WORK = Path(os.environ.get('DYD415_ASSURANCE_WORK', str(Path(os.environ['TEMP']) / 'dyd415-fix2')))
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
    if name != 'runtime-paths' and (EVIDENCE / (name + '.command.json')).exists():
        previous = EVIDENCE / 'attempts' / (name + '-' + str(time.time_ns()))
        previous.mkdir(parents=True)
        for extension in ['.command.json', '.stdout', '.stderr']:
            shutil.move(str(EVIDENCE / (name + extension)), previous / (name + extension))
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
    if digest(package) != 'e86c97d44bb1bc8a1de35e753b85ea1d938f6f9f881639a181507e079bca4556':
        raise ValueError('PSScriptAnalyzer 1.24.0 package hash mismatch')
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
    sources = [p for p in metrics.glob('*') if p.is_file()] + [WORK / 'Directory.Build.props', WORK / '.editorconfig', sonar / 'SonarLint.xml']
    save('metric-tool-sources.json', [dict(path=p.relative_to(WORK).as_posix(), sha256=digest(p)) for p in sources])
    run('metrics-build', ['dotnet', 'build', metrics / 'GateMetrics.csproj', '-c', 'Release',
                          '-m:1', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'])


def main():
    mode = argparse.ArgumentParser()
    mode.add_argument('mode', choices=['provision', 'measure', 'instrument', 'analyze', 'baseline', 'finalize'])
    args = mode.parse_args()
    if not WORK.resolve().is_relative_to(Path(os.environ['TEMP']).resolve()):
        raise ValueError('Assurance prerequisites and builds must stay under task TEMP')
    WORK.mkdir(exist_ok=True)
    os.environ.update(DOTNET_CLI_HOME=str(WORK / 'dotnet-home'), NUGET_PACKAGES=str(WORK / 'packages'),
                      NUGET_HTTP_CACHE_PATH=str(WORK / 'nuget-http'), NUGET_SCRATCH=str(WORK / 'nuget-scratch'),
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
        save(args.mode + '-driver.json', dict(path=__file__, sha256=digest(Path(__file__))))
        failure = None
        try:
            before = runtime_identity()
            save('runtime-before.json', before)
            if args.mode == 'provision':
                provision()
            elif args.mode == 'finalize':
                run('docs', ['dydo.exe', 'check'], ROOT)
                run('whitespace', ['git', 'diff', '--check', '--cached'], ROOT)
                run('historical-proof-unchanged', ['git', 'diff', '--exit-code', PIN, '--',
                                                 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'], ROOT)
            elif args.mode == 'baseline':
                run('baseline-build', ['dotnet', 'build', WORK / 'tests/Adapter.Tests.csproj', '-c', 'Debug',
                                      '--no-incremental', '-m:1', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'])
                run('baseline-tests', ['dotnet', 'test', WORK / 'tests/Adapter.Tests.csproj', '-c', 'Debug',
                                      '--no-build', '--no-restore', '--logger', 'trx;LogFileName=baseline.trx',
                                      '--results-directory', EVIDENCE / 'baseline-results', '--',
                                      'RunConfiguration.TreatNoTestsAsError=true', 'RunConfiguration.MaxCpuCount=1'], allowed=(0, 1))
            else:
                sys.path.insert(0, str(EVIDENCE))
                import measure
                if args.mode == 'measure':
                    measure.execute(sys.modules[__name__])
                elif args.mode == 'instrument':
                    measure.instrument(sys.modules[__name__])
                else:
                    measure.analyze(sys.modules[__name__])
            after = runtime_identity()
            assert before == after
            save('runtime-after.json', after)
        except BaseException as error:
            failure = error
            save(args.mode + '-failure.json', dict(type=type(error).__name__, message=str(error)))
        finally:
            lock.unlink()
    save(args.mode + '-released.json', dict(**owner, fifoReleased=True, heavyReleased=True, dydoLockReleased=True))
    if failure is not None:
        raise failure
    if args.mode == 'finalize':
        save('proof-manifest.json', [dict(path=p.relative_to(EVIDENCE).as_posix(), bytes=p.stat().st_size, sha256=digest(p))
                                     for p in sorted(EVIDENCE.rglob('*')) if p.is_file()
                                     and p.name != 'proof-manifest.json' and '__pycache__' not in p.parts])


if __name__ == '__main__':
    main()

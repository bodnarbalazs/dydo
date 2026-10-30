"""Superseding fresh-output recipe. Historical execution remains in fix3."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
from config import ROOT, OUT, OLD, TEMP, WORK, PRIOR, PIN, METRICS, LC, RECIPE, preflight
VERSION = '6.0.4'
PACKAGE_SHA256 = '48ad640bc67eb52e0f95c2f80f6368b24088033c5792e834d7ec4ae2ae543217'
sys.path.insert(0, str(LC))
from tools.testing.runner import suite_slot
from tools.testing.runner.machine_lock import machine_lock, HEAVY


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(name, value):
    (OUT / name).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


def run(name, argv, cwd=WORK, allowed=(0,)):
    assert not (OUT / (name + '.command.json')).exists(), 'Never overwrite an attempt'
    started = time.time()
    result = subprocess.run(list(map(str, argv)), cwd=cwd, capture_output=True,
                            creationflags=subprocess.CREATE_NO_WINDOW, timeout=600)
    (OUT / (name + '.stdout')).write_bytes(result.stdout)
    (OUT / (name + '.stderr')).write_bytes(result.stderr)
    save(name + '.command.json', dict(argv=list(map(str, argv)), cwd=str(cwd),
         exit=result.returncode, seconds=time.time() - started, hidden=True,
         ticket=os.environ['LC_SUITE_SLOT_TICKET']))
    print(name, result.returncode, flush=True)
    assert result.returncode in allowed, name + ' failed; inspect raw evidence'
    return result


def runtime_identity():
    paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', PIN,
        'skills/engineering/statechart/scripts'], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().splitlines()
    rows = []
    for path in paths:
        if Path(path).suffix not in ('.ts', '.ps1'):
            continue
        original = subprocess.check_output(['git', 'show', PIN + ':' + path], cwd=ROOT,
            creationflags=subprocess.CREATE_NO_WINDOW)
        assert (ROOT / path).read_bytes() == original, path
        rows.append(dict(path=path, bytes=len(original), sha256=sha(ROOT / path)))
    return rows


def assembly(name):
    return json.loads(run(name, ['dotnet', METRICS, '--assembly',
        WORK / 'tests/bin/Debug/net10.0/Adapter.dll', '--root', WORK,
        '--project', WORK / 'adapter/Adapter.csproj']).stdout)


def tests(name):
    return run(name + '-tests', ['dotnet', 'test', WORK / 'tests/Adapter.Tests.csproj',
        '-c', 'Debug', '--no-build', '--no-restore', '--logger', 'trx;LogFileName=' + name + '.trx',
        '--results-directory', OUT / (name + '-results'), '--',
        'RunConfiguration.TreatNoTestsAsError=true', 'RunConfiguration.MaxCpuCount=1'], allowed=(0, 1))


def target():
    assert os.environ.get('LC_SUITE_SLOT_TICKET'), 'Coverlet target must inherit admission'
    assert (TEMP / 'dydo-fullsuite.lock').exists()
    assembly('native-instrumented')
    sys.exit(tests('instrumented').returncode)


def measure():
    assert not WORK.exists(), 'Fresh owned output required'
    WORK.mkdir()
    os.environ.update(DOTNET_CLI_HOME=str(WORK / 'dotnet-home'),
        NUGET_PACKAGES=str(WORK / 'packages'), NUGET_HTTP_CACHE_PATH=str(WORK / 'nuget-http'),
        NUGET_SCRATCH=str(WORK / 'nuget-scratch'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0',
        MSBUILDDISABLENODEREUSE='1')
    # Reuse the verified metric executable, but compile the exact source and tests afresh.
    expected_tools = json.loads((OLD / 'metric-tool-sources.json').read_text())
    assert all(sha(PRIOR / row['path']) == row['sha256'] for row in expected_tools)
    save('metric-tool-reuse.json', dict(binary=str(METRICS), sha256=sha(METRICS),
         sourceManifestSha256=sha(OLD / 'metric-tool-sources.json')))
    for name in ['Directory.Build.props', '.editorconfig', 'DynaDocs.Tests/coverage/SonarLint.xml']:
        dest = WORK / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / name, dest)
    for name, folder in [('Adapter.csproj', 'adapter'), ('Adapter.Tests.csproj', 'tests'),
                         ('AdapterTests.cs', 'tests')]:
        dest = WORK / folder / name
        dest.parent.mkdir(exist_ok=True)
        shutil.copy2(OLD / (name + '.template'), dest)
        shutil.copy2(dest, OUT / (name + '.template'))
    extraction = json.loads((OLD / 'extraction.json').read_text())
    source = (ROOT / extraction['source']).read_bytes()
    assert hashlib.sha256(source).hexdigest() == extraction['sourceSha256']
    code = source[extraction['excerptByteOffset']:extraction['excerptByteOffset'] + extraction['excerptBytes']]
    assert code == (OLD / 'StatechartJob.cs.exact').read_bytes()
    assert hashlib.sha256(code).hexdigest() == extraction['excerptSha256']
    (WORK / 'adapter/StatechartJob.cs').write_bytes(code)
    (OUT / 'StatechartJob.cs.exact').write_bytes(code)
    save('extraction.json', extraction)
    save('input-identity.json', [dict(path=name, sha256=sha(OUT / name),
         priorSha256=sha(OLD / name)) for name in ['StatechartJob.cs.exact',
         'Adapter.csproj.template', 'Adapter.Tests.csproj.template', 'AdapterTests.cs.template']])
    run('dotnet-info', ['dotnet', '--info'])
    run('coverlet-install', ['dotnet', 'tool', 'install', 'coverlet.console', '--version', VERSION,
         '--tool-path', WORK / 'tools'])
    campaign()


def campaign():
    for name, folder in [('Adapter.csproj', 'adapter'), ('Adapter.Tests.csproj', 'tests'),
                         ('AdapterTests.cs', 'tests')]:
        assert (WORK / folder / name).read_bytes() == (OLD / (name + '.template')).read_bytes()
    assert (WORK / 'adapter/StatechartJob.cs').read_bytes() == (OLD / 'StatechartJob.cs.exact').read_bytes()
    packages = list((WORK / 'tools/.store/coverlet.console' / VERSION).rglob('coverlet.console.' + VERSION + '.nupkg'))
    assert len(packages) == 1
    assert sha(packages[0]) == PACKAGE_SHA256, 'Pinned Coverlet package hash mismatch'
    save('coverlet-package.json', dict(id='coverlet.console', version=VERSION,
         path=str(packages[0]), sha256=sha(packages[0]), bytes=packages[0].stat().st_size))
    tool = WORK / 'tools/coverlet.exe'
    run('coverlet-version', [tool, '--version'])
    run('coverlet-help', [tool, '--help'])
    run('tests-build', ['dotnet', 'build', WORK / 'tests/Adapter.Tests.csproj', '-c', 'Debug',
        '-m:1', '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'])
    for folder in ['adapter', 'tests']:
        shutil.copy2(WORK / folder / 'packages.lock.json', OUT / (folder + '-packages.lock.json'))
    run('compiler-settings', ['dotnet', 'msbuild', WORK / 'adapter/Adapter.csproj',
        '-getProperty:NETCoreSdkVersion,TargetFramework,LangVersion,Optimize,DebugType,Nullable,TreatWarningsAsErrors,EnforceCodeStyleInBuild'])
    before = assembly('native-before')
    save('binary-before.json', {name: sha(WORK / 'tests/bin/Debug/net10.0' / name)
         for name in ['Adapter.dll', 'Adapter.pdb', 'Adapter.Tests.dll', 'Adapter.Tests.pdb']})
    discovery = run('discovery', ['dotnet', 'test', WORK / 'tests/Adapter.Tests.csproj', '-c',
         'Debug', '--no-build', '--no-restore', '--list-tests']).stdout.decode('utf-8-sig')
    expected = json.loads((OLD / 'native-selection.json').read_text())['expected']
    assert len(expected) == 6 and all(discovery.count('AdapterTests.' + n) == 1 for n in expected)
    save('selection.json', dict(expected=expected, count=6, fullPlatformSuite=False))
    tests('baseline')
    run('coverlet', [tool, WORK / 'tests/bin/Debug/net10.0/Adapter.Tests.dll',
        '--target', sys.executable, '--targetargs', subprocess.list2cmdline([str(RECIPE / 'driver.py'), 'target']),
        '--include', '[Adapter]*', '--format', 'json', '--format', 'opencover',
        '--output', OUT / 'coverage', '--verbosity', 'detailed'], allowed=(0, 1))
    after = assembly('native-restored')
    assert before == after, 'Coverlet must restore exact assembly/PDB'
    assert json.loads((OUT / 'binary-before.json').read_text()) == {
        name: sha(WORK / 'tests/bin/Debug/net10.0' / name)
        for name in ['Adapter.dll', 'Adapter.pdb', 'Adapter.Tests.dll', 'Adapter.Tests.pdb']}
    save('restoration.json', dict(exactAssemblyPdbRestored=True, testsBinaryUnchanged=True))
    run('roslyn-project', ['dotnet', METRICS, '--project', WORK / 'adapter/Adapter.csproj', '--root', WORK])
    analyze()
    run('retained-counter-report', [sys.executable, RECIPE / 'finalize.py', 'child'], ROOT)


def analyze():
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    parity = {}
    for campaign in ['baseline', 'instrumented']:
        tree = ET.parse(OUT / (campaign + '-results') / (campaign + '.trx'))
        rows = [dict(name=r.attrib['testName'], outcome=r.attrib['outcome'],
                     output='\n'.join(r.itertext()).strip())
                for r in tree.findall('.//t:UnitTestResult', ns)]
        expected = {'AdapterTests.' + n for n in json.loads((OUT / 'selection.json').read_text())['expected']}
        assert {r['name'] for r in rows} == expected and len(rows) == 6
        parity[campaign] = sorted(rows, key=lambda r: r['name'])
    parity['sameOutcomes'] = all(a['outcome'] == b['outcome'] for a, b in
        zip(parity['baseline'], parity['instrumented']))
    save('native-test-parity.json', parity)


def main():
    assert sys.argv[1:] in (['measure'], ['preflight'])
    # The harness redirects these variables; this commission requires the real host namespace.
    os.environ.update(LOCALAPPDATA=str(TEMP.parent), TEMP=str(TEMP), TMP=str(TEMP))
    os.environ.update(DOTNET_CLI_HOME=str(WORK / 'dotnet-home'),
        NUGET_PACKAGES=str(WORK / 'packages'), NUGET_HTTP_CACHE_PATH=str(WORK / 'nuget-http'),
        NUGET_SCRATCH=str(WORK / 'nuget-scratch'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0',
        MSBUILDDISABLENODEREUSE='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false',
        DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false')
    command = subprocess.list2cmdline([sys.executable, __file__, sys.argv[1]])
    with suite_slot.book(command, worktree=ROOT) as booking, machine_lock(HEAVY,
            on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
        lock = TEMP / 'dydo-fullsuite.lock'
        owner = dict(owner='DYD-415 portable recipe', pid=os.getpid(), command=command,
             ticket=os.environ['LC_SUITE_SLOT_TICKET'], namespace=str(suite_slot.live().directory),
             inherited=booking.inherited, owned=booking.owned)
        with lock.open('x') as handle:
            json.dump(owner, handle)
        initialized = False
        failure = None
        try:
            checks = preflight()
            OUT.mkdir(parents=True, exist_ok=False)
            initialized = True
            save('preflight.json', checks)
            save('reservation.json', owner)
            save('driver-identity.json', dict(path=__file__, sha256=sha(Path(__file__))))
            before = runtime_identity()
            save('runtime-before.json', before)
            if sys.argv[1] == 'measure':
                measure()
            after = runtime_identity()
            assert before == after
            save('runtime-after.json', after)
        except BaseException as error:
            if initialized:
                save('failure.json', dict(type=type(error).__name__, message=str(error)))
            failure = error
        finally:
            lock.unlink()
    if initialized:
        save('released.json', dict(**owner, fifoReleased=booking.owned,
            inheritedReturned=booking.inherited, heavyReleased=True, dydoLockReleased=True))
        save('proof-manifest.json', [dict(path=p.relative_to(OUT).as_posix(), bytes=p.stat().st_size,
            sha256=sha(p)) for p in sorted(OUT.rglob('*')) if p.is_file() and p.name != 'proof-manifest.json'])
    if failure is not None:
        raise failure


if __name__ == '__main__':
    if sys.argv[1:] == ['target']:
        target()
    else:
        main()

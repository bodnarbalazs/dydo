"""Join retained counters and check fix3 evidence; never rerun native tests."""
import importlib.util
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import zipfile

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix3'


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def checks(d):
    package = json.loads((OUT / 'coverlet-package.json').read_text())
    assert d.sha(Path(package['path'])) == package['sha256']
    payload = []
    with zipfile.ZipFile(package['path']) as archive:
        for member in archive.namelist():
            if Path(member).name not in ('coverlet.console.dll', 'coverlet.core.dll'):
                continue
            expected = hashlib.sha256(archive.read(member)).hexdigest()
            installed = Path(package['path']).parent / member
            assert d.sha(installed) == expected
            payload.append(dict(member=member, installed=str(installed), sha256=expected))
    assert {Path(row['member']).name for row in payload} == {'coverlet.console.dll', 'coverlet.core.dll'}
    d.save('coverlet-payload-identity.json', dict(packageSha256=package['sha256'], files=payload))
    settings = json.loads((OUT / 'compiler-settings.stdout').read_text())['Properties']
    compiler = Path('C:/Program Files/dotnet/sdk') / settings['NETCoreSdkVersion'] / 'Roslyn/bincore/csc.dll'
    d.run('compiler-version', ['dotnet', compiler, '-version'])
    d.save('compiler-binary.json', dict(path=str(compiler), sha256=d.sha(compiler), settings=settings))
    load('coverlet_report', OUT / 'report.py').execute(d)
    runtime = d.runtime_identity()
    assert runtime == json.loads((OUT / 'runtime-before.json').read_text())
    d.save('finalize-runtime.json', runtime)
    d.run('historical-evidence-unchanged', ['git', 'diff', '--exit-code',
        'f0cfa5064eeb544cf2a7438ebcfeb51ec90c6c35', '--',
        'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1',
        'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2'], ROOT)
    status = d.run('owned-paths', ['git', 'status', '--porcelain=v1',
        '--untracked-files=all'], ROOT).stdout.decode().splitlines()
    assert status and all(row[3:].startswith(
        'dydo/project/pitfalls/dyd-415-statechart-evidence/fix3/') for row in status)
    d.run('docs', ['dydo.exe', 'check'], ROOT)
    d.run('whitespace', ['git', 'diff', '--check'], ROOT)


def main():
    assert sys.argv[1:] == ['child'], 'Run through the admitted driver'
    d = load('coverlet_driver', OUT / 'driver.py')
    os.environ.update(LOCALAPPDATA='C:\\Users\\User\\AppData\\Local', TEMP=str(d.TEMP), TMP=str(d.TEMP))
    command = subprocess.list2cmdline([sys.executable, __file__])
    owner = json.loads((d.TEMP / 'dydo-fullsuite.lock').read_text())
    assert owner['ticket'] == os.environ['LC_SUITE_SLOT_TICKET']
    with d.suite_slot.book(command, worktree=ROOT) as booking:
        assert booking.inherited and not booking.owned
        d.save('finalize-inherited.json', dict(parent=owner, childPid=os.getpid(),
            inherited=True, heavyAndDydoHeldByParent=True, sha256=d.sha(Path(__file__)),
            reportSha256=d.sha(OUT / 'report.py')))
        checks(d)


if __name__ == '__main__':
    main()

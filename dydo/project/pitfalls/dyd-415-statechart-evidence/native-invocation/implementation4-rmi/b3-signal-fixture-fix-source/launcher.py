"""Normal-TEMP hidden file launcher, matching the commissioned external-driver lifecycle."""
import argparse
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
DRIVER = TEMP / 'dyd415-rmi-b3-signal-fixture-fix-driver-20261031.py'
def validate_host():
    if os.name != 'nt' or sys.platform != 'win32':
        raise RuntimeError('B3 requires the commissioned Windows host')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve():
        raise RuntimeError('Redirected LOCALAPPDATA; refuse before driver startup')
    if Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected TEMP; refuse before driver startup')


def validate_release(packet, signal):
    declaration = json.loads((packet / 'source-pin.json').read_bytes())
    release = json.loads((packet / 'execution-release.json').read_bytes())
    if release['state'] != 'RELEASED_B3_SIGNAL_FIXTURE_FIX' or signal not in release['signals'] or len(set(release['signals'])) != len(release['signals']) or not set(release['signals']).issubset({'SIGINT', 'SIGTERM'}) or release['sourcePinSha256'] != hashlib.sha256((packet / 'source-pin.json').read_bytes()).hexdigest():
        raise RuntimeError('Exact captain release after source inspection required')
    if not release.get('captainInspectionRecord') or not release.get('dockerIdentity'):
        raise RuntimeError('Captain inspection and exact Docker identity required')
    return declaration


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--signal', choices=['SIGINT', 'SIGTERM'], required=True)
    signal = parser.parse_args().signal
    preflight = dict(osName=os.name, platform=sys.platform, localAppData=os.environ.get('LOCALAPPDATA'),
        temp=os.environ.get('TEMP'), inheritedTicket=os.environ.get('LC_SUITE_SLOT_TICKET'),
        launcherSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        driverSha256=hashlib.sha256(DRIVER.read_bytes()).hexdigest())
    print('Launcher preflight:', json.dumps(preflight), flush=True)
    validate_host()
    output = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f/dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi') / ('b3-signal-fixture-fix-20261031a-' + signal)
    packet = output.parent / 'b3-signal-fixture-fix-source'
    declaration = validate_release(packet, signal)
    for path in json.loads((packet / 'closure.json').read_bytes())['namespacePackageAbsences']:
        if Path(path).exists():
            raise RuntimeError('LC namespace topology changed before import')
    sys.path.insert(0, str(LC))
    for name, expected in {
        'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
        'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
        'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'
    }.items():
        assert hashlib.sha256((LC / name).read_bytes()).hexdigest() == expected, 'LC input changed'
    from tools.testing.runner import suite_slot
    from tools.testing.runner import machine_lock as lock_module
    if Path(suite_slot.__file__).resolve() != (LC / 'tools/testing/runner/suite_slot.py').resolve() or Path(lock_module.__file__).resolve() != (LC / 'tools/testing/runner/machine_lock.py').resolve():
        raise RuntimeError('Actual LC import origin differs')
    live = suite_slot.live()
    assert live.directory.resolve() == Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve(), 'Wrong actual live namespace'
    assert live.prefix == 'LC-Suite-Slot' and live.variable == 'LC_SUITE_SLOT_TICKET', 'Wrong LC live API identity'
    preflight.update(namespace=str(live.directory), prefix=live.prefix, variable=live.variable)
    assert not output.exists(), 'B3 evidence output already exists'
    assert declaration['driverSha256'] == preflight['driverSha256'] and declaration['launcherSha256'] == preflight['launcherSha256']
    output.mkdir()
    directory = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b3-signal-fixture-fix-launch-', dir=TEMP))
    with (directory / 'preflight.json').open('x', encoding='utf-8') as stream:
        json.dump(preflight, stream, indent=2)
    with (output / 'launcher.preflight.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(preflight, logDirectory=str(directory)), stream, indent=2)
    print('Launcher logs:', directory, flush=True)
    with (directory / 'stdout').open('xb') as stdout, (directory / 'stderr').open('xb') as stderr:
        result = subprocess.run([sys.executable, '-B', str(DRIVER), '--signal', signal], stdout=stdout, stderr=stderr,
            creationflags=subprocess.CREATE_NO_WINDOW)
    for name in ['stdout', 'stderr']:
        with (output / ('launcher.' + name)).open('xb') as stream: stream.write((directory / name).read_bytes())
    with (output / 'launcher.result.json').open('x', encoding='utf-8') as stream: json.dump(dict(exit=result.returncode, logDirectory=str(directory)), stream, indent=2)
    print('Driver exit:', result.returncode)
    print((directory / 'stdout').read_text(errors='replace'), end='')
    print((directory / 'stderr').read_text(errors='replace'), end='', file=sys.stderr)
    sys.exit(result.returncode)


if __name__ == '__main__':
    main()

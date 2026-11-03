"""Exact two-choice normal-host launcher; no release authored by the writer."""
import argparse
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
PACKET = BASE / 'b3-janitor-host-envelope2-source'
DRIVER = PACKET / 'driver.py'
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
PYTHON = Path('C:/Users/User/AppData/Local/Programs/Python/Python313/python.exe')
LC_PINS = {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'}


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(output, name, value):
    with (output / name).open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def validate_host():
    if os.name != 'nt' or sys.platform != 'win32' or Path(sys.executable).resolve() != PYTHON.resolve():
        raise RuntimeError('Exact commissioned Windows Python host required')
    if Path(os.environ.get('LOCALAPPDATA', '')).resolve() != Path('C:/Users/User/AppData/Local').resolve() or Path(tempfile.gettempdir()).resolve() != TEMP.resolve():
        raise RuntimeError('Redirected host namespace; refuse before startup')


def validate_release(selection):
    pin = json.loads((PACKET / 'source-pin.json').read_bytes())
    release = json.loads((PACKET / 'execution-release.json').read_bytes())
    if release.get('state') != 'RELEASED_B3_JANITOR_HOST2' or release.get('selection') != selection or release.get('once') is not True or not release.get('captainInspectionRecord'):
        raise RuntimeError('One exact inspected captain selector release required')
    if release.get('sourcePinSha256') != sha(PACKET / 'source-pin.json'):
        raise RuntimeError('Exact source pin required')
    if selection == 'TYPECHECK' and not release.get('helper12GreenReceiptSha256'):
        raise RuntimeError('Applicable complete helper12 GREEN receipt binding required')
    if sha(DRIVER) != pin['driverSha256'] or sha(Path(__file__)) != pin['launcherSha256']:
        raise RuntimeError('Counterpart identity differs')
    for row in pin['externalInputs']:
        path = Path(row['path'])
        if sha(path) != row['sha256'] or path.stat().st_size != row['bytes']:
            raise RuntimeError('External identity differs before startup')
    return pin


def main():
    parser = argparse.ArgumentParser()
    selected = parser.add_mutually_exclusive_group(required=True)
    selected.add_argument('--helpers', action='store_true')
    selected.add_argument('--typecheck', action='store_true')
    args = parser.parse_args()
    selection = 'HELPERS' if args.helpers else 'TYPECHECK'
    validate_host()
    validate_release(selection)
    for path in json.loads((PACKET / 'closure.json').read_bytes())['namespacePackageAbsences']:
        if Path(path).exists():
            raise RuntimeError('LC namespace topology changed')
    for name, expected in LC_PINS.items():
        if sha(LC / name) != expected:
            raise RuntimeError('LC identity differs before import')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner import machine_lock as lock_module
    if Path(suite_slot.__file__).resolve() != (LC / 'tools/testing/runner/suite_slot.py').resolve() or Path(lock_module.__file__).resolve() != (LC / 'tools/testing/runner/machine_lock.py').resolve():
        raise RuntimeError('Actual LC import origin differs')
    live = suite_slot.live()
    if live.directory.resolve() != Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve() or live.prefix != 'LC-Suite-Slot' or live.variable != 'LC_SUITE_SLOT_TICKET':
        raise RuntimeError('Actual default live FIFO namespace required')
    output = BASE / ('b3-janitor-host2-20261031a-' + selection)
    if output.exists():
        raise RuntimeError('Once-only output already exists')
    output.mkdir()
    preflight = dict(namespace=str(live.directory), python=str(PYTHON),
        launcherSha256=sha(Path(__file__)), driverSha256=sha(DRIVER), selection=selection)
    save(output, 'launcher.preflight.json', preflight)
    # Primary driver logs are directly retained; no TEMP log relocation can fail.
    with (output / 'driver.stdout').open('xb') as stdout, (output / 'driver.stderr').open('xb') as stderr:
        child = subprocess.Popen([str(PYTHON), '-I', '-B', str(DRIVER), '--' + selection.lower()],
            cwd=ROOT, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
        # Native child has the bounded180+5s controller. Do not outer-kill its owner.
        result = child.wait()
    save(output, 'launcher.result.json', dict(exit=result, directDriverReaped=True,
        nativeDeadlineSeconds=180, directNativeReapSeconds=5, outerOwnerKillForbidden=True))
    print('Driver exit:', result)
    print('Evidence:', output)
    return result


if __name__ == '__main__':
    sys.exit(main())

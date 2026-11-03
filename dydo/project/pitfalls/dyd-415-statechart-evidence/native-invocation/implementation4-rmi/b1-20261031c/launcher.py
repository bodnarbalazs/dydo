"""Normal-TEMP hidden file launcher, matching the commissioned external-driver lifecycle."""
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
DRIVER = TEMP / 'dyd415-rmi-b1c-driver-20261031.py'
preflight = dict(osName=os.name, platform=sys.platform, localAppData=os.environ.get('LOCALAPPDATA'),
    temp=os.environ.get('TEMP'), inheritedTicket=os.environ.get('LC_SUITE_SLOT_TICKET'),
    launcherSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
    driverSha256=hashlib.sha256(DRIVER.read_bytes()).hexdigest())
print('Launcher preflight:', json.dumps(preflight), flush=True)
assert os.name == 'nt' and sys.platform == 'win32', 'B1c requires the commissioned Windows host'
assert Path(os.environ.get('LOCALAPPDATA', '')).resolve() == Path('C:/Users/User/AppData/Local').resolve(), 'Redirected LOCALAPPDATA; refuse before driver startup'
assert Path(tempfile.gettempdir()).resolve() == TEMP.resolve(), 'Redirected TEMP; refuse before driver startup'
sys.path.insert(0, str(LC))
for name, expected in {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'
}.items():
    assert hashlib.sha256((LC / name).read_bytes()).hexdigest() == expected, 'LC input changed'
from tools.testing.runner import suite_slot
live = suite_slot.live()
assert live.directory.resolve() == Path('C:/Users/User/AppData/Local/lc-test/suite-slot').resolve(), 'Wrong actual live namespace'
assert live.prefix == 'LC-Suite-Slot' and live.variable == 'LC_SUITE_SLOT_TICKET', 'Wrong LC live API identity'
preflight.update(namespace=str(live.directory), prefix=live.prefix, variable=live.variable)
output = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f/dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b1-20261031c')
declaration = json.loads((output / 'pre-execution-pin.json').read_bytes())
assert declaration['driverSha256'] == preflight['driverSha256'] and declaration['launcherSha256'] == preflight['launcherSha256']
directory = Path(tempfile.mkdtemp(prefix='dyd415-rmi-b1c-launch-', dir=TEMP))
with (directory / 'preflight.json').open('x', encoding='utf-8') as stream:
    json.dump(preflight, stream, indent=2)
with (output / 'launcher.preflight.json').open('x', encoding='utf-8') as stream:
    json.dump(dict(preflight, logDirectory=str(directory)), stream, indent=2)
print('Launcher logs:', directory, flush=True)
with (directory / 'stdout').open('xb') as stdout, (directory / 'stderr').open('xb') as stderr:
    result = subprocess.run([sys.executable, '-B', str(DRIVER)], stdout=stdout, stderr=stderr,
        creationflags=subprocess.CREATE_NO_WINDOW)
for name in ['stdout', 'stderr']:
    with (output / ('launcher.' + name)).open('xb') as stream: stream.write((directory / name).read_bytes())
with (output / 'launcher.result.json').open('x', encoding='utf-8') as stream: json.dump(dict(exit=result.returncode, logDirectory=str(directory)), stream, indent=2)
print('Driver exit:', result.returncode)
print((directory / 'stdout').read_text(errors='replace'), end='')
print((directory / 'stderr').read_text(errors='replace'), end='', file=sys.stderr)
sys.exit(result.returncode)

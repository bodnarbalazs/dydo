"""Copy the commissioned driver to normal TEMP, launch it hidden, retain its actual exit."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
TEMP = Path('C:/Users/User/AppData/Local/Temp')
assert len(sys.argv) == 3
assert sys.argv[1] in ['preflight', 'g1', 'g2', 'light', 'checks', 'close', 'seal']
driver = Path(tempfile.mkdtemp(prefix='dyd415-native-driver-', dir=TEMP))
for name in ['gate-driver.py', 'milestone_checks.py']:
    shutil.copy2(HERE / name, driver / name)
with (driver / 'stdout').open('xb') as stdout, (driver / 'stderr').open('xb') as stderr:
    result = subprocess.run([sys.executable, '-B', str(driver / 'gate-driver.py'), *sys.argv[1:]],
        stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
print('Driver:', driver, 'exit:', result.returncode, flush=True)
print((driver / 'stdout').read_text(errors='replace'), end='')
print((driver / 'stderr').read_text(errors='replace'), end='', file=sys.stderr)
sys.exit(result.returncode)

"""Launch the normal-TEMP supervisor hidden and propagate its actual exit."""
from pathlib import Path
import subprocess
import sys

TEMP = Path('C:/Users/User/AppData/Local/Temp')
assert sys.argv[1:] in [['red'], ['probes'], ['final']]
phase = sys.argv[1]
with (TEMP / ('dyd415-integration3-' + phase + '.stdout')).open('xb') as stdout, (
        TEMP / ('dyd415-integration3-' + phase + '.stderr')).open('xb') as stderr:
    result = subprocess.run([sys.executable, str(TEMP / 'dyd415-integration3-checks.py'), phase],
        stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
print('Hidden supervisor exit:', result.returncode)
sys.exit(result.returncode)

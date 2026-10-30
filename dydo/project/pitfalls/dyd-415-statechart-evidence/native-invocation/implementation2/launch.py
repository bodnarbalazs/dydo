"""Launch only this stage's normal-TEMP hidden file driver; preserve its raw streams."""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
TEMP = Path('C:/Users/User/AppData/Local/Temp')


def main(arguments, here=HERE, temp=TEMP):
    assert len(arguments) == 2 and arguments[0] in ['product', 'static', 'attribution', 'catalogue', 'request', 'fidelity', 'remove', 'focused', 'archives', 'archives-red', 'public-red', 'public-flow', 'public-corrected', 'public-probe-corrected', 'public-resume', 'public-light', 'readiness-diagnostic']
    directory = Path(tempfile.mkdtemp(prefix='dyd415-implementation2-driver-', dir=temp))
    shutil.copy2(here / 'driver.py', directory / 'driver.py')
    with (directory / 'stdout').open('xb') as stdout, (directory / 'stderr').open('xb') as stderr:
        result = subprocess.run([sys.executable, '-B', str(directory / 'driver.py'), *arguments],
            stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
    print('Driver:', directory, 'exit:', result.returncode)
    print((directory / 'stdout').read_text(errors='replace'), end='')
    print((directory / 'stderr').read_text(errors='replace'), end='', file=sys.stderr)
    return result.returncode


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))

"""One native unittest file; real child deadline/output bounds, no runtime/dependency work."""
import hashlib
import json
import os
from pathlib import Path
import re
import resource
import signal
import subprocess
import sys
import time

OUTPUT = Path('/output')
REPO = Path('/input/repo')
LIMIT = 16 * 1024 * 1024
ARGV = [sys.executable, '-B', '-m', 'unittest', 'discover', '-v', '-f',
        '-s', str(REPO / 'DynaDocs.Tests/coverage/tests'), '-p', 'test_statechart_rmi_bc.py']


def save(name, value):
    with (OUTPUT / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def file_limits():
    resource.setrlimit(resource.RLIMIT_FSIZE, (LIMIT, LIMIT))


def verify_inputs():
    manifest = json.loads(Path('/input/input-manifest.json').read_bytes())
    expected = {item['path'] for item in manifest['files']}
    actual = {path.relative_to(REPO).as_posix() for path in REPO.rglob('*') if path.is_file()}
    if len(expected) != 4 or actual != expected or len(manifest['names']) != 22 or len(set(manifest['names'])) != 22:
        raise ValueError('Exact four input files and twenty-two distinct selected test names required')
    for item in manifest['files']:
        path = REPO / item['path']
        if path.is_symlink() or path.stat().st_size != item['bytes'] or hashlib.sha256(path.read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Input identity differs before unittest: ' + item['path'])
    return manifest


def run_child():
    env = dict(os.environ)
    for name in ['CLASSPATH', 'PYTHONPATH', 'PYTHONHOME', 'JAVA', 'STATECHART_JAVA', 'TLA2TOOLS', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'NODE_OPTIONS']:
        env.pop(name, None)
    env.update(TMPDIR='/work/tmp', PYTHONDONTWRITEBYTECODE='1', LC_ALL='C.UTF-8', LANG='C.UTF-8')
    save('unittest.argv.json', dict(argv=ARGV, cwd='/input', environment=env, seconds=180,
        stdoutMaximumBytes=LIMIT, stderrMaximumBytes=LIMIT, sizeEnforcement='Child hard RLIMIT_FSIZE per regular output file',
        timeEnforcement='Popen.wait(timeout=180) in contained worker; owned session kill/drain on timeout'))
    started = time.monotonic()
    with (OUTPUT / 'unittest.stdout').open('xb') as stdout, (OUTPUT / 'unittest.stderr').open('xb') as stderr:
        child = subprocess.Popen(ARGV, cwd='/input', env=env, stdout=stdout, stderr=stderr,
            start_new_session=True, preexec_fn=file_limits)
        try:
            try:
                stat = Path('/proc', str(child.pid), 'stat').read_text()
            except FileNotFoundError:
                stat = None
            save('unittest.owner.json', dict(pid=child.pid, parentPid=os.getpid(), newSession=True, stat=stat))
            code = child.wait(timeout=180)
        finally:
            drained = False
            try:
                if child.poll() is None:
                    os.killpg(child.pid, signal.SIGKILL)
                child.wait(timeout=5)
                drained = True
            finally:
                save('unittest.result.json', dict(exit=child.returncode, drained=drained, seconds=time.monotonic()-started))
    return code


def interpret(text, code, names):
    observed = re.findall(r'^(test_\S+) \(test_statechart_rmi_bc\.RmiBcContracts\.\S+\) \.\.\. ok$', text, re.M)
    if code != 0 or 'Ran 22 tests' not in text or '\nOK\n' not in text or 'skipped=' in text:
        raise ValueError('Twenty-two-contract native unittest result/summary differs')
    if len(observed) != 22 or len(set(observed)) != 22 or sorted(observed) != sorted(names):
        raise ValueError('Missing/duplicate/unselected native test execution')
    if any(' ... ' in line and not line.endswith(' ... ok') for line in text.splitlines()):
        raise ValueError('Skipped/failed/unexpected native test status')
    return observed


def main():
    if sys.platform != 'linux':
        raise RuntimeError('Contained Linux worker only')
    manifest = verify_inputs()
    code = run_child()
    text = (OUTPUT / 'unittest.stderr').read_text(encoding='utf-8')
    names = interpret(text, code, manifest['names'])
    if (OUTPUT / 'unittest.stdout').stat().st_size or any((OUTPUT / name).stat().st_size > LIMIT for name in ['unittest.stdout', 'unittest.stderr']):
        raise ValueError('Unexpected stdout or exceeded hard child output size')
    verify_inputs()
    remaining = [int(path.name) for path in Path('/proc').iterdir() if path.name.isdigit() and int(path.name) != os.getpid()]
    save('namespace-after.json', remaining)
    if remaining:
        raise RuntimeError('Owned namespace still has another process; independent container drain required')
    save('completed.json', dict(cases=22, passed=22, skipped=0, names=names, inputFiles=4,
        realNetwork=False, java=False, node=False, extracted=False, installed=False))


if __name__ == '__main__':
    main()

"""One corrected CLI replay and twelve finite close-order regressions; never real Java."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import resource
import re
import shutil
import signal
import subprocess
import sys
import tarfile
import time

INPUT = Path('/input')
OUTPUT = Path('/output')
REPO = Path('/toolchain/repo')
SCRIPTS = REPO / 'skills/engineering/statechart/scripts'
NODE = Path('/toolchain/node-v22.23.2-linux-x64/bin/node')
CASE = SCRIPTS / 'test/corpus/Counter'
JANITOR = 'while read -r line; do :; done; kill -s KILL -- "-$1"; rm -rf -- "$2" "$3"'
LIMIT = 16 * 1024 * 1024


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(name, value):
    with (OUTPUT / name).open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def verify_files():
    manifest = json.loads((INPUT / 'input-manifest.json').read_bytes())
    expected = {row['path'] for row in manifest['files']}
    actual = {path.relative_to(REPO).as_posix() for path in REPO.rglob('*') if path.is_file()}
    if actual != expected:
        raise ValueError('Exact source closure required')
    for row in manifest['files']:
        target = REPO / row['path']
        if target.is_symlink() or sha(target) != row['sha256'] or target.stat().st_mode & 0o777 != row['mode']:
            raise ValueError('Source bytes/mode differ: ' + row['path'])
    if sha(NODE) != manifest['nodeBinarySha256'] or NODE.stat().st_mode & 0o777 != 0o755:
        raise ValueError('Pinned Node binary/mode differs')
    if sha(INPUT / 'tla2tools.jar') != manifest['checkerSha256']:
        raise ValueError('Checker DATA differs')
    for root in [Path('/usr'), Path('/opt'), Path('/toolchain')]:
        if any(path.is_file() and path.name in ['java', 'javac', 'java.exe', 'javac.exe'] for path in root.rglob('*')):
            raise ValueError('Java/JDK executable unexpectedly available to replay')
    if shutil.which('java') or shutil.which('javac'):
        raise ValueError('Java/JDK on container PATH')
    return manifest


def extract():
    """Future contained preparation only: admitted Node executable, no JDK/npm/install."""
    manifest = json.loads((INPUT / 'input-manifest.json').read_bytes())
    archive = INPUT / 'node.tar.gz'
    if sha(archive) != manifest['nodeArchiveSha256']:
        raise ValueError('Node archive differs before opening')
    members = json.loads((INPUT / 'node-members.json').read_bytes())['members']
    with tarfile.open(archive) as bundle:
        actual = [dict(name=m.name, bytes=m.size, mode=m.mode, linkname=m.linkname,
            kind='file' if m.isfile() else 'directory' if m.isdir() else 'symlink' if m.issym() else 'unsupported') for m in bundle.getmembers()]
        expected = [{key: row[key] for key in ['name', 'bytes', 'mode', 'linkname', 'kind']} for row in members]
        if actual != expected:
            raise ValueError('Node archive metadata differs')
        for row in actual:
            path = PurePosixPath(row['name'])
            if path.is_absolute() or '..' in path.parts or row['kind'] == 'unsupported':
                raise ValueError('Unsafe archive member')
        member = bundle.getmember('node-v22.23.2-linux-x64/bin/node')
        if not member.isfile() or member.mode != 0o755:
            raise ValueError('Node binary is not the pinned plain executable')
        NODE.parent.mkdir(parents=True)
        with bundle.extractfile(member) as source, NODE.open('xb') as destination:
            shutil.copyfileobj(source, destination)
        NODE.chmod(0o755)
    for row in manifest['files']:
        source, destination = INPUT / 'repo' / row['path'], REPO / row['path']
        if source.is_symlink() or sha(source) != row['sha256']:
            raise ValueError('Source differs before contained copy')
        destination.parent.mkdir(parents=True, exist_ok=True)
        with source.open('rb') as reader, destination.open('xb') as writer:
            shutil.copyfileobj(reader, writer)
        destination.chmod(row['mode'])
    verify_files()
    save('extracted.json', dict(nodeSha256=sha(NODE), inputFiles=len(manifest['files']),
        java=False, jdk=False, npm=False, install=False, nodeExecuted=False))


def census():
    rows = []
    for folder in sorted(Path('/proc').iterdir(), key=lambda path: path.name):
        if not folder.name.isdigit():
            continue
        try:
            raw = (folder / 'stat').read_text()
            fields = raw.rsplit(') ', 1)[1].split()
            command = (folder / 'cmdline').read_bytes()
            confirmed = (folder / 'stat').read_text().rsplit(') ', 1)[1].split()
            if fields[19] != confirmed[19]:
                continue
            rows.append(dict(pid=int(folder.name), starttime=int(fields[19]), parentPid=int(fields[1]),
                processGroup=int(fields[2]), session=int(fields[3]), state=fields[0], stat=raw,
                cmdlineHex=command.hex(), argv=[arg.decode('utf-8', 'replace') for arg in command.split(b'\0') if arg]))
        except (FileNotFoundError, ProcessLookupError):
            continue
    return rows


def environment():
    # An allowlist prevents ambient recording, runtime/tool selection, Node/Python hooks and JVM flags.
    return dict(PATH=str(NODE.parent) + ':/usr/local/bin:/usr/bin:/bin', HOME='/work/home',
        TMPDIR='/work/tmp', LC_ALL='C.UTF-8', LANG='C.UTF-8',
        JAVA=str(SCRIPTS / 'test/java.ts'), TLA2TOOLS='/input/tla2tools.jar',
        STATECHART_REPLAY=str(CASE), STATECHART_JAVA='/no-runtime/java-forbidden',
        STATECHART_LOG='/output/replayed-configs.log', NODE_OPTIONS='--disable-warning=ExperimentalWarning')


def remaining_processes(name):
    rows = census()
    save(name, rows)
    if any(row['pid'] != os.getpid() for row in rows):
        raise RuntimeError('Owned namespace still has another process; preserve raw evidence for independent container drain')


def replay_counter():
    """The original red case's real CLI route, without the test preload."""
    argv = [str(NODE), '--disable-warning=ExperimentalWarning', str(SCRIPTS / 'statechart.ts'),
        str(CASE / 'Counter.tla'), '--out', '/work/result']
    save('counter.argv.json', dict(argv=argv, cwd=str(REPO), environment=environment(),
        timeoutSeconds=120, sampleCap=60502, sampleIntervalSeconds=0.002,
        postExitObservationSeconds=1, fileLimitBytes=LIMIT, directChildDrainSeconds=5))
    started = time.monotonic_ns()
    observed_exit = None
    with (OUTPUT / 'counter.stdout').open('xb') as stdout, (OUTPUT / 'counter.stderr').open('xb') as stderr, \
            (OUTPUT / 'processes.jsonl').open('x', encoding='utf-8') as trace:
        child = subprocess.Popen(argv, cwd=REPO, env=environment(), stdout=stdout, stderr=stderr, start_new_session=True)
        save('counter.owned.json', dict(pid=child.pid, pgid=os.getpgid(child.pid),
            stat=Path('/proc', str(child.pid), 'stat').read_text(), startNewSession=True))
        try:
            for sample in range(60502):
                now = time.monotonic_ns()
                trace.write(json.dumps(dict(monotonicNs=now, rows=census())) + '\n')
                trace.flush()
                if any(path.stat().st_size > LIMIT for path in OUTPUT.rglob('*') if path.is_file()):
                    raise RuntimeError('Replay evidence file bound exceeded; no retry')
                if observed_exit is None and child.poll() is not None:
                    observed_exit = dict(exit=child.returncode, observedReapedMonotonicNs=time.monotonic_ns())
                if observed_exit and now - observed_exit['observedReapedMonotonicNs'] >= 1_000_000_000:
                    break
                if now - started >= 120_000_000_000:
                    raise TimeoutError('Counter replay deadline exceeded; no retry')
                time.sleep(0.002)
            else:
                raise TimeoutError('Replay passive sample bound exhausted')
        finally:
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGKILL)
            child.wait(timeout=5)
            save('counter.result.json', dict(exit=child.returncode, samples=sample + 1,
                exitObservation=observed_exit, directChildReaped=True, adoptedChildrenReaped=False))
    report, chart = CASE / 'report.md', CASE / 'chart.mmd'
    if child.returncode != 0 or (OUTPUT / 'counter.stderr').stat().st_size:
        raise ValueError('Corrected real CLI Counter replay failed')
    if Path('/work/result/report.md').read_bytes() != report.read_bytes() or Path('/work/result/chart.mmd').read_bytes() != chart.read_bytes():
        raise ValueError('Counter chart/report drift')
    if (OUTPUT / 'counter.stdout').read_bytes() != report.read_bytes() + b'\nWrote /work/result\n':
        raise ValueError('Counter stdout drift')
    if list(Path('/work').glob('.result.statechart-*')) or list(Path('/work/tmp').iterdir()):
        raise ValueError('CLI-owned staging/scratch remained')
    save('counter.semantic.json', dict(actualCli=True, replayOnly=True, exit=0,
        chartSha256=sha(chart), reportSha256=sha(report), stageAndScratchAbsent=True,
        preload=False, java=False, sourceInstrumentation=False))
    # This is the original strict guard: zombies, adopted children and live children all fail.
    remaining_processes('counter.processes-after.json')


def regressions(manifest):
    names = manifest['names']
    if len(names) != 12 or len(set(names)) != 12:
        raise ValueError('Exactly twelve unique finite names required')
    pattern = '^(?:' + '|'.join(re.escape(name) for name in names) + ')$'
    argv = [str(NODE), '--disable-warning=ExperimentalWarning', '--test', '--test-reporter=tap',
        '--test-concurrency=1', '--test-name-pattern=' + pattern, 'test/cli.test.ts']
    env = environment()
    env['STATECHART_JANITOR_PROOF'] = '/output/janitor-events'
    Path(env['STATECHART_JANITOR_PROOF']).mkdir()
    save('regressions.argv.json', dict(argv=argv, cwd=str(SCRIPTS), environment=env,
        names=names, selected=12, timeoutSeconds=120, fileLimitBytes=LIMIT, directChildDrainSeconds=5))
    out, err = OUTPUT / 'regressions.stdout', OUTPUT / 'regressions.stderr'
    with out.open('xb') as stdout, err.open('xb') as stderr:
        child = subprocess.Popen(argv, cwd=SCRIPTS, env=env, stdout=stdout, stderr=stderr, start_new_session=True)
        save('regressions.owned.json', dict(pid=child.pid, pgid=os.getpgid(child.pid),
            stat=Path('/proc', str(child.pid), 'stat').read_text(), startNewSession=True))
        try:
            child.wait(timeout=120)
        finally:
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGKILL)
            child.wait(timeout=5)
            save('regressions.result.json', dict(exit=child.returncode, directChildReaped=True))
    if child.returncode != 0 or err.stat().st_size:
        raise ValueError('Focused janitor regressions failed; no correction/retry')
    selected = []
    for line in out.read_text(encoding='utf-8').splitlines():
        match = re.match(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', line)
        if not match:
            continue
        status, name, directive = match.groups()
        if name in names:
            if status != 'ok' or directive:
                raise ValueError('Selected regression failed/skipped: ' + name)
            selected.append(name)
        elif status != 'ok' or not directive or not directive.startswith('SKIP test name does not match pattern'):
            raise ValueError('Unselected execution/non-ok test: ' + name)
    if sorted(selected) != sorted(names):
        raise ValueError('Exact twelve unique native ok names not observed')
    text = out.read_text(encoding='utf-8')
    if not re.search(r'^# pass 12$', text, re.M) or not re.search(r'^# fail 0$', text, re.M):
        raise ValueError('Native pass/fail summary differs')
    events = list(Path(env['STATECHART_JANITOR_PROOF']).glob('*/events.jsonl'))
    if len(events) != 12:
        raise ValueError('Missing/extra actual CLI lifecycle observer logs')
    # Native tests assert close-before-exit/report and ownership failures from these raw logs.
    save('regressions.discovery.json', dict(selected=12, nativeOkNames=selected,
        selectedSkipped=0, eventLogs=12, unselectedPatternSkipsOnly=True,
        testPreload=True, observerChangesTiming=True, realChildren=True, java=False))
    remaining_processes('regressions.processes-after.json')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux' or os.getpid() != 1:
        raise RuntimeError('Owned network-none Linux Python PID1 only; no init/subreaper')
    if args.extract:
        extract()
        return
    resource.setrlimit(resource.RLIMIT_FSIZE, (LIMIT, LIMIT))
    manifest = verify_files()
    Path('/work/home').mkdir()
    remaining_processes('processes-before.json')
    replay_counter()
    regressions(manifest)
    verify_files()
    if any(path.stat().st_size > LIMIT for path in OUTPUT.rglob('*') if path.is_file()):
        raise RuntimeError('Evidence file bound exceeded')
    remaining_processes('processes-after.json')
    save('completed.json', dict(actualCounterReplay=True, selectedRegressions=12, selectedSkipped=0,
        strictRemainingProcesses=0, abruptParentCertified=False, java=False,
        windows=False, newDriverDirectAssurance='OWED'))


if __name__ == '__main__':
    main()



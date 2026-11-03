"""One unchanged actual CLI replay, passive ancestry observation; never real Java."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import resource
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


def observe():
    manifest = verify_files()
    Path('/work/home').mkdir()
    env = environment()
    argv = [str(NODE), '--disable-warning=ExperimentalWarning', str(SCRIPTS / 'statechart.ts'),
        str(CASE / 'Counter.tla'), '--out', '/work/result']
    save('cli.argv.json', dict(argv=argv, cwd=str(SCRIPTS), environment=env,
        childSeconds=120, passivePostExitSeconds=1, sampleIntervalSeconds=0.002,
        sampleLimit=60502, outputHardBytes=LIMIT, ownedDrainSeconds=5))
    resource.setrlimit(resource.RLIMIT_FSIZE, (LIMIT, LIMIT))
    save('processes-before.json', census())
    started = time.monotonic_ns()
    samples, cli_exit = 0, None
    with (OUTPUT / 'processes.jsonl').open('x', encoding='utf-8') as trace, \
            (OUTPUT / 'cli.stdout').open('xb') as stdout, (OUTPUT / 'cli.stderr').open('xb') as stderr:
        cli = subprocess.Popen(argv, cwd=SCRIPTS, env=env, stdout=stdout, stderr=stderr, start_new_session=True)
        save('cli.owner.json', dict(pid=cli.pid, processGroup=os.getpgid(cli.pid),
            session=os.getsid(cli.pid), parentPid=os.getpid(), startedMonotonicNs=started))
        seen, janitors = {}, {}
        try:
            for _ in range(60502):
                now = time.monotonic_ns()
                rows = census()
                # Raw samples retain numeric identity and raw stat/cmdline, including PID1 and zombies.
                trace.write(json.dumps(dict(monotonicNs=now, rows=rows)) + '\n')
                trace.flush()
                samples += 1
                for row in rows:
                    key = (row['pid'], row['starttime'])
                    seen[key] = row
                    args = row['argv']
                    if row['parentPid'] == cli.pid and len(args) == 7 and args[:4] == ['/bin/sh', '-c', JANITOR, 'sh']:
                        if not args[4].isdigit() or not args[5].startswith('/work/.result.statechart-') or not args[6].startswith('/work/tmp/statechart-'):
                            raise ValueError('Unexpected janitor worker/scratch argv')
                        janitors.setdefault(key, dict(firstSeenMonotonicNs=now, alive=row, workerPid=int(args[4]),
                            lastSeen=row, lastSeenMonotonicNs=now))
                    if key in janitors:
                        janitors[key].update(lastSeen=row, lastSeenMonotonicNs=now)
                        if row['state'] == 'Z':
                            janitors[key].setdefault('firstZombieObservedMonotonicNs', now)
                        if row['parentPid'] == 1:
                            janitors[key].setdefault('firstAdoptionObservedMonotonicNs', now)
                # poll/wait reaps only our direct CLI child, never an adopted janitor.
                if cli_exit is None and cli.poll() is not None:
                    cli_exit = dict(exit=cli.returncode, observedReapedMonotonicNs=time.monotonic_ns())
                    save('cli.exit-observed.json', cli_exit)
                if cli_exit and now - cli_exit['observedReapedMonotonicNs'] >= 1_000_000_000:
                    break
                if now - started >= 120_000_000_000:
                    raise TimeoutError('One CLI replay deadline exceeded; no retry')
                time.sleep(0.002)
            else:
                raise TimeoutError('Passive sample bound exhausted')
        finally:
            if cli.poll() is None:
                os.killpg(cli.pid, signal.SIGKILL)
            cli.wait(timeout=5)
            save('cli.result.json', dict(exit=cli.returncode, samples=samples, cliExitObservation=cli_exit,
                directChildReaped=True, noWaitpidOnAdoptedChildren=True))
    remaining = census()
    save('processes-after.json', remaining)
    for value in janitors.values():
        key = (value['alive']['pid'], value['alive']['starttime'])
        value['terminalSnapshot'] = next((row for row in remaining if (row['pid'], row['starttime']) == key), None)
        value['workerWitnesses'] = [row for row in seen.values() if row['pid'] == value['workerPid'] and
            row['parentPid'] == cli.pid and str(SCRIPTS / 'worker.ts') in row['argv']]
    matched = [value for value in janitors.values() if value['terminalSnapshot'] and
        value['terminalSnapshot']['state'] == 'Z' and value['terminalSnapshot']['parentPid'] == 1]
    others = [row for row in remaining if row['pid'] != os.getpid()]
    save('attribution.json', dict(janitors=list(janitors.values()), matchedZombieCount=len(matched),
        observedProcessIdentities=len(seen), remaining=others,
        interpretation='JANITOR_PID_STARTTIME_MATCHED_ZOMBIE' if len(matched) == 1 else 'INCONCLUSIVE_NO_UNIQUE_JANITOR_ZOMBIE',
        limits=['Polling can miss short-lived spawns/transitions', 'Cross-file proc reads are not atomic',
            'CLI poll records observation/reap time, not exact process.exit or libuv close event',
            'PID1 adoption does not alone distinguish an original-parent reap race from init policy',
            'No source fix or root-cause proof from absence, and no automatic retry']))
    expected_report = (CASE / 'report.md').read_bytes()
    expected_chart = (CASE / 'chart.mmd').read_bytes()
    if cli.returncode != 0 or (OUTPUT / 'cli.stderr').stat().st_size:
        raise ValueError('Single accepted Counter replay failed')
    if Path('/work/result/report.md').read_bytes() != expected_report or Path('/work/result/chart.mmd').read_bytes() != expected_chart:
        raise ValueError('Replay chart/report bytes differ from retained corpus')
    expected_stdout = expected_report + b'\nWrote /work/result\n'
    if (OUTPUT / 'cli.stdout').read_bytes() != expected_stdout:
        raise ValueError('Actual CLI stdout differs from retained report and destination')
    if list(Path('/work').glob('.result.statechart-*')) or list(Path('/work/tmp').iterdir()):
        raise ValueError('CLI-owned staging/scratch remained')
    verify_files()
    save('semantic-result.json', dict(exit=0, chartSha256=sha(Path('/work/result/chart.mmd')),
        reportSha256=sha(Path('/work/result/report.md')), actualCli=True, replayOnly=True, java=False,
        stageAndScratchAbsent=True, noProductionInstrumentation=True, inputFiles=len(manifest['files'])))
    # Do not erase or exempt zombies. Independent exact-ID host teardown owns namespace drain.
    if others:
        raise RuntimeError('Owned namespace still has another process; preserve attribution and independently drain container')
    save('completed.json', dict(replaySemanticPass=True, remainingProcesses=0,
        attribution='NOT_REPRODUCED_OR_MISSED_SAMPLING', rootCauseEstablished=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux':
        raise RuntimeError('Owned network-none Linux container only')
    if os.getpid() != 1:
        raise RuntimeError('Original Python PID1 topology required; no --init or subreaper')
    if args.extract:
        extract()
    else:
        observe()


if __name__ == '__main__':
    main()

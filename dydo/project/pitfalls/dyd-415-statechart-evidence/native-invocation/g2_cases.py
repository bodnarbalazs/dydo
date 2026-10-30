"""Linux NativeAOT feasibility cases; pidfds pin test observations, not product ownership."""
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import select
import signal
import subprocess
import sys

BINARY = Path(sys.argv[1])
SCRATCH = Path(sys.argv[2])
OUT = Path(sys.argv[3])
RESULTS = []


def start(*arguments):
    return subprocess.Popen([str(BINARY), *arguments], stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)


def line(process):
    with ThreadPoolExecutor(max_workers=1) as pool:
        pending = pool.submit(process.stdout.readline)
        try:
            value = pending.result(timeout=15).strip()
        except TimeoutError:
            process.kill()
            raise
    assert value, process.stderr.read()
    return value


def send(process, value):
    process.stdin.write(value + '\n')
    process.stdin.flush()


def exited(fd, milliseconds):
    poll = select.poll()
    poll.register(fd, select.POLLIN)
    return bool(poll.poll(milliseconds))


def wait_dead(fds):
    for fd in fds:
        assert exited(fd, 10000), 'Owned native descendant survived'


def exec_cases():
    process = start('posix-exec')
    pid = process.pid
    stdout, stderr = process.communicate(timeout=15)
    assert process.returncode == 17, stderr
    assert stdout.strip() == f'{pid}|environment with spaces é|argument with spaces é', stdout
    RESULTS.append(dict(case='exec-preserves-pid-argv-environment-exit', pidPreserved=True,
        unicodeAndSpacesPreserved=True, exit=17))
    process = start('posix-exec-wait')
    assert line(process) == 'exec-ready'
    process.send_signal(signal.SIGTERM)
    assert process.wait(timeout=10) == -signal.SIGTERM
    RESULTS.append(dict(case='exec-preserves-sigterm', signal=15, nativePidReplaced=True))


def ownership(name, worker_exit=False, kill_anchor=False, kill_owner=False):
    receipt = SCRATCH / name
    owner = start('posix-owner', str(receipt))
    fds = []
    try:
        ready = line(owner).split()
        assert ready[0] == 'gated' and int(ready[1]) == owner.pid, ready
        anchor = int(ready[2])
        fds.append(os.pidfd_open(anchor))
        send(owner, 'go')
        tree = line(owner).split()
        assert tree[0] == 'tree' and int(tree[1]) == anchor, tree
        worker, leaf = map(int, tree[2:])
        fds.extend([os.pidfd_open(worker), os.pidfd_open(leaf)])
        if worker_exit:
            send(owner, 'exit-worker')
            assert line(owner) == 'worker-exited'
            assert exited(fds[1], 10000)
            assert not exited(fds[0], 0) and not exited(fds[2], 0)
        if kill_owner:
            owner.kill()
        elif kill_anchor:
            os.kill(anchor, signal.SIGKILL)
        else:
            owner.stdin.close()
        code = owner.wait(timeout=15)
        wait_dead(fds)
        result = receipt.read_text().strip() if not kill_owner else 'owner-lost; no cleanup claim'
        if not kill_owner:
            assert code == 0, owner.stderr.read()
            assert result == 'drained status=9 replayRefused=True', result
        RESULTS.append(dict(case=name, workerExitedWithLiveDescendant=worker_exit,
            anchorLoss=kill_anchor, cleanupOwnerLoss=kill_owner,
            allObservedDescendantsTerminated=True, result=result,
            actualPlatform='linux-x64', productionScratchDeletionProved=False))
    finally:
        if owner.poll() is None:
            owner.kill()
            owner.wait(timeout=10)
        for fd in fds:
            os.close(fd)


def main():
    assert sys.platform == 'linux' and BINARY.is_file()
    assert not SCRATCH.exists() and not OUT.exists()
    SCRATCH.mkdir()
    try:
        exec_cases()
        ownership('cli-eof')
        ownership('anchor-loss', kill_anchor=True)
        ownership('cleanup-owner-loss', kill_owner=True)
        ownership('cleanup-loss-after-worker-exit', worker_exit=True, kill_owner=True)
        ownership('cli-loss-after-worker-exit', worker_exit=True)
    finally:
        OUT.write_text(json.dumps(dict(outcomes=RESULTS, expected=7, completed=len(RESULTS),
            nativeAot=True, actualPlatform='linux-x64', noMacOsOrArm64Claim=True,
            noNpmOrJavaBoundaryClaim=True), indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()

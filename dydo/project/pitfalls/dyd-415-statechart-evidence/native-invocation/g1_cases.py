"""Real NativeAOT descendants; process handles pin identities before any forced loss."""
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor

BINARY = Path(sys.argv[1])
SCRATCH = Path(sys.argv[2])
OUT = Path(sys.argv[3])
KERNEL = ctypes.WinDLL('kernel32', use_last_error=True)
KERNEL.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
KERNEL.OpenProcess.restype = wintypes.HANDLE
KERNEL.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
KERNEL.WaitForSingleObject.restype = wintypes.DWORD
KERNEL.CloseHandle.argtypes = [wintypes.HANDLE]
KERNEL.CloseHandle.restype = wintypes.BOOL
RESULTS = []


def start(*arguments):
    return subprocess.Popen([str(BINARY), *arguments], stdin=subprocess.PIPE,
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        creationflags=subprocess.CREATE_NO_WINDOW)


def line(process):
    with ThreadPoolExecutor(max_workers=1) as pool:
        future = pool.submit(process.stdout.readline)
        try:
            result = future.result(timeout=10).strip()
        except TimeoutError:
            process.kill()
            raise
    assert result, 'Native fixture closed stdout: ' + process.stderr.read()
    return result


def send(process, value):
    process.stdin.write(value + '\n')
    process.stdin.flush()


def handle(pid):
    result = KERNEL.OpenProcess(0x100000, False, pid)
    if not result:
        raise ctypes.WinError(ctypes.get_last_error())
    return result


def wait_dead(handles):
    for item in handles:
        assert KERNEL.WaitForSingleObject(item, 10000) == 0, 'Owned process survived'


def native_error(pid, expected):
    process = start('error', str(pid))
    stdout, stderr = process.communicate(timeout=10)
    assert process.returncode == 0, stderr
    assert int(stdout.strip()) == expected, (stdout, stderr)
    RESULTS.append(dict(case='native-error-' + str(pid), nativeError=expected, exit=0))


def ownership(case, stage, graceful=False):
    receipt = SCRATCH / (case + '.observer')
    owner = start('owner', str(receipt))
    handles = []
    try:
        header = line(owner).split()
        assert header[0] == 'gated', header
        observer, cli = map(int, header[2:])
        observer_handle = handle(observer)
        handles.append(handle(cli))
        if stage >= 1:
            send(owner, 'attach')
            assert line(owner) == 'owned True False'
        if stage >= 2:
            send(owner, 'register')
            assert line(owner) == 'registered'
        if stage >= 3:
            send(owner, 'go')
            tree = line(owner).split()
            assert tree[0] == 'tree' and int(tree[1]) == cli, tree
            handles.extend(handle(pid) for pid in map(int, tree[2:]))
        if graceful:
            send(owner, 'terminate')
            assert line(owner) == 'terminated'
            wait_dead(handles)
            send(owner, 'count')
            assert line(owner) == 'active 0'
        else:
            owner.kill()
        owner.wait(timeout=10)
        wait_dead(handles)
        wait_dead([observer_handle])
        observed = receipt.read_text().strip()
        assert observed in ('empty-observed', 'ambiguous-retained'), observed
        if stage == 0:
            assert observed == 'ambiguous-retained', 'No job process existed; no drain may be inferred'
        RESULTS.append(dict(case=case, releasedStage=stage, descendants=len(handles),
            abruptOwnerLoss=not graceful, descendantsTerminated=True,
            observerOutsideJob=True, independentObserverResult=observed,
            productionScratchDeletionProved=False))
    finally:
        if owner.poll() is None:
            owner.kill()
            owner.wait(timeout=10)
        for item in handles:
            KERNEL.CloseHandle(item)
        if 'observer_handle' in locals():
            KERNEL.CloseHandle(observer_handle)


def main():
    assert os.name == 'nt'
    assert BINARY.is_file() and not SCRATCH.exists() and not OUT.exists()
    SCRATCH.mkdir()
    try:
        native_error(0, 87)
        native_error(4, 5)
        ownership('loss-before-assignment', 0)
        ownership('loss-after-assignment-before-registration', 1)
        ownership('loss-after-registration-before-go', 2)
        ownership('loss-with-native-descendants', 3)
        ownership('normal-termination-and-authoritative-empty-query', 3, graceful=True)
    finally:
        OUT.write_text(json.dumps(dict(outcomes=RESULTS, completed=len(RESULTS),
            expected=7, nativeAot=True, noJavaClaim=True), indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()

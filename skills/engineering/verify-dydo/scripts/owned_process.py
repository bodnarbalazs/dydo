"""Bounded Windows commands using the repository's existing native job owner."""
from contextlib import ExitStack
import ctypes
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[4] / "DynaDocs.Tests/coverage"))
import windows_job as jobs


def supervise(kernel, job, process, readers, request, stack):
    state = {"readers": readers, "held": {}}
    failure = None
    error_message = None
    try:
        if kernel.ResumeThread(process.thread) == 0xffffffff:
            jobs.checked(False)
        if jobs.wait_campaign(kernel, job, process, state, stack, request["execution_seconds"]):
            failure = "timeout"
    except (Exception, KeyboardInterrupt) as error:
        failure = type(error).__name__
        error_message = str(error)
    if failure:
        jobs.checked(kernel.TerminateJobObject(job, 2))
    result = jobs.finish(kernel, job, process, readers, state["held"],
                         time.monotonic() + request["teardown_seconds"])
    return {**result, "failure_category": failure, "error_message": error_message}


def native(request, environment, payload, output, result):
    import msvcrt
    kernel = jobs.bindings()
    with ExitStack() as stack:
        job = jobs.checked(kernel.CreateJobObjectW(None, None))
        # KILL_ON_JOB_CLOSE also contains unexpected failures and abrupt owner exit.
        stack.callback(jobs.close_handle, kernel, job)
        limits = jobs.Limits(basic=jobs.BasicLimits(flags=0x2000))
        jobs.checked(kernel.SetInformationJobObject(job, 9, ctypes.byref(limits), ctypes.sizeof(limits)))
        files, handles, readers = jobs.pipes(stack, output, time.monotonic() + request["teardown_seconds"])
        stdin = stack.enter_context((output / "stdin.txt").open("w+b"))
        stdin.write(payload.encode("utf-8"))
        stdin.seek(0)
        handles[0] = msvcrt.get_osfhandle(stdin.fileno())
        os.set_handle_inheritable(handles[0], True)
        storage = jobs.attributes(kernel, job, handles, stack)
        startup = jobs.StartupEx(startup=jobs.Startup(cb=ctypes.sizeof(jobs.StartupEx), flags=0x100))
        startup.startup.stdin, startup.startup.stdout, startup.startup.stderr = handles
        startup.attributes = ctypes.addressof(storage[0])
        process = jobs.ProcessInfo()
        jobs.checked(jobs.launch(kernel, request, environment, startup, process))
        stack.callback(jobs.close_handle, kernel, process.process)
        stack.callback(jobs.close_handle, kernel, process.thread)
        result["root_identity"] = jobs.identity(kernel, process.process, process.pid)
        for stream in files[1:]:
            stream.close()
        result.update(supervise(kernel, job, process, readers, request, stack))


def run_owned(argv, *, cwd, env, input, output, timeout):
    """Return captured bytes as UTF-8 text only after the owned job has drained."""
    executable = shutil.which(argv[0])
    if executable is None:
        raise FileNotFoundError(argv[0])
    request = jobs.request([executable, *argv[1:]], cwd, output, execution_seconds=timeout)
    runtime = jobs.validate(request)  # Unsupported platforms fail before creating any child.
    output.mkdir()
    result = {"request": request, "runtime": runtime, "cleanup_confirmed": False}
    try:
        native(request, env, input, output, result)
    except (Exception, KeyboardInterrupt) as error:
        result.update(failure_category=type(error).__name__, error=str(error))
        raise
    finally:
        (output / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    stdout = (output / "stdout.log").read_bytes().decode("utf-8", errors="surrogateescape")
    stderr = (output / "stderr.log").read_bytes().decode("utf-8", errors="surrogateescape")
    if not result["cleanup_confirmed"]:
        raise RuntimeError("Owned process cleanup unconfirmed; retain scratch and inspect evidence")
    if result["failure_category"] == "KeyboardInterrupt":
        raise KeyboardInterrupt()
    if result["failure_category"] == "timeout":
        raise subprocess.TimeoutExpired(argv, timeout, output=stdout, stderr=stderr)
    if result["failure_category"]:
        raise RuntimeError(f"Owned process observation failed: {result['failure_category']}")
    return subprocess.CompletedProcess(argv, result["subject_status"], stdout, stderr)

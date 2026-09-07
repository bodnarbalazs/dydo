"""Own one Windows process campaign until its job, handles and pipes are complete."""
import ctypes as c
from ctypes import wintypes as w
from contextlib import ExitStack
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid


_unconfirmed_io = []


class Overlapped(c.Structure):
    _fields_ = [("internal", c.c_size_t), ("internal_high", c.c_size_t),
                ("offset", w.DWORD), ("offset_high", w.DWORD), ("event", w.HANDLE)]


class Startup(c.Structure):
    _fields_ = [("cb", w.DWORD), ("reserved", w.LPWSTR), ("desktop", w.LPWSTR),
                ("title", w.LPWSTR), ("x", w.DWORD), ("y", w.DWORD),
                ("xs", w.DWORD), ("ys", w.DWORD), ("xc", w.DWORD), ("yc", w.DWORD),
                ("fill", w.DWORD), ("flags", w.DWORD), ("show", w.WORD),
                ("reserved2", w.WORD), ("bytes", c.POINTER(c.c_byte)),
                ("stdin", w.HANDLE), ("stdout", w.HANDLE), ("stderr", w.HANDLE)]


class StartupEx(c.Structure):
    _fields_ = [("startup", Startup), ("attributes", c.c_void_p)]


class ProcessInfo(c.Structure):
    _fields_ = [("process", w.HANDLE), ("thread", w.HANDLE), ("pid", w.DWORD), ("tid", w.DWORD)]


class BasicLimits(c.Structure):
    _fields_ = [("process_time", c.c_longlong), ("job_time", c.c_longlong),
                ("flags", w.DWORD), ("min_ws", c.c_size_t), ("max_ws", c.c_size_t),
                ("active_limit", w.DWORD), ("affinity", c.c_size_t),
                ("priority", w.DWORD), ("scheduling", w.DWORD)]


class IoCounters(c.Structure):
    _fields_ = [(name, c.c_ulonglong) for name in
                ("read_ops", "write_ops", "other_ops", "read_bytes", "write_bytes", "other_bytes")]


class Limits(c.Structure):
    _fields_ = [("basic", BasicLimits), ("io", IoCounters),
                ("process_memory", c.c_size_t), ("job_memory", c.c_size_t),
                ("peak_process", c.c_size_t), ("peak_job", c.c_size_t)]


class Accounting(c.Structure):
    _fields_ = [("user", c.c_longlong), ("kernel", c.c_longlong),
                ("period_user", c.c_longlong), ("period_kernel", c.c_longlong),
                ("faults", w.DWORD), ("total", w.DWORD), ("active", w.DWORD), ("terminated", w.DWORD)]


def config_hash(value):
    encoded = json.dumps({key: item for key, item in value.items() if key != "config_sha256"},
                         sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def request(argv, cwd, output, execution_seconds=60, teardown_seconds=10):
    value = {"version": 1, "owner_id": uuid.uuid4().hex, "argv": list(argv),
             "cwd": str(Path(cwd).resolve()), "output": str(Path(output).resolve()),
             "execution_seconds": execution_seconds, "teardown_seconds": teardown_seconds,
             "helper_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    value["config_sha256"] = config_hash(value)
    return value


def validate(value):
    keys = {"version", "owner_id", "argv", "cwd", "output", "execution_seconds",
            "teardown_seconds", "helper_sha256", "config_sha256"}
    if (set(value) != keys or type(value["version"]) is not int or value["version"] != 1
            or value["config_sha256"] != config_hash(value)):
        raise ValueError("Invalid owner configuration")
    if os.name != "nt" or sys.version_info[:3] != (3, 12, 14):
        raise ValueError("Owner requires Windows Python 3.12.14")
    if value["helper_sha256"] != hashlib.sha256(Path(__file__).read_bytes()).hexdigest():
        raise ValueError("Owner source changed")
    argv = value["argv"]
    if not isinstance(argv, list) or not argv or any(not isinstance(arg, str) or "\0" in arg for arg in argv):
        raise ValueError("Owner requires a string argument vector")
    if not Path(argv[0]).is_absolute() or not Path(value["cwd"]).is_dir():
        raise ValueError("Owner requires an absolute executable and existing cwd")
    if not Path(value["output"]).is_absolute():
        raise ValueError("Owner output must be absolute")
    for key, maximum in (("execution_seconds", 1800), ("teardown_seconds", 10)):
        if type(value[key]) not in (float, int) or not 0 < value[key] <= maximum:
            raise ValueError("Invalid owner deadline")
    if not isinstance(value["owner_id"], str) or len(value["owner_id"]) != 32 or any(char not in "0123456789abcdef" for char in value["owner_id"]):
        raise ValueError("Invalid owner identity")


def bindings():
    kernel = c.WinDLL("kernel32", use_last_error=True)
    specifications = {
        "CreateNamedPipeW": ([w.LPCWSTR, w.DWORD, w.DWORD, w.DWORD, w.DWORD, w.DWORD, w.DWORD, c.c_void_p], w.HANDLE),
        "CreateFileW": ([w.LPCWSTR, w.DWORD, w.DWORD, c.c_void_p, w.DWORD, w.DWORD, w.HANDLE], w.HANDLE),
        "CreateEventW": ([c.c_void_p, w.BOOL, w.BOOL, w.LPCWSTR], w.HANDLE),
        "ResetEvent": ([w.HANDLE], w.BOOL),
        "ReadFile": ([w.HANDLE, c.c_void_p, w.DWORD, c.c_void_p, c.POINTER(Overlapped)], w.BOOL),
        "GetOverlappedResult": ([w.HANDLE, c.POINTER(Overlapped), c.POINTER(w.DWORD), w.BOOL], w.BOOL),
        "CancelIoEx": ([w.HANDLE, c.POINTER(Overlapped)], w.BOOL),
        "CreateJobObjectW": ([c.c_void_p, w.LPCWSTR], w.HANDLE),
        "SetInformationJobObject": ([w.HANDLE, c.c_int, c.c_void_p, w.DWORD], w.BOOL),
        "QueryInformationJobObject": ([w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p], w.BOOL),
        "CreateProcessW": ([w.LPCWSTR, w.LPWSTR, c.c_void_p, c.c_void_p, w.BOOL, w.DWORD,
                            c.c_void_p, w.LPCWSTR, c.c_void_p, c.POINTER(ProcessInfo)], w.BOOL),
        "InitializeProcThreadAttributeList": ([c.c_void_p, w.DWORD, w.DWORD, c.POINTER(c.c_size_t)], w.BOOL),
        "UpdateProcThreadAttribute": ([c.c_void_p, w.DWORD, c.c_size_t, c.c_void_p, c.c_size_t, c.c_void_p, c.c_void_p], w.BOOL),
        "DeleteProcThreadAttributeList": ([c.c_void_p], None),
        "ResumeThread": ([w.HANDLE], w.DWORD),
        "TerminateJobObject": ([w.HANDLE, w.UINT], w.BOOL),
        "WaitForSingleObject": ([w.HANDLE, w.DWORD], w.DWORD),
        "GetExitCodeProcess": ([w.HANDLE, c.POINTER(w.DWORD)], w.BOOL),
        "CloseHandle": ([w.HANDLE], w.BOOL),
        "OpenProcess": ([w.DWORD, w.BOOL, w.DWORD], w.HANDLE),
        "IsProcessInJob": ([w.HANDLE, w.HANDLE, c.POINTER(w.BOOL)], w.BOOL),
        "GetProcessTimes": ([w.HANDLE, *[c.POINTER(w.FILETIME)] * 4], w.BOOL),
    }
    for name, (arguments, result) in specifications.items():
        function = getattr(kernel, name)
        function.argtypes, function.restype = arguments, result
    return kernel


def checked(value):
    if not value:
        raise c.WinError(c.get_last_error())
    return value


def close_handle(kernel, handle):
    checked(kernel.CloseHandle(handle))


def accounting(kernel, job):
    row = Accounting()
    checked(kernel.QueryInformationJobObject(job, 1, c.byref(row), c.sizeof(row), None))
    return row


def identity(kernel, handle, pid):
    values = [w.FILETIME() for _ in range(4)]
    checked(kernel.GetProcessTimes(handle, *[c.byref(value) for value in values]))
    return {"pid": pid, "creation_filetime": (values[0].dwHighDateTime << 32) | values[0].dwLowDateTime}


def members(kernel, job):
    size = 1024
    while size <= 1048576:
        data = c.create_string_buffer(size)
        if kernel.QueryInformationJobObject(job, 3, data, size, None):
            count = w.DWORD.from_buffer(data, 4).value
            return [c.c_size_t.from_buffer(data, 8 + index * c.sizeof(c.c_size_t)).value for index in range(count)]
        if c.get_last_error() != 234:
            checked(False)
        size *= 2
    raise ValueError("Job membership exceeds bounded native buffer")


def retain_members(kernel, job, held, stack):
    for pid in members(kernel, job):
        if pid in held:
            continue
        handle = kernel.OpenProcess(0x101000, False, pid)
        if not handle:
            continue  # An exited member may disappear before OpenProcess; job accounting remains authoritative.
        stack.callback(close_handle, kernel, handle)
        inside = w.BOOL()
        checked(kernel.IsProcessInJob(handle, job, c.byref(inside)))
        if inside.value:
            held[pid] = (handle, identity(kernel, handle, pid))


def native_handle(value):
    if value == c.c_void_p(-1).value:
        checked(False)
    return checked(value)


def issue_read(kernel, state):
    checked(kernel.ResetEvent(state['event']))
    state['overlapped'] = Overlapped()
    state['overlapped'].event = state['event']
    if kernel.ReadFile(state['server'], state['buffer'], 65536, None, c.byref(state['overlapped'])):
        state['pending'] = True
        return
    error = c.get_last_error()
    if error == 997:
        state['pending'] = True
    elif error == 109:
        state['eof'] = True
    else:
        raise c.WinError(error)


def poll_pipe(kernel, state):
    for _ in range(16):
        if not state['pending']:
            return
        count = w.DWORD()
        if not kernel.GetOverlappedResult(state['server'], c.byref(state['overlapped']), c.byref(count), False):
            error = c.get_last_error()
            if error == 996:
                return
            if error == 109:
                state['pending'] = False
                state['eof'] = True
                return
            state['error'] = True
            raise c.WinError(error)
        state['pending'] = False
        state['target'].write(state['buffer'].raw[:count.value])
        state['bytes'] += count.value
        issue_read(kernel, state)


def close_pipe(kernel, state):
    if state.get('closed') or state.get('retained'):
        return
    if state['pending']:
        confirmed = False
        try:
            kernel.CancelIoEx(state['server'], c.byref(state['overlapped']))
            confirmed = signaled(kernel, state['event'], state['deadline'])
        finally:
            if not confirmed:
                state['retained'] = True
                _unconfirmed_io.append(state)
        if not confirmed:
            return
        state['pending'] = False
    with ExitStack() as release:
        release.callback(close_handle, kernel, state['event'])
        release.callback(close_handle, kernel, state['server'])
        state['target'].close()
    state['closed'] = True


def make_pipe(kernel, output, name, stack, deadline):
    import msvcrt
    pipe_name = '\\\\.\\pipe\\dydo-' + uuid.uuid4().hex
    server = native_handle(kernel.CreateNamedPipeW(pipe_name, 0x40000001, 0, 1, 65536, 65536, 0, None))
    event = None
    registered = False
    try:
        event = checked(kernel.CreateEventW(None, True, False, None))
        client = native_handle(kernel.CreateFileW(pipe_name, 0x40000000, 0, None, 3, 0, None))
        file = stack.enter_context(os.fdopen(msvcrt.open_osfhandle(client, os.O_WRONLY | os.O_BINARY), 'wb'))
        state = {'server': server, 'event': event, 'buffer': c.create_string_buffer(65536),
                 'overlapped': Overlapped(), 'pending': False, 'eof': False, 'error': False,
                 'bytes': 0, 'target': open(output / (name + '.log'), 'wb'), 'deadline': deadline}
        stack.callback(close_pipe, kernel, state)
        registered = True
        issue_read(kernel, state)
        return file, state
    except BaseException:
        if not registered:
            try:
                close_handle(kernel, server)
            finally:
                if event:
                    close_handle(kernel, event)
        raise


def pipes(stack, output, deadline):
    import msvcrt
    if _unconfirmed_io:
        raise ValueError('Unconfirmed native I/O is retained; refuse owner reuse')
    kernel = bindings()
    files = [stack.enter_context(open(os.devnull, 'rb'))]
    readers = []
    for name in ('stdout', 'stderr'):
        file, state = make_pipe(kernel, output, name, stack, deadline)
        files.append(file)
        readers.append(state)
    handles = [msvcrt.get_osfhandle(value.fileno()) for value in files]
    for handle in handles:
        os.set_handle_inheritable(handle, True)
    return files, handles, readers


def attributes(kernel, job, handles, stack):
    size = c.c_size_t()
    kernel.InitializeProcThreadAttributeList(None, 2, 0, c.byref(size))
    if not size.value:
        checked(False)
    buffer = c.create_string_buffer(size.value)
    checked(kernel.InitializeProcThreadAttributeList(buffer, 2, 0, c.byref(size)))
    stack.callback(kernel.DeleteProcThreadAttributeList, buffer)
    jobs = (w.HANDLE * 1)(job)
    native = (w.HANDLE * 3)(*handles)
    checked(kernel.UpdateProcThreadAttribute(buffer, 0, 0x2000D, jobs, c.sizeof(jobs), None, None))
    checked(kernel.UpdateProcThreadAttribute(buffer, 0, 0x20002, native, c.sizeof(native), None, None))
    return buffer, jobs, native


def launch(kernel, value, environment, startup, pi):
    entries = []
    for key, item in sorted(environment.items(), key=lambda pair: pair[0].upper()):
        if not isinstance(key, str) or not isinstance(item, str) or "\0" in key + item or "=" in key[1:]:
            raise ValueError("Invalid native environment")
        entries.append(key + "=" + item)
    block = c.create_unicode_buffer("\0".join(entries) + "\0\0")
    argv = value["argv"]
    return kernel.CreateProcessW(argv[0], c.create_unicode_buffer(subprocess.list2cmdline(argv)),
                                 None, None, True, 0x08080404, block, value["cwd"], c.byref(startup), c.byref(pi))


def signaled(kernel, handle, deadline):
    result = kernel.WaitForSingleObject(handle, max(0, int((deadline - time.monotonic()) * 1000)))
    if result == 0xffffffff:
        checked(False)
    return result == 0


def finish(kernel, job, pi, readers, held, deadline):
    for state in readers:
        state['deadline'] = deadline
    while time.monotonic() < deadline:
        for state in readers:
            poll_pipe(kernel, state)
        if accounting(kernel, job).active == 0 and all(state['eof'] for state in readers):
            break
        time.sleep(.005)
    state = accounting(kernel, job)
    root_done = signaled(kernel, pi.process, deadline)
    observations = [{**row, "signaled": signaled(kernel, handle, deadline)} for handle, row in held.values()]
    drained = all(state['eof'] and not state['error'] and not state['pending'] for state in readers)
    code = w.DWORD()
    checked(kernel.GetExitCodeProcess(pi.process, c.byref(code)))
    return {"job_empty": state.active == 0, "job_total": state.total,
            "root_signaled": root_done, "held_handles": observations, "streams_complete": drained,
            "subject_status": code.value, "cleanup_confirmed": state.active == 0 and root_done and drained
            and all(row["signaled"] for row in observations)}


def wait_campaign(kernel, job, pi, state, stack, seconds):
    deadline = time.monotonic() + seconds
    while True:
        for reader in state["readers"]:
            poll_pipe(kernel, reader)
        retain_members(kernel, job, state["held"], stack)
        current = accounting(kernel, job)
        drained = all(part["eof"] and not part["error"] for part in state["readers"])
        if not current.active and drained and signaled(kernel, pi.process, time.monotonic()):
            return False
        if time.monotonic() >= deadline:
            return True
        time.sleep(.01)


def supervise(kernel, job, pi, state, value, stack):
    failure = None
    try:
        if kernel.ResumeThread(pi.thread) == 0xffffffff:
            checked(False)
        if wait_campaign(kernel, job, pi, state, stack, value["execution_seconds"]):
            failure = "timeout"
    except (OSError, ValueError):
        failure = "observation"
    start = time.monotonic()
    deadline = start + value["teardown_seconds"]
    if failure:
        checked(kernel.TerminateJobObject(job, 2))
    result = finish(kernel, job, pi, state["readers"], state["held"], deadline)
    result.update(failure_category=failure, teardown_elapsed_seconds=time.monotonic() - start)
    result["complete"] = failure is None and result["cleanup_confirmed"]
    return result


def native_run(value, environment, output, result):
    kernel = bindings()
    with ExitStack() as stack:
        job = checked(kernel.CreateJobObjectW(None, None))
        stack.callback(close_handle, kernel, job)
        limits = Limits()
        limits.basic.flags = 0x2000  # KILL_ON_JOB_CLOSE; breakaway is deliberately absent.
        checked(kernel.SetInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits)))
        files, handles, readers = pipes(stack, output, time.monotonic() + value['teardown_seconds'])
        storage = attributes(kernel, job, handles, stack)
        startup = StartupEx()
        startup.startup.cb, startup.startup.flags = c.sizeof(startup), 0x100
        startup.startup.stdin, startup.startup.stdout, startup.startup.stderr = handles
        startup.attributes = c.addressof(storage[0])
        pi = ProcessInfo()
        if not launch(kernel, value, environment, startup, pi):
            error_code = c.get_last_error()
            for stream in files[1:]:
                stream.close()
            deadline = time.monotonic() + value["teardown_seconds"]
            for state in readers:
                state['deadline'] = deadline
            return {"launch_state": "refused", "error_code": error_code, "failure_category": "creation"}
        stack.callback(close_handle, kernel, pi.process)
        stack.callback(close_handle, kernel, pi.thread)
        result["launch_state"] = "started"
        result["root_identity"] = identity(kernel, pi.process, pi.pid)
        for stream in files[1:]:
            stream.close()
        state = {"readers": readers, "held": {}}
        result.update(supervise(kernel, job, pi, state, value, stack))
        return result


def preflight():
    if os.name != "nt" or sys.version_info[:3] != (3, 12, 14):
        raise ValueError("Owner requires Windows Python 3.12.14")
    import msvcrt
    kernel = bindings()
    with ExitStack() as stack:
        job = checked(kernel.CreateJobObjectW(None, None))
        stack.callback(close_handle, kernel, job)
        limits = Limits()
        limits.basic.flags = 0x2000
        checked(kernel.SetInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits)))
        files = [stack.enter_context(open(os.devnull, "rb")) for _ in range(3)]
        handles = [msvcrt.get_osfhandle(value.fileno()) for value in files]
        attributes(kernel, job, handles, stack)
    return {"version": list(sys.version_info[:3]), "executable": sys.executable,
            "executable_sha256": hashlib.sha256(Path(sys.executable).read_bytes()).hexdigest(),
            "base_executable": sys._base_executable,
            "base_sha256": hashlib.sha256(Path(sys._base_executable).read_bytes()).hexdigest(),
            "helper_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), "capability": True}


def run(value, environment=None):
    started = time.monotonic()
    result = {"version": 1, "owner_id": value.get("owner_id"), "config_sha256": value.get("config_sha256"),
              "helper_sha256": value.get("helper_sha256"), "launch_state": "not_started",
              "root_identity": None, "subject_status": None, "complete": False,
              "cleanup_confirmed": False, "failure_category": "preflight", "error_code": None,
              "error_message": None,
              "execution_seconds": value.get("execution_seconds"), "teardown_seconds": value.get("teardown_seconds")}
    output = None
    try:
        validate(value)
        candidate = Path(value["output"])
        candidate.mkdir(parents=True, exist_ok=False)
        output = candidate
        result.update(stdout_path=str(output / "stdout.log"), stderr_path=str(output / "stderr.log"), retained_paths=[str(output)])
        result.update(native_run(value, dict(os.environ) if environment is None else dict(environment), output, result))
    except (OSError, ValueError, AttributeError, TypeError) as error:
        result.update(complete=False, cleanup_confirmed=False)
        result["error_code"] = getattr(error, "winerror", None)
        result["error_message"] = str(error)
        if output is not None:
            result["failure_category"] = "native_failure"
    result["elapsed_seconds"] = time.monotonic() - started
    if output is not None:
        (output / "result.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    return result


def main():
    try:
        if sys.argv[1:] == ["--preflight"]:
            print(json.dumps(preflight()))
            return 0
        value = json.load(sys.stdin)
        result = run(value)
    except (OSError, ValueError, TypeError, AttributeError):
        result = {"complete": False, "failure_category": "request", "launch_state": "not_started"}
    print(json.dumps(result))
    return 0 if result["complete"] else 2


if __name__ == "__main__":
    raise SystemExit(main())

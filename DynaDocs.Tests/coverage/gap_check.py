#!/usr/bin/env python3
"""Run this project's declared tests and assurance gates without a shell."""
import atexit
from contextlib import contextmanager
import ctypes as c
import hashlib
import json
import os
import re
import signal
import shutil
import subprocess
import sys
import time
import uuid
from pathlib import Path, PureWindowsPath

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")


interrupted = False
interrupt_deferred = False


def interrupt(_signum, _frame):
    """Record the run's first interrupt, raising outside ownership changes. A further one is
    dropped: it could only abort the adapter's stop, which CLEANUP_SECONDS and REAP_SECONDS already
    bound, and the result's publication."""
    global interrupted
    if not interrupted:
        interrupted = True
        if not interrupt_deferred:
            raise KeyboardInterrupt


@contextmanager
def defer_interrupt(deferred=True):
    """Record signals without unwinding ownership changes; never change the child's signal mask.

    The caller checks `interrupted` at its next safe boundary. Waiting for an owned child opts
    back into delivery so cancellation can stop it promptly.
    """
    global interrupt_deferred
    previous = interrupt_deferred
    interrupt_deferred = deferred
    try:
        yield
    finally:
        interrupt_deferred = previous


if sys.platform == "win32":
    signal.signal(signal.SIGBREAK, interrupt)

CAPS = ("test", "static", "coverage", "mutation")
EXITS = {"passed": 0, "failed": 1, "unavailable": 2, "invalid": 2, "interrupted": 130}
EXECUTION_SECONDS_MAXIMUM = 1800
CLEANUP_SECONDS = 30
REAP_SECONDS = 1
ROW_DEADLINE_ENV = "DYDO_ROW_DEADLINE"
MEMORY_MAX_ENV = "DYDO_MEMORY_MAX"
MEMORY_MAX_DEFAULT = 8 * 1024 ** 3
# Set only for the operation's own rerun under systemd-run, which removes it before any child starts.
MEMORY_SCOPE_ENV = "DYDO_MEMORY_CAP_SCOPE"
MEMORY_UNITS = {"": 1, "K": 1024, "M": 1024 ** 2, "G": 1024 ** 3, "T": 1024 ** 4}
CGROUP_ROOT = Path("/sys/fs/cgroup")
SELF_CGROUP = Path("/proc/self/cgroup")
SYSTEMD_SECONDS = 30
HELP = """Usage: gap_check.py [--force-run] [operation] [options] [-- native arguments]

Project testing facade. It runs manifest argv vectors directly; it never invokes a shell.
  test --stack NAME [-- ARGS]                run one stack's selected tests
  all [--stack NAME[,NAME]]                  run selected tests (all by default)
  gate static|coverage [--stack NAME[,NAME]]
  gate mutation --since BASE [--stack NAME[,NAME]]
  capabilities                               report configuration without execution
  --force-run                                tests, static, coverage for every stack

test requires one stack. all and gate default to every declared stack in manifest order.
Native arguments after -- are forwarded only by test. Every started operation writes a result
artifact: <artifactRoot>/run-<unique-id>/result.json, using the adjacent manifest's artifactRoot.
Examples: test --stack dotnet -- --filter FullyQualifiedName~ParserTests
          all --stack frontend,python
          gate mutation --since BASE --stack dotnet
 Exit 0 is pass, 1 measured failure, 2 invalid or unavailable work, and 130 interruption
after adapter cleanup. Results record candidate identity, argv, cwd, isolation, evidence, and artifacts.
Operations that run children are capped at 8G by a Windows job or, with swap off, Linux systemd;
DYDO_MEMORY_MAX may only lower the cap. A reported hit fails the row; Windows reporting is best-effort.
"""


class ContractError(Exception):
    pass


def contained(root, value):
    if not isinstance(value, str) or not value or any(c in value for c in "<>") or Path(value).is_absolute() or PureWindowsPath(value).is_absolute():
        return None
    try:
        path = (root / value).resolve()
        path.relative_to(root)
        return path
    except ValueError:
        return None


def read_manifest(path):
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ContractError(f"invalid JSON manifest: {error}") from error
    if not isinstance(value, dict) or type(value.get("schema")) is not int or value["schema"] != 1:
        raise ContractError("schema must be the integer 1")
    if set(value) != {"schema", "artifactRoot", "stacks"}:
        raise ContractError("manifest requires schema, artifactRoot and stacks")
    stacks = value["stacks"]
    if not isinstance(stacks, list) or not stacks:
        raise ContractError("stacks field must be a nonempty array")
    names = [item.get("name") if isinstance(item, dict) else None for item in stacks]
    if any(not isinstance(name, str) or not name for name in names):
        raise ContractError("every stack needs a nonempty name")
    if len(set(names)) != len(names):
        raise ContractError("duplicate stack names")
    return value


def result(stack, capability, state, **values):
    item = {"stack": stack.get("name", "unknown"), "capability": capability, "state": state,
            "argv": [], "cwd": stack.get("cwd", ""), "isolation": stack.get("isolation"),
            "environment": {}, "childExit": None, "resultExit": EXITS[state], "artifacts": []}
    item.update(values)
    return item


def stack_error(stack, root):
    if not isinstance(stack, dict) or set(stack) != {"name", "kind", "cwd", "isolation", "capabilities"}:
        return "stack must have exactly name, kind, cwd, isolation and capabilities"
    if not isinstance(stack["kind"], str) or not stack["kind"] or not contained(root, stack["cwd"]) or not contained(root, stack["cwd"]).is_dir():
        return "stack kind and repository-contained cwd are required"
    isolation = stack["isolation"]
    if not isinstance(isolation, dict) or set(isolation) != {"requirement", "evidence"}:
        return "malformed isolation"
    if isolation["requirement"] not in ("in-place", "git-worktree-copy-working-changes", "per-run-artifacts"):
        return "unknown isolation requirement"
    if not isinstance(stack["capabilities"], dict) or set(stack["capabilities"]) != set(CAPS):
        return "capabilities must contain exactly test, static, coverage and mutation"


def evidence_error(stack, root):
    evidence = stack["isolation"]["evidence"]
    requirement = stack["isolation"]["requirement"]
    if not isinstance(evidence, dict) or not isinstance(evidence.get("state"), str):
        return "invalid", "malformed configured evidence"
    if evidence["state"] == "unavailable" and set(evidence) == {"state", "reason"}:
        reason = evidence.get("reason")
        return ("unavailable", reason) if isinstance(reason, str) and reason else ("invalid", "unavailable evidence requires reason")
    if evidence["state"] != "verified":
        return "invalid", "evidence state must be verified or unavailable"
    if requirement == "in-place":
        return ("passed", None) if evidence.get("kind") == "direct" and set(evidence) == {"state", "kind"} else ("invalid", "verified in-place evidence requires kind direct")
    path = contained(root, evidence.get("path"))
    return ("passed", None) if evidence.get("kind") == "adapter" and set(evidence) == {"state", "kind", "path"} and path and path.is_file() else ("invalid", "verified isolation requires a repository-contained adapter")


def valid_unavailable(config):
    return (isinstance(config, dict) and config.get("state") == "unavailable"
            and isinstance(config.get("reason"), str) and bool(config["reason"])
            and set(config) <= {"state", "reason", "exampleArgv"}
            and ("exampleArgv" not in config or isinstance(config["exampleArgv"], list)
                 and all(isinstance(item, str) and item for item in config["exampleArgv"])))


def resolve_executable(value, working_directory):
    executable = Path(value)
    if not executable.is_absolute() and ("/" in value or os.sep in value):
        executable = (working_directory / executable).resolve()
    search_path = None
    if sys.platform == "win32" and executable.parent == Path("."):
        search_path = os.pathsep.join((str(working_directory), os.environ.get("PATH", "")))
    return shutil.which(str(executable), path=search_path)


def suite_verdict_error(declaration):
    if not isinstance(declaration, dict) or set(declaration) != {"exit", "failure"}:
        return "invalid suite verdict declaration"
    exit_path = declaration["exit"]
    if not isinstance(exit_path, list) or not exit_path or not all(isinstance(item, str) and item for item in exit_path):
        return "invalid suite verdict declaration"
    failure = declaration["failure"]
    if not isinstance(failure, list) or len(failure) < 2:
        return "invalid suite verdict declaration"
    if not all(isinstance(item, str) and item for item in failure[:-1]):
        return "invalid suite verdict declaration"
    matcher = failure[-1]
    if (not isinstance(matcher, dict) or not matcher
            or not all(isinstance(key, str) and key for key in matcher)
            or not all(type(value) in (str, int, bool) for value in matcher.values())):
        return "invalid suite verdict declaration"
    return None


def command_argv_error(command):
    if not isinstance(command, dict) or set(command) != {"kind", "argv"} or command["kind"] not in ("argv", "current-python"):
        return "command kind must be argv or current-python"
    argv = command["argv"]
    if not isinstance(argv, list) or not argv or not all(isinstance(x, str) and x for x in argv):
        return "command argv must be a nonempty array of strings"
    if any("\x00" in item for item in argv):
        return "command argv cannot contain NUL"
    if any("<" in x or ">" in x for x in argv):
        return "command vector contains an angle placeholder"
    return None


def artifacts_error(artifacts, root):
    if not isinstance(artifacts, list):
        return "artifacts must be an array"
    for item in artifacts:
        if not isinstance(item, dict) or set(item) != {"path", "required"} or not isinstance(item["required"], bool) or not contained(root, item["path"]):
            return "artifact requires contained path and required boolean"
    return None


def configured_command(config, root, capability):
    if "suiteVerdict" in config and capability != "coverage":
        return None, None, None, "invalid suite verdict declaration"
    if set(config) - {"suiteVerdict"} != {"state", "command", "artifacts"}:
        return None, None, None, "configured capability requires command and artifacts and forbids reason"
    error = command_argv_error(config["command"])
    if error:
        return None, None, None, error
    artifacts = config["artifacts"]
    error = artifacts_error(artifacts, root)
    if error:
        return None, None, None, error
    if capability != "test" and not any(item["required"] for item in artifacts):
        return None, None, None, "configured gates require a required artifact"
    if "suiteVerdict" in config:
        error = suite_verdict_error(config["suiteVerdict"])
        if error:
            return None, None, None, error
        if sum(1 for item in artifacts if item["required"]) != 1:
            return None, None, None, "invalid suite verdict declaration"
    return config["command"]["kind"], config["command"]["argv"], artifacts, None


def mutation_command(argv, since, inspection):
    if argv.count("{base}") != 1 or any("{base}" in item and item != "{base}" for item in argv):
        return None, "mutation requires exactly one argv element equal to {base} and no substring occurrence"
    if not since and not inspection:
        return None, "mutation requires --since BASE"
    if since is not None:
        argv = [since if item == "{base}" else item for item in argv]
    return argv, None


def command_error(capability, config, root, working_directory, since, inspection=False):
    if not isinstance(config, dict) or config.get("state") not in ("configured", "unavailable"):
        return None, None, "invalid capability state"
    if config["state"] == "unavailable":
        if valid_unavailable(config):
            return None, None, config["reason"]
        return None, None, "unavailable capability requires reason and forbids command and artifacts"
    kind, argv, artifacts, error = configured_command(config, root, capability)
    if error:
        return None, None, error
    if capability == "mutation":
        argv, error = mutation_command(argv, since, inspection)
        if error:
            return None, None, error
    elif any("{base}" in item for item in argv):
        return None, None, "{base} is only valid for mutation"
    actual = [sys.executable, *argv] if kind == "current-python" else list(argv)
    if kind == "argv" and not resolve_executable(actual[0], working_directory):
        return None, None, f"missing executable: {actual[0]}"
    return actual, artifacts, None


def prepare_row(stack, capability, root, since=None, inspection=False):
    error = stack_error(stack, root)
    if error:
        return None, None, "invalid", error
    config = stack["capabilities"][capability]
    working_directory = contained(root, stack["cwd"])
    argv, artifacts, error = command_error(capability, config, root, working_directory, since, inspection)
    if error and not valid_unavailable(config):
        return None, None, "invalid", error
    state, reason = evidence_error(stack, root)
    if state != "passed":
        return None, None, state, reason
    if error:
        return None, None, "unavailable", error
    return argv, artifacts, "configured", None


def artifact_snapshot(root, value):
    path = contained(root, value)
    if path is None:
        raise ContractError(f"artifact escapes repository: {value}")
    if not path.exists():
        return None
    entries = [path, *sorted(path.rglob("*"))] if path.is_dir() else [path]
    snapshot = []
    for entry in entries:
        if not entry.resolve().is_relative_to(root):
            raise ContractError(f"artifact entry escapes repository: {entry}")
        info = entry.stat()
        digest = None
        if entry.is_file():
            hasher = hashlib.sha256()
            with entry.open("rb") as stream:
                for chunk in iter(lambda: stream.read(65536), b""):
                    hasher.update(chunk)
            digest = hasher.digest()
        elif not entry.is_dir():
            raise ContractError(f"artifact is not a file or directory: {entry}")
        snapshot.append((str(entry.relative_to(path)), info.st_mode, info.st_ino, info.st_size, info.st_mtime_ns, digest))
    return snapshot


def held(call, *args):
    """Make one Popen call with SIGINT held. An interrupt raised inside POSIX Popen after it takes
    its waitpid lock leaks the lock, and the facade can then never reap an exited adapter. Held, the
    interrupt surfaces here once the call returns. Windows Popen reaps by handle and takes no lock.
    The mask is the calling thread's: this holds only while the facade's main thread is the one
    thread a process-directed SIGINT can reach, so the facade must start no thread that leaves it
    unblocked."""
    if not hasattr(signal, "pthread_sigmask") or signal.SIGINT in signal.pthread_sigmask(signal.SIG_BLOCK, ()):
        return call(*args)
    try:
        signal.pthread_sigmask(signal.SIG_BLOCK, (signal.SIGINT,))
        return call(*args)
    finally:
        signal.pthread_sigmask(signal.SIG_UNBLOCK, (signal.SIGINT,))


def exit_confirmed(child, deadline):
    """Wait in short held slices, so an interrupt surfaces between them, never inside the reap."""
    while held(child.poll) is None:
        if time.monotonic() >= deadline:
            return False
        try:
            held(child.wait, min(0.2, max(0, deadline - time.monotonic())))
        except subprocess.TimeoutExpired:
            pass
    return True


def kill_child(child):
    if sys.platform == "win32":
        child.kill()
    else:
        try:
            os.killpg(child.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    # Stuck in the kernel: the reap stays unconfirmed and returncode stays None.
    exit_confirmed(child, time.monotonic() + REAP_SECONDS)


def stop_child(child, deadline):
    if child.returncode is not None:
        return  # Already reaped: its pid, and so its process group, may belong to another process.
    try:
        if sys.platform == "win32":
            child.send_signal(signal.CTRL_BREAK_EVENT)
        else:
            os.killpg(child.pid, signal.SIGINT)
        if exit_confirmed(child, deadline):
            return
    except OSError:
        pass
    kill_child(child)


def stopped(reason, child):
    return reason if child.returncode is not None else f"{reason}; the killed adapter was not reaped within {REAP_SECONDS} s"


def wait_for_child(child, capability, deadline):
    execution_deadline = deadline if capability != "test" else deadline - CLEANUP_SECONDS
    exit_confirmed(child, execution_deadline)
    # The clock is read after the exit is observed, so an exit that races the deadline is deadline-exceeded.
    if time.monotonic() < execution_deadline:
        return True
    stop_child(child, deadline)
    return False


def artifact_error(root, capability, artifacts, before):
    if capability == "test":
        return None
    for path, previous in before.items():
        current = artifact_snapshot(root, path)
        if current is None or current == previous:
            return f"required artifact was not produced or refreshed: {path}"
    return None


def completed_row(stack, capability, argv, environment, child, artifacts):
    if capability != "test" and child.returncode in (2, 130):
        state = "invalid" if child.returncode == 2 else "interrupted"
    else:
        state = "passed" if child.returncode == 0 else "failed"
    return result(stack, capability, state, argv=argv, environment=environment,
                  childExit=child.returncode, artifacts=artifacts)


def run_row(stack, capability, root, since, forwarded, deadline):
    argv, artifacts, state, reason = prepare_row(stack, capability, root, since)
    if state != "configured":
        return result(stack, capability, state, reason=reason)
    if capability == "test":
        argv += forwarded
    child = None
    environment = {ROW_DEADLINE_ENV: format(deadline, ".9f")}
    try:
        working_directory = contained(root, stack["cwd"])
        before = {item["path"]: artifact_snapshot(root, item["path"]) for item in artifacts
                  if item["required"] and capability != "test"}
        with defer_interrupt():
            if not interrupted:
                child = subprocess.Popen(argv, executable=resolve_executable(argv[0], working_directory), cwd=working_directory, env={**os.environ, "PYTHON": sys.executable, **environment}, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                                         start_new_session=sys.platform != "win32")
        with defer_interrupt(False):
            if interrupted:
                raise KeyboardInterrupt
            finished = wait_for_child(child, capability, deadline)
        if not finished:
            return result(stack, capability, "interrupted", argv=argv, environment=environment,
                          childExit=child.returncode, artifacts=artifacts,
                          reason=stopped("row deadline exceeded after adapter cleanup", child))
        error = artifact_error(root, capability, artifacts, before)
        if interrupted:
            raise KeyboardInterrupt
        if error:
            return result(stack, capability, "invalid", argv=argv, environment=environment,
                          childExit=child.returncode, artifacts=artifacts, reason=error)
        return completed_row(stack, capability, argv, environment, child, artifacts)
    except KeyboardInterrupt:
        if child is None:
            return result(stack, capability, "interrupted", argv=argv, environment=environment, artifacts=artifacts, reason="interrupted before adapter launch")
        stop_child(child, time.monotonic() + CLEANUP_SECONDS)
        return result(stack, capability, "interrupted", argv=argv, environment=environment, childExit=child.returncode, artifacts=artifacts, reason=stopped("adapter interrupted after cleanup window", child))
    except (OSError, ContractError) as error:
        return result(stack, capability, "invalid", argv=argv, environment=environment, artifacts=artifacts, childExit=child.returncode if child else None, reason=f"adapter or artifact error: {error}")


INTERRUPTED_BEFORE_COVERAGE = "suite verdict not established: the run was interrupted before the coverage row"


def deferral_applies(stack, capabilities, root):
    if "test" not in capabilities or "coverage" not in capabilities:
        return False
    _, _, coverage_state, _ = prepare_row(stack, "coverage", root)
    if coverage_state != "configured":
        return False
    if "suiteVerdict" not in stack["capabilities"]["coverage"]:
        return False
    _, _, test_state, _ = prepare_row(stack, "test", root)
    return test_state == "configured"


def walk(value, keys):
    for key in keys:
        if not isinstance(value, dict) or key not in value:
            return None
        value = value[key]
    return value


def failure_finding(report, declaration):
    container = walk(report, declaration[:-1])
    if not isinstance(container, list):
        return None
    matcher = declaration[-1]
    return next((item for item in container
                if isinstance(item, dict) and all(key in item and item[key] == value
                                                  for key, value in matcher.items())), None)


def named_tests(finding):
    summary = finding.get("failingTestsSummary")
    names = finding.get("failingTests")
    if not isinstance(summary, str) or not isinstance(names, list):
        return ""
    return f"; {summary}" + (f": {', '.join(names)}" if names else "")


def diagnosis_text(finding):
    if not isinstance(finding, dict):
        return ""
    unavailable = finding.get("diagnosisUnavailable")
    path = finding.get("identitiesPath")
    if isinstance(unavailable, str):
        return f"; {unavailable}" + (f" at {path}" if isinstance(path, str) else "")
    parts = [value for value in (finding.get("failureCategory"), finding.get("errorMessage"))
            if isinstance(value, str)]
    if not parts:
        return ""
    return f"; {': '.join(parts)}" + (f" at {path}" if isinstance(path, str) else "")


def suite_verdict(root, stack, coverage_row):
    if coverage_row.get("memoryCapHit"):
        return result(stack, "test", "failed", memoryCapHit=True,
                      reason=f"test verdict derived from the coverage row: {coverage_row['reason']}")
    state = coverage_row["state"]
    if state == "interrupted":
        return result(stack, "test", "interrupted",
                      reason=f"suite verdict not established: the coverage row is {state}")
    if state not in ("passed", "failed"):
        return result(stack, "test", "invalid",
                      reason=f"suite verdict not established: the coverage row is {state}")
    declaration = stack["capabilities"]["coverage"]["suiteVerdict"]
    exit_path = declaration["exit"]
    artifact = next(item["path"] for item in coverage_row["artifacts"] if item["required"])
    unreadable = f'suite verdict not established: {artifact} does not record an integer at {"/".join(exit_path)}'
    try:
        report = json.loads((root / artifact).read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError):
        return result(stack, "test", "invalid", reason=unreadable)
    value = walk(report, exit_path)
    if type(value) is not int:
        return result(stack, "test", "invalid", reason=unreadable)
    if value == 0:
        return result(stack, "test", "passed", childExit=0,
                      reason=f"test verdict derived from the coverage row: suite exit 0 at {artifact}")
    finding = failure_finding(report, declaration["failure"])
    if finding is not None:
        return result(stack, "test", "failed", childExit=value,
                      reason=f"test verdict derived from the coverage row: suite exit {value} "
                             f"at {artifact}{named_tests(finding)}")
    errors = walk(report, [*declaration["failure"][:-2], "errors"])
    diagnosis = errors[0] if isinstance(errors, list) and errors else None
    return result(stack, "test", "invalid",
                  reason=f"suite verdict not established: the coverage row did not attribute child exit {value} "
                         f"to the suite{diagnosis_text(diagnosis)}")


def split_forwarded(args, operation):
    if "--" not in args:
        return args, []
    index = args.index("--")
    if operation != "test":
        raise ContractError("native arguments are only valid for test")
    return args[:index], args[index + 1:]


def request_options(args):
    stacks = since = None
    while args:
        flag = args.pop(0)
        if flag == "--stack" and args and stacks is None:
            stacks = args.pop(0).split(",")
        elif flag == "--since" and args and since is None:
            since = args.pop(0)
        else:
            raise ContractError("invalid operation syntax")
    return stacks, since


def request(args):
    if args == ["--force-run"]:
        return "force-run", CAPS[:3], None, None, []
    if not args: return None
    if args in (["--help"], ["-h"]): return "help"
    operation = args.pop(0)
    if operation == "capabilities" and not args: return "capabilities", (), None, None, []
    if operation not in {"test", "all", "gate"}: raise ContractError("invalid operation syntax")
    capability = "test" if operation != "gate" else None
    if operation == "gate":
        if not args or args[0] not in CAPS[1:]: raise ContractError("gate requires static, coverage or mutation")
        capability = args.pop(0)
    args, forwarded = split_forwarded(args, operation)
    stacks, since = request_options(args)
    if operation == "test" and (stacks is None or len(stacks) != 1 or not stacks[0]):
        raise ContractError("test requires exactly one --stack NAME")
    if capability == "mutation" and not since: raise ContractError("mutation requires --since BASE")
    if since is not None and capability != "mutation": raise ContractError("--since is only valid for mutation")
    return ("gate " + capability if operation == "gate" else operation), (capability,), since, stacks, forwarded


def candidate_identity(root):
    safe = ["git", "-c", f"safe.directory={root.as_posix()}"]
    git = subprocess.run([*safe, "rev-parse", "HEAD"], cwd=root, text=True, capture_output=True)
    dirty = subprocess.run([*safe, "status", "--porcelain"], cwd=root, text=True, capture_output=True)
    return {"commit": git.stdout.strip() if git.returncode == 0 else "unknown", "dirty": bool(dirty.stdout)}


def print_capabilities(stacks, root):
    exit_code = 0
    for stack in stacks:
        print(f"{stack['name']}:")
        for capability in CAPS:
            _, _, state, diagnostic = prepare_row(stack, capability, root, inspection=True)
            if state == "invalid":
                exit_code = 2
            reason = f": {diagnostic}" if diagnostic else ""
            print(f"  {capability}: {state}{reason}")
    return exit_code


def artifact_destination(root, value):
    destination = contained(root, value)
    if not destination:
        raise ContractError("artifactRoot must be repository-relative and contained")
    for entry in (destination, *destination.parents):
        if entry.exists() and not entry.is_dir():
            raise ContractError("artifactRoot requires a usable directory destination")
        if entry == root:
            break
    return destination


def prepare_result(destination):
    run = destination / f"run-{int(time.time() * 1000)}-{uuid.uuid4().hex[:8]}"
    run.mkdir(parents=True)
    with (run / "result.tmp").open("x", encoding="utf-8"):
        pass
    return run


def selected_stacks(all_stacks, names):
    available = [item["name"] for item in all_stacks]
    if names is not None and (not names or any(not name or name not in available for name in names)):
        raise ContractError("unknown selected stack")
    return [item for item in all_stacks if names is None or item["name"] in names]


def print_row(current):
    child = f" (child exit {current['childExit']})" if current["childExit"] is not None else ""
    reason = f": {current['reason']}" if current.get("reason") else ""
    print(f"{current['stack']} {current['capability']}: {current['state'].upper()}{child}{reason}", flush=True)


def memory_size(value):
    unit = next(unit for unit in ("T", "G", "M", "K", "") if value % MEMORY_UNITS[unit] == 0)
    return f"{value // MEMORY_UNITS[unit]}{unit}"


def memory_target():
    """The cap in bytes: the default, or a lower DYDO_MEMORY_MAX in systemd size syntax."""
    text = os.environ.get(MEMORY_MAX_ENV)
    if text is None:
        return MEMORY_MAX_DEFAULT
    match = re.fullmatch(r"(\d+(?:\.\d+)?)([KMGT]?)", text)
    limit = int(float(match[1]) * MEMORY_UNITS[match[2]]) if match else 0
    if limit <= 0:
        raise ContractError(f"{MEMORY_MAX_ENV} must be a positive systemd size such as 256M or 4G, not {text!r}")
    if limit > MEMORY_MAX_DEFAULT:
        raise ContractError(f"{MEMORY_MAX_ENV}={text} is above the {memory_size(MEMORY_MAX_DEFAULT)} default: "
                            "a cap hit is a finding; raising the cap is not allowed")
    return limit


def cgroup_number(directory, name):
    """A cgroup v2 byte count; None for `max` or an unreadable file."""
    try:
        return int((directory / name).read_text(encoding="ascii"))
    except (OSError, UnicodeError, ValueError):
        return None


def oom_kills(directory):
    try:
        fields = dict(line.split(" ", 1) for line in (directory / "memory.events").read_text(encoding="ascii").splitlines())
        return int(fields["oom_kill"])
    except (OSError, UnicodeError, ValueError, KeyError):
        return None


def cgroup_ancestry():
    """This process's cgroup v2 directory, then each ancestor below the root; empty without cgroup v2."""
    try:
        lines = SELF_CGROUP.read_text(encoding="utf-8").splitlines()
    except (OSError, UnicodeError):
        return []
    relative = next((line[3:].lstrip("/") for line in lines if line.startswith("0::/")), None)
    if relative is None:
        return []
    leaf = CGROUP_ROOT / relative
    return [leaf, *(parent for parent in leaf.parents if parent.is_relative_to(CGROUP_ROOT) and parent != CGROUP_ROOT)]


def caller_slice(ancestry):
    """Nearest slice below the user manager; system-manager slices cannot be joined with --user."""
    manager = next((index for index, directory in enumerate(ancestry)
                    if re.fullmatch(r"user@\d+\.service", directory.name)), 0)
    return next((directory.name for directory in ancestry[:manager] if directory.name.endswith(".slice")), "app.slice")


def oom_policy_continues(directory):
    """Whether the unit owning `directory` keeps its scope alive after an OOM kill; unknown is stop."""
    systemctl = shutil.which("systemctl")
    if systemctl is None:
        return False
    try:
        shown = subprocess.run([systemctl, "--user", "show", "-p", "OOMPolicy", "--value", directory.name],
                               stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=SYSTEMD_SECONDS)
    except (OSError, subprocess.TimeoutExpired):
        return False
    return shown.returncode == 0 and shown.stdout.strip() == "continue"


class JobBasicLimits(c.Structure):
    # Fixed-width Windows DWORDs also let the mocked API exercise the ABI on Unix.
    _fields_ = [("process_time", c.c_int64), ("job_time", c.c_int64),
                ("flags", c.c_uint32), ("min_ws", c.c_size_t), ("max_ws", c.c_size_t),
                ("active_limit", c.c_uint32), ("affinity", c.c_size_t),
                ("priority", c.c_uint32), ("scheduling", c.c_uint32)]


class JobLimits(c.Structure):
    _fields_ = [("basic", JobBasicLimits), ("io", c.c_uint64 * 6),
                ("process_memory", c.c_size_t), ("job_memory", c.c_size_t),
                ("peak_process", c.c_size_t), ("peak_job", c.c_size_t)]


class JobPort(c.Structure):
    _fields_ = [("key", c.c_void_p), ("port", c.c_void_p)]


def job_api():
    kernel = c.WinDLL("kernel32", use_last_error=True)
    handle, dword, boolean = c.c_void_p, c.c_uint32, c.c_int32
    specifications = {
        "GetCurrentProcess": ([], handle),
        "IsProcessInJob": ([handle, handle, c.POINTER(boolean)], boolean),
        "CreateJobObjectW": ([handle, c.c_wchar_p], handle),
        "SetInformationJobObject": ([handle, c.c_int, handle, dword], boolean),
        "QueryInformationJobObject": ([handle, c.c_int, handle, dword, handle], boolean),
        "AssignProcessToJobObject": ([handle, handle], boolean),
        "CreateIoCompletionPort": ([handle, handle, c.c_size_t, dword], handle),
        "GetQueuedCompletionStatus": ([handle, c.POINTER(dword), c.POINTER(c.c_size_t),
                                        c.POINTER(handle), dword], boolean),
        "CloseHandle": ([handle], boolean),
    }
    for name, (arguments, result_type) in specifications.items():
        function = getattr(kernel, name)
        function.argtypes, function.restype = arguments, result_type
    return kernel


def job_checked(value):
    if not value:
        raise c.WinError(c.get_last_error())
    return value


class WindowsMemoryJob:
    """Own the nested cap and its port for this process's lifetime; never kill job members."""

    def __init__(self, target):
        self.kernel = job_api()
        self.job = self.port = None
        self.hits = 0
        try:
            process = self.kernel.GetCurrentProcess()
            in_job = c.c_int32()
            job_checked(self.kernel.IsProcessInJob(process, None, c.byref(in_job)))
            outer = self.limits(None) if in_job.value else JobLimits()
            self.limit = min(target, outer.job_memory) if outer.basic.flags & 0x200 else target
            self.job = job_checked(self.kernel.CreateJobObjectW(None, None))
            self.port = job_checked(self.kernel.CreateIoCompletionPort(c.c_void_p(-1), None, 0, 1))
            association = JobPort(self.job, self.port)
            job_checked(self.kernel.SetInformationJobObject(self.job, 7, c.byref(association), c.sizeof(association)))
            limits = JobLimits()
            limits.basic.flags, limits.job_memory = 0x200, self.limit  # JOB_OBJECT_LIMIT_JOB_MEMORY only.
            job_checked(self.kernel.SetInformationJobObject(self.job, 9, c.byref(limits), c.sizeof(limits)))
            job_checked(self.kernel.AssignProcessToJobObject(self.job, process))
        except OSError:
            self.close()
            raise
        atexit.register(self.close)

    def close(self):
        for handle in (self.job, self.port):
            if handle:
                self.kernel.CloseHandle(handle)
        self.job = self.port = None

    def limits(self, job):
        limits = JobLimits()
        job_checked(self.kernel.QueryInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits), None))
        return limits

    def delivered_hits(self):
        message, key, overlapped = c.c_uint32(), c.c_size_t(), c.c_void_p()
        while self.kernel.GetQueuedCompletionStatus(self.port, c.byref(message), c.byref(key), c.byref(overlapped), 0):
            if message.value == 10 and key.value == self.job:  # JOB_OBJECT_MSG_JOB_MEMORY_LIMIT.
                self.hits += 1
        error = c.get_last_error()
        if error != 258:  # WAIT_TIMEOUT is the normal end of a nonblocking drain.
            raise c.WinError(error)
        return self.hits


class MemoryCap:
    """The operation's cap record and per-row evidence: Linux OOM kills or Windows messages."""

    def __init__(self, state, limit=None, mechanism=None, reason=None, cgroup=None, job=None):
        self.record = {"state": state, "limit": limit, "mechanism": mechanism, "reason": reason}
        reporting = {"linux": "exact", "win32": "best-effort"}.get(sys.platform)
        if reporting:
            self.record["memoryCapHitReporting"] = reporting
        self.cgroup = cgroup
        self.job = job

    def job_unavailable(self, error):
        if self.record["state"] != "unavailable":
            reason = f"windows job object: {error}"
            print(f"memory cap unavailable: {reason}; this operation continues", file=sys.stderr, flush=True)
            self.record.update(MemoryCap("unavailable", reason=reason).record)

    def kills(self):
        if self.job:
            try:
                return self.job.delivered_hits() if self.record["state"] != "unavailable" else None
            except OSError as error:
                self.job_unavailable(error)
                return None
        return oom_kills(self.cgroup) if self.cgroup else None

    def finding(self, before):
        """A positive cap-hit observation since `before`; missing Windows messages prove nothing."""
        after = self.kills()
        if before is None or after is None or after <= before:
            return None
        return f"memory cap hit (MemoryMax={memory_size(self.record['limit'])}): a finding; do not raise the cap"

    def annotated(self, row, source=None):
        """Carry cap metadata on executed, deferred and derived rows alike."""
        if "memoryCapHitReporting" in self.record:
            row = {**row, "memoryCapHitReporting": self.record["memoryCapHitReporting"]}
        if self.record.get("memoryCapHitReporting") == "best-effort":
            row = {**row, "peakJobMemory": (source or row).get("peakJobMemory")}
        return row

    def judged(self, row, before):
        """A reported hit wins over every row outcome. Peak memory is diagnostic only."""
        finding = self.finding(before)
        row = self.annotated(row)
        if self.job:
            try:
                row["peakJobMemory"] = self.job.limits(self.job.job).peak_job
            except OSError as error:
                self.job_unavailable(error)
        if finding is None:
            return row
        print(f"{row['stack']} {row['capability']}: {finding}", file=sys.stderr, flush=True)
        return {**row, "state": "failed", "resultExit": EXITS["failed"], "memoryCapHit": True, "reason": finding}


def uncapped(reason):
    print(f"memory cap unavailable: {reason}; this operation runs uncapped", file=sys.stderr, flush=True)
    return MemoryCap("unavailable", reason=reason)


def rerun_cap(ancestry):
    """The rerun under systemd-run confirms its own scope; it never wraps a second time."""
    leaf = ancestry[0] if ancestry else None
    limit = cgroup_number(leaf, "memory.max") if leaf else None
    if limit is None or cgroup_number(leaf, "memory.swap.max") != 0:
        return uncapped("systemd-run started this operation, but its cgroup shows no memory cap")
    return MemoryCap("enforced", limit, "systemd-run --user --scope",
                     f"MemoryMax={memory_size(limit)} MemorySwapMax=0 OOMPolicy=continue "
                     f"slice={caller_slice(ancestry)} on {leaf}", leaf)


def enforce(limit, ancestry):
    """Rerun this operation once, unchanged, inside a new capped scope; return only when that cannot start."""
    systemd_run = shutil.which("systemd-run")
    if systemd_run is None:
        return uncapped("systemd-run is not on PATH")
    slice_name = caller_slice(ancestry)
    slice_option = [f"--slice={slice_name}"] if slice_name != "app.slice" else []
    # OOMPolicy=continue keeps the facade alive to record a row's cap hit; expansion off keeps `$` literal.
    wrap = [systemd_run, "--user", "--scope", *slice_option, "--quiet", "-p", f"MemoryMax={limit}", "-p", "MemorySwapMax=0",
            "-p", "OOMPolicy=continue", "--expand-environment=no", "--"]
    try:
        probe = subprocess.run([*wrap, "true"], stdin=subprocess.DEVNULL, capture_output=True, text=True,
                               timeout=SYSTEMD_SECONDS)
    except (OSError, subprocess.TimeoutExpired) as error:
        return uncapped(f"systemd-run cannot start a capped scope: {error}")
    if probe.returncode != 0:
        detail = probe.stderr.strip().splitlines()[-1:] or ["no diagnostic"]
        return uncapped(f"systemd-run cannot start a capped scope (exit {probe.returncode}): {detail[0]}")
    sys.stdout.flush()
    sys.stderr.flush()
    os.environ[MEMORY_SCOPE_ENV] = "1"
    os.execv(systemd_run, [*wrap, sys.executable, *sys.orig_argv[1:]])


def memory_cap():
    """Cap this operation before it starts a child. Inside an outer scope that already caps it as
    tightly, with swap off and OOMPolicy=continue, the cap is inherited; otherwise the operation
    reruns under systemd-run in the caller's slice at the tighter of the target and any ancestor
    limit. Concurrent runs share that slice's budget. Windows always creates its own nested job.
    Unavailable caps are recorded and warned once."""
    names = {name.upper(): name for name in os.environ} if sys.platform == "win32" else {}
    for name, value in (("MSBUILDDISABLENODEREUSE", "1"), ("DOTNET_CLI_USE_MSBUILD_SERVER", "0"),
                        ("UseSharedCompilation", "false")):
        os.environ.setdefault(names.get(name.upper(), name), value)
    target = memory_target()
    rerun = os.environ.pop(MEMORY_SCOPE_ENV, None)
    if sys.platform == "win32":
        try:
            job = WindowsMemoryJob(target)
        except OSError as error:
            return uncapped(f"windows job object: {error}")
        return MemoryCap("enforced", job.limit, "windows job object",
                         f"JOB_OBJECT_LIMIT_JOB_MEMORY={memory_size(job.limit)} in the facade's nested job", job=job)
    if sys.platform != "linux":
        return uncapped(f"{sys.platform} has no systemd memory cap; each row keeps its {EXECUTION_SECONDS_MAXIMUM} s deadline")
    ancestry = cgroup_ancestry()
    if rerun is not None:
        return rerun_cap(ancestry)
    limits = [(limit, directory) for directory in ancestry
              if (limit := cgroup_number(directory, "memory.max")) is not None]
    tightest, capping = min(limits, key=lambda item: item[0], default=(target, None))
    if (capping and tightest <= target and cgroup_number(capping, "memory.swap.max") == 0
            and oom_policy_continues(capping)):
        return MemoryCap("inherited", tightest, "cgroup v2",
                         f"an outer scope caps this run with swap off and OOMPolicy=continue: {capping}", capping)
    return enforce(min(target, tightest), ancestry)


def capped_row(stack, capability, root, since, forwarded, cap):
    """Run one row, judged against the cap; also whether it was interrupted, which stops the run."""
    deadline = time.monotonic() + EXECUTION_SECONDS_MAXIMUM + CLEANUP_SECONDS
    before = cap.kills()
    current = run_row(stack, capability, root, since, forwarded, deadline)
    return cap.judged(current, before), current["state"] == "interrupted"


def execute_rows(selected, capabilities, root, since, forwarded, cap=None, rows=None):
    """Complete the selected row record, then dispatch unless interrupted.

    Publication recovery can call this with finished rows: it only fills omitted rows when a
    facade signal follows an adapter halt, and the interrupt prevents any further dispatch.
    """
    cap = cap or MemoryCap("unavailable")
    rows = [] if rows is None else rows
    with defer_interrupt():
        plan = []
        for stack in selected:
            deferred = deferral_applies(stack, capabilities, root)
            for capability in capabilities:
                plan.append((stack, capability, deferred))
                if len(rows) < len(plan):
                    reason = INTERRUPTED_BEFORE_COVERAGE if deferred and capability == "test" else "run interrupted before this row"
                    rows.append(cap.annotated(result(stack, capability, "interrupted", reason=reason)))
        execute_plan(plan, rows, root, since, forwarded, cap)
    return rows


def execute_plan(plan, rows, root, since, forwarded, cap):
    """Commit finished rows before reporting them; the enclosing deferral protects those commits."""
    halted = False
    deferred_index = None
    for index, (stack, capability, deferred) in enumerate(plan):
        if interrupted or halted:
            break
        if deferred and capability == "test":
            deferred_index = index
            continue
        current, halted = capped_row(stack, capability, root, since, forwarded, cap)
        rows[index] = current
        if deferred and capability == "coverage":
            rows[deferred_index] = cap.annotated(suite_verdict(root, stack, current), current)
            print_row(rows[deferred_index])
        print_row(current)
        if halted and not interrupted:
            # Adapter deadlines and cap findings retain their existing row/aggregate semantics.
            del rows[index + 1:]


def write_result(root, run, operation, selected, rows, candidate, memory=None):
    if not run.resolve().is_relative_to(root):
        raise ContractError("result artifact escapes repository")
    exit_code = 130 if interrupted else max((EXITS[row["state"]] for row in rows), default=0)
    payload = {"schema": 1, "candidate": candidate, "memoryCap": memory,
               "operation": operation, "selectedStacks": [x["name"] for x in selected], "results": rows, "aggregateExit": exit_code}
    path = run / "result.json"
    pending = run / "result.tmp"
    pending.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    pending.replace(path)
    print(f"Aggregate: {exit_code}")
    print(f"Result: {path}"); return exit_code


def run_operation(root, destination, selected, parsed, cap, candidate, rows):
    name, capabilities, since, _, forwarded = parsed
    operation = {"name": name, **({"since": since} if since else {})}
    run = None
    try:
        with defer_interrupt():
            run = prepare_result(destination)
            execute_rows(selected, capabilities, root, since, forwarded, cap, rows)
            if interrupted:
                execute_rows(selected, capabilities, root, since, forwarded, cap, rows)
        return write_result(root, run, operation, selected, rows, candidate, cap.record)
    except KeyboardInterrupt:
        if run is None:
            raise
        execute_rows(selected, capabilities, root, since, forwarded, cap, rows)
        return write_result(root, run, operation, selected, rows, candidate, cap.record)


def main(argv=None):
    if signal.getsignal(signal.SIGINT) is signal.default_int_handler:
        signal.signal(signal.SIGINT, interrupt)  # An inherited SIG_IGN or a caller's handler stays.
    args = list(sys.argv[1:] if argv is None else argv)
    path = Path(__file__).with_suffix(".json")
    # A sandbox's empty `.git` mount point names no repository: only a gitfile or a gitdir with HEAD does.
    root = next((parent for parent in Path(__file__).resolve().parents
                 if (parent / ".git").is_file() or (parent / ".git/HEAD").is_file()), path.parent.resolve())
    rows = []
    try:
        parsed = request(args)
        if parsed is None: print(HELP); return 2
        if parsed == "help": print(HELP); return 0
        name, _, _, names, _ = parsed
        data = read_manifest(path)
        destination = artifact_destination(root, data["artifactRoot"])
        selected = selected_stacks(data["stacks"], names)
        if name == "capabilities":
            return print_capabilities(selected, root)
        cap = memory_cap()
        candidate = candidate_identity(root)
        return run_operation(root, destination, selected, parsed, cap, candidate, rows)
    except (ContractError, OSError) as error:
        print(f"Invalid request or artifact destination: {error}", file=sys.stderr)
        return 130 if interrupted or any(row["state"] == "interrupted" for row in rows) else 2


if __name__ == "__main__":
    sys.exit(main())

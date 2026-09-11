#!/usr/bin/env python3
"""Run this project's declared tests and assurance gates without a shell."""
import hashlib
import json
import os
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


def interrupt(_signum, _frame):
    raise KeyboardInterrupt


if sys.platform == "win32":
    signal.signal(signal.SIGBREAK, interrupt)

CAPS = ("test", "static", "coverage", "mutation")
EXITS = {"passed": 0, "failed": 1, "unavailable": 2, "invalid": 2, "interrupted": 130}
EXECUTION_SECONDS_MAXIMUM = 1800
CLEANUP_SECONDS = 30
ROW_DEADLINE_ENV = "DYDO_ROW_DEADLINE"
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


def configured_command(config, root, capability):
    if "suiteVerdict" in config and capability != "coverage":
        return None, None, None, "invalid suite verdict declaration"
    if set(config) - {"suiteVerdict"} != {"state", "command", "artifacts"}:
        return None, None, None, "configured capability requires command and artifacts and forbids reason"
    command = config["command"]
    if not isinstance(command, dict) or set(command) != {"kind", "argv"} or command["kind"] not in ("argv", "current-python"):
        return None, None, None, "command kind must be argv or current-python"
    argv = command["argv"]
    if not isinstance(argv, list) or not argv or not all(isinstance(x, str) and x for x in argv):
        return None, None, None, "command argv must be a nonempty array of strings"
    if any("\x00" in item for item in argv):
        return None, None, None, "command argv cannot contain NUL"
    if any("<" in x or ">" in x for x in argv):
        return None, None, None, "command vector contains an angle placeholder"
    artifacts = config["artifacts"]
    if not isinstance(artifacts, list):
        return None, None, None, "artifacts must be an array"
    for item in artifacts:
        if not isinstance(item, dict) or set(item) != {"path", "required"} or not isinstance(item["required"], bool) or not contained(root, item["path"]):
            return None, None, None, "artifact requires contained path and required boolean"
    if capability != "test" and not any(item["required"] for item in artifacts):
        return None, None, None, "configured gates require a required artifact"
    if capability == "coverage" and "suiteVerdict" in config:
        error = suite_verdict_error(config["suiteVerdict"])
        if error:
            return None, None, None, error
        if sum(1 for item in artifacts if item["required"]) != 1:
            return None, None, None, "invalid suite verdict declaration"
    return command["kind"], argv, artifacts, None


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


def stop_child(child, deadline):
    try:
        if sys.platform == "win32":
            child.send_signal(signal.CTRL_BREAK_EVENT)
        else:
            os.killpg(child.pid, signal.SIGINT)
        child.wait(timeout=max(0, deadline - time.monotonic()))
    except (OSError, subprocess.TimeoutExpired):
        if sys.platform == "win32":
            child.kill()
        else:
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        child.wait()


def wait_for_child(child, capability, deadline):
    execution_deadline = deadline if capability != "test" else deadline - CLEANUP_SECONDS
    while child.poll() is None:
        remaining = execution_deadline - time.monotonic()
        if remaining <= 0:
            stop_child(child, deadline)
            return False
        try:
            child.wait(timeout=min(0.2, remaining))
        except subprocess.TimeoutExpired:
            pass
    return True


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
        child = subprocess.Popen(argv, executable=resolve_executable(argv[0], working_directory), cwd=working_directory, env={**os.environ, "PYTHON": sys.executable, **environment}, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                                 start_new_session=sys.platform != "win32")
        if not wait_for_child(child, capability, deadline):
            return result(stack, capability, "interrupted", argv=argv, environment=environment,
                          childExit=child.returncode, artifacts=artifacts,
                          reason="row deadline exceeded after adapter cleanup")
        error = artifact_error(root, capability, artifacts, before)
        if error:
            return result(stack, capability, "invalid", argv=argv, environment=environment,
                          childExit=child.returncode, artifacts=artifacts, reason=error)
        return completed_row(stack, capability, argv, environment, child, artifacts)
    except KeyboardInterrupt:
        if child is None:
            return result(stack, capability, "interrupted", argv=argv, environment=environment, artifacts=artifacts, reason="interrupted before adapter launch")
        stop_child(child, time.monotonic() + CLEANUP_SECONDS)
        return result(stack, capability, "interrupted", argv=argv, environment=environment, childExit=child.returncode, artifacts=artifacts, reason="adapter interrupted after cleanup window")
    except (OSError, ContractError) as error:
        return result(stack, capability, "invalid", argv=argv, environment=environment, artifacts=artifacts, childExit=child.returncode if child else None, reason=f"adapter or artifact error: {error}")


INTERRUPTED_BEFORE_COVERAGE = "suite verdict not established: the run was interrupted before the coverage row"


def deferral_applies(stack, capabilities, root):
    if "test" not in capabilities or "coverage" not in capabilities:
        return False
    config = stack.get("capabilities", {}).get("coverage")
    if not isinstance(config, dict) or "suiteVerdict" not in config:
        return False
    _, _, coverage_state, _ = prepare_row(stack, "coverage", root)
    if coverage_state != "configured":
        return False
    _, _, test_state, _ = prepare_row(stack, "test", root)
    return test_state == "configured"


def walk(value, keys):
    for key in keys:
        if not isinstance(value, dict) or key not in value:
            return None
        value = value[key]
    return value


def failure_matches(report, declaration):
    container = walk(report, declaration[:-1])
    if not isinstance(container, list):
        return False
    matcher = declaration[-1]
    return any(isinstance(item, dict) and all(key in item and item[key] == value for key, value in matcher.items())
               for item in container)


def suite_verdict(root, stack, coverage_row):
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
    if failure_matches(report, declaration["failure"]):
        return result(stack, "test", "failed", childExit=value,
                      reason=f"test verdict derived from the coverage row: suite exit {value} at {artifact}")
    return result(stack, "test", "invalid",
                  reason=f"suite verdict not established: the coverage row did not attribute child exit {value} to the suite")


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


def execute_rows(selected, capabilities, root, since, forwarded):
    rows = []
    for stack in selected:
        deferred = None
        for capability in capabilities:
            if capability == "test" and deferral_applies(stack, capabilities, root):
                deferred = len(rows)
                rows.append(result(stack, "test", "interrupted", reason=INTERRUPTED_BEFORE_COVERAGE))
                continue
            deadline = time.monotonic() + EXECUTION_SECONDS_MAXIMUM + CLEANUP_SECONDS
            current = run_row(stack, capability, root, since, forwarded, deadline)
            rows.append(current)
            print_row(current)
            if deferred is not None and capability == "coverage":
                derived = suite_verdict(root, stack, current)
                rows[deferred] = derived
                deferred = None
                print_row(derived)
            if current["state"] == "interrupted":
                break
        if deferred is not None:
            print_row(rows[deferred])
        if rows[-1]["state"] == "interrupted":
            break
    return rows


def write_result(root, run, operation, selected, rows, candidate):
    if not run.resolve().is_relative_to(root):
        raise ContractError("result artifact escapes repository")
    exit_code = max((EXITS[row["state"]] for row in rows), default=0)
    payload = {"schema": 1, "candidate": candidate,
               "operation": operation, "selectedStacks": [x["name"] for x in selected], "results": rows, "aggregateExit": exit_code}
    path = run / "result.json"
    pending = run / "result.tmp"
    pending.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    pending.replace(path)
    print(f"Aggregate: {exit_code}")
    print(f"Result: {path}"); return exit_code


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    path = Path(__file__).with_suffix(".json")
    root = next((parent for parent in Path(__file__).resolve().parents if (parent / ".git").exists()), path.parent)
    rows = []
    try:
        parsed = request(args)
        if parsed is None: print(HELP); return 2
        if parsed == "help": print(HELP); return 0
        name, capabilities, since, names, forwarded = parsed
        data = read_manifest(path)
        destination = artifact_destination(root, data["artifactRoot"])
        selected = selected_stacks(data["stacks"], names)
        if name == "capabilities":
            return print_capabilities(selected, root)
        candidate = candidate_identity(root)
        run = prepare_result(destination)
        rows = execute_rows(selected, capabilities, root, since, forwarded)
        return write_result(root, run, {"name": name, **({"since": since} if since else {})}, selected, rows, candidate)
    except (ContractError, OSError) as error:
        print(f"Invalid request or artifact destination: {error}", file=sys.stderr)
        return 130 if any(row["state"] == "interrupted" for row in rows) else 2


if __name__ == "__main__":
    sys.exit(main())

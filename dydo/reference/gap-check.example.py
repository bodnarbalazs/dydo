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
            "childExit": None, "resultExit": EXITS[state], "artifacts": []}
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


def command_error(capability, config, root, working_directory, since, inspection=False):
    if not isinstance(config, dict) or config.get("state") not in ("configured", "unavailable"):
        return None, None, "invalid capability state"
    if config["state"] == "unavailable":
        if valid_unavailable(config):
            return None, None, config["reason"]
        return None, None, "unavailable capability requires reason and forbids command and artifacts"
    if set(config) != {"state", "command", "artifacts"}:
        return None, None, "configured capability requires command and artifacts and forbids reason"
    command = config["command"]
    if not isinstance(command, dict) or set(command) != {"kind", "argv"} or command["kind"] not in ("argv", "current-python"):
        return None, None, "command kind must be argv or current-python"
    argv = command["argv"]
    if not isinstance(argv, list) or not argv or not all(isinstance(x, str) and x for x in argv):
        return None, None, "command argv must be a nonempty array of strings"
    if any("\x00" in item for item in argv):
        return None, None, "command argv cannot contain NUL"
    if any("<" in x or ">" in x for x in argv):
        return None, None, "command vector contains an angle placeholder"
    artifacts = config["artifacts"]
    if not isinstance(artifacts, list):
        return None, None, "artifacts must be an array"
    for item in artifacts:
        if not isinstance(item, dict) or set(item) != {"path", "required"} or not isinstance(item["required"], bool) or not contained(root, item["path"]):
            return None, None, "artifact requires contained path and required boolean"
    if capability != "test" and not any(item["required"] for item in artifacts):
        return None, None, "configured gates require a required artifact"
    if capability == "mutation":
        if argv.count("{base}") != 1 or any("{base}" in x and x != "{base}" for x in argv):
            return None, None, "mutation requires exactly one argv element equal to {base} and no substring occurrence"
        if not since and not inspection:
            return None, None, "mutation requires --since BASE"
        if since is not None:
            argv = [since if x == "{base}" else x for x in argv]
    elif any("{base}" in x for x in argv):
        return None, None, "{base} is only valid for mutation"
    actual = [sys.executable, *argv] if command["kind"] == "current-python" else list(argv)
    if command["kind"] == "argv" and not resolve_executable(actual[0], working_directory):
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


def run_row(stack, capability, root, since, forwarded):
    argv, artifacts, state, reason = prepare_row(stack, capability, root, since)
    if state != "configured":
        return result(stack, capability, state, reason=reason)
    if capability == "test":
        argv += forwarded
    child = None
    try:
        working_directory = contained(root, stack["cwd"])
        before = {item["path"]: artifact_snapshot(root, item["path"]) for item in artifacts
                  if item["required"] and capability != "test"}
        child = subprocess.Popen(argv, executable=resolve_executable(argv[0], working_directory), cwd=working_directory, env={**os.environ, "PYTHON": sys.executable}, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                                 start_new_session=sys.platform != "win32")
        while child.poll() is None:
            try:
                child.wait(timeout=0.2)
            except subprocess.TimeoutExpired:
                pass
        if child.returncode == 0:
            for path, previous in before.items():
                current = artifact_snapshot(root, path)
                if current is None or current == previous:
                    return result(stack, capability, "invalid", argv=argv, childExit=0, artifacts=artifacts, reason=f"required artifact was not produced or refreshed: {path}")
        return result(stack, capability, "passed" if child.returncode == 0 else "failed", argv=argv, childExit=child.returncode, artifacts=artifacts)
    except KeyboardInterrupt:
        if child is None:
            return result(stack, capability, "interrupted", argv=argv, artifacts=artifacts, reason="interrupted before adapter launch")
        try:
            if sys.platform == "win32":
                child.send_signal(signal.CTRL_BREAK_EVENT)
            else:
                os.killpg(child.pid, signal.SIGINT)
            child.wait(timeout=30)
        except (OSError, subprocess.TimeoutExpired):
            child.kill()
            child.wait()
        return result(stack, capability, "interrupted", argv=argv, childExit=child.returncode, artifacts=artifacts, reason="adapter interrupted after cleanup window")
    except (OSError, ContractError) as error:
        return result(stack, capability, "invalid", argv=argv, artifacts=artifacts, childExit=child.returncode if child else None, reason=f"adapter or artifact error: {error}")


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
    forwarded = []
    if "--" in args:
        index = args.index("--"); forwarded, args = args[index + 1:], args[:index]
        if operation != "test": raise ContractError("native arguments are only valid for test")
    stacks = since = None
    while args:
        flag = args.pop(0)
        if flag == "--stack" and args and stacks is None: stacks = args.pop(0).split(",")
        elif flag == "--since" and args and since is None: since = args.pop(0)
        else: raise ContractError("invalid operation syntax")
    if operation == "test" and (stacks is None or len(stacks) != 1 or not stacks[0]):
        raise ContractError("test requires exactly one --stack NAME")
    if capability == "mutation" and not since: raise ContractError("mutation requires --since BASE")
    if since is not None and capability != "mutation": raise ContractError("--since is only valid for mutation")
    return ("gate " + capability if operation == "gate" else operation), (capability,), since, stacks, forwarded


def candidate_identity(root):
    git = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, text=True, capture_output=True)
    dirty = subprocess.run(["git", "status", "--porcelain"], cwd=root, text=True, capture_output=True)
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
        all_stacks = data["stacks"]
        if names is not None and (not names or any(not x or x not in [item["name"] for item in all_stacks] for x in names)):
            raise ContractError("unknown selected stack")
        selected = [item for item in all_stacks if names is None or item["name"] in names]
        if name == "capabilities":
            return print_capabilities(selected, root)
        candidate = candidate_identity(root)
        run = prepare_result(destination)
        for stack in selected:
            for capability in capabilities:
                current = run_row(stack, capability, root, since, forwarded); rows.append(current)
                print(f"{current['stack']} {capability}: {current['state'].upper()}" + (f" (child exit {current['childExit']})" if current["childExit"] is not None else "") + (f": {current['reason']}" if current.get("reason") else ""), flush=True)
                if current["state"] == "interrupted":
                    break
            if rows[-1]["state"] == "interrupted":
                break
        return write_result(root, run, {"name": name, **({"since": since} if since else {})}, selected, rows, candidate)
    except (ContractError, OSError) as error:
        print(f"Invalid request or artifact destination: {error}", file=sys.stderr)
        return 130 if any(row["state"] == "interrupted" for row in rows) else 2


if __name__ == "__main__":
    sys.exit(main())

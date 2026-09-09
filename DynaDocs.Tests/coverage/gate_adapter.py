"""Private native stack adapter for the public gap-check facade."""
import argparse
import ast
import json
import os
import re
import signal
import sys
import time
import uuid
import hashlib
import subprocess
from pathlib import Path

from gate_run import checked_result


CLEANUP_SECONDS = 30
ROW_DEADLINE_ENV = "DYDO_ROW_DEADLINE"


class MeasurementTimeout(ValueError):
    pass


def run_coverage_command(argv, cwd):
    deadline_text = os.environ.get(ROW_DEADLINE_ENV)
    deadline = float(deadline_text) if deadline_text is not None else None
    timeout = None if deadline is None else max(0, deadline - time.monotonic() - CLEANUP_SECONDS)
    child = subprocess.Popen(argv, cwd=cwd,
                             creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                             start_new_session=sys.platform != "win32")
    try:
        return child.wait(timeout=timeout)
    except subprocess.TimeoutExpired as error:
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
        raise MeasurementTimeout("coverage campaign exceeded row deadline after owned cleanup") from error


def _publication_payload(run, stack, gate, report, candidate, inventory):
    report = checked_result(report)
    run, inventory = Path(run).resolve(), Path(inventory).resolve()
    inventory_relative = inventory.relative_to(run.parent.parent).as_posix()
    findings = report["findings"]
    gaps = report["errors"]
    return {"schema": 1, "candidate": candidate, "stack": stack, "gate": gate,
               "inventory": {"path": inventory_relative,
                              "sha256": hashlib.sha256(inventory.read_bytes()).hexdigest()},
               "tools": report["facts"].get("tools", {}),
               "commands": report["facts"].get("commands", []),
               "collectors": report["facts"].get("collectors", report["facts"]),
               "findings": findings, "gaps": gaps,
               "measurementComplete": not gaps,
               "exitCode": {"pass": 0, "fail": 1, "error": 2}[report["status"]]}


def _write_run_report(run, payload):
    encoded = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    (run / "report.json").write_text(encoded, encoding="utf-8")
    return encoded


def publish(summary, run, stack, gate, report, candidate, inventory):
    summary, run = Path(summary).resolve(), Path(run).resolve()
    payload = _publication_payload(run, stack, gate, report, candidate, inventory)
    encoded = _write_run_report(run, payload)
    summary.parent.mkdir(parents=True, exist_ok=True)
    lock = Path(str(summary) + ".lock")
    try:
        descriptor = os.open(lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL)
    except FileExistsError as error:
        raise ValueError(f"adapter publication lock already exists: {lock}") from error
    os.close(descriptor)
    temporary = summary.with_name(summary.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        temporary.write_text(encoded, encoding="utf-8")
        temporary.replace(summary)
    finally:
        temporary.unlink(missing_ok=True)
        lock.unlink()
    return payload["exitCode"]


def _candidate(root):
    safe = ["git", "-c", f"safe.directory={root.as_posix()}"]
    commit = subprocess.run([*safe, "rev-parse", "HEAD"], cwd=root, check=True,
                            text=True, capture_output=True).stdout.strip()
    dirty = bool(subprocess.run([*safe, "status", "--porcelain=v1", "-z"], cwd=root, check=True,
                                capture_output=True).stdout)
    from inventory import git_file_state, build_file_rows, source_fingerprint
    paths, deleted = git_file_state(root)
    files = build_file_rows(root, paths, deleted)
    return {"commit": commit, "dirty": dirty, "sourceFingerprint": source_fingerprint(files)}, files


def _inventory_artifact(root, run, candidate):
    from gate_collect import Collectors
    collector = Collectors(root, run / "inventory-commands")
    collector.discovery = _ordinary_discovery(root, collector.paths)
    project_result = collector.projects()
    inventory_result = collector.source_inventory()
    association_result = collector.associations()
    payload = collector.inventory or {"schema": 1, "files": [], "sources": [],
                                      "excluded": [], "projects": [], "errors": []}
    payload["candidate"] = candidate
    payload["errors"] = [*payload.get("errors", []), *project_result["errors"],
                         *inventory_result["errors"], *association_result["errors"]]
    inventory = run / "inventory.json"
    inventory.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return inventory, payload["errors"]


def _ordinary_discovery(root, paths):
    rows = []
    for relative in paths:
        path = root / relative
        if not path.is_file():
            continue
        if relative.endswith(".py") and Path(relative).name.startswith("test_"):
            try:
                tree = ast.parse(path.read_text(encoding="utf-8-sig"), relative)
            except (SyntaxError, OSError):
                continue
            native = any(isinstance(node, ast.ClassDef) and any(
                getattr(base, "attr", getattr(base, "id", "")) == "TestCase" for base in node.bases)
                         for node in ast.walk(tree))
            if native:
                rows.append({"id": relative + "#unittest", "file": relative})
        elif re.search(r"\.test\.(?:c|m)?js$", relative):
            text = path.read_text(encoding="utf-8-sig")
            if re.search(r"\btest\s*\(", text) and "node:test" in text:
                rows.append({"id": relative + "#node-test", "file": relative})
    return rows


def _aggregate(orchestration):
    findings, errors = [], []
    for name, row in orchestration["collectors"].items():
        findings.extend({"collector": name, **item} for item in row["findings"])
        errors.extend({"collector": name, **item} for item in row["errors"])
    return {"status": "error" if errors else ("fail" if findings else "pass"),
            "facts": orchestration, "findings": findings, "errors": errors}


def collect_static(root, output, stack):
    from gate_collect import Collectors
    from gate_run import collect_all
    from gate_versions import collect_versions
    collector = Collectors(root, output)
    collector.discovery = _ordinary_discovery(root, collector.paths)
    methods = {"projects": collector.projects, "source-inventory": collector.source_inventory,
               "associations": collector.associations}
    if stack == "dotnet":
        methods.update({"csharp-source": collector.csharp_source,
                        "csharp-analyzers": collector.csharp_analyzers,
                        "versions": lambda: collect_versions(collector)})
    elif stack == "python":
        methods.update({"python-source": collector.python_source,
                        "python-dead-code": collector.python_dead_code,
                        "python-dependencies": collector.python_dependencies})
    elif stack == "node":
        methods.update({"javascript-source": collector.javascript_source,
                        "javascript-dependencies": collector.javascript_dependencies,
                        "javascript-unused-exports": collector.javascript_unused_exports,
                        "clones": collector.clones})
    else:
        raise ValueError(f"Unknown stack: {stack}")
    return _aggregate(collect_all(methods, list(methods)))


def _target_paths(root, language):
    from inventory import git_file_state, language_of
    paths, deleted = git_file_state(root)
    if deleted:
        raise ValueError(f"Deleted maintained inputs prevent coverage: {sorted(deleted)}")
    tests = {row["file"] for row in _ordinary_discovery(root, paths)}
    return [relative for relative in paths
            if language_of(root / relative) == language and relative not in tests]


def collect_python_coverage(root, raw):
    from python_coverage import collect as native_collect
    from python_join import collect as join
    from gate_policy import evaluate_policy
    sources = _target_paths(root, "python")
    command = ["{python}", "-m", "unittest", "discover", "-s",
               "DynaDocs.Tests/coverage/tests", "-p", "test_*.py"]
    child = native_collect(root, raw, sources, command)
    if child != 0:
        return {"status": "fail", "facts": {"child_exit": child},
                "findings": [{"gate": "functional", "child_exit": child}], "errors": []}
    modules = join(root, raw, sources)
    findings = evaluate_policy(modules)
    return {"status": "fail" if findings else "pass",
            "facts": {"child_exit": child, "modules": modules},
            "findings": findings, "errors": []}


def collect_node_coverage(root, raw):
    from gate_policy import evaluate_policy
    from inventory import git_file_state, language_of
    paths, deleted = git_file_state(root)
    if deleted:
        raise ValueError("Deleted maintained inputs prevent JavaScript coverage")
    tests = {row["file"] for row in _ordinary_discovery(root, paths)}
    sources = [relative for relative in paths
               if language_of(root / relative) == "javascript" and relative not in tests]
    extensionless = [relative for relative in sources if not Path(relative).suffix]
    measurable = [relative for relative in sources if Path(relative).suffix]
    request = {"root": str(root), "output": str(raw), "targets": measurable,
               "command": ["{node}", str(root / "DynaDocs.Tests/coverage/node_tests.cjs")]}
    request_path = raw.parent / (raw.name + "-request.json")
    request_path.write_text(json.dumps(request, indent=2), encoding="utf-8")
    script = root / "DynaDocs.Tests/coverage/javascript_coverage.cjs"
    child = run_coverage_command(["node", str(script), "--root", str(root), "--output", str(raw),
                                  "--targets-json", json.dumps(measurable), "--command-json",
                                  json.dumps(request["command"])], root)
    if child not in (0, 1):
        return {"status": "error", "facts": {"child_exit": child}, "findings": [],
                "errors": [{"gate": "javascript-coverage", "message": "native c8 campaign failed"}]}
    joined = json.loads((raw / "joined.json").read_text(encoding="utf-8")) if child == 0 else {"modules": []}
    findings = ([{"gate": "functional", "child_exit": child}] if child else [])
    if child == 0:
        findings.extend(evaluate_policy(joined["modules"]))
    errors = [{"gate": "extensionless-javascript", "path": path,
               "message": "Native analyzer filename identity pending DYD-105"}
              for path in extensionless]
    return {"status": "error" if errors else ("fail" if findings else "pass"),
            "facts": {"child_exit": child, **joined}, "findings": findings, "errors": errors}


def collect_coverage(root, output, stack):
    raw = output / ("raw-" + uuid.uuid4().hex)
    if stack == "python":
        return collect_python_coverage(root, raw)
    if stack == "node":
        return collect_node_coverage(root, raw)
    if stack == "dotnet":
        run = root / "DynaDocs.Tests/coverage/run_tests.py"
        child = run_coverage_command([sys.executable, str(run), "--assurance-output", str(raw)], root)
        if child not in (0, 1):
            return {"status": "error", "facts": {"child_exit": child}, "findings": [],
                    "errors": [{"gate": "csharp-coverage", "message": "native campaign incomplete"}]}
        if child:
            return {"status": "fail", "facts": {"child_exit": child, "raw": str(raw)},
                    "findings": [{"gate": "functional", "child_exit": child}], "errors": []}
        joined = json.loads((raw / "raw/joined.json").read_text(encoding="utf-8"))
        findings = joined["findings"]
        return {"status": "fail" if findings else "pass",
                "facts": {"child_exit": child, "raw": str(raw), **joined},
                "findings": findings, "errors": []}
    raise ValueError(f"Unknown stack: {stack}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--gate", choices=("static", "coverage"), required=True)
    parser.add_argument("--stack", choices=("dotnet", "python", "node"), required=True)
    parser.add_argument("--root")
    parser.add_argument("--output")
    args = parser.parse_args()
    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parents[2]
    output = Path(args.output).resolve() if args.output else root / "DynaDocs.Tests/coverage/results"
    run = output / "assurance" / ("run-" + uuid.uuid4().hex)
    run.mkdir(parents=True, exist_ok=False)
    summary = output / "adapters" / f"{args.stack}-{args.gate}.json"
    candidate, _ = _candidate(root)
    inventory, inventory_errors = _inventory_artifact(root, run, candidate)
    try:
        local = root / "dydo/_system/.local/appdata"
        os.environ["APPDATA"] = str(local)
        os.environ.setdefault("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
        report = collect_static(root, run / "raw", args.stack) \
            if args.gate == "static" else collect_coverage(root, run, args.stack)
        if inventory_errors:
            report = {"status": "error", "facts": report.get("facts", {}),
                      "findings": report.get("findings", []),
                      "errors": [*report.get("errors", []), *inventory_errors]}
    except (ValueError, KeyError, OSError, TypeError) as error:
        report = {"status": "error", "facts": {}, "findings": [],
                  "errors": [{"type": type(error).__name__, "message": str(error)}]}
    try:
        return publish(summary, run, args.stack, args.gate, report, candidate, inventory)
    except (ValueError, KeyError, OSError, TypeError) as error:
        collision = {"status": "error", "facts": {}, "findings": [],
                     "errors": [{"type": type(error).__name__, "message": str(error)}]}
        _write_run_report(run, _publication_payload(run, args.stack, args.gate,
                                                    collision, candidate, inventory))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

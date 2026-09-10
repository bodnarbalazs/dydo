"""Private native stack adapter for the public gap-check facade."""
import argparse
import ast
import json
import os
import platform
import re
import signal
import sys
import time
import uuid
import hashlib
import subprocess
from pathlib import Path

from gate_run import checked_result, result


CLEANUP_SECONDS = 30
ROW_DEADLINE_ENV = "DYDO_ROW_DEADLINE"
STATIC_GATE_INTERPRETER = "dydo/_system/.local/static-gates/python/Scripts/python.exe"
SUPPLIED_ENVIRONMENT = ("APPDATA", "NUGET_PACKAGES", ROW_DEADLINE_ENV)
# A defective report is a broken measurement, never a policy outcome: ParseError is a
# SyntaxError, and an unexpected join shape raises Type/Attribute/IndexError before any finding.
REPORT_DEFECTS = (ValueError, KeyError, OSError, TypeError, AttributeError, IndexError,
                  SyntaxError)


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


def _sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _requirement_pins(text):
    return dict(re.findall(r"(?m)^([A-Za-z0-9_.-]+)==(\S+)$", text))


def _package_pins(text):
    return json.loads(text)["dependencies"]


def _locked_pins(text):
    return {name: row["resolved"]
            for name, row in json.loads(text)["dependencies"]["net10.0"].items()}


def _manifest_pins(text):
    return {name: row["version"] for name, row in json.loads(text)["tools"].items()}


def _pinned_lock(root, relative, extract):
    """One resolved pin set, read from the same lock gate_versions validates installs against."""
    path = Path(root) / relative
    try:
        return {"path": relative, "sha256": _sha256(path),
                "resolved": extract(path.read_text(encoding="utf-8-sig"))}
    except REPORT_DEFECTS as error:
        return {"path": relative, "resolved": None, "reason": f"{type(error).__name__}: {error}"}


def _interpreter_identity(path, identity=None):
    try:
        return {"path": identity or str(path), "sha256": _sha256(path)}
    except OSError as error:
        return {"path": identity or str(path), "sha256": None, "reason": str(error)}


def gate_tools(root):
    """Both interpreter identities and every resolved tool pin this gate measures through."""
    tools = "DynaDocs.Tests/coverage"
    return {"interpreters":
                {"caller": {**_interpreter_identity(sys.executable),
                            "version": platform.python_version()},
                 "dependencyBearing": _interpreter_identity(Path(root) / STATIC_GATE_INTERPRETER,
                                                            STATIC_GATE_INTERPRETER)},
            "pins": {name: _pinned_lock(root, relative, extract) for name, relative, extract in (
                ("python", f"{tools}/requirements.lock", _requirement_pins),
                ("javascript", f"{tools}/package.json", _package_pins),
                ("dotnet", f"{tools}/metrics/packages.lock.json", _locked_pins),
                ("altcover", ".config/dotnet-tools.json", _manifest_pins))}}


def _report_root(raw):
    """The run directory owning this raw tree; every published path is relative to it."""
    return Path(raw).parent


def _report_relative(value, run):
    path = Path(value)
    if not path.is_relative_to(run):
        return path.as_posix()
    return path.relative_to(run).as_posix()


def _raw_artifacts(paths, run):
    return [{"path": _report_relative(path, run), "sha256": _sha256(path)}
            for path in sorted(paths) if Path(path).is_file()]


def _logged_commands(rows, run):
    """Ordered native command evidence with report-relative stream paths."""
    return [{"name": row["name"], "argv": row["command"], "cwd": row["cwd"],
             "environment": row.get("environment", {}), "exit": row.get("exit_code"),
             "elapsedSeconds": row.get("duration_seconds"),
             "stdout": _report_relative(row["stdout"], run),
             "stdoutSha256": row.get("stdout_sha256"),
             "stderr": _report_relative(row["stderr"], run),
             "stderrSha256": row.get("stderr_sha256")} for row in rows]


def _supplied_environment():
    return {name: os.environ[name] for name in SUPPLIED_ENVIRONMENT if name in os.environ}


def _inherited_row(name, argv, cwd, exit_code, started):
    """One child whose streams the operator watches live; its own evidence hashes them."""
    return {"name": name, "argv": [str(item) for item in argv], "cwd": str(cwd),
            "environment": _supplied_environment(), "exit": exit_code,
            "elapsedSeconds": round(time.monotonic() - started, 6),
            "stdout": None, "stdoutSha256": None, "stderr": None, "stderrSha256": None,
            "streams": "inherited"}


def _campaign_commands(evidence, run):
    """The isolated C# campaign's own recorded rows, rebased on this report directory."""
    manifest = Path(evidence) / "commands.json"
    if not manifest.is_file():
        return []
    return [{**row, "stdout": _report_relative(Path(evidence) / row["stdout"], run),
             "stderr": _report_relative(Path(evidence) / row["stderr"], run)}
            for row in json.loads(manifest.read_text(encoding="utf-8"))]


def _coverage_report(name, facts, findings, errors, commands, artifacts):
    """One coverage collector, published in the row shape the static gate already uses."""
    row = {**result(facts, findings, errors), "artifacts": artifacts}
    return result({"commands": commands, "collectors": {name: row}}, findings, errors)


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
    return inventory, payload["errors"], _logged_commands(collector.log.rows, run)


def _unittest_discovery(path, relative):
    if not (relative.endswith(".py") and Path(relative).name.startswith("test_")):
        return None
    try:
        tree = ast.parse(path.read_text(encoding="utf-8-sig"), relative)
    except (SyntaxError, OSError):
        return None
    bases = (base for node in ast.walk(tree) if isinstance(node, ast.ClassDef) for base in node.bases)
    if any(getattr(base, "attr", getattr(base, "id", "")) == "TestCase" for base in bases):
        return {"id": relative + "#unittest", "file": relative}
    return None


def _node_discovery(path, relative):
    if not re.search(r"\.test\.(?:c|m)?js$", relative):
        return None
    text = path.read_text(encoding="utf-8-sig")
    if re.search(r"\btest\s*\(", text) and "node:test" in text:
        return {"id": relative + "#node-test", "file": relative}
    return None


def _ordinary_discovery(root, paths):
    rows = []
    for relative in paths:
        path = root / relative
        if path.is_file():
            row = _unittest_discovery(path, relative) or _node_discovery(path, relative)
            if row:
                rows.append(row)
    return rows


def _aggregate(orchestration):
    findings, errors = [], []
    for name, row in orchestration["collectors"].items():
        findings.extend({"collector": name, **item} for item in row["findings"])
        errors.extend({"collector": name, **item} for item in row["errors"])
    status = "pass"
    if findings:
        status = "fail"
    if errors:
        status = "error"
    return {"status": status,
            "facts": orchestration, "findings": findings, "errors": errors}


def _stack_methods(collector, stack):
    from gate_versions import collect_versions
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
    return methods


def _recorded(collector, name, method, artifacts):
    """Attribute every raw artifact a collector writes beside its own command log."""
    logs = collector.output / "commands"

    def collect():
        before = set(collector.output.rglob("*"))
        try:
            return method()
        finally:
            artifacts[name] = _raw_artifacts(
                [path for path in collector.output.rglob("*")
                 if path not in before and not path.is_relative_to(logs)],
                _report_root(collector.output))
    return collect


def collect_static(root, output, stack):
    from gate_collect import Collectors
    from gate_run import collect_all
    collector = Collectors(root, output)
    collector.discovery = _ordinary_discovery(root, collector.paths)
    artifacts = {}
    recorded = {name: _recorded(collector, name, method, artifacts)
                for name, method in _stack_methods(collector, stack).items()}
    report = _aggregate(collect_all(recorded, list(recorded)))
    for name, row in report["facts"]["collectors"].items():
        row["artifacts"] = artifacts.get(name, [])
    report["facts"]["commands"] = _logged_commands(collector.log.rows, _report_root(output))
    return report


def _target_paths(inventory, language):
    payload = json.loads(Path(inventory).read_text(encoding="utf-8"))
    return [row["path"] for row in payload["sources"]
            if row["language"] == language and row["role"] != "test"]


def _python_artifacts(raw, run):
    return _raw_artifacts([raw / name for name in
                           ("config.json", "coverage.json", "counters.json")], run)


def collect_python_coverage(root, raw, inventory):
    from python_coverage import collect as native_collect
    from python_join import collect as join
    from gate_policy import evaluate_policy
    run = _report_root(raw)
    sources = _target_paths(inventory, "python")
    argv = [sys.executable, "-m", "unittest", "discover", "-s",
            "DynaDocs.Tests/coverage/tests", "-p", "test_*.py"]
    started = time.monotonic()
    child = native_collect(root, raw, sources, argv)
    commands = [_inherited_row("python-coverage", argv, root, child, started)]
    facts = {"child_exit": child, "raw": _report_relative(raw, run)}
    if child != 0:
        return _coverage_report("python-coverage", facts,
                                [{"gate": "functional", "child_exit": child}], [],
                                commands, _python_artifacts(raw, run))
    modules = join(root, raw, sources)
    return _coverage_report("python-coverage", {**facts, "modules": modules},
                            evaluate_policy(modules), [], commands,
                            _python_artifacts(raw, run))


def collect_node_coverage(root, raw):
    from gate_policy import evaluate_policy
    from inventory import git_file_state, language_of
    run = _report_root(raw)
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
    argv = ["node", str(script), "--root", str(root), "--output", str(raw),
            "--targets-json", json.dumps(measurable), "--command-json",
            json.dumps(request["command"])]
    started = time.monotonic()
    child = run_coverage_command(argv, root)
    commands = [_inherited_row("javascript-coverage", argv, root, child, started)]
    facts = {"child_exit": child, "raw": _report_relative(raw, run)}
    if child not in (0, 1):
        return _coverage_report(
            "javascript-coverage", facts, [],
            [{"gate": "javascript-coverage", "message": "native c8 campaign failed"}],
            commands, _raw_artifacts([request_path, raw / "joined.json"], run))
    joined = {"modules": []}
    findings = []
    if child == 0:
        joined = json.loads((raw / "joined.json").read_text(encoding="utf-8"))
        findings.extend(evaluate_policy(joined["modules"]))
    else:
        findings.append({"gate": "functional", "child_exit": child})
    errors = [{"gate": "extensionless-javascript", "path": path,
               "message": "Native analyzer filename identity pending DYD-105"}
              for path in extensionless]
    return _coverage_report("javascript-coverage", {**facts, **joined}, findings, errors,
                            commands, _raw_artifacts([request_path, raw / "joined.json"], run))


def collect_dotnet_coverage(root, raw):
    run, evidence = _report_root(raw), raw / "raw"
    script = root / "DynaDocs.Tests/coverage/run_tests.py"
    argv = [sys.executable, str(script), "--assurance-output", str(raw)]
    started = time.monotonic()
    child = run_coverage_command(argv, root)
    commands = [_inherited_row("csharp-campaign", argv, root, child, started),
                *_campaign_commands(evidence, run)]
    artifacts = _raw_artifacts([evidence / "joined.json", evidence / "commands.json",
                                evidence / "coverage.opencover.xml", raw / "identities.json"], run)
    facts = {"child_exit": child, "raw": _report_relative(raw, run)}
    if child not in (0, 1):
        return _coverage_report(
            "csharp-coverage", facts, [],
            [{"gate": "csharp-coverage", "message": "native campaign incomplete"}],
            commands, artifacts)
    if child:
        return _coverage_report("csharp-coverage", facts,
                                [{"gate": "functional", "child_exit": child}], [],
                                commands, artifacts)
    joined = json.loads((evidence / "joined.json").read_text(encoding="utf-8"))
    return _coverage_report("csharp-coverage", {**facts, **joined}, joined["findings"], [],
                            commands, artifacts)


def collect_coverage(root, output, stack, inventory):
    raw = output / ("raw-" + uuid.uuid4().hex)
    if stack == "python":
        return collect_python_coverage(root, raw, inventory)
    if stack == "node":
        return collect_node_coverage(root, raw)
    if stack == "dotnet":
        return collect_dotnet_coverage(root, raw)
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
    local = root / "dydo/_system/.local/appdata"
    os.environ["APPDATA"] = str(local)
    os.environ.setdefault("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
    candidate, _ = _candidate(root)
    inventory, inventory_errors, inventory_commands = _inventory_artifact(root, run, candidate)
    tools = gate_tools(root)
    try:
        report = collect_static(root, run / "raw", args.stack) \
            if args.gate == "static" else collect_coverage(root, run, args.stack, inventory)
        if inventory_errors:
            report = {"status": "error", "facts": report.get("facts", {}),
                      "findings": report.get("findings", []),
                      "errors": [*report.get("errors", []), *inventory_errors]}
    except REPORT_DEFECTS as error:
        report = {"status": "error", "facts": {}, "findings": [],
                  "errors": [{"type": type(error).__name__, "message": str(error)}]}
    report["facts"] = {**report["facts"], "tools": tools,
                       "commands": [*inventory_commands, *report["facts"].get("commands", [])]}
    try:
        return publish(summary, run, args.stack, args.gate, report, candidate, inventory)
    except REPORT_DEFECTS as error:
        collision = {"status": "error", "facts": {"tools": tools, "commands": inventory_commands},
                     "findings": [],
                     "errors": [{"type": type(error).__name__, "message": str(error)}]}
        _write_run_report(run, _publication_payload(run, args.stack, args.gate,
                                                    collision, candidate, inventory))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

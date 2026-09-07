"""Private native stack adapter for the public gap-check facade."""
import argparse
import ast
import json
import os
import re
import sys
import uuid
from pathlib import Path

from gate_run import checked_result


def publish(output, stack, gate, report):
    report = checked_result(report)
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    run_name = "run-" + uuid.uuid4().hex
    run = output / run_name
    run.mkdir()
    payload = {"schema": 1, "stack": stack, "gate": gate,
               "run": run_name, "result": report}
    encoded = json.dumps(payload, indent=2, sort_keys=True) + "\n"
    (run / "report.json").write_text(encoded, encoding="utf-8")
    temporary = output / ("latest-" + uuid.uuid4().hex + ".tmp")
    temporary.write_text(encoded, encoding="utf-8")
    temporary.replace(output / "latest.json")
    return {"pass": 0, "fail": 1, "error": 2}[report["status"]]


def _ordinary_discovery(root, paths):
    rows = []
    for relative in paths:
        path = root / relative
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
    methods = {"projects": collector.projects, "source-inventory": collector.source_inventory}
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
    import subprocess
    child = subprocess.run(["node", str(script), "--root", str(root), "--output", str(raw),
                            "--targets-json", json.dumps(measurable), "--command-json",
                            json.dumps(request["command"])], cwd=root).returncode
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
        import subprocess
        run = root / "DynaDocs.Tests/coverage/run_tests.py"
        child = subprocess.run([sys.executable, str(run), "--assurance-output", str(raw)], cwd=root).returncode
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
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root, output = Path(args.root).resolve(), Path(args.output).resolve()
    try:
        local = root / "dydo/_system/.local/appdata"
        os.environ["APPDATA"] = str(local)
        os.environ.setdefault("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
        report = collect_static(root, output / ("commands-" + uuid.uuid4().hex), args.stack) \
            if args.gate == "static" else collect_coverage(root, output, args.stack)
        return publish(output, args.stack, args.gate, report)
    except (ValueError, KeyError, OSError, TypeError) as error:
        return publish(output, args.stack, args.gate,
                       {"status": "error", "facts": {}, "findings": [],
                        "errors": [{"type": type(error).__name__, "message": str(error)}]})


if __name__ == "__main__":
    raise SystemExit(main())

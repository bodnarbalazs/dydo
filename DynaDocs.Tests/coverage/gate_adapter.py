"""Private native stack adapter for the public gap-check facade."""
import argparse
import ast
import json
import os
import platform
import re
import shlex
import signal
import sys
import time
import uuid
import hashlib
import subprocess
from pathlib import Path

from gate_run import STATIC_GATE_INTERPRETER, checked_result, result


CLEANUP_SECONDS = 30
ROW_DEADLINE_ENV = "DYDO_ROW_DEADLINE"
SUBJECT_NAME_CAP = 10
STATECHART_PACKAGE = "skills/engineering/statechart/scripts"
_DOTNET_FAILURE = re.compile(r"(?m)^\s*Failed\s+(?P<name>\S.*?)\s+\[[^\]\r\n]*\]\s*$")
_UNITTEST_FAILURE = re.compile(r"(?m)^(?:FAIL|ERROR):\s+(?P<name>\S+)(?:\s+\((?P<case>[^)\r\n]+)\))?")
SUPPLIED_ENVIRONMENT = ("APPDATA", "NUGET_PACKAGES", ROW_DEADLINE_ENV)
# A defective report is a broken measurement, never a policy outcome: ParseError is a
# SyntaxError, and an unexpected join shape raises Type/Attribute/IndexError before any finding.
REPORT_DEFECTS = (ValueError, KeyError, OSError, TypeError, AttributeError, IndexError,
                  SyntaxError, subprocess.SubprocessError)


class MeasurementTimeout(ValueError):
    pass


def run_coverage_command(argv, cwd):
    from run_tests import contain_descendants, defer_interruption
    deadline_text = os.environ.get(ROW_DEADLINE_ENV)
    deadline = float(deadline_text) if deadline_text is not None else None
    execution_deadline = None if deadline is None else deadline - CLEANUP_SECONDS
    containment = contain_descendants()
    child = subprocess.Popen(argv, cwd=cwd,
                             creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                             start_new_session=sys.platform != "win32")
    try:
        status = _wait_coverage_child(child, execution_deadline)
    except (subprocess.TimeoutExpired, KeyboardInterrupt) as error:
        # Leave time inside the facade's cleanup window for forced descendant cleanup.
        remaining = CLEANUP_SECONDS if deadline is None else max(0, deadline - time.monotonic())
        cleanup_confirmed = False
        try:
            with defer_interruption():
                cleanup_deadline = time.monotonic() + min(remaining, CLEANUP_SECONDS)
                stopped = _stop_coverage_campaign(child, cleanup_deadline, containment)
                if stopped == 2:
                    raise MeasurementTimeout("coverage owner could not confirm interrupted cleanup")
                cleanup_confirmed = True
        except (OSError, subprocess.TimeoutExpired) as cleanup_error:
            raise MeasurementTimeout("coverage cleanup could not be confirmed") from cleanup_error
        except KeyboardInterrupt:
            if not cleanup_confirmed:
                raise MeasurementTimeout("coverage cleanup was interrupted before confirmation") from None
            raise SystemExit(130) from None
        if isinstance(error, KeyboardInterrupt):
            raise SystemExit(130) from None
        raise MeasurementTimeout("coverage campaign exceeded row deadline after owned cleanup") from error
    if status == 130:
        raise SystemExit(130)
    return status


def _wait_coverage_child(child, deadline):
    # Windows' native wait does not dispatch Python SIGBREAK handlers until it returns.
    while True:
        remaining = .2 if deadline is None else max(0, deadline - time.monotonic())
        try:
            return child.wait(timeout=min(.2, remaining))
        except subprocess.TimeoutExpired:
            _reap_orphans(child.pid)
            if deadline is not None and remaining <= .2:
                raise


def _reap_orphans(campaign):
    """As its descendants' subreaper on Linux, this process collects each orphan that has exited, as
    init would, so a test watching a killed process group disappear sees it go. Popen keeps the
    campaign's own status."""
    if sys.platform != "linux":
        return
    while True:
        try:
            exited = os.waitid(os.P_ALL, 0, os.WEXITED | os.WNOHANG | os.WNOWAIT)
        except ChildProcessError:
            return
        if exited is None or exited.si_pid == campaign:
            return
        os.waitpid(exited.si_pid, 0)


def _stop_coverage_campaign(child, deadline, containment):
    from run_tests import end_descendants
    try:
        if sys.platform == "win32":
            child.send_signal(signal.CTRL_BREAK_EVENT)
        else:
            os.killpg(child.pid, signal.SIGINT)
        _wait_coverage_child(child, time.monotonic() + max(0, deadline - time.monotonic()) / 3)
    except (OSError, subprocess.TimeoutExpired):
        if sys.platform == "win32":
            child.kill()
        else:
            try:
                os.killpg(child.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        _wait_coverage_child(child, min(deadline, time.monotonic() + 5))
    finally:
        # A killed Linux owner can leave detached children adopted by this adapter.
        end_descendants(containment, seconds=max(0, deadline - time.monotonic()))
    return child.returncode


def _sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _requirement_pins(text):
    return dict(re.findall(r"(?m)^([A-Za-z0-9_.-]+)==(\S+)$", text))


def _package_pins(text):
    return json.loads(text)["dependencies"]


def _locked_pins(text):
    return {name: row["resolved"]
            for name, row in json.loads(text)["dependencies"]["net10.0"].items()}


def _pnpm_pins(text):
    """A pnpm package's own importer's resolved versions, read from its lock without a YAML parser."""
    importer = text.split("\nimporters:\n", 1)[1].split("\npackages:\n", 1)[0]
    return dict(re.findall(r"(?m)^      '?([^'\s]+?)'?:\n        specifier: \S+\n        version: ([^\s(]+)",
                           importer))


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
                ("altcover", ".config/dotnet-tools.json", _manifest_pins),
                ("viewer", "viewer/pnpm-lock.yaml", _pnpm_pins),
                ("statechart", f"{STATECHART_PACKAGE}/pnpm-lock.yaml", _pnpm_pins))}}


def _report_root(raw):
    """The run directory owning this raw tree; every published path is relative to it."""
    return Path(raw).parent


def _report_relative(value, run):
    path_text, run_text = str(value), str(run)
    if os.name == "nt":
        path_text = path_text.removeprefix("\\\\?\\")
        run_text = run_text.removeprefix("\\\\?\\")
    path = Path(path_text).resolve(strict=True)
    run = Path(run_text).resolve(strict=True)
    if not path.is_relative_to(run):
        return path.as_posix()
    return path.relative_to(run).as_posix()


def _report_location(value, run):
    """Project an output location whose existing parent supplies its identity."""
    path = Path(value)
    parent = _report_relative(path.parent, run)
    return (Path(parent) / path.name).as_posix()


def _raw_artifacts(paths, run):
    return [{"path": _report_relative(path, run), "sha256": _sha256(path)}
            for path in sorted(paths) if Path(path).is_file()]


def _logged_commands(rows, run):
    """Ordered native command evidence with report-relative stream paths."""
    commands = []
    for row in rows:
        project_stream = _report_location if row["exit_code"] is None else _report_relative
        commands.append({"name": row["name"], "argv": row["command"], "cwd": row["cwd"],
                         "environment": row.get("environment", {}), "exit": row["exit_code"],
                         "elapsedSeconds": row.get("duration_seconds"),
                         "stdout": project_stream(row["stdout"], run),
                         "stdoutSha256": row.get("stdout_sha256"),
                         "stderr": project_stream(row["stderr"], run),
                         "stderrSha256": row.get("stderr_sha256")})
    return commands


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
    commands = []
    for row in json.loads(manifest.read_text(encoding="utf-8")):
        stdout, stderr = Path(evidence) / row["stdout"], Path(evidence) / row["stderr"]
        commands.append({**row, "stdout": _report_relative(stdout, run),
                         "stdoutSha256": _sha256(stdout),
                         "stderr": _report_relative(stderr, run),
                         "stderrSha256": _sha256(stderr)})
    return commands


def _failing_subjects(evidence):
    """Names for the dotnet campaign's failed subjects, honest about what it could not read."""
    sources = (("altcover-runner.stdout", _DOTNET_FAILURE),
               ("altcover-runner.stderr", _UNITTEST_FAILURE))
    names, unavailable = [], []
    for filename, pattern in sources:
        try:
            text = (Path(evidence) / filename).read_text(encoding="utf-8", errors="replace")
        except OSError:
            unavailable.append(filename)
            continue
        names.extend(match.groupdict().get("case") or match.group("name")
                    for match in pattern.finditer(text))
    names = list(dict.fromkeys(names))
    total = len(names)
    if total:
        summary = (f"{total} failing tests" if total <= SUBJECT_NAME_CAP
                  else f"{total} failing tests (first {SUBJECT_NAME_CAP} named)")
        return names[:SUBJECT_NAME_CAP], summary
    if unavailable:
        return [], f"campaign subject streams unavailable: {', '.join(unavailable)}"
    return [], "no failing test named in altcover-runner.stdout or altcover-runner.stderr"


def _campaign_diagnosis(raw):
    """windows_job's diagnosis for an incomplete native campaign, honest about what it could not read."""
    path = Path(raw) / "identities.json"
    try:
        manifest = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"identitiesPath": str(path), "diagnosisUnavailable": "identities.json is missing or unreadable"}
    campaign = manifest.get("campaign") if isinstance(manifest, dict) else None
    if not isinstance(campaign, dict):
        return {"identitiesPath": str(path), "diagnosisUnavailable": "identities.json does not record a campaign"}
    category, message = campaign.get("failure_category"), campaign.get("error_message")
    if not isinstance(category, str) and not isinstance(message, str):
        return {"identitiesPath": str(path),
               "diagnosisUnavailable": "identities.json campaign names no failure_category or error_message"}
    diagnosis = {"identitiesPath": str(path)}
    if isinstance(category, str):
        diagnosis["failureCategory"] = category
    if isinstance(message, str):
        diagnosis["errorMessage"] = message
    return diagnosis


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
    inventory_fingerprint = payload.get("candidate", {}).get("sourceFingerprint")
    outer_fingerprint = candidate["sourceFingerprint"]
    fingerprint_errors = [] if inventory_fingerprint == outer_fingerprint else [{
        "message": "Outer and inventory source fingerprints differ",
        "outerSourceFingerprint": outer_fingerprint,
        "inventorySourceFingerprint": inventory_fingerprint}]
    payload["candidate"] = {**candidate, "sourceFingerprint": inventory_fingerprint}
    payload["errors"] = [*payload.get("errors", []), *project_result["errors"],
                         *inventory_result["errors"], *association_result["errors"],
                         *fingerprint_errors]
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
    if not re.search(r"\.test\.(?:(?:c|m)?js|ts)$", relative):
        return None
    text = path.read_text(encoding="utf-8-sig")
    if re.search(r"\btest\s*\(", text) and "node:test" in text:
        return {"id": relative + "#node-test", "file": relative}
    return None


def _vitest_discovery(path, relative):
    if not (relative.startswith("viewer/src/") and re.search(r"\.test\.tsx?$", relative)):
        return None
    text = path.read_text(encoding="utf-8-sig")
    if re.search(r"\b(?:test|it)\s*\(", text) and re.search(r"""\bfrom\s+['"]vitest['"]""", text):
        return {"id": relative + "#vitest", "file": relative}
    return None


def _ordinary_discovery(root, paths):
    rows = []
    for relative in paths:
        path = root / relative
        if path.is_file():
            row = (_unittest_discovery(path, relative) or _node_discovery(path, relative)
                   or _vitest_discovery(path, relative))
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
    elif stack == "viewer":
        methods.update({"typescript-build": collector.viewer_typecheck,
                        "typescript-lint": collector.viewer_lint,
                        "typescript-source": collector.typescript_source,
                        "typescript-dependencies": collector.typescript_dependencies,
                        "typescript-unused-exports": collector.typescript_unused_exports,
                        "clones": collector.typescript_clones})
    elif stack == "statechart":
        methods.update({"typescript-source": collector.typescript_source,
                        "typescript-dependencies": collector.typescript_dependencies,
                        "typescript-unused-exports": collector.typescript_unused_exports,
                        "clones": collector.typescript_clones})
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


def collect_static(root, output, stack, initial_fingerprint=None):
    from gate_collect import Collectors
    from gate_run import collect_all
    collector = Collectors(root, output)
    collector.stack = stack
    collector.discovery = _ordinary_discovery(root, collector.paths)
    artifacts = {}
    methods = _stack_methods(collector, stack)
    methods["repository-inputs"] = lambda: collector.repository_inputs(initial_fingerprint)
    recorded = {name: _recorded(collector, name, method, artifacts)
                for name, method in methods.items()}
    report = _aggregate(collect_all(recorded, list(recorded)))
    for name, row in report["facts"]["collectors"].items():
        row["artifacts"] = artifacts.get(name, [])
    report["facts"]["commands"] = _logged_commands(collector.log.rows, _report_root(output))
    return report


def _target_paths(inventory, language, stack=None):
    from gate_inventory import typescript_stack
    payload = json.loads(Path(inventory).read_text(encoding="utf-8"))
    return [row["path"] for row in payload["sources"]
            if row["language"] == language and row["role"] != "test"
            and not row.get("coverageExemption")
            and stack in (None, typescript_stack(row["path"]))]


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
    facts = {"child_exit": child, "raw": _report_location(raw, run)}
    if child != 0:
        return _coverage_report("python-coverage", facts,
                                [{"gate": "functional", "child_exit": child}], [],
                                commands, _python_artifacts(raw, run))
    modules = join(root, raw, sources)
    return _coverage_report("python-coverage", {**facts, "modules": modules},
                            evaluate_policy(modules), [], commands,
                            _python_artifacts(raw, run))


def collect_node_coverage(root, raw, inventory, initial_fingerprint=None):
    from gate_policy import evaluate_policy
    from gate_collect import repository_inputs
    from inventory import build_file_rows, git_file_state, source_fingerprint
    run = _report_root(raw)
    paths, deleted = git_file_state(root)
    initial_fingerprint = initial_fingerprint or source_fingerprint(
        build_file_rows(root, paths, deleted))
    if deleted:
        raise ValueError("Deleted maintained inputs prevent JavaScript coverage")
    sources = _target_paths(inventory, "javascript")
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
    integrity = repository_inputs(root, initial_fingerprint)
    facts = {"child_exit": child, "raw": _report_location(raw, run),
             "repositoryInputs": integrity["facts"]}
    if child not in (0, 1):
        return _coverage_report(
            "javascript-coverage", facts, [],
            [{"gate": "javascript-coverage", "message": "native c8 campaign failed"},
             *integrity["errors"]],
            commands, _raw_artifacts([request_path, raw / "joined.json"], run))
    if integrity["errors"]:
        findings = [{"gate": "functional", "child_exit": child}] if child else []
        return _coverage_report("javascript-coverage", facts, findings, integrity["errors"],
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


def _vitest_suite(report):
    """The suite verdict vitest itself recorded, apart from its own coverage thresholds."""
    try:
        payload = json.loads(Path(report).read_text(encoding="utf-8"))
        failed = payload["numFailedTests"] + payload["numFailedTestSuites"]
        names = [test["fullName"] for suite in payload["testResults"]
                 for test in suite["assertionResults"] if test["status"] == "failed"]
        ran = payload["numTotalTests"] > 0
    except (OSError, ValueError, KeyError, TypeError):
        return None, []
    return (1 if failed or not ran else 0), names


def _walked_targets(root, raw, inventory, targets):
    """The static row's own walker, run over the coverage targets."""
    from gate_collect import Collectors
    collector = Collectors(root, raw / "walker")
    payload = json.loads(Path(inventory).read_text(encoding="utf-8"))
    collector.inventory = {**payload, "sources": [row for row in payload["sources"] if row["path"] in targets]}
    walk = collector.typescript_source()
    return {row["path"]: row for row in walk["facts"].get("modules", [])}, walk["errors"], collector.log.rows


def _node_test_suite(report):
    """The suite verdict node's TAP reporter recorded: a failed or cancelled test, or none, fails."""
    try:
        text = Path(report).read_text(encoding="utf-8")
    except OSError:
        return None, [], None
    counts = dict(re.findall(r"(?m)^# (tests|fail|cancelled|skipped) (\d+)$", text))
    if set(counts) != {"tests", "fail", "cancelled", "skipped"}:
        return None, [], None
    names = re.findall(r"(?m)^\s*not ok \d+ - (.*?)(?: # (?:SKIP|TODO)\b.*)?$", text)
    failed = int(counts["fail"]) + int(counts["cancelled"])
    return (1 if failed or not int(counts["tests"]) else 0), names, int(counts["skipped"])


def _typescript_coverage(root, raw, inventory, campaign):
    """One TypeScript stack's recorded suite verdict, then its targets' LCOV joined to the walker."""
    from gate_policy import evaluate_policy
    from typescript_join import join
    runner, facts = campaign["runner"], campaign["facts"]
    commands, artifacts = campaign["commands"], campaign["artifacts"]
    if facts["suite_exit"] is None:
        return _coverage_report("typescript-coverage", facts, [], [
            {"gate": "typescript-coverage", "message": f"{runner} recorded no readable suite result"}],
            commands, artifacts)
    if facts["suite_exit"]:
        failing = campaign["failing"]
        summary = f"{len(failing)} failing tests" if failing else "failed without a named test"
        return _coverage_report("typescript-coverage", facts, [
            {"gate": "functional", "child_exit": facts["suite_exit"], "failingTests": failing[:SUBJECT_NAME_CAP],
             "failingTestsSummary": summary}], [], commands, artifacts)
    targets = _target_paths(inventory, "typescript", campaign["stack"])
    walked, errors, rows = _walked_targets(root, raw, inventory, set(targets))
    commands.extend(_logged_commands(rows, _report_root(raw)))
    if errors:
        return _coverage_report("typescript-coverage", facts, [], errors, commands, artifacts)
    modules = join(campaign["lcov"], root, campaign["base"], targets, walked)
    findings = evaluate_policy(modules)
    if facts["child_exit"] and not findings:
        errors.append({"gate": "typescript-coverage", "child_exit": facts["child_exit"],
                       "message": f"{runner} failed for a reason the gate did not measure"})
    return _coverage_report("typescript-coverage", {**facts, "modules": modules}, findings, errors,
                            commands, artifacts)


def collect_viewer_coverage(root, raw, inventory):
    from gate_collect import viewer_script
    run = _report_root(raw)
    raw.mkdir(parents=True)
    report, lcov = raw / "vitest.json", raw / "coverage" / "lcov.info"
    argv = viewer_script("coverage", "--coverage.reporter=lcov",
                         f"--coverage.reportsDirectory={raw / 'coverage'}",
                         "--reporter=default", "--reporter=json", f"--outputFile.json={report}")
    started = time.monotonic()
    child = run_coverage_command(argv, root)
    suite, failing = _vitest_suite(report)
    return _typescript_coverage(root, raw, inventory, {
        "runner": "vitest", "stack": "viewer", "base": root / "viewer", "lcov": lcov, "failing": failing,
        "facts": {"child_exit": child, "suite_exit": suite, "raw": _report_location(raw, run)},
        "commands": [_inherited_row("typescript-coverage", argv, root, child, started)],
        "artifacts": _raw_artifacts([report, lcov], run)})


def _statechart_argv(root, raw):
    """The package's own test script, run by node with its test coverage and two machine reporters."""
    script = json.loads((root / STATECHART_PACKAGE / "package.json").read_text(encoding="utf-8"))["scripts"]["test"]
    node, *arguments = shlex.split(script)
    if node != "node":
        raise ValueError(f"The statechart test script does not run node: {script}")
    return ["node", "--experimental-test-coverage",
            "--test-reporter=spec", "--test-reporter-destination=stdout",
            "--test-reporter=tap", f"--test-reporter-destination={raw / 'suite.tap'}",
            "--test-reporter=lcov", f"--test-reporter-destination={raw / 'lcov.info'}", *arguments]


def collect_statechart_coverage(root, raw, inventory):
    run, package = _report_root(raw), root / STATECHART_PACKAGE
    raw.mkdir(parents=True)
    report, lcov = raw / "suite.tap", raw / "lcov.info"
    argv = _statechart_argv(root, raw)
    started = time.monotonic()
    child = run_coverage_command(argv, package)
    suite, failing, skipped = _node_test_suite(report)
    return _typescript_coverage(root, raw, inventory, {
        "runner": "node --test", "stack": "statechart", "base": package, "lcov": lcov, "failing": failing,
        "facts": {"child_exit": child, "suite_exit": suite, "skipped": skipped, "raw": _report_location(raw, run)},
        "commands": [_inherited_row("typescript-coverage", argv, package, child, started)],
        "artifacts": _raw_artifacts([report, lcov], run)})


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
    facts = {"child_exit": child, "raw": _report_location(raw, run)}
    if child not in (0, 1):
        finding = {"gate": "csharp-coverage", "message": "native campaign incomplete"}
        if child == 2:
            finding.update(_campaign_diagnosis(raw))
        return _coverage_report(
            "csharp-coverage", facts, [], [finding],
            commands, artifacts)
    if child:
        names, summary = _failing_subjects(evidence)
        return _coverage_report("csharp-coverage", facts,
                                [{"gate": "functional", "child_exit": child,
                                  "failingTests": names, "failingTestsSummary": summary}], [],
                                commands, artifacts)
    joined = json.loads((evidence / "joined.json").read_text(encoding="utf-8"))
    return _coverage_report("csharp-coverage", {**facts, **joined}, joined["findings"], [],
                            commands, artifacts)


def collect_coverage(root, output, stack, inventory):
    raw = output / ("raw-" + uuid.uuid4().hex)
    if stack == "python":
        return collect_python_coverage(root, raw, inventory)
    if stack == "node":
        payload = json.loads(Path(inventory).read_text(encoding="utf-8"))
        return collect_node_coverage(root, raw, inventory,
                                     payload["candidate"]["sourceFingerprint"])
    if stack == "dotnet":
        return collect_dotnet_coverage(root, raw)
    if stack == "viewer":
        return collect_viewer_coverage(root, raw, inventory)
    if stack == "statechart":
        return collect_statechart_coverage(root, raw, inventory)
    raise ValueError(f"Unknown stack: {stack}")


def main():
    if sys.platform == "win32":
        signal.signal(signal.SIGBREAK, signal.default_int_handler)
    parser = argparse.ArgumentParser()
    parser.add_argument("--gate", choices=("static", "coverage"), required=True)
    parser.add_argument("--stack", choices=("dotnet", "python", "node", "viewer", "statechart"), required=True)
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
    candidate = {"commit": None, "dirty": None, "sourceFingerprint": None}
    inventory = run / "inventory.json"
    inventory_errors, inventory_commands, tools = [], [], {}
    try:
        candidate, _ = _candidate(root)
        inventory, inventory_errors, inventory_commands = _inventory_artifact(
            root, run, candidate)
        tools = gate_tools(root)
        inventory_candidate = json.loads(
            inventory.read_text(encoding="utf-8")).get("candidate", {})
        initial_fingerprint = inventory_candidate.get(
            "sourceFingerprint", candidate.get("sourceFingerprint"))
        report = collect_static(root, run / "raw", args.stack, initial_fingerprint) \
            if args.gate == "static" else collect_coverage(root, run, args.stack, inventory)
        if inventory_errors:
            report = {"status": "error", "facts": report.get("facts", {}),
                      "findings": report.get("findings", []),
                      "errors": [*report.get("errors", []), *inventory_errors]}
    except REPORT_DEFECTS as error:
        report = {"status": "error", "facts": {}, "findings": [],
                  "errors": [{"type": type(error).__name__, "message": str(error)}]}
        if not inventory.is_file():
            inventory.write_text(json.dumps({"schema": 1, "candidate": candidate,
                                             "files": [], "sources": [], "excluded": [],
                                             "projects": [], "errors": report["errors"]},
                                            indent=2, sort_keys=True) + "\n", encoding="utf-8")
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

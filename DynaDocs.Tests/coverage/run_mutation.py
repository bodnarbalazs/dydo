#!/usr/bin/env python3
"""Run mutation engines in an isolated candidate with immutable judging evidence."""
import argparse
import hashlib
import importlib.util
import json
import os
import re
import shlex
import shutil
import signal
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

import mutation_results as evidence
import run_tests
import tomllib

ROOT = Path(__file__).resolve().parents[2]
COVERAGE = Path(__file__).resolve().parent
DOTNET_NON_BLOCK = ("Statement", "Arithmetic", "Equality", "Boolean", "Logical", "Assignment", "Unary", "Update",
                    "Checked", "Linq", "String", "Bitwise", "Initializer", "Regex", "NullCoalescing", "Math",
                    "StringMethod", "Conditional", "CollectionExpression")


def validate_settings(settings, language):
    evidence.require(settings.get("concurrency") == 1 and type(settings["concurrency"]) is int,
                     "Mutation concurrency must be explicitly 1")
    evidence.require(settings.get("thresholds") == {"high": 100, "low": 100, "break": 100}, "Mutation thresholds must all be 100")
    evidence.require(settings.get("reporters") == ["json", "html"], "Only local JSON/HTML reporters are allowed")
    forbidden = ("ignore-mutations", "ignore-methods", "ignore-linq-expressions", "since", "baseline", "with-baseline",
                 "incremental", "ignorePatterns", "excludedMutations", "dashboard", "testCaseFilter", "test-case-filter")
    evidence.require(not any(settings.get(key) for key in forbidden), "Mutation configuration suppresses obligations")
    if language == "javascript":
        evidence.require(settings.get("coverageAnalysis") == "off" and settings.get("disableBail") is True,
                         "JS requires full suite execution without fake native coverage")


def effective_dotnet(text):
    evidence.require(re.findall(r"Version: ([0-9.]+)", text) == ["4.16.0"], "Missing or wrong effective Stryker.NET version")
    evidence.require(re.findall(r"Stryker will use a max of (\d+) parallel testsessions\.", text) == ["1"],
                     "Missing or conflicting effective .NET concurrency")


def native_count(text, language, count):
    pattern = r"(\d+) mutants created"
    if language == "javascript":
        evidence.require(re.findall(r"ProjectReader Found (\d+) of \d+ file\(s\) to be mutated\.", text) == ["1"],
                         "Native JS exact source selection is incomplete")
        pattern = r"Instrumenter Instrumented 1 source file\(s\) with (\d+) mutant\(s\)"
    evidence.require(re.findall(pattern, text) == [str(count)], "Native generated count differs from complete report")


def bootstrap(folder, sources):
    folder.mkdir(parents=True, exist_ok=False)
    manifest = {}
    for source in sources:
        destination = folder / source.name
        evidence.require(str(destination) not in manifest, "Ambiguous bootstrap basename")
        shutil.copyfile(source, destination)
        manifest[str(destination.resolve())] = hashlib.sha256(destination.read_bytes()).hexdigest()
    return manifest


def verify_bootstrap(manifest):
    evidence.require(isinstance(manifest, dict) and manifest, "Missing bootstrap manifest")
    for filename, digest in manifest.items():
        path = Path(filename)
        evidence.require(path.is_file() and not path.is_symlink()
                         and hashlib.sha256(path.read_bytes()).hexdigest() == digest,
                         f"Missing or tampered immutable bootstrap: {filename}")


def process(argv, cwd, prefix, timeout, manifest=None):
    if manifest is not None:
        verify_bootstrap(manifest)
    prefix.parent.mkdir(parents=True, exist_ok=True)
    record = {"argv": list(argv), "cwd": str(cwd), "timeout": False,
              "stdout": str(prefix.with_suffix(".stdout.log")),
              "stderr": str(prefix.with_suffix(".stderr.log")), "returncode": None}
    environment = run_tests.isolated_environment()
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    environment.pop("NODE_TEST_CONTEXT", None)
    started = time.monotonic()
    with Path(record["stdout"]).open("wb") as out, Path(record["stderr"]).open("wb") as err:
        child = subprocess.Popen(argv, cwd=cwd, env=environment, stdout=out, stderr=err,
                                 start_new_session=os.name != "nt")
        record["pid"] = child.pid
        try:
            child.wait(timeout=timeout)
        except (subprocess.TimeoutExpired, KeyboardInterrupt):
            record["timeout"] = True
            if os.name == "nt":
                terminated = subprocess.run(["taskkill", "/PID", str(child.pid), "/T", "/F"],
                                            capture_output=True, timeout=10, check=False)
                record["termination_exit"] = terminated.returncode
                if terminated.returncode != 0:
                    record["cleanup_error"] = "Process tree termination could not be confirmed"
                    child.kill()
            else:
                os.killpg(child.pid, signal.SIGKILL)
            child.wait(timeout=10)
        record["returncode"] = child.returncode
    record["elapsed_seconds"] = time.monotonic() - started
    prefix.with_suffix(".process.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
    return record


def read_json(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        raise evidence.Incomplete(f"Missing or malformed JSON: {path}") from error


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".partial")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")
    temporary.replace(path)


def suite_argv(command, language, harness, output, job):
    if language == "python":
        evidence.require(command[1:3] == ["-m", "unittest"], "Python test command must select the actual unittest suite")
        return [command[0], str(harness / "python-test-adapter.py"), "--output", str(output),
                "--job", job, "--", *command[3:]]
    evidence.require(language == "javascript" and "--test" in command,
                     "JavaScript test command must select the actual node:test suite")
    evidence.require(not any(arg.startswith("--test-reporter") for arg in command), "Conflicting Node reporter")
    evidence.require(not any(arg.startswith("--experimental-test-isolation") for arg in command), "Default Node child isolation is required")
    index = command.index("--test") + 1
    return [*command[:index], "--test-reporter=" + (harness / "node-test-reporter.cjs").as_uri(),
            "--test-reporter-destination=" + str(output), *command[index:]]


def run_suite(job, folder, cwd, expected=None):
    verify_bootstrap(job["bootstrap"])
    identity = job["job"]
    output = folder / ("suite.json" if job["language"] == "python" else "events.jsonl")
    argv = suite_argv(job["test_command"], job["language"], Path(job["harness"]), output, identity)
    record = process(argv, cwd, folder / "suite", job["timeout"], job["bootstrap"])
    evidence.require(not record["timeout"] and not record.get("cleanup_error"), "Actual suite timed out or did not clean up")
    if job["language"] == "python":
        raw = read_json(output)
        cases = raw.get("expected")
        state = evidence.python_suite(raw, identity, expected if expected is not None else cases)
    else:
        try:
            raw = [json.loads(line) for line in output.read_text(encoding="utf-8").splitlines()]
        except (OSError, ValueError) as error:
            raise evidence.Incomplete("Missing or partial Node reporter stream") from error
        raw = normalize_node_paths(raw, cwd)
        cases = evidence.node_cases(raw)
        state = evidence.node_suite(raw, expected if expected is not None else cases)
    evidence.require(record["returncode"] == (0 if state == "surviving" else 1), "Suite exit contradicts real case events")
    result = {"state": state, "expected": cases, "process": record, "raw": str(output)}
    if job["language"] == "javascript":
        result["tree"] = sorted(node["id"] for node in evidence.node_tree(raw))
        if job.get("expected_tree") is not None:
            evidence.require(result["tree"] == job["expected_tree"], "Changed Node suite tree")
    return result


def normalize_node_paths(events, cwd):
    for event in events:
        data = event.get("data", {})
        if data.get("file"):
            path = Path(data["file"])
            evidence.require(path.resolve().is_relative_to(cwd.resolve()), "Node event outside actual suite root")
            data["file"] = path.resolve().relative_to(cwd.resolve()).as_posix()
    return events


def execute_job(path, digest):
    evidence.require(hashlib.sha256(path.read_bytes()).hexdigest() == digest, "Changed immutable job envelope")
    job = read_json(path)
    verify_bootstrap(job["bootstrap"])
    native = os.environ.get("__STRYKER_ACTIVE_MUTANT__") if job["language"] == "javascript" else job.get("native_id")
    key = "baseline" if native is None else str(native)
    evidence.require(key == "baseline" or key.isalnum(), "Malformed native job identity")
    folder = Path(job["receipts"]) / key / uuid.uuid4().hex
    folder.mkdir(parents=True, exist_ok=False)
    current = {**job, "native_id": native, "invocation": folder.name, "complete": False}
    write_json(folder / "started.json", current)
    try:
        if job["language"] == "python":
            try:
                source = Path.cwd() / job["path"]
                compile(source.read_bytes(), str(source), "exec")
            except SyntaxError as error:
                current.update(state="invalid", syntax={"class": type(error).__name__, "message": str(error)})
                return 2
        result = run_suite(job, folder, Path.cwd(), job.get("expected"))
        current.update(result)
        current["complete"] = True
        return 0 if result["state"] == "surviving" else 1
    except (OSError, ValueError) as error:
        current.update(state="error", error=str(error))
        return 2
    finally:
        verify_bootstrap(job["bootstrap"])
        write_json(folder / "terminal.json", current)


def job_command(job, folder):
    path = folder / "job.json"
    write_json(path, job)
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    argv = [sys.executable, str(Path(job["harness"]) / "run_mutation.py"), "--job", str(path), "--job-sha256", digest]
    # Both engines receive only this constant launcher and an opaque, hashed envelope.
    if job["language"] == "python" or os.name != "nt":
        return shlex.join([Path(arg).as_posix() if "\\" in arg else arg for arg in argv])
    evidence.require(all(not any(char in arg for char in ('"', '%', '!', '\r', '\n')) for arg in argv), "Unsafe Windows launcher path")
    return " ".join('"' + arg + '"' for arg in argv)


def receipts(folder, expected_ids, job):
    found = {}
    for directory in folder.iterdir() if folder.exists() else []:
        records = list(directory.glob("*/terminal.json"))
        starts = list(directory.glob("*/started.json"))
        evidence.require(len(records) == len(starts) == 1, "Missing, partial or duplicate suite sidecar")
        evidence.require(records[0].parent == starts[0].parent, "Start/terminal sidecars belong to different invocations")
        record = read_json(records[0])
        start = read_json(starts[0])
        evidence.require(record.get("invocation") == records[0].parent.name and
                         all(record.get(key) == value for key, value in start.items() if key != "complete"),
                         "Changed suite invocation or immutable envelope")
        evidence.require(record.get("job") == job and record.get("native_id") == (None if directory.name == "baseline" else directory.name),
                         "Stale suite sidecar")
        evidence.require(record.get("complete") is True or record.get("state") == "invalid", "Suite sidecar did not complete")
        found[directory.name] = record
    evidence.require(set(found) == set(expected_ids), "Missing or extra suite jobs")
    return found


def checked(argv, cwd, prefix, timeout=120, manifest=None):
    record = process(argv, cwd, prefix, timeout, manifest)
    evidence.require(record["returncode"] == 0 and not record["timeout"] and not record.get("cleanup_error"),
                     f"Command did not complete successfully: {prefix}; see retained streams")
    return record


def cosmic_database(request):
    from cosmic_ray.work_db import WorkDB, use_db
    with use_db(request["database"], mode=WorkDB.Mode.open) as database:
        work = list(database.work_items)
        results = [{"job": job, "worker": result.worker_outcome.value,
                    "test": result.test_outcome.value, "output": result.output} for job, result in database.results]
    if request.get("selected"):
        selected = [item for item in work if item.job_id == request["selected"]]
        evidence.require(len(selected) == 1, "Missing or duplicate selected Cosmic job")
        with use_db(request["destination"]) as database:
            database.clear()
            database.add_work_items(selected)
    items = []
    for item in work:
        evidence.require(len(item.mutations) == 1, "Unexpected multi-mutation Cosmic work item")
        mutation = item.mutations[0]
        items.append({"job": item.job_id, "path": str(mutation.module_path).replace("\\", "/"),
                      "operator": mutation.operator_name, "occurrence": mutation.occurrence,
                      "start": list(mutation.start_pos), "end": list(mutation.end_pos)})
    write_json(Path(request["output"]), {"items": items, "results": results})


def cosmic_command(request, context, prefix):
    request_path = prefix.with_suffix(".request.json")
    write_json(request_path, request)
    checked([context["python"], str(context["harness"] / "run_mutation.py"), "--cosmic-database", str(request_path)],
            context["root"], prefix, manifest=context["bootstrap"])
    return read_json(Path(request["output"]))


def cosmic_config(path, module, command):
    template = (COVERAGE / "mutation/cosmic-ray.toml").read_text(encoding="utf-8")
    settings = tomllib.loads(template).get("cosmic-ray")
    evidence.require(settings == {"module-path": "", "timeout": 70.0, "excluded-modules": [],
                                  "test-command": "", "distributor": {"name": "local"}}, "Unexpected CosmicRay template policy")
    content = template.replace('module-path = ""', "module-path = " + json.dumps(module))
    content = content.replace('test-command = ""', "test-command = " + json.dumps(command))
    path.write_text(content, encoding="utf-8")


def make_job(context, module, folder, expected=None):
    return {"schema_version": 1, "job": uuid.uuid4().hex, "language": module["language"],
            "path": module["path"], "test_command": module["test_command"], "timeout": 60,
            "harness": str(context["harness"]), "bootstrap": context["bootstrap"],
            "receipts": str(folder / "receipts"), "expected": expected,
            "base_sha": context["base"], "candidate_sha": context["candidate"],
            "source_fingerprint": context["fingerprint"]}


def verify_fingerprint(context):
    actual = context["inventory"].fingerprint(context["root"], context["inventory"].git_paths(context["root"]))
    evidence.require(actual == context["fingerprint"], "Candidate source/test/config fingerprint drift")
    verify_bootstrap(context["bootstrap"])


def python_mutations(context, module, members, folder):
    source = evidence.Source((context["root"] / module["path"]).read_bytes())
    baseline_job = make_job(context, module, folder)
    baseline = run_suite(baseline_job, folder, context["root"])
    evidence.require(baseline["state"] == "surviving", "Python baseline has failing real cases")
    baseline_job["expected"] = baseline["expected"]
    config = folder / "cosmic.toml"
    cosmic_config(config, module["path"], job_command(baseline_job, folder))
    database = folder / "all.sqlite"
    checked([context["python"], "-m", "cosmic_ray.cli", "init", str(config), str(database)], context["root"], folder / "init", manifest=context["bootstrap"])
    inventory = cosmic_command({"database": str(database), "output": str(folder / "initialized.json")}, context, folder / "inventory")
    checked([context["python"], "-m", "cosmic_ray.cli", "baseline", str(config), "--session-file", str(folder / "baseline.sqlite")],
            context["root"], folder / "baseline-engine", manifest=context["bootstrap"])
    native_baseline = receipts(folder / "receipts", ["baseline"], baseline_job["job"])["baseline"]
    evidence.require(native_baseline["state"] == "surviving", "Native Python baseline did not pass")
    rows, jobs = [], []
    for item in inventory["items"]:
        evidence.require(item["path"] == module["path"], "Cosmic initialized foreign source")
        span = (*source.codepoint_position(item["start"]), *source.codepoint_position(item["end"]))
        row = {"path": module["path"], "span": span, "mutator": item["operator"],
               "replacement": str(item["occurrence"]), "native_id": item["job"], "source_sha256": source.sha256}
        if evidence.owner(row, members) is None:
            continue
        result = python_job(context, module, folder, database, baseline, item)
        row.update(state=result["state"], native=result)
        rows.append(row)
        jobs.append(result)
    return {**evidence.evaluate(members, rows), "baseline": baseline, "jobs": jobs}


def python_job(context, module, folder, database, baseline, item):
    target = folder / item["job"]
    target.mkdir()
    selected = target / "selected.sqlite"
    cosmic_command({"database": str(database), "selected": item["job"], "destination": str(selected),
                    "output": str(target / "selected.json")}, context, target / "select")
    job = make_job(context, module, target, baseline["expected"])
    job["native_id"] = item["job"]
    config = target / "cosmic.toml"
    cosmic_config(config, module["path"], job_command(job, target))
    original = (context["root"] / module["path"]).read_bytes()
    try:
        engine = process([context["python"], "-m", "cosmic_ray.cli", "exec", str(config), str(selected)],
                         context["root"], target / "engine", 120, context["bootstrap"])
    finally:
        (context["root"] / module["path"]).write_bytes(original)
        verify_fingerprint(context)
    native = cosmic_command({"database": str(selected), "output": str(target / "results.json")}, context, target / "read-results")
    evidence.require(len(native["results"]) == 1 and native["results"][0]["job"] == item["job"], "Missing/extra Cosmic result job")
    actual = receipts(target / "receipts", [item["job"]], job["job"])[item["job"]]
    outcome = native["results"][0]
    state = "error"
    if engine["timeout"]:
        state = "timeout"
    elif engine["returncode"] == 0 and outcome["worker"] == "normal":
        state = cosmic_state(outcome["test"], actual)
    return {"state": state, "engine": engine, "native": outcome, "suite": actual}


def cosmic_state(native, actual):
    if actual["state"] == "invalid":
        return "invalid"
    if native == "survived":
        evidence.require(actual["state"] == "surviving", "Contradictory Cosmic survivor/real-case failure")
        return "surviving"
    if native == "killed":
        evidence.require(actual["state"] == "killed", "Cosmic nonzero is not a real-case kill")
        return "killed"
    return "unrun" if native in ("skipped", "no-test") else "error"


def glob_literal(path):
    return "".join("[" + char + "]" if char in "*?[]{}()!" else char for char in path)


def javascript_mutations(context, module, members, folder):
    job = make_job(context, module, folder)
    baseline = run_suite(job, folder, context["root"])
    evidence.require(baseline["state"] == "surviving", "Node baseline has failing real cases")
    job["expected"] = baseline["expected"]
    job["expected_tree"] = baseline["tree"]
    config = read_json(COVERAGE / "mutation/stryker-js.json")
    validate_settings(config, "javascript")
    source = evidence.Source((context["root"] / module["path"]).read_bytes())
    config.update(commandRunner={"command": job_command(job, folder)}, mutate=[js_range(member, source.bom) for member in members],
                  jsonReporter={"fileName": str(folder / "mutation.json")},
                  htmlReporter={"fileName": str(folder / "mutation.html")}, tempDirName=str(folder / "sandbox"))
    config_path = folder / "stryker.json"
    write_json(config_path, config)
    engine = process(["node", str(context["js_tool"]), "run", str(config_path)], context["root"], folder / "engine", 1800, context["bootstrap"])
    verify_fingerprint(context)
    report = read_json(folder / "mutation.json")
    observed = Path(engine["stdout"]).read_text(encoding="utf-8", errors="replace")
    observed = re.sub(r"\x1b\[[0-9;]*m", "", observed)
    evidence.require(re.findall(r"ConcurrencyTokenProvider Creating (\d+) test runner process\(es\)\.", observed) == ["1"],
                     "Missing or conflicting effective JS concurrency")
    sources = {module["path"]: evidence.Source((context["root"] / module["path"]).read_bytes())}
    evidence.require(report.get("framework", {}).get("version") == "9.6.1", "Missing effective StrykerJS version")
    effective = report.get("config", {})
    # The built-in dashboard URL is inert with strictly local reporters.
    validate_settings({key: value for key, value in effective.items() if key != "dashboard"}, "javascript")
    evidence.require(effective.get("mutate") == config["mutate"] and effective.get("commandRunner") == config["commandRunner"], "Effective JS scope/launcher mismatch")
    rows = evidence.stryker_rows(report, sources if report.get("files") else {})
    native_count(observed, "javascript", len(rows))
    selected = [row for row in rows if evidence.owner(row, members)]
    expected = ["baseline", *(row["native_id"] for row in selected if row["state"] != "invalid")]
    suites = receipts(folder / "receipts", expected, job["job"])
    evidence.require(suites["baseline"]["state"] == "surviving", "Instrumented Node baseline failed")
    for row in selected:
        if row["state"] == "invalid":
            continue
        actual = suites[row["native_id"]]
        if row["state"] in ("killed", "surviving"):
            evidence.require(row["state"] == actual["state"], "Native JS status contradicts actual suite")
        row["suite"] = actual
    result = evidence.evaluate(members, selected)
    if engine["timeout"] or engine.get("cleanup_error") or engine["returncode"] not in (0, 1):
        result["exit_code"] = 2
    return {**result, "baseline": baseline, "engine": engine, "config": config, "suites": suites}


def js_range(member, bom=False):
    span = list(evidence.member_span(member))
    span[1] += int(bom and span[0] == 1)
    span[3] += int(bom and span[2] == 1)
    return f"{glob_literal(member['path'])}:{span[0]}:{span[1]}-{span[2]}:{span[3]}"


def dotnet_baseline(context, module, folder):
    command = module["test_command"]
    evidence.require(command[:2] == ["dotnet", "test"] and "--" not in command,
                     "C# baseline requires unambiguous dotnet test argv")
    argv = [*command, "--logger", "trx;LogFileName=baseline.trx", "--results-directory", str(folder),
            "--", "RunConfiguration.TreatNoTestsAsError=true"]
    result = checked(argv, context["root"], folder / "baseline", 600, context["bootstrap"])
    trx = folder / "baseline.trx"
    try:
        tree = ET.parse(trx)
    except (OSError, ET.ParseError) as error:
        raise evidence.Incomplete("Missing or malformed .NET baseline TRX") from error
    cases = [node.attrib for node in tree.iter() if node.tag.endswith("}UnitTestResult")]
    evidence.require(cases and all(item.get("outcome") == "Passed" for item in cases)
                     and len({item.get("executionId") for item in cases}) == len(cases), "Empty, duplicate or unsuccessful .NET baseline cases")
    return {"process": result, "cases": cases, "trx": str(trx)}


def dotnet_config(context, module, members, folder):
    source = read_json(context["root"] / "stryker-config.json")
    settings = source.get("stryker-config")
    evidence.require(isinstance(settings, dict), "Missing .NET root mutation configuration")
    validate_settings(settings, "cs")
    project_dir = Path(module["project"]).parent
    settings.update({"project": str(context["root"] / module["project"]),
                     "test-projects": [str(context["root"] / path) for path in module["test_projects"]],
                     "concurrency": 1, "thresholds": {"high": 100, "low": 100, "break": 100},
                     "reporters": ["json", "html"], "break-on-initial-test-failure": True,
                     "mutation-level": "Complete", "disable-bail": True, "disable-mix-mutants": True,
                     "mutate": []})
    for member in members:
        path = member["path"]
        relative = Path(path).relative_to(project_dir).as_posix()
        text = evidence.Source((context["root"] / path).read_bytes())
        span = evidence.member_span(member)
        settings["mutate"].append(f"{glob_literal(relative)}{{{text.offset(span[:2])}..{text.offset(span[2:])}}}")
    return source


def dotnet_report(context, module, folder, members, supplemental=False):
    reports = list(folder.glob("**/mutation-report.json"))
    evidence.require(len(reports) == 1, "Missing or duplicate .NET native report")
    report = read_json(reports[0])
    sources = {}
    normalized = {}
    for native_path, value in report.get("files", {}).items():
        path = Path(native_path)
        evidence.require(path.is_absolute() and path.resolve().is_relative_to(context["root"].resolve()), "Unjoined .NET report path")
        relative = path.relative_to(context["root"]).as_posix()
        evidence.canonical_file(context["root"], relative)
        normalized[relative] = value
        sources[relative] = evidence.Source(path.read_bytes())
    report["files"] = normalized
    rows = evidence.stryker_rows(report, sources)
    tests = [test for file in report.get("testFiles", {}).values() for test in file.get("tests", [])]
    ids = {test.get("id") for test in tests}
    evidence.require(ids and None not in ids and len(ids) == len(tests), "Missing or duplicate .NET native test identities")
    selected = []
    for row in rows:
        if evidence.owner(row, members) is None:
            allowed = {"Removed by mutate filter"}
            if supplemental:
                allowed.add("Removed by mutation type filter")
            evidence.require(row["state"] == "ignored" and row["native"].get("statusReason") in allowed,
                             "Unexpected native mutation outside selected scope")
            continue
        if row["state"] == "killed":
            killed = row["native"].get("killedBy")
            evidence.require(isinstance(killed, list) and killed and set(killed) <= ids, "Native .NET kill lacks real testcase identities")
        selected.append(row)
    return selected, tests, str(reports[0])


def dotnet_batch(context, module, members, folder, config):
    folder.mkdir(parents=True, exist_ok=True)
    baseline = dotnet_baseline(context, module, folder)
    path = folder / "stryker.json"
    write_json(path, config)
    record = process(["dotnet", "stryker", "--config-file", str(path), "--concurrency", "1", "--output", str(folder / "native"), "--skip-version-check"],
                     context["root"], folder / "engine", 1800, context["bootstrap"])
    verify_fingerprint(context)
    evidence.require(not record["timeout"] and not record.get("cleanup_error") and record["returncode"] in (0, 2), "Incomplete .NET engine batch")
    effective_dotnet(Path(record["stdout"]).read_text(encoding="utf-8", errors="replace"))
    rows, tests, report = dotnet_report(context, module, folder / "native", members,
                                       config["stryker-config"].get("ignore-mutations") == list(DOTNET_NON_BLOCK))
    raw_count = sum(len(file["mutants"]) for file in read_json(Path(report))["files"].values())
    native_count(Path(record["stdout"]).read_text(encoding="utf-8", errors="replace"), "cs", raw_count)
    evidence.require(sorted(test["name"] for test in tests) == sorted(test["testName"] for test in baseline["cases"]),
                     "Native .NET test inventory differs from complete baseline")
    return {"rows": rows, "baseline": baseline, "test_identities": tests, "report": report,
            "process": record, "config": config, "source_fingerprint": context["fingerprint"]}


def dotnet_mutations(context, module, members, folder):
    config = dotnet_config(context, module, members, folder)
    primary = dotnet_batch(context, module, members, folder / "primary", config)
    supplements = []
    for row in primary["rows"]:
        if row["state"] != "ignored":
            continue
        reason = row["native"].get("statusReason", "")
        evidence.require(row["mutator"] == "Block removal mutation" and reason == "Removed by block already covered filter",
                         f"Unexpected selected ignored mutation: {reason}")
        supplement = supplemental_config(context, module, row, primary["rows"], config)
        batch = dotnet_batch(context, module, members, folder / ("block-" + row["native_id"]), supplement)
        batch["scheduled"] = [evidence.identity(row)]
        supplements.append(batch)
    rows = evidence.reconcile(primary["rows"], supplements)
    return {**evidence.evaluate(members, rows), "primary": primary, "supplements": supplements}


def supplemental_config(context, module, row, primary, config):
    config = json.loads(json.dumps(config))
    settings = config["stryker-config"]
    settings["ignore-mutations"] = list(DOTNET_NON_BLOCK)
    relative = Path(row["path"]).relative_to(Path(module["project"]).parent).as_posix()
    source = evidence.Source((context["root"] / row["path"]).read_bytes())
    span = row["span"]
    settings["mutate"] = [f"{glob_literal(relative)}{{{source.offset(span[:2])}..{source.offset(span[2:])}}}"]
    for child in primary:
        if child["path"] == row["path"] and child["mutator"] == "Block removal mutation" and child is not row:
            inner = child["span"]
            evidence.require(inner != span, "Equal-span competing block cannot be scheduled")
            if evidence.contains(span, inner):
                settings["mutate"].append(f"!{glob_literal(relative)}{{{source.offset(inner[:2])}..{source.offset(inner[2:])}}}")
    return config


def load_inventory(path):
    specification = importlib.util.spec_from_file_location("mutation_inventory", path)
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    evidence.require(callable(getattr(module, "git_paths", None)) and callable(getattr(module, "fingerprint", None)),
                     "Parent inventory helpers are not implemented")
    return module


def resolve_commit(root, value):
    result = subprocess.run(["git", "rev-parse", "--verify", value + "^{commit}"], cwd=root,
                            capture_output=True, text=True, check=False)
    evidence.require(result.returncode == 0, "Base is not an existing immutable commit")
    return result.stdout.strip()


def restore_tools(context, languages):
    root, folder = context["root"], context["folder"]
    versions = {}
    if "python" in languages:
        venv = folder / "python"
        checked([sys.executable, "-m", "venv", str(venv)], root, folder / "python-venv")
        interpreter = venv / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
        checked([str(interpreter), "-m", "pip", "install", "--require-hashes", "-r", str(root / "DynaDocs.Tests/coverage/requirements.lock")],
                root, folder / "python-restore", 600)
        record = checked([str(interpreter), "-c", "import importlib.metadata; print(importlib.metadata.version('cosmic-ray'))"], root, folder / "python-version")
        evidence.require(Path(record["stdout"]).read_text().strip() == "8.7.0", "Unexpected CosmicRay version")
        context["python"] = str(interpreter)
        versions["cosmic-ray"] = "8.7.0"
    if "javascript" in languages:
        tools = root / "DynaDocs.Tests/coverage/tools"
        checked(["npm.cmd" if os.name == "nt" else "npm", "ci", "--ignore-scripts"], tools, folder / "npm-restore", 600)
        manifest = read_json(tools / "node_modules/@stryker-mutator/core/package.json")
        evidence.require(manifest.get("version") == "9.6.1", "Unexpected StrykerJS version")
        context["js_tool"] = tools / "node_modules/@stryker-mutator/core/bin/stryker.js"
        versions["stryker-js"] = "9.6.1"
    if "cs" in languages:
        checked(["dotnet", "tool", "restore"], root, folder / "dotnet-restore", 600)
        record = checked(["dotnet", "tool", "list", "--local"], root, folder / "dotnet-version")
        evidence.require(re.search(r"^dotnet-stryker\s+4\.16\.0\s", Path(record["stdout"]).read_text(), re.MULTILINE),
                         "Unexpected Stryker.NET version")
        versions["stryker-net"] = "4.16.0"
    return versions


def run_candidate(context):
    inventory_path = context["root"] / "DynaDocs.Tests/coverage/inventory.py"
    evidence.require(inventory_path.is_file(), "Parent inventory.py dependency is not implemented")
    sources = [COVERAGE / "run_mutation.py", COVERAGE / "mutation_results.py", COVERAGE / "run_tests.py",
               COVERAGE / "mutation/python-test-adapter.py", COVERAGE / "mutation/node-test-reporter.cjs", inventory_path]
    context["bootstrap"] = bootstrap(context["harness"], sources)
    context["inventory"] = load_inventory(context["harness"] / "inventory.py")
    context["fingerprint"] = context["inventory"].fingerprint(context["root"], context["inventory"].git_paths(context["root"]))
    inventory_file = context["folder"] / "inventory.json"
    checked([sys.executable, str(context["root"] / "DynaDocs.Tests/coverage/gap_check.py"),
             "--mutation-input", context["base"], "--output", str(inventory_file)], context["root"], context["folder"] / "inventory")
    data = evidence.validate_inventory(read_json(inventory_file), context["root"], context["candidate"], context["base"], context["fingerprint"])
    verify_fingerprint(context)
    report = {"schema_version": 1, "base_sha": context["base"], "candidate_sha": context["candidate"],
              "source_fingerprint": context["fingerprint"], "inventory": str(inventory_file),
              "bootstrap": context["bootstrap"], "modules": [], "non_behavior_changes": data["non_behavior_changes"]}
    report["versions"], unavailable = prepare_languages(context, data["modules"])
    engines = {"python": python_mutations, "javascript": javascript_mutations, "cs": dotnet_mutations}
    for index, module in enumerate(data["modules"]):
        folder = context["folder"] / f"module-{index}"
        folder.mkdir()
        members = [member for member in data["changed_members"] if member["path"] == module["path"]]
        try:
            evidence.require(module["language"] not in unavailable, unavailable.get(module["language"], ""))
            outcome = engines[module["language"]](context, module, members, folder)
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            outcome = {"exit_code": 2, "error": str(error)}
        report["modules"].append({**module, **outcome, "evidence": str(folder)})
    verify_fingerprint(context)
    report["exit_code"] = max((module["exit_code"] for module in report["modules"]), default=0)
    return report


def prepare_languages(context, modules):
    versions, unavailable = {}, {}
    for language in sorted({module["language"] for module in modules}):
        try:
            versions.update(restore_tools(context, {language}))
        except (OSError, ValueError, subprocess.SubprocessError) as error:
            unavailable[language] = str(error)
    return versions, unavailable


def run(since):
    candidate = resolve_commit(ROOT, "HEAD")
    base = resolve_commit(ROOT, since)
    folder = ROOT / "dydo/_system/.local/mutation" / (candidate[:12] + "-" + uuid.uuid4().hex)
    folder.mkdir(parents=True, exist_ok=False)
    snapshot = None
    report = {"schema_version": 1, "candidate_sha": candidate, "base_sha": base, "exit_code": 2}
    try:
        run_tests.ROOT = ROOT
        snapshot = run_tests.create_worktree()
        evidence.require(snapshot is not None, "Could not isolate mutation candidate")
        run_tests.copy_dirty_files(snapshot)
        report = run_candidate({"root": snapshot, "candidate": candidate, "base": base,
                                "folder": folder, "harness": folder / "harness"})
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        report["error"] = str(error)
    finally:
        if snapshot is not None:
            unconfirmed = unconfirmed_cleanup(folder)
            if not unconfirmed:
                run_tests.remove_worktree(snapshot)
            if snapshot.exists() or unconfirmed:
                report.update(exit_code=2, cleanup_error="Retained mutation worktree: " + str(snapshot))
        evidence.write_summary(folder, report)
        print(str(folder / "summary.json"))
        print(str(folder / "summary.html"))
    return report["exit_code"]


def unconfirmed_cleanup(folder):
    for path in folder.rglob("*.process.json"):
        try:
            if read_json(path).get("cleanup_error"):
                return True
        except (OSError, ValueError):
            return True
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--since")
    parser.add_argument("--job", type=Path, help=argparse.SUPPRESS)
    parser.add_argument("--job-sha256", help=argparse.SUPPRESS)
    parser.add_argument("--cosmic-database", type=Path, help=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
        if args.job:
            return execute_job(args.job, args.job_sha256)
        if args.cosmic_database:
            cosmic_database(read_json(args.cosmic_database))
            return 0
        evidence.require(args.since is not None, "--since is required")
        return run(args.since)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print("Mutation evidence incomplete: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())

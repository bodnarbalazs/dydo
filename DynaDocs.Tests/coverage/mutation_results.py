"""Strict mutation evidence joins and acceptance policy; no native score is trusted."""
import hashlib
import html
import json
import re
from collections import Counter
from pathlib import PurePosixPath
from urllib.parse import quote


class Incomplete(ValueError):
    """The evidence cannot establish mutation acceptance."""


def require(condition, message):
    if not condition:
        raise Incomplete(message)


def integer(value, minimum=0):
    return type(value) is int and value >= minimum


def canonical_file(root, relative):
    require(isinstance(relative, str) and relative and "\\" not in relative,
            f"Noncanonical path: {relative!r}")
    path = PurePosixPath(relative)
    require(not path.is_absolute() and path.as_posix() == relative
            and all(part not in (".", "..") and ":" not in part for part in path.parts),
            f"Unsafe path: {relative}")
    current = root.resolve()
    for part in path.parts:
        require(current.is_dir() and part in {entry.name for entry in current.iterdir()},
                f"Missing or wrong-case path: {relative}")
        current = current / part
        require(not current.is_symlink() and current.resolve().is_relative_to(root.resolve()),
                f"Aliased path: {relative}")
    require(current.is_file(), f"Not a regular file: {relative}")
    return current


class Source:
    """Convert native positions without losing BOM, CRLF or surrogate boundaries."""

    def __init__(self, raw):
        self.sha256 = hashlib.sha256(raw).hexdigest()
        self.bom = raw.startswith(b"\xef\xbb\xbf")
        try:
            self.text = raw.decode("utf-8-sig")
        except UnicodeError as error:
            raise Incomplete("Mutation source is not strict UTF8") from error
        self.lines = self.text.splitlines(keepends=True)
        if not self.lines or self.text.endswith(("\r", "\n")):
            self.lines.append("")

    def offset(self, position):
        line, column = position
        require(integer(line, 1) and integer(column) and line <= len(self.lines), "Invalid source position")
        text = self.lines[line - 1].rstrip("\r\n")
        encoded = text.encode("utf-16-le")
        require(column * 2 <= len(encoded), "Position exceeds source line")
        try:
            encoded[:column * 2].decode("utf-16-le")
        except UnicodeError as error:
            raise Incomplete("Position splits a surrogate pair") from error
        return sum(len(value.encode("utf-16-le")) // 2 for value in self.lines[:line - 1]) + column

    def codepoint_position(self, position):
        line, column = position
        require(integer(line, 1) and integer(column) and line <= len(self.lines), "Invalid native position")
        text = self.lines[line - 1].rstrip("\r\n")
        require(column <= len(text), "Native position exceeds line")
        return line, len(text[:column].encode("utf-16-le")) // 2

    def extract(self, span):
        start, end = self.offset(span[:2]), self.offset(span[2:])
        require(start < end, "Empty or reversed mutation span")
        return self.text.encode("utf-16-le")[start * 2:end * 2].decode("utf-16-le")


def member_span(member):
    try:
        return tuple(member[key] for key in ("start_line", "start_column", "end_line", "end_column"))
    except (KeyError, TypeError) as error:
        raise Incomplete("Member has no canonical span") from error


def contains(outer, inner):
    return outer[:2] <= inner[:2] and inner[2:] <= outer[2:]


def validate_inventory(data, root, candidate, base, fingerprint):
    require(isinstance(data, dict), "Inventory must be an object")
    expected = {"schema_version": 1, "position_encoding": "utf16", "candidate_sha": candidate,
                "base_sha": base, "source_fingerprint": fingerprint}
    for key, value in expected.items():
        require(data.get(key) == value, f"Missing or mismatched inventory {key}")
    for value, length in ((candidate, 40), (base, 40), (fingerprint, 64)):
        require(isinstance(value, str) and re.fullmatch(f"[0-9a-f]{{{length}}}", value), "Malformed source identity")
    modules, members, changes = (data.get(key) for key in ("modules", "changed_members", "non_behavior_changes"))
    require(all(isinstance(value, list) for value in (modules, members, changes)), "Missing inventory collections")
    by_path = {}
    for module in modules:
        path = validate_module(module, root)
        require(path not in by_path, "Duplicate module path")
        by_path[path] = module
    ids, affected = set(), set()
    for member in members:
        require(isinstance(member, dict) and isinstance(member.get("id"), str) and member["id"], "Missing member ID")
        require(member["id"] not in ids and member.get("path") in by_path, "Duplicate or unjoined member")
        ids.add(member["id"])
        affected.add(member["path"])
        Source(canonical_file(root, member["path"]).read_bytes()).extract(member_span(member))
        if by_path[member["path"]]["language"] == "javascript":
            validate_js_coverage(member, fingerprint)
    require(affected == set(by_path), "Target module has no changed member obligations")
    for change in changes:
        require(isinstance(change, dict) and isinstance(change.get("path"), str)
                and isinstance(change.get("reason"), str) and change["reason"].strip(), "Missing nonbehavior evidence")
        require(change["path"] not in affected, "Nonbehavior claim contradicts executable obligation")
    require(members or changes, "Empty inventory proves no scope")
    return data


def validate_module(module, root):
    require(isinstance(module, dict), "Malformed module")
    path = module.get("path")
    canonical_file(root, path)
    require(module.get("language") in ("cs", "python", "javascript") and module.get("role") == "target", "Unknown module language or role")
    command = module.get("test_command")
    require(isinstance(command, list) and command and all(isinstance(arg, str) and arg and "\0" not in arg for arg in command), "Test command must be structured argv")
    projects = module.get("test_projects")
    require(isinstance(projects, list), "Missing test projects")
    if module["language"] == "cs":
        canonical_file(root, module.get("project"))
        require(projects and all(isinstance(project, str) for project in projects) and len(set(projects)) == len(projects), "Missing or duplicate test projects")
        for project in projects:
            canonical_file(root, project)
    else:
        require(module.get("project") is None and projects == [], "Unexpected non-C# project")
    return path


def validate_js_coverage(member, fingerprint):
    coverage = member.get("coverage")
    require(isinstance(coverage, dict) and coverage.get("source_fingerprint") == fingerprint,
            "Missing or stale JavaScript callable coverage")
    require(integer(coverage.get("execution_count")), "Missing JavaScript callable execution count")
    lines = coverage.get("body_lines")
    require(isinstance(lines, dict) and lines, "Missing JavaScript body point denominator")
    for line, hits in lines.items():
        require(isinstance(line, str) and line.isdecimal() and str(int(line)) == line
                and integer(hits) and member["start_line"] <= int(line) <= member["end_line"],
                "Malformed JavaScript body point")


STRYKER_STATES = {"Killed": "killed", "Survived": "surviving", "NoCoverage": "uncovered",
                  "CompileError": "invalid", "Timeout": "timeout", "RuntimeError": "error",
                  "NotRun": "unrun", "Pending": "unrun", "Ignored": "ignored"}


def stryker_rows(report, sources):
    require(isinstance(report, dict) and report.get("schemaVersion") in ("1.0", "2")
            and isinstance(report.get("files"), dict), "Malformed Stryker report")
    rows = []
    for path, file in report["files"].items():
        require(path in sources and isinstance(file, dict), f"Unjoined native file: {path}")
        source = sources[path]
        native_bom = report["schemaVersion"] == "1.0" and source.bom
        require(file.get("source") == ("\ufeff" if native_bom else "") + source.text, f"Native source mismatch: {path}")
        require(isinstance(file.get("mutants"), list), "Missing native mutants")
        ids = set()
        for native in file["mutants"]:
            row = stryker_row(native, path, source, native_bom)
            require(row["native_id"] not in ids, "Duplicate native mutant ID")
            ids.add(row["native_id"])
            rows.append(row)
    require(set(report["files"]) == set(sources), "Native report omitted selected file")
    return rows


def stryker_row(native, path, source, native_bom=False):
    require(isinstance(native, dict) and isinstance(native.get("id"), str), "Missing native mutant ID")
    try:
        start, end = (native["location"][key] for key in ("start", "end"))
        span = (start["line"], start["column"] - 1 - int(native_bom and start["line"] == 1),
                end["line"], end["column"] - 1 - int(native_bom and end["line"] == 1))
        source.extract(span)
    except (KeyError, TypeError) as error:
        raise Incomplete("Malformed native mutation location") from error
    require(isinstance(native.get("mutatorName"), str) and native["mutatorName"]
            and isinstance(native.get("replacement"), str), "Missing native mutation identity")
    return {"path": path, "span": span, "mutator": native["mutatorName"],
            "replacement": native["replacement"], "native_id": native["id"],
            "source_sha256": source.sha256, "state": STRYKER_STATES.get(native.get("status"), "unknown"),
            "native": native}


def identity(row):
    return json.dumps([row.get("source_sha256"), row["path"], list(row["span"]),
                       row["mutator"], row["replacement"]], ensure_ascii=False, separators=(",", ":"))


def unique_rows(rows):
    keyed = {identity(row): row for row in rows}
    require(len(keyed) == len(rows), "Duplicate mutation identity within batch")
    return keyed


def reconcile(primary, supplements):
    indexed = unique_rows(primary)
    outcomes = {key: [row["state"]] for key, row in indexed.items()}
    for batch in supplements:
        rows = unique_rows(batch["rows"])
        scheduled = batch["scheduled"]
        require(isinstance(scheduled, list) and len(set(scheduled)) == len(scheduled), "Duplicate scheduled identity")
        require(set(scheduled) <= rows.keys() <= indexed.keys(), "Missing or foreign supplemental identity")
        for key, row in rows.items():
            if key in scheduled or row["state"] != "ignored":
                outcomes[key].append(row["state"])
    result = []
    for key, row in indexed.items():
        states = outcomes[key]
        require("ignored" not in states or any(state != "ignored" for state in states), "Unresolved native ignored mutation")
        state = next((value for value in ("unknown", "error", "timeout", "unrun", "surviving", "uncovered") if value in states), None)
        if state is None:
            require(not ("invalid" in states and "killed" in states), "Contradictory invalid/killed mutation")
            state = "killed" if "killed" in states else "invalid"
        result.append({**row, "state": state, "batch_states": states})
    return result


def owner(row, members):
    candidates = [member for member in members if member["path"] == row["path"]
                  and contains(member_span(member), row["span"])]
    if not candidates:
        return None
    for first in candidates:
        for second in candidates:
            if first is second:
                continue
            a, b = member_span(first), member_span(second)
            require(a != b and (contains(a, b) or contains(b, a)), "Equal or crossing fragment ownership")
    return next(member for member in candidates
                if all(contains(member_span(other), member_span(member)) for other in candidates))


def evaluate(members, rows):
    unique_rows(rows)
    grouped = {member["id"]: [] for member in members}
    for row in rows:
        selected = owner(row, members)
        require(selected is not None, "Native mutation falls outside selected fragments")
        grouped[selected["id"]].append({**row, "state": measured_state(row, selected)})
    summaries = [member_summary(member, grouped[member["id"]]) for member in members]
    return {"members": summaries, "exit_code": max((m["exit_code"] for m in summaries), default=0)}


def measured_state(row, selected):
    state = row["state"]
    require(state in set(STRYKER_STATES.values()) | {"unknown"}, "Unknown normalized mutation state")
    coverage = selected.get("coverage")
    if not coverage or state != "killed":
        return state
    first, _, last, column = row["span"]
    if column == 0:
        last -= 1
    covered = any(first <= int(line) <= last and hits > 0 for line, hits in coverage["body_lines"].items())
    return "uncovered" if coverage["execution_count"] == 0 or not covered else state


def member_summary(member, values):
    counts = Counter(row["state"] for row in values)
    valid = len(values) - counts["invalid"]
    incomplete = any(counts[state] for state in ("timeout", "error", "unrun", "ignored", "unknown"))
    code = 2 if incomplete else int(valid == 0 or counts["killed"] != valid)
    return {**member, "generated": len(values), "valid": valid,
            "score": 100 * counts["killed"] / valid if valid else None,
            **{state: counts[state] for state in set(STRYKER_STATES.values()) | {"unknown"}},
            "rows": values, "exit_code": code}


def write_summary(folder, report):
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "summary.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    escaped = html.escape(json.dumps(report, indent=2, ensure_ascii=False))
    links = []
    for path in sorted(folder.rglob("*")):
        if path.is_file() and path.name not in ("summary.html", "summary.json"):
            relative = path.relative_to(folder).as_posix()
            links.append('<li><a href="' + quote(relative) + '">' + html.escape(relative) + '</a></li>')
    (folder / "summary.html").write_text("<!doctype html><meta charset='utf-8'><title>Mutation evidence</title>"
                                         "<h1>Mutation evidence</h1><pre>" + escaped + "</pre><h2>Retained artifacts</h2><ul>"
                                         + "".join(links) + "</ul>", encoding="utf-8")


def python_suite(report, job, expected):
    require(isinstance(report, dict) and report.get("schema_version") == 1 and report.get("job") == job,
            "Stale or malformed Python suite receipt")
    require(report.get("complete") is True and report.get("phase") == "completed"
            and report.get("discovery_errors") == [], "Python suite did not complete discovery and execution")
    require(expected and len(set(expected)) == len(expected) and report.get("expected") == expected
            and report.get("tests_run") == len(expected), "Changed or empty Python case inventory")
    grouped = {case: [] for case in expected}
    for event in report.get("events", []):
        require(isinstance(event, dict) and event.get("id") in grouped, "Unexpected Python case event")
        grouped[event["id"]].append(event.get("phase"))
    failed = False
    for phases in grouped.values():
        require(len(phases) >= 3 and phases[0] == "start" and phases[-1] == "stop", "Incomplete Python case lifecycle")
        middle = phases[1:-1]
        if all(phase in ("subtest-failure", "subtest-error") for phase in middle):
            failed = True
        else:
            require(len(middle) == 1 and middle[0] in ("success", "failure", "error"), "Skipped or malformed Python case lifecycle")
            failed |= middle[0] != "success"
    require(report.get("success") is (not failed), "Contradictory Python suite success")
    return "killed" if failed else "surviving"


def node_signature(data):
    return tuple(data.get(key) for key in ("file", "nesting", "name", "line", "column"))


def node_tree(events):
    require(isinstance(events, list), "Missing Node event stream")
    summaries = [event.get("data", {}) for event in events if event.get("type") == "test:summary"]
    aggregate = [item for item in summaries if not item.get("file")]
    children = {item.get("file"): item for item in summaries if item.get("file")}
    require(len(aggregate) == 1 and children and len(children) == len(summaries) - 1,
            "Missing or duplicate Node child/aggregate summary")
    state = {"stacks": {}, "nodes": [], "occurrences": Counter(), "complete": Counter(), "plans": []}
    for event in events:
        node_event(event, state, children)
    require(all(not stack for stack in state["stacks"].values()), "Node case has no terminal event")
    validate_node_totals(state, aggregate[0], children)
    return state["nodes"]


def node_event(event, state, children):
    require(isinstance(event, dict) and isinstance(event.get("data"), dict), "Malformed Node event")
    kind, data = event.get("type"), event["data"]
    file = data.get("file")
    if kind == "test:complete":
        state["complete"][node_signature(data)] += 1
        return
    if kind == "test:plan":
        node_plan(data, state)
        return
    if kind not in ("test:start", "test:pass", "test:fail"):
        return
    require(file in children, "Node testcase has no completed child summary")
    stack = state["stacks"].setdefault(file, [])
    nesting = data.get("nesting")
    require(integer(nesting), "Invalid Node nesting")
    if kind == "test:start":
        require(nesting == len(stack), "Node ancestry changed or is incomplete")
        names = [entry["part"] for entry in stack]
        key = json.dumps([file, names, data.get("name")], ensure_ascii=False)
        state["occurrences"][key] += 1
        part = [data.get("name"), state["occurrences"][key]]
        node = {"id": json.dumps([file, names + [part]], ensure_ascii=False), "part": part,
                "start": data, "children": 0, "plan": None}
        if stack:
            stack[-1]["children"] += 1
        stack.append(node)
        return
    require(len(stack) == nesting + 1 and node_signature(stack[-1]["start"]) == node_signature(data),
            "Missing, duplicate or mismatched Node start/terminal")
    node = stack.pop()
    node.update(event=event, file=file, suite=data.get("details", {}).get("type") == "suite")
    require(state["complete"][node_signature(data)] == 1, "Missing or duplicate Node completion")
    state["complete"][node_signature(data)] -= 1
    if node["children"] or node["suite"]:
        require(node["plan"] == node["children"], "Node child plan differs from completed tree")
    state["nodes"].append(node)


def node_plan(data, state):
    require(integer(data.get("count")) and integer(data.get("nesting")), "Malformed Node plan")
    if data["nesting"] == 0:
        state["plans"].append(data["count"])
        return
    stack = state["stacks"].get(data.get("file"), [])
    require(len(stack) == data["nesting"] and stack[-1]["plan"] is None,
            "Missing parent or duplicate Node child plan")
    stack[-1]["plan"] = data["count"]


def validate_node_totals(state, aggregate, children):
    nodes = state["nodes"]
    for file, summary in children.items():
        group = [node for node in nodes if node["file"] == file]
        validate_node_summary(summary, group)
    validate_node_summary(aggregate, nodes)
    top = sum(node["start"]["nesting"] == 0 for node in nodes)
    require(state["plans"] == [top], "Missing, duplicate or inconsistent Node top-level plan")
    require(any(not node["suite"] and not node["children"] for node in nodes), "No real Node leaf cases")


def validate_node_summary(summary, nodes):
    counts = summary.get("counts", {})
    tests = [node for node in nodes if not node["suite"]]
    require(counts.get("tests") == len(tests) and counts.get("suites") == len(nodes) - len(tests),
            "Node summary differs from actual completed tree")
    require(all(counts.get(key) == 0 for key in ("cancelled", "skipped", "todo")), "Node cases skipped/cancelled/TODO")
    failed = sum(node["event"]["type"] == "test:fail" for node in tests)
    require(counts.get("failed") == failed and counts.get("passed") == len(tests) - failed
            and summary.get("success") is (failed == 0), "Contradictory Node summary outcomes")


def node_cases(events):
    return sorted(node["id"] for node in node_tree(events) if not node["suite"] and not node["children"])


def node_suite(events, expected):
    nodes = node_tree(events)
    actual = sorted(node["id"] for node in nodes if not node["suite"] and not node["children"])
    require(expected and actual == expected, "Changed Node leaf-case inventory")
    failed = False
    for node in nodes:
        event = node["event"]
        data = event["data"]
        require(not data.get("skip") and not data.get("todo"), "Skipped or TODO Node case")
        if event["type"] == "test:fail":
            failure = data.get("details", {}).get("error", {}).get("failureType")
            require(failure == "testCodeFailure" or (failure == "subtestsFailed" and node["children"]),
                    "Node timeout, cancellation or suite launch failure")
            failed = True
    return "killed" if failed else "surviving"

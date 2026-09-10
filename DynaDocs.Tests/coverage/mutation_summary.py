"""Normalize real mutation evidence into the schema-1 summary the mutation adapter publishes.

Three stages, each pure data in and data out, so a captured report replays anywhere:

1. `read_stryker_report` / `read_cosmic_session` read one engine's own evidence and return
   `{"projectRoot", "files", "rows", "gaps"}`, where every row is
   `{"path", "id", "mutator", "status", "span", "raw"}` with the engine's own path spelling, the
   specification's normalized status, and
   `span = {"startLine", "startColumn", "endLine", "endColumn"}` exactly as the engine reported it.
2. `map_report_paths` maps every vendor path onto the canonical inventory spelling against an
   explicitly supplied snapshot root -- never `cwd`, the environment or a module constant.
3. `normalize` applies the policy: the ordered report validation, the counts, one finding per
   non-killed valid mutant, the gaps, the score and the exit code.

`--read-cosmic-session` is the one subcommand the adapter runs out of process, because a Cosmic Ray
session is a session database rather than a report file; it takes the campaign's marker nonce
explicitly, exactly as the readers take the snapshot root.
"""
import argparse
import json
import os
import re
import sqlite3
import sys
import urllib.parse
from pathlib import Path

COUNT_FIELDS = ("generated", "valid", "killed", "survived", "noCoverage", "timeout",
                "compileError", "ignored", "runtimeError", "unrun", "unknown")
SPAN_FIELDS = ("startLine", "startColumn", "endLine", "endColumn")

# The one normalized status outside `valid`: a mutant the engine never got to run against a test.
INVALID = "compileError"

STRYKER_STATUSES = {"Killed": "killed", "Survived": "survived", "NoCoverage": "noCoverage",
                    "Timeout": "timeout", "RuntimeError": "runtimeError",
                    "CompileError": INVALID, "Ignored": "ignored", "Pending": "unrun",
                    "NotRun": "unrun"}

# The session stores SQLAlchemy `Enum(WorkerOutcome)` / `Enum(TestOutcome)`, which persists the
# member *name*, so a session spells `NO_TEST` where the API spells `no-test`. Both maps are the
# complete 8.7.0 membership: a value outside them is a schema departure, never a guess.
WORKER_OUTCOMES = {"NORMAL": "normal", "EXCEPTION": "exception", "ABNORMAL": "abnormal",
                   "NO_TEST": "no-test", "SKIPPED": "skipped"}
TEST_OUTCOMES = {"SURVIVED": "survived", "KILLED": "killed", "INCOMPETENT": "incompetent"}

SESSION_SCHEMA = {
    "work_items": {"job_id"},
    "mutation_specs": {"module_path", "operator_name", "operator_args", "occurrence",
                       "start_pos_row", "start_pos_col", "end_pos_row", "end_pos_col",
                       "definition_name", "job_id"},
    "work_results": {"worker_outcome", "output", "test_outcome", "diff", "job_id"},
}

SESSION_QUERY = """
select items.job_id as job, specs.job_id as mutation, results.job_id as result,
       specs.module_path as module_path, specs.operator_name as operator_name,
       specs.start_pos_row as start_line, specs.start_pos_col as start_column,
       specs.end_pos_row as end_line, specs.end_pos_col as end_column,
       results.worker_outcome as worker_outcome, results.test_outcome as test_outcome,
       results.output as output
  from work_items as items
  left join mutation_specs as specs on specs.job_id = items.job_id
  left join work_results as results on results.job_id = items.job_id
 order by items.rowid
"""

MARKER = "##DYDO-SUITE-COMPLETE {nonce} exit="
MARKER_END = "##"
DECIMAL = re.compile(r"-?[0-9]+")

UNMAPPABLE = "unmappable report path: "


def read_stryker_report(path, report=None):
    """Read one Stryker.NET or StrykerJS mutation-testing-report JSON file.

    `report` is the label recorded in each row's `raw`, defaulting to the file's name.
    """
    path = Path(path)
    report = report if report is not None else path.name
    malformed = _reading(None, [], [], [{"reason": "malformed report", "path": report}])
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, ValueError):
        return malformed
    if not isinstance(document, dict) or not isinstance(document.get("projectRoot"), str) \
            or not isinstance(document.get("files"), dict):
        return malformed
    rows = []
    for key, entry in document["files"].items():
        if not isinstance(entry, dict) or not isinstance(entry.get("mutants"), list):
            return malformed
        for mutant in entry["mutants"]:
            row = _stryker_row(key, mutant, report)
            if row is None:
                return malformed
            rows.append(row)
    return _reading(document["projectRoot"], list(document["files"]), rows, [])


def read_cosmic_session(path, marker_nonce, report=None):
    """Read one Cosmic Ray session, reading each `killed` row against `marker_nonce`.

    The reading is taken with the standard library against the schema pinned to Cosmic Ray 8.7.0
    by `mutation/requirements.lock`, and any departure from that schema -- a missing table, an
    unexpected column, an enum value 8.7.0 does not spell -- is invalid measurement rather than a
    best-effort parse.
    """
    path = Path(path)
    report = report if report is not None else path.name
    malformed = _reading(None, [], [], [{"reason": "malformed report", "path": report}])
    try:
        connection = sqlite3.connect(_read_only_uri(path), uri=True)
    except sqlite3.Error:
        return malformed
    connection.row_factory = sqlite3.Row
    try:
        if not _pinned_session_schema(connection):
            return malformed
        items = connection.execute("select count(*) from work_items").fetchone()[0]
        records = connection.execute(SESSION_QUERY).fetchall()
    except sqlite3.Error:
        return malformed
    finally:
        connection.close()
    if len(records) != items or any(record["mutation"] is None for record in records):
        return malformed
    if any(record["result"] is None for record in records):
        return _reading(None, [], [], [{"reason": "partial report", "path": report}])

    files, rows, gaps = [], [], []
    for record in records:
        span = tuple(record[field] for field in ("start_line", "start_column",
                                                 "end_line", "end_column"))
        if not isinstance(record["module_path"], str) \
                or not isinstance(record["operator_name"], str) \
                or not all(isinstance(value, int) for value in span):
            return malformed
        if record["module_path"] not in files:
            files.append(record["module_path"])
        status = _cosmic_status(record, marker_nonce)
        if status is None:
            gaps.append({"reason": "engine could not run mutant", "path": record["module_path"]})
            continue
        rows.append({"path": record["module_path"], "id": record["job"],
                     "mutator": record["operator_name"], "status": status,
                     "span": dict(zip(SPAN_FIELDS, span)),
                     "raw": {"report": report, "id": record["job"]}})
    return _reading(None, files, rows, gaps)


def map_report_paths(reading, snapshot_root, inventory_paths):
    """Map every vendor path in a reading onto the one inventory path it resolves to.

    A key outside the snapshot root, a remainder no inventory row spells, or a match that only
    holds after case folding is a gap and its file is dropped, so no vendor spelling reaches the
    summary. Reader gaps carry the report label or the `module-path` the adapter itself generated,
    so they are canonical already.
    """
    root = os.path.realpath(str(snapshot_root))
    inventory = set(inventory_paths)
    canonical, gaps = {}, []
    for key in reading["files"]:
        path = _canonical_path(key, reading["projectRoot"], root, inventory)
        if path is None:
            gaps.append({"reason": UNMAPPABLE + key})
        else:
            canonical[key] = path
    return _reading(reading["projectRoot"],
                    [canonical[key] for key in reading["files"] if key in canonical],
                    [{**row, "path": canonical[row["path"]]} for row in reading["rows"]
                     if row["path"] in canonical],
                    reading["gaps"] + gaps)


def normalize(reading, selected, engine, substantive=True):
    """Apply the mutation policy to a mapped reading of one campaign.

    The specification's numbered report validation, in its order: the reader's own refusal, then
    the zero-mutant rule, then per-file presence, then the foreign-file rule. One departure is
    recorded: an unmappable report path is judged last, because the mapper drops that file and a
    *selected* file it dropped is named by the rule that explains it (`partial report`) rather
    than by a vendor key. Every one of them is exit 2, so only the reason moves.
    """
    unmappable = [gap for gap in reading["gaps"] if gap["reason"].startswith(UNMAPPABLE)]
    refusal = [gap for gap in reading["gaps"] if not gap["reason"].startswith(UNMAPPABLE)]
    if refusal:
        return _refused(refusal)

    selected = list(selected)
    counts = _counted(row for row in reading["rows"] if row["path"] in selected)
    if counts["generated"] == 0:
        if substantive:
            return _outcome(counts, gaps=[{"reason": "zero-mutant campaign"}])
        return _outcome(counts, gaps=unmappable)
    if counts["valid"] == 0:
        return _outcome(counts, gaps=[{"reason": "all mutants invalid"}])

    missing = [{"reason": "partial report", "path": path}
               for path in selected if path not in reading["files"]]
    if missing:
        return _refused(missing)
    foreign, witness = _foreign(reading, selected, engine)
    if foreign:
        return _refused(foreign)
    if unmappable:
        return _refused(unmappable)

    findings = [{"gate": "mutation", "path": row["path"], "span": row["span"],
                 "mutator": row["mutator"], "status": row["status"], "raw": row["raw"]}
                for row in reading["rows"]
                if row["path"] in selected and row["status"] not in ("killed", INVALID)]
    return _outcome(counts, findings=findings, witness=witness)


def main(argv=None):
    """--read-cosmic-session <session> --marker-nonce <nonce> --output <json>."""
    parser = argparse.ArgumentParser(description="Read one Cosmic Ray session as JSON.")
    parser.add_argument("--read-cosmic-session", required=True)
    parser.add_argument("--marker-nonce", required=True)
    parser.add_argument("--output", required=True)
    arguments = parser.parse_args(argv)
    reading = read_cosmic_session(arguments.read_cosmic_session, arguments.marker_nonce)
    Path(arguments.output).write_text(json.dumps(reading, indent=2) + "\n", encoding="utf-8")
    return 2 if reading["gaps"] else 0


def _reading(project_root, files, rows, gaps):
    return {"projectRoot": project_root, "files": files, "rows": rows, "gaps": gaps}


def _stryker_row(key, mutant, report):
    """One mutation-testing-report mutant, or None when the report cannot be believed."""
    if not isinstance(mutant, dict):
        return None
    span = _stryker_span(mutant.get("location"))
    identity, mutator, native = mutant.get("id"), mutant.get("mutatorName"), mutant.get("status")
    if span is None or not isinstance(identity, str) or not isinstance(mutator, str) \
            or not isinstance(native, str):
        return None
    return {"path": key, "id": identity, "mutator": mutator,
            "status": STRYKER_STATUSES.get(native, "unknown"), "span": span,
            "raw": {"report": report, "id": identity}}


def _stryker_span(location):
    if not isinstance(location, dict):
        return None
    start, end = location.get("start"), location.get("end")
    if not isinstance(start, dict) or not isinstance(end, dict):
        return None
    values = (start.get("line"), start.get("column"), end.get("line"), end.get("column"))
    if not all(isinstance(value, int) for value in values):
        return None
    return dict(zip(SPAN_FIELDS, values))


def _canonical_path(key, project_root, root, inventory):
    """One report key as the inventory spells it, or None when the campaign cannot own it.

    A relative key joins onto its own report's `projectRoot`; an absolute one stands alone.
    """
    resolved = os.path.realpath(os.path.join(project_root or root, key))
    prefix = os.path.normcase(root).rstrip(os.sep) + os.sep
    if not os.path.normcase(resolved).startswith(prefix):
        return None
    remainder = resolved[len(prefix):].replace(os.sep, "/")
    return remainder if remainder in inventory else None


def _read_only_uri(path):
    return "file:" + urllib.parse.quote(path.resolve().as_posix()) + "?mode=ro"


def _pinned_session_schema(connection):
    for table, columns in SESSION_SCHEMA.items():
        found = {row["name"] for row in
                 connection.execute("select name from pragma_table_info(?)", (table,))}
        if found != columns:
            return False
    return True


def _cosmic_status(record, marker_nonce):
    """The normalized status of one session row, or None for a mutant the engine could not run.

    Worker outcome first, then test outcome, so no row matches two rules and none matches nothing.
    """
    worker = WORKER_OUTCOMES.get(record["worker_outcome"])
    if worker in ("skipped", "no-test"):
        return "unrun"
    if worker != "normal":
        return None
    test = TEST_OUTCOMES.get(record["test_outcome"])
    if test == "survived":
        return "survived"
    if test == "killed":
        return _cosmic_kill(record["output"], marker_nonce)
    return None


def _cosmic_kill(output, marker_nonce):
    """The four readings of a `killed` row, in the specification's order.

    Cosmic Ray calls every nonzero exit of the test process `killed`, which is equally what a
    crashed, cut-short or never-started suite does; only this campaign's own completion marker,
    reporting a nonzero suite exit, says a mutant was really killed.
    """
    if output == "timeout":
        return "timeout"
    code = _completion_code(output, marker_nonce)
    if code is None or code == 0:
        return "runtimeError"
    return "killed"


def _completion_code(output, marker_nonce):
    """The suite exit code this campaign's completion marker reports, when it is the last line.

    Last-non-empty-line is the shape rule and the nonce is the defence: a marker-shaped line
    bearing another campaign's nonce was not written by this campaign's generated suite runner.
    """
    lines = [line for line in (output or "").splitlines() if line.strip()]
    if not lines:
        return None
    last = lines[-1].rstrip()
    head = MARKER.format(nonce=marker_nonce)
    if not (last.startswith(head) and last.endswith(MARKER_END)):
        return None
    code = last[len(head):-len(MARKER_END)]
    return int(code) if DECIMAL.fullmatch(code) else None


def _counted(rows):
    counts = dict.fromkeys(COUNT_FIELDS, 0)
    for row in rows:
        counts["generated"] += 1
        counts[row["status"]] += 1
        if row["status"] != INVALID:
            counts["valid"] += 1
    return counts


def _foreign(reading, selected, engine):
    """The files the report names that the campaign did not select.

    Stryker.NET's `mutate` is a mutant filter rather than a file filter, so an unselected file
    whose every mutant it removed stays in the report and is witnessed, not counted; any other
    status there, and any unselected file at all under the other two engines, is invalid.
    """
    gaps, witness = [], []
    for path in reading["files"]:
        if path in selected:
            continue
        if engine != "stryker-net":
            gaps.append({"reason": f"foreign file: {path}", "path": path})
        elif all(row["status"] == "ignored"
                 for row in reading["rows"] if row["path"] == path):
            witness.append({"path": path, "reason": "removed by mutate filter"})
        else:
            gaps.append({"reason": f"foreign mutant: {path}", "path": path})
    return gaps, witness


def _refused(gaps):
    """A campaign we cannot believe publishes no counts: the refusal is the whole measurement."""
    return _outcome(_counted(()), gaps=gaps)


def _outcome(counts, findings=(), gaps=(), witness=()):
    return {"counts": counts, "findings": list(findings), "gaps": list(gaps),
            "score": 100 * counts["killed"] / counts["valid"] if counts["valid"] else None,
            "witness": list(witness), "measurementComplete": not gaps,
            "exitCode": 2 if gaps else 1 if findings else 0}


if __name__ == "__main__":
    sys.exit(main())

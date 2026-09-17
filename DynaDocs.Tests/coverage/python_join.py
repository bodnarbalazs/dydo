"""Join native coverage.py facts to exact flat monitored callable counters."""
import argparse
import hashlib
import json
import sys
from pathlib import Path

from python_metrics import module_scores, source_metrics
from python_runtime import callable_inventory, module_inventory


def _span(row):
    return tuple(row[key] for key in ("line", "column", "end_line", "end_column"))


def _hits(value):
    if type(value) is not int or value < 0:
        raise ValueError("Invalid Python execution counter")
    return value


def _checked_rows(source, path, counter, kind="callables"):
    inventory = callable_inventory(source, path) if kind == "callables" else [module_inventory(source, path)]
    expected = {row["id"]: row for row in inventory}
    actual = [row for row in counter[kind] if row["path"] == path]
    if len(actual) != len(expected) or {row["id"] for row in actual} != set(expected):
        raise ValueError(f"Incomplete Python callable counter inventory: {path}")
    for row in actual:
        original = expected[row["id"]]
        if (_span(row) != _span(original) or row["kind"] != original["kind"]
                or set(row["body_lines"]) != set(original["body_lines"])):
            raise ValueError(f"Mismatched Python callable body inventory: {path}:{row['id']}")
        _hits(row["execution_count"])
        for hits in row["body_lines"].values():
            _hits(hits)
    return actual


def _methods(source, path, counter):
    facts = source_metrics(source)
    measured = {_span(row): row for row in _checked_rows(source, path, counter)}
    if len(measured) != len(facts["methods"]) or set(measured) != {_span(row) for row in facts["methods"]}:
        raise ValueError(f"Missing or ambiguous Python source/compiled callable join: {path}")
    methods = []
    for metric in facts["methods"]:
        row = measured[_span(metric)]
        if not row["body_lines"]:
            raise ValueError(f"Callable has no measured body denominator: {path}:{row['id']}")
        methods.append({**metric, "id": row["id"], "kind": row["kind"],
                        "covered": sum(hits > 0 for hits in row["body_lines"].values()),
                        "total": len(row["body_lines"]), "body_lines": row["body_lines"],
                        "execution_count": row["execution_count"],
                        "monitoring_branches": row["branches"],
                        "physical_branches": row["physical_branches"]})
    entry = _checked_rows(source, path, counter, "modules")[0]
    if entry["body_lines"]:
        methods.append({**entry, **module_scores(source), "parameters": 0,
                        "constructor": False,
                        "covered": sum(hits > 0 for hits in entry["body_lines"].values()),
                        "total": len(entry["body_lines"])})
    return methods


def _native_points(row):
    executed, missing = set(row["executed_lines"]), set(row["missing_lines"])
    if executed & missing or any(type(line) is not int or line < 1 for line in executed | missing):
        raise ValueError("Invalid coverage.py line partition")
    lines = {str(line): int(line in executed) for line in sorted(executed | missing)}
    hit_edges = {tuple(edge) for edge in row["executed_branches"]}
    missed_edges = {tuple(edge) for edge in row["missing_branches"]}
    if hit_edges & missed_edges or any(len(edge) != 2 for edge in hit_edges | missed_edges):
        raise ValueError("Invalid coverage.py branch partition")
    branches = {f"{start}:{end}": int((start, end) in hit_edges)
                for start, end in sorted(hit_edges | missed_edges)}
    summary = row["summary"]
    if summary["num_statements"] != len(lines) or summary["num_branches"] != len(branches):
        raise ValueError("Incomplete coverage.py executable denominator")
    return lines, branches


def collect(root, output, source_paths):
    root, output = Path(root).resolve(), Path(output).resolve()
    report = json.loads((output / "coverage.json").read_text(encoding="utf-8"))
    counter = json.loads((output / "counters.json").read_text(encoding="utf-8"))
    if report.get("meta", {}).get("version") != "7.16.0" or report["meta"].get("branch_coverage") is not True:
        raise ValueError("Unexpected coverage.py report semantics")
    native = {(root / path).resolve(): row for path, row in report["files"].items()}
    expected = [(root / path).resolve() for path in source_paths]
    if set(native) != set(expected) or len(native) != len(expected):
        raise ValueError("Incomplete Python native module coverage inventory")
    fingerprint = hashlib.sha256("".join(
        hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(expected)).encode()).hexdigest()
    modules = []
    for path in expected:
        source = path.read_text(encoding="utf-8-sig")
        if counter["sources"].get(str(path)) != hashlib.sha256(source.encode("utf-8")).hexdigest():
            raise ValueError(f"Python source/counter hash mismatch: {path}")
        lines, branches = _native_points(native[path])
        modules.append({"path": path.relative_to(root).as_posix(), "language": "python",
                        "executable": bool(lines), "lines": lines, "branches": branches,
                        "methods": _methods(source, str(path), counter),
                        "source_fingerprint": fingerprint})
    return modules


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--sources-json", required=True)
    args = parser.parse_args()
    try:
        print(json.dumps(collect(args.root, args.output, json.loads(args.sources_json))))
        return 0
    except (ValueError, KeyError, OSError, TypeError) as error:
        print(error, file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

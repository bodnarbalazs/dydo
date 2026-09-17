"""One ordinary coverage.py collector and callable witness per Python process."""
import argparse
import atexit
import hashlib
import json
import os
import subprocess
import sys
import uuid
from pathlib import Path

_ACTIVE = None


def _identity(row):
    return row["path"], row["id"]


def _shape(row):
    return {key: value for key, value in row.items()
            if key not in ("execution_count", "body_lines", "branches", "physical_branches")}


def _hits(value):
    if type(value) is not int or value < 0:
        raise ValueError("Invalid Python execution counter")
    return value


def _original_counters(receipt, kind):
    rows = receipt.get(kind, [])
    original = {_identity(row): row for row in rows}
    if len(original) != len(rows):
        raise ValueError("Duplicate Python counter identity")
    return original


def _copy_counters(original):
    return {identity: {**row, "body_lines": dict(row["body_lines"]),
                       "branches": list(row["branches"]),
                       "physical_branches": list(row["physical_branches"])}
            for identity, row in original.items()}


def _merge_counter_row(target, expected, row):
    if _shape(row) != _shape(expected) or set(row["body_lines"]) != set(expected["body_lines"]):
        raise ValueError("Python counter body inventory mismatch")
    target["execution_count"] += _hits(row["execution_count"])
    for line, hits in row["body_lines"].items():
        target["body_lines"][line] += _hits(hits)
    for branch_kind in ("branches", "physical_branches"):
        for edge in row[branch_kind]:
            if edge not in target[branch_kind]:
                target[branch_kind].append(edge)


def _merge_receipt(first, receipt, kind, original, accumulated):
    if receipt.get("schema") != 1 or receipt.get("sources") != first["sources"]:
        raise ValueError("Python counter source inventory mismatch")
    rows = receipt.get(kind, [])
    actual = {_identity(row): row for row in rows}
    if len(actual) != len(rows) or set(actual) != set(original):
        raise ValueError("Python counter callable inventory mismatch")
    for identity, row in actual.items():
        _merge_counter_row(accumulated[identity], original[identity], row)


def _combine_counter_kind(first, receipts, kind):
    original = _original_counters(first, kind)
    accumulated = _copy_counters(original)
    for receipt in receipts:
        _merge_receipt(first, receipt, kind, original, accumulated)
    return [accumulated[key] for key in sorted(accumulated)]


def combine_counters(receipts):
    """Union flat per-process counters after exact complete source/body matching."""
    if not receipts:
        raise ValueError("Missing Python counter files")
    first = receipts[0]
    if first.get("schema") != 1 or not isinstance(first.get("sources"), dict):
        raise ValueError("Invalid Python counter schema")
    output = {"schema": 1, "sources": first["sources"], "callables": [], "modules": []}
    for kind in ("callables", "modules"):
        output[kind] = _combine_counter_kind(first, receipts[1:], kind)
    return output


def startup_from_environment():
    """Start once in the current process when the invocation config is inherited."""
    global _ACTIVE
    config_path = os.environ.get("DYDO_PYTHON_COVERAGE_CONFIG")
    if not config_path or _ACTIVE is not None:
        return _ACTIVE
    import coverage
    from python_runtime import CallableWitness
    config = json.loads(Path(config_path).read_text(encoding="utf-8"))
    if config.get("schema") != 1 or coverage.__version__ != "7.16.0":
        raise RuntimeError("Python campaign requires schema 1 and coverage.py 7.16.0")
    sources = {path: Path(path).read_text(encoding="utf-8-sig") for path in config["sources"]}
    collector = coverage.Coverage(data_file=str(Path(config["output"]) / ".coverage"),
                                  data_suffix=True, branch=True, source=[config["root"]],
                                  config_file=False)
    witness = CallableWitness(sources)
    collector.start()
    witness.__enter__()
    _ACTIVE = collector, witness

    def finish():
        global _ACTIVE
        if _ACTIVE is None:
            return
        collector.stop()
        witness.__exit__(None, None, None)
        collector.save()
        receipt = {
            "schema": 1,
            "sources": {path: hashlib.sha256(text.encode("utf-8")).hexdigest()
                        for path, text in sources.items()},
            "callables": witness.rows(), "modules": witness.module_rows(),
        }
        output = Path(config["output"]) / f"counter-{os.getpid()}-{uuid.uuid4().hex}.json"
        output.write_text(json.dumps(receipt, sort_keys=True), encoding="utf-8")
        _ACTIVE = None

    atexit.register(finish)
    return _ACTIVE


def collect(root, output, source_paths, command):
    """Run one ordinary command, then combine native data and flat process counters."""
    import coverage
    root, output = Path(root).resolve(), Path(output).resolve()
    if output.exists() or not source_paths or not command:
        raise ValueError("Python campaign requires a new output and nonempty sources/command")
    output.mkdir(parents=True)
    sources = []
    for relative in source_paths:
        path = (root / relative).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError(f"Invalid Python target source: {relative}")
        sources.append(str(path))
    config = {"schema": 1, "root": str(root), "output": str(output), "sources": sources}
    config_path = output / "config.json"
    config_path.write_text(json.dumps(config, indent=2), encoding="utf-8")
    startup = output / "startup"
    startup.mkdir()
    (startup / "sitecustomize.py").write_text(
        "from python_coverage import startup_from_environment\nstartup_from_environment()\n",
        encoding="utf-8")
    env = dict(os.environ)
    env["DYDO_PYTHON_COVERAGE_CONFIG"] = str(config_path)
    tools = Path(__file__).resolve().parent
    env["PYTHONPATH"] = os.pathsep.join([str(startup), str(tools), env.get("PYTHONPATH", "")])
    argv = [sys.executable if item == "{python}" else item for item in command]
    result = subprocess.run(argv, cwd=root, env=env)
    data = coverage.Coverage(data_file=str(output / ".coverage"), branch=True,
                             source=[str(root)], config_file=False)
    data.combine(data_paths=[str(output)], strict=True, keep=True)
    data.json_report(morfs=sources, outfile=str(output / "coverage.json"))
    receipts = [json.loads(path.read_text(encoding="utf-8"))
                for path in sorted(output.glob("counter-*.json"))]
    combined = combine_counters(receipts)
    (output / "counters.json").write_text(
        json.dumps(combined, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return result.returncode


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--sources-json", required=True)
    parser.add_argument("--command-json", required=True)
    args = parser.parse_args()
    try:
        return collect(args.root, args.output, json.loads(args.sources_json),
                       json.loads(args.command_json))
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(error, file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

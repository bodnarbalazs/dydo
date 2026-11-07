#!/usr/bin/env python3
"""The required Release performance-proof lane (DYD-840).

`run` builds and runs exactly the mapped proofs once, in Release and uninstrumented, through the
isolated testing facade, then judges the run's TRX. `verify` judges a TRX alone. Evidence that is
missing, unreadable, failed, partial, unmapped or not the Release build fails the lane. The map,
release-proofs.json, also names the filter that keeps the Release proofs out of instrumented
coverage, where their witnesses run instead.
"""
import argparse
import hashlib
import json
import subprocess
import sys
import xml.etree.ElementTree as ElementTree
from collections import Counter
from pathlib import Path, PurePosixPath

TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parents[1]
MAP = TOOLS / "release-proofs.json"
RESULTS = ROOT / "DynaDocs.Tests/coverage/results/release-proof"
TRX_NAME = "release-proof.trx"
LANES = ("Release", "Shared")
TRX = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def _text(value, field):
    if not isinstance(value, str) or not value:
        raise ValueError(f"release proof map needs a nonempty {field}")
    return value


def _proof(row):
    if not isinstance(row, dict) or set(row) != {"proof", "lane", "cases", "assertions", "witness"}:
        raise ValueError(f"release proof row needs exactly proof, lane, cases, assertions and witness: {row!r}")
    name = _text(row["proof"], "proof")
    _text(row["witness"], "witness")
    if row["lane"] not in LANES:
        raise ValueError(f"{name}: lane must be one of {LANES}")
    if type(row["cases"]) is not int or row["cases"] < 1:
        raise ValueError(f"{name}: cases must be a positive integer")
    if not isinstance(row["assertions"], list) or not row["assertions"]:
        raise ValueError(f"{name}: every proof names its assertions")
    for assertion in row["assertions"]:
        _text(assertion, "assertion")
    if row["lane"] == "Shared" and row["witness"] != name:
        raise ValueError(f"{name}: a shared proof is its own coverage witness")
    return name


def load_map(path=MAP):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    fields = {"schema", "decision", "trait", "releaseFilter", "coverageFilter", "assembly", "laneCheck", "proofs"}
    if not isinstance(data, dict) or set(data) != fields or data["schema"] != 1:
        raise ValueError("release proof map must use closed schema 1")
    for field in fields - {"schema", "proofs"}:
        _text(data[field], field)
    if not isinstance(data["proofs"], list) or not data["proofs"]:
        raise ValueError("release proof map needs proofs")
    names = [_proof(row) for row in data["proofs"]] + [data["laneCheck"]]
    if len(set(names)) != len(names):
        raise ValueError("release proof map names a test twice")
    return data


def expected_counts(mapping):
    """Each mapped test's result count in one lane run: its cases, and the lane check once."""
    return {**{row["proof"]: row["cases"] for row in mapping["proofs"]}, mapping["laneCheck"]: 1}


def coverage_filter(path=MAP):
    """The instrumented suite's selection: everything but the Release-lane proofs."""
    return load_map(path)["coverageFilter"]


def facade_command(mapping, results):
    return [sys.executable, str(TOOLS / "gap_check.py"), "test", "--stack", "dotnet", "--",
            "-c", "Release", "--filter", mapping["releaseFilter"],
            "--logger", f"trx;LogFileName={TRX_NAME}", "--results-directory", str(results)]


def _method(definition):
    method = definition.find("t:TestMethod", TRX)
    if method is None:
        raise ValueError(f"TRX test {definition.get('id')} has no TestMethod")
    # A theory's case names its arguments after the method; the method is the proof.
    return f"{method.get('className')}.{method.get('name', '').split('(', 1)[0]}"


def read_trx(path):
    """The TRX's definitions (id: method, storage), results (test id, outcome) and counters."""
    root = ElementTree.parse(path).getroot()
    definitions = {}
    for test in root.iterfind("t:TestDefinitions/t:UnitTest", TRX):
        identity = test.get("id")
        if not identity:
            raise ValueError("A TRX definition has no id")
        if identity in definitions:
            raise ValueError(f"Duplicate TRX definition id {identity}")
        definitions[identity] = (_method(test), test.get("storage", ""))
    results = [(result.get("testId"), result.get("outcome"))
               for result in root.iterfind("t:Results/t:UnitTestResult", TRX)]
    counters = root.find("t:ResultSummary/t:Counters", TRX)
    return definitions, results, None if counters is None else dict(counters.attrib)


def _counter_problems(counters, results):
    if counters is None:
        return ["The TRX has no ResultSummary/Counters"]
    total, passed = counters.get("total"), counters.get("passed")
    problems = [] if total == passed else [f"TRX counters show total {total} but passed {passed}"]
    if total != str(len(results)):
        problems.append(f"TRX counters show total {total} but the TRX has {len(results)} results")
    return problems


def _identity_problems(definitions, results):
    counts = Counter(test for test, _ in results)
    problems = [] if all(counts) else ["A TRX result has no testId"]
    problems += [f"TRX test {test} has {count} results, not exactly one"
                 for test, count in counts.items() if count != 1]
    problems += [f"TRX test {test} has no result" for test in definitions if test not in counts]
    problems += [f"TRX result names undefined test {test}" for test in counts if test not in definitions]
    return problems


def _assembly_problems(definitions, assembly):
    # The TRX records each test's storage path lowercased.
    suffix = PurePosixPath(assembly.casefold()).parts
    return sorted({f"{method} ran from {storage}, not the Release build {assembly}"
                   for method, storage in definitions.values()
                   if PurePosixPath(storage.casefold().replace("\\", "/")).parts[-len(suffix):] != suffix})


def _count_problems(counts, expected):
    problems = [f"{name} produced {counts.get(name, 0)} results, not its {cases} cases"
                for name, cases in expected.items() if counts.get(name, 0) != cases]
    return problems + [f"{name} ran in the lane but is not a mapped proof"
                       for name in sorted(set(counts) - set(expected))]


def verify(trx, mapping):
    """Problems with a lane's evidence, none when every mapped case ran in Release and passed."""
    try:
        definitions, results, counters = read_trx(trx)
    except (OSError, ElementTree.ParseError, ValueError) as error:
        return [f"No readable Release proof evidence at {trx}: {error}"], {}
    counts = {}
    problems = [] if results else ["The lane produced zero results"]
    problems += _identity_problems(definitions, results)
    for test, outcome in results:
        method = definitions.get(test, ("an undefined test " + str(test), ""))[0]
        counts[method] = counts.get(method, 0) + 1
        if outcome != "Passed":
            problems.append(f"{method} is {outcome}, not Passed")
    if len(definitions) != len(results):
        problems.append(f"The TRX defines {len(definitions)} tests but has {len(results)} results")
    problems += _counter_problems(counters, results)
    problems += _assembly_problems(definitions, mapping["assembly"])
    return problems + _count_problems(counts, expected_counts(mapping)), counts


def _digest(path):
    try:
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    except OSError:
        return None


def _candidate():
    result = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True)
    return result.stdout.strip() if result.returncode == 0 else None


def judge(trx, mapping, receipt, command=None, status=0):
    """Writes the lane's receipt and returns its exit: 0 only for complete, passing Release evidence."""
    problems, counts = verify(trx, mapping)
    if status:
        problems.insert(0, f"The isolated Release run exited {status}")
    payload = {"schema": 1, "candidate": _candidate(), "command": command, "trx": str(trx),
               "trxSha256": _digest(trx), "expected": expected_counts(mapping), "counts": counts,
               "problems": problems, "verdict": "fail" if problems else "pass"}
    Path(receipt).parent.mkdir(parents=True, exist_ok=True)
    Path(receipt).write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    for problem in problems:
        print(f"release proof: {problem}", file=sys.stderr)
    print(f"Release proof {payload['verdict']}: {sum(counts.values())} results; receipt {receipt}")
    return 1 if problems else 0


def run(results=RESULTS, mapping_path=MAP):
    mapping = load_map(mapping_path)
    results = Path(results).resolve()
    results.mkdir(parents=True, exist_ok=True)
    trx = results / TRX_NAME
    # Evidence from an earlier run is never this run's.
    trx.unlink(missing_ok=True)
    command = facade_command(mapping, results)
    status = subprocess.run(command, cwd=ROOT).returncode
    return judge(trx, mapping, results / "receipt.json", command, status)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("run").add_argument("--results", default=str(RESULTS))
    check = commands.add_parser("verify")
    check.add_argument("--trx", required=True)
    check.add_argument("--receipt", required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "run":
            return run(args.results)
        return judge(Path(args.trx), load_map(), args.receipt)
    except (OSError, ValueError) as error:
        print(f"release proof: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

"""Ordinary coverage.py collection includes child-only and never-called callables."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_coverage import combine_counters


def _facade(root, output, sources, command):
    tools = Path(__file__).resolve().parents[1]
    python = tools.parents[1] / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
    return subprocess.run([
        str(python), str(tools / "python_coverage.py"), "--root", str(root),
        "--output", str(output), "--sources-json", json.dumps(sources),
        "--command-json", json.dumps(command),
    ], cwd=root, text=True, capture_output=True)


def _counter_row(**changes):
    return {"id": "f:1:0", "path": "a.py", "line": 1, "column": 0, "end_line": 1,
            "end_column": 18, "kind": "FunctionDef", "execution_count": 1,
            "body_lines": {"1": 1}, "branches": [], "physical_branches": [], **changes}


def _counter_receipt(rows, **changes):
    return {"schema": 1, "sources": {"a.py": "a" * 64}, "callables": rows, "modules": [], **changes}


_COLD_BOOTSTRAP = """\
import importlib.util
import json
import os
import sys
from pathlib import Path

tools, work = (Path(argument).resolve() for argument in sys.argv[1:3])
sys.path[:0] = [str(tools), str(work)]
inherited = os.environ.get("DYDO_PYTHON_COVERAGE_CONFIG")
for cached in ("python_runtime", "positions"):
    sys.modules.pop(cached, None)
spec = importlib.util.spec_from_file_location("cold_bootstrap", tools / "python_coverage.py")
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)

os.environ["DYDO_PYTHON_COVERAGE_CONFIG"] = str(work / "unpinned.json")
refusal = ""
try:
    bootstrap.startup_from_environment()
except RuntimeError as error:
    refusal = str(error)
os.environ["DYDO_PYTHON_COVERAGE_CONFIG"] = str(work / "config.json")
session = bootstrap.startup_from_environment()
restarted = session is not bootstrap.startup_from_environment()

import subject

subject.only()
(work / "observed.json").write_text(json.dumps({
    "pid": os.getpid(), "inherited": inherited, "refusal": refusal,
    "started": session is not None, "restarted": restarted,
    "runtime": sys.modules["python_runtime"].__spec__.origin,
    "positions": sys.modules["positions"].__spec__.origin}), encoding="utf-8")
"""


class PythonCoverageTests(unittest.TestCase):
    def campaign(self, root, output, sources, command):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        result = subprocess.run([
            str(python), str(tools / "python_coverage.py"),
            "--root", str(root), "--output", str(output),
            "--sources-json", json.dumps(sources), "--command-json", json.dumps(command),
        ], cwd=root, text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return json.loads((output / "counters.json").read_text())

    def test_project_facade_tests_execute_the_canonical_source_identity(self):
        root = Path(__file__).resolve().parents[3]
        source = "DynaDocs.Tests/coverage/gap_check.py"
        test = "DynaDocs.Tests/coverage/tests/test_testing_facade.py"
        with tempfile.TemporaryDirectory() as folder:
            counters = self.campaign(root, Path(folder) / "evidence", [source],
                                     ["{python}", test, "TestingFacadeTests.test_help"])
        main = next(row for row in counters["callables"] if row["id"].startswith("main:"))
        self.assertGreater(main["execution_count"], 0)

    def test_flat_counter_union_requires_identical_complete_inventory(self):
        row = {"schema": 1, "sources": {"a.py": "a" * 64}, "callables": [{
            "id": "f:1:0", "path": "a.py", "line": 1, "column": 0,
            "end_line": 1, "end_column": 18, "kind": "FunctionDef",
            "execution_count": 1, "body_lines": {"1": 1}, "branches": [],
            "physical_branches": []}], "modules": []}
        merged = combine_counters([row, row])
        self.assertEqual(2, merged["callables"][0]["execution_count"])
        changed = json.loads(json.dumps(row))
        changed["callables"] = []
        with self.assertRaisesRegex(ValueError, "inventory"):
            combine_counters([row, changed])

    def test_real_unittest_and_python_child_share_native_campaign(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text(
                "def direct(): return 1\ndef child_only(): return 2\ndef never(): return 3\n"
                "if __name__ == '__main__': child_only()\n", encoding="utf-8")
            (root / "test_subject.py").write_text(
                "import subprocess,sys,unittest,subject\nclass T(unittest.TestCase):\n"
                " def test_direct_and_child(self):\n  self.assertEqual(1,subject.direct())\n"
                "  subprocess.run([sys.executable,'subject.py'],check=True)\n", encoding="utf-8")
            output = root / "evidence"
            counters = self.campaign(root, output, ["subject.py"],
                                     ["{python}", "-m", "unittest", "discover", "-s", "."])
            calls = {row["id"].split(":", 1)[0]: row["execution_count"]
                     for row in counters["callables"]}
            self.assertGreater(calls["direct"], 0)
            self.assertGreater(calls["child_only"], 0)
            self.assertEqual(0, calls["never"])
            native = json.loads((output / "coverage.json").read_text())
            self.assertTrue(native["meta"]["branch_coverage"])
            joined = subprocess.run([
                str(python), str(tools / "python_join.py"), "--root", str(root),
                "--output", str(output), "--sources-json", '["subject.py"]'],
                cwd=root, text=True, capture_output=True)
            self.assertEqual(0, joined.returncode, joined.stderr)
            module = json.loads(joined.stdout)[0]
            never = next(row for row in module["methods"] if row["id"].startswith("never:"))
            self.assertEqual((0, 0), (never["execution_count"], never["covered"]))
            self.assertTrue(module["lines"])

            def rejoin():
                return subprocess.run([
                    str(python), str(tools / "python_join.py"), "--root", str(root),
                    "--output", str(output), "--sources-json", '["subject.py"]'],
                    cwd=root, text=True, capture_output=True)

            original = (root / "subject.py").read_text(encoding="utf-8")
            (root / "subject.py").write_text(original.replace("return 3", "return 4"), encoding="utf-8")
            stale = rejoin()
            self.assertEqual(2, stale.returncode, stale.stdout)
            self.assertIn("source/counter hash mismatch", stale.stderr)

            (root / "subject.py").write_text(original, encoding="utf-8")
            counters = json.loads((output / "counters.json").read_text(encoding="utf-8"))
            counters["callables"][0]["body_lines"] = {"999": 0}
            (output / "counters.json").write_text(json.dumps(counters), encoding="utf-8")
            substituted = rejoin()
            self.assertEqual(2, substituted.returncode, substituted.stdout)
            self.assertIn("Mismatched Python callable body inventory", substituted.stderr)

    def test_counter_union_refuses_every_inconsistent_receipt(self):
        base = _counter_receipt([_counter_row()])
        for message, receipts in (
            ("Missing Python counter files", []),
            ("Invalid Python counter schema", [_counter_receipt([], schema=2)]),
            ("Duplicate Python counter identity",
             [_counter_receipt([_counter_row(), _counter_row()])]),
            ("source inventory mismatch", [base, _counter_receipt([_counter_row()], sources={})]),
            ("body inventory mismatch", [base, _counter_receipt([_counter_row(body_lines={"2": 1})])]),
            ("Invalid Python execution counter",
             [base, _counter_receipt([_counter_row(execution_count=-1)])]),
        ):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                combine_counters(receipts)

    def assert_campaign_receipt_survived(self, observed):
        campaign = observed["inherited"]
        if not campaign:
            return
        output = Path(json.loads(Path(campaign).read_text(encoding="utf-8"))["output"])
        self.assertTrue(list(output.glob(f"counter-{observed['pid']}-*.json")),
                        "the nested session consumed the inherited campaign receipt")

    def test_startup_bootstraps_a_cold_module_graph_beside_an_inherited_campaign(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        with tempfile.TemporaryDirectory() as folder:
            work = Path(folder)
            (work / "subject.py").write_text("def only():\n    return 1\n", encoding="utf-8")
            (work / "cold_start.py").write_text(_COLD_BOOTSTRAP, encoding="utf-8")
            (work / "unpinned.json").write_text('{"schema": 2}', encoding="utf-8")
            evidence = work / "evidence"
            evidence.mkdir()
            (work / "config.json").write_text(json.dumps({
                "schema": 1, "root": str(work), "output": str(evidence),
                "sources": [str(work / "subject.py")]}), encoding="utf-8")
            child = subprocess.run([str(python), str(work / "cold_start.py"), str(tools), str(work)],
                                   cwd=work, text=True, capture_output=True)
            self.assertEqual(0, child.returncode, child.stdout + child.stderr)
            receipts = list(evidence.glob("counter-*.json"))
            self.assertEqual(1, len(receipts), child.stdout + child.stderr)
            self.assertTrue(list(evidence.glob(".coverage.*")))
            receipt = json.loads(receipts[0].read_text(encoding="utf-8"))
            observed = json.loads((work / "observed.json").read_text(encoding="utf-8"))
        self.assertEqual((True, False), (observed["started"], observed["restarted"]))
        self.assertIn("coverage.py 7.16.0", observed["refusal"])
        self.assertEqual([str(tools / "python_runtime.py"), str(tools / "positions.py")],
                         [observed["runtime"], observed["positions"]])
        self.assertEqual([("only:1:0", 1)],
                         [(row["id"], row["execution_count"]) for row in receipt["callables"]])
        self.assert_campaign_receipt_survived(observed)

    def test_campaign_refuses_an_empty_selection_or_a_source_outside_the_root(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            empty = _facade(root, root / "empty-selection", [], ["{python}"])
            self.assertEqual(2, empty.returncode, empty.stdout + empty.stderr)
            self.assertIn("nonempty sources/command", empty.stderr)
            escaped = _facade(root, root / "outside-root", ["../escaped.py"], ["{python}"])
            self.assertEqual(2, escaped.returncode, escaped.stdout + escaped.stderr)
            self.assertIn("Invalid Python target source", escaped.stderr)
            self.assertFalse((root / "empty-selection").exists())


if __name__ == "__main__":
    unittest.main()

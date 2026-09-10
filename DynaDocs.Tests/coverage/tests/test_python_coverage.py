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

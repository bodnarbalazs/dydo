"""Exercise actual mutation harness processes and suite accounting in scratch directories."""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mutation_results as results
import run_mutation as runner

COVERAGE = Path(__file__).resolve().parents[1]


class SuiteEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)

    def python_suite(self, source):
        (self.root / "test_case.py").write_text(source, encoding="utf-8")
        receipt = self.root / "receipt.json"
        result = subprocess.run([sys.executable, str(COVERAGE / "mutation/python-test-adapter.py"),
                                 "--output", str(receipt), "--job", "current", "--", "discover", "-s", "."],
                                cwd=self.root, capture_output=True, timeout=20)
        self.assertTrue(receipt.is_file(), result.stderr.decode(errors="replace"))
        return json.loads(receipt.read_text()), result

    def test_real_completed_case_success_failure_error_and_timeout_text(self):
        for body, expected in (("print('timeout'); self.assertEqual(1, 1)", "surviving"),
                               ("self.assertEqual(1, 2)", "killed"),
                               ("raise ValueError('subject failure')", "killed")):
            with self.subTest(body=body):
                report, _ = self.python_suite("import unittest\nclass Case(unittest.TestCase):\n    def test_actual(self):\n        " + body + "\n")
                self.assertEqual(expected, results.python_suite(report, "current", ["test_case.Case.test_actual"]))

    def test_zero_import_syntax_skip_class_setup_and_expected_failure_are_incomplete(self):
        sources = ["import unittest\n", "raise ImportError('missing')\n", "broken (\n",
                   "import unittest\nclass Case(unittest.TestCase):\n    @unittest.skip('skip')\n    def test_actual(self): pass\n",
                   "import unittest\nclass Case(unittest.TestCase):\n    @classmethod\n    def setUpClass(cls): raise ValueError('setup')\n    def test_actual(self): pass\n",
                   "import unittest\nclass Case(unittest.TestCase):\n    @unittest.expectedFailure\n    def test_actual(self): self.fail()\n"]
        for source in sources:
            with self.subTest(source=source):
                report, _ = self.python_suite(source)
                with self.assertRaises(results.Incomplete):
                    results.python_suite(report, "current", ["test_case.Case.test_actual"])

    def test_missing_duplicate_stale_or_changed_case_receipt_is_incomplete(self):
        report, _ = self.python_suite("import unittest\nclass Case(unittest.TestCase):\n    def test_actual(self): pass\n")
        for job, expected in (("stale", ["test_case.Case.test_actual"]), ("current", ["other"]), ("current", [])):
            with self.assertRaises(results.Incomplete):
                results.python_suite(report, job, expected)
        report["events"].append(report["events"][0])
        with self.assertRaises(results.Incomplete):
            results.python_suite(report, "current", ["test_case.Case.test_actual"])

    def node_suite(self, source):
        (self.root / "test_case.cjs").write_text(source, encoding="utf-8")
        reporter = (COVERAGE / "mutation/node-test-reporter.cjs").as_uri()
        environment = dict(os.environ)
        environment.pop("NODE_TEST_CONTEXT", None)
        result = subprocess.run(["node", "--test", "--test-reporter=" + reporter, "test_case.cjs"],
                                cwd=self.root, env=environment, capture_output=True, timeout=20)
        return [json.loads(line) for line in result.stdout.splitlines()], result

    def test_node_real_case_is_distinct_from_synthetic_file_wrapper(self):
        events, _ = self.node_suite("const test=require('node:test'); test('actual',()=>{});\n")
        inventory = results.node_cases(events)
        self.assertEqual(1, len(inventory))
        self.assertEqual("surviving", results.node_suite(events, inventory))
        empty, _ = self.node_suite("// no actual tests\n")
        with self.assertRaises(results.Incomplete):
            results.node_cases(empty)

    def test_node_real_case_failure_kills_but_import_skip_and_timeout_do_not(self):
        source = "const test=require('node:test'); test('actual',()=>{throw Error('timeout');});\n"
        events, _ = self.node_suite(source)
        self.assertEqual("killed", results.node_suite(events, results.node_cases(events)))
        for source in ("throw Error('import');\n", "const test=require('node:test'); test.skip('actual',()=>{});\n",
                       "const test=require('node:test'); test('actual',{timeout:5},async()=>{await new Promise(r=>setTimeout(r,50));});\n"):
            events, _ = self.node_suite(source)
            with self.assertRaises(results.Incomplete):
                results.node_suite(events, results.node_cases(events))


class BootstrapTests(unittest.TestCase):
    def test_missing_and_tampered_snapshots_never_launch(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "judge.py"
            source.write_text("print('judge')", encoding="utf-8")
            manifest = runner.bootstrap(root / "harness", [source])
            runner.verify_bootstrap(manifest)
            copied = Path(next(iter(manifest)))
            copied.write_text("tampered", encoding="utf-8")
            with self.assertRaises(results.Incomplete):
                runner.verify_bootstrap(manifest)
            copied.unlink()
            with self.assertRaises(results.Incomplete):
                runner.verify_bootstrap(manifest)

    def test_process_preserves_literal_argv_and_drains_both_streams(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            code = "import sys; print(repr(sys.argv[1])); sys.stdout.write('o'*200000); sys.stderr.write('e'*200000)"
            argument = "spaces ' quotes \" ; & $(literal)"
            record = runner.process([sys.executable, "-c", code, argument], root, root / "run", 10)
            self.assertEqual(0, record["returncode"])
            self.assertIn(repr(argument), Path(record["stdout"]).read_text())
            self.assertEqual(200000, Path(record["stderr"]).stat().st_size)

    def test_timeout_is_incomplete_and_process_is_reaped(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            record = runner.process([sys.executable, "-c", "import time; time.sleep(30)"], root, root / "run", 0.1)
            self.assertTrue(record["timeout"])
            self.assertIsNotNone(record["returncode"])


if __name__ == "__main__":
    unittest.main()

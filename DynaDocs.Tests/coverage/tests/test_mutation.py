"""Exercise actual mutation harness processes and suite accounting in scratch directories."""
import hashlib
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
                                cwd=self.root, capture_output=True, timeout=20, check=False)
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
                                cwd=self.root, env=environment, capture_output=True, timeout=20, check=False)
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

    def test_node_nested_duplicate_names_have_stable_leaf_identities_and_complete_tree(self):
        source = "const {test,describe,it}=require('node:test'); describe('suite',()=>{it('same',()=>{});it('same',()=>{});describe('nested',()=>{it('leaf',()=>{});});}); test('parent',async(t)=>{await t.test('child',()=>{});});"
        events, _ = self.node_suite(source)
        identities = results.node_cases(events)
        self.assertEqual(4, len(identities))
        self.assertEqual(4, len(set(identities)))
        self.assertEqual("surviving", results.node_suite(events, identities))
        for kind in ("test:start", "test:complete", "test:plan", "test:summary"):
            damaged = list(events)
            index = next(index for index, event in enumerate(damaged) if event["type"] == kind)
            del damaged[index]
            with self.subTest(kind=kind), self.assertRaises(results.Incomplete):
                results.node_suite(damaged, identities)


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

    def test_sidecars_cannot_splice_start_and_terminal_from_different_invocations(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first, second = root / "0/first", root / "0/second"
            first.mkdir(parents=True)
            second.mkdir()
            value = {"job": "current", "native_id": "0", "invocation": "second", "complete": True, "state": "killed"}
            runner.write_json(first / "started.json", {**value, "complete": False})
            runner.write_json(second / "terminal.json", value)
            with self.assertRaises(results.Incomplete):
                runner.receipts(root, ["0"], "current")

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

    def test_real_job_preserves_environment_and_uses_exact_native_mutant_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            harness = root / "harness"
            paths = [COVERAGE / name for name in ("run_mutation.py", "mutation_results.py", "run_tests.py",
                                                "mutation/python-test-adapter.py", "mutation/node-test-reporter.cjs")]
            manifest = runner.bootstrap(harness, paths)
            subject = root / "test_case.cjs"
            subject.write_text("const test=require('node:test'); const assert=require('node:assert/strict'); test('env',()=>{assert.equal(process.env.STRYKER_MUTANT,'caller'); assert.equal(process.env.UNRELATED_MUTATION_TEST,'keep');});", encoding="utf-8")
            for identity in (None, "17"):
                folder = root / (identity or "baseline")
                folder.mkdir()
                job = {"job": "current", "language": "javascript", "test_command": ["node", "--test", "test_case.cjs"],
                       "harness": str(harness), "bootstrap": manifest, "receipts": str(folder / "receipts"), "timeout": 10}
                path = folder / "job.json"
                runner.write_json(path, job)
                digest = hashlib.sha256(path.read_bytes()).hexdigest()
                environment = {**os.environ, "STRYKER_MUTANT": "caller", "UNRELATED_MUTATION_TEST": "keep", "NODE_TEST_CONTEXT": "child-v8"}
                environment.pop("__STRYKER_ACTIVE_MUTANT__", None)
                if identity is not None:
                    environment["__STRYKER_ACTIVE_MUTANT__"] = identity
                command = [sys.executable, str(harness / "run_mutation.py"), "--job", str(path), "--job-sha256", digest]
                completed = subprocess.run(command, cwd=root, env=environment, capture_output=True, timeout=20, check=False)
                self.assertEqual(0, completed.returncode, completed.stderr)
                records = runner.receipts(folder / "receipts", [identity or "baseline"], "current")
                self.assertEqual(identity, records[identity or "baseline"]["native_id"])
                self.assertEqual("child-v8", environment["NODE_TEST_CONTEXT"])
                with self.assertRaises(results.Incomplete):
                    runner.receipts(folder / "receipts", [identity or "baseline"], "stale")
                subprocess.run(command, cwd=root, env=environment, capture_output=True, timeout=20, check=True)
                with self.assertRaises(results.Incomplete):
                    runner.receipts(folder / "receipts", [identity or "baseline"], "current")


class NativeConfigurationTests(unittest.TestCase):
    def test_configured_thresholds_concurrency_reporters_and_exclusions_fail_closed(self):
        good = {"concurrency": 1, "thresholds": {"high": 100, "low": 100, "break": 100}, "reporters": ["json", "html"]}
        runner.validate_settings(good, "cs")
        for key, value in (("concurrency", 2), ("concurrency", None), ("thresholds", {"high": 99}),
                           ("reporters", ["json", "dashboard"]), ("ignore-mutations", ["Arithmetic"]),
                           ("since", "HEAD"), ("incremental", True)):
            with self.subTest(key=key), self.assertRaises(results.Incomplete):
                runner.validate_settings({**good, key: value}, "cs")

    def test_dotnet_block_supplement_uses_the_pinned_enum_without_unsupported_names(self):
        self.assertEqual({"Statement", "Arithmetic", "Equality", "Boolean", "Logical", "Assignment", "Unary", "Update",
                          "Checked", "Linq", "String", "Bitwise", "Initializer", "Regex", "NullCoalescing", "Math",
                          "StringMethod", "Conditional", "CollectionExpression"}, set(runner.DOTNET_NON_BLOCK))

    def test_effective_concurrency_and_version_must_be_observed(self):
        runner.effective_dotnet("Version: 4.16.0\nStryker will use a max of 1 parallel testsessions.")
        for text in ("", "Version: 4.16.0\nStryker will use a max of 2 parallel testsessions.",
                     "Version: 4.17.0\nStryker will use a max of 1 parallel testsessions."):
            with self.assertRaises(results.Incomplete):
                runner.effective_dotnet(text)

    def test_partial_native_reports_cannot_hide_generated_mutants(self):
        for language, output in (("cs", "4 mutants created"),
                                 ("javascript", "ProjectReader Found 1 of 4 file(s) to be mutated.\nInstrumenter Instrumented 1 source file(s) with 4 mutant(s)")):
            runner.native_count(output, language, 4)
            for count in (0, 3, 5):
                with self.subTest(language=language, count=count), self.assertRaises(results.Incomplete):
                    runner.native_count(output, language, count)


class PublicCommandTests(unittest.TestCase):
    def test_actual_cli_isolates_dirty_and_nested_untracked_files_and_cleans_up(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "DynaDocs.Tests/coverage"
            target.mkdir(parents=True)
            for relative in ("run_mutation.py", "mutation_results.py", "run_tests.py", "mutation/python-test-adapter.py", "mutation/node-test-reporter.cjs"):
                (target / relative).parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(COVERAGE / relative, target / relative)
            (root / ".gitignore").write_text("dydo/_system/.local/\n**/__pycache__/\n", encoding="utf-8")
            (target / "inventory.py").write_text("def git_paths(root): return []\ndef fingerprint(root, paths): return 'c'*64\n", encoding="utf-8")
            (target / "gap_check.py").write_text("import json,subprocess,sys\nfrom pathlib import Path\nroot=Path.cwd()\nassert (root/'dirty.txt').read_text()=='changed'\nassert (root/'nested folder/new file.txt').read_text()=='new'\njson.dump({'schema_version':1,'position_encoding':'utf16','base_sha':sys.argv[2],'candidate_sha':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'source_fingerprint':'c'*64,'modules':[],'changed_members':[],'non_behavior_changes':[{'path':'dirty.txt','reason':'fixture parent confirms documentation-only'}],'observed_root':str(root)},open(sys.argv[4],'w'))\n", encoding="utf-8")
            (root / "dirty.txt").write_text("before", encoding="utf-8")
            subprocess.run(["git", "init", "--quiet", str(root)], check=True)
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True)
            subprocess.run(["git", "-c", "user.name=fixture", "-c", "user.email=fixture@localhost", "commit", "--quiet", "-m", "base"], cwd=root, check=True)
            base = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip()
            (root / "dirty.txt").write_text("changed", encoding="utf-8")
            (root / "nested folder").mkdir()
            (root / "nested folder/new file.txt").write_text("new", encoding="utf-8")
            result = subprocess.run([sys.executable, str(target / "run_mutation.py"), "--since", base], cwd=root, capture_output=True, timeout=30, check=False)
            self.assertEqual(0, result.returncode, result.stderr.decode(errors="replace"))
            summaries = list(root.glob("dydo/_system/.local/mutation/*/summary.json"))
            self.assertEqual(1, len(summaries))
            report = json.loads(summaries[0].read_text())
            observed = Path(json.loads(Path(report["inventory"]).read_text())["observed_root"])
            self.assertNotEqual(root, observed)
            self.assertFalse(observed.exists())
            self.assertEqual("changed", (root / "dirty.txt").read_text())
            self.assertEqual("new", (root / "nested folder/new file.txt").read_text())
            (target / "inventory.py").unlink()
            failed = subprocess.run([sys.executable, str(target / "run_mutation.py"), "--since", base], cwd=root, capture_output=True, timeout=30, check=False)
            self.assertEqual(2, failed.returncode)
            reports = [json.loads(path.read_text()) for path in root.glob("dydo/_system/.local/mutation/*/summary.json")]
            self.assertTrue(any("dependency is not implemented" in report.get("error", "") for report in reports))


if __name__ == "__main__":
    unittest.main()

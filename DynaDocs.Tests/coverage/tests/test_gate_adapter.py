"""Private adapters publish one complete stable schema under an exclusive lock."""
import json
import hashlib
import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr
from unittest import mock
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import gate_adapter
from gate_adapter import _target_paths, publish

TOOLS = Path(__file__).resolve().parents[1]
STATIC_PYTHON = TOOLS.parents[1] / "dydo/_system/.local/static-gates/python/Scripts/python.exe"

PYTHON_TARGET = "def add(left, right):\n    return left + right\n"
PYTHON_SUITE = ("import unittest\n\n\n"
                "class AddTests(unittest.TestCase):\n"
                "    def test_add(self):\n"
                "        self.assertEqual(3, 1 + 2)\n")
PYTHON_FAILING_SUITE = PYTHON_SUITE.replace("self.assertEqual(3, 1 + 2)",
                                            "self.fail('fixture regression')")

DISCOVERY_SOURCES = {
    "tests/test_unittest_case.py": "import unittest\n\n\nclass Case(unittest.TestCase):\n    pass\n",
    "tests/test_plain_class.py": "class Case(object):\n    pass\n",
    "tests/test_unparsable.py": "def broken(:\n",
    "tests/helper.py": "import unittest\n\n\nclass Case(unittest.TestCase):\n    pass\n",
    "tests/adapter.test.cjs": "const { test } = require('node:test');\ntest('a', () => {});\n",
    "tests/browser.test.cjs": "test('a', () => {});\n",
    "tests/library.cjs": "module.exports = {};\n",
}

# The fixture repository delegates to the real producer: a copy would lose its node_modules/c8.
NODE_PRODUCER_SHIM = (
    "'use strict';\n"
    "const { spawnSync } = require('node:child_process');\n"
    "const answer = spawnSync(process.execPath,\n"
    "  [" + json.dumps((TOOLS / "javascript_coverage.cjs").as_posix()) + ",\n"
    "   ...process.argv.slice(2)], { stdio: 'inherit' });\n"
    "process.exitCode = answer.status === null ? 130 : answer.status;\n")
NODE_TEST_DRIVER = (
    "'use strict';\n"
    "const path = require('node:path');\n"
    "const { spawnSync } = require('node:child_process');\n"
    "const suite = path.resolve(__dirname, 'tests', 'sum.test.cjs');\n"
    "const answer = spawnSync(process.execPath, ['--test', suite], { stdio: 'inherit' });\n"
    "process.exitCode = answer.status === null ? 130 : answer.status;\n")
NODE_IGNORED = ("DynaDocs.Tests/coverage/javascript_coverage.cjs\n"
                "DynaDocs.Tests/coverage/node_tests.cjs\n")
NODE_SOURCE = ("'use strict';\n"
               "function sum(a, b) {\n  return a + b;\n}\n"
               "module.exports = { sum };\n")
NODE_SOURCE_WITH_GAP = ("'use strict';\n"
                        "function sum(a, b) {\n  return a + b;\n}\n"
                        "function unused(a) {\n  return a - 1;\n}\n"
                        "module.exports = { sum, unused };\n")
NODE_SUITE = ("'use strict';\n"
              "const test = require('node:test');\n"
              "const assert = require('node:assert');\n"
              "const { sum } = require('../../../lib/sum.cjs');\n"
              "test('sum adds', () => { assert.strictEqual(sum(1, 2), 3); });\n")


def write_file(root, relative, text):
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def git_repository(root):
    subprocess.run(["git", "init", "--quiet", str(root)], check=True)
    return root


def committed_repository(root):
    git_repository(root)
    subprocess.run(["git", "-C", str(root), "add", "-A"], check=True)
    subprocess.run(["git", "-C", str(root), "-c", "user.name=fixture",
                    "-c", "user.email=fixture@example.invalid",
                    "commit", "--quiet", "-m", "fixture"], check=True)
    return root


def collector_row(answer, name):
    return answer["facts"]["collectors"][name]


def python_repository(folder, suite=PYTHON_SUITE):
    root = Path(folder) / "repo"
    write_file(root, "lib/arithmetic.py", PYTHON_TARGET)
    write_file(root, "DynaDocs.Tests/coverage/tests/test_arithmetic.py", suite)
    write_file(root, "DynaDocs.Tests/coverage/test-associations.json",
               '{"schema": 1, "modules": []}')
    return committed_repository(root)


class GateAdapterTests(unittest.TestCase):
    def test_python_coverage_targets_only_inventory_maintained_sources(self):
        with tempfile.TemporaryDirectory() as folder:
            inventory = Path(folder) / "inventory.json"
            inventory.write_text(json.dumps({"sources": [
                {"path": "active.py", "language": "python", "role": "target"},
                {"path": "test_active.py", "language": "python", "role": "test"},
                {"path": "active.js", "language": "javascript", "role": "target"},
            ], "excluded": [{"path": "derived.py", "reason": "derived-copy"}]}))

            self.assertEqual(["active.py"], _target_paths(inventory, "python"))

    def fixture(self, root):
        run = root / "assurance/run-a"
        run.mkdir(parents=True)
        inventory = run / "inventory.json"
        inventory.write_text('{"schema":1}')
        report = {"status": "fail", "facts": {"commands": []},
                  "findings": [{"gate": "line-coverage", "path": "a.py"}], "errors": []}
        candidate = {"commit": "a" * 40, "dirty": True, "sourceFingerprint": "b" * 64}
        return run, inventory, report, candidate

    def test_publish_retains_findings_and_removes_only_its_lock(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run, inventory, report, candidate = self.fixture(root)
            summary = root / "adapters/python-coverage.json"
            self.assertEqual(1, publish(summary, run, "python", "coverage", report,
                                        candidate, inventory))
            payload = json.loads(summary.read_text())
            self.assertEqual({"schema", "candidate", "stack", "gate", "inventory", "tools",
                              "commands", "collectors", "findings", "gaps",
                              "measurementComplete", "exitCode"}, set(payload))
            self.assertEqual("line-coverage", payload["findings"][0]["gate"])
            self.assertFalse(Path(str(summary) + ".lock").exists())
            self.assertTrue((run / "report.json").is_file())

    def test_publication_collision_preserves_foreign_lock_and_summary(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run, inventory, report, candidate = self.fixture(root)
            summary = root / "adapters/python-coverage.json"
            summary.parent.mkdir()
            summary.write_text("foreign")
            lock = Path(str(summary) + ".lock")
            lock.write_text("foreign-lock")
            with self.assertRaisesRegex(ValueError, "lock"):
                publish(summary, run, "python", "coverage", report, candidate, inventory)
            self.assertEqual("foreign", summary.read_text())
            self.assertEqual("foreign-lock", lock.read_text())
            self.assertTrue((run / "report.json").is_file())

    def test_coverage_subprocess_gets_one_remaining_timeout_and_cooperative_cleanup(self):
        child = mock.Mock(returncode=130)
        child.wait.side_effect = [subprocess.TimeoutExpired(["campaign"], 20), 130]
        with mock.patch.dict(os.environ, {"DYDO_ROW_DEADLINE": "150"}), \
                mock.patch.object(gate_adapter.time, "monotonic", side_effect=[100, 120]), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child) as popen:
            with self.assertRaisesRegex(gate_adapter.MeasurementTimeout, "deadline"):
                gate_adapter.run_coverage_command(["campaign"], Path.cwd())
        self.assertEqual(20, child.wait.call_args_list[0].kwargs["timeout"])
        self.assertEqual(30, child.wait.call_args_list[1].kwargs["timeout"])
        self.assertEqual(1, child.send_signal.call_count)
        child.kill.assert_not_called()
        kwargs = popen.call_args.kwargs
        self.assertNotIn("stdout", kwargs)
        self.assertNotIn("stderr", kwargs)
        self.assertNotIn("stdin", kwargs)
        self.assertEqual(subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                         kwargs["creationflags"])

    def test_coverage_subprocess_force_terminates_after_cleanup_budget(self):
        child = mock.Mock(returncode=2)
        child.wait.side_effect = [subprocess.TimeoutExpired(["campaign"], 20),
                                  subprocess.TimeoutExpired(["campaign"], 30), 2]
        with mock.patch.dict(os.environ, {"DYDO_ROW_DEADLINE": "150"}), \
                mock.patch.object(gate_adapter.time, "monotonic", side_effect=[100, 120]), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child):
            with self.assertRaisesRegex(gate_adapter.MeasurementTimeout, "deadline"):
                gate_adapter.run_coverage_command(["campaign"], Path.cwd())
        self.assertEqual(1, child.send_signal.call_count)
        child.kill.assert_called_once_with()
        self.assertEqual(3, child.wait.call_count)

    # A defect the catch tuple misses leaves main with exit 1 and no summary at all, which the
    # facade can only report as an unrefreshed artifact rather than a named measurement error.
    def test_coverage_defects_are_published_as_measurement_errors_without_traceback(self):
        for defect in (gate_adapter.MeasurementTimeout("row deadline"),
                       AttributeError("joined row exposes no coverage"),
                       IndexError("empty measured sequence"),
                       SyntaxError("no element found in the native report")):
            with self.subTest(defect=type(defect).__name__), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                output = root / "results"
                inventory = root / "inventory.json"
                inventory.write_text('{"schema":1}', encoding="utf-8")
                captured = {}

                def publication(summary, run, stack, gate, report, candidate, path):
                    captured.update(report)
                    return 2

                stderr = io.StringIO()
                argv = ["gate_adapter.py", "--gate", "coverage", "--stack", "node",
                        "--root", str(root), "--output", str(output)]
                with mock.patch.object(sys, "argv", argv), \
                        mock.patch.object(gate_adapter, "_candidate", return_value=({"commit": "a" * 40}, [])), \
                        mock.patch.object(gate_adapter, "_inventory_artifact", return_value=(inventory, [], [])), \
                        mock.patch.object(gate_adapter, "collect_coverage", side_effect=defect), \
                        mock.patch.object(gate_adapter, "publish", side_effect=publication), \
                        redirect_stderr(stderr):
                    self.assertEqual(2, gate_adapter.main())
                self.assertEqual("error", captured["status"])
                self.assertEqual(type(defect).__name__, captured["errors"][0]["type"])
                self.assertNotIn("Traceback", stderr.getvalue())

    def test_candidate_and_inventory_setup_defects_use_the_adapter_error_path(self):
        cases = (("candidate", OSError("git unavailable")),
                 ("candidate", subprocess.CalledProcessError(128, ["git", "status"])),
                 ("inventory", ValueError("inventory unavailable")))
        for seam, defect in cases:
            with self.subTest(seam=seam), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                output = root / "results"
                captured = {}

                def publication(summary, run, stack, gate, report, candidate, inventory):
                    captured.update(report=report, candidate=candidate, inventory=inventory)
                    return 2

                argv = ["gate_adapter.py", "--gate", "static", "--stack", "python",
                        "--root", str(root), "--output", str(output)]
                candidate = mock.patch.object(
                    gate_adapter, "_candidate",
                    side_effect=defect if seam == "candidate" else None,
                    return_value=({"commit": "a" * 40, "dirty": False,
                                   "sourceFingerprint": "b" * 64}, []))
                inventory = mock.patch.object(
                    gate_adapter, "_inventory_artifact", side_effect=defect)
                with mock.patch.object(sys, "argv", argv), candidate, \
                        (inventory if seam == "inventory" else mock.patch.object(
                            gate_adapter, "_inventory_artifact")), \
                        mock.patch.object(gate_adapter, "collect_static") as collect, \
                        mock.patch.object(gate_adapter, "publish", side_effect=publication), \
                        redirect_stderr(io.StringIO()) as stderr:
                    self.assertEqual(2, gate_adapter.main())

                collect.assert_not_called()
                self.assertEqual("error", captured["report"]["status"])
                self.assertEqual(type(defect).__name__, captured["report"]["errors"][0]["type"])
                self.assertTrue(Path(captured["inventory"]).is_file())
                if seam == "candidate":
                    self.assertIsNone(captured["candidate"]["sourceFingerprint"])
                self.assertNotIn("Traceback", stderr.getvalue())

    def test_inventory_evaluation_uses_isolated_appdata_before_collection(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            output = root / "results"
            inventory = root / "inventory.json"
            inventory.write_text('{"schema":1}', encoding="utf-8")
            observed = {}

            def collect_inventory(*_args):
                observed["appdata"] = os.environ.get("APPDATA")
                observed["packages"] = os.environ.get("NUGET_PACKAGES")
                return inventory, [], []

            argv = ["gate_adapter.py", "--gate", "static", "--stack", "python",
                    "--root", str(root), "--output", str(output)]
            with mock.patch.object(sys, "argv", argv), \
                    mock.patch.dict(os.environ, {"APPDATA": "foreign", "NUGET_PACKAGES": ""}, clear=False), \
                    mock.patch.object(gate_adapter, "_candidate", return_value=({"commit": "a" * 40}, [])), \
                    mock.patch.object(gate_adapter, "_inventory_artifact", side_effect=collect_inventory), \
                    mock.patch.object(gate_adapter, "collect_static", return_value={"status": "pass", "facts": {}, "findings": [], "errors": []}), \
                    mock.patch.object(gate_adapter, "publish", return_value=0):
                self.assertEqual(0, gate_adapter.main())
            self.assertEqual(str(root / "dydo/_system/.local/appdata"), observed["appdata"])
            self.assertEqual("", observed["packages"])


class PublishedProvenanceTests(unittest.TestCase):
    """The summary must name what measured it, not merely what it concluded."""

    def test_tools_name_both_interpreters_and_every_resolved_tool_pin(self):
        root = TOOLS.parents[1]

        tools = gate_adapter.gate_tools(root)

        interpreters = tools["interpreters"]
        self.assertEqual(sys.executable, interpreters["caller"]["path"])
        self.assertEqual(hashlib.sha256(Path(sys.executable).read_bytes()).hexdigest(),
                         interpreters["caller"]["sha256"])
        self.assertEqual("dydo/_system/.local/static-gates/python/Scripts/python.exe",
                         interpreters["dependencyBearing"]["path"])
        self.assertEqual(hashlib.sha256(STATIC_PYTHON.read_bytes()).hexdigest(),
                         interpreters["dependencyBearing"]["sha256"])
        pins = tools["pins"]
        locks = json.loads((TOOLS / "metrics/packages.lock.json").read_text(encoding="utf-8"))
        self.assertEqual(locks["dependencies"]["net10.0"]["SonarAnalyzer.CSharp"]["resolved"],
                         pins["dotnet"]["resolved"]["SonarAnalyzer.CSharp"])
        self.assertEqual(json.loads((TOOLS / "package.json").read_text(encoding="utf-8"))
                         ["dependencies"]["c8"], pins["javascript"]["resolved"]["c8"])
        self.assertIn("coverage==" + pins["python"]["resolved"]["coverage"],
                      (TOOLS / "requirements.lock").read_text(encoding="utf-8"))
        self.assertEqual(json.loads((root / ".config/dotnet-tools.json").read_text(encoding="utf-8"))
                         ["tools"]["altcover.global"]["version"],
                         pins["altcover"]["resolved"]["altcover.global"])
        self.assertEqual(hashlib.sha256((TOOLS / "requirements.lock").read_bytes()).hexdigest(),
                         pins["python"]["sha256"])

    def test_an_unreadable_pin_is_named_without_destroying_the_measurement(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            write_file(root, "DynaDocs.Tests/coverage/requirements.lock", "coverage==7.16.0\n")
            write_file(root, ".config/dotnet-tools.json", "{ not json")

            tools = gate_adapter.gate_tools(root)

            self.assertEqual({"coverage": "7.16.0"}, tools["pins"]["python"]["resolved"])
            self.assertIsNone(tools["pins"]["altcover"]["resolved"])
            self.assertIn("JSONDecodeError", tools["pins"]["altcover"]["reason"])
            self.assertIsNone(tools["pins"]["dotnet"]["resolved"])
            self.assertIsNone(tools["interpreters"]["dependencyBearing"]["sha256"])

    def test_logged_commands_carry_argv_environment_exit_elapsed_and_hashed_streams(self):
        from gate_run import CommandLog
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder).resolve()
            log = CommandLog(run, run / "raw/commands")
            log.run("probe", [sys.executable, "-c", "import os,sys; sys.stdout.write(os.environ['DYDO_PROBE'])"],
                    environment={"DYDO_PROBE": "supplied"})

            rows = gate_adapter._logged_commands(log.rows, run)

            self.assertEqual(1, len(rows))
            row = rows[0]
            self.assertEqual("probe", row["name"])
            self.assertEqual(sys.executable, row["argv"][0])
            self.assertEqual({"DYDO_PROBE": "supplied"}, row["environment"])
            self.assertEqual(0, row["exit"])
            self.assertEqual("raw/commands/0000-probe.stdout", row["stdout"])
            self.assertEqual("raw/commands/0000-probe.stderr", row["stderr"])
            self.assertEqual(hashlib.sha256(b"supplied").hexdigest(), row["stdoutSha256"])
            self.assertGreater(row["elapsedSeconds"], 0)
            self.assertEqual(str(run), row["cwd"])

    def test_logged_commands_project_unlaunched_stream_destinations(self):
        from gate_run import CommandLog
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder)
            log = CommandLog(run, run / "raw/commands")
            with self.assertRaises(OSError):
                log.run("missing", [str(run / "missing-executable")])

            rows = gate_adapter._logged_commands(log.rows, run)

            self.assertEqual(1, len(rows))
            self.assertEqual((None, "raw/commands/0000-missing.stdout", None,
                              "raw/commands/0000-missing.stderr", None),
                             (rows[0]["exit"], rows[0]["stdout"], rows[0]["stdoutSha256"],
                              rows[0]["stderr"], rows[0]["stderrSha256"]))
            self.assertIn("FileNotFoundError", log.rows[0]["error"])

    def test_logged_commands_require_retained_streams_for_completed_commands(self):
        from gate_run import CommandLog
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder)
            log = CommandLog(run, run / "raw/commands")
            log.run("probe", [sys.executable, "-c", "raise SystemExit(7)"])
            self.assertEqual(7, log.rows[0]["exit_code"])
            Path(log.rows[0]["stdout"]).unlink()

            with self.assertRaises(OSError):
                gate_adapter._logged_commands(log.rows, run)

    def test_campaign_command_rows_are_rebased_on_the_report_directory(self):
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder)
            evidence = run / "raw-abc/raw"
            stdout = write_file(evidence, "altcover-runner.stdout", "collected\n")
            stderr = write_file(evidence, "altcover-runner.stderr", "warning\n")
            write_file(evidence, "commands.json", json.dumps([
                {"name": "altcover-runner", "argv": ["dotnet", "tool", "run"], "cwd": str(run),
                 "environment": {"MSBUILDDISABLENODEREUSE": "1"}, "exit": 0,
                 "elapsedSeconds": 211.938, "stdout": "altcover-runner.stdout",
                 "stdoutSha256": "a" * 64, "stderr": "altcover-runner.stderr",
                 "stderrSha256": "b" * 64}]))

            rows = gate_adapter._campaign_commands(evidence, run)

            self.assertEqual("raw-abc/raw/altcover-runner.stdout", rows[0]["stdout"])
            self.assertEqual("raw-abc/raw/altcover-runner.stderr", rows[0]["stderr"])
            self.assertEqual(211.938, rows[0]["elapsedSeconds"])
            self.assertEqual({"MSBUILDDISABLENODEREUSE": "1"}, rows[0]["environment"])
            self.assertEqual(hashlib.sha256(stdout.read_bytes()).hexdigest(),
                             rows[0]["stdoutSha256"])
            self.assertEqual(hashlib.sha256(stderr.read_bytes()).hexdigest(),
                             rows[0]["stderrSha256"])
            self.assertEqual([], gate_adapter._campaign_commands(run / "absent", run))

            stderr.unlink()
            with self.assertRaises(OSError):
                gate_adapter._campaign_commands(evidence, run)

    def test_a_path_outside_the_report_directory_stays_absolute(self):
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder) / "run"
            contained = write_file(run, "raw/a.json", "{}")
            foreign = write_file(Path(folder), "other.json", "{}")

            self.assertEqual("raw/a.json", gate_adapter._report_relative(contained, run))
            self.assertEqual(foreign.resolve().as_posix(),
                             gate_adapter._report_relative(foreign, run))

    def test_projection_uses_resolved_report_and_artifact_identities(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / "long-run"
            alias = Path(folder) / "run-alias"
            artifact = write_file(target, "raw/a.json", "{}")
            if os.name == "nt":
                created = subprocess.run(
                    [os.environ["COMSPEC"], "/d", "/c", "mklink", "/J", str(alias), str(target)],
                    capture_output=True, text=True, encoding="utf-8")
                self.assertEqual(0, created.returncode, created.stdout + created.stderr)
            else:
                alias.symlink_to(target, target_is_directory=True)

            artifact_spelling = "\\\\?\\" + str(artifact).swapcase() if os.name == "nt" else artifact
            self.assertEqual("raw/a.json", gate_adapter._report_relative(artifact_spelling, alias))
            foreign = write_file(Path(folder), "foreign.json", "{}")
            self.assertEqual(foreign.resolve().as_posix(),
                             gate_adapter._report_relative(foreign, alias))
            self.assertEqual(foreign.resolve().as_posix(), gate_adapter._report_relative(
                target / "raw/../../foreign.json", alias))
            escape = target / "escape"
            if os.name == "nt":
                created = subprocess.run(
                    [os.environ["COMSPEC"], "/d", "/c", "mklink", "/J", str(escape), str(Path(folder))],
                    capture_output=True, text=True, encoding="utf-8")
                self.assertEqual(0, created.returncode, created.stdout + created.stderr)
            else:
                escape.symlink_to(Path(folder), target_is_directory=True)
            self.assertEqual(foreign.resolve().as_posix(),
                             gate_adapter._report_relative(escape / "foreign.json", alias))
            for value, run in ((target / "missing.json", alias),
                               (artifact, Path(folder) / "missing-run")):
                with self.subTest(value=value, run=run), self.assertRaises(OSError):
                    gate_adapter._report_relative(value, run)

            missing_raw = target / "raw/missing-output"
            self.assertEqual("raw/missing-output",
                             gate_adapter._report_location(missing_raw, alias))

    def test_a_collector_owns_the_raw_files_it_writes_beside_its_command_log(self):
        from gate_run import CommandLog
        with tempfile.TemporaryDirectory() as folder:
            run = Path(folder)
            collector = mock.Mock(output=run / "raw", log=CommandLog(run, run / "raw/commands"))
            (run / "raw/stale.json").write_text("before", encoding="utf-8")
            artifacts = {}

            def produce():
                (run / "raw/analyzers-0.sarif").write_text("native", encoding="utf-8")
                (run / "raw/commands/0000-noise.stdout").write_text("noise", encoding="utf-8")
                return {"status": "pass"}

            answer = gate_adapter._recorded(collector, "csharp-analyzers", produce, artifacts)()

            self.assertEqual({"status": "pass"}, answer)
            self.assertEqual([{"path": "raw/analyzers-0.sarif",
                               "sha256": hashlib.sha256(b"native").hexdigest()}],
                             artifacts["csharp-analyzers"])

    def test_static_collectors_carry_their_own_raw_artifact_hashes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(Path(folder).resolve())
            run = Path(folder) / "run"

            answer = gate_adapter.collect_static(root, run / "raw", "python")

            source = answer["facts"]["collectors"]["python-source"]
            self.assertEqual([], source["artifacts"])
            self.assertTrue(all("artifacts" in row
                                for row in answer["facts"]["collectors"].values()))
            names = [row["name"] for row in answer["facts"]["commands"]]
            self.assertEqual(["python-source-0", "python-source-1", "ruff"], sorted(names))
            ruff = next(row for row in answer["facts"]["commands"] if row["name"] == "ruff")
            self.assertEqual("raw/commands/0002-ruff.stdout", ruff["stdout"])
            self.assertEqual("raw/commands/0002-ruff.stderr", ruff["stderr"])
            self.assertEqual(str(root), ruff["cwd"])
            self.assertEqual({}, ruff["environment"])
            self.assertIn("static-gates", ruff["argv"][0])


class NativeDiscoveryTests(unittest.TestCase):
    def test_discovery_accepts_only_unittest_cases_and_node_test_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for relative, text in DISCOVERY_SOURCES.items():
                write_file(root, relative, text)

            rows = gate_adapter._ordinary_discovery(root, [*sorted(DISCOVERY_SOURCES),
                                                           "tests/absent.py"])

            self.assertEqual([{"id": "tests/adapter.test.cjs#node-test",
                               "file": "tests/adapter.test.cjs"},
                              {"id": "tests/test_unittest_case.py#unittest",
                               "file": "tests/test_unittest_case.py"}], rows)

    def test_aggregate_prefers_measurement_gaps_over_findings_and_names_each_collector(self):
        clean = {"findings": [], "errors": []}
        failed = {"findings": [{"gate": "line-coverage"}], "errors": []}
        absent = {"findings": [], "errors": [{"message": "no native producer"}]}
        rows = [{"a": clean}, {"a": clean, "b": failed}, {"b": failed, "c": absent}]

        statuses = [gate_adapter._aggregate({"collectors": row})["status"] for row in rows]

        self.assertEqual(["pass", "fail", "error"], statuses)
        answer = gate_adapter._aggregate({"collectors": rows[2]})
        self.assertEqual([{"collector": "b", "gate": "line-coverage"}], answer["findings"])
        self.assertEqual([{"collector": "c", "message": "no native producer"}], answer["errors"])
        self.assertEqual(rows[2], answer["facts"]["collectors"])


class CandidateInventoryTests(unittest.TestCase):
    def test_candidate_reports_the_head_commit_and_notices_an_untracked_change(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder)
            head = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"], check=True,
                                  text=True, capture_output=True).stdout.strip()

            clean, tracked = gate_adapter._candidate(root)
            write_file(root, "lib/extra.py", "value = 1\n")
            dirty, extended = gate_adapter._candidate(root)

            self.assertEqual(head, clean["commit"])
            self.assertFalse(clean["dirty"])
            self.assertTrue(dirty["dirty"])
            self.assertNotEqual(clean["sourceFingerprint"], dirty["sourceFingerprint"])
            self.assertEqual(["lib/extra.py"], [row["path"] for row in extended
                                                if row not in tracked])

    def test_inventory_artifact_retains_the_candidate_roles_and_the_missing_project_gap(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder)
            candidate, _ = gate_adapter._candidate(root)

            inventory, errors, commands = gate_adapter._inventory_artifact(
                root, Path(folder) / "run", candidate)

            payload = json.loads(inventory.read_text(encoding="utf-8"))
            self.assertEqual(candidate, payload["candidate"])
            self.assertEqual([{"message": "No evaluated C# projects"}], errors)
            roles = {row["path"]: row["role"] for row in payload["sources"]}
            self.assertEqual("target", roles["lib/arithmetic.py"])
            self.assertEqual("test", roles["DynaDocs.Tests/coverage/tests/test_arithmetic.py"])
            self.assertEqual([], commands)

    def test_inventory_artifact_does_not_overwrite_a_mismatched_source_fingerprint(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder)
            candidate, _ = gate_adapter._candidate(root)
            candidate["sourceFingerprint"] = "b" * 64

            inventory, errors, _ = gate_adapter._inventory_artifact(
                root, Path(folder) / "run", candidate)

            payload = json.loads(inventory.read_text(encoding="utf-8"))
            actual = payload["candidate"]["sourceFingerprint"]
            self.assertNotEqual(candidate["sourceFingerprint"], actual)
            self.assertIn({"message": "Outer and inventory source fingerprints differ",
                           "outerSourceFingerprint": candidate["sourceFingerprint"],
                           "inventorySourceFingerprint": actual}, errors)

    def test_static_gate_registers_the_collectors_its_stack_owns(self):
        shared = {"projects", "source-inventory", "associations", "repository-inputs"}
        expected = {
            "python": shared | {"python-source", "python-dead-code", "python-dependencies"},
            "node": shared | {"javascript-source", "javascript-dependencies",
                              "javascript-unused-exports", "clones"},
            "dotnet": shared | {"csharp-source", "csharp-analyzers", "versions"},
        }
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder)
            for stack, names in expected.items():
                answer = gate_adapter.collect_static(root, Path(folder) / ("static-" + stack), stack)
                self.assertEqual(names, set(answer["facts"]["collectors"]), stack)
            with self.assertRaisesRegex(ValueError, "Unknown stack: elixir"):
                gate_adapter.collect_static(root, Path(folder) / "static-elixir", "elixir")

    def test_each_static_stack_invalidates_when_repository_inputs_change_during_collection(self):
        from gate_run import result
        for stack in ("dotnet", "python", "node"):
            with self.subTest(stack=stack), tempfile.TemporaryDirectory() as folder:
                root = python_repository(folder)
                target = root / "lib/arithmetic.py"

                def mutate():
                    target.write_text(PYTHON_TARGET + "# changed during collection\n",
                                      encoding="utf-8")
                    return result(findings=[{"gate": "probe"}])

                with mock.patch.object(gate_adapter, "_stack_methods",
                                       return_value={"probe": mutate}):
                    answer = gate_adapter.collect_static(
                        root, Path(folder) / ("static-" + stack), stack)

                self.assertEqual("error", answer["status"])
                self.assertEqual([{"collector": "probe", "gate": "probe"}],
                                 answer["findings"])
                integrity = collector_row(answer, "repository-inputs")
                self.assertEqual("error", integrity["status"])
                self.assertEqual("Repository inputs changed during collection",
                                 integrity["errors"][0]["message"])

    def test_repository_input_check_invalidates_changed_deleted_and_new_files(self):
        from gate_collect import repository_inputs
        for mutation in ("changed", "deleted", "new"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as folder:
                root = python_repository(folder)
                initial, _ = gate_adapter._candidate(root)
                if mutation == "changed":
                    write_file(root, "lib/arithmetic.py", PYTHON_TARGET + "# changed\n")
                elif mutation == "deleted":
                    (root / "lib/arithmetic.py").unlink()
                else:
                    write_file(root, "lib/extra.py", "value = 1\n")

                answer = repository_inputs(root, initial["sourceFingerprint"])

                self.assertEqual("error", answer["status"])
                self.assertEqual("Repository inputs changed during collection",
                                 answer["errors"][0]["message"])

    def test_repository_input_recheck_failure_is_a_measurement_error(self):
        from gate_collect import repository_inputs
        with mock.patch("gate_collect.git_file_state", side_effect=OSError("unreadable")):
            answer = repository_inputs(Path.cwd(), "a" * 64)

        self.assertEqual("error", answer["status"])
        self.assertEqual("Repository inputs could not be rechecked",
                         answer["errors"][0]["message"])
        self.assertIsNone(answer["facts"]["currentSourceFingerprint"])


class CoverageCampaignTests(unittest.TestCase):
    def coverage_gate(self, root, output):
        return subprocess.run([str(STATIC_PYTHON), str(TOOLS / "gate_adapter.py"),
                               "--gate", "coverage", "--stack", "python",
                               "--root", str(root), "--output", str(output)],
                              text=True, capture_output=True, encoding="utf-8", errors="replace")

    def node_repository(self, folder, source):
        root = Path(folder) / "repo"
        write_file(root, "lib/sum.cjs", source)
        write_file(root, ".gitignore", NODE_IGNORED)
        write_file(root, "DynaDocs.Tests/coverage/javascript_coverage.cjs", NODE_PRODUCER_SHIM)
        write_file(root, "DynaDocs.Tests/coverage/node_tests.cjs", NODE_TEST_DRIVER)
        write_file(root, "DynaDocs.Tests/coverage/tests/sum.test.cjs", NODE_SUITE)
        return git_repository(root)

    def test_python_coverage_gate_publishes_measured_findings_beside_inventory_gaps(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder)
            output = Path(folder) / "results"

            process = self.coverage_gate(root, output)

            self.assertEqual(2, process.returncode, process.stderr)
            payload = json.loads((output / "adapters/python-coverage.json").read_text(encoding="utf-8"))
            self.assertEqual([{"actual": 0.0, "gate": "line-coverage", "member": None,
                               "path": "lib/arithmetic.py", "threshold": 80}], payload["findings"])
            self.assertEqual([{"message": "No evaluated C# projects"}], payload["gaps"])
            self.assertFalse(payload["measurementComplete"])
            self.assertEqual(str(STATIC_PYTHON),
                             payload["tools"]["interpreters"]["caller"]["path"])
            self.assertEqual(["python-coverage"], [row["name"] for row in payload["commands"]])

    def test_python_coverage_gate_reports_a_failed_suite_before_any_policy(self):
        with tempfile.TemporaryDirectory() as folder:
            root = python_repository(folder, PYTHON_FAILING_SUITE)
            output = Path(folder) / "results"

            process = self.coverage_gate(root, output)

            self.assertEqual(2, process.returncode, process.stderr)
            payload = json.loads((output / "adapters/python-coverage.json").read_text(encoding="utf-8"))
            self.assertEqual([{"gate": "functional", "child_exit": 1}], payload["findings"])

    def test_dotnet_coverage_reports_an_incomplete_campaign_and_rejects_an_unknown_stack(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "repo"
            root.mkdir()
            inventory = write_file(root, "inventory.json", '{"sources": []}')

            answer = gate_adapter.collect_coverage(root, Path(folder), "dotnet", inventory)

            self.assertEqual("error", answer["status"])
            self.assertEqual([{"gate": "csharp-coverage", "message": "native campaign incomplete"}],
                             answer["errors"])
            row = collector_row(answer, "csharp-coverage")
            self.assertEqual(2, row["facts"]["child_exit"])
            self.assertEqual([], row["artifacts"])
            command = answer["facts"]["commands"][0]
            self.assertEqual(("csharp-campaign", 2, "inherited"),
                             (command["name"], command["exit"], command["streams"]))
            self.assertIn("run_tests.py", command["argv"][1])
            self.assertGreaterEqual(command["elapsedSeconds"], 0)
            with self.assertRaisesRegex(ValueError, "Unknown stack: elixir"):
                gate_adapter.collect_coverage(root, Path(folder), "elixir", inventory)

    def test_a_measured_csharp_campaign_publishes_its_findings_evidence_and_command(self):
        with tempfile.TemporaryDirectory() as folder:
            root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
            finding = {"gate": "line-coverage", "path": "Commands/SyncCommand.cs"}
            joined = write_file(raw, "raw/joined.json",
                                json.dumps({"modules": [], "findings": [finding]}))

            with mock.patch.dict(os.environ, {"APPDATA": str(root / "appdata")}, clear=False), \
                    mock.patch.object(gate_adapter, "run_coverage_command", return_value=0):
                answer = gate_adapter.collect_dotnet_coverage(root, raw)

            self.assertEqual([finding], answer["findings"])
            row = collector_row(answer, "csharp-coverage")
            self.assertEqual(("fail", "raw-abc"), (row["status"], row["facts"]["raw"]))
            self.assertEqual([{"path": "raw-abc/raw/joined.json",
                               "sha256": hashlib.sha256(joined.read_bytes()).hexdigest()}],
                             row["artifacts"])
            campaign = answer["facts"]["commands"][0]
            self.assertEqual(str(root), campaign["cwd"])
            self.assertEqual(str(root / "appdata"), campaign["environment"]["APPDATA"])

    def test_a_failed_csharp_suite_is_a_functional_finding_before_any_join(self):
        with tempfile.TemporaryDirectory() as folder:
            root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
            raw.parent.mkdir()

            with mock.patch.object(gate_adapter, "run_coverage_command", return_value=1):
                answer = gate_adapter.collect_dotnet_coverage(root, raw)

            self.assertEqual([{"gate": "functional", "child_exit": 1, "failingTests": [],
                               "failingTestsSummary": "campaign subject streams unavailable: "
                                                      "altcover-runner.stdout, altcover-runner.stderr"}],
                             answer["findings"])

    def test_a_failed_csharp_suite_names_the_dotnet_tests_that_failed(self):
        with tempfile.TemporaryDirectory() as folder:
            root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
            write_file(raw, "raw/altcover-runner.stdout",
                      "some AltCover visit-flush chatter\r\n"
                      "  Failed DynaDocs.Tests.A.B [12 ms]\r\n"
                      "  Failed DynaDocs.Tests.C.D(x: 1) [< 1 ms]\r\n"
                      "Failed!  - Failed:     2, Passed:  1705, Skipped:     0, Total:  1707, "
                      "Duration: 2 m 9 s - DynaDocs.Tests.dll (net10.0)\r\n")
            write_file(raw, "raw/altcover-runner.stderr", "")

            with mock.patch.object(gate_adapter, "run_coverage_command", return_value=1):
                answer = gate_adapter.collect_dotnet_coverage(root, raw)

            self.assertEqual([{"gate": "functional", "child_exit": 1,
                               "failingTests": ["DynaDocs.Tests.A.B", "DynaDocs.Tests.C.D(x: 1)"],
                               "failingTestsSummary": "2 failing tests"}],
                             answer["findings"])

    def test_a_failed_python_subject_is_named_from_the_runner_stderr(self):
        with tempfile.TemporaryDirectory() as folder:
            root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
            write_file(raw, "raw/altcover-runner.stdout",
                      "Passed!  - Failed:     0, Passed:  1707, Skipped:     0, Total:  1707, "
                      "Duration: 2 m 9 s - DynaDocs.Tests.dll (net10.0)\r\n")
            write_file(raw, "raw/altcover-runner.stderr",
                      "......F.E.........\r\n"
                      "======================================================================\r\n"
                      "FAIL: test_x (tests.test_csharp_metrics.Case.test_x)\r\n"
                      "----------------------------------------------------------------------\r\n"
                      "AssertionError\r\n"
                      "======================================================================\r\n"
                      "ERROR: test_y (tests.test_csharp_metrics.Case.test_y)\r\n"
                      "----------------------------------------------------------------------\r\n"
                      "ValueError\r\n"
                      "----------------------------------------------------------------------\r\n"
                      "Ran 35 tests in 59.727s\r\n"
                      "\r\n"
                      "FAILED (failures=1, errors=1)\r\n")

            with mock.patch.object(gate_adapter, "run_coverage_command", return_value=1):
                answer = gate_adapter.collect_dotnet_coverage(root, raw)

            finding = answer["findings"][0]
            self.assertEqual(["tests.test_csharp_metrics.Case.test_x",
                              "tests.test_csharp_metrics.Case.test_y"], finding["failingTests"])
            self.assertEqual("2 failing tests", finding["failingTestsSummary"])

    def test_more_failing_tests_than_the_cap_are_counted_and_truncated(self):
        with tempfile.TemporaryDirectory() as folder:
            root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
            blocks = "".join(f"  Failed DynaDocs.Tests.Case.Test{i} [1 ms]\r\n" for i in range(137))
            write_file(raw, "raw/altcover-runner.stdout",
                      blocks + "Failed!  - Failed:   137, Passed:  1570, Skipped:     0, Total:  1707, "
                      "Duration: 2 m 9 s - DynaDocs.Tests.dll (net10.0)\r\n")
            write_file(raw, "raw/altcover-runner.stderr", "")

            with mock.patch.object(gate_adapter, "run_coverage_command", return_value=1):
                answer = gate_adapter.collect_dotnet_coverage(root, raw)

            finding = answer["findings"][0]
            self.assertEqual([f"DynaDocs.Tests.Case.Test{i}" for i in range(10)],
                             finding["failingTests"])
            self.assertEqual("137 failing tests (first 10 named)", finding["failingTestsSummary"])

    def test_an_unparseable_or_missing_stream_says_so_instead_of_implying_no_failures(self):
        for case in ("both-present-naming-nothing", "stderr-absent"):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as folder:
                root, raw = Path(folder) / "repo", Path(folder) / "run/raw-abc"
                write_file(raw, "raw/altcover-runner.stdout", "some unrelated noise\r\n")
                if case == "both-present-naming-nothing":
                    write_file(raw, "raw/altcover-runner.stderr", "some unrelated noise\r\n")
                    expected_summary = ("no failing test named in altcover-runner.stdout or "
                                       "altcover-runner.stderr")
                else:
                    expected_summary = "campaign subject streams unavailable: altcover-runner.stderr"

                with mock.patch.object(gate_adapter, "run_coverage_command", return_value=1):
                    answer = gate_adapter.collect_dotnet_coverage(root, raw)

                self.assertEqual("fail", answer["status"])
                finding = answer["findings"][0]
                self.assertEqual([], finding["failingTests"])
                self.assertEqual(expected_summary, finding["failingTestsSummary"])

    def test_node_coverage_passes_when_the_native_c8_campaign_covers_every_target(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE)
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("pass", answer["status"], json.dumps(answer["findings"]))
            self.assertEqual([], answer["errors"])
            row = collector_row(answer, "javascript-coverage")
            self.assertEqual(0, row["facts"]["child_exit"])
            self.assertEqual(raw.name, row["facts"]["raw"])
            self.assertEqual(["lib/sum.cjs"], [item["path"] for item in row["facts"]["modules"]])
            artifacts = {item["path"]: item["sha256"] for item in row["artifacts"]}
            self.assertEqual({raw.name + "-request.json", raw.name + "/joined.json"},
                             set(artifacts))
            request_path = raw.parent / (raw.name + "-request.json")
            request = json.loads(request_path.read_text(encoding="utf-8"))
            self.assertEqual(["lib/sum.cjs"], request["targets"])
            self.assertEqual(hashlib.sha256(request_path.read_bytes()).hexdigest(),
                             artifacts[raw.name + "-request.json"])
            self.assertEqual(hashlib.sha256((raw / "joined.json").read_bytes()).hexdigest(),
                             artifacts[raw.name + "/joined.json"])

    def test_node_coverage_reports_uncovered_targets_beside_extensionless_gaps(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE_WITH_GAP)
            write_file(root, "bin/launcher", "#!/usr/bin/env node\n")
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("error", answer["status"])
            self.assertEqual(["line-coverage"], [row["gate"] for row in answer["findings"]])
            self.assertEqual([{"gate": "extensionless-javascript", "path": "bin/launcher",
                               "message": "Native analyzer filename identity pending DYD-105"}],
                             answer["errors"])

    def test_node_coverage_refuses_a_reused_output_and_deleted_maintained_inputs(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE)
            raw = Path(folder) / "output/raw"
            raw.mkdir(parents=True)

            answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("error", answer["status"])
            self.assertEqual(2, collector_row(answer, "javascript-coverage")["facts"]["child_exit"])
            committed_repository(root)
            (root / "lib/sum.cjs").unlink()
            with self.assertRaisesRegex(ValueError, "Deleted maintained inputs"):
                gate_adapter.collect_node_coverage(root, Path(folder) / "output/second")

    def test_node_coverage_retains_a_child_failure_but_invalidates_changed_inputs(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE)
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            def mutate(*_args):
                write_file(root, "lib/sum.cjs", NODE_SOURCE + "// changed during campaign\n")
                return 1

            with mock.patch.object(gate_adapter, "run_coverage_command", side_effect=mutate):
                answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("error", answer["status"])
            self.assertEqual([{"gate": "functional", "child_exit": 1}], answer["findings"])
            self.assertEqual("Repository inputs changed during collection",
                             answer["errors"][-1]["message"])

    def test_node_coverage_does_not_join_successful_evidence_after_inputs_change(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE)
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            def mutate(*_args):
                write_file(root, "lib/sum.cjs", NODE_SOURCE + "// changed during campaign\n")
                return 0

            with mock.patch.object(gate_adapter, "run_coverage_command", side_effect=mutate):
                answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("error", answer["status"])
            self.assertEqual([], answer["findings"])
            self.assertEqual("Repository inputs changed during collection",
                             answer["errors"][-1]["message"])

    def test_node_coverage_without_c8_is_measurement_error_not_functional_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "repo"
            write_file(root, "lib/sum.cjs", NODE_SOURCE)
            write_file(root, "DynaDocs.Tests/coverage/javascript_coverage.cjs",
                       (TOOLS / "javascript_coverage.cjs").read_text(encoding="utf-8"))
            write_file(root, "DynaDocs.Tests/coverage/js_metrics.cjs",
                       (TOOLS / "js_metrics.cjs").read_text(encoding="utf-8"))
            write_file(root, "DynaDocs.Tests/coverage/package.json",
                       (TOOLS / "package.json").read_text(encoding="utf-8"))
            write_file(root, ".gitignore", NODE_IGNORED +
                       "DynaDocs.Tests/coverage/js_metrics.cjs\n" +
                       "DynaDocs.Tests/coverage/package.json\n")
            write_file(root, "DynaDocs.Tests/coverage/node_tests.cjs", NODE_TEST_DRIVER)
            write_file(root, "DynaDocs.Tests/coverage/tests/sum.test.cjs", NODE_SUITE)
            git_repository(root)
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("error", answer["status"])
            self.assertEqual([], answer["findings"])
            self.assertEqual(2, collector_row(answer, "javascript-coverage")["facts"]["child_exit"])

    def test_node_coverage_with_available_c8_preserves_a_real_failing_suite(self):
        with tempfile.TemporaryDirectory() as folder:
            root = self.node_repository(folder, NODE_SOURCE)
            write_file(root, "DynaDocs.Tests/coverage/tests/sum.test.cjs",
                       NODE_SUITE.replace("assert.strictEqual(sum(1, 2), 3)",
                                          "assert.fail('fixture regression')"))
            raw = Path(folder) / "output/raw"
            raw.parent.mkdir()

            answer = gate_adapter.collect_node_coverage(root, raw)

            self.assertEqual("fail", answer["status"])
            self.assertEqual([], answer["errors"])
            self.assertEqual([{"gate": "functional", "child_exit": 1}], answer["findings"])


if __name__ == "__main__":
    unittest.main()

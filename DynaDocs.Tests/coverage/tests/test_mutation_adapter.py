"""Process-boundary and seam tests of the exclusive mutation adapter.

Selection is exercised over inventory envelopes written here as data, template validation over the
three templates the repository ships, and the engine launch over a recorded `windows_job.run`, so
nothing in this module starts an engine.
"""
import hashlib
import json
import shlex
import subprocess
import sys
import tempfile
import tomllib
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mutation_adapter

COVERAGE = Path(__file__).resolve().parents[1]
MUTATION = COVERAGE / "mutation"
PROJECT = "DynaDocs.csproj"


def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def source(path, language, role, projects=(), executable=True, test_files=()):
    return {"path": path, "sha256": digest(path), "language": language, "role": role,
            "projects": list(projects), "executable": executable, "testFiles": list(test_files)}


def envelope(sources=(), excluded=(), projects=(), extra_files=()):
    """One complete schema-1 inventory envelope, as DYD-96's producer writes it."""
    files = sorted([{"path": row["path"], "sha256": row["sha256"]} for row in sources]
                   + list(extra_files), key=lambda row: row["path"])
    return {"schema": 1,
            "candidate": {"commit": "0" * 40, "dirty": False, "sourceFingerprint": "1" * 64},
            "files": files, "sources": list(sources), "excluded": list(excluded),
            "projects": list(projects), "errors": []}


def selection(mode, reason, changed_targets=(), selected=(), witness=(), gaps=()):
    return {"mode": mode, "reason": reason, "changedTargets": list(changed_targets),
            "selected": list(selected), "witness": list(witness), "gaps": list(gaps)}


PYTHON_TARGET = source("src/alpha.py", "python", "target", test_files=["tests/test_alpha.py"])
PYTHON_PEER = source("src/beta.py", "python", "target", test_files=["tests/test_alpha.py"])
PYTHON_TEST = source("tests/test_alpha.py", "python", "test", executable=False)
NODE_TARGET = source("src/value.cjs", "javascript", "target")
NODE_TEST = source("src/value.test.cjs", "javascript", "test", executable=False)
CS_TARGET = source("src/Number.cs", "cs", "target", projects=[PROJECT])
CS_FOREIGN = source("metrics/Gate.cs", "cs", "target", projects=["metrics/GateMetrics.csproj"])

PYTHON_INVENTORY = envelope([PYTHON_TARGET, PYTHON_PEER, PYTHON_TEST])
NODE_INVENTORY = envelope([NODE_TARGET, NODE_TEST])
CS_INVENTORY = envelope([CS_TARGET, CS_FOREIGN],
                        projects=[{"path": PROJECT, "compile": ["src/Number.cs"],
                                   "testProject": False, "assembly": "bin/DynaDocs.dll"},
                                  {"path": "metrics/GateMetrics.csproj",
                                   "compile": ["metrics/Gate.cs"], "testProject": False,
                                   "assembly": "metrics/bin/GateMetrics.dll"}])


class ConfigurationWideningTests(unittest.TestCase):
    """The build-configuration table is the specification's, spelled once."""

    def test_the_widening_table_is_the_specified_one(self):
        self.assertEqual(
            {"dotnet": (".config/dotnet-tools.json", ".editorconfig", "Directory.Build.props",
                        "Directory.Build.targets", "DynaDocs.Tests/coverage/gap_check.json",
                        "DynaDocs.Tests/coverage/mutation/stryker-net.json",
                        "DynaDocs.Tests/coverage/test-associations.json", "DynaDocs.sln",
                        "global.json"),
             "python": ("DynaDocs.Tests/coverage/gap_check.json",
                        "DynaDocs.Tests/coverage/mutation/cosmic-ray.toml",
                        "DynaDocs.Tests/coverage/mutation/requirements.lock",
                        "DynaDocs.Tests/coverage/mutation/requirements.txt",
                        "DynaDocs.Tests/coverage/requirements.lock",
                        "DynaDocs.Tests/coverage/requirements.txt",
                        "DynaDocs.Tests/coverage/test-associations.json"),
             "node": ("DynaDocs.Tests/coverage/gap_check.json",
                      "DynaDocs.Tests/coverage/mutation/stryker-js.json",
                      "DynaDocs.Tests/coverage/test-associations.json")},
            mutation_adapter.CONFIG_WIDENING)

    def test_every_stack_names_its_own_inventory_language(self):
        self.assertEqual({"dotnet": "cs", "python": "python", "node": "javascript"},
                         mutation_adapter.STACK_LANGUAGE)


class SelectionTests(unittest.TestCase):
    """Wider selection is always allowed; narrower than the changed file set never."""

    def test_no_changed_path_of_this_language_is_no_obligation(self):
        self.assertEqual(
            selection("none", "no changed target", witness=[
                {"path": "README.md", "classification": "no obligation"}]),
            mutation_adapter.select(PYTHON_INVENTORY, {"README.md": "M"}, "python"))

    def test_one_changed_target_selects_exactly_that_file(self):
        self.assertEqual(
            selection("changed", "changed target", ["src/alpha.py"], ["src/alpha.py"]),
            mutation_adapter.select(PYTHON_INVENTORY, {"src/alpha.py": "M"}, "python"))

    def test_a_changed_test_file_selects_every_target_that_lists_it(self):
        self.assertEqual(
            selection("changed", "changed target", ["src/alpha.py", "src/beta.py"],
                      ["src/alpha.py", "src/beta.py"]),
            mutation_adapter.select(PYTHON_INVENTORY, {"tests/test_alpha.py": "M"}, "python"))

    def test_a_test_file_no_target_lists_widens_the_whole_stack(self):
        orphan = envelope([NODE_TARGET, NODE_TEST])
        self.assertEqual(
            selection("widened", "test change with no associated target",
                      selected=["src/value.cjs"]),
            mutation_adapter.select(orphan, {"src/value.test.cjs": "M"}, "node"))

    def test_a_deleted_source_widens_the_whole_stack(self):
        self.assertEqual(
            selection("widened", "deleted or renamed source",
                      selected=["src/alpha.py", "src/beta.py"]),
            mutation_adapter.select(PYTHON_INVENTORY, {"src/gone.py": "D"}, "python"))

    def test_a_changed_excluded_source_widens_the_whole_stack(self):
        excluded = envelope([PYTHON_TARGET, PYTHON_PEER, PYTHON_TEST], excluded=[
            {"path": "src/derived.py", "reason": "derived-copy",
             "origin": {"source": "src/alpha.py", "canonicalSourceSha256": digest("src/alpha.py"),
                        "producer": "src/make.py", "producerTest": "tests/test_alpha.py"}}])
        self.assertEqual(
            selection("widened", "excluded source changed",
                      selected=["src/alpha.py", "src/beta.py"]),
            mutation_adapter.select(excluded, {"src/derived.py": "M"}, "python"))

    def test_a_changed_build_configuration_path_widens_its_named_stack(self):
        self.assertEqual(
            selection("widened", "configuration changed", selected=["src/Number.cs"]),
            mutation_adapter.select(CS_INVENTORY, {".config/dotnet-tools.json": "M"}, "dotnet"))

    def test_any_package_manifest_widens_the_node_stack(self):
        self.assertEqual(
            selection("widened", "configuration changed", selected=["src/value.cjs"]),
            mutation_adapter.select(NODE_INVENTORY, {"npm/dydo/package.json": "M"}, "node"))

    def test_a_widened_dotnet_campaign_leaves_out_every_foreign_project(self):
        self.assertEqual(["src/Number.cs"],
                         mutation_adapter.select(CS_INVENTORY, {"DynaDocs.sln": "M"},
                                                 "dotnet")["selected"])

    def test_a_changed_path_the_inventory_does_not_know_is_invalid(self):
        self.assertEqual(
            [{"reason": "inventory is stale: src/unknown.py", "path": "src/unknown.py"}],
            mutation_adapter.select(PYTHON_INVENTORY, {"src/unknown.py": "M"}, "python")["gaps"])

    def test_a_changed_source_in_neither_sources_nor_excluded_is_invalid(self):
        unclassified = envelope([PYTHON_TARGET, PYTHON_PEER, PYTHON_TEST],
                                extra_files=[{"path": "src/loose.py", "sha256": digest("loose")}])
        self.assertEqual(
            [{"reason": "unclassified source: src/loose.py", "path": "src/loose.py"}],
            mutation_adapter.select(unclassified, {"src/loose.py": "M"}, "python")["gaps"])

    def test_a_changed_c_sharp_target_outside_the_mutated_project_has_no_route(self):
        self.assertEqual(
            [{"reason": "no .NET test project route: metrics/Gate.cs "
                        "(metrics/GateMetrics.csproj)", "path": "metrics/Gate.cs"}],
            mutation_adapter.select(CS_INVENTORY, {"metrics/Gate.cs": "M"}, "dotnet")["gaps"])

    def test_a_selected_javascript_target_without_an_extension_cannot_be_parsed(self):
        extensionless = envelope([source("npm/dydo/bin", "javascript", "target")])
        self.assertEqual(
            [{"reason": "extensionless target: npm/dydo/bin", "path": "npm/dydo/bin"}],
            mutation_adapter.select(extensionless, {"npm/dydo/bin": "M"}, "node")["gaps"])


class TemplateValidationTests(unittest.TestCase):
    """Campaign settings are pinned in the templates and checked before any launch."""

    def stryker_net(self, **overrides):
        template = json.loads((MUTATION / "stryker-net.json").read_text(encoding="utf-8"))
        template["stryker-config"].update(overrides)
        return template

    def stryker_js(self, **overrides):
        template = json.loads((MUTATION / "stryker-js.json").read_text(encoding="utf-8"))
        template.update(overrides)
        return template

    def cosmic_ray(self, **overrides):
        template = tomllib.loads((MUTATION / "cosmic-ray.toml").read_text(encoding="utf-8"))
        template["cosmic-ray"].update(overrides)
        return template

    def test_the_shipped_dotnet_template_validates_and_another_concurrency_does_not(self):
        self.assertEqual(
            {"shipped": [], "departed": ["concurrency"]},
            {"shipped": mutation_adapter.validate_template("dotnet", self.stryker_net()),
             "departed": mutation_adapter.validate_template("dotnet",
                                                            self.stryker_net(concurrency=2))})

    def test_a_dotnet_template_that_lowers_a_threshold_is_invalid(self):
        self.assertEqual(["thresholds"], mutation_adapter.validate_template(
            "dotnet", self.stryker_net(thresholds={"high": 100, "low": 100, "break": 80})))

    def test_a_dotnet_template_that_drops_a_reporter_is_invalid(self):
        self.assertEqual(["reporters"], mutation_adapter.validate_template(
            "dotnet", self.stryker_net(reporters=["json"])))

    def test_a_dotnet_template_carrying_a_forbidden_key_is_invalid(self):
        self.assertEqual(["since"], mutation_adapter.validate_template(
            "dotnet", self.stryker_net(since="main")))

    def test_the_shipped_node_template_validates_and_coverage_analysis_on_does_not(self):
        self.assertEqual(
            {"shipped": [], "departed": ["coverageAnalysis"]},
            {"shipped": mutation_adapter.validate_template("node", self.stryker_js()),
             "departed": mutation_adapter.validate_template(
                 "node", self.stryker_js(coverageAnalysis="perTest"))})

    def test_a_node_template_that_leaves_in_place_off_is_invalid(self):
        self.assertEqual(["inPlace"], mutation_adapter.validate_template(
            "node", self.stryker_js(inPlace=False)))

    def test_a_node_template_with_another_concurrency_is_invalid(self):
        self.assertEqual(["concurrency"], mutation_adapter.validate_template(
            "node", self.stryker_js(concurrency=2)))

    def test_the_shipped_python_template_validates_and_another_distributor_does_not(self):
        template = tomllib.loads((MUTATION / "cosmic-ray.toml").read_text(encoding="utf-8"))
        template["cosmic-ray"]["distributor"] = {"name": "http"}
        self.assertEqual(
            {"shipped": [], "departed": ["distributor"]},
            {"shipped": mutation_adapter.validate_template("python", self.cosmic_ray()),
             "departed": mutation_adapter.validate_template("python", template)})


class GeneratedRunnerTests(unittest.TestCase):
    """The runner is the command's only entry point and carries this campaign's nonce."""

    NONCE = "1f2a7c0b9d4e4f6ab3c85d17e0a2469c"

    def test_the_generated_runner_embeds_the_campaign_nonce_and_writes_it_last(self):
        runner = mutation_adapter.render_suite_runner(self.NONCE)
        self.assertEqual(
            (1, True, True, True),
            (runner.count(self.NONCE),
             "##DYDO-SUITE-COMPLETE %s exit=%d##" in runner,
             'reconfigure(encoding="utf-8", errors="backslashreplace")' in runner,
             "runpy.run_module" in runner and "runpy.run_path" in runner))

    def test_the_generated_runner_reports_the_suites_own_exit_code(self):
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-runner-")
        self.addCleanup(directory.cleanup)
        runner = Path(directory.name) / "suite_runner.py"
        runner.write_text(mutation_adapter.render_suite_runner(self.NONCE), encoding="utf-8",
                          newline="\n")
        (Path(directory.name) / "test_red.py").write_text(
            "import unittest\n\n\nclass T(unittest.TestCase):\n"
            "    def test_fails(self):\n        self.assertEqual(1, 2)\n",
            encoding="utf-8", newline="\n")
        finished = subprocess.run(
            [sys.executable, str(runner), "-m", "unittest", "discover", "-s", ".", "-p",
             "test_*.py"], cwd=directory.name, capture_output=True, text=True, timeout=120)
        lines = [line for line in finished.stdout.splitlines() if line.strip()]
        self.assertEqual((1, f"##DYDO-SUITE-COMPLETE {self.NONCE} exit=1##"),
                         (finished.returncode, lines[-1].rstrip() if lines else ""))

    def test_the_cosmic_ray_command_is_joined_the_way_the_engine_splits_it(self):
        command = mutation_adapter.render_cosmic_test_command(
            r"C:\Program Files\python\python.exe", r"C:\run\python\suite_runner.py",
            ["-m", "unittest", "discover", "-s", ".", "-p", "test_*.py"])
        self.assertEqual(
            [r"C:\Program Files\python\python.exe", r"C:\run\python\suite_runner.py",
             "-m", "unittest", "discover", "-s", ".", "-p", "test_*.py"],
            shlex.split(command))


class EngineLaunchTests(unittest.TestCase):
    """Every launch is contained, and this caller's own bound is the one that applies."""

    LIMIT = 14400

    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-caller-")
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name).resolve()
        (self.root / "src").mkdir()
        (self.root / "DynaDocs.Tests/coverage").mkdir(parents=True)
        (self.root / "src/alpha.py").write_text("def value():\n    return 1234\n",
                                                encoding="utf-8", newline="\n")
        (self.root / "DynaDocs.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n'
            "    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n",
            encoding="utf-8", newline="\n")
        (self.root / "DynaDocs.Tests/coverage/test-associations.json").write_text(
            json.dumps({"schema": 1, "modules": []}), encoding="utf-8", newline="\n")
        git = ["git", "-c", "user.name=dydo", "-c", "user.email=dydo@example.invalid"]
        subprocess.run(["git", "init", "-q", "-b", "main"], cwd=self.root, check=True,
                       capture_output=True)
        subprocess.run([*git, "add", "-A"], cwd=self.root, check=True, capture_output=True)
        subprocess.run([*git, "commit", "-qm", "base"], cwd=self.root, check=True,
                       capture_output=True)
        self.base = subprocess.run(["git", "rev-parse", "HEAD"], cwd=self.root, check=True,
                                   capture_output=True, text=True).stdout.strip()
        self.summary = self.root / "DynaDocs.Tests/coverage/results/adapters/python-mutation.json"

    def drive(self, result):
        launches = []

        def recorder(value, environment=None, **keywords):
            launches.append({"value": value, "keywords": keywords})
            return dict(result)

        with mock.patch.object(mutation_adapter.run_tests, "ROOT", self.root), \
                mock.patch.object(mutation_adapter.windows_job, "run", recorder):
            code = mutation_adapter.main(["--stack", "python", "--since", self.base,
                                          "--root", str(self.root), "--output", str(self.summary)])
        published = json.loads(self.summary.read_text(encoding="utf-8")) \
            if self.summary.is_file() else None
        return code, launches, published

    def complete(self, **overrides):
        return {"complete": True, "cleanup_confirmed": True, "subject_status": 0,
                "stdout_path": str(self.root / "stdout.log"),
                "stderr_path": str(self.root / "stderr.log"), "elapsed_seconds": 1.0, **overrides}

    def test_every_engine_launch_carries_this_callers_own_maximum(self):
        _, launches, _ = self.drive(self.complete())
        self.assertEqual(
            {"launched": True, "maxima": {self.LIMIT}},
            {"launched": bool(launches),
             "maxima": {call["keywords"].get("execution_seconds_maximum") for call in launches}})

    def test_a_campaign_that_reaches_that_maximum_is_invalid(self):
        code, _, published = self.drive(
            self.complete(complete=False, elapsed_seconds=float(self.LIMIT)))
        self.assertEqual(
            (2, [{"reason": f"campaign limit exceeded ({self.LIMIT} s)"}], False),
            (code, (published or {}).get("gaps"), (published or {}).get("measurementComplete")))


if __name__ == "__main__":
    unittest.main()

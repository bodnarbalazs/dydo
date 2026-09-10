"""Process-boundary and seam tests of the exclusive mutation adapter.

Selection is exercised over inventory envelopes written here as data, template validation over the
three templates the repository ships, and one whole invocation over a candidate committed here
with a recorded `windows_job.run`, so nothing in this module starts an engine.
"""
import hashlib
import json
import shlex
import shutil
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

ALPHA_PY = "def value():\n    return 1234\n"
TEST_ALPHA_PY = ("import unittest\n\nimport sys\nsys.path.insert(0, 'src')\nimport alpha\n\n\n"
                 "class ValueTests(unittest.TestCase):\n"
                 "    def test_value_is_1234(self):\n"
                 "        self.assertEqual(1234, alpha.value())\n")
CSPROJ = ('<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup>\n'
          "    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n")
NUMBER_CS = ("namespace Subject;\n\npublic static class Number\n{\n"
             "    public static int Value(int input) => input + 1;\n}\n")
OTHER_CS = ("namespace Subject;\n\npublic static class Other\n{\n"
            "    public static int Half(int input) => input * 2;\n}\n")
PYTHON_TEST_COMMAND = ["-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py"]


def testing_manifest(kind="current-python", argv=None):
    """The DYD-96 manifest, carrying the one row the adapter reads: the stack's test command."""
    return {"schema": 1, "artifactRoot": "DynaDocs.Tests/coverage/results",
            "stacks": [{"name": "python", "kind": "fixture", "cwd": ".",
                        "isolation": {"requirement": "in-place",
                                      "evidence": {"state": "verified", "kind": "direct"}},
                        "capabilities": {
                            "test": {"state": "configured", "artifacts": [],
                                     "command": {"kind": kind,
                                                 "argv": list(argv or PYTHON_TEST_COMMAND)}}}}]}


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


class InventoryBoundaryTests(unittest.TestCase):
    """The whole schema-1 envelope is judged before one selection field is read.

    The producer is DYD-96's, so every one of these departures is a boundary this adapter owns:
    a selection taken from an envelope it cannot spell out is measurement of nothing.
    """

    def refusal(self, payload):
        with self.assertRaises(mutation_adapter._Refusal) as raised:
            mutation_adapter._validate_inventory(payload)
        return [gap["reason"] for gap in raised.exception.gaps]

    def test_the_producers_own_envelope_is_accepted(self):
        self.assertIsNone(mutation_adapter._validate_inventory(PYTHON_INVENTORY))

    def test_an_unknown_root_property_is_invalid(self):
        self.assertEqual(["invalid inventory: root properties"],
                         self.refusal({**PYTHON_INVENTORY, "tools": {}}))

    def test_a_duplicate_source_row_is_invalid(self):
        self.assertEqual(
            ["invalid inventory: sources order"],
            self.refusal({**PYTHON_INVENTORY,
                          "sources": [PYTHON_TARGET, PYTHON_TARGET, PYTHON_PEER, PYTHON_TEST]}))

    def test_a_case_folding_alias_of_a_maintained_path_is_invalid(self):
        alias = {"path": "src/Alpha.py", "sha256": digest("src/Alpha.py")}
        self.assertEqual(
            ["invalid inventory: files order"],
            self.refusal(envelope([PYTHON_TARGET, PYTHON_PEER, PYTHON_TEST],
                                  extra_files=[alias])))

    def test_rows_out_of_canonical_order_are_invalid(self):
        self.assertEqual(
            ["invalid inventory: files order"],
            self.refusal({**PYTHON_INVENTORY,
                          "files": list(reversed(PYTHON_INVENTORY["files"]))}))

    def test_a_file_row_of_another_shape_is_invalid(self):
        rows = [{**row, "sha256": None} if row["path"] == "src/alpha.py" else row
                for row in PYTHON_INVENTORY["files"]]
        self.assertEqual(["invalid inventory: file row src/alpha.py"],
                         self.refusal({**PYTHON_INVENTORY, "files": rows}))

    def test_a_source_row_of_another_shape_is_invalid(self):
        rows = [{**row, "coverage": 1} if row["path"] == "src/alpha.py" else row
                for row in PYTHON_INVENTORY["sources"]]
        self.assertEqual(["invalid inventory: source row src/alpha.py"],
                         self.refusal({**PYTHON_INVENTORY, "sources": rows}))


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


class CandidateTestCase(unittest.TestCase):
    """A committed candidate mirroring the coverage layout, for whole-invocation tests.

    The campaign templates are the ones the repository ships, so a campaign that reaches them
    reads what the project pins rather than a paraphrase of it.
    """

    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-caller-")
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name).resolve()
        self.write("src/alpha.py", ALPHA_PY)
        self.write("tests/test_alpha.py", TEST_ALPHA_PY)
        self.write("DynaDocs.csproj", CSPROJ)
        self.write("DynaDocs.Tests/coverage/test-associations.json", json.dumps(
            {"schema": 1, "modules": [{"module": "src/alpha.py",
                                       "tests": ["tests/test_alpha.py"]}]}, indent=1))
        self.write("DynaDocs.Tests/coverage/gap_check.json",
                   json.dumps(testing_manifest(), indent=1))
        (self.root / "DynaDocs.Tests/coverage/mutation").mkdir(parents=True, exist_ok=True)
        for template in ("cosmic-ray.toml", "stryker-net.json"):
            shutil.copyfile(MUTATION / template,
                            self.root / "DynaDocs.Tests/coverage/mutation" / template)
        subprocess.run(["git", "init", "-q", "-b", "main"], cwd=self.root, check=True,
                       capture_output=True)
        self.base = self.commit("base")
        self.summary = self.root / "DynaDocs.Tests/coverage/results/adapters/python-mutation.json"

    def write(self, relative, text):
        destination = self.root / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(text, encoding="utf-8", newline="\n")
        return destination

    def git(self, *arguments, check=True):
        return subprocess.run(
            ["git", "-c", "user.name=dydo", "-c", "user.email=dydo@example.invalid", *arguments],
            cwd=self.root, check=check, capture_output=True, text=True)

    def commit(self, message):
        self.git("add", "-A")
        self.git("commit", "-qm", message)
        return self.git("rev-parse", "HEAD").stdout.strip()


class EngineLaunchTests(CandidateTestCase):
    """Every launch is contained, and this caller's own bound is the one that applies."""

    LIMIT = 14400

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


class CampaignRefusalTests(CandidateTestCase):
    """One whole invocation's fail-closed seams, over a recorded `windows_job.run`.

    Each of these is a refusal the campaign takes before it can report anything, so each test
    asks for the published gaps exactly: a seam that stopped believing its own guard would go on
    to refuse for another reason, or to pass, and either is a different list.
    """

    # The one recorded reading a campaign over this candidate depends on before it runs.
    RESTORED = {"cosmic-ray-version": "8.7.0\n"}

    def setUp(self):
        super().setUp()
        self.write("src/alpha.py", ALPHA_PY + "\n\ndef doubled(value):\n    return value * 2\n")
        self.commit("change the python target")
        self.launches = []

    def launcher(self, outputs=None, results=None):
        """Stand in for `windows_job.run`: the recorded evidence of each contained launch."""
        recorded, departed = dict(outputs or {}), dict(results or {})

        def run(value, environment=None, **keywords):
            output = Path(value["output"])
            output.mkdir(parents=True, exist_ok=True)
            name = output.name.removeprefix("job-")
            (output / "stdout.log").write_text(recorded.get(name, ""), encoding="utf-8")
            (output / "stderr.log").write_text("", encoding="utf-8")
            self.launches.append(name)
            return {"complete": True, "cleanup_confirmed": True, "subject_status": 0,
                    "stdout_path": str(output / "stdout.log"),
                    "stderr_path": str(output / "stderr.log"), "elapsed_seconds": 1.0,
                    **departed.get(name, {})}
        return run

    def drive(self, run, stack="python"):
        """Run one campaign over this candidate and return its exit and its summary."""
        summary = self.root / f"DynaDocs.Tests/coverage/results/adapters/{stack}-mutation.json"
        with mock.patch.object(mutation_adapter.run_tests, "ROOT", self.root), \
                mock.patch.object(mutation_adapter.windows_job, "run", run):
            code = mutation_adapter.main(["--stack", stack, "--since", self.base,
                                          "--root", str(self.root), "--output", str(summary)])
        return code, json.loads(summary.read_text(encoding="utf-8"))

    def clear_snapshot(self, remove, snapshot):
        """Remove a snapshot the campaign under test was kept from removing, however it ends.

        `remove` is the real remover, taken before the test replaced it: this cleanup runs
        while that replacement is still in place.
        """
        with mock.patch.object(mutation_adapter.run_tests, "ROOT", self.root):
            remove(snapshot)

    def generated(self, relative):
        """One configuration this invocation generated for its engine, from the retained run."""
        assurance = self.root / "DynaDocs.Tests/coverage/results/assurance"
        return json.loads(next(iter(assurance.glob(f"run-*/{relative}")))
                          .read_text(encoding="utf-8"))

    def producer(self, edit):
        """DYD-96's inventory producer, writing one envelope that departs by `edit`."""
        def produce(root, run, candidate):
            _, files = mutation_adapter.gate_adapter._candidate(root)
            document = edit({"schema": 1, "candidate": candidate, "files": files, "sources": [],
                             "excluded": [], "projects": [], "errors": []})
            path = run / "inventory.json"
            path.write_text(json.dumps(document, indent=2, sort_keys=True) + "\n",
                            encoding="utf-8")
            return path, document["errors"]
        self.enterContext(mock.patch.object(mutation_adapter.gate_adapter,
                                            "_inventory_artifact", produce))

    def suite_output(self, suite):
        """What the generated runner really prints for one suite, and the code it really exits."""
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-suite-")
        self.addCleanup(directory.cleanup)
        runner = Path(directory.name) / "suite_runner.py"
        runner.write_text(mutation_adapter.render_suite_runner("0" * 32), encoding="utf-8",
                          newline="\n")
        tests = Path(directory.name) / "tests"
        tests.mkdir()
        (tests / "test_suite.py").write_text(suite, encoding="utf-8", newline="\n")
        finished = subprocess.run(
            [sys.executable, str(runner), *PYTHON_TEST_COMMAND], cwd=directory.name,
            capture_output=True, text=True, timeout=120)
        return finished.returncode, finished.stdout

    def test_a_host_the_owner_refuses_never_starts_a_campaign(self):
        self.enterContext(mock.patch.object(
            mutation_adapter.windows_job, "preflight",
            mock.Mock(side_effect=ValueError("Owner requires Windows Python 3.12.14"))))
        code, published = self.drive(self.launcher(self.RESTORED))
        self.assertEqual(
            (2, [{"reason": "unsupported host: Owner requires Windows Python 3.12.14"}], []),
            (code, published["gaps"], self.launches))

    def test_a_launch_whose_containment_is_not_confirmed_is_invalid(self):
        code, published = self.drive(
            self.launcher(self.RESTORED, {"baseline": {"cleanup_confirmed": False}}))
        self.assertEqual((2, [{"reason": "engine did not complete"}]),
                         (code, published["gaps"]))

    def test_a_snapshot_this_invocation_cannot_verify_gone_is_invalid(self):
        retained, remove = [], mutation_adapter.run_tests.remove_worktree
        self.enterContext(mock.patch.object(mutation_adapter.run_tests, "remove_worktree",
                                            retained.append))
        code, published = self.drive(self.launcher())
        self.addCleanup(self.clear_snapshot, remove, retained[0])
        self.assertEqual(
            {"code": 2, "gap": {"reason": f"snapshot removal unverified: {retained[0]}"},
             "retained": True},
            {"code": code, "gap": published["gaps"][-1], "retained": retained[0].exists()})

    def test_a_python_baseline_that_does_not_report_suite_completion_is_invalid(self):
        # A suite leaving by `os._exit` never lets the runner write its completion marker, and
        # the process still exits 0: the campaign has nothing to measure against.
        status, observed = self.suite_output("import os\n\nos._exit(0)\n")
        code, published = self.drive(
            self.launcher({**self.RESTORED, "baseline": observed}))
        self.assertEqual(
            {"suite": (0, ""), "code": 2, "baseline": 0,
             "gaps": [{"reason": "baseline did not report suite completion"}]},
            {"suite": (status, observed), "code": code,
             "baseline": published["mutation"]["baseline"]["exit"],
             "gaps": published["gaps"]})

    def test_a_changed_dotnet_campaign_hands_the_engine_every_changed_target(self):
        self.write("src/Number.cs", NUMBER_CS)
        self.write("src/Other.cs", OTHER_CS)
        self.commit("two changed C# targets")
        code, published = self.drive(
            self.launcher({"tool-list": "dotnet-stryker      4.16.0\n"}), stack="dotnet")
        self.assertEqual(
            {"code": 2, "gaps": [{"reason": "no mutation report produced (Stryker.NET exit 0)"}],
             "mutate": ["src/Number.cs", "src/Other.cs"]},
            {"code": code, "gaps": published["gaps"],
             "mutate": self.generated("dotnet/stryker-config.json")["stryker-config"]["mutate"]})

    def test_a_python_test_command_the_runner_cannot_dispatch_is_invalid(self):
        self.write("DynaDocs.Tests/coverage/gap_check.json",
                   json.dumps(testing_manifest(kind="argv", argv=["python", "-m", "pytest"]),
                              indent=1))
        self.commit("a python test command no in-process runner can dispatch")
        code, published = self.drive(self.launcher(self.RESTORED))
        self.assertEqual(
            (2, [{"reason": "unsupported python test command: ['python', '-m', 'pytest']"}]),
            (code, published["gaps"]))

    def test_an_inventory_whose_fingerprint_is_not_the_snapshots_is_invalid(self):
        self.producer(lambda document: {
            **document, "candidate": {**document["candidate"], "sourceFingerprint": "0" * 64}})
        code, published = self.drive(self.launcher(self.RESTORED))
        self.assertEqual(
            (2, [{"reason": "inventory sourceFingerprint does not match the snapshot"}]),
            (code, published["gaps"]))

    def test_a_duplicate_inventory_row_refuses_instead_of_selecting(self):
        def duplicated(document):
            row = {"path": "src/alpha.py", "language": "python", "role": "target", "projects": [],
                   "executable": True, "testFiles": [],
                   "sha256": {row["path"]: row["sha256"]
                              for row in document["files"]}["src/alpha.py"]}
            return {**document, "sources": [row, row]}

        self.producer(duplicated)
        code, published = self.drive(self.launcher(self.RESTORED))
        self.assertEqual(
            (2, [{"reason": "invalid inventory: sources order"}], None),
            (code, published["gaps"], published["mutation"]["selection"]))

    def test_an_unresolved_index_entry_refuses_and_leaves_no_worktree_behind(self):
        self.git("checkout", "-q", "-b", "divergent", self.base)
        self.write("src/alpha.py", ALPHA_PY + "\n\ndef trebled(value):\n    return value * 3\n")
        self.commit("a divergent change to the same target")
        self.git("checkout", "-q", "main")
        self.git("merge", "divergent", check=False)
        snapshots = []
        create = mutation_adapter.run_tests.create_worktree

        def record(path):
            snapshots.append(path)
            return create(path)

        self.enterContext(mock.patch.object(mutation_adapter.run_tests, "create_worktree", record))
        code, published = self.drive(self.launcher(self.RESTORED))
        self.addCleanup(self.clear_snapshot, mutation_adapter.run_tests.remove_worktree,
                        snapshots[0])
        reason = published["gaps"][0]["reason"]
        self.assertEqual(
            {"code": 2, "gaps": 1, "refused": True, "snapshot": False, "worktrees": 1},
            {"code": code, "gaps": len(published["gaps"]),
             "refused": reason.startswith(f"cannot snapshot the candidate at {snapshots[0]}: ")
                        and "Malformed or unresolved Git status" in reason,
             "snapshot": snapshots[0].exists(),
             "worktrees": self.git("worktree", "list",
                                   "--porcelain").stdout.count("worktree ")})


if __name__ == "__main__":
    unittest.main()

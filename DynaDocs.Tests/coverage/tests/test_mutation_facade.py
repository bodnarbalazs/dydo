"""One named probe per @DYD-103 scenario of `Features/mutation-assurance.feature`.

Every probe drives the real `gap_check.py` over a temporary Git repository that mirrors the
coverage layout, with the real adapter, the real normalizer and the real DYD-96 inventory
producer. Only the three engines are shimmed, and each shim emits a report or session captured
from a real engine (`fixtures/mutation/origin.json` records their provenance), re-keyed onto the
files of the campaign it stands in for. No probe launches a real engine.

`DynaDocs.Tests/Steps/MutationAssuranceSteps.cs` maps every scenario title, and every outline
argument row, to exactly one probe named here.
"""
import json
import os
import queue
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
import tomllib
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
COVERAGE = ROOT / "DynaDocs.Tests/coverage"
FIXTURES = Path(__file__).resolve().parent / "fixtures/mutation"
ADAPTER = "DynaDocs.Tests/coverage/mutation_adapter.py"


def _project_manifest_configures_mutation():
    """Whether the real project manifest already carries the three configured mutation rows."""
    try:
        manifest = json.loads((COVERAGE / "gap_check.json").read_text(encoding="utf-8"))
        return all(stack["capabilities"]["mutation"].get("state") == "configured"
                   for stack in manifest["stacks"])
    except (OSError, ValueError, KeyError, TypeError):
        return False


# DYD-130 lands DYD-96, after which spec step 7 configures the three mutation rows in the real
# DynaDocs.Tests/coverage/gap_check.json. The two probes that bind that manifest activate then.
PROJECT_MANIFEST_CONFIGURED = _project_manifest_configures_mutation()
AWAITING_DYD_130 = "DYD-130 then spec step 7 configure the project manifest's mutation rows"

NUMBER_CS = """namespace Subject;

public static class Number
{
    public static int Value(int input) => input + 1;
}
"""
OTHER_CS = """namespace Subject;

public static class Other
{
    public static int Half(int input) => input * 2;
}
"""
GATE_CS = """namespace Metrics;

public static class Gate
{
    public static int Value(int input) => input + 1;
}
"""
MOD_PY = "def flag():\n    return True\n"
TEST_MOD_PY = ("import unittest\n\nimport sys\nsys.path.insert(0, 'src')\nimport mod\n\n\n"
               "class FlagTests(unittest.TestCase):\n"
               "    def test_flag_is_true(self):\n        self.assertTrue(mod.flag())\n")
FAILING_TEST_MOD_PY = ("import unittest\n\n\nclass FlagTests(unittest.TestCase):\n"
                       "    def test_fails(self):\n        self.assertEqual(1, 2)\n")
VALUE_CJS = "exports.value = () => 1234;\n"
VALUE_TEST_CJS = ("const { test } = require('node:test');\n"
                  "const assert = require('node:assert/strict');\n"
                  "const { value } = require('../src/value.cjs');\n\n"
                  "test('value is exactly 1234', () => assert.equal(value(), 1234));\n")
FAILING_TEST_CJS = ("const { test } = require('node:test');\n"
                    "const assert = require('node:assert/strict');\n\n"
                    "test('always fails', () => assert.equal(1, 2));\n")
def compile_items(paths):
    return "".join(f'    <Compile Include="{path}" />\n' for path in sorted(paths))


CSPROJ = '''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
{items}  </ItemGroup>
</Project>
'''

# Every shim reads the `shim.json` beside it, so nothing depends on an environment the adapter
# deliberately strips.
DOTNET_SHIM_CMD = '@echo off\r\n"{python}" "{script}" %*\r\n'

DOTNET_SHIM = r'''"""Stand in for the dotnet CLI: `tool list --local` and `stryker`."""
import json, shutil, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PLAN = json.loads((HERE / "shim.json").read_text(encoding="utf-8"))
ARGV = sys.argv[1:]

if ARGV[:2] == ["tool", "list"]:
    print("Package Id      Version      Commands")
    print("-------------------------------------")
    for entry in PLAN.get("toolList", []):
        print(entry)
    raise SystemExit(0)

if not ARGV or ARGV[0] != "stryker":
    raise SystemExit(0)

plan = PLAN["stryker"]
if plan.get("sleep"):
    __import__("time").sleep(plan["sleep"])
output = Path(ARGV[ARGV.index("--output") + 1])
configuration = json.loads(Path(ARGV[ARGV.index("--config-file") + 1]).read_text(encoding="utf-8"))
project_root = Path(configuration["stryker-config"]["project"]).resolve().parent
if plan.get("report", True):
    report = json.loads((Path(PLAN["fixtures"]) / plan["fixture"]).read_text(encoding="utf-8"))
    keys = sorted(report["files"])
    files = {}
    for index, key in enumerate(keys):
        files[str(project_root / plan["files"][index])] = report["files"][key]
    report["files"] = files
    report["projectRoot"] = str(project_root)
    destination = output / "reports/mutation-report.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(report)
    if plan.get("corrupt"):
        text = text[: len(text) // 2]
    destination.write_text(text, encoding="utf-8", newline="\n")
if plan.get("mutateSource"):
    target = project_root / plan["files"][0]
    target.write_text(target.read_text(encoding="utf-8") + "// mutated\n", encoding="utf-8")
raise SystemExit(plan.get("exit", 0))
'''

STRYKER_JS_SHIM = r'''// Stand in for the StrykerJS runner: emit a captured report for the configured files.
const fs = require('fs');
const path = require('path');

const plan = JSON.parse(fs.readFileSync(path.join(__dirname, 'shim.json'), 'utf8'));
const settings = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const node = plan.stryker;
if (node.sleep) {
  const until = Date.now() + node.sleep * 1000;
  while (Date.now() < until) { Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 200); }
}
if (node.report !== false) {
  const report = JSON.parse(fs.readFileSync(path.join(plan.fixtures, node.fixture), 'utf8'));
  const keys = Object.keys(report.files).sort();
  const files = {};
  keys.forEach((key, index) => { files[node.files[index]] = report.files[key]; });
  report.files = files;
  report.projectRoot = process.cwd();
  let target = settings.jsonReporter.fileName;
  if (!path.isAbsolute(target)) target = path.join(process.cwd(), target);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  let text = JSON.stringify(report);
  if (node.corrupt) text = text.slice(0, Math.floor(text.length / 2));
  fs.writeFileSync(target, text);
}
process.exit(node.exit === undefined ? 0 : node.exit);
'''

COSMIC_SHIM = r'''"""Stand in for cosmic_ray.cli: emit a captured session for this campaign's nonce."""
import json, re, shutil, sqlite3, sys, tomllib
from pathlib import Path

HERE = Path(__file__).resolve().parent
PLAN = json.loads((HERE / "shim.json").read_text(encoding="utf-8"))
STAGE, CONFIGURATION, SESSION = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
plan = PLAN["cosmic"]
settings = tomllib.loads(CONFIGURATION.read_text(encoding="utf-8"))["cosmic-ray"]
if plan.get("sleep") and STAGE == "exec":
    __import__("time").sleep(plan["sleep"])

shutil.copyfile(Path(PLAN["fixtures"]) / plan["fixture"], SESSION)
connection = sqlite3.connect(str(SESSION))
connection.execute("update mutation_specs set module_path = ?", (settings["module-path"],))
if STAGE == "init":
    connection.execute("delete from work_results")
elif not plan.get("foreignNonce"):
    runner = Path(__import__("shlex").split(settings["test-command"])[1])
    nonce = re.search(r'_NONCE = "([0-9a-f]+)"', runner.read_text(encoding="utf-8")).group(1)
    for job, output in connection.execute(
            "select job_id, output from work_results").fetchall():
        if output:
            connection.execute("update work_results set output = ? where job_id = ?",
                               (re.sub(r"##DYDO-SUITE-COMPLETE [0-9a-f]+ exit=",
                                       "##DYDO-SUITE-COMPLETE %s exit=" % nonce, output), job))
connection.commit()
connection.close()
raise SystemExit(plan.get("exit", 0))
'''


def digest_path(payload, dotted):
    """Read a dotted path out of a payload, or None wherever it stops."""
    for part in dotted.split("."):
        if not isinstance(payload, dict) or part not in payload:
            return None
        payload = payload[part]
    return payload


class Observation:
    """What one facade invocation left behind."""

    def __init__(self, repository, process, payload, output):
        self.repository = repository
        self.process = process
        self.payload = payload or {}
        self.output = output

    @property
    def exit(self):
        return self.process.returncode

    def row(self, stack, capability="mutation"):
        rows = [row for row in self.payload.get("results", [])
                if row["capability"] == capability and row["stack"] == stack]
        if len(rows) != 1:
            return ("no such row", None, None)
        return (rows[0]["state"], rows[0].get("childExit"), rows[0].get("resultExit"))

    def states(self, capability="mutation"):
        return [(row["stack"], row["state"], row.get("resultExit"))
                for row in self.payload.get("results", []) if row["capability"] == capability]

    def selected(self):
        return [(row["stack"], row["capability"]) for row in self.payload.get("results", [])]

    def summary(self, stack):
        path = (self.repository.path
                / f"DynaDocs.Tests/coverage/results/adapters/{stack}-mutation.json")
        return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None

    def facts(self, stack, *dotted):
        summary = self.summary(stack)
        return {name: digest_path(summary, name) for name in dotted}

    def gap_reasons(self, stack):
        summary = self.summary(stack) or {}
        return [gap.get("reason") for gap in summary.get("gaps", [])]

    def engine_targets(self, stack):
        """The files this stack's generated engine configuration itself names.

        What the summary calls `selection.selected` is the adapter's account of the campaign;
        this is the file filter the engine was actually handed. `None` where the configuration
        carries none at all, which is what a widened Stryker.NET campaign must generate.
        """
        assurance = self.repository.path / "DynaDocs.Tests/coverage/results/assurance"
        if stack == "python":
            return [tomllib.loads(path.read_text(encoding="utf-8"))["cosmic-ray"]["module-path"]
                    for path in sorted(assurance.glob("run-*/python/sessions/*.toml"))]
        generated = sorted(assurance.glob("run-*/dotnet/stryker-config.json" if stack == "dotnet"
                                          else "run-*/node/stryker.json"))
        if len(generated) != 1:
            # A campaign that generated no configuration must not read as "no file filter".
            return f"{len(generated)} generated configurations"
        settings = json.loads(generated[0].read_text(encoding="utf-8"))
        return (settings["stryker-config"] if stack == "dotnet" else settings).get("mutate")

    def run_reports(self):
        runs = sorted((self.repository.path / "DynaDocs.Tests/coverage/results/assurance")
                      .glob("run-*/report.json"))
        return [json.loads(path.read_text(encoding="utf-8")) for path in runs]


class Repository:
    """A temporary Git repository that mirrors the coverage layout."""

    def __init__(self, path):
        self.path = path
        self.base = None
        self.shims = path / "shims"

    # -- construction -------------------------------------------------------------------

    def write(self, relative, text):
        destination = self.path / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(text, encoding="utf-8", newline="\n")
        return destination

    def git(self, *args, check=True):
        command = ["git", "-c", "user.name=dydo", "-c", "user.email=dydo@example.invalid", *args]
        return subprocess.run(command, cwd=self.path, check=check, capture_output=True, text=True)

    def commit(self, message):
        self.git("add", "-A")
        self.git("commit", "-qm", message)
        return self.git("rev-parse", "HEAD").stdout.strip()

    def manifest(self, mutation="configured"):
        def capability(stack, name, argv, kind="current-python", artifact=True):
            if name == "mutation" and mutation != "configured":
                return {"state": "unavailable", "reason": mutation}
            row = {"state": "configured", "command": {"kind": kind, "argv": argv},
                   "artifacts": []}
            if artifact:
                row["artifacts"] = [{"path": f"DynaDocs.Tests/coverage/results/adapters/"
                                             f"{stack}-{name}.json", "required": True}]
            return row

        def gate(stack, name):
            artifact = f"DynaDocs.Tests/coverage/results/adapters/{stack}-{name}.json"
            return capability(stack, name, [
                "-c", "import json;from pathlib import Path;"
                      f"p=Path({artifact!r});p.parent.mkdir(parents=True,exist_ok=True);"
                      "p.write_text(json.dumps({'schema': 1, 'exitCode': 0}))"])

        def mutation_row(stack):
            return capability(stack, "mutation",
                              [ADAPTER, "--stack", stack, "--since", "{base}"])

        tests = {"dotnet": ("current-python", ["-c", "print('dotnet tests')"]),
                 "python": ("current-python",
                            ["-m", "unittest", "discover", "-s", "tests", "-p", "test_*.py"]),
                 # Node 22 loads a directory argument as a module and dies; its own discovery
                 # from the campaign root is the form that runs `tests/*.test.cjs`.
                 "node": ("argv", ["node", "--test"])}
        stacks = []
        for name in ("dotnet", "python", "node"):
            kind, argv = tests[name]
            stacks.append({
                "name": name, "kind": "fixture", "cwd": ".",
                "isolation": {"requirement": "in-place",
                              "evidence": {"state": "verified", "kind": "direct"}},
                "capabilities": {
                    "test": capability(name, "test", argv, kind=kind, artifact=False),
                    "static": gate(name, "static"), "coverage": gate(name, "coverage"),
                    "mutation": mutation_row(name)}})
        return {"schema": 1, "artifactRoot": "DynaDocs.Tests/coverage/results", "stacks": stacks}

    # -- shims --------------------------------------------------------------------------

    def plan(self, **entries):
        payload = {"fixtures": str(FIXTURES), "toolList": ["dotnet-stryker      4.16.0"],
                   **entries}
        self.write("shims/shim.json", json.dumps(payload, indent=1))
        core = ("DynaDocs.Tests/coverage/mutation/node_modules/@stryker-mutator/core")
        self.write(f"{core}/bin/shim.json", json.dumps(payload, indent=1))
        self.write("dydo/_system/.local/mutation/python/Lib/site-packages/cosmic_ray/shim.json",
                   json.dumps(payload, indent=1))

    def install_shims(self):
        self.write("shims/dotnet_shim.py", DOTNET_SHIM)
        (self.shims / "dotnet.cmd").write_bytes(
            DOTNET_SHIM_CMD.format(python=sys.executable,
                                   script=self.shims / "dotnet_shim.py").encode("utf-8"))
        core = "DynaDocs.Tests/coverage/mutation/node_modules/@stryker-mutator/core"
        self.write(f"{core}/package.json",
                   json.dumps({"name": "@stryker-mutator/core", "version": "9.6.1",
                               "bin": {"stryker": "bin/stryker.js"}}, indent=1))
        self.write(f"{core}/bin/stryker.js", STRYKER_JS_SHIM)
        venv = self.path / "dydo/_system/.local/mutation/python"
        subprocess.run([sys.executable, "-m", "venv", "--without-pip", str(venv)], check=True,
                       capture_output=True)
        packages = venv / "Lib/site-packages"
        packages.mkdir(parents=True, exist_ok=True)
        (packages / "cosmic_ray").mkdir(exist_ok=True)
        (packages / "cosmic_ray/__init__.py").write_text("", encoding="utf-8")
        (packages / "cosmic_ray/cli.py").write_text(COSMIC_SHIM, encoding="utf-8", newline="\n")
        info = packages / "cosmic_ray-8.7.0.dist-info"
        info.mkdir(exist_ok=True)
        (info / "METADATA").write_text("Metadata-Version: 2.1\nName: cosmic-ray\nVersion: 8.7.0\n",
                                       encoding="utf-8", newline="\n")
        (info / "RECORD").write_text("", encoding="utf-8")
        (info / "INSTALLER").write_text("dydo\n", encoding="utf-8")

    def environment(self):
        environment = dict(os.environ)
        environment["PATH"] = str(self.shims) + os.pathsep + environment.get("PATH", "")
        return environment

    # -- driving ------------------------------------------------------------------------

    def invoke(self, *arguments, timeout=600):
        runner = self.path / "DynaDocs.Tests/coverage/gap_check.py"
        process = subprocess.run([sys.executable, "-u", str(runner), *arguments], cwd=self.path,
                                 capture_output=True, text=True, encoding="utf-8",
                                 errors="replace", timeout=timeout, env=self.environment())
        output = process.stdout + process.stderr
        results = [line[8:] for line in process.stdout.splitlines() if line.startswith("Result: ")]
        payload = json.loads(Path(results[0]).read_text(encoding="utf-8")) if results else None
        return Observation(self, process, payload, output)


class MutationFacadeTests(unittest.TestCase):
    """Facade-boundary probes; one per scenario of `mutation-assurance.feature`."""

    maxDiff = None

    # -- harness ------------------------------------------------------------------------

    def repository(self, sources=("dotnet", "python", "node"), mutation="configured",
                   failing_tests=(), extra=(), ignored=True):
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-facade-")
        self.addCleanup(directory.cleanup)
        repository = Repository(Path(directory.name).resolve())
        repository.git("init", "-q", "-b", "main")
        compiled = []
        if "dotnet" in sources:
            repository.write("src/Number.cs", NUMBER_CS)
            compiled.append("src/Number.cs")
        if "python" in sources:
            repository.write("src/mod.py", MOD_PY)
            repository.write("tests/test_mod.py",
                             FAILING_TEST_MOD_PY if "python" in failing_tests else TEST_MOD_PY)
        if "node" in sources:
            repository.write("src/value.cjs", VALUE_CJS)
            repository.write("tests/value.test.cjs",
                             FAILING_TEST_CJS if "node" in failing_tests else VALUE_TEST_CJS)
        for relative, text in extra:
            repository.write(relative, text)
            if relative.endswith(".cs") and not relative.startswith("metrics/"):
                compiled.append(relative)
        repository.write("DynaDocs.csproj", CSPROJ.format(items=compile_items(compiled)))
        repository.write("DynaDocs.sln", "Microsoft Visual Studio Solution File, Format 12.00\n")
        repository.write(".config/dotnet-tools.json", json.dumps(
            {"version": 1, "isRoot": True,
             "tools": {"dotnet-stryker": {"version": "4.16.0", "commands": ["dotnet-stryker"]}}},
            indent=1))
        repository.write("DynaDocs.Tests/coverage/test-associations.json", json.dumps(
            {"schema": 1, "modules": [
                {"module": "src/mod.py", "tests": ["tests/test_mod.py"]},
                {"module": "src/value.cjs", "tests": ["tests/value.test.cjs"]}]}, indent=1))
        repository.write(".gitignore",
                         "DynaDocs.Tests/coverage/*.py\ndydo/_system/.local/\nshims/\n"
                         "DynaDocs.Tests/coverage/results/\n" if ignored else "")
        for module in sorted(COVERAGE.glob("*.py")):
            shutil.copyfile(module, repository.path / "DynaDocs.Tests/coverage" / module.name)
        (repository.path / "DynaDocs.Tests/coverage/mutation").mkdir(parents=True, exist_ok=True)
        for template in ("stryker-net.json", "stryker-js.json", "cosmic-ray.toml", ".gitignore"):
            shutil.copyfile(COVERAGE / "mutation" / template,
                            repository.path / "DynaDocs.Tests/coverage/mutation" / template)
        repository.write("DynaDocs.Tests/coverage/gap_check.json",
                         json.dumps(repository.manifest(mutation), indent=1))
        repository.install_shims()
        repository.base = repository.commit("base")
        repository.plan()
        return repository

    def campaign(self, stack, fixture=None, files=None, plan=None, since=None, change=True,
                 repository=None, arguments=None, **knobs):
        """Build a candidate with one changed target of `stack` and run its campaign."""
        repository = repository or self.repository(**knobs)
        if change:
            self.change_target(repository, stack)
        if fixture is not None or plan is not None:
            repository.plan(**(plan or self.engine_plan(stack, fixture, files)))
        base = repository.base if since is None else since
        return repository.invoke(*(arguments or ["gate", "mutation", "--since", base,
                                                 "--stack", stack]))

    def engine_plan(self, stack, fixture, files=None, **extra):
        default = {"dotnet": ["src/Number.cs"], "node": ["src/value.cjs"],
                   "python": ["src/mod.py"]}[stack]
        entry = {"fixture": fixture, "files": list(files or default), **extra}
        return {"stryker" if stack != "python" else "cosmic": entry}

    def change_target(self, repository, stack):
        target = {"dotnet": "src/Number.cs", "python": "src/mod.py",
                  "node": "src/value.cjs"}[stack]
        text = (repository.path / target).read_text(encoding="utf-8")
        repository.write(target, text + ("// changed\n" if stack != "python"
                                         else "# changed\n"))
        repository.commit("change " + target)
        return target

    # -- scenario: capabilities ---------------------------------------------------------

    @unittest.skipUnless(PROJECT_MANIFEST_CONFIGURED, AWAITING_DYD_130)
    def test_capabilities_report_the_configured_mutation_rows(self):
        process = subprocess.run(
            [sys.executable, "-u", str(COVERAGE / "gap_check.py"), "capabilities"], cwd=ROOT,
            capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120)
        manifest = json.loads((COVERAGE / "gap_check.json").read_text(encoding="utf-8"))
        self.assertEqual(
            (0, {"dotnet": "configured", "python": "configured", "node": "configured"}, False),
            (process.returncode,
             {stack["name"]: stack["capabilities"]["mutation"]["state"]
              for stack in manifest["stacks"]},
             (COVERAGE / "results/adapters/dotnet-mutation.json").is_file()))

    @unittest.skipUnless(PROJECT_MANIFEST_CONFIGURED, AWAITING_DYD_130)
    def test_each_stack_mutation_row_names_the_adapter_and_base_placeholder(self):
        manifest = json.loads((COVERAGE / "gap_check.json").read_text(encoding="utf-8"))
        rows = {stack["name"]: stack["capabilities"]["mutation"] for stack in manifest["stacks"]}
        self.assertEqual(
            {stack: {"kind": "current-python",
                     "argv": [ADAPTER, "--stack", stack, "--since", "{base}"],
                     "artifacts": [{"path": f"DynaDocs.Tests/coverage/results/adapters/"
                                            f"{stack}-mutation.json", "required": True}]}
             for stack in ("dotnet", "python", "node")},
            {stack: {"kind": row["command"]["kind"], "argv": row["command"]["argv"],
                     "artifacts": row["artifacts"]} for stack, row in rows.items()})

    def test_full_g_compatibility_still_never_runs_mutation(self):
        repository = self.repository()
        seen = repository.invoke("--force-run")
        self.assertEqual(
            [(stack, capability) for stack in ("dotnet", "python", "node")
             for capability in ("test", "static", "coverage")], seen.selected())
        self.assertEqual(
            [None, None, None],
            [seen.summary(stack) for stack in ("dotnet", "python", "node")])

    # -- scenario: a campaign whose valid mutants are all killed passes -----------------

    def all_killed(self, stack, fixture):
        seen = self.campaign(stack, fixture)
        target = {"dotnet": "src/Number.cs", "python": "src/mod.py",
                  "node": "src/value.cjs"}[stack]
        self.assertEqual(("passed", 0, 0), seen.row(stack))
        self.assertEqual(
            {"schema": 1, "stack": stack, "gate": "mutation", "measurementComplete": True,
             "exitCode": 0, "findings": [], "gaps": [],
             "mutation.selection.mode": "changed",
             "mutation.selection.changedTargets": [target],
             "mutation.counts.killed": 1, "mutation.counts.valid": 1, "mutation.score": 100.0},
            seen.facts(stack, "schema", "stack", "gate", "measurementComplete", "exitCode",
                       "findings", "gaps", "mutation.selection.mode",
                       "mutation.selection.changedTargets", "mutation.counts.killed",
                       "mutation.counts.valid", "mutation.score"))
        recorded = seen.facts(stack, "candidate.commit", "candidate.dirty",
                              "candidate.sourceFingerprint", "inventory.path", "inventory.sha256",
                              "tools", "commands", "mutation.rawReports")
        # Every field is recorded; `candidate.dirty` is a boolean, so a truthful `False` is
        # a reading, not an absence.
        self.assertEqual([], [name for name, value in recorded.items()
                              if not value and not isinstance(value, bool)])
        self.assertEqual([target], seen.engine_targets(stack))
        return seen

    def test_an_all_killed_dotnet_campaign_passes(self):
        self.all_killed("dotnet", "stryker-net-killed.json")

    def test_an_all_killed_python_campaign_passes(self):
        self.all_killed("python", "cosmic-ray-killed.sqlite")

    def test_an_all_killed_node_campaign_passes(self):
        self.all_killed("node", "stryker-js-killed.json")

    def test_mutate_filter_removals_are_witnessed_not_counted(self):
        repository = self.repository(extra=[("src/Other.cs", OTHER_CS)])
        seen = self.campaign("dotnet", "stryker-net-mutate-filter-ignored.json",
                             files=["src/Number.cs", "src/Other.cs"], repository=repository)
        self.assertEqual(("passed", 0, 0), seen.row("dotnet"))
        self.assertEqual(
            {"mutation.counts.generated": 1, "mutation.counts.killed": 1, "findings": [],
             "gaps": [], "mutation.selection.witness": [
                 {"path": "src/Other.cs", "reason": "removed by mutate filter"}]},
            seen.facts("dotnet", "mutation.counts.generated", "mutation.counts.killed",
                       "findings", "gaps", "mutation.selection.witness"))

    # -- scenario outline: a mutant that is not killed is a measured finding ------------

    def one_finding(self, stack, fixture, status, plan=None):
        target = {"dotnet": "src/Number.cs", "python": "src/mod.py",
                  "node": "src/value.cjs"}[stack]
        seen = self.campaign(stack, fixture, plan=plan)
        self.assertEqual((("failed", 1, 1), 1), (seen.row(stack), seen.exit))
        findings = (seen.summary(stack) or {}).get("findings")
        self.assertEqual(1, len(findings or []), seen.output)
        self.assertEqual(
            {"gate": "mutation", "path": target, "status": status,
             "span": True, "mutator": True, "raw": True, "measurementComplete": True},
            {"gate": findings[0].get("gate"), "path": findings[0].get("path"),
             "status": findings[0].get("status"), "span": bool(findings[0].get("span")),
             "mutator": bool(findings[0].get("mutator")), "raw": bool(findings[0].get("raw")),
             "measurementComplete": (seen.summary(stack) or {}).get("measurementComplete")})
        return seen

    def test_a_survived_dotnet_mutant_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-survived.json", "survived")

    def test_an_uncovered_dotnet_mutant_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-no-coverage.json", "noCoverage")

    def test_a_timed_out_dotnet_mutant_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-timeout.json", "timeout")

    def test_a_dotnet_runtime_error_mutant_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-runtime-error.json", "runtimeError")

    def test_an_ignored_dotnet_mutant_in_a_selected_file_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-ignored.json", "ignored")

    def test_a_pending_dotnet_mutant_is_a_finding(self):
        self.one_finding("dotnet", "stryker-net-pending.json", "unrun")

    def test_a_survived_node_mutant_is_a_finding(self):
        self.one_finding("node", "stryker-js-survived.json", "survived")

    def test_a_timed_out_node_mutant_is_a_finding(self):
        self.one_finding("node", "stryker-js-timeout.json", "timeout")

    def test_a_node_runtime_error_mutant_is_a_finding(self):
        self.one_finding("node", "stryker-js-runtime-error.json", "runtimeError")

    def test_a_survived_python_mutant_is_a_finding(self):
        self.one_finding("python", "cosmic-ray-survived.sqlite", "survived")

    def test_a_python_mutant_killed_after_the_engine_timeout_is_a_finding(self):
        self.one_finding("python", "cosmic-ray-timeout.sqlite", "timeout")

    def test_a_python_kill_with_no_completion_marker_is_a_finding(self):
        self.one_finding("python", "cosmic-ray-killed-without-marker.sqlite", "runtimeError")

    def test_a_python_kill_whose_marker_reads_exit_zero_is_a_finding(self):
        self.one_finding("python", "cosmic-ray-killed-marker-exit-zero.sqlite", "runtimeError")

    def test_a_python_kill_whose_marker_is_another_campaigns_is_a_finding(self):
        self.one_finding("python", "cosmic-ray-killed.sqlite", "runtimeError",
                         plan={"cosmic": {"fixture": "cosmic-ray-killed.sqlite",
                                          "files": ["src/mod.py"], "foreignNonce": True}})

    def test_a_skipped_python_mutant_is_an_unrun_finding(self):
        self.one_finding("python", "cosmic-ray-skipped.sqlite", "unrun")

    def test_a_python_mutant_with_no_test_is_an_unrun_finding(self):
        self.one_finding("python", "cosmic-ray-no-test.sqlite", "unrun")

    # -- scenario outline: missing or invalid measurement is invalid, never a pass ------

    def invalid(self, stack, reason, **campaign):
        seen = self.campaign(stack, **campaign)
        self.assertEqual((("invalid", 2, 2), 2), (seen.row(stack), seen.exit))
        named = [gap for gap in seen.gap_reasons(stack) if reason in (gap or "")]
        self.assertEqual(
            {"named": [reason], "measurementComplete": False},
            {"named": [reason] if named else seen.gap_reasons(stack),
             "measurementComplete": (seen.summary(stack) or {}).get("measurementComplete")})
        return seen

    def test_an_unrestored_dotnet_engine_is_invalid(self):
        self.invalid("dotnet", "dotnet tool restore",
                     plan={"toolList": [], "stryker": {"fixture": "stryker-net-killed.json",
                                                       "files": ["src/Number.cs"]}})

    def test_an_unrestored_node_engine_is_invalid(self):
        repository = self.repository()
        shutil.rmtree(repository.path
                      / "DynaDocs.Tests/coverage/mutation/node_modules/@stryker-mutator")
        self.invalid("node", "npm --prefix DynaDocs.Tests/coverage/mutation ci --ignore-scripts",
                     repository=repository)

    def test_an_unrestored_python_engine_is_invalid(self):
        repository = self.repository()
        shutil.rmtree(repository.path / "dydo/_system/.local/mutation/python")
        self.invalid("python",
                     "pip install --no-deps -r DynaDocs.Tests/coverage/mutation/requirements.lock",
                     repository=repository)

    def test_a_failing_python_baseline_is_invalid(self):
        self.invalid("python", "baseline test run failed",
                     fixture="cosmic-ray-killed.sqlite", failing_tests=("python",))

    def test_a_failing_node_baseline_is_invalid(self):
        self.invalid("node", "baseline test run failed",
                     fixture="stryker-js-killed.json", failing_tests=("node",))

    def test_a_nonzero_dotnet_exit_without_a_report_is_invalid(self):
        self.invalid("dotnet", "no mutation report produced",
                     plan={"toolList": ["dotnet-stryker      4.16.0"],
                           "stryker": {"fixture": "stryker-net-killed.json",
                                       "files": ["src/Number.cs"], "report": False, "exit": 1}})

    def test_a_malformed_dotnet_report_is_invalid(self):
        self.invalid("dotnet", "malformed report",
                     plan={"toolList": ["dotnet-stryker      4.16.0"],
                           "stryker": {"fixture": "stryker-net-killed.json",
                                       "files": ["src/Number.cs"], "corrupt": True}})

    def test_a_node_report_that_omits_the_selected_file_is_invalid(self):
        repository = self.repository(extra=[("src/beta.cjs", "exports.beta = () => 1;\n")])
        repository.write("src/beta.cjs", "exports.beta = () => 2;\n")
        repository.write("src/value.cjs", VALUE_CJS + "// changed\n")
        repository.commit("change both node targets")
        seen = self.invalid("node", "partial report", repository=repository, change=False,
                            fixture="stryker-js-killed.json", files=["src/value.cjs"])
        self.assertEqual(["src/beta.cjs", "src/value.cjs"], seen.engine_targets("node"))

    def test_a_non_ignored_mutant_in_an_unselected_dotnet_file_is_invalid(self):
        repository = self.repository(extra=[("src/Other.cs", OTHER_CS)])
        self.invalid("dotnet", "foreign mutant", repository=repository,
                     fixture="stryker-net-foreign-mutant.json",
                     files=["src/Number.cs", "src/Other.cs"])

    def test_an_unselected_file_in_a_node_report_is_invalid(self):
        repository = self.repository(extra=[("src/beta.cjs", "exports.beta = () => 1;\n")])
        self.invalid("node", "foreign file", repository=repository,
                     fixture="stryker-js-two-files.json",
                     files=["src/value.cjs", "src/beta.cjs"])

    def test_a_python_session_whose_work_item_has_no_result_is_invalid(self):
        self.invalid("python", "partial report", fixture="cosmic-ray-partial-session.sqlite")

    def test_a_python_worker_outcome_of_exception_is_invalid(self):
        self.invalid("python", "engine could not run mutant",
                     fixture="cosmic-ray-exception.sqlite")

    def test_a_python_test_outcome_of_incompetent_is_invalid(self):
        self.invalid("python", "engine could not run mutant",
                     fixture="cosmic-ray-incompetent.sqlite")

    def test_a_source_the_engine_changed_and_did_not_restore_is_invalid(self):
        self.invalid("dotnet", "candidate changed during the campaign",
                     plan={"toolList": ["dotnet-stryker      4.16.0"],
                           "stryker": {"fixture": "stryker-net-killed.json",
                                       "files": ["src/Number.cs"], "mutateSource": True}})

    def test_a_node_campaign_with_zero_generated_mutants_is_invalid(self):
        self.invalid("node", "zero-mutant campaign", fixture="stryker-js-zero-mutants.json")

    def test_a_dotnet_campaign_of_only_compile_errors_is_invalid(self):
        self.invalid("dotnet", "all mutants invalid", fixture="stryker-net-compile-error.json")

    def test_a_base_no_commit_resolves_is_invalid(self):
        self.invalid("dotnet", "unresolvable base", fixture="stryker-net-killed.json",
                     since="0" * 40)

    def test_a_base_that_is_not_an_ancestor_is_invalid(self):
        repository = self.repository()
        repository.git("checkout", "-q", "--detach")
        repository.write("src/divergent.md", "divergent\n")
        divergent = repository.commit("divergent")
        repository.git("checkout", "-q", "main")
        self.invalid("node", "base is not an ancestor", repository=repository,
                     fixture="stryker-js-killed.json", since=divergent)

    def test_an_inventory_with_nonempty_errors_is_invalid(self):
        repository = self.repository(extra=[("src/Orphan.cs", GATE_CS)])
        (repository.path / "DynaDocs.csproj").write_text(
            CSPROJ.format(items=compile_items(["src/Number.cs"])), encoding="utf-8")
        repository.commit("orphan source outside every project")
        self.invalid("dotnet", "inventory errors", repository=repository,
                     fixture="stryker-net-killed.json")

    def test_a_changed_c_sharp_target_outside_the_project_is_invalid(self):
        repository = self.repository(extra=[
            ("metrics/Gate.cs", GATE_CS),
            ("metrics/GateMetrics.csproj", CSPROJ.format(
                items=compile_items(["Gate.cs"])))])
        repository.write("metrics/Gate.cs", GATE_CS + "// changed\n")
        repository.commit("change the foreign project's source")
        self.invalid("dotnet", "no .NET test project route", repository=repository, change=False,
                     fixture="stryker-net-killed.json")

    def test_a_changed_extensionless_javascript_target_is_invalid(self):
        repository = self.repository(extra=[("npm/dydo/bin", "#!/usr/bin/env node\n")])
        repository.write("npm/dydo/bin", "#!/usr/bin/env node\nconsole.log(1);\n")
        repository.commit("change the extensionless entry point")
        self.invalid("node", "extensionless target", repository=repository, change=False,
                     fixture="stryker-js-killed.json")

    def test_a_template_whose_concurrency_departs_is_invalid(self):
        repository = self.repository()
        template = json.loads((repository.path
                               / "DynaDocs.Tests/coverage/mutation/stryker-net.json")
                              .read_text(encoding="utf-8"))
        template["stryker-config"]["concurrency"] = 2
        repository.write("DynaDocs.Tests/coverage/mutation/stryker-net.json",
                         json.dumps(template, indent=1))
        repository.commit("depart from the pinned concurrency")
        self.invalid("dotnet", "invalid mutation configuration", repository=repository,
                     change=False, fixture="stryker-net-killed.json")

    # -- scenario: a busy mutation slot -------------------------------------------------

    def test_a_busy_mutation_slot_is_refused_without_touching_its_owner(self):
        repository = self.repository()
        adapters = repository.path / "DynaDocs.Tests/coverage/results/adapters"
        adapters.mkdir(parents=True, exist_ok=True)
        summary = adapters / "python-mutation.json"
        lock = adapters / "python-mutation.json.lock"
        summary.write_text("foreign summary", encoding="utf-8")
        lock.write_text("foreign lock", encoding="utf-8")
        seen = self.campaign("python", "cosmic-ray-killed.sqlite", repository=repository)
        reports = seen.run_reports()
        self.assertEqual(
            {"row": ("invalid", 2, 2), "summary": "foreign summary", "lock": "foreign lock",
             "named": True},
            {"row": seen.row("python"), "summary": summary.read_text(encoding="utf-8"),
             "lock": lock.read_text(encoding="utf-8"),
             "named": any("mutation slot busy" in (gap.get("reason") or "")
                          and str(lock) in (gap.get("reason") or "")
                          for report in reports for gap in report.get("gaps", []))})

    # -- scenario: a change touching no maintained source -------------------------------

    def test_a_change_touching_no_maintained_source_passes_as_a_witnessed_no_op(self):
        repository = self.repository()
        repository.write("NOTES.md", "# notes\n")
        repository.commit("documentation only")
        repository.plan(**{"stryker": {"fixture": "stryker-net-killed.json", "files": []},
                           "cosmic": {"fixture": "cosmic-ray-killed.sqlite", "files": []}})
        seen = repository.invoke("gate", "mutation", "--since", repository.base)
        self.assertEqual([("dotnet", "passed", 0), ("python", "passed", 0), ("node", "passed", 0)],
                         seen.states())
        self.assertEqual(
            {stack: {"mode": "none", "changedTargets": [], "generated": 0,
                     "measurementComplete": True,
                     "witness": [{"path": "NOTES.md", "classification": "no obligation"}]}
             for stack in ("dotnet", "python", "node")},
            {stack: {"mode": digest_path(seen.summary(stack), "mutation.selection.mode"),
                     "changedTargets": digest_path(seen.summary(stack),
                                                   "mutation.selection.changedTargets"),
                     "generated": digest_path(seen.summary(stack), "mutation.counts.generated"),
                     "measurementComplete": digest_path(seen.summary(stack),
                                                        "measurementComplete"),
                     "witness": digest_path(seen.summary(stack), "mutation.selection.witness")}
             for stack in ("dotnet", "python", "node")})

    # -- scenario outline: uncertain selection widens -----------------------------------

    def widened(self, stack, reason, repository, selected, fixture):
        seen = self.campaign(stack, fixture, files=selected, repository=repository, change=False)
        self.assertEqual(
            {"mode": "widened", "reason": reason, "selected": sorted(selected)},
            {"mode": digest_path(seen.summary(stack), "mutation.selection.mode"),
             "reason": digest_path(seen.summary(stack), "mutation.selection.reason"),
             "selected": digest_path(seen.summary(stack), "mutation.selection.selected")})
        # Stryker.NET mutates the whole project when the campaign widens, so the one engine
        # whose `mutate` is a filter must be handed none at all.
        self.assertEqual(None if stack == "dotnet" else sorted(selected),
                         seen.engine_targets(stack))
        return seen

    def test_a_renamed_dotnet_target_widens_the_stack(self):
        repository = self.repository(extra=[("src/Other.cs", OTHER_CS)])
        repository.git("mv", "src/Other.cs", "src/Renamed.cs")
        repository.write("DynaDocs.csproj", CSPROJ.format(
            items=compile_items(["src/Number.cs", "src/Renamed.cs"])))
        repository.commit("rename a maintained C# source")
        self.widened("dotnet", "deleted or renamed source", repository,
                     ["src/Number.cs", "src/Renamed.cs"], "stryker-net-mutate-filter-ignored.json")

    def test_a_deleted_python_target_widens_the_stack(self):
        repository = self.repository(extra=[("src/other.py", "def other():\n    return 2\n")])
        repository.git("rm", "-q", "src/other.py")
        repository.commit("delete a maintained python source")
        self.widened("python", "deleted or renamed source", repository, ["src/mod.py"],
                     "cosmic-ray-killed.sqlite")

    def test_a_node_test_no_target_lists_widens_the_stack(self):
        repository = self.repository(extra=[("tests/orphan.test.cjs",
                                             "const { test } = require('node:test');\n"
                                             "test('orphan', () => {});\n")])
        repository.write("tests/orphan.test.cjs",
                         "const { test } = require('node:test');\ntest('orphan', () => { });\n")
        repository.commit("change a node test no target lists")
        self.widened("node", "test change with no associated target", repository,
                     ["src/value.cjs"], "stryker-js-killed.json")

    def test_a_changed_tool_manifest_widens_the_dotnet_stack(self):
        repository = self.repository()
        manifest = json.loads((repository.path / ".config/dotnet-tools.json")
                              .read_text(encoding="utf-8"))
        manifest["tools"]["dotnet-stryker"]["version"] = "4.16.0"
        manifest["isRoot"] = True
        repository.write(".config/dotnet-tools.json", json.dumps(manifest, indent=2))
        repository.commit("reformat the tool manifest")
        self.widened("dotnet", "configuration changed", repository, ["src/Number.cs"],
                     "stryker-net-killed.json")

    # -- scenario: a test-only change ---------------------------------------------------

    def test_a_test_only_change_reruns_exactly_the_targets_that_list_it(self):
        repository = self.repository(extra=[("src/beta.py", "def beta():\n    return 2\n")])
        repository.write("DynaDocs.Tests/coverage/test-associations.json", json.dumps(
            {"schema": 1, "modules": [
                {"module": "src/beta.py", "tests": ["tests/test_mod.py"]},
                {"module": "src/mod.py", "tests": ["tests/test_mod.py"]},
                {"module": "src/value.cjs", "tests": ["tests/value.test.cjs"]}]}, indent=1))
        # The association belongs to the base, so the test file is the scenario's only change.
        repository.base = repository.commit("associate both python targets with the one test")
        repository.write("tests/test_mod.py", TEST_MOD_PY + "\n# changed\n")
        repository.commit("change only the test file")
        seen = self.campaign("python", "cosmic-ray-killed.sqlite",
                             files=["src/beta.py", "src/mod.py"], repository=repository,
                             change=False)
        self.assertEqual(
            {"mode": "changed", "changedTargets": ["src/beta.py", "src/mod.py"],
             "selected": ["src/beta.py", "src/mod.py"],
             "engine": ["src/beta.py", "src/mod.py"]},
            {"mode": digest_path(seen.summary("python"), "mutation.selection.mode"),
             "changedTargets": digest_path(seen.summary("python"),
                                           "mutation.selection.changedTargets"),
             "selected": digest_path(seen.summary("python"), "mutation.selection.selected"),
             "engine": seen.engine_targets("python")})

    # -- scenario: dirty and untracked content ------------------------------------------

    def test_dirty_and_untracked_content_is_measured_without_touching_the_callers_tree(self):
        repository = self.repository()
        repository.write("src/mod.py", MOD_PY + "# dirty\n")
        repository.write("src/fresh.py", "def fresh():\n    return 3\n")
        repository.write("DynaDocs.Tests/coverage/test-associations.json", json.dumps(
            {"schema": 1, "modules": [
                {"module": "src/fresh.py", "tests": ["tests/test_mod.py"]},
                {"module": "src/mod.py", "tests": ["tests/test_mod.py"]},
                {"module": "src/value.cjs", "tests": ["tests/value.test.cjs"]}]}, indent=1))
        repository.plan(**{"cosmic": {"fixture": "cosmic-ray-killed.sqlite",
                                      "files": ["src/fresh.py", "src/mod.py"]}})
        # Taken after the harness's own last write, so what it compares is the adapter's effect.
        before = {path.relative_to(repository.path).as_posix(): path.read_bytes()
                  for path in sorted(repository.path.rglob("*"))
                  if path.is_file() and ".git" not in path.parts
                  and "results" not in path.parts}
        seen = repository.invoke("gate", "mutation", "--since", repository.base, "--stack",
                                 "python")
        after = {path.relative_to(repository.path).as_posix(): path.read_bytes()
                 for path in sorted(repository.path.rglob("*"))
                 if path.is_file() and ".git" not in path.parts and "results" not in path.parts}
        worktrees = repository.git("worktree", "list", "--porcelain").stdout
        self.assertEqual(
            {"selected": ["src/fresh.py", "src/mod.py"],
             "engine": ["src/fresh.py", "src/mod.py"], "tree": True, "worktrees": 1},
            {"selected": digest_path(seen.summary("python"), "mutation.selection.selected"),
             "engine": seen.engine_targets("python"),
             "tree": before == after,
             "worktrees": worktrees.count("worktree ")})

    # -- scenario outline: interruption --------------------------------------------------

    def interruption(self, stack, fixture):
        repository = self.repository()
        self.change_target(repository, stack)
        repository.plan(**self.engine_plan(stack, fixture, sleep=90))
        runner = repository.path / "DynaDocs.Tests/coverage/gap_check.py"
        facade = subprocess.Popen(
            [sys.executable, "-u", str(runner), "gate", "mutation", "--since", repository.base,
             "--stack", stack], cwd=repository.path, stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace",
            env=repository.environment(),
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0)
        collected, lines = [], queue.Queue()

        def collect():
            for line in facade.stdout:
                collected.append(line)
                lines.put(line)

        reader = threading.Thread(target=collect, daemon=True)
        reader.start()
        try:
            deadline = time.monotonic() + 120
            while (time.monotonic() < deadline and facade.poll() is None and not list(
                    (repository.path / "DynaDocs.Tests/coverage/results").glob(
                        "assurance/run-*/*/job-*"))):
                time.sleep(0.5)
            facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == "nt"
                               else signal.SIGINT)
            facade.wait(timeout=120)
        finally:
            if facade.poll() is None:
                facade.kill()
                facade.wait(timeout=30)
            reader.join(timeout=5)
            facade.stdout.close()
        results = [line[8:].strip() for line in collected if line.startswith("Result: ")]
        payload = json.loads(Path(results[0]).read_text(encoding="utf-8")) if results else {}
        observation = Observation(repository, facade, payload, "".join(collected))
        summary = observation.summary(stack) or {}
        reports = observation.run_reports()
        self.assertEqual(
            {"exit": 130, "row": ("interrupted", 130, 130), "exitCode": 130,
             "gaps": [{"reason": "interrupted"}], "findings": [], "complete": False,
             "reportExitCode": [130], "worktrees": 1},
            {"exit": facade.returncode, "row": observation.row(stack),
             "exitCode": summary.get("exitCode"), "gaps": summary.get("gaps"),
             "findings": summary.get("findings"),
             "complete": summary.get("measurementComplete"),
             "reportExitCode": [report.get("exitCode") for report in reports],
             "worktrees": repository.git("worktree", "list",
                                         "--porcelain").stdout.count("worktree ")})

    def test_interrupting_a_dotnet_campaign_completes_its_cleanup_first(self):
        self.interruption("dotnet", "stryker-net-killed.json")

    def test_interrupting_a_python_campaign_completes_its_cleanup_first(self):
        self.interruption("python", "cosmic-ray-killed.sqlite")

    def test_interrupting_a_node_campaign_completes_its_cleanup_first(self):
        self.interruption("node", "stryker-js-killed.json")

    # -- scenario outline: campaign settings are pinned in the templates ----------------

    def pinned(self, template, setting, expected, departure, stack, fixture):
        """The shipped template carries the pinned value, and any other value is invalid."""
        dense = "".join((COVERAGE / "mutation" / template)
                        .read_text(encoding="utf-8").split())
        repository = self.repository()
        copy = repository.path / "DynaDocs.Tests/coverage/mutation" / template
        copy.write_text(departure, encoding="utf-8", newline="\n")
        repository.commit("depart from the pinned " + setting)
        seen = self.campaign(stack, fixture, repository=repository, change=False)
        self.assertEqual(
            {"shipped": True, "row": ("invalid", 2, 2), "named": True},
            {"shipped": expected in dense,
             "row": seen.row(stack),
             "named": any("invalid mutation configuration" in (reason or "")
                          for reason in seen.gap_reasons(stack))})

    def net_template(self, **overrides):
        template = json.loads((COVERAGE / "mutation/stryker-net.json").read_text(encoding="utf-8"))
        template["stryker-config"].update(overrides)
        return json.dumps(template, indent=2) + "\n"

    def js_template(self, **overrides):
        template = json.loads((COVERAGE / "mutation/stryker-js.json").read_text(encoding="utf-8"))
        template.update(overrides)
        return json.dumps(template, indent=2) + "\n"

    def test_the_dotnet_template_pins_concurrency(self):
        self.pinned("stryker-net.json", "concurrency", '"concurrency":1',
                    self.net_template(concurrency=2), "dotnet", "stryker-net-killed.json")

    def test_the_dotnet_template_pins_its_thresholds(self):
        self.pinned("stryker-net.json", "thresholds", '"high":100,"low":100,"break":100',
                    self.net_template(thresholds={"high": 100, "low": 100, "break": 80}),
                    "dotnet", "stryker-net-killed.json")

    def test_the_dotnet_template_pins_its_reporters(self):
        self.pinned("stryker-net.json", "reporters", '"reporters":["json","html"]',
                    self.net_template(reporters=["json"]), "dotnet", "stryker-net-killed.json")

    def test_the_node_template_pins_coverage_analysis_off(self):
        self.pinned("stryker-js.json", "coverageAnalysis", '"coverageAnalysis":"off"',
                    self.js_template(coverageAnalysis="perTest"), "node",
                    "stryker-js-killed.json")

    def test_the_node_template_pins_concurrency(self):
        self.pinned("stryker-js.json", "concurrency", '"concurrency":1',
                    self.js_template(concurrency=2), "node", "stryker-js-killed.json")

    def test_the_node_template_pins_in_place(self):
        self.pinned("stryker-js.json", "inPlace", '"inPlace":true',
                    self.js_template(inPlace=False), "node", "stryker-js-killed.json")

    def test_the_python_template_pins_the_local_distributor(self):
        self.pinned("cosmic-ray.toml", "distributor", 'name="local"',
                    '[cosmic-ray]\nmodule-path = ""\ntimeout = 0.0\nexcluded-modules = []\n'
                    'test-command = ""\n\n[cosmic-ray.distributor]\nname = "http"\n',
                    "python", "cosmic-ray-killed.sqlite")


if __name__ == "__main__":
    unittest.main()

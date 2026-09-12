"""Gate 10: replay the strong/weak subjects through the completed adapter with real engines.

This file lives outside the ordinary unit discovery (`DynaDocs.Tests/coverage/tests/`) on purpose:
the probes below launch the pinned Stryker.NET 4.16.0, StrykerJS 9.6.1 and Cosmic Ray 8.7.0
engines for real, which is far too slow to sit in the unit gate. It drives the adapter's own CLI
seam -- `mutation_adapter.py --stack <name> --since <base> --root <subject> --output <summary>` --
over a throwaway Git repository built from the data strings below, then reads the published
schema-1 summary.

Per engine the exact/strong-assertion subject must be killed exactly once with score 100 and an
adapter exit 0, the weak-assertion subject must survive exactly once with an adapter exit 1, and
the vendor's own concurrency witness must appear in the raw stdout the adapter retained. The
subjects are data strings inside this file; nothing here re-enters `gap_check.py`, so the unit gate
can never recurse into a real campaign.

Run with the pinned interpreter:

    <P> -m unittest discover -s DynaDocs.Tests/coverage/mutation/probes -p "test_replay.py"
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
COVERAGE = ROOT / "DynaDocs.Tests/coverage"
ADAPTER = COVERAGE / "mutation_adapter.py"
VENV = ROOT / "dydo/_system/.local/mutation/python"
NODE_MODULES = COVERAGE / "mutation/node_modules"
PYTHON = sys.executable

# -- the subjects, as data strings ----------------------------------------------------------

NUMBER_CS = """namespace Subject;

public static class Number
{
    public static int Value(int input) => input + 1;
}
"""
DOTNET_STRONG = """using Xunit;

public class NumberTests
{
    [Fact]
    public void ValueIsExact()
    {
        Assert.Equal(2, Subject.Number.Value(1));
    }
}
"""
DOTNET_WEAK = """using Xunit;

public class NumberTests
{
    [Fact]
    public void ValueIsAnInt()
    {
        Assert.IsType<int>(Subject.Number.Value(1));
    }
}
"""

MOD_PY = "def flag():\n    return True\n"
PYTHON_STRONG = ("import unittest\n\nimport mod\n\n\nclass FlagTests(unittest.TestCase):\n"
                 "    def test_flag_is_true(self):\n        self.assertTrue(mod.flag())\n")
PYTHON_WEAK = ("import unittest\n\nimport mod\n\n\nclass FlagTests(unittest.TestCase):\n"
               "    def test_flag_is_a_bool(self):\n        self.assertIsInstance(mod.flag(), bool)\n")

VALUE_CJS = "exports.value = () => 1234;\n"
NODE_STRONG = ("const { test } = require('node:test');\n"
               "const assert = require('node:assert/strict');\n"
               "const { value } = require('./value.cjs');\n\n"
               "test('value is exactly 1234', () => assert.equal(value(), 1234));\n")
NODE_WEAK = ("const { test } = require('node:test');\n"
             "const assert = require('node:assert/strict');\n"
             "const { value } = require('./value.cjs');\n\n"
             "test('value is a function', () => assert.equal(typeof value, 'function'));\n")

# The vendor log line that proves the effective test-runner concurrency the adapter requested.
WITNESS = {"dotnet": "Stryker will use a max of 1 parallel testsessions.",
           "node": "ConcurrencyTokenProvider Creating 1 test runner process(es)."}

# One changed target and one strong/weak test per stack.
TARGET = {"dotnet": "src/Number.cs", "python": "mod.py", "node": "value.cjs"}
TEST_FILE = {"dotnet": "DynaDocs.Tests/NumberTests.cs", "python": "test_mod.py",
             "node": "value.test.cjs"}
TEST_TEXT = {"dotnet": (DOTNET_STRONG, DOTNET_WEAK), "python": (PYTHON_STRONG, PYTHON_WEAK),
             "node": (NODE_STRONG, NODE_WEAK)}
TEST_COMMAND = {
    "dotnet": ("argv", ["dotnet", "test", "DynaDocs.Tests/DynaDocs.Tests.csproj"]),
    "python": ("current-python", ["-m", "unittest", "discover", "-s", ".", "-p", "test_*.py"]),
    "node": ("argv", ["node", "--test"]),
}
MANIFEST = {"schema": 1, "artifactRoot": "DynaDocs.Tests/coverage/results",
            "stacks": [{"name": name,
                        "capabilities": {"test": {"state": "configured",
                                                  "command": {"kind": TEST_COMMAND[name][0],
                                                              "argv": TEST_COMMAND[name][1]}}}}
                       for name in ("dotnet", "python", "node")]}

DYNADOCS_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="src/Number.cs" />
  </ItemGroup>
</Project>
"""

TESTS_CSPROJ = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../DynaDocs.csproj" />
  </ItemGroup>
</Project>
"""

GITIGNORE = """DynaDocs.Tests/coverage/*.py
DynaDocs.Tests/coverage/mutation/node_modules/
DynaDocs.Tests/coverage/results/
dydo/_system/.local/
**/bin/
**/obj/
"""


def _write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _junction(link, target):
    """Create a directory junction `link` -> `target` so no engine is copied or retyped."""
    link.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(target)],
                            capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"junction {link} -> {target}: {result.stdout}{result.stderr}")


class Subject:
    """A throwaway Git repository mirroring the coverage layout with its engines linked in."""

    def __init__(self, stack, strong):
        self.stack = stack
        self.strong = strong
        self.path = Path(tempfile.mkdtemp(prefix=f"dydo-replay-{stack}-")).resolve()
        self.junctions = []
        self._build()

    # -- construction -------------------------------------------------------------------

    def git(self, *arguments, check=True):
        command = ["git", "-c", "core.autocrlf=false", "-c", "user.name=dydo",
                   "-c", "user.email=dydo@example.invalid", *arguments]
        return subprocess.run(command, cwd=self.path, check=check, capture_output=True, text=True)

    def write(self, relative, text):
        _write(self.path / relative, text)

    def commit(self, message):
        self.git("add", "-A")
        self.git("commit", "-qm", message)
        return self.git("rev-parse", "HEAD").stdout.strip()

    def _build(self):
        self.git("init", "-q", "-b", "main")
        (self.path / "DynaDocs.Tests/coverage").mkdir(parents=True, exist_ok=True)
        for module in sorted(COVERAGE.glob("*.py")):
            shutil.copyfile(module, self.path / "DynaDocs.Tests/coverage" / module.name)
        (self.path / "DynaDocs.Tests/coverage/mutation").mkdir(parents=True, exist_ok=True)
        for template in ("stryker-net.json", "stryker-js.json", "cosmic-ray.toml"):
            shutil.copyfile(COVERAGE / "mutation" / template,
                            self.path / "DynaDocs.Tests/coverage/mutation" / template)
        self.write("DynaDocs.Tests/coverage/gap_check.json",
                   json.dumps(MANIFEST, indent=1) + "\n")
        self.write("DynaDocs.Tests/coverage/test-associations.json", json.dumps(
            {"schema": 1, "modules": [
                {"module": "mod.py", "tests": ["test_mod.py"]},
                {"module": "value.cjs", "tests": ["value.test.cjs"]}]}, indent=1) + "\n")
        self.write("DynaDocs.Tests/coverage/results/.gitkeep", "")
        self.write("DynaDocs.Tests/coverage/mutation/.gitignore", "node_modules/\n")
        self.write(".gitignore", GITIGNORE)
        self.write(".config/dotnet-tools.json", json.dumps(
            {"version": 1, "isRoot": True,
             "tools": {"dotnet-stryker": {"version": "4.16.0", "commands": ["dotnet-stryker"]}}},
            indent=1) + "\n")
        self.write("DynaDocs.csproj", DYNADOCS_CSPROJ)
        self.write("DynaDocs.Tests/DynaDocs.Tests.csproj", TESTS_CSPROJ)
        self.write(TARGET["dotnet"], NUMBER_CS)
        self.write(TEST_FILE["dotnet"], TEST_TEXT["dotnet"][0 if self.strong else 1])
        self.write(TARGET["python"], MOD_PY)
        self.write(TEST_FILE["python"], TEST_TEXT["python"][0 if self.strong else 1])
        self.write(TARGET["node"], VALUE_CJS)
        self.write(TEST_FILE["node"], TEST_TEXT["node"][0 if self.strong else 1])
        self._link_engines()
        self.git("add", "-A")
        self.git("commit", "-qm", "base")
        self.base = self.git("rev-parse", "HEAD").stdout.strip()
        subprocess.run(["dotnet", "tool", "restore"], cwd=self.path,
                       capture_output=True, text=True)
        self.change_target()

    def _link_engines(self):
        node_modules = self.path / "DynaDocs.Tests/coverage/mutation/node_modules"
        if NODE_MODULES.is_dir() and not node_modules.exists():
            _junction(node_modules, NODE_MODULES)
            self.junctions.append(node_modules)
        venv = self.path / "dydo/_system/.local/mutation/python"
        if VENV.is_dir() and not venv.exists():
            _junction(venv, VENV)
            self.junctions.append(venv)

    # -- the one target change and the strong variant -----------------------------------

    def change_target(self):
        target = self.path / TARGET[self.stack]
        suffix = "// changed\n" if self.stack != "python" else "# changed\n"
        target.write_text(target.read_text(encoding="utf-8") + suffix,
                          encoding="utf-8", newline="\n")
        self.git("add", "-A")
        self.git("commit", "-qm", f"change {TARGET[self.stack]}")

    # -- driving and cleanup ------------------------------------------------------------

    def campaign(self, timeout=3600):
        summary = (self.path / "DynaDocs.Tests/coverage/results/adapters"
                   / f"{self.stack}-mutation.json")
        process = subprocess.run(
            [PYTHON, "-u", str(self.path / "DynaDocs.Tests/coverage/mutation_adapter.py"),
             "--stack", self.stack, "--since", self.base, "--root", str(self.path),
             "--output", str(summary)],
            cwd=self.path, capture_output=True, text=True, encoding="utf-8", errors="replace",
            timeout=timeout)
        payload = json.loads(summary.read_text(encoding="utf-8")) if summary.is_file() else None
        return process, payload

    def raw_stdout(self):
        runs = sorted((self.path / "DynaDocs.Tests/coverage/results/assurance").glob("run-*"))
        texts = []
        for run in runs:
            for path in sorted(run.glob(f"{self.stack}/job-*/stdout.log")):
                texts.append(path.read_text(encoding="utf-8", errors="replace"))
        return "\n".join(texts)

    def cleanup(self):
        for link in self.junctions:
            try:
                os.rmdir(link)
            except OSError:
                pass
        shutil.rmtree(self.path, ignore_errors=True)


class ReplayTests(unittest.TestCase):
    """Gate 10 probes: one per engine, each replaying the strong and weak subject for real."""

    maxDiff = None

    def replay(self, stack):
        weak_subject = Subject(stack, strong=False)
        self.addCleanup(weak_subject.cleanup)
        weak_process, weak = weak_subject.campaign()
        self.assertIsNotNone(weak, weak_process.stdout + weak_process.stderr)
        self.assertEqual(
            {"exit": 1, "exitCode": 1, "survived": 1, "killed": 0, "valid": 1,
             "changedTargets": [TARGET[stack]], "mode": "changed", "complete": True},
            {"exit": weak_process.returncode, "exitCode": weak["exitCode"],
             "survived": weak["mutation"]["counts"]["survived"],
             "killed": weak["mutation"]["counts"]["killed"],
             "valid": weak["mutation"]["counts"]["valid"],
             "changedTargets": weak["mutation"]["selection"]["changedTargets"],
             "mode": weak["mutation"]["selection"]["mode"],
             "complete": weak["measurementComplete"]},
            weak_process.stdout + weak_process.stderr)

        strong_subject = Subject(stack, strong=True)
        self.addCleanup(strong_subject.cleanup)
        strong_process, strong = strong_subject.campaign()
        self.assertIsNotNone(strong, strong_process.stdout + strong_process.stderr)
        self.assertEqual(
            {"exit": 0, "exitCode": 0, "killed": 1, "survived": 0, "valid": 1, "score": 100.0,
             "findings": [], "gaps": [], "changedTargets": [TARGET[stack]], "mode": "changed",
             "complete": True},
            {"exit": strong_process.returncode, "exitCode": strong["exitCode"],
             "killed": strong["mutation"]["counts"]["killed"],
             "survived": strong["mutation"]["counts"]["survived"],
             "valid": strong["mutation"]["counts"]["valid"],
             "score": strong["mutation"]["score"], "findings": strong["findings"],
             "gaps": strong["gaps"],
             "changedTargets": strong["mutation"]["selection"]["changedTargets"],
             "mode": strong["mutation"]["selection"]["mode"],
             "complete": strong["measurementComplete"]},
            strong_process.stdout + strong_process.stderr)

        witness = WITNESS.get(stack)
        if witness:
            self.assertIn(witness, strong_subject.raw_stdout(), strong_process.stdout)

    def test_dotnet_strong_subject_is_killed_and_weak_subject_survives(self):
        self.replay("dotnet")

    def test_python_strong_subject_is_killed_and_weak_subject_survives(self):
        self.replay("python")

    def test_node_strong_subject_is_killed_and_weak_subject_survives(self):
        self.replay("node")


if __name__ == "__main__":
    unittest.main()

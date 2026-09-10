"""The C# campaign is one exact AltCover eager full-suite invocation."""
import io
import sys
import tempfile
import unittest
import hashlib
import json
import os
import shutil
import subprocess
import xml.etree.ElementTree as ET
from contextlib import redirect_stderr
from unittest.mock import patch
from pathlib import Path
from copy import deepcopy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import (ASSEMBLY_PROJECTS, GATE_METRICS_PREBUILT_ENV, _assembly_facts, _candidate_assemblies, _identity_classes, _identity_producer, _source_facts, _source_facts_artifacts, _same_instrumented_map,
                             _altcover_aliases, _same_artifacts, _same_native_map, _same_restored_map,
                             _subject_commands, _template_original_map, _validate_instrumented,
                             _validate_restored, _write_commands, altcover_commands, main,
                             run_campaign, run_subject, snapshot_artifacts)
from csharp_join import coverage_methods, excluded_physical_tokens, join_methods

MINIMAL_SOLUTION = """Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DynaDocs", "DynaDocs.csproj", "{8537ADD5-149C-4DAA-B9B7-C26CA3852204}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DynaDocs.Tests", "DynaDocs.Tests\\DynaDocs.Tests.csproj", "{0838948C-BE5B-4F0D-980D-389149315058}"
EndProject
Global
\tGlobalSection(SolutionConfigurationPlatforms) = preSolution
\t\tDebug|Any CPU = Debug|Any CPU
\t\tRelease|Any CPU = Release|Any CPU
\tEndGlobalSection
\tGlobalSection(ProjectConfigurationPlatforms) = postSolution
\t\t{8537ADD5-149C-4DAA-B9B7-C26CA3852204}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
\t\t{8537ADD5-149C-4DAA-B9B7-C26CA3852204}.Debug|Any CPU.Build.0 = Debug|Any CPU
\t\t{8537ADD5-149C-4DAA-B9B7-C26CA3852204}.Release|Any CPU.ActiveCfg = Release|Any CPU
\t\t{8537ADD5-149C-4DAA-B9B7-C26CA3852204}.Release|Any CPU.Build.0 = Release|Any CPU
\t\t{0838948C-BE5B-4F0D-980D-389149315058}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
\t\t{0838948C-BE5B-4F0D-980D-389149315058}.Debug|Any CPU.Build.0 = Debug|Any CPU
\t\t{0838948C-BE5B-4F0D-980D-389149315058}.Release|Any CPU.ActiveCfg = Release|Any CPU
\t\t{0838948C-BE5B-4F0D-980D-389149315058}.Release|Any CPU.Build.0 = Release|Any CPU
\tEndGlobalSection
EndGlobal
"""

MINIMAL_LIBRARY = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>dydo</AssemblyName>
    <RootNamespace>Mini</RootNamespace>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="src/Widget.cs" />
  </ItemGroup>
</Project>
"""

MINIMAL_WIDGET = """namespace Mini;

public static class Widget
{
    public static int Classify(int value)
    {
        if (value < 0)
        {
            return -1;
        }

        return value == 0 ? 0 : 1;
    }
}
"""

MINIMAL_TEST_PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>DynaDocs.Tests</AssemblyName>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="WidgetTests.cs" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\DynaDocs.csproj" />
  </ItemGroup>
</Project>
"""

MINIMAL_WIDGET_TESTS = """using Mini;
using Xunit;

namespace Mini.Tests;

public class WidgetTests
{
    [Fact]
    public void ClassifiesNegativeAndPositiveValues()
    {
        Assert.Equal(-1, Widget.Classify(-2));
        Assert.Equal(1, Widget.Classify(5));
    }
}
"""

MINIMAL_PRODUCER_PROJECT = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.9.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="5.9.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.9.0" />
    <PackageReference Include="Mono.Cecil" Version="0.11.6" />
    <PackageReference Include="SonarAnalyzer.CSharp" Version="10.33.0.1635" PrivateAssets="all" GeneratePathProperty="true" />
    <Reference Include="SonarAnalyzer.CSharp">
      <HintPath>$(PkgSonarAnalyzer_CSharp)/analyzers/SonarAnalyzer.CSharp.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
"""

SUBJECT_LAUNCHER = """import runpy

runpy.run_path(r"{module}", run_name="__main__")
"""

SUBJECT_METRICS = """import os
import subprocess

producer = os.environ["DYNADOCS_GATE_METRICS_PREBUILT_DLL"]
done = subprocess.run(["dotnet", producer, "--syntax"], input="class A { int M(int v) => v; }",
                      text=True, capture_output=True)
raise SystemExit(done.returncode)
"""


def _write_all(root, files):
    for relative, text in files.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")


def _minimal_campaign_root(root):
    """A real .NET root shaped exactly as the campaign expects, holding no repository product code."""
    tools = Path(__file__).resolve().parents[1]
    _write_all(root, {
        ".config/dotnet-tools.json": (tools.parents[1] / ".config/dotnet-tools.json").read_text(encoding="utf-8"),
        "DynaDocs.sln": MINIMAL_SOLUTION,
        "DynaDocs.csproj": MINIMAL_LIBRARY,
        "src/Widget.cs": MINIMAL_WIDGET,
        "DynaDocs.Tests/DynaDocs.Tests.csproj": MINIMAL_TEST_PROJECT,
        "DynaDocs.Tests/WidgetTests.cs": MINIMAL_WIDGET_TESTS,
        "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj": MINIMAL_PRODUCER_PROJECT,
        "DynaDocs.Tests/coverage/csharp_coverage.py": SUBJECT_LAUNCHER.format(
            module=tools / "csharp_coverage.py"),
        "DynaDocs.Tests/coverage/tests/test_csharp_metrics.py": SUBJECT_METRICS,
    })
    for source in sorted((tools / "metrics").glob("*.cs")):
        shutil.copy2(source, root / "DynaDocs.Tests/coverage/metrics" / source.name)
    return root


def _template_module_xml(name, path, digest, methods=""):
    return (f'<Module hash="{digest}"><ModuleName>{name}</ModuleName><ModulePath>{path}</ModulePath>'
            f'<Classes><Class><Methods>{methods}</Methods></Class></Classes></Module>')


def _template_report(*modules):
    return f"<CoverageSession><Modules>{''.join(modules)}</Modules></CoverageSession>"


def _materialize_crlf_sources(root, source_commit, sources, destination):
    for row in sources:
        path = destination / row["path"]
        path.parent.mkdir(parents=True, exist_ok=True)
        blob = subprocess.run(
            ["git", "-c", f"safe.directory={root.as_posix()}", "show",
             f"{source_commit}:{row['path']}"], cwd=root, capture_output=True)
        if blob.returncode:
            raise AssertionError(blob.stderr.decode("utf-8", errors="replace"))
        path.write_bytes(blob.stdout.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))


def _write_replay_provenance(root, cache):
    producer = _identity_producer(root)[1]
    derived = {name: _source_facts(root, producer, name, cache) for name in ASSEMBLY_PROJECTS}
    (cache / "source-facts-artifacts.json").write_text(
        json.dumps(_source_facts_artifacts(root, cache, ASSEMBLY_PROJECTS), indent=2, sort_keys=True) + "\n",
        encoding="utf-8")
    provenance = cache / "provenance.json"
    provenance.write_text(json.dumps({
        "schema": 1, "kind": "newly derived diagnostic replay", "sourceCommit": "ec97c1b4",
        "retainedInputs": ["identity-pre.json", "identity-pre-artifacts.json",
                           "template-original-map.json", "coverage.opencover.xml"],
        "sourceRepresentation": "pinned checkout LF normalized to CRLF for retained checksum validation",
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return derived, provenance


def _replay_facts(root, raw):
    cache = raw / "newly-derived-ec97c1b4"
    cache.mkdir(exist_ok=True)
    provenance = cache / "provenance.json"
    if not provenance.is_file():
        return _write_replay_provenance(root, cache)
    derived = {name: json.loads((cache / f"source-facts-{name}.json").read_text(encoding="utf-8"))
               for name in ASSEMBLY_PROJECTS}
    return derived, provenance


def _retarget_assemblies(xml, root, pre):
    for equivalence in pre:
        name, path = equivalence["facts"]["assembly_name"], equivalence["facts"]["path"]
        old = next(module for module in ET.fromstring(xml).findall("./Modules/Module")
                   if module.findtext("ModuleName") == name).findtext("ModulePath")
        xml = xml.replace(old, str(root / path), 1)
    return xml


class CSharpCoverageTests(unittest.TestCase):
    def test_campaign_mechanism_callables_stay_within_cognitive_budget(self):
        root = Path(__file__).resolve().parents[3]
        python = root / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        metrics = subprocess.run([
            str(python), str(root / "DynaDocs.Tests/coverage/python_metrics.py")
        ], input=(root / "DynaDocs.Tests/coverage/csharp_coverage.py").read_text(encoding="utf-8"),
            text=True, capture_output=True, check=True)
        rows = {row["id"].split(":", 1)[0]: row["cognitive"]
                for row in json.loads(metrics.stdout)["methods"]}
        self.assertLessEqual(rows["_template_original_map"], 20)
        self.assertLessEqual(rows["run_campaign"], 20)

    def test_runner_subject_keeps_full_suite_first_then_uses_prebuilt_metrics(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            commands = _subject_commands(root)
            self.assertEqual("dotnet", commands[0][0])
            self.assertEqual(["test", "DynaDocs.sln"], commands[0][1:3])
            self.assertIn("--no-build", commands[0])
            self.assertEqual(sys.executable, commands[1][0])
            self.assertEqual(root / "DynaDocs.Tests/coverage/tests/test_csharp_metrics.py",
                             Path(commands[1][1]))
            self.assertNotIn("build", commands[1])

    def test_subject_runs_both_actions_in_order_with_inherited_recorder_environment(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            recorder = str(root / "recorder")
            for exits, expected in (((7, 0), 7), ((0, 9), 9)):
                with self.subTest(exits=exits), \
                        patch.dict(os.environ, {"ALTCOVER_RECORDER": recorder}, clear=True), \
                        patch("csharp_coverage.subprocess.run", side_effect=[
                            subprocess.CompletedProcess([], code) for code in exits
                        ]) as run:
                    self.assertEqual(expected, run_subject(root))
                self.assertEqual(_subject_commands(root), [call.args[0] for call in run.call_args_list])
                for call in run.call_args_list:
                    self.assertEqual(recorder, call.kwargs["env"]["ALTCOVER_RECORDER"])
                    self.assertFalse(call.kwargs.get("capture_output", False))
                self.assertEqual(str(root / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll"),
                                 run.call_args_list[1].kwargs["env"][GATE_METRICS_PREBUILT_ENV])

    def test_metrics_suite_records_real_gate_metrics_sequence_and_branch_visits(self):
        root = Path(__file__).resolve().parents[3]
        project = root / "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"
        build = subprocess.run(["dotnet", "build", str(project), "-c", "Debug", "--no-restore",
                                "-p:RunAnalyzers=false", "-p:NuGetAudit=false"],
                               cwd=root, text=True, capture_output=True)
        self.assertEqual(0, build.returncode, build.stdout + build.stderr)
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)
            instrumented = output / "instrumented"
            shutil.copytree(project.parent / "bin/Debug/net10.0", instrumented)
            template, report = output / "template.xml", output / "coverage.xml"
            prepare = ["dotnet", "tool", "run", "altcover", "--",
                       f"--inputDirectory={instrumented}", "--inplace",
                       f"--report={template}", "--reportFormat=OpenCover", "--eager",
                       "--localSource", "--visibleBranches",
                       "--assemblyFilter=^(?!(GateMetrics)$).*"]
            prepared = subprocess.run(prepare, cwd=root, text=True, capture_output=True)
            self.assertEqual(0, prepared.returncode, prepared.stdout + prepared.stderr)
            env = os.environ.copy()
            env[GATE_METRICS_PREBUILT_ENV] = str(instrumented / "GateMetrics.dll")
            runner = ["dotnet", "tool", "run", "altcover", "--", "runner",
                      f"--recorderDirectory={instrumented}", f"--workingDirectory={root}",
                      f"--executable={sys.executable}", f"--outputFile={report}", "--summary=N", "--",
                      str(root / "DynaDocs.Tests/coverage/tests/test_csharp_metrics.py")]
            collected = subprocess.run(runner, cwd=root, env=env, text=True, capture_output=True)
            self.assertEqual(0, collected.returncode, collected.stdout + collected.stderr)
            module = next(row for row in ET.parse(report).findall("./Modules/Module")
                          if row.findtext("ModuleName") == "GateMetrics")
            sequence_visits = sum(int(point.attrib["vc"])
                                  for point in module.findall(".//SequencePoint"))
            branch_visits = sum(int(point.attrib["vc"])
                                for point in module.findall(".//BranchPoint"))
            self.assertGreater(sequence_visits, 0)
            self.assertGreater(branch_visits, 0)

    def test_source_facts_persists_the_exact_producer_stdout(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            output = root / "raw"
            output.mkdir()
            stdout = '{"files":[],"behavior":{}}\n'
            with patch("csharp_coverage.subprocess.run", return_value=subprocess.CompletedProcess([], 0, stdout, "")) as run:
                self.assertEqual({"files": [], "behavior": {}},
                                 _source_facts(root, root / "producer.dll", "dydo", output))
            self.assertEqual(stdout, (output / "source-facts-dydo.json").read_text(encoding="utf-8"))
            self.assertEqual(1, run.call_count)

    def test_identity_producer_is_built_outside_instrumented_debug_directories(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            command, producer = _identity_producer(root)
            self.assertEqual("Release", command[command.index("-c") + 1])
            self.assertEqual(root / "DynaDocs.Tests/coverage/metrics/bin/Release/net10.0/GateMetrics.dll",
                             producer)
            prepare, _ = altcover_commands(root, root / "evidence")
            inputs = [Path(item.split("=", 1)[1]) for item in prepare
                      if item.startswith("--inputDirectory=")]
            self.assertFalse(any(producer.is_relative_to(path) for path in inputs))

    def test_completed_command_rows_are_persisted_incrementally(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)
            rows = [{"name": "build", "exit": 0}]
            _write_commands(output, rows)
            self.assertEqual(rows, json.loads((output / "commands.json").read_text()))

    def test_prepare_and_runner_commands_are_exact_and_unfiltered(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            output = root / "evidence/csharp"
            prepare, runner = altcover_commands(root, output)
            rendered = " ".join(prepare)
            self.assertIn("--eager", prepare)
            self.assertIn("--visibleBranches", prepare)
            self.assertNotIn("--showGenerated", prepare)
            self.assertIn("--assemblyFilter=^(?!(dydo|DynaDocs.Tests|GateMetrics)$).*", prepare)
            self.assertEqual(3, sum(arg.startswith("--inputDirectory=") for arg in prepare))
            self.assertEqual("runner", runner[5])
            self.assertNotIn("--filter", runner)
            executable = next(item for item in runner if item.startswith("--executable="))
            self.assertEqual(sys.executable, executable.split("=", 1)[1])
            self.assertEqual(2, runner.count("--"))
            self.assertIn("--_subject", runner)
            self.assertTrue(str(output / "template.opencover.xml") in rendered)

    def test_snapshot_hashes_assembly_pdb_source_and_reports(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ("A.dll", "A.pdb", "A.cs", "coverage.opencover.xml"):
                (root / name).write_bytes(name.encode())
            rows = snapshot_artifacts(root, [root / name for name in
                ("A.dll", "A.pdb", "A.cs", "coverage.opencover.xml")])
            self.assertEqual(["A.cs", "A.dll", "A.pdb", "coverage.opencover.xml"],
                             [row["path"] for row in rows])
            self.assertTrue(all(len(row["sha256"]) == 64 for row in rows))

    def test_identity_comparison_includes_document_table_and_point_origin(self):
        self.assertEqual({
            "dydo": "DynaDocs.csproj",
            "DynaDocs.Tests": "DynaDocs.Tests/DynaDocs.Tests.csproj",
            "GateMetrics": "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj",
        }, ASSEMBLY_PROJECTS)
        facts = {"assembly_name": "A", "module_id": "m", "documents": {"C:/A.cs": "A.cs"},
                 "methods": [{"token": 1, "identity": "A::M()", "key": "A::M()",
                              "points": [{"path": "A.cs", "origin": "maintained"}]}]}
        changed_document = {**facts, "documents": {"C:/A.cs": "obj/A.cs"}}
        changed_origin = {**facts, "methods": [{**facts["methods"][0],
                                                 "points": [{"path": "A.cs", "origin": "generated"}]}]}
        self.assertFalse(_same_native_map(facts, changed_document))
        self.assertFalse(_same_native_map(facts, changed_origin))

    def test_template_binds_original_tokens_despite_instrumented_renumbering(self):
        root = Path.cwd()
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
            {"token": 2, "identity": "A::Second()", "points": []},
        ]}, "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        template = """<CoverageSession><Modules><Module hash=\"ab\"><ModuleName>A</ModuleName>
            <ModulePath>bin/A.dll</ModulePath><Classes><Class><Methods>
            <Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>
            <Method><MetadataToken>2</MetadataToken><Name>A::Second()</Name></Method>
            </Methods></Class></Classes></Module></Modules></CoverageSession>"""
        self.assertEqual({"bin/A.dll": {1: "A::First()", 2: "A::Second()"}},
                         _template_original_map(template, root, [original]))
        saved = template.replace("bin/A.dll", str(root / "bin/__Saved/A.dll"))
        self.assertEqual({"bin/A.dll": {1: "A::First()", 2: "A::Second()"}},
                         _template_original_map(saved, root, [original]))
        before = {"assembly_name": "A", "module_id": "m", "pdb_sha256": "p",
                  "documents": {"C:/A.cs": "A.cs"}, "methods": [
                      {"token": 1, "identity": "A::First()", "key": "A::First()",
                       "points": [{"path": "A.cs", "origin": "maintained", "offset": 0,
                                   "checksum_algorithm": "SHA256", "checksum": "hash", "line": 1,
                                   "column": 0, "end_line": 1, "end_column": 2}]},
                  ]}
        instrumented = deepcopy(before)
        instrumented["methods"][0]["token"] = 100
        instrumented["methods"][0]["points"][0]["offset"] = 99
        self.assertTrue(_same_instrumented_map(before, instrumented))
        changed_point = deepcopy(instrumented)
        changed_point["methods"][0]["points"][0]["line"] = 2
        self.assertFalse(_same_instrumented_map(before, changed_point))
        ambiguous = deepcopy(instrumented)
        ambiguous["methods"][0]["points"].append(deepcopy(ambiguous["methods"][0]["points"][0]))
        self.assertFalse(_same_instrumented_map(before, ambiguous))
        ambiguous_documents = deepcopy(instrumented)
        ambiguous_documents["documents"]["C:/Other.cs"] = "A.cs"
        ambiguous_before = deepcopy(before)
        ambiguous_before["documents"]["C:/Other.cs"] = "A.cs"
        self.assertFalse(_same_instrumented_map(ambiguous_before, ambiguous_documents))

    def test_template_mapping_and_restoration_mismatches_fail_closed(self):
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
        ]}, "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        for method, message in (("<Method><MetadataToken>2</MetadataToken><Name>A::First()</Name></Method>", "Missing original"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method><Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>", "Duplicate template"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::Other()</Name></Method>", "Template signature")):
            template = f"<CoverageSession><Modules><Module hash=\"ab\"><ModuleName>A</ModuleName><ModulePath>bin/A.dll</ModulePath><Classes><Class><Methods>{method}</Methods></Class></Classes></Module></Modules></CoverageSession>"
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                    _template_original_map(template, Path.cwd(), [original])
        facts = {"assembly_name": "A", "sha256": "dll", "pdb_sha256": "pdb", "module_id": "m",
                 "documents": {"C:/A.cs": "A.cs"}, "methods": [{"token": 1, "identity": "A::First()",
                 "key": "A::First()", "points": [{"path": "A.cs", "origin": "maintained"}]}]}
        for field, value in (("sha256", "other-dll"), ("pdb_sha256", "other-pdb"),
                             ("module_id", "other-mvid"), ("documents", {"C:/A.cs": "Other.cs"})):
            changed = deepcopy(facts)
            changed[field] = value
            with self.subTest(field=field):
                self.assertFalse(_same_restored_map(facts, changed))
        self.assertFalse(_same_artifacts([{"path": "Source.cs", "bytes": 1, "sha256": "before"}],
                                         [{"path": "Source.cs", "bytes": 1, "sha256": "after"}]))

    def test_campaign_applies_source_behavior_eligibility_before_coverage_completeness(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dll, source = root / "bin/A.dll", root / "A.cs"
            dll.parent.mkdir()
            dll.write_bytes(b"original")
            source.write_text("void M() { }\nrecord A { public int Auto { get; set; } }\n")
            checksum = hashlib.sha256(source.read_bytes()).hexdigest()
            facts = {"assembly_name": "A", "sha1": hashlib.sha1(dll.read_bytes()).hexdigest(),
                     "documents": {str(source): "A.cs"}, "methods": [
                         {"token": 1, "identity": "System.Void A::M()", "key": "A::M()",
                          "points": [{"path": "A.cs", "origin": "maintained", "line": 1,
                                      "column": 0, "end_line": 1, "end_column": 12,
                                      "checksum_algorithm": "SHA256", "checksum": checksum}]},
                         {"token": 2, "identity": "System.Int32 A::get_Auto()", "key": "A::get_Auto()",
                          "points": [{"path": "A.cs", "origin": "maintained", "line": 2,
                                      "column": 0, "end_line": 2, "end_column": 40,
                                      "checksum_algorithm": "SHA256", "checksum": checksum}]},
                     ]}
            source_facts = {
                "files": [{"path": "A.cs", "methods": [{
                    "id": "A::M()", "line": 1, "column": 0, "end_line": 1, "end_column": 12,
                    "constructor": False, "cognitive": 0, "policy_cc": 1, "parameters": 0,
                }]}],
                "generated_files": [],
                "behavior": {
                    "structural_methods": [{
                        "key": "A::get_Auto()", "reason": "semantic synthesized auto accessor"
                    }],
                    "constructors": [], "fragments": [],
                    "declared_methods": [{
                        "key": "A::M()", "path": "A.cs", "line": 1, "column": 0,
                        "end_line": 1, "end_column": 12,
                    }],
                },
            }
            xml = f'''<CoverageSession><Modules><Module hash="{facts['sha1']}"><ModulePath>{dll}</ModulePath>
              <ModuleName>A</ModuleName><Files><File uid="1" fullPath="{source}" /></Files><Classes><Class><Methods>
              <Method><MetadataToken>1</MetadataToken><Name>System.Void A::M()</Name><SequencePoints>
              <SequencePoint vc="1" uspid="1" ordinal="0" offset="0" sl="1" sc="1" el="1" ec="2" fileid="1" />
              </SequencePoints><BranchPoints /></Method></Methods></Class></Classes></Module></Modules></CoverageSession>'''
            excluded = excluded_physical_tokens(source_facts, facts)
            self.assertEqual({2}, excluded)
            coverage = coverage_methods(xml, root, facts, ["bin/A.dll"], excluded)
            self.assertEqual(["System.Void A::M()"], list(coverage))
            joined = join_methods(root, source_facts, facts, coverage)
            self.assertEqual(1, joined["accountingSummary"]["total"])
            self.assertEqual("semantic synthesized auto accessor",
                             joined["accounting"][0]["sourceBehavior"]["reason"])
            no_point = {"assembly_name": "A", "methods": [
                {"token": 3, "identity": "System.Void GuardContext::.ctor()",
                 "key": "DynaDocs.Commands.GuardCommand/GuardContext::.ctor`0()", "points": []}
            ]}
            self.assertEqual(set(), excluded_physical_tokens({"behavior": {"structural_methods": [
                {"key": "DynaDocs.Commands.GuardCommand/GuardContext::.ctor`0()",
                 "reason": "semantic implicit constructor with no authored executable fragments"}
            ]}}, no_point))

    def test_snapshot_rejects_artifacts_outside_the_root_or_absent_from_disk(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "root"
            root.mkdir()
            (Path(folder) / "stray.dll").write_bytes(b"stray")
            with self.assertRaisesRegex(ValueError, "Artifact outside campaign root"):
                snapshot_artifacts(root, [Path(folder) / "stray.dll"])
            with self.assertRaisesRegex(ValueError, "Missing campaign artifact: absent.dll"):
                snapshot_artifacts(root, [root / "absent.dll"])

    def test_candidate_assemblies_require_every_named_campaign_assembly(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(ValueError, "Missing exact C# campaign assemblies"):
                _candidate_assemblies(Path(folder))

    def test_instrumented_comparison_rejects_points_without_full_source_identity(self):
        facts = {"assembly_name": "A", "module_id": "m", "pdb_sha256": "p",
                 "documents": {"C:/A.cs": "A.cs"},
                 "methods": [{"token": 1, "identity": "A::M()", "key": "A::M()",
                              "points": [{"path": "A.cs", "origin": "maintained", "line": 1}]}]}
        self.assertFalse(_same_instrumented_map(facts, deepcopy(facts)))

    def test_template_module_selection_rejects_every_unproven_binding(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
                {"token": 1, "identity": "A::First()", "points": []}]},
                "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
            method = "<Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>"
            for message, report in (
                ("Missing or ambiguous template module",
                 _template_report(_template_module_xml("B", "bin/A.dll", "ab", method))),
                ("Template module outside campaign root",
                 _template_report(_template_module_xml("A", "C:/elsewhere/A.dll", "ab", method))),
                ("Unknown template module alias",
                 _template_report(_template_module_xml("A", "bin/Other.dll", "ab", method))),
                ("Original template module hash mismatch",
                 _template_report(_template_module_xml("A", "bin/A.dll", "cd", method))),
                ("Missing template MethodDef token",
                 _template_report(_template_module_xml("A", "bin/A.dll", "ab",
                                                       "<Method><Name>A::First()</Name></Method>"))),
                ("Template module missing original identity",
                 _template_report(_template_module_xml("A", "bin/A.dll", "ab", method),
                                  _template_module_xml("B", "bin/B.dll", "ef"))),
            ):
                with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                    _template_original_map(report, root, [original])

    def test_template_mapping_rejects_one_token_claimed_by_two_original_methods(self):
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
            {"token": 1, "identity": "A::Second()", "points": []}]},
            "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        report = _template_report(_template_module_xml(
            "A", "bin/A.dll", "ab",
            "<Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>"))
        with self.assertRaisesRegex(ValueError, "Duplicate original MethodDef token: 1"):
            _template_original_map(report, Path.cwd(), [original])

    def test_source_facts_fail_closed_when_the_producer_exits_nonzero(self):
        with patch("csharp_coverage.subprocess.run",
                   return_value=subprocess.CompletedProcess([], 2, "", "producer refused\n")):
            with self.assertRaisesRegex(ValueError, "Source identity failed for dydo: producer refused"):
                _source_facts(Path.cwd(), Path("producer.dll"), "dydo")

    def test_command_line_rejects_incomplete_or_unusable_campaign_arguments(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            taken = root / "taken"
            taken.mkdir()
            for arguments in (["--root", str(root)],
                              ["--root", str(root), "--result-root", str(root / "fresh"),
                               "--extra-json", "{}"]):
                with self.subTest(arguments=arguments), \
                        patch.object(sys, "argv", ["csharp_coverage.py", *arguments]):
                    self.assertEqual(2, main())
            self.assertFalse((root / "fresh").exists())
            stderr = io.StringIO()
            with patch.object(sys, "argv", ["csharp_coverage.py", "--root", str(root),
                                            "--result-root", str(taken)]), redirect_stderr(stderr):
                self.assertEqual(2, main())
            self.assertIn("taken", stderr.getvalue())
            with self.assertRaisesRegex(ValueError, "ordinary unfiltered full suite"):
                run_campaign(root, root / "filtered", ["--filter=Category!=Slow"])

    def test_campaign_records_the_failing_native_build_and_stops(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            evidence = root / "evidence"
            arguments = ["csharp_coverage.py", "--root", str(root), "--result-root", str(evidence)]
            with patch.dict(os.environ), patch.object(sys, "argv", arguments):
                self.assertEqual(2, main())
            commands = json.loads((evidence / "commands.json").read_text(encoding="utf-8"))
            self.assertEqual(["build-0"], [row["name"] for row in commands])
            self.assertNotEqual(0, commands[0]["exit"])
            self.assertIn("DynaDocs.sln", " ".join(commands[0]["argv"]))
            self.assertTrue((evidence / "build-0.stdout").is_file())
            self.assertFalse((evidence / "identity-pre.json").exists())

    def test_hop3_05_replay_applies_physical_eligibility_to_retained_collector_output(self):
        """Replay only: retained source facts are absent, so this deliberately stops before join."""
        root = Path(__file__).resolve().parents[3]
        raw = root / "DynaDocs.Tests/coverage/results/native-g-20260909-hop3-05/raw"
        self.assertFalse((raw / "source-facts.json").exists())
        pre = json.loads((raw / "identity-pre.json").read_text(encoding="utf-8"))
        assembly = next(row["facts"] for row in pre if row["facts"]["assembly_name"] == "dydo")
        derived = root / "DynaDocs.Tests/coverage/results/native-g-20260909-hop3-06/raw/newly-derived-ec97c1b4"
        provenance = json.loads((derived / "provenance.json").read_text(encoding="utf-8"))
        self.assertEqual("ec97c1b4", provenance["sourceCommit"])
        source = json.loads((derived / "source-facts-dydo.json").read_text(encoding="utf-8"))
        xml = (raw / "coverage.opencover.xml").read_text(encoding="utf-8-sig")
        original_path = next(module for module in ET.fromstring(xml).findall("./Modules/Module")
                             if module.findtext("ModuleName") == "dydo").findtext("ModulePath")
        xml = xml.replace(original_path, str(root / assembly["path"]), 1)
        coverage = coverage_methods(xml, root, assembly, [assembly["path"]],
                                    excluded_physical_tokens(source, assembly))
        self.assertTrue(coverage)

    def test_hop3_06_newly_derived_crlf_replay_completes_retained_join(self):
        """Replay only: SourceBehavior and CRLF source bytes are newly derived from pinned ec97c1b4."""
        root = Path(__file__).resolve().parents[3]
        source_commit = "ec97c1b44015d83992303ea3dc9a9c045a82587e"
        raw = root / "DynaDocs.Tests/coverage/results/native-g-20260909-hop3-06/raw"
        pre = json.loads((raw / "identity-pre.json").read_text(encoding="utf-8"))
        artifacts = json.loads((raw / "identity-pre-artifacts.json").read_text(encoding="utf-8"))
        sources = [row for row in artifacts if row["path"].endswith(".cs")]
        expected = {row["path"]: (row["bytes"], row["sha256"]) for row in sources}
        with tempfile.TemporaryDirectory() as folder:
            diagnostic_root = Path(folder)
            _materialize_crlf_sources(root, source_commit, sources, diagnostic_root)
            actual = {row["path"]: (row["bytes"], row["sha256"]) for row in
                      snapshot_artifacts(diagnostic_root, [diagnostic_root / row["path"] for row in sources])}
            self.assertEqual(expected, actual)
            derived, provenance = _replay_facts(root, raw)
            cache = provenance.parent
            self.assertEqual("ec97c1b4", json.loads(provenance.read_text(encoding="utf-8"))["sourceCommit"])
            self.assertEqual(_source_facts_artifacts(root, cache, ASSEMBLY_PROJECTS),
                             json.loads((cache / "source-facts-artifacts.json").read_text(encoding="utf-8")))
            xml = _retarget_assemblies(
                (raw / "coverage.opencover.xml").read_text(encoding="utf-8-sig"), root, pre)
            template = {path: {int(token): identity for token, identity in methods.items()}
                        for path, methods in json.loads((raw / "template-original-map.json").read_text(encoding="utf-8")).items()}
            self.assertEqual(template, _template_original_map(xml, root, pre))
            coverage = {row["facts"]["assembly_name"]: coverage_methods(
                xml, root, row["facts"], _altcover_aliases(row["aliases"]),
                excluded_physical_tokens(derived[row["facts"]["assembly_name"]], row["facts"])) for row in pre}
            for name in ("dydo", "GateMetrics"):
                assembly = next(row["facts"] for row in pre if row["facts"]["assembly_name"] == name)
                self.assertTrue(join_methods(diagnostic_root, derived[name], assembly, coverage[name])["modules"])


class CSharpCampaignTests(unittest.TestCase):
    """One real AltCover 9.0.102 campaign, driven over a generated minimal .NET root."""

    @classmethod
    def setUpClass(cls):
        cls.folder = tempfile.TemporaryDirectory(ignore_cleanup_errors=True)
        cls.root = _minimal_campaign_root(Path(cls.folder.name))
        restored = subprocess.run(["dotnet", "tool", "restore"], cwd=cls.root, text=True,
                                  capture_output=True)
        if restored.returncode:
            raise AssertionError(restored.stdout + restored.stderr)
        cls.evidence = cls.root / "evidence"
        cls.producer = _identity_producer(cls.root)[1]
        with patch.dict(os.environ):
            cls.campaign_exit = run_campaign(cls.root, cls.evidence)

    @classmethod
    def tearDownClass(cls):
        cls.folder.cleanup()

    def recorded(self, name):
        return json.loads((self.evidence / name).read_text(encoding="utf-8"))

    def test_real_campaign_runs_every_native_command_and_restores_the_originals(self):
        self.assertEqual(0, self.campaign_exit)
        commands = self.recorded("commands.json")
        self.assertEqual(["build-0", "build-1", "build-2", "altcover-prepare", "altcover-runner"],
                         [row["name"] for row in commands])
        self.assertEqual([0] * 5, [row["exit"] for row in commands])
        self.assertIn("visits recorded",
                      (self.evidence / "altcover-runner.stdout").read_text(encoding="utf-8"))
        pre = {row["canonical"]: row["facts"]["sha256"] for row in self.recorded("identity-pre.json")}
        instrumented = {row["canonical"]: row["facts"]["sha256"]
                        for row in self.recorded("identity-instrumented.json")}
        self.assertEqual(sorted(pre), sorted(instrumented))
        self.assertTrue(all(pre[alias] != instrumented[alias] for alias in pre))
        self.assertEqual(self.recorded("identity-pre.json"), self.recorded("identity-post.json"))

    def test_real_campaign_joins_collected_hits_onto_the_authored_source_method(self):
        joined = self.recorded("joined.json")
        self.assertEqual(["dydo", "GateMetrics"], [row["assembly"] for row in joined["targets"]])
        library = next(row for row in joined["modules"] if row["path"] == "src/Widget.cs")
        method = library["methods"][0]
        self.assertTrue(method["id"].endswith("|System.Int32 Mini.Widget::Classify(System.Int32)"))
        self.assertEqual((6, 6, 3, 2), (method["covered"], method["total"], method["cc"],
                                        method["cognitive"]))
        self.assertTrue(library["branches"])
        self.assertTrue(all(hits > 0 for hits in library["branches"].values()))
        self.assertNotIn("src/Widget.cs", [row["path"] for row in joined["findings"]])

    def test_campaign_identity_validation_rejects_changed_identity_or_changed_bytes(self):
        pre, artifacts = self.recorded("identity-pre.json"), self.recorded("identity-pre-artifacts.json")
        assemblies = _candidate_assemblies(self.root)
        changed = deepcopy(pre)
        changed[0]["facts"]["module_id"] = "00000000-0000-0000-0000-000000000000"
        doctored = deepcopy(artifacts)
        next(row for row in doctored if row["path"].endswith(".cs"))["sha256"] = "0" * 64
        scratch = self.root / "validation"
        scratch.mkdir()
        for message, facts, rows in (("changed portable-PDB identity", changed, artifacts),
                                     ("changed portable-PDB or source bytes", pre, doctored)):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                _validate_instrumented(self.root, scratch, self.producer, assemblies, facts, rows)
        for message, facts, rows in (("artifact restoration mismatch", pre, doctored),
                                     ("Post-campaign restoration mismatch", changed, artifacts)):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                _validate_restored(self.root, self.producer, assemblies, facts, rows)

    def test_assembly_identity_rejects_same_name_conflicts_and_producer_failures(self):
        build = subprocess.run(["dotnet", "build", "DynaDocs.csproj", "-c", "Release",
                                "-p:RunAnalyzers=false", "-p:NuGetAudit=false",
                                "-p:UseSharedCompilation=false"],
                               cwd=self.root, text=True, capture_output=True)
        self.assertEqual(0, build.returncode, build.stdout + build.stderr)
        variants = [self.root / "bin/Debug/net10.0/dydo.dll",
                    self.root / "bin/Release/net10.0/dydo.dll"]
        with self.assertRaisesRegex(ValueError, "Conflicting same-name assembly identity: dydo"):
            _identity_classes(self.root, self.producer, variants)
        unreadable = self.root / "unreadable"
        unreadable.mkdir()
        shutil.copy2(variants[0], unreadable / "dydo.dll")
        with self.assertRaisesRegex(ValueError, "Assembly identity failed"):
            _assembly_facts(self.root, self.producer, unreadable / "dydo.dll")


if __name__ == "__main__":
    unittest.main()

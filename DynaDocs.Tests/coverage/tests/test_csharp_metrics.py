"""The real Roslyn/Sonar producer must preserve callable and language semantics."""
import json
import os
import shutil
import subprocess
import sys
import unittest
import tempfile
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import GATE_METRICS_PREBUILT_ENV
from gate_collect import Collectors, metric_findings
from gate_run import CommandLog


SDK_PROJECT = ('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
               '</PropertyGroup>{targets}</Project>')
STOP_BEFORE_COMPILE = ('<Target Name="StopBeforeCompile" BeforeTargets="CoreCompile" '
                       'Condition="\'$(RunAnalyzers)\' == \'true\'">'
                       '<Error Text="fixture stops before the compiler runs" /></Target>')
FAIL_AFTER_COMPILE = ('<Target Name="FailAfterCompile" AfterTargets="CoreCompile" '
                      'Condition="\'$(RunAnalyzers)\' == \'true\'">'
                      '<Error Text="fixture fails after the compiler wrote its log" /></Target>')
DUPLICATE_COMPILE = ('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
                     '<EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'
                     '<Compile Include="Thing.cs" /><Compile Include="./Thing.cs" /></ItemGroup></Project>')
UNCLOSED_PROJECT = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
UNCOMPILABLE_SOURCE = "namespace Fixture; public static class Invalid { public static int Value() => absent; }"


def tangled_subject():
    branches = "\n".join(f"        if (value == {index}) {{ value++; }}" for index in range(21))
    return ("namespace Fixture;\n\npublic sealed class Subject\n{\n"
            "    public Subject(int a, int b, int c, int d, int e, int f, int g, int h)\n"
            "    {\n        Total = a + b + c + d + e + f + g + h;\n    }\n\n"
            "    public int Total { get; }\n\n"
            "    public int Tangle(int value)\n    {\n" + branches + "\n        return value;\n    }\n}\n")


def trivial_class(name, value):
    return f"namespace Fixture;\n\npublic static class {name}\n{{\n    public static int Value() => {value};\n}}\n"


def write_repository(root, files):
    for relative, text in files.items():
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
    subprocess.run(["git", "init", "-q"], cwd=root, check=True, capture_output=True)


def gate_metrics_dll(root):
    prebuilt = os.environ.get(GATE_METRICS_PREBUILT_ENV)
    if prebuilt:
        dll = Path(prebuilt).resolve()
        if not dll.is_file():
            raise AssertionError(f"Missing prebuilt GateMetrics DLL: {dll}")
        return dll
    project = Path(root).resolve() / "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"
    build = subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet",
                            "--no-restore", "-p:RunAnalyzers=false"],
                           text=True, capture_output=True)
    if build.returncode:
        raise AssertionError(build.stdout + build.stderr)
    return project.parent / "bin/Debug/net10.0/GateMetrics.dll"


class GateMetricsSetupTests(unittest.TestCase):
    def test_explicit_prebuilt_mode_uses_named_dll_without_building(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dll = root / "instrumented/GateMetrics.dll"
            dll.parent.mkdir()
            dll.write_bytes(b"instrumented")
            with patch.dict(os.environ, {GATE_METRICS_PREBUILT_ENV: str(dll)}), \
                    patch(__name__ + ".subprocess.run") as run:
                self.assertEqual(dll.resolve(), gate_metrics_dll(root))
            run.assert_not_called()


class CSharpMetricsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]
        os.environ["APPDATA"] = str(cls.root / "dydo/_system/.local/appdata")
        os.environ.setdefault("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
        cls.project = cls.root / "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"
        cls.dll = gate_metrics_dll(cls.root)

    def measure(self, source):
        result = subprocess.run(["dotnet", str(self.dll), "--syntax"], input=source,
                                text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        return json.loads(result.stdout)["methods"]

    def compiled_facts(self, root, files):
        project = root / "Subject.csproj"
        items = ''.join(f'<Compile Include="{name}" />' for name in files)
        output = '<OutputType>Exe</OutputType>' if 'Program.cs' in files else ''
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>' + output + '</PropertyGroup><ItemGroup>' + items + '</ItemGroup></Project>')
        for name, source in files.items():
            (root / name).write_text(source, encoding="utf-8")
        subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"], check=True, capture_output=True)
        def collect(option, subject):
            command = ["dotnet", str(self.dll), option, str(subject), "--root", str(root)]
            if option == "--assembly":
                command.extend(["--project", str(project)])
            result = subprocess.run(command, text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr)
            return json.loads(result.stdout)
        return (collect("--project", project), collect("--assembly", root / "bin/Debug/net10.0/Subject.dll"))

    def sdk_fixture(self, root):
        project = root / "Subject.csproj"
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" /></ItemGroup></Project>')
        (root / "Source.cs").write_text("public sealed class Subject { public int Value() => 1; }")
        subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"],
                       check=True, capture_output=True)
        assembly = root / "bin/Debug/net10.0/Subject.dll"
        package_url = Path(os.environ["NUGET_PACKAGES"]) / "microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs"
        package_identity = "nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs"
        return project, assembly, package_url, package_identity

    def test_constructor_fragments_are_unique_shared_and_delegation_aware(self):
        with tempfile.TemporaryDirectory() as folder:
            source, emitted = self.compiled_facts(Path(folder), {
                "B.cs": 'public partial class C { int b = System.Environment.TickCount > 0 ? 1 : 2; public C(bool flag) : base() { if(flag) b++; } public C(int value) { b += value; } public C() : this(true) { } }',
                "A.cs": 'public partial class C { int a = System.Environment.TickCount > 0 ? 3 : 4; public int Auto { get; set; } public int Explicit { get { return a; } } }'
            })
            behavior = source["behavior"]
            fields = [row for row in behavior["fragments"] if row["kind"] == "initializer"]
            self.assertEqual(2, len(fields))
            self.assertEqual(2, len({row["id"] for row in fields}))
            self.assertTrue(all(len(row["owners"]) == 2 for row in fields))
            constructors = {row["key"]: row for row in behavior["constructors"]}
            self.assertEqual(3, len(constructors))
            self.assertEqual([0, 2, 3], sorted(row["cognitive"] for row in constructors.values()))
            self.assertTrue(set(constructors) <= {row["key"] for row in emitted["methods"]})
            structural = {row["key"] for row in behavior["structural_methods"]}
            self.assertTrue(any("get_Auto" in key for key in structural))
            self.assertFalse(any("get_Explicit" in key for key in structural))

    def test_primary_record_base_arguments_and_generic_metadata_identities(self):
        with tempfile.TemporaryDirectory() as folder:
            source, emitted = self.compiled_facts(Path(folder), {
                "Source.cs": 'public record B(int N); public record R(bool flag) : B(flag ? 1 : 2) { int x = System.Environment.TickCount > 0 ? 3 : 4; } public class O<T> { public class I<U> { public V M<V>(ref T t, U[,] values, V v) => v; } }'
            })
            behavior = source["behavior"]
            record = [row for row in behavior["constructors"] if row["key"].startswith("R::")]
            self.assertEqual(1, len(record))
            self.assertEqual(2, record[0]["cognitive"])
            self.assertTrue(any(row["kind"] == "constructor-clause" for row in behavior["fragments"]))
            semantic = {row["key"] for row in behavior["declared_methods"]}
            physical = {row["key"] for row in emitted["methods"]}
            self.assertTrue(any("O`1/I`1::M`1(!0&,!1[,],!!0)" == key for key in semantic))
            self.assertTrue(semantic <= physical)

    def test_conversion_operator_return_types_have_unique_semantic_and_emitted_keys(self):
        with tempfile.TemporaryDirectory() as folder:
            source, emitted = self.compiled_facts(Path(folder), {
                'Source.cs': 'public class C { public static explicit operator int(C value) => 1; public static explicit operator long(C value) => 2L; public static implicit operator string(C value) => "value"; }'
            })
            semantic = [row['key'] for row in source['behavior']['declared_methods']]
            physical = [row['key'] for row in emitted['methods'] if '::op_' in row['key']]
            self.assertEqual(3, len(semantic))
            self.assertEqual(3, len(set(semantic)))
            self.assertCountEqual(semantic, physical)
            self.assertEqual({'System.Int32', 'System.Int64', 'System.String'},
                             {key.rsplit('->', 1)[1] for key in semantic})

    def test_dynamic_parameter_identity_matches_emitted_object_metadata(self):
        with tempfile.TemporaryDirectory() as folder:
            source, emitted = self.compiled_facts(Path(folder), {
                'Source.cs': 'public class C { public dynamic Echo(dynamic value) => value; }'
            })
            semantic = {row['key'] for row in source['behavior']['declared_methods']}
            physical = {row['key'] for row in emitted['methods']}

            self.assertIn('C::Echo`0(System.Object)', semantic)
            self.assertTrue(semantic <= physical)

    def test_missing_workspace_language_service_cannot_fall_back_to_syntax_facts(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _ = self.compiled_facts(root, {'Source.cs': 'public class C { public int M() => 1; }'})
            self.assertTrue(source['has_compilation'])
            isolated = root / 'producer'
            shutil.copytree(self.dll.parent, isolated)
            (isolated / 'Microsoft.CodeAnalysis.CSharp.Workspaces.dll').unlink()
            result = subprocess.run(['dotnet', str(isolated / self.dll.name), '--project',
                                     str(root / 'Subject.csproj'), '--root', str(root)],
                                    text=True, capture_output=True)
            self.assertEqual(2, result.returncode, result.stdout + result.stderr)
            self.assertEqual('', result.stdout.strip())
            self.assertIn('C#', result.stderr)

    def test_shadow_mode_is_not_exposed(self):
        result = subprocess.run(["dotnet", str(self.dll), "--shadow", "subject.dll", "--root", str(self.root),
                                 "--output", "derived"], text=True, capture_output=True)
        self.assertEqual(2, result.returncode)
        self.assertEqual("", result.stdout.strip())

    def test_recursion_switch_and_boolean_runs_use_official_metric(self):
        rows = self.measure("class A { int R(int x) { if(x==0) return 0; return R(x-1); } int S(int x) => x switch { 1=>1, 2=>2, _=>0 }; bool B(bool a,bool b,bool c) { if(a && b && c) return true; return false; } }")
        self.assertEqual([2, 1, 2], [row["cognitive"] for row in rows])

    def test_same_short_types_overloads_and_parameters_keep_distinct_identities(self):
        rows = self.measure("namespace One { class A { void M(int x){} void M(string x){} } } namespace Two { class A { void M(int x){} A(int a,int b,int c,int d,int e,int f,int g,int h){} } }")
        self.assertEqual(4, len({row["id"] for row in rows}))
        self.assertEqual([1, 1, 1, 8], [row["parameters"] for row in rows])
        self.assertEqual([False, False, False, True], [row["constructor"] for row in rows])

    def test_local_function_and_lambda_are_accounted_as_source_callables(self):
        rows = self.measure("class A { int M(int x) { int L(int y) { if(y>0) return 1; return 0; } System.Func<int,int> f = z => z>0 ? 1 : 0; return L(x)+f(x); } }")
        self.assertEqual(3, len(rows))
        self.assertEqual(3, len({row["id"] for row in rows}))
        self.assertTrue(all(row["cognitive"] > 0 for row in rows))
        by_member = {row["member"]: row["policy_cc"] for row in rows}
        self.assertEqual(1, by_member["M"])
        self.assertEqual(2, by_member["L"])
        self.assertEqual(2, by_member["<lambda>"])

    def test_policy_cc_matches_the_pinned_sonar_syntax_convention(self):
        rows = self.measure("""
class C {
  int Straight() => 1;
  int Decisions(bool a, bool b, int? n) {
    if (a && b) n ??= 1;
    return n?.CompareTo(1) ?? 0;
  }
  int Switch(int n) => n switch { 0 => 0, 1 when n > 0 => 1, _ => 2 };
  int Catches() { try { return 1; } catch when (System.DateTime.Now.Ticks > 0 && System.Environment.TickCount > 0) { return 2; } finally { } }
}
""")
        by_member = {row["member"]: row["policy_cc"] for row in rows}
        self.assertEqual(1, by_member["Straight"])
        self.assertEqual(6, by_member["Decisions"])
        self.assertEqual(4, by_member["Switch"])
        self.assertEqual(2, by_member["Catches"])

    def test_malformed_source_fails_instead_of_emitting_partial_facts(self):
        result = subprocess.run(["dotnet", str(self.dll), "--syntax"], input="class {",
                                text=True, capture_output=True)
        self.assertEqual(2, result.returncode)
        self.assertIn("syntax", result.stderr.lower())

    def test_top_level_entrypoint_is_an_executable_member(self):
        rows = self.measure('if(args.Length > 0) System.Console.WriteLine(args[0]);')
        self.assertEqual(1, len(rows))
        self.assertEqual("<Main>$", rows[0]["member"])
        self.assertEqual(1, rows[0]["cognitive"])

    def test_semantic_cycles_include_qualified_references_but_not_using_only(self):
        source = "namespace A { public class One { public B.Two Value; } } namespace B { public class Two { public A.One Value; } }"
        result = subprocess.run(["dotnet", str(self.dll), "--syntax"], input=source,
                                text=True, capture_output=True, check=True)
        self.assertEqual([["A", "B"], ["B", "A"]], json.loads(result.stdout)["namespace_edges"])
        result = subprocess.run(["dotnet", str(self.dll), "--syntax"],
                                input="namespace A { using B; class One {} } namespace B { using A; class Two {} }",
                                text=True, capture_output=True, check=True)
        self.assertEqual([], json.loads(result.stdout)["namespace_edges"])

    def test_namespace_reference_in_type_declaration_uses_declaring_namespace(self):
        source = "namespace Root { public class Parent {} } namespace Root.Child { public class Child : Root.Parent {} }"
        result = subprocess.run(["dotnet", str(self.dll), "--syntax"], input=source,
                                text=True, capture_output=True, check=True)
        self.assertEqual([["Root.Child", "Root"]], json.loads(result.stdout)["namespace_edges"])

    def test_real_msbuild_project_produces_compilation_and_exact_file_identity(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / "Subject.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (root / "Source.cs").write_text("namespace A { public class One { public B.Two Value; } } namespace B { public class Two { public int Choose(bool yes) => yes ? 1 : 2; } }")
            subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"], check=True, capture_output=True)
            result = subprocess.run(["dotnet", str(self.dll), "--project", str(project), "--root", str(root)],
                                    text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr)
            facts = json.loads(result.stdout)
            self.assertTrue(facts["has_compilation"])
            self.assertEqual(["Source.cs"], [item["path"] for item in facts["files"]])
            self.assertEqual([["A", "B"]], facts["namespace_edges"])

    def test_package_compile_is_classified_by_locked_nuget_origin(self):
        project = self.root / "DynaDocs.Tests/DynaDocs.Tests.csproj"
        result = subprocess.run(["dotnet", str(self.dll), "--project", str(project), "--root", str(self.root)],
                                text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        facts = json.loads(result.stdout)
        self.assertEqual(["nuget:microsoft.net.test.sdk/18.0.1/build/net8.0/Microsoft.NET.Test.Sdk.Program.cs"],
                         [path for path in facts["generated_files"] if path.startswith("nuget:")])

    def test_sdk_generated_pdb_document_is_classified_by_locked_nuget_origin(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project, assembly, package_url, package_identity = self.sdk_fixture(root)
            result = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                     "--project", str(project)], text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr)
            facts = json.loads(result.stdout)
            main = next(row for row in facts["methods"]
                        if row["identity"] == "System.Void AutoGeneratedProgram::Main(System.String[])")
            self.assertTrue(main["points"])
            self.assertTrue(all(point["origin"] == "package" and point["path"] == package_identity
                                for point in main["points"]))
            self.assertEqual(package_identity, facts["documents"][str(package_url)])
            source = subprocess.run(["dotnet", str(self.dll), "--project", str(project), "--root", str(root)],
                                    text=True, capture_output=True, check=True)
            self.assertIn(package_identity, json.loads(source.stdout)["generated_files"])

    def test_in_root_obj_compile_item_is_classified_generated(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            generated = root / "obj/Gen.cs"
            generated.parent.mkdir()
            generated.write_text("public sealed class Generated { public int Value() => 1; }")
            project = root / "Subject.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="obj/Gen.cs" /></ItemGroup></Project>')
            subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"],
                           check=True, capture_output=True)
            assembly = root / "bin/Debug/net10.0/Subject.dll"
            result = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                     "--project", str(project)], text=True, capture_output=True, check=True)
            facts = json.loads(result.stdout)
            points = [point for row in facts["methods"] for point in row["points"]]
            self.assertTrue(points)
            self.assertTrue(all(point["path"] == "obj/Gen.cs" and point["origin"] == "generated"
                                for point in points))
            self.assertEqual({str(generated): "obj/Gen.cs"}, facts["documents"])
            source = subprocess.run(["dotnet", str(self.dll), "--project", str(project), "--root", str(root)],
                                    text=True, capture_output=True, check=True)
            self.assertIn("obj/Gen.cs", json.loads(source.stdout)["generated_files"])

    def test_unaccounted_pdb_document_fails_with_its_url(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project, assembly, package_url, _ = self.sdk_fixture(root)
            assets = root / "obj/project.assets.json"
            assets.unlink()
            result = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                     "--project", str(project)], text=True, capture_output=True)
            self.assertEqual(2, result.returncode)
            self.assertEqual("", result.stdout)
            self.assertTrue(result.stderr.strip().endswith(f"outside inventory root: {package_url}"), result.stderr)

            other = root / "other"
            other.mkdir()
            other_project = other / "Other.csproj"
            other_project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (other / "Other.cs").write_text("public sealed class Other { }")
            subprocess.run(["dotnet", "restore", str(other_project), "--verbosity", "quiet", "-p:NuGetAudit=false"],
                           check=True, capture_output=True)
            result = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                     "--project", str(other_project)], text=True, capture_output=True)
            self.assertEqual(2, result.returncode)
            self.assertEqual("", result.stdout)
            self.assertTrue(result.stderr.strip().endswith(f"outside inventory root: {package_url}"), result.stderr)

    def test_unaccounted_source_outside_root_fails(self):
        with tempfile.TemporaryDirectory() as folder, tempfile.TemporaryDirectory() as outside:
            root = Path(folder)
            external = Path(outside) / "External.cs"
            external.write_text("public class External { }")
            project = root / "Subject.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="' + str(external) + '" /></ItemGroup></Project>')
            subprocess.run(["dotnet", "restore", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"], check=True, capture_output=True)
            result = subprocess.run(["dotnet", str(self.dll), "--project", str(project), "--root", str(root)],
                                    text=True, capture_output=True)
            self.assertEqual(2, result.returncode, result.stdout + result.stderr)
            self.assertIn("outside inventory root", result.stderr)

    def test_portable_pdb_keeps_async_kickoff_and_adjacent_lambda_spans(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            project = root / "Subject.csproj"
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (root / "Source.cs").write_text('using System; using System.Threading.Tasks; public class C { int x = Environment.TickCount; public async Task<int> A() { await Task.Yield(); return x; } public int L() { Func<int> a = () => 1, b = () => 2; return a()+b(); } }')
            subprocess.run(["dotnet", "build", str(project), "--verbosity", "quiet", "-p:NuGetAudit=false"], check=True, capture_output=True)
            assembly = root / "bin/Debug/net10.0/Subject.dll"
            legacy = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root)],
                                    text=True, capture_output=True)
            self.assertEqual(2, legacy.returncode)
            self.assertEqual("", legacy.stdout)
            self.assertEqual("Expected --syntax, --project <csproj> --root <root> or --assembly <dll> --root <root> --project <csproj>.",
                             legacy.stderr.strip())
            result = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                     "--project", str(project)],
                                    text=True, capture_output=True)
            self.assertEqual(0, result.returncode, result.stderr)
            facts = json.loads(result.stdout)
            self.assertEqual("Subject", facts["assembly_name"])
            self.assertEqual("bin/Debug/net10.0/Subject.dll", facts["path"])
            self.assertGreater(facts["bytes"], 0)
            methods = facts["methods"]
            self.assertEqual(len(methods), len({row["token"] for row in methods}))
            moved = [row for row in methods if row["kickoff"]]
            self.assertEqual(["System.Threading.Tasks.Task`1<System.Int32> C::A()"], [row["kickoff"] for row in moved])
            lambdas = [row for row in methods if "b__" in row["identity"] and row["points"]]
            self.assertEqual(2, len(lambdas))
            self.assertNotEqual(lambdas[0]["points"], lambdas[1]["points"])
            self.assertTrue(all(point["path"] == "Source.cs" for row in methods for point in row["points"]))
            self.assertTrue(all(point["origin"] == "maintained" for row in methods for point in row["points"]))
            self.assertEqual({str(root / "Source.cs"): "Source.cs"}, facts["documents"])
            assembly.with_suffix(".pdb").unlink()
            missing = subprocess.run(["dotnet", str(self.dll), "--assembly", str(assembly), "--root", str(root),
                                      "--project", str(project)],
                                     text=True, capture_output=True)
            self.assertEqual(2, missing.returncode)


class CSharpCollectorTests(unittest.TestCase):
    """Drive the C# collectors against a real throwaway repository and its native tools."""

    @classmethod
    def setUpClass(cls):
        cls.tooling = Path(__file__).resolve().parents[1]
        os.environ["APPDATA"] = str(cls.tooling.parents[1] / "dydo/_system/.local/appdata")
        os.environ.setdefault("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
        cls.fixture = Path(tempfile.mkdtemp(prefix="dyd96-csharp-collectors-"))
        write_repository(cls.fixture, {
            "subject/Subject.csproj": SDK_PROJECT.format(targets=""),
            "subject/Subject.cs": tangled_subject(),
            "broken/Broken.csproj": UNCLOSED_PROJECT,
            "duplicate/Duplicate.csproj": DUPLICATE_COMPILE,
            "duplicate/Thing.cs": trivial_class("Thing", 1),
            "invalid/Invalid.csproj": SDK_PROJECT.format(targets=""),
            "invalid/Invalid.cs": UNCOMPILABLE_SOURCE,
            "stopped/Stopped.csproj": SDK_PROJECT.format(targets=STOP_BEFORE_COMPILE),
            "stopped/Stopped.cs": trivial_class("Stopped", 2),
            "late/Late.csproj": SDK_PROJECT.format(targets=FAIL_AFTER_COMPILE),
            "late/Late.cs": trivial_class("Late", 3),
        })
        collector = Collectors(cls.fixture, cls.fixture / "gate-output")
        collector.coverage = cls.tooling
        cls.projects = collector.projects()
        cls.inventory = collector.source_inventory()
        cls.stale_associations = collector.associations()
        cls.source = collector.csharp_source()
        evaluated = list(collector.project_rows)
        collector.project_rows = [row for row in evaluated if row["path"].startswith("subject/")]
        cls.clean_analyzers = collector.csharp_analyzers()
        collector.project_rows = [row for row in evaluated if not row["path"].startswith("subject/")]
        cls.gap_analyzers = collector.csharp_analyzers()

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.fixture, ignore_errors=True)

    def temporary(self, prefix):
        folder = Path(tempfile.mkdtemp(prefix=prefix))
        self.addCleanup(shutil.rmtree, folder, True)
        return folder

    def detached(self, root, coverage):
        collector = Collectors.__new__(Collectors)
        collector.root, collector.coverage = root, coverage
        collector.output = root / "gate-output"
        collector.log = CommandLog(root, collector.output / "commands")
        collector.inventory, collector.project_rows, collector.static = None, [], {}
        return collector

    def test_every_git_tracked_project_is_evaluated_through_real_msbuild(self):
        rows = self.projects["facts"]["projects"]
        self.assertEqual(["invalid/Invalid.csproj", "late/Late.csproj", "stopped/Stopped.csproj",
                          "subject/Subject.csproj"], [row["path"] for row in rows])
        subject = rows[-1]
        self.assertEqual(["subject/Subject.cs"], subject["compile"])
        self.assertEqual("subject/bin/Debug/net10.0/Subject.dll", subject["assembly"])
        self.assertFalse(subject["test"])

    def test_unrestorable_and_ambiguous_projects_are_accounted_not_dropped(self):
        self.assertEqual("error", self.projects["status"])
        self.assertEqual({"broken/Broken.csproj": "Project restore failed; evaluated imports are unreliable",
                          "duplicate/Duplicate.csproj": "Duplicate evaluated Compile identity"},
                         {row["path"]: row["message"] for row in self.projects["errors"]})

    def test_a_repository_without_an_evaluated_project_is_a_measurement_error(self):
        folder = self.temporary("dyd96-no-project-")
        write_repository(folder, {"Broken.csproj": UNCLOSED_PROJECT})

        answer = Collectors(folder, folder / "gate-output").projects()

        self.assertEqual("error", answer["status"])
        self.assertIn({"message": "No evaluated C# projects"}, answer["errors"])

    def test_source_inventory_takes_its_roles_from_evaluated_compile_ownership(self):
        roles = {row["path"]: row["role"] for row in self.inventory["facts"]["sources"]}
        self.assertEqual("target", roles["subject/Subject.cs"])
        self.assertEqual("unknown", roles["duplicate/Thing.cs"])
        self.assertEqual([{"path": "duplicate/Thing.cs", "type": "missing-evaluated-compile"}],
                         self.inventory["errors"])

    def test_an_association_manifest_that_misses_the_inventory_is_an_error(self):
        self.assertEqual("error", self.stale_associations["status"])
        self.assertEqual(1, len(self.stale_associations["errors"]))
        self.assertIn("unknown or duplicate associated module",
                      self.stale_associations["errors"][0]["message"])

    def test_a_matching_manifest_leaves_only_unassociated_targets_as_findings(self):
        folder = self.temporary("dyd96-associations-")
        manifest = {"schema": 1, "modules": [{"module": "target.py", "tests": ["tests/test_target.py"]}]}
        (folder / "test-associations.json").write_text(json.dumps(manifest), encoding="utf-8")
        collector = self.detached(folder, folder)
        collector.inventory = {"sources": [
            {"path": "target.py", "role": "target", "executable": True},
            {"path": "tests/test_target.py", "role": "test", "executable": True},
            {"path": "lonely.py", "role": "target", "executable": True}]}

        answer = collector.associations()

        self.assertEqual(manifest, answer["facts"]["manifest"])
        self.assertEqual([{"gate": "test-association", "path": "lonely.py",
                           "reason": "non-trivial target has no associated test file"}], answer["findings"])

    def test_collectors_that_need_the_inventory_fail_closed_without_it(self):
        collector = self.detached(self.temporary("dyd96-no-inventory-"), self.tooling)

        self.assertEqual([{"message": "Source inventory unavailable"}],
                         collector.associations()["errors"])
        with self.assertRaisesRegex(ValueError, "Source inventory unavailable"):
            collector.sources("cs")

    def test_roslyn_source_metrics_gate_cognitive_and_exempt_constructor_parameters(self):
        self.assertEqual([("subject/Subject.cs", "cognitive", 21)],
                         [(row["path"], row["gate"], row["actual"]) for row in self.source["findings"]])
        subject = next(row for row in self.source["facts"]["projects"]
                       if row["project"] == "subject/Subject.csproj")
        constructor = next(row for row in subject["files"][0]["methods"] if row["constructor"])
        self.assertEqual(8, constructor["parameters"])
        self.assertIn("subject/obj/Debug/net10.0/Subject.AssemblyInfo.cs", subject["generated_files"])

    def test_a_project_the_producer_rejects_does_not_hide_the_measured_projects(self):
        self.assertEqual("error", self.source["status"])
        self.assertEqual(["invalid/Invalid.csproj"], [row["path"] for row in self.source["errors"]])
        self.assertIn("csharp-source-", self.source["errors"][0]["message"])
        self.assertEqual(["late/Late.csproj", "stopped/Stopped.csproj", "subject/Subject.csproj"],
                         sorted(row["project"] for row in self.source["facts"]["projects"]))

    def test_a_producer_that_cannot_be_built_stops_csharp_source_measurement(self):
        folder = self.temporary("dyd96-no-producer-")
        (folder / "metrics").mkdir()
        (folder / "metrics/GateMetrics.csproj").write_text(UNCLOSED_PROJECT, encoding="utf-8")

        answer = self.detached(folder, folder).csharp_source()

        self.assertEqual("error", answer["status"])
        self.assertEqual("Measurement producer build failed", answer["errors"][0]["message"])

    def test_a_clean_analyzer_build_keeps_informational_rows_out_of_the_findings(self):
        raw = self.clean_analyzers["facts"]["raw"]
        self.assertEqual("pass", self.clean_analyzers["status"])
        self.assertEqual(["analyzers-0-prepare", "analyzers-0"],
                         [row["name"] for row in self.clean_analyzers["facts"]["commands"]])
        self.assertEqual([0, 0], [row["exit_code"] for row in self.clean_analyzers["facts"]["commands"]])
        self.assertEqual(["subject/Subject.csproj"], sorted({row["project"] for row in raw}))
        self.assertEqual(["note"], sorted({row["diagnostic"]["level"] for row in raw}))
        self.assertEqual([], self.clean_analyzers["findings"])

    def test_each_analyzer_report_gap_is_accounted_against_its_own_project(self):
        messages = {row["path"]: row["message"] for row in self.gap_analyzers["errors"] if "path" in row}
        self.assertEqual({"invalid/Invalid.csproj": "Native analyzer preparation build failed",
                          "late/Late.csproj": "Build failed without complete analyzer diagnostics",
                          "stopped/Stopped.csproj": "Missing native analyzer SARIF"}, messages)
        self.assertIn({"message": "Native build failed without an accounted unsuppressed diagnostic"},
                      self.gap_analyzers["errors"])

    def test_an_invalid_static_method_metric_fails_closed(self):
        for value in (-1, True, "3", None):
            method = {"id": "Fixture.Subject::Value", "line": 1, "cognitive": value,
                      "parameters": 0, "constructor": False}
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "Invalid static method metric"):
                metric_findings("Subject.cs", [method])


if __name__ == "__main__":
    unittest.main()

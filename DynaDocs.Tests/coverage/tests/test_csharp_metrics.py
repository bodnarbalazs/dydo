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


if __name__ == "__main__":
    unittest.main()

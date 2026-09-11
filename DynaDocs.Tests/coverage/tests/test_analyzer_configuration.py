"""Selected C# analyzer rules are effective in real builds for every project kind."""
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


class AnalyzerConfigurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(__file__).resolve().parents[3]

    def environment(self):
        env = dict(os.environ)
        env["APPDATA"] = str(self.root / "dydo/_system/.local/appdata")
        env["NUGET_PACKAGES"] = env.get("NUGET_PACKAGES", str(Path.home() / ".nuget/packages"))
        return env

    def test_effective_strictness_is_evaluated_for_product_test_and_metrics_projects(self):
        for relative in ("DynaDocs.csproj", "DynaDocs.Tests/DynaDocs.Tests.csproj",
                         "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"):
            with self.subTest(project=relative):
                result = subprocess.run([
                    "dotnet", "msbuild", str(self.root / relative), "-nologo",
                    "-getProperty:TreatWarningsAsErrors", "-getProperty:EnforceCodeStyleInBuild",
                ], cwd=self.root, env=self.environment(), text=True, capture_output=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertIn("true", result.stdout.lower())

    def test_each_selected_rule_fails_and_corrected_fixture_passes(self):
        local = self.root / "dydo/_system/.local"
        local.mkdir(parents=True, exist_ok=True)
        folder = Path(tempfile.mkdtemp(prefix="dyd96-analyzers-", dir=local))
        self.addCleanup(lambda: shutil.rmtree(folder, ignore_errors=True))
        (folder / "Subject.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework>'
            '<OutputType>Library</OutputType></PropertyGroup></Project>', encoding="utf-8")
        conditions = " ".join(f"if (x == {value}) x++;" for value in range(21))
        broken = f'''class Subject {{
    private int unusedField;
    private int UnusedMethod() => 1;
    public int Broken(int unused, int x, bool a, bool b) {{
        unusedField = x;
        {conditions}
        return a ? (b ? x : 1) : 0;
    }}
}}'''
        (folder / "Subject.cs").write_text(broken, encoding="utf-8")
        failure = subprocess.run(["dotnet", "build", "Subject.csproj", "-nologo", "-p:NuGetAudit=false"],
                                 cwd=folder, env=self.environment(), text=True, capture_output=True)
        output = failure.stdout + failure.stderr
        self.assertNotEqual(0, failure.returncode, output)
        for rule in ("S3776", "S3358", "IDE0051", "IDE0052", "IDE0060"):
            self.assertIn(rule, output)
        sonar_rules = set(re.findall(r"\bS\d{3,5}\b", output))
        self.assertEqual({"S3776", "S3358"}, sonar_rules)
        (folder / "Subject.cs").write_text(
            "public static class Subject { public static int Good(int value) => value + 1; }",
            encoding="utf-8")
        passing = subprocess.run(["dotnet", "build", "Subject.csproj", "-nologo", "--no-restore"],
                                 cwd=folder, env=self.environment(), text=True, capture_output=True)
        self.assertEqual(0, passing.returncode, passing.stdout + passing.stderr)


if __name__ == "__main__":
    unittest.main()

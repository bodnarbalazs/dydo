"""The C# campaign is one exact AltCover eager full-suite invocation."""
import sys
import tempfile
import unittest
from pathlib import Path
from copy import deepcopy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import (ASSEMBLY_PROJECTS, _identity_producer, _same_instrumented_map,
                             _same_artifacts, _same_native_map, _same_restored_map, _template_original_map,
                             _write_commands, altcover_commands, snapshot_artifacts)


class CSharpCoverageTests(unittest.TestCase):
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
            self.assertEqual(rows, __import__("json").loads((output / "commands.json").read_text()))

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
            self.assertIn("--no-build", runner)
            self.assertNotIn("--filter", runner)
            self.assertEqual(3, runner.count("--"))
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
                         _template_original_map(template, [original]))
        before = {"assembly_name": "A", "module_id": "m", "pdb_sha256": "p",
                  "documents": {"C:/A.cs": "A.cs"}, "methods": [
                      {"token": 1, "identity": "A::First()", "key": "A::First()",
                       "points": [{"path": "A.cs", "origin": "maintained", "offset": 0}]},
                  ]}
        instrumented = deepcopy(before)
        instrumented["methods"][0]["token"] = 100
        self.assertTrue(_same_instrumented_map(before, instrumented))

    def test_template_mapping_and_restoration_mismatches_fail_closed(self):
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
        ]}, "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        for method, message in (("<Method><MetadataToken>2</MetadataToken><Name>A::First()</Name></Method>", "Missing original"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method><Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>", "Duplicate template"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::Other()</Name></Method>", "Template signature")):
            template = f"<CoverageSession><Modules><Module hash=\"ab\"><ModuleName>A</ModuleName><ModulePath>bin/A.dll</ModulePath><Classes><Class><Methods>{method}</Methods></Class></Classes></Module></Modules></CoverageSession>"
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                _template_original_map(template, [original])
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


if __name__ == "__main__":
    unittest.main()

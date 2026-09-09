"""The C# campaign is one exact AltCover eager full-suite invocation."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import (ASSEMBLY_PROJECTS, _identity_producer, _same_native_map,
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


if __name__ == "__main__":
    unittest.main()

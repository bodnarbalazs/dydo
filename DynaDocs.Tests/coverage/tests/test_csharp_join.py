"""C# joins reject invented metrics, stale source and ambiguous logical owners."""
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_join import coverage_methods, join_methods


class CSharpJoinTests(unittest.TestCase):
    def test_full_method_signature_is_required_in_both_coverage_formats(self):
        data = {"A.dll": {"C:/repo/A.cs": {"A": {"System.Int32 A::M(System.Int32)": {"Lines": {"1": 1}, "Branches": []}}}}}
        xml = '<CoverageSession><Modules><Module><ModuleName>A</ModuleName><Classes><Class><Methods><Method cyclomaticComplexity="2"><Name>System.Int32 A::M(System.String)</Name></Method></Methods></Class></Classes></Module></Modules></CoverageSession>'
        with self.assertRaisesRegex(ValueError, "complexity"):
            coverage_methods(data, xml, Path("C:/repo"), "A")

    def fixture(self, root):
        source = "class A { int M() => 1; }"
        (root / "A.cs").write_text(source)
        digest = hashlib.sha256(source.encode()).hexdigest()
        method = {"id": "A::M@10", "line": 1, "column": 10, "end_line": 1, "end_column": 22,
                  "cognitive": 0, "parameters": 0, "constructor": False}
        facts = {"files": [{"path": "A.cs", "methods": [method]}], "generated_files": [],
                 "behavior": {"constructors": [], "fragments": [], "structural_methods": [],
                              "declared_methods": [{"key": "A::M`0()", "path": "A.cs", "line": 1, "column": 10, "end_line": 1, "end_column": 22}]}}
        assembly = {"methods": [{"identity": "System.Int32 A::M()", "key": "A::M`0()", "kickoff_key": None,
                                "points": [{"path": "A.cs", "offset": 0, "line": 1, "column": 21,
                                            "end_line": 1, "end_column": 22, "checksum_algorithm": "SHA256", "checksum": digest}]}]}
        coverage = {"System.Int32 A::M()": {"cc": 1, "files": {"A.cs": {"Lines": {"1": 1}, "Branches": []}}}}
        return facts, assembly, coverage

    def test_exact_method_join_and_stale_source_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            facts, assembly, coverage = self.fixture(root)
            result = join_methods(root, facts, assembly, coverage)
            self.assertEqual((1, 1), (result["modules"][0]["methods"][0]["covered"], result["modules"][0]["methods"][0]["total"]))
            (root / "A.cs").write_text("class A { int M() => 2; }")
            with self.assertRaisesRegex(ValueError, "checksum"):
                join_methods(root, facts, assembly, coverage)

    def test_missing_physical_coverage_and_equal_source_spans_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            facts, assembly, coverage = self.fixture(root)
            with self.assertRaisesRegex(ValueError, "coverage"):
                join_methods(root, facts, assembly, {})
            facts["files"][0]["methods"].append({**facts["files"][0]["methods"][0], "id": "other"})
            with self.assertRaisesRegex(ValueError, "ambiguous"):
                join_methods(root, facts, assembly, coverage)


if __name__ == "__main__":
    unittest.main()


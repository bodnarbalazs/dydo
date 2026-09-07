"""Coverlet campaign artifacts preserve exact native identities."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import locate_reports, merge_coverlet_json, snapshot_artifacts


class CSharpCoverageTests(unittest.TestCase):
    def test_exact_three_format_inventory_rejects_missing_or_duplicates(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ("coverage.json", "coverage.opencover.xml", "coverage.cobertura.xml"):
                (root / name).write_text("{}" if name.endswith("json") else "<CoverageSession/>")
            self.assertEqual(3, len(locate_reports(root)))
            (root / "other").mkdir()
            (root / "other/coverage.json").write_text("{}")
            with self.assertRaisesRegex(ValueError, "ambiguous"):
                locate_reports(root)

    def test_json_union_uses_exact_points_and_keeps_zero_hit_methods(self):
        base = {"A.dll": {"A.cs": {"A": {
            "System.Int32 A::Hit()": {"Lines": {"2": 1}, "Branches": []},
            "System.Int32 A::Miss()": {"Lines": {"3": 0}, "Branches": []},
        }}}}
        extra = {"A.dll": {"A.cs": {"A": {
            "System.Int32 A::Hit()": {"Lines": {"2": 4}, "Branches": []},
            "System.Int32 A::Miss()": {"Lines": {"3": 0}, "Branches": []},
        }}}}
        merged = merge_coverlet_json([base, extra])
        methods = merged["A.dll"]["A.cs"]["A"]
        self.assertEqual(4, methods["System.Int32 A::Hit()"]["Lines"]["2"])
        self.assertEqual(0, methods["System.Int32 A::Miss()"]["Lines"]["3"])
        extra["A.dll"]["A.cs"]["A"]["System.Int32 A::Hit()"]["Branches"] = [
            {"Line": 2, "Offset": 0, "EndOffset": 1, "Path": 0, "Ordinal": 0, "Hits": 1}]
        with self.assertRaisesRegex(ValueError, "inventory"):
            merge_coverlet_json([base, extra])

    def test_snapshot_hashes_assembly_pdb_source_and_reports(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ("A.dll", "A.pdb", "A.cs", "coverage.json"):
                (root / name).write_bytes(name.encode())
            rows = snapshot_artifacts(root, [root / name for name in
                ("A.dll", "A.pdb", "A.cs", "coverage.json")])
            self.assertEqual(["A.cs", "A.dll", "A.pdb", "coverage.json"],
                             [row["path"] for row in rows])
            self.assertTrue(all(len(row["sha256"]) == 64 for row in rows))


if __name__ == "__main__":
    unittest.main()

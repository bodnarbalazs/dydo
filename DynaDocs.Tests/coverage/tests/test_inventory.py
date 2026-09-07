"""The mutation handoff inventory is exact, complete, and fail-closed."""
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inventory import build_file_rows, source_fingerprint


class InventoryTests(unittest.TestCase):
    def test_fingerprint_uses_compact_sorted_json_and_includes_deletions(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "alive.py").write_text("print(1)\n", encoding="utf-8")
            rows = build_file_rows(root, ["alive.py", "gone.py"], deleted={"gone.py"})
            self.assertEqual({"path": "gone.py", "sha256": None, "deleted": True}, rows[1])
            expected = hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":"))
                                      .encode("utf-8")).hexdigest()
            self.assertEqual(expected, source_fingerprint(rows))

    def test_duplicate_case_and_missing_unaccounted_file_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "a.py").write_text("pass\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                build_file_rows(root, ["a.py", "A.py"])
            with self.assertRaises(ValueError):
                build_file_rows(root, ["missing.py"])


if __name__ == "__main__":
    unittest.main()

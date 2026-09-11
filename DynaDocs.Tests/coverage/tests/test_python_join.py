"""Python join rejects incomplete native executable partitions."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_join import _native_points


class PythonJoinTests(unittest.TestCase):
    def test_native_lines_and_branches_are_exact_disjoint_partitions(self):
        row = {"executed_lines": [1], "missing_lines": [2],
               "executed_branches": [[1, 2]], "missing_branches": [[1, 3]],
               "summary": {"num_statements": 2, "num_branches": 2}}
        lines, branches = _native_points(row)
        self.assertEqual({"1": 1, "2": 0}, lines)
        self.assertEqual({"1:2": 1, "1:3": 0}, branches)
        row["missing_lines"] = [1, 2]
        with self.assertRaisesRegex(ValueError, "partition"):
            _native_points(row)

    def test_native_summary_cannot_drop_zero_hit_denominators(self):
        row = {"executed_lines": [1], "missing_lines": [2],
               "executed_branches": [], "missing_branches": [],
               "summary": {"num_statements": 1, "num_branches": 0}}
        with self.assertRaisesRegex(ValueError, "denominator"):
            _native_points(row)


if __name__ == "__main__":
    unittest.main()

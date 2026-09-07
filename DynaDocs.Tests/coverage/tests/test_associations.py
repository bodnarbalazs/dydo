"""Simple exact source-to-test-file association contract."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from associations import validate_associations


class AssociationTests(unittest.TestCase):
    def setUp(self):
        self.inventory = [
            {"path": "src/a.py", "role": "target", "executable": True},
            {"path": "src/b.py", "role": "target", "executable": True},
            {"path": "tests/test_flow.py", "role": "test", "executable": True,
             "nativeTest": True},
        ]

    def test_many_to_many_exact_file_intent_is_valid(self):
        manifest = {"schema": 1, "modules": [
            {"module": "src/a.py", "tests": ["tests/test_flow.py"]},
            {"module": "src/b.py", "tests": ["tests/test_flow.py"]},
        ]}
        self.assertEqual([], validate_associations(self.inventory, manifest))

    def test_missing_relation_is_policy_failure(self):
        manifest = {"schema": 1, "modules": [
            {"module": "src/a.py", "tests": ["tests/test_flow.py"]},
        ]}
        self.assertEqual([{"gate": "test-association", "path": "src/b.py",
                           "reason": "non-trivial target has no associated test file"}],
                         validate_associations(self.inventory, manifest))

    def test_unknown_duplicate_wildcard_or_non_test_paths_are_invalid(self):
        invalid = [
            {"schema": 1, "modules": [{"module": "src/a.py", "tests": ["tests/*.py"]}]},
            {"schema": 1, "modules": [{"module": "missing.py", "tests": ["tests/test_flow.py"]}]},
            {"schema": 1, "modules": [{"module": "src/a.py", "tests": ["src/b.py"]}]},
            {"schema": 1, "modules": [{"module": "src/a.py", "tests": ["tests/test_flow.py", "tests/test_flow.py"]}]},
        ]
        for manifest in invalid:
            with self.subTest(manifest=manifest), self.assertRaises(ValueError):
                validate_associations(self.inventory, manifest)


if __name__ == "__main__":
    unittest.main()

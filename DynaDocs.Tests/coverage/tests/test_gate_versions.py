"""Assurance dependency evidence stays exact and independent of mutation tooling."""
import inspect
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import gate_versions


class GateVersionTests(unittest.TestCase):
    def test_version_collector_has_no_mutation_configuration_dependency(self):
        source = inspect.getsource(gate_versions.collect_versions).lower()
        self.assertNotIn("stryker", source)
        self.assertNotIn("mutation", source)

    def test_javascript_lock_requires_every_declared_exact_version(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "package.json").write_text(json.dumps({"dependencies": {"tool": "1.0.0"}}))
            (root / "package-lock.json").write_text(json.dumps({"packages": {
                "": {}, "node_modules/tool": {"version": "2.0.0"}}}))
            with self.assertRaisesRegex(ValueError, "disagree"):
                gate_versions.javascript_versions(root)


if __name__ == "__main__":
    unittest.main()

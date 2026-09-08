"""Private adapters publish one complete stable schema under an exclusive lock."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_adapter import publish


class GateAdapterTests(unittest.TestCase):
    def fixture(self, root):
        run = root / "assurance/run-a"
        run.mkdir(parents=True)
        inventory = run / "inventory.json"
        inventory.write_text('{"schema":1}')
        report = {"status": "fail", "facts": {"commands": []},
                  "findings": [{"gate": "line-coverage", "path": "a.py"}], "errors": []}
        candidate = {"commit": "a" * 40, "dirty": True, "sourceFingerprint": "b" * 64}
        return run, inventory, report, candidate

    def test_publish_retains_findings_and_removes_only_its_lock(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run, inventory, report, candidate = self.fixture(root)
            summary = root / "adapters/python-coverage.json"
            self.assertEqual(1, publish(summary, run, "python", "coverage", report,
                                        candidate, inventory))
            payload = json.loads(summary.read_text())
            self.assertEqual({"schema", "candidate", "stack", "gate", "inventory", "tools",
                              "commands", "collectors", "findings", "gaps",
                              "measurementComplete", "exitCode"}, set(payload))
            self.assertEqual("line-coverage", payload["findings"][0]["gate"])
            self.assertFalse(Path(str(summary) + ".lock").exists())
            self.assertTrue((run / "report.json").is_file())

    def test_publication_collision_preserves_foreign_lock_and_summary(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            run, inventory, report, candidate = self.fixture(root)
            summary = root / "adapters/python-coverage.json"
            summary.parent.mkdir()
            summary.write_text("foreign")
            lock = Path(str(summary) + ".lock")
            lock.write_text("foreign-lock")
            with self.assertRaisesRegex(ValueError, "lock"):
                publish(summary, run, "python", "coverage", report, candidate, inventory)
            self.assertEqual("foreign", summary.read_text())
            self.assertEqual("foreign-lock", lock.read_text())
            self.assertTrue((run / "report.json").is_file())


if __name__ == "__main__":
    unittest.main()

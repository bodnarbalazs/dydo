"""Private adapter publishes normalized evidence without hiding native outcomes."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_adapter import publish


class GateAdapterTests(unittest.TestCase):
    def test_publish_retains_findings_and_maps_only_normalized_status(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "artifact"
            report = {"status": "fail", "facts": {"native": 17},
                      "findings": [{"gate": "line-coverage"}], "errors": []}
            self.assertEqual(1, publish(root, "python", "coverage", report))
            latest = json.loads((root / "latest.json").read_text())
            self.assertEqual((1, "python", "coverage"),
                             (latest["schema"], latest["stack"], latest["gate"]))
            self.assertEqual(17, latest["result"]["facts"]["native"])
            self.assertTrue((root / latest["run"] / "report.json").is_file())

    def test_invalid_result_or_existing_run_identity_fails_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaisesRegex(ValueError, "pass hides"):
                publish(Path(folder), "python", "coverage",
                        {"status": "pass", "facts": {}, "findings": [1], "errors": []})


if __name__ == "__main__":
    unittest.main()

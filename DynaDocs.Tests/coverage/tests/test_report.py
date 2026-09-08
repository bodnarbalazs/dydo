"""Human output renders the current normalized adapter summaries."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from report import render


class ReportTests(unittest.TestCase):
    def test_current_gate_summary_uses_hcrap_vocabulary(self):
        text = render({"stack": "python", "gate": "coverage", "exitCode": 1,
                       "measurementComplete": True,
                       "findings": [{"gate": "hcrap", "path": "a.py", "member": "f"}],
                       "gaps": []})
        self.assertIn("python coverage: exit 1", text)
        self.assertIn("hcrap: a.py [f]", text)
        self.assertNotIn("CRAP", text)
        self.assertNotIn("T1", text)


if __name__ == "__main__":
    unittest.main()

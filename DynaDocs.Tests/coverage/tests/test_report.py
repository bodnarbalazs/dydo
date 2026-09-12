"""Human output renders the current normalized adapter summaries."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from report import render

TOOLING = Path(__file__).resolve().parents[1]
EMPTY_DEFAULT = ("import os, pathlib, sys\n"
                 f"sys.path.insert(0, {str(TOOLING)!r})\n"
                 "import report\n"
                 "report.DEFAULT = pathlib.Path(os.environ['DYDO_TEST_ADAPTERS'])\n"
                 "raise SystemExit(report.main())\n")


def _summary(exit_code, findings, gaps):
    return {"stack": "python", "gate": "coverage", "exitCode": exit_code,
            "measurementComplete": True, "findings": findings, "gaps": gaps}


def _run(arguments, environment=None):
    return subprocess.run([sys.executable, *arguments], capture_output=True, text=True,
                          encoding="utf-8", env=environment, timeout=120)


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

    def test_command_renders_named_summaries_and_returns_the_worst_exit(self):
        with tempfile.TemporaryDirectory() as folder:
            first = Path(folder) / "python-coverage.json"
            first.write_text(json.dumps(_summary(1, [{"gate": "line-coverage", "path": "a.py"}],
                                                 [{"collector": "knip", "reason": "unavailable"}])),
                             encoding="utf-8")
            second = Path(folder) / "node-static.json"
            second.write_text(json.dumps(_summary(0, [], [])), encoding="utf-8")

            answer = _run([str(TOOLING / "report.py"), str(first), str(second)])

            self.assertEqual(1, answer.returncode, answer.stderr)
            self.assertIn("  line-coverage: a.py", answer.stdout)
            self.assertIn("  gap: knip: unavailable", answer.stdout)
            self.assertIn("python coverage: exit 1 (complete)", answer.stdout)

    def test_unreadable_summary_is_reported_as_incomplete_measurement(self):
        with tempfile.TemporaryDirectory() as folder:
            broken = Path(folder) / "python-static.json"
            broken.write_text("{\"stack\": \"python\"}", encoding="utf-8")

            answer = _run([str(TOOLING / "report.py"), str(broken), str(Path(folder) / "absent.json")])

            self.assertEqual(2, answer.returncode, answer.stderr)
            self.assertIn("invalid summary", answer.stdout)
            self.assertIn("absent.json", answer.stdout)

    def test_command_without_adapter_summaries_reports_missing_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            environment = dict(os.environ, DYDO_TEST_ADAPTERS=folder)

            answer = _run(["-c", EMPTY_DEFAULT], environment)

            self.assertEqual(2, answer.returncode, answer.stderr)
            self.assertEqual("No adapter summaries found", answer.stdout.strip())


if __name__ == "__main__":
    unittest.main()

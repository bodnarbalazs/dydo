"""Private adapters publish one complete stable schema under an exclusive lock."""
import json
import io
import os
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr
from unittest import mock
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import gate_adapter
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

    def test_coverage_subprocess_gets_one_remaining_timeout_and_cooperative_cleanup(self):
        child = mock.Mock(returncode=130)
        child.wait.side_effect = [subprocess.TimeoutExpired(["campaign"], 20), 130]
        with mock.patch.dict(os.environ, {"DYDO_ROW_DEADLINE": "150"}), \
                mock.patch.object(gate_adapter.time, "monotonic", side_effect=[100, 120]), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child) as popen:
            with self.assertRaisesRegex(gate_adapter.MeasurementTimeout, "deadline"):
                gate_adapter.run_coverage_command(["campaign"], Path.cwd())
        self.assertEqual(20, child.wait.call_args_list[0].kwargs["timeout"])
        self.assertEqual(30, child.wait.call_args_list[1].kwargs["timeout"])
        self.assertEqual(1, child.send_signal.call_count)
        child.kill.assert_not_called()
        kwargs = popen.call_args.kwargs
        self.assertNotIn("stdout", kwargs)
        self.assertNotIn("stderr", kwargs)
        self.assertNotIn("stdin", kwargs)
        self.assertEqual(subprocess.CREATE_NEW_PROCESS_GROUP if sys.platform == "win32" else 0,
                         kwargs["creationflags"])

    def test_coverage_subprocess_force_terminates_after_cleanup_budget(self):
        child = mock.Mock(returncode=2)
        child.wait.side_effect = [subprocess.TimeoutExpired(["campaign"], 20),
                                  subprocess.TimeoutExpired(["campaign"], 30), 2]
        with mock.patch.dict(os.environ, {"DYDO_ROW_DEADLINE": "150"}), \
                mock.patch.object(gate_adapter.time, "monotonic", side_effect=[100, 120]), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child):
            with self.assertRaisesRegex(gate_adapter.MeasurementTimeout, "deadline"):
                gate_adapter.run_coverage_command(["campaign"], Path.cwd())
        self.assertEqual(1, child.send_signal.call_count)
        child.kill.assert_called_once_with()
        self.assertEqual(3, child.wait.call_count)

    def test_coverage_timeout_is_published_as_measurement_error_without_traceback(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            output = root / "results"
            inventory = root / "inventory.json"
            inventory.write_text('{"schema":1}', encoding="utf-8")
            captured = {}

            def publication(summary, run, stack, gate, report, candidate, path):
                captured.update(report)
                return 2

            stderr = io.StringIO()
            argv = ["gate_adapter.py", "--gate", "coverage", "--stack", "node",
                    "--root", str(root), "--output", str(output)]
            with mock.patch.object(sys, "argv", argv), \
                    mock.patch.object(gate_adapter, "_candidate", return_value=({"commit": "a" * 40}, [])), \
                    mock.patch.object(gate_adapter, "_inventory_artifact", return_value=(inventory, [])), \
                    mock.patch.object(gate_adapter, "collect_coverage",
                                      side_effect=gate_adapter.MeasurementTimeout("row deadline")), \
                    mock.patch.object(gate_adapter, "publish", side_effect=publication), \
                    redirect_stderr(stderr):
                self.assertEqual(2, gate_adapter.main())
            self.assertEqual("error", captured["status"])
            self.assertEqual("MeasurementTimeout", captured["errors"][0]["type"])
            self.assertNotIn("Traceback", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()

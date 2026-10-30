"""Real Windows job ownership: blocked descendants, interruption and retained evidence."""
import contextlib
import ctypes
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "skills/engineering/verify-dydo/scripts"))
import control_dydo as control
import owned_process


@unittest.skipUnless(os.name == "nt" and sys.version_info[:2] == (3, 12), "Windows CPython 3.12 owner")
class ControlDydoLifetimeTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="dydo-lifetime-")
        self.addCleanup(self.temporary.cleanup)
        root = patch.object(control, "RUNS", Path(self.temporary.name) / "runs")
        root.start()
        self.addCleanup(root.stop)
        self.run = control.allocate()
        self.addCleanup(self.retain)

    def retain(self):
        destination = os.environ.get("DYDO_LIFETIME_EVIDENCE")
        if destination:
            shutil.copytree(self.run, Path(destination) / self._testMethodName)

    def fixture(self, exit_root=False):
        descendant = "import threading; print('descendant-ready', flush=True); threading.Event().wait()"
        ending = "" if exit_root else "import threading; threading.Event().wait()"
        script = ("import subprocess, sys; "
                  f"subprocess.Popen([sys.executable, '-c', {descendant!r}], stdin=subprocess.DEVNULL, "
                  "stdout=sys.stdout, stderr=sys.stderr, creationflags=subprocess.CREATE_NO_WINDOW); "
                  + ending)
        return [sys._base_executable, "-c", script]

    def evidence(self, label):
        receipt = json.loads(next((self.run / "evidence").glob(label + '-*.json')).read_text())
        result = json.loads(Path(receipt["lifecycle"]).read_text())
        self.assertTrue(result["cleanup_confirmed"])
        self.assertTrue(result["root_signaled"])
        self.assertTrue(result["job_empty"])
        self.assertTrue(result["streams_complete"])
        self.assertGreaterEqual(result["job_total"], 2)
        self.assertTrue(all(row["signaled"] for row in result["held_handles"]))
        self.assertIn("descendant-ready", Path(receipt["stdout_path"]).read_text())
        return receipt, result

    def test_timeout_reaps_root_and_descendant_and_preserves_foreign_process(self):
        with subprocess.Popen([sys._base_executable, '-c', 'import threading; threading.Event().wait()'],
                              creationflags=subprocess.CREATE_NO_WINDOW) as foreign:
            try:
                with self.assertRaises(subprocess.TimeoutExpired):
                    control.execute(self.run, "hung", self.fixture(), self.run, timeout=3)
                receipt, result = self.evidence("hung")
                self.assertEqual("TimeoutExpired", receipt["error_type"])
                self.assertEqual("timeout", result["failure_category"])
                self.assertIsNone(foreign.poll())
                control.cleanup(self.run)
                self.assertFalse((self.run / "scratch").exists())
                self.assertTrue(Path(receipt["lifecycle"]).exists())
            finally:
                foreign.terminate()
                foreign.wait(timeout=10)

    def test_exited_root_does_not_leave_blocked_descendant(self):
        with self.assertRaises(subprocess.TimeoutExpired):
            control.execute(self.run, "orphan", self.fixture(exit_root=True), self.run, timeout=3)
        _, result = self.evidence("orphan")
        self.assertEqual(0, result["subject_status"])
        control.cleanup(self.run)

    def test_interrupt_after_descendant_output_cleans_before_releasing_busy(self):
        original = owned_process.jobs.poll_pipe
        interrupted = False

        def interrupt_after_output(kernel, reader):
            nonlocal interrupted
            original(kernel, reader)
            stream = Path(reader["target"].name)
            if interrupted or stream.name != "stdout.log":
                return
            # Interpreter startup diagnostics on stderr do not establish readiness.
            reader["target"].flush()
            if b"descendant-ready" in stream.read_bytes().splitlines():
                interrupted = True
                raise KeyboardInterrupt()

        def launch(run):
            control.execute(run, "interrupt", self.fixture(), run, timeout=10)

        with patch.object(control, "allocate", return_value=self.run), patch.object(control, "launch", launch):
            with patch.object(owned_process.jobs, "poll_pipe", side_effect=interrupt_after_output):
                with contextlib.redirect_stdout(io.StringIO()), self.assertRaises(KeyboardInterrupt):
                    control.main(["launch"])
        self.evidence("interrupt")
        self.assertFalse((self.run / "busy").exists())
        self.assertFalse((self.run / "scratch").exists())
        failure = json.loads(next((self.run / "evidence").glob("failure-*.json")).read_text())
        self.assertEqual("KeyboardInterrupt", failure["type"])

    def test_stdin_stdout_stderr_and_nonzero_exit_are_preserved(self):
        kernel = owned_process.jobs.bindings()
        set_limits = kernel.SetInformationJobObject
        launch = owned_process.jobs.launch

        def checked_limits(job, kind, pointer, size):
            self.assertEqual((9, 144), (kind, size))
            self.assertEqual(bytes(16) + b"\x00\x20\x00\x00" + bytes(124),
                             ctypes.string_at(pointer, size))
            return set_limits(job, kind, pointer, size)

        def checked_startup(api, request, environment, startup, process):
            # Win64 STARTUPINFOEXW: cb at 0, dwFlags at 60, attribute list at 104.
            raw = bytes(startup)
            self.assertEqual(112, len(raw))
            self.assertEqual(112, int.from_bytes(raw[:4], "little"))
            self.assertEqual(0x100, int.from_bytes(raw[60:64], "little"))
            self.assertNotEqual(bytes(8), raw[104:112])
            return launch(api, request, environment, startup, process)

        script = "import sys; print(sys.stdin.read(), end=''); print('error', file=sys.stderr); sys.exit(7)"
        with patch.object(owned_process.jobs, "bindings", return_value=kernel):
            with patch.object(kernel, "SetInformationJobObject", side_effect=checked_limits) as limits:
                with patch.object(owned_process.jobs, "launch", side_effect=checked_startup) as startup:
                    result = control.execute(self.run, "streams", [sys.executable, "-c", script],
                                             self.run, "payload\n")
        limits.assert_called_once()
        startup.assert_called_once()
        self.assertEqual((7, "payload\r\n", "error\r\n"), (result.returncode, result.stdout, result.stderr))
        control.cleanup(self.run)

    def test_unconfirmed_cleanup_retains_scratch(self):
        output = self.run / "evidence/unconfirmed"
        output.mkdir()
        control.write_json(output / "result.json", {"cleanup_confirmed": False})
        with self.assertRaisesRegex(ValueError, "cleanup unconfirmed"):
            control.cleanup(self.run)
        self.assertTrue((self.run / "scratch").exists())

    def test_missing_executable_retains_command_receipt(self):
        with self.assertRaises(FileNotFoundError):
            control.execute(self.run, "absent", [str(self.run / 'absent.exe')], self.run)
        receipt = json.loads(next((self.run / "evidence").glob("absent-*.json")).read_text())
        self.assertEqual("FileNotFoundError", receipt["error_type"])
        control.cleanup(self.run)

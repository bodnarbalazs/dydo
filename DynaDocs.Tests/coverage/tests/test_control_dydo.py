"""Driver boundary contracts; real CLI/hook evidence is a separate queued lifecycle run."""
import contextlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "skills/engineering/verify-dydo/scripts"))
import control_dydo as control


class ControlDydoContractTests(unittest.TestCase):
    real_process = staticmethod(subprocess.run)

    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="dydo-control-test-")
        self.addCleanup(temporary.cleanup)
        self.parent = Path(temporary.name)
        self.addCleanup(patch.stopall)
        patch.object(control, "RUNS", self.parent / "runs").start()
        self.child = patch.object(control.subprocess, "run", side_effect=self.process).start()
        self.head = b"pinned\n"
        self.untracked = b""
        self.sessions = set()
        self.run = control.allocate()

    def process(self, argv, **options):
        if argv[0] == "git":
            return self.git(argv, options["cwd"])
        if argv[1] == "build":
            folder = Path(argv[argv.index("--output") + 1])
            folder.mkdir()
            (folder / "dydo.dll").write_bytes(b"candidate")
            return subprocess.CompletedProcess(argv, 0, "built", "")
        return self.cli_process(argv, options)

    def git(self, argv, cwd):
        output = b""
        if argv[1] == "rev-parse":
            output = self.head
        elif argv[1] == "ls-files":
            output = self.untracked
        elif argv[1] == "init":
            (cwd / ".git").mkdir()
            output = "initialized"
        return subprocess.CompletedProcess(argv, 0, output, "")

    def cli_process(self, argv, options):
        project = Path(options["cwd"])
        verb = argv[2]
        code, output = 0, "ok"
        if verb == "init":
            self.scaffold(project)
        elif verb == "check" and (project / "dydo/verification-broken.md").exists():
            code, output = 1, "broken link: missing-target.md"
        elif verb == "guard":
            output = self.hook(argv, options["input"])
        return subprocess.CompletedProcess(argv, code, output, "")

    def scaffold(self, project):
        for name in ("dydo.json", "dydo/index.md", "AGENTS.md", "dydo/files-off-limits.md"):
            path = project / name
            path.parent.mkdir(exist_ok=True)
            path.write_text("generated")
        (project / ".claude").mkdir()
        control.write_json(project / ".claude/settings.json", {"hooks": {"PreToolUse": [
            {"hooks": [{"command": "dydo guard --host claude"}]}]}})

    def hook(self, argv, data):
        payload = json.loads(data)
        command = payload["tool_input"]["command"]
        session = payload["session_id"]
        verdict, reason = "deny", "blocked"
        if "mode set" in command:
            reason = f"own terminal: {command}"
            if "--host" in argv and session in self.sessions:
                verdict = "ask"
            self.sessions.add(session)
        return json.dumps({"hookSpecificOutput": {
            "permissionDecision": verdict, "permissionDecisionReason": reason}})

    def invoke(self, verb, journey=None):
        arguments = [verb, "--run", str(self.run)]
        if journey:
            arguments += ["--journey", journey]
        with contextlib.redirect_stdout(io.StringIO()):
            return control.main(arguments)

    def ready(self):
        control.launch(self.run)
        return self.run / "scratch/project"

    def test_launch_builds_once_and_doctor_pins_artifacts(self):
        self.ready()
        self.assertEqual(0, self.invoke("doctor"))
        builds = [row for row in self.child.call_args_list if row.args[0][1] == "build"]
        self.assertEqual(1, len(builds))
        self.assertIn(str(ROOT / "DynaDocs.csproj"), builds[0].args[0])
        state = json.loads((self.run / "state.json").read_text())
        self.assertEqual("pinned", state["candidate"]["head"])
        self.assertIn("dydo.dll", state["build"])

    def test_argv_is_fixed_shell_free_hidden_and_inherits_fifo(self):
        self.ready()
        self.invoke("drive", "activation")
        for call in self.child.call_args_list:
            options = call.kwargs
            self.assertNotIn("shell", options)
            self.assertEqual(control.HIDDEN, options["creationflags"])
            if "env" in options:
                self.assertEqual(os.environ.get("LC_SUITE_SLOT_TICKET"), options["env"].get("LC_SUITE_SLOT_TICKET"))
                self.assertEqual(str(self.run / "scratch/codex-home"), options["env"]["CODEX_HOME"])
            self.assertNotIn("mode", call.args[0])

    def test_foreign_instance_is_refused(self):
        for path in (self.parent, self.run / "..", self.run.with_name("not-a-run")):
            with self.subTest(path=path), self.assertRaises(ValueError):
                control.owned(path)
        control.write_json(self.run / "owner.json", {"checkout": "foreign"})
        with self.assertRaisesRegex(ValueError, "Foreign run owner"):
            self.invoke("cleanup")
        self.assertTrue((self.run / "scratch").exists())

    def test_cleanup_retains_evidence_and_is_repeatable(self):
        self.ready()
        self.invoke("drive", "init-docs")
        self.invoke("cleanup")
        self.invoke("cleanup")
        self.assertFalse((self.run / "scratch").exists())
        self.assertEqual("pinned", json.loads((self.run / "evidence/candidate.json").read_text())["head"])
        self.assertEqual(5, len(json.loads((self.run / "evidence/init-side-effects.json").read_text())))

    def test_cleanup_refuses_symlink_or_junction_escape(self):
        foreign = self.parent / "foreign"
        foreign.mkdir()
        (foreign / "keep").write_text("foreign")
        link = self.run / "scratch/escape"
        with patch.object(control.subprocess, "run", new=self.real_process):
            if os.name == "nt":
                subprocess.run(["cmd", "/c", "mklink", "/J", str(link), str(foreign)],
                               check=True, capture_output=True, creationflags=control.HIDDEN)
                self.addCleanup(os.rmdir, link)
            else:
                link.symlink_to(foreign, target_is_directory=True)
                self.addCleanup(link.unlink)
        with self.assertRaises(ValueError):
            self.invoke("cleanup")
        self.assertEqual("foreign", (foreign / "keep").read_text())
        self.assertTrue((self.run / "scratch").exists())
        (foreign / "keep").unlink()
        foreign.rmdir()
        with self.assertRaises(ValueError):
            self.invoke("cleanup")

    def test_doctor_refuses_changed_source_and_retains_failure(self):
        self.ready()
        self.head = b"different\n"
        with self.assertRaisesRegex(ValueError, "Candidate changed"):
            self.invoke("doctor")
        self.assertFalse((self.run / "scratch").exists())
        self.assertTrue(list((self.run / "evidence").glob("failure-*.json")))

    def test_doctor_refuses_changed_build(self):
        self.ready()
        (self.run / "scratch/bin/dydo.dll").write_bytes(b"other")
        with self.assertRaisesRegex(ValueError, "Built candidate changed"):
            self.invoke("doctor")

    def test_doctor_refuses_changed_fixture_configuration(self):
        project = self.ready()
        (project / "dydo.json").write_text("other configuration")
        with self.assertRaisesRegex(ValueError, "Owned fixture changed"):
            self.invoke("doctor")

    def test_doctor_detects_new_untracked_candidate_input(self):
        self.ready()
        self.untracked = b"DynaDocs.Tests/coverage/tests/test_control_dydo.py\0"
        with self.assertRaisesRegex(ValueError, "Candidate changed"):
            self.invoke("doctor")

    def test_launch_error_retains_attempted_argv(self):
        self.child.side_effect = OSError("cannot launch")
        with self.assertRaisesRegex(OSError, "cannot launch"):
            control.cli(self.run, "launch-error", ["--version"])
        receipt = next((self.run / "evidence").glob("launch-error-*.json"))
        log = json.loads(receipt.read_text())
        self.assertIsNone(log["exit"])
        self.assertEqual("--version", log["argv"][-1])
        self.assertEqual("cannot launch", log["error"])

    def test_doctor_requires_launch_and_installed_hook(self):
        with self.assertRaisesRegex(ValueError, "not ready"):
            control.doctor(self.run)
        project = self.ready()
        control.write_json(project / ".claude/settings.json", {"hooks": {"PreToolUse": []}})
        with self.assertRaisesRegex(ValueError, "hook missing"):
            self.invoke("doctor")

    def test_corrupt_ready_state_fails_with_retained_evidence_and_cleanup(self):
        control.write_json(self.run / "state.json", {"phase": "ready"})
        with self.assertRaises(KeyError):
            self.invoke("doctor")
        self.assertFalse((self.run / "scratch").exists())
        receipt = next((self.run / "evidence").glob("failure-*.json"))
        self.assertEqual("KeyError", json.loads(receipt.read_text())["type"])

    def test_child_failure_keeps_raw_exit_stdout_stderr(self):
        self.ready()
        self.child.side_effect = None
        self.child.return_value = subprocess.CompletedProcess([], 9, "out", "failure")
        with self.assertRaisesRegex(ValueError, r"Command failed \(9\)"):
            control.success(control.cli(self.run, "failed", ["check"]))
        control.cleanup(self.run)
        log = json.loads(next((self.run / "evidence").glob("failed-*.json")).read_text())
        self.assertEqual((9, "out", "failure"), (log["exit"], log["stdout"], log["stderr"]))

    def test_run_cannot_be_double_driven(self):
        with control.exclusive(self.run):
            with self.assertRaises(FileExistsError):
                self.invoke("cleanup")
        self.assertFalse((self.run / "busy").exists())
        self.assertTrue((self.run / "scratch").exists())

    def test_no_generic_command_or_mode_set_verb(self):
        for arguments in (["mode", "set", "overnight", "on"], ["drive", "--run", str(self.run),
                          "--journey", "merge"], ["launch", "--command", "anything"]):
            with self.subTest(arguments=arguments), contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit):
                    control.main(arguments)
        for arguments in (["doctor"], ["launch", "--run", str(self.run)], ["drive", "--run", str(self.run)]):
            with self.assertRaises(ValueError):
                control.main(arguments)

    def test_init_docs_observes_broken_link_and_restoration(self):
        project = self.ready()
        self.assertEqual(0, self.invoke("drive", "init-docs"))
        self.assertFalse((project / "dydo/verification-broken.md").exists())
        receipts = [json.loads(path.read_text()) for path in (self.run / "evidence").glob("check-*.json")]
        self.assertEqual([0, 0, 1], sorted(row["exit"] for row in receipts))

    def test_guard_payload_is_never_executed(self):
        project = self.ready()
        self.invoke("drive", "guard")
        self.assertEqual("retain this file\n", (project / "keep.txt").read_text())
        for call in self.child.call_args_list:
            self.assertNotIn("rm", call.args[0])

    def test_activation_records_both_directions_without_claiming_ui(self):
        self.ready()
        self.invoke("drive", "activation")
        receipts = [json.loads(path.read_text()) for path in (self.run / "evidence").glob("guard-*.json")]
        for value in ("on", "off"):
            rows = [row for row in receipts if f"overnight {value}" in row["stdin"]]
            decisions = [json.loads(row["stdout"])["hookSpecificOutput"]["permissionDecision"] for row in rows]
            self.assertEqual(["ask", "deny", "deny"], sorted(decisions))
        limits = json.loads((self.run / "evidence/activation-limits.json").read_text())
        self.assertFalse(limits["mode_set_executed"])
        self.assertEqual("unverified", limits["host_approval_ui"])

    def test_main_launch_returns_reconnectable_run(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(0, control.main(["launch"]))
        created = json.loads(output.getvalue())
        self.run = Path(created["run"])
        self.assertEqual(0, self.invoke("doctor"))

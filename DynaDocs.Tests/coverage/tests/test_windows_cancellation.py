"""Cancellation protocol regressions; native process proof runs on Windows CI."""
import json
import os
from pathlib import Path
import select
import signal
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import unittest
from contextlib import ExitStack, chdir
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import gate_adapter
import windows_job


class CancellationProtocolTests(unittest.TestCase):
    def test_adapter_records_the_launched_group_even_with_a_python_redirector(self):
        child = mock.Mock(pid=2468, returncode=0)
        child.wait.return_value = 0
        with tempfile.TemporaryDirectory() as folder, chdir(folder), \
                mock.patch.object(signal, "SIGBREAK", signal.SIGINT, create=True), \
                mock.patch.object(signal, "signal"), \
                mock.patch.object(gate_adapter, "sys", platform="win32"), \
                mock.patch.object(subprocess, "CREATE_NEW_PROCESS_GROUP", 512, create=True), \
                mock.patch.object(subprocess, "Popen", return_value=child) as launch, \
                mock.patch("run_tests.contain_descendants", return_value=None):
            self.assertEqual(0, windows_adapter("repeat"))
            self.assertEqual("2468", Path("owner-group-pid").read_text())
            self.assertEqual(512, launch.call_args.kwargs["creationflags"])

    def test_repeat_break_targets_the_launched_group_not_the_python_pid(self):
        # Model a venv redirector: Python PID 1780 belongs to launch group 2468.
        # Exercise the supervisor through its OS boundaries, including both teardown ACKs.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name, pid in (("owner-pid", 1780), ("owner-group-pid", 2468), ("adapter-pid", 2676)):
                (root / name).write_text(str(pid))
            result = root / "results/run/result.json"
            result.parent.mkdir(parents=True)
            result.write_text('{"aggregateExit": 130}')
            listener, ready = mock.MagicMock(), mock.MagicMock()
            listener.__enter__.return_value = listener
            listener.accept.return_value = (ready, None)
            ready.__enter__.return_value = ready
            ready.makefile.return_value.readline.side_effect = [b"[3176, 8644]\n", b"teardown\n", b"ack\n", b"ack\n"]
            facade, foreign = mock.Mock(), mock.Mock()
            facade.poll.return_value = foreign.poll.return_value = None
            facade.wait.return_value = 130
            kernel = mock.Mock()
            kernel.OpenProcess.side_effect = lambda _access, _inherit, pid: pid
            kernel.WaitForSingleObject.return_value = 0
            with mock.patch.object(subprocess, "run"), \
                    mock.patch.object(subprocess, "Popen", side_effect=[facade, foreign]), \
                    mock.patch.object(subprocess, "CREATE_NEW_PROCESS_GROUP", 512, create=True), \
                    mock.patch.object(signal, "CTRL_BREAK_EVENT", 1, create=True), \
                    mock.patch.object(socket, "socket", return_value=listener), \
                    mock.patch.object(select, "select", return_value=([listener], [], [])), \
                    mock.patch.object(windows_job, "bindings", return_value=kernel), \
                    mock.patch("run_tests.contain_descendants", return_value=None), \
                    mock.patch("run_tests.end_descendants"), \
                    mock.patch.object(os, "kill") as send_break:
                windows_probe("repeat", root)
            self.assertEqual([mock.call(2468, 1), mock.call(2468, 1)], send_break.call_args_list)
            self.assertEqual([mock.call(b"x"), mock.call(b"x")], ready.sendall.call_args_list)
            observed = json.loads((root / "observation.json").read_text())
            self.assertTrue(observed["foreign_alive"])
            self.assertTrue(all(observed["signaled"]))

    def test_native_fixture_passes_real_facade_preflight_on_every_platform(self):
        for mode in ("facade", "repeat", "adapter-killed", "escalation"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                prepare_windows_fixture(mode, root)
                child = subprocess.run([sys.executable, str(root / "gap_check.py"), "capabilities"],
                                       cwd=root, capture_output=True, text=True, timeout=10)
                self.assertEqual(0, child.returncode, child.stdout + child.stderr)
                self.assertIn("coverage: configured", child.stdout)
                self.assertFalse((root / "adapter-pid").exists())
                self.assertFalse((root / "owner-pid").exists())

    def test_readiness_reports_facade_rejection_before_accepting(self):
        with tempfile.TemporaryDirectory() as folder, socket.socket() as listener:
            root = Path(folder)
            prepare_windows_fixture("facade", root)
            manifest_path = root / "gap_check.json"
            manifest = json.loads(manifest_path.read_text())
            manifest["stacks"][0]["capabilities"]["coverage"]["artifacts"] = []
            manifest_path.write_text(json.dumps(manifest))
            with (root / "facade.stdout").open("wb") as stdout, (root / "facade.stderr").open("wb") as stderr:
                with subprocess.Popen([sys.executable, str(root / "gap_check.py"), "gate", "coverage"],
                                      cwd=root, stdout=stdout, stderr=stderr) as facade:
                    self.assertEqual(2, facade.wait(timeout=10))
                    # An unbound listener cannot accept: the exit must be diagnosed first.
                    with self.assertRaisesRegex(AssertionError, "Facade exited 2 before subject readiness.*") as error:
                        accept_subject(listener, facade, root)
            self.assertIn("configured gates require a required artifact", str(error.exception))
            self.assertFalse((root / "adapter-pid").exists())

    def test_readiness_accepts_the_subject_while_facade_is_running(self):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            listener.listen()
            with socket.create_connection(listener.getsockname()) as subject:
                subject.sendall(b"ready")
                with accept_subject(listener, mock.Mock(poll=mock.Mock(return_value=None)), Path.cwd()) as ready:
                    self.assertEqual(b"ready", ready.recv(5))

    def test_readiness_keeps_a_bounded_deadline_for_a_live_facade(self):
        with mock.patch.object(time, "monotonic", side_effect=[0, 20]):
            with self.assertRaisesRegex(TimeoutError, "Subject readiness timed out"):
                accept_subject(mock.Mock(), mock.Mock(poll=mock.Mock(return_value=None)), Path.cwd())

    def test_no_row_deadline_keeps_waiting_after_a_native_slice_expires(self):
        child = mock.Mock(returncode=0)
        completed = iter((False, False, True))

        def wait(timeout):
            if timeout is None or next(completed):
                return 0
            raise subprocess.TimeoutExpired(["campaign"], timeout)

        child.wait.side_effect = wait
        with mock.patch.dict(os.environ, {}, clear=True), \
                mock.patch("run_tests.contain_descendants", return_value=None), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child):
            self.assertEqual(0, gate_adapter.run_coverage_command(["campaign"], Path.cwd()))

    def test_native_wait_slices_recheck_deadline_and_observe_handle_completion(self):
        for completed in (False, True):
            with self.subTest(completed=completed):
                clock = [0.0]
                kernel = mock.Mock()

                def wait(_handle, milliseconds):
                    self.assertLessEqual(milliseconds, 200)
                    clock[0] += milliseconds / 1000
                    return 0 if completed and clock[0] >= .4 else 258

                kernel.WaitForSingleObject.side_effect = wait
                with mock.patch.object(windows_job.time, "monotonic", side_effect=lambda: clock[0]):
                    self.assertEqual(completed, windows_job.signaled(kernel, 123, 1))
                self.assertLessEqual(clock[0], 1)

    def test_cleanup_failure_remains_measurement_error_even_with_repeat_interrupt(self):
        for repeat in (False, True):
            with self.subTest(repeat=repeat):
                child = mock.Mock(returncode=130)
                child.wait.side_effect = [KeyboardInterrupt, 130]

                def cleanup(*_args, **_kwargs):
                    if repeat:
                        signal.raise_signal(signal.SIGINT)
                    raise TimeoutError("unconfirmed descendants")

                with mock.patch.dict(os.environ, {}, clear=True), \
                        mock.patch("run_tests.contain_descendants", return_value=None), \
                        mock.patch("run_tests.end_descendants", side_effect=cleanup), \
                        mock.patch.object(gate_adapter.os, "killpg", create=True), \
                        mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child):
                    with self.assertRaisesRegex(gate_adapter.MeasurementTimeout, "cleanup"):
                        gate_adapter.run_coverage_command(["campaign"], Path.cwd())

    def test_adapter_wait_returns_to_python_between_native_wait_slices(self):
        child = mock.Mock(returncode=0)

        def wait(timeout):
            self.assertIsNotNone(timeout)
            self.assertLessEqual(timeout, .2)
            return 0

        child.wait.side_effect = wait
        with mock.patch.dict(os.environ, {"DYDO_ROW_DEADLINE": "1800"}), \
                mock.patch.object(gate_adapter.time, "monotonic", return_value=0), \
                mock.patch("run_tests.contain_descendants", return_value=None), \
                mock.patch.object(gate_adapter.subprocess, "Popen", return_value=child):
            self.assertEqual(0, gate_adapter.run_coverage_command(["campaign"], Path.cwd()))

    def test_interrupted_owner_keeps_a_truthful_receipt_even_without_cleanup_evidence(self):
        with tempfile.TemporaryDirectory() as folder:
            value = windows_job.request([sys.executable], folder, Path(folder) / "owner")
            with mock.patch.object(windows_job, "validate", return_value={}), \
                    mock.patch.object(windows_job, "native_run", side_effect=KeyboardInterrupt):
                try:
                    result = windows_job.run(value)
                except KeyboardInterrupt:
                    self.fail("owner interruption escaped without its receipt")
            self.assertFalse(result["cleanup_confirmed"])
            self.assertFalse(result["complete"])
            self.assertEqual("KeyboardInterrupt", result["failure_category"])
            self.assertEqual(result, json.loads((Path(value["output"]) / "result.json").read_text()))

    def test_repeated_owner_signals_do_not_interrupt_teardown_or_receipt(self):
        def teardown(*_args):
            signal.raise_signal(signal.SIGINT)
            signal.raise_signal(signal.SIGINT)
            return {"cleanup_confirmed": True, "complete": True}

        with tempfile.TemporaryDirectory() as folder:
            value = windows_job.request([sys.executable], folder, Path(folder) / "owner")
            previous = signal.getsignal(signal.SIGINT)
            with mock.patch.object(windows_job, "validate", return_value={}), \
                    mock.patch.object(windows_job, "native_run", side_effect=teardown):
                try:
                    result = windows_job.run(value)
                except KeyboardInterrupt:
                    self.fail("repeat signal interrupted owner teardown")
            self.assertIs(previous, signal.getsignal(signal.SIGINT))
            self.assertTrue(result["cleanup_confirmed"])
            self.assertFalse(result["complete"])
            self.assertEqual("KeyboardInterrupt", result["failure_category"])
            self.assertEqual(result, json.loads((Path(value["output"]) / "result.json").read_text()))


@unittest.skipUnless(sys.platform == "win32", "Native Windows console and job containment")
class NativeWindowsCancellationTests(unittest.TestCase):
    def test_facade_cancellation_confirms_real_job_members_and_preserves_receipt(self):
        self.check_probe("facade")

    def test_repeat_break_during_native_teardown_preserves_receipt(self):
        self.check_probe("repeat")

    def test_unresponsive_owner_is_escalated_without_leaving_live_members(self):
        self.check_probe("escalation")

    def test_killed_adapter_cannot_leave_its_separate_owner_or_subject_alive(self):
        self.check_probe("adapter-killed")

    def check_probe(self, mode):
        # The disposable supervisor owns its own console/job. Never join the unittest parent.
        evidence = os.environ.get("DYDO_CANCELLATION_EVIDENCE")
        with tempfile.TemporaryDirectory() as folder:
            root = Path(evidence).resolve() / mode if evidence else Path(folder)
            root.mkdir(parents=True, exist_ok=True)
            probe = subprocess.run([sys.executable, str(Path(__file__).resolve()), "--probe", mode, str(root)],
                                   creationflags=subprocess.CREATE_NEW_CONSOLE,
                                   capture_output=True, text=True, timeout=90)
            (root / "probe.stdout").write_text(probe.stdout, encoding="utf-8")
            (root / "probe.stderr").write_text(probe.stderr, encoding="utf-8")
            self.assertEqual(0, probe.returncode, probe.stderr)
            observed = json.loads((root / "observation.json").read_text())
            self.assertTrue(all(observed["signaled"]), observed)
            self.assertTrue(observed["foreign_alive"], observed)
            if mode == "adapter-killed":
                return
            self.assertEqual(130, observed["exit"], observed)
            self.assertEqual(130, observed["facade"]["aggregateExit"], observed)
            if mode == "escalation":
                self.assertIsNone(observed["campaign"], observed)
                return
            receipt = observed["campaign"]
            self.assertTrue(receipt["cleanup_confirmed"], observed)
            self.assertFalse(receipt["complete"], observed)
            self.assertEqual("KeyboardInterrupt", receipt["failure_category"])
            self.assertGreaterEqual(receipt["job_total"], 2)
            self.assertTrue(receipt["job_empty"])
            self.assertTrue(receipt["root_signaled"])
            self.assertTrue(receipt["streams_complete"])
            self.assertTrue(all(row["signaled"] for row in receipt["held_handles"]))


def windows_subject():
    """A real, separately grouped descendant, ready before the supervisor interrupts."""
    with subprocess.Popen([sys.executable, "-c", "import threading; threading.Event().wait(80)"],
                          creationflags=subprocess.CREATE_NEW_PROCESS_GROUP) as child:
        with socket.create_connection(("127.0.0.1", int(os.environ["CANCEL_PORT"])), timeout=15) as ready:
            ready.sendall(json.dumps([os.getpid(), child.pid]).encode() + b"\n")
        child.wait(timeout=80)


def windows_owner(mode):
    import run_tests
    signal.signal(signal.SIGBREAK, signal.default_int_handler)
    Path("owner-pid").write_text(str(os.getpid()))
    if mode == "escalation":
        # Emulate an owner stuck in native code: capture and discard cooperative requests.
        original = windows_job.run

        def ignore_break(*args, **kwargs):
            with mock.patch.object(windows_job.threading.Event, "set"):
                return original(*args, **kwargs)

        windows_job.run = ignore_break
    if mode == "repeat":
        original_bindings = windows_job.bindings

        def bindings():
            kernel = original_bindings()
            terminate = kernel.TerminateJobObject

            def teardown(*args):
                answer = terminate(*args)
                with socket.create_connection(("127.0.0.1", int(os.environ["CANCEL_PORT"])), timeout=15) as ready:
                    ready.sendall(b"teardown\n")
                    for _ in range(2):
                        ready.recv(1)
                        ready.sendall(b"ack\n")
                return answer

            kernel.TerminateJobObject = teardown
            return kernel

        windows_job.bindings = bindings
    return run_tests._run_assurance_campaign(Path.cwd(), [], str(Path("published").resolve()))


def windows_adapter(mode):
    signal.signal(signal.SIGBREAK, signal.default_int_handler)
    Path("adapter-pid").write_text(str(os.getpid()))
    if mode == "escalation":
        gate_adapter.CLEANUP_SECONDS = 3
    launch = subprocess.Popen

    def record_group(*args, **kwargs):
        child = launch(*args, **kwargs)
        # A Windows venv redirector is the group leader; its Python child has another PID.
        Path("owner-group-pid").write_text(str(child.pid))
        return child

    with mock.patch.object(subprocess, "Popen", side_effect=record_group):
        return gate_adapter.run_coverage_command(
            [sys.executable, str(Path(__file__).resolve()), "--owner", mode], Path.cwd())


def prepare_windows_fixture(mode, root):
    """Build the same facade inputs for portable preflight and native cancellation."""
    # Bound facade root discovery even when CI retains fixtures beneath the checkout.
    subprocess.run(["git", "init", "--quiet", str(root)], check=True)
    subject = root / "DynaDocs.Tests/coverage/csharp_coverage.py"
    subject.parent.mkdir(parents=True)
    subject.write_text("import runpy,sys; sys.argv=['subject','--subject']; runpy.run_path(" +
                       repr(str(Path(__file__).resolve())) + ",run_name='__main__')")
    shutil.copyfile(TOOLS / "gap_check.py", root / "gap_check.py")
    command = [sys.executable, str(Path(__file__).resolve()), "--adapter", mode]
    caps = {name: {"state": "unavailable", "reason": "fixture"} for name in ("test", "static", "mutation")}
    # The campaign publishes this receipt on completion and cooperative interruption.
    # Forced interruption may omit it; the facade's interrupted path preserves that truth.
    caps["coverage"] = {"state": "configured", "command": {"kind": "argv", "argv": command},
                        "artifacts": [{"path": "published/identities.json", "required": True}]}
    manifest = {"schema": 1, "artifactRoot": "results", "stacks": [{"name": "dotnet", "kind": "fixture",
                "cwd": ".", "isolation": {"requirement": "in-place", "evidence": {"state": "verified", "kind": "direct"}},
                "capabilities": caps}]}
    (root / "gap_check.json").write_text(json.dumps(manifest))


def accept_subject(listener, facade, root):
    deadline = time.monotonic() + 20
    while True:
        code = facade.poll()
        if code is not None:
            stdout = (root / "facade.stdout").read_text(encoding="utf-8", errors="replace")
            stderr = (root / "facade.stderr").read_text(encoding="utf-8", errors="replace")
            raise AssertionError(f"Facade exited {code} before subject readiness\nstdout:\n{stdout}\nstderr:\n{stderr}")
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError("Subject readiness timed out while facade was still running")
        if select.select([listener], [], [], min(.2, remaining))[0]:
            return listener.accept()[0]


def windows_probe(mode, root):
    import run_tests
    prepare_windows_fixture(mode, root)
    # Even the red implementation's leaked tree is cleaned by this disposable supervisor.
    containment = run_tests.contain_descendants()
    kernel = windows_job.bindings()
    with ExitStack() as stack:
        listener = stack.enter_context(socket.socket())
        listener.bind(("127.0.0.1", 0))
        listener.listen()
        listener.settimeout(20)
        env = {**os.environ, "CANCEL_PORT": str(listener.getsockname()[1])}
        stdout = stack.enter_context((root / "facade.stdout").open("wb"))
        stderr = stack.enter_context((root / "facade.stderr").open("wb"))
        facade = subprocess.Popen([sys.executable, str(root / "gap_check.py"), "gate", "coverage"], cwd=root,
                                  env=env, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP, stdout=stdout, stderr=stderr)
        foreign = subprocess.Popen([sys.executable, "-c", "import threading; threading.Event().wait(80)"],
                                   creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        try:
            with accept_subject(listener, facade, root) as ready:
                pids = json.loads(ready.makefile("rb").readline())
            owner = int((root / "owner-pid").read_text())
            adapter = int((root / "adapter-pid").read_text())
            handles = []
            for pid in [*pids, owner, adapter]:
                handle = windows_job.checked(kernel.OpenProcess(0x101001, False, pid))
                stack.callback(windows_job.close_handle, kernel, handle)
                handles.append(handle)
            if mode == "adapter-killed":
                kernel.TerminateProcess.argtypes = [windows_job.w.HANDLE, windows_job.w.UINT]
                windows_job.checked(kernel.TerminateProcess(handles[-1], 9))
            else:
                facade.send_signal(signal.CTRL_BREAK_EVENT)
            if mode == "repeat":
                with listener.accept()[0] as ready:
                    ready.settimeout(15)
                    stream = ready.makefile("rb")
                    assert stream.readline() == b"teardown\n"
                    # The adapter records this before forwarding the first break, so the
                    # teardown handshake also guarantees publication and a live group.
                    owner_group = int((root / "owner-group-pid").read_text())
                    handle = windows_job.checked(kernel.OpenProcess(0x101001, False, owner_group))
                    stack.callback(windows_job.close_handle, kernel, handle)
                    handles.append(handle)
                    for _ in range(2):
                        os.kill(owner_group, signal.CTRL_BREAK_EVENT)
                        ready.sendall(b"x")
                        assert stream.readline() == b"ack\n"
            code = facade.wait(timeout=35)
            observed = {"exit": code, "pids": pids, "signaled": [kernel.WaitForSingleObject(h, 5000) == 0 for h in handles],
                        "foreign_alive": foreign.poll() is None, "campaign": None,
                        "facade": json.loads(next((root / "results").glob("*/result.json")).read_text())}
            receipt = root / "published/identities.json"
            if receipt.exists():
                observed["campaign"] = json.loads(receipt.read_text())["campaign"]
            (root / "observation.json").write_text(json.dumps(observed, indent=2))
        finally:
            run_tests.end_descendants(containment)
            facade.wait(timeout=5)
            foreign.wait(timeout=5)


if __name__ == "__main__":
    if sys.argv[1:2] == ["--probe"]:
        windows_probe(sys.argv[2], Path(sys.argv[3]))
    elif sys.argv[1:2] == ["--owner"]:
        raise SystemExit(windows_owner(sys.argv[2]))
    elif sys.argv[1:2] == ["--adapter"]:
        raise SystemExit(windows_adapter(sys.argv[2]))
    elif sys.argv[1:2] == ["--subject"]:
        windows_subject()
    else:
        unittest.main()

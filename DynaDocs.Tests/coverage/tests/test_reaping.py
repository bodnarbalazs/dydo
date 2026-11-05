"""A contained driver, as the Linux subreaper of its run, reaps every orphan it adopts."""
import json
import os
import subprocess
import sys
import tempfile
import threading
import unittest
from contextlib import contextmanager
from pathlib import Path
from unittest import mock

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import run_tests

SELF_PID = 4242
ORPHANS = 4
# How long the subject waits for its driver to reap the orphans it left, before it reports them.
REAP_WAIT_SECONDS = 5

# Leaves ORPHANS grandchildren that outlive their own parent and exit while the subject is still
# running, so each is adopted by the driver and exits there. Pipes order every step: no sleep
# decides an outcome. The subject then reports which exited orphans its driver has not reaped.
SUBJECT = r'''
import json, os, sys, time
report, orphans, wait_seconds = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
release_read, release_write = os.pipe()
exited_read, exited_write = os.pipe()
grandchildren = []
for _ in range(orphans):
    pid_read, pid_write = os.pipe()
    if os.fork() == 0:
        grandchild = os.fork()
        if grandchild == 0:
            os.close(release_write)
            os.read(release_read, 1)
            os._exit(0)
        os.write(pid_write, str(grandchild).encode())
        os._exit(0)
    os.close(pid_write)
    grandchildren.append(int(os.read(pid_read, 32)))
    os.close(pid_read)
    os.wait()
os.close(release_write)
os.close(exited_write)
assert os.read(exited_read, 1) == b""  # every orphan has exited
deadline = time.monotonic() + wait_seconds
while time.monotonic() < deadline and any(os.path.exists(f"/proc/{pid}") for pid in grandchildren):
    time.sleep(0.01)
unreaped = [pid for pid in grandchildren if os.path.exists(f"/proc/{pid}")]
with open(report, "w") as stream:
    json.dump({"driver": os.getppid(), "unreaped": unreaped}, stream)
raise SystemExit(7)
'''

# Runs one real driver path, with the real containment, as its own process, then lists the
# zombie children the driver still holds once that path has returned.
PROBE = r'''
import json, os, sys
from pathlib import Path
sys.path.insert(0, sys.argv[1])
import gate_adapter, run_tests
entry, folder, orphans, wait_seconds = sys.argv[2], Path(sys.argv[3]), sys.argv[4], sys.argv[5]
subject, report = folder / "subject.py", folder / "report.json"
command = [sys.executable, str(subject), str(report), orphans, wait_seconds]
if entry == "campaign":
    status = run_tests._run_linux_campaign(command, folder, folder / "owner")["subject_status"]
else:
    status = gate_adapter.run_coverage_command(command, folder)
zombies = [pid for pid, (parent, state) in run_tests._process_table().items()
           if parent == os.getpid() and state == "Z"]
observed = json.loads(report.read_text())
print(json.dumps({"status": status, "driver": os.getpid(), "subject_driver": observed["driver"],
                  "unreaped_during_run": observed["unreaped"], "zombies_after_return": zombies}))
'''


def probe(entry, orphans=ORPHANS, wait_seconds=REAP_WAIT_SECONDS):
    """Drive `entry` in a fresh process, so its subreaper state never reaches this one."""
    with tempfile.TemporaryDirectory() as folder:
        (Path(folder) / "subject.py").write_text(SUBJECT, encoding="utf-8")
        environment = {key: value for key, value in os.environ.items() if key != "DYDO_ROW_DEADLINE"}
        completed = subprocess.run([sys.executable, "-c", PROBE, str(TOOLS), entry, folder,
                                    str(orphans), str(wait_seconds)],
                                   capture_output=True, text=True, timeout=60, env=environment,
                                   check=False)
    if completed.returncode != 0:
        raise AssertionError(completed.stderr)
    return json.loads(completed.stdout)


@unittest.skipUnless(sys.platform == "linux", "Linux subreaper containment")
class AdoptedOrphanReapingTests(unittest.TestCase):
    def test_each_contained_driver_reaps_adopted_orphans_while_running_and_keeps_its_subject_status(self):
        for entry in ("campaign", "adapter"):
            with self.subTest(entry=entry):
                observed = probe(entry)
                self.assertEqual(observed["driver"], observed["subject_driver"], observed)
                self.assertEqual(7, observed["status"], observed)
                self.assertEqual([], observed["unreaped_during_run"], observed)
                self.assertEqual([], observed["zombies_after_return"], observed)

    def test_the_reaper_never_takes_an_owned_childs_status_and_reaps_every_other_exited_child(self):
        owned = subprocess.Popen([sys.executable, "-c", "raise SystemExit(7)"])
        other = subprocess.Popen([sys.executable, "-c", "raise SystemExit(3)"])
        for child in (owned, other):  # both are zombies, neither yet collected
            os.waitid(os.P_PID, child.pid, os.WEXITED | os.WNOWAIT)
        with mock.patch.object(run_tests, "_children", return_value={owned.pid, other.pid}):
            run_tests.reap_adopted(run_tests.SUBREAPER, owned=(owned.pid,))
        self.assertEqual(7, owned.wait())
        with self.assertRaises(ChildProcessError):
            os.waitid(os.P_PID, other.pid, os.WEXITED | os.WNOHANG)
        # Why `owned` must name every child still waited on: an unnamed one loses its status.
        self.assertEqual(0, other.wait())

    def test_the_reaper_tolerates_a_child_collected_since_the_listing(self):
        # pid 1 is never this process's child, so waiting on it fails as a collected one would.
        with mock.patch.object(run_tests, "_children", return_value={1}):
            run_tests.reap_adopted(run_tests.SUBREAPER)

    def test_a_contained_run_keeps_its_nonzero_status_while_the_reaper_is_active(self):
        completed = run_tests.run_contained(run_tests.SUBREAPER,
                                            [sys.executable, "-c", "raise SystemExit(7)"],
                                            stdin=subprocess.DEVNULL)
        self.assertEqual(7, completed.returncode)
        self.assertEqual([sys.executable, "-c", "raise SystemExit(7)"], completed.args)

    def test_children_are_this_process_s_own_children(self):
        child = subprocess.Popen([sys.executable, "-c", "import sys; sys.stdin.read()"],
                                 stdin=subprocess.PIPE)
        try:
            self.assertIn(child.pid, run_tests._children())
        finally:
            child.communicate()


def write_stat(proc, pid, state, parent):
    (proc / str(pid)).mkdir(parents=True, exist_ok=True)
    (proc / str(pid) / "stat").write_text(f"{pid} (name) {state} {parent} 0 0\n", encoding="utf-8")


class ReapingSeamTests(unittest.TestCase):
    def test_children_come_from_every_thread_s_children_list(self):
        with tempfile.TemporaryDirectory() as folder:
            proc = Path(folder)
            for task, children in ((str(SELF_PID), "11 12 "), ("4243", "13"), ("4244", "")):
                (proc / str(SELF_PID) / "task" / task).mkdir(parents=True)
                (proc / str(SELF_PID) / "task" / task / "children").write_text(children)
            with mock.patch.object(run_tests, "PROC_ROOT", proc), \
                    mock.patch.object(run_tests.os, "getpid", return_value=SELF_PID):
                self.assertEqual({11, 12, 13}, run_tests._children())

    def test_children_fall_back_to_the_process_table_without_a_children_list(self):
        with tempfile.TemporaryDirectory() as folder:
            proc = Path(folder)
            write_stat(proc, SELF_PID, "S", 1)
            write_stat(proc, 11, "Z", SELF_PID)
            write_stat(proc, 12, "S", SELF_PID)
            write_stat(proc, 13, "S", 11)
            with mock.patch.object(run_tests, "PROC_ROOT", proc), \
                    mock.patch.object(run_tests.os, "getpid", return_value=SELF_PID):
                self.assertEqual({11, 12}, run_tests._children())

    def test_reaping_needs_a_subreaper(self):
        with mock.patch.object(run_tests, "_children", side_effect=AssertionError("listed")):
            for job in (None, 42):
                run_tests.reap_adopted(job)

    def test_the_reaper_reaps_each_interval_until_stopped(self):
        stop = mock.Mock()
        stop.wait.side_effect = [False, False, True]
        with mock.patch.object(run_tests, "reap_adopted") as reap:
            run_tests._reap_until(stop, 42)
        self.assertEqual([mock.call(run_tests.SUBREAPER, owned=(42,))] * 2, reap.call_args_list)
        stop.wait.assert_called_with(run_tests.REAP_INTERVAL_SECONDS)

    def test_reaping_runs_the_reaper_beside_the_body_and_reaps_once_more_on_leaving(self):
        started, observed = threading.Event(), {}

        def reaper(stop, child):
            observed["child"] = child
            started.set()
            stop.wait()
            observed["stopped"] = stop.is_set()

        for failure in (None, KeyboardInterrupt):
            observed.clear()
            started.clear()
            with self.subTest(failure=failure), \
                    mock.patch.object(run_tests, "_reap_until", side_effect=reaper), \
                    mock.patch.object(run_tests, "reap_adopted") as reap:
                try:
                    with run_tests.reaping(run_tests.SUBREAPER, mock.Mock(pid=42)):
                        self.assertTrue(started.wait(10))
                        reap.assert_not_called()
                        if failure:
                            raise failure
                except KeyboardInterrupt:
                    pass
                self.assertEqual({"child": 42, "stopped": True}, observed)
                reap.assert_called_once_with(run_tests.SUBREAPER, owned=(42,))

    def test_reaping_without_a_subreaper_starts_no_reaper(self):
        with mock.patch.object(run_tests.threading, "Thread", side_effect=AssertionError("reaper")), \
                mock.patch.object(run_tests, "reap_adopted") as reap:
            for job in (None, 42):
                with run_tests.reaping(job, mock.Mock(pid=7)):
                    pass
        reap.assert_not_called()

    def test_a_run_without_a_subreaper_is_a_plain_run(self):
        completed = subprocess.CompletedProcess(["subject"], 5)
        with mock.patch.object(run_tests.subprocess, "run", return_value=completed) as run:
            for job in (None, 42):
                self.assertIs(completed, run_tests.run_contained(job, ["subject"], timeout=9, cwd="x"))
        run.assert_called_with(["subject"], timeout=9, check=False, cwd="x")

    def test_a_contained_run_ends_its_child_on_timeout_and_interruption(self):
        @contextmanager
        def no_reaper(_job, _child):
            yield

        for failure in (subprocess.TimeoutExpired(["subject"], 9), KeyboardInterrupt()):
            with self.subTest(failure=type(failure).__name__):
                popen = mock.MagicMock()
                child = popen.return_value.__enter__.return_value
                child.wait.side_effect = [failure, 0]
                with mock.patch.object(run_tests.subprocess, "Popen", popen), \
                        mock.patch.object(run_tests, "reaping", no_reaper), \
                        self.assertRaises(type(failure)):
                    run_tests.run_contained(run_tests.SUBREAPER, ["subject"], timeout=9, cwd="x")
                popen.assert_called_once_with(["subject"], cwd="x")
                child.kill.assert_called_once_with()
                expected = [mock.call(timeout=9), mock.call()]
                self.assertEqual(expected if isinstance(failure, subprocess.TimeoutExpired)
                                 else expected[:1], child.wait.call_args_list)


if __name__ == "__main__":
    unittest.main()

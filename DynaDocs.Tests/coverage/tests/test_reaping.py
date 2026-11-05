"""A contained driver, as the Linux subreaper of its run, reaps every orphan it adopts."""
import contextlib
import io
import json
import os
import shutil
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
import gate_adapter
import run_tests

SELF_PID = 4242
ORPHANS = 4
# How long the subject waits for its driver to reap the orphans it left, before it reports them.
REAP_WAIT_SECONDS = 5
# Reaper passes a held wait lets run after its child exited, so one full pass saw that child.
HELD_PASSES = 2

# Leaves `orphans` grandchildren that outlive their own parent and exit while the subject is still
# running, so each is adopted by the driver and exits there. With `leak`, it also leaves one more
# that never exits and has left its process group, so only the driver's cleanup ends it. Without
# a `ready` FIFO the subject releases its orphans at once and exits 7; with one, it reports ready
# there and releases them when the driver interrupts it, then exits 130. Pipes order every step:
# no sleep decides an outcome. The subject then reports which exited orphans its driver has not
# reaped.
SUBJECT = r'''
import json, os, signal, sys, time
report, orphans, wait_seconds = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
leak, ready = sys.argv[4] == "leak", sys.argv[5]
signal.alarm(60)  # no subject outlives a broken driver for long

def orphaned(body):
    """Run `body` in a grandchild whose own parent exits at once, and return its pid."""
    pid_read, pid_write = os.pipe()
    if os.fork() == 0:
        os.close(pid_read)
        grandchild = os.fork()
        if grandchild == 0:
            os.close(pid_write)
            body()
            os._exit(0)
        os.write(pid_write, str(grandchild).encode())
        os._exit(0)
    os.close(pid_write)
    pid = int(os.read(pid_read, 32))
    os.close(pid_read)
    os.wait()
    return pid

def lingering():
    os.setsid()
    devnull = os.open(os.devnull, os.O_RDWR)
    for stream in (0, 1, 2):
        os.dup2(devnull, stream)
    signal.alarm(60)
    while True:
        signal.pause()

leaked = orphaned(lingering) if leak else None
release_read, release_write = os.pipe()
exited_read, exited_write = os.pipe()

def awaiting_release():
    signal.signal(signal.SIGINT, signal.SIG_IGN)
    os.close(release_write)
    os.read(release_read, 1)

grandchildren = [orphaned(awaiting_release) for _ in range(orphans)]
os.close(exited_write)

def zombie_of_driver(pid):
    try:
        with open(f"/proc/{pid}/stat") as stream:
            state, parent = stream.read().rsplit(") ", 1)[1].split()[:2]
    except OSError:
        return False
    return state == "Z" and int(parent) == os.getppid()

def finish(status):
    os.close(release_write)
    assert os.read(exited_read, 1) == b""  # every orphan has exited
    deadline = time.monotonic() + wait_seconds
    while time.monotonic() < deadline and any(map(zombie_of_driver, grandchildren)):
        time.sleep(0.01)
    with open(report, "w") as stream:
        json.dump({"driver": os.getppid(), "leaked": leaked,
                   "unreaped": [pid for pid in grandchildren if zombie_of_driver(pid)]}, stream)
    raise SystemExit(status)

if ready == "-":
    finish(7)
signal.signal(signal.SIGINT, lambda _signum, _frame: finish(130))
with open(ready, "w") as stream:  # a FIFO: this blocks until the driver listens
    stream.write("ready")
while True:
    signal.pause()
'''

# Runs one real driver path, with the real containment, in a fresh process, so its subreaper state
# never reaches this one.
DRIVE = ("import sys; sys.path.insert(0, sys.argv[1]); import test_reaping; "
         "test_reaping.drive(*sys.argv[2:])")


@contextmanager
def held_status(command, passes=HELD_PASSES):
    """Hold the first wait on `command`'s child until the reaper has run `passes` more times after
    that child exited: a reaper that took its status would have done so by then, deterministically."""
    real_reap, real_wait = run_tests.reap_adopted, subprocess.Popen.wait
    reaped = threading.Condition()
    count = 0

    def counted_reap(job, owned=()):
        nonlocal count
        real_reap(job, owned=owned)
        with reaped:
            count += 1
            reaped.notify_all()

    def held_wait(child, timeout=None):
        if child.args == command and child.returncode is None:
            try:
                os.waitid(os.P_PID, child.pid, os.WEXITED | os.WNOWAIT)  # exited, not collected
            except ChildProcessError:
                pass  # already taken: the wait below reports what Popen makes of that
            with reaped:
                target = count + passes
                if not reaped.wait_for(lambda: count >= target, timeout=REAP_WAIT_SECONDS):
                    raise AssertionError("no reaper ran while the wait was held")
        return real_wait(child, timeout=timeout)

    with mock.patch.object(run_tests, "reap_adopted", counted_reap), \
            mock.patch.object(subprocess.Popen, "wait", held_wait):
        yield


def _campaign(command, folder):
    return run_tests._run_linux_campaign(command, folder, folder / "owner")["subject_status"]


def _adapter(command, folder):
    return gate_adapter.run_coverage_command(command, folder)


def _cancelled_adapter(command, folder):
    """The adapter's row deadline passes once the subject is ready, so cancellation stops it."""
    real_wait, waits = gate_adapter._wait_coverage_child, []

    def deadline_passes_once_ready(child, deadline):
        if waits:
            return real_wait(child, deadline)
        waits.append(child)
        (folder / "ready").read_text(encoding="utf-8")  # returns once the subject is ready
        raise subprocess.TimeoutExpired(child.args, 0)

    with mock.patch.object(gate_adapter, "_wait_coverage_child", deadline_passes_once_ready):
        try:
            gate_adapter.run_coverage_command(command, folder)
        except gate_adapter.MeasurementTimeout as error:
            return type(error).__name__
    raise AssertionError("the adapter was not cancelled")


def _plain(command, folder):
    """run_tests.main's plain dotnet run, with the worktree's git steps stubbed out."""
    with mock.patch.object(run_tests, "prune_stale_test_worktrees"), \
            mock.patch.object(run_tests, "is_registered_worktree", return_value=False), \
            mock.patch.object(run_tests, "create_worktree", return_value=True), \
            mock.patch.object(run_tests, "copy_dirty_files"), \
            mock.patch.object(run_tests, "materialize_skill_links"), \
            mock.patch.object(run_tests, "remove_worktree", side_effect=shutil.rmtree), \
            mock.patch.object(run_tests, "test_command", return_value=command), \
            mock.patch.object(sys, "argv", ["run_tests.py"]), \
            contextlib.redirect_stdout(io.StringIO()):
        try:
            run_tests.main()
        except SystemExit as exited:
            return exited.code
    raise AssertionError("run_tests.main did not exit")


DRIVERS = {"campaign": _campaign, "adapter": _adapter, "cancel": _cancelled_adapter, "plain": _plain}


def drive(entry, folder, orphans, leak):
    """Run driver `entry` on the subject in `folder` and print what was left unreaped."""
    folder = Path(folder)
    report, ready = folder / "report.json", folder / "ready"
    if entry == "cancel":
        os.mkfifo(ready)
    command = [sys.executable, str(folder / "subject.py"), str(report), orphans,
               str(REAP_WAIT_SECONDS), leak, str(ready) if entry == "cancel" else "-"]
    with contextlib.ExitStack() as stack:
        if leak == "leak":
            # No reaper pass runs, so only the reaps after cleanup can collect what cleanup killed.
            stack.enter_context(mock.patch.object(run_tests, "REAP_INTERVAL_SECONDS", 3600))
        elif entry != "cancel":
            stack.enter_context(held_status(command))
        status = DRIVERS[entry](command, folder)
    zombies = [pid for pid, (parent, state) in run_tests._process_table().items()
               if parent == os.getpid() and state == "Z"]
    observed = json.loads(report.read_text(encoding="utf-8"))
    print(json.dumps({"status": status, "driver": os.getpid(), "subject_driver": observed["driver"],
                      "unreaped_during_run": observed["unreaped"], "leaked": observed["leaked"],
                      "zombies_after_return": zombies,
                      "live_after_return": run_tests._live_descendants(os.getpid())}))


def probe(entry, orphans=ORPHANS, leak="none"):
    """Drive `entry` in a fresh process and return what it observed."""
    with tempfile.TemporaryDirectory() as folder:
        (Path(folder) / "subject.py").write_text(SUBJECT, encoding="utf-8")
        environment = {key: value for key, value in os.environ.items()
                       if key != gate_adapter.ROW_DEADLINE_ENV}
        completed = subprocess.run([sys.executable, "-c", DRIVE, str(Path(__file__).parent), entry,
                                    folder, str(orphans), leak],
                                   capture_output=True, text=True, timeout=60, env=environment,
                                   check=False)
    if completed.returncode != 0:
        raise AssertionError(completed.stderr)
    return json.loads(completed.stdout.splitlines()[-1])


@unittest.skipUnless(sys.platform == "linux", "Linux subreaper containment")
class AdoptedOrphanReapingTests(unittest.TestCase):
    def test_each_contained_driver_reaps_adopted_orphans_while_running_and_keeps_its_subject_status(self):
        for entry in ("campaign", "adapter", "plain"):
            with self.subTest(entry=entry):
                observed = probe(entry)
                self.assertEqual(observed["driver"], observed["subject_driver"], observed)
                self.assertEqual(7, observed["status"], observed)
                self.assertEqual([], observed["unreaped_during_run"], observed)
                self.assertEqual([], observed["zombies_after_return"], observed)

    def test_an_adapter_cancellation_reaps_adopted_orphans_while_it_stops_its_subject(self):
        observed = probe("cancel")
        self.assertEqual(observed["driver"], observed["subject_driver"], observed)
        self.assertEqual("MeasurementTimeout", observed["status"], observed)
        self.assertEqual([], observed["unreaped_during_run"], observed)
        self.assertEqual([], observed["zombies_after_return"], observed)

    def test_each_driver_reaps_the_leaked_descendant_its_cleanup_kills(self):
        for entry in ("campaign", "plain", "cancel"):
            with self.subTest(entry=entry):
                observed = probe(entry, orphans=0, leak="leak")
                self.assertIsInstance(observed["leaked"], int, observed)
                self.assertEqual([], observed["live_after_return"], observed)
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

    def test_a_contained_run_keeps_its_status_while_reaper_passes_follow_its_exit(self):
        command = [sys.executable, "-c", "raise SystemExit(7)"]
        with held_status(command):
            completed = run_tests.run_contained(run_tests.SUBREAPER, command, stdin=subprocess.DEVNULL)
        self.assertEqual(7, completed.returncode)
        self.assertEqual(command, completed.args)

    def test_a_contained_run_that_cannot_start_its_reaper_still_waits_and_reaps_on_leaving(self):
        command = [sys.executable, "-c", "raise SystemExit(7)"]
        with mock.patch.object(run_tests.threading.Thread, "start",
                               side_effect=RuntimeError("can't start new thread")), \
                mock.patch.object(run_tests, "reap_adopted", wraps=run_tests.reap_adopted) as reap:
            completed = run_tests.run_contained(run_tests.SUBREAPER, command, timeout=60,
                                                stdin=subprocess.DEVNULL)
        self.assertEqual(7, completed.returncode)
        self.assertEqual(1, reap.call_count)

    def test_an_adapter_that_cannot_start_its_reaper_still_waits_on_its_child(self):
        with mock.patch.object(run_tests, "contain_descendants", return_value=run_tests.SUBREAPER), \
                mock.patch.object(run_tests.threading.Thread, "start",
                                  side_effect=RuntimeError("can't start new thread")), \
                mock.patch.dict(os.environ, clear=False) as environment:
            environment.pop(gate_adapter.ROW_DEADLINE_ENV, None)
            status = gate_adapter.run_coverage_command([sys.executable, "-c", "raise SystemExit(7)"],
                                                       Path.cwd())
        self.assertEqual(7, status)

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

    def test_reaping_that_cannot_start_its_reaper_runs_the_body_and_reaps_on_leaving(self):
        thread = mock.Mock()
        thread.return_value.start.side_effect = RuntimeError("can't start new thread")
        with mock.patch.object(run_tests.threading, "Thread", thread), \
                mock.patch.object(run_tests, "reap_adopted") as reap:
            with run_tests.reaping(run_tests.SUBREAPER, mock.Mock(pid=42)):
                reap.assert_not_called()
        thread.return_value.join.assert_not_called()
        reap.assert_called_once_with(run_tests.SUBREAPER, owned=(42,))

    def test_an_interrupt_during_the_reaper_join_retries_it_reaps_and_is_then_delivered(self):
        steps = mock.Mock()
        thread = steps.Thread
        thread.return_value.join.side_effect = [KeyboardInterrupt, None]
        with mock.patch.object(run_tests.threading, "Thread", thread), \
                mock.patch.object(run_tests, "reap_adopted", steps.reap_adopted), \
                self.assertRaises(KeyboardInterrupt):
            with run_tests.reaping(run_tests.SUBREAPER, mock.Mock(pid=42)):
                pass
        self.assertEqual([mock.call.Thread().join(), mock.call.Thread().join(),
                          mock.call.reap_adopted(run_tests.SUBREAPER, owned=(42,))],
                         [step for step in steps.mock_calls
                          if step[0] in ("Thread().join", "reap_adopted")])

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

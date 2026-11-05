"""A contained driver, as the Linux subreaper of its run, reaps every orphan it adopts."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
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


if __name__ == "__main__":
    unittest.main()

"""Ordinary coverage.py collection includes child-only and never-called callables."""
import contextlib
import itertools
import json
import os
import socket
import subprocess
import sys
import tempfile
import threading
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_run import STATIC_GATE_INTERPRETER
import python_coverage
from python_coverage import combine_counters


def _facade(root, output, sources, command, timeout=None):
    tools = Path(__file__).resolve().parents[1]
    python = tools.parents[1] / STATIC_GATE_INTERPRETER
    return subprocess.run([
        str(python), str(tools / "python_coverage.py"), "--root", str(root),
        "--output", str(output), "--sources-json", json.dumps(sources),
        "--command-json", json.dumps(command),
    ], cwd=root, text=True, capture_output=True, timeout=timeout)


def _release(listener):
    """Let the one child waiting on `listener` exit, and return once it has."""
    held, _ = listener.accept()
    with held, contextlib.suppress(ConnectionResetError):  # Windows may reset what an exiting process left open
        held.sendall(b"!")
        held.recv(1)


def _counter_row(**changes):
    return {"id": "f:1:0", "path": "a.py", "line": 1, "column": 0, "end_line": 1,
            "end_column": 18, "kind": "FunctionDef", "execution_count": 1,
            "body_lines": {"1": 1}, "branches": [], "physical_branches": [], **changes}


def _counter_receipt(rows, **changes):
    return {"schema": 1, "sources": {"a.py": "a" * 64}, "callables": rows, "modules": [], **changes}


_COLD_BOOTSTRAP = """\
import importlib.util
import json
import os
import socket
import sys
from pathlib import Path

tools, work = (Path(argument).resolve() for argument in sys.argv[1:3])
sys.path[:0] = [str(tools), str(work)]
inherited = os.environ.get("DYDO_PYTHON_COVERAGE_CONFIG")
for cached in ("python_runtime", "positions"):
    sys.modules.pop(cached, None)
spec = importlib.util.spec_from_file_location("cold_bootstrap", tools / "python_coverage.py")
bootstrap = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bootstrap)

# The deliberate refusal below is no measurement failure of an inherited campaign.
refusals = socket.create_server(("127.0.0.1", 0))
os.environ["DYDO_PYTHON_COVERAGE_FAILURES"] = str(refusals.getsockname()[1])
os.environ["DYDO_PYTHON_COVERAGE_CONFIG"] = str(work / "unpinned.json")
refusal = ""
try:
    bootstrap.startup_from_environment()
except RuntimeError as error:
    refusal = str(error)
os.environ["DYDO_PYTHON_COVERAGE_CONFIG"] = str(work / "config.json")
session = bootstrap.startup_from_environment()
restarted = session is not bootstrap.startup_from_environment()

import subject

subject.only()
(work / "observed.json").write_text(json.dumps({
    "pid": os.getpid(), "inherited": inherited, "refusal": refusal,
    "started": session is not None, "restarted": restarted,
    "runtime": sys.modules["python_runtime"].__spec__.origin,
    "positions": sys.modules["positions"].__spec__.origin}), encoding="utf-8")
"""


_ASSERTING_SUBJECT = 'def work(value=True):\n    """Docstring."""\n    assert value\n    return 1\n'


def _children_driver(*flag_sets):
    """An unmeasured (-S) command that runs one measured child per interpreter flag set."""
    return "import subprocess, sys\n" + "".join(
        f"subprocess.run([sys.executable, *{flags!r}, '-c', 'import subject; subject.work()'], check=True)\n"
        for flags in flag_sets)


_DAMAGE_CACHE = """\
import json, os, pickle, subprocess, sys
from pathlib import Path
from python_runtime import _seal, _unsealed

config_path = Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"])
config = json.loads(config_path.read_text(encoding="utf-8"))
cache = Path(config["inventory"])
case = sys.argv[1]
if case == "no cache":
    del config["inventory"]
    config_path.write_text(json.dumps(config), encoding="utf-8")
elif case == "missing":
    cache.unlink()
elif case == "unreadable":
    cache.unlink()
    cache.mkdir()
elif case == "corrupt":
    cache.write_bytes(b"not a pickle")
elif case == "truncated":
    cache.write_bytes(cache.read_bytes()[:-10])
elif case == "wrong structure":
    payload = _unsealed(cache.read_bytes())
    payload["sources"] = {path: (digest, inventory[:2])
                          for path, (digest, inventory) in payload["sources"].items()}
    cache.write_bytes(_seal(pickle.dumps(payload)))
elif case == "foreign schema":
    cache.write_bytes(_seal(pickle.dumps({"schema": "foreign"})))
subprocess.run([sys.executable, "-c", "import subject; subject.work()"], check=True)
"""


_TWO_CALLABLES = ("def work(flag=True):\n    if flag:\n        return list(item for item in (1, 2) if item)\n"
                  "    return []\n\n\ndef helper():\n    return 2\n")


# Each case leaves a cache that is sealed and unpickles, but whose subject.py entry is internally
# inconsistent: a key, index, owner or id that the writer can never produce.
_INCONSISTENT_CACHE = """\
import json, os, pickle, subprocess, sys
from pathlib import Path
from python_runtime import _seal, _unsealed

config_path = Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"])
config = json.loads(config_path.read_text(encoding="utf-8"))
cache = Path(config["inventory"])
payload = _unsealed(cache.read_bytes())
entries = {Path(path).name: inventory for path, (_, inventory) in payload["sources"].items()}
rows, module, index = entries["subject.py"]
work, helper = rows
elsewhere = entries["other.py"][0][0]
qualname, line, points = work["_key"]
damage = {
    "empty index": index.clear,
    "index missing a callable key": lambda: index.pop(work["_key"]),
    "index missing the module key": lambda: index.pop(module["_key"]),
    "index key at a sibling row": lambda: index.update({work["_key"]: helper}),
    "index key at another source's row": lambda: index.update({work["_key"]: elsewhere}),
    "row key absent": lambda: work.update(_key=None),
    "row key a list": lambda: work.update(_key=[qualname, line, points]),
    "row key line not an int": lambda: work.update(_key=(qualname, str(line), points)),
    "row key positions a list": lambda: work.update(_key=(qualname, line, list(points))),
    "row key position short": lambda: work.update(_key=(qualname, line, tuple(p[:3] for p in points))),
    "module key absent": lambda: module.update(_key=None),
    "row id not a string": lambda: work.update(id=[]),
    "row id of another form": lambda: work.update(id="work:9:0"),
    "two rows with one id": lambda: helper.update(id=work["id"]),
    "row listed twice": lambda: rows.append(work),
    "body line keys integers": lambda: work.update(body_lines={int(line): 0 for line in work["body_lines"]}),
    "body line missing": lambda: work["body_lines"].pop("2"),
    "body line in another form": lambda: work["body_lines"].update({"02": 0}),
    "body line no code implies": lambda: work["body_lines"].update({"9": 0}),
}
case = sys.argv[1]
if case == "no cache":
    del config["inventory"]
    config_path.write_text(json.dumps(config), encoding="utf-8")
elif case == "byte damage that still unpickles":
    body = pickle.dumps(payload)
    work["end_column"] += 1
    damaged = pickle.dumps(payload)
    assert len(body) == len(damaged) and sum(a != b for a, b in zip(body, damaged)) == 1
    cache.write_bytes(_seal(body)[:-len(body)] + damaged)
else:
    damage[case]()
    cache.write_bytes(_seal(pickle.dumps(payload)))
subprocess.run([sys.executable, "-c", "import subject; subject.work(); subject.helper()"], check=True)
"""


# The facade with a writer bug forced into its serializer: the sealed inventory differs from the one computed.
_MISMATCHED_WRITER = """\
import copy, pickle, sys, types
tools, case = sys.argv.pop(1), sys.argv.pop(1)
sys.path.insert(0, tools)
import python_coverage, python_runtime


def mismatched(payload):
    payload = copy.deepcopy(payload)
    (rows, _, _), = (inventory for _, inventory in payload["sources"].values())
    work = rows[0]
    {"body line dropped": lambda: work["body_lines"].pop("2"),
     "body line added": lambda: work["body_lines"].update({"9": 0}),
     "end column shifted": lambda: work.update(end_column=work["end_column"] + 1),
     "end line a float": lambda: work.update(end_line=float(work["end_line"]))}[case]()
    return pickle.dumps(payload)


if case not in ("no cache", "faithful"):
    python_runtime.pickle = types.SimpleNamespace(dumps=mismatched, loads=pickle.loads)
raise SystemExit(python_coverage.main())
"""


_UNCACHED_OR_AS_WRITTEN = """\
import json, os, subprocess, sys
from pathlib import Path

if sys.argv[1] == "no cache":
    config_path = Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"])
    config = json.loads(config_path.read_text(encoding="utf-8"))
    del config["inventory"]
    config_path.write_text(json.dumps(config), encoding="utf-8")
subprocess.run([sys.executable, "-c", "import subject; subject.work(); subject.helper()"], check=True)
"""


def _measured_as_itself(root, output, case=None):
    """Give the one measured child whose failure a test names a sitecustomize of its own in `root/own-pid`: it
    writes the pid that child runs as to `root/failed.pid` and reads in `startup`, the sitecustomize `collect`
    wrote for the campaign in `output`, as it stands. Then it runs `startup`, or `case`: setup that runs it.

    A measured process names itself by that pid, and `Popen.pid` is not always it: on Windows a venv's
    python.exe is a redirector that runs the base interpreter as a child process of its own."""
    generated = str(output / "startup" / "sitecustomize.py")
    (root / "own-pid").mkdir()
    (root / "own-pid" / "sitecustomize.py").write_text(
        f"import os\nwith open({str(root / 'failed.pid')!r}, 'x', encoding='utf-8') as own:\n"
        f"    own.write(str(os.getpid()))\nwith open({generated!r}, encoding='utf-8') as generated:\n"
        f"    startup = compile(generated.read(), generated.name, 'exec')\n{case or _START}", encoding="utf-8")


# Measured children around one that fails measurement: at startup on a config of another schema, or
# at finish on a callable the inventory cannot name. Either way the failing child itself exits 0.
_FAILING_CHILD = """\
import os, subprocess, sys
from pathlib import Path

good = "import subject; subject.work()"
ghost = good + "; exec(compile('def ghost():\\\\n    return 0\\\\nghost()\\\\n', subject.work.__code__.co_filename, 'exec'))"
subprocess.run([sys.executable, "-c", good], check=True)
if sys.argv[1] == "startup":
    Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"]).write_text('{"schema": 2}', encoding="utf-8")
own_pid = {**os.environ, "PYTHONPATH": os.pathsep.join([str(Path("own-pid").resolve()), os.environ["PYTHONPATH"]])}
child = subprocess.Popen([sys.executable, "-c", good if sys.argv[1] == "startup" else ghost], env=own_pid)
assert child.wait() == 0
"""


# The same, but no file can be written beside the failing child's config and the driver ignores that
# child's exit: at startup its config lies in a directory that does not exist; at finish a file stands in
# for the campaign's output from just before it exits until the driver, exiting 0 itself, restores it.
_UNRECORDABLE_AND_IGNORED = """\
import os, subprocess, sys
from pathlib import Path

evidence = Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"]).parent
away = evidence.with_name("away")
good = "import subject; subject.work()"
subprocess.run([sys.executable, "-c", good], check=True)
own_pid = {**os.environ, "PYTHONPATH": os.pathsep.join([str(Path("own-pid").resolve()), os.environ["PYTHONPATH"]])}
if sys.argv[1] == "startup":
    gone = {**own_pid, "DYDO_PYTHON_COVERAGE_CONFIG": str(evidence / "gone" / "config.json")}
    child = subprocess.Popen([sys.executable, "-c", good], env=gone)
else:
    hide = f"import os; os.rename({str(evidence)!r}, {str(away)!r}); open({str(evidence)!r}, 'x').close()"
    child = subprocess.Popen([sys.executable, "-c", f"{good}; {hide}"], env=own_pid)
child.wait()
if away.exists():
    evidence.unlink()
    away.rename(evidence)
"""


# A process measured through a real sitecustomize whose failure record cannot be sent: at startup it
# finds no port in a config that does not exist; at finish, on a ghost callable, its connection to a
# listener that never reads refuses every send.
_UNRECORDABLE = """\
import socket, sys

if sys.argv[1] == "finish":
    import subject
    exec(compile("def ghost():\\n    return 0\\nghost()\\n", subject.work.__code__.co_filename, "exec"))

    def unsent(connection, data):
        raise BrokenPipeError("the campaign stopped listening")

    socket.socket.sendall = unsent
print("ran", flush=True)
"""


# Measured children around one whose measurement fails and whose exit the driver ignores: at finish with
# every descriptor in use, or with the port variable dropped and the config kept, at startup on a source
# it cannot read or at finish on a callable the inventory cannot name.
_IGNORED_FAILURE = """\
import os, subprocess, sys
from pathlib import Path

case = sys.argv[1]
subprocess.run([sys.executable, "-c", "import subject; subject.work()"], check=True)
env = {**os.environ, "PYTHONPATH": os.pathsep.join([str(Path("own-pid").resolve()), os.environ["PYTHONPATH"]])}
if case.startswith("port variable dropped"):
    del env["DYDO_PYTHON_COVERAGE_FAILURES"]
if case.endswith("startup"):
    Path("subject.py").rename("subject.away")
script = {"descriptors exhausted at finish": ["exhaust.py"], "port variable dropped at startup": ["-c", "pass"],
          "port variable dropped at finish": ["ghost.py"]}[case]
child = subprocess.Popen([sys.executable, *script], env=env)
child.wait()
if case.endswith("startup"):
    Path("subject.away").rename("subject.py")
"""


_EXHAUST = """\
import contextlib, os, resource, subject

subject.work()
resource.setrlimit(resource.RLIMIT_NOFILE, (64, 64))
held = []
with contextlib.suppress(OSError):
    while True:
        held.append(open(os.devnull))
"""


_GHOST = ("import subject\nsubject.work()\n"
          "exec(compile('def ghost():\\n    return 0\\nghost()\\n', subject.work.__code__.co_filename, 'exec'))\n")


# A measured child after another, whose reporting connection is hard to open and whose exit the driver
# ignores. Its own sitecustomize, found ahead of the campaign's, sets up the case before running the
# campaign's; its program leaves a marker once it runs.
_UNCONNECTED_AND_IGNORED = """\
import os, subprocess, sys
from pathlib import Path

subprocess.run([sys.executable, "-c", "import subject; subject.work()"], check=True)
env = {**os.environ, "PYTHONPATH": os.pathsep.join([str(Path("own-pid").resolve()), os.environ["PYTHONPATH"]])}
child = subprocess.Popen([sys.executable, "-c", "open('ran', 'x').close(); import subject; subject.work()"], env=env)
Path("failed.exit").write_text(str(child.wait()), encoding="utf-8")
"""


# A measured child after another, whose exit the driver ignores, started through the campaign's sitecustomize
# from a directory of its test's, and whose `python_coverage` cannot be imported: one earlier on its path
# raises, or none is on it. Without a campaign config, the raising one stays on its path.
_UNIMPORTABLE_AND_IGNORED = """\
import json, os, subprocess, sys
from pathlib import Path

case = sys.argv[1]
subprocess.run([sys.executable, "-c", "import subject; subject.work()"], check=True)
_, tools, *rest = os.environ["PYTHONPATH"].split(os.pathsep)
startup = str(Path("own-pid").resolve())
env = {**os.environ, "PYTHONPATH": os.pathsep.join([startup, str(Path("poisoned").resolve()), tools, *rest])}
if case == "python_coverage missing":
    env["PYTHONPATH"] = startup
if case == "no campaign config":
    del env["DYDO_PYTHON_COVERAGE_CONFIG"]
child = subprocess.Popen([sys.executable, "-c", "open('ran', 'x').close(); import subject; subject.work()"],
                         env=env, stderr=subprocess.PIPE, text=True)
_, stderr = child.communicate()
Path("child.json").write_text(json.dumps({"exit": child.returncode, "stderr": stderr}))
"""


_START = "exec(startup)\n"


_CONNECT_FAILING_ONCE = """\
import errno, socket
connect = socket.socket.connect


def fail_once(connection, address):
    socket.socket.connect = connect
    raise OSError(errno.EADDRNOTAVAIL, "no ephemeral port free yet")


socket.socket.connect = fail_once
"""


_UNCONNECTED = {
    # Only the reporting socket is left to allocate once every descriptor is in use.
    "descriptors exhausted": """\
import contextlib, os, resource, socket, python_coverage
resource.setrlimit(resource.RLIMIT_NOFILE, (64, 64))
held = []
with contextlib.suppress(OSError):
    while True:
        held.append(os.open(os.devnull, os.O_RDONLY))
try:
    exec(startup)
finally:  # a program that startup lets run may open files again
    for descriptor in held:
        os.close(descriptor)
""",
    # A port bound, so no other process takes it, but never listened on.
    "connect refused throughout": """\
import os, socket
unheard = socket.socket()
unheard.bind(("127.0.0.1", 0))
os.environ["DYDO_PYTHON_COVERAGE_FAILURES"] = str(unheard.getsockname()[1])
""" + _START,
    "connect failing once": _CONNECT_FAILING_ONCE + _START,
    "connect failing once, then startup failing": _CONNECT_FAILING_ONCE + """\
import json, os
from pathlib import Path
config = Path(os.environ["DYDO_PYTHON_COVERAGE_CONFIG"])
config.write_text(json.dumps({**json.loads(config.read_text(encoding="utf-8")), "schema": 2}), encoding="utf-8")
""" + _START,
}


# A child of the command still running when the command returns: measured, or unmeasured and holding a
# record it never finished on a connection of its own to `collect`; or measured and killed by the command
# first. Ready, it tells the command the pid it runs as, then stays until the test releases it on a loopback
# port of the test's own, outside the campaign.
_OUTLIVING = """\
import os, socket, sys

case, release = sys.argv[1], int(sys.argv[2])
if case == "partial record held open":
    record = socket.create_connection(("127.0.0.1", int(os.environ["DYDO_PYTHON_COVERAGE_FAILURES"])))
    record.sendall(b'"Python measurement failed')
held = socket.create_connection(("127.0.0.1", release))
print("ready", os.getpid(), flush=True)
held.recv(1)
"""


_OUTLIVED = """\
import subprocess, sys
from pathlib import Path

case = sys.argv[1]
subprocess.run([sys.executable, "-c", "import subject; subject.work()"], check=True)
flags = ["-S"] if case == "partial record held open" else []
child = subprocess.Popen([sys.executable, *flags, str(Path("outliving.py").resolve()), *sys.argv[1:]],
                         cwd=Path.cwd().parent, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                         stderr=subprocess.DEVNULL)
ready, pid = child.stdout.readline().split()
assert ready == b"ready"
Path("outliving.pid").write_bytes(pid)
if case == "killed":
    child.kill()
    child.wait()
else:
    Path("outliving.ready").touch()
"""


class PythonCoverageTests(unittest.TestCase):
    def campaign(self, root, output, sources, command):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / STATIC_GATE_INTERPRETER
        result = subprocess.run([
            str(python), str(tools / "python_coverage.py"),
            "--root", str(root), "--output", str(output),
            "--sources-json", json.dumps(sources), "--command-json", json.dumps(command),
        ], cwd=root, text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return json.loads((output / "counters.json").read_text())

    def test_project_facade_tests_execute_the_canonical_source_identity(self):
        root = Path(__file__).resolve().parents[3]
        source = "DynaDocs.Tests/coverage/gap_check.py"
        test = "DynaDocs.Tests/coverage/tests/test_testing_facade.py"
        with tempfile.TemporaryDirectory() as folder:
            counters = self.campaign(root, Path(folder) / "evidence", [source],
                                     ["{python}", test, "TestingFacadeTests.test_help"])
        main = next(row for row in counters["callables"] if row["id"].startswith("main:"))
        self.assertGreater(main["execution_count"], 0)

    def test_flat_counter_union_requires_identical_complete_inventory(self):
        row = {"schema": 1, "sources": {"a.py": "a" * 64}, "callables": [{
            "id": "f:1:0", "path": "a.py", "line": 1, "column": 0,
            "end_line": 1, "end_column": 18, "kind": "FunctionDef",
            "execution_count": 1, "body_lines": {"1": 1}, "branches": [],
            "physical_branches": []}], "modules": []}
        merged = combine_counters([row, row])
        self.assertEqual(2, merged["callables"][0]["execution_count"])
        changed = json.loads(json.dumps(row))
        changed["callables"] = []
        with self.assertRaisesRegex(ValueError, "inventory"):
            combine_counters([row, changed])

    def test_real_unittest_and_python_child_share_native_campaign(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / STATIC_GATE_INTERPRETER
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text(
                "def direct(): return 1\ndef child_only(): return 2\ndef never(): return 3\n"
                "if __name__ == '__main__': child_only()\n", encoding="utf-8")
            (root / "test_subject.py").write_text(
                "import subprocess,sys,unittest,subject\nclass T(unittest.TestCase):\n"
                " def test_direct_and_child(self):\n  self.assertEqual(1,subject.direct())\n"
                "  subprocess.run([sys.executable,'subject.py'],check=True)\n", encoding="utf-8")
            output = root / "evidence"
            counters = self.campaign(root, output, ["subject.py"],
                                     ["{python}", "-m", "unittest", "discover", "-s", "."])
            calls = {row["id"].split(":", 1)[0]: row["execution_count"]
                     for row in counters["callables"]}
            self.assertGreater(calls["direct"], 0)
            self.assertGreater(calls["child_only"], 0)
            self.assertEqual(0, calls["never"])
            native = json.loads((output / "coverage.json").read_text())
            self.assertTrue(native["meta"]["branch_coverage"])
            joined = subprocess.run([
                str(python), str(tools / "python_join.py"), "--root", str(root),
                "--output", str(output), "--sources-json", '["subject.py"]'],
                cwd=root, text=True, capture_output=True)
            self.assertEqual(0, joined.returncode, joined.stderr)
            module = json.loads(joined.stdout)[0]
            never = next(row for row in module["methods"] if row["id"].startswith("never:"))
            self.assertEqual((0, 0), (never["execution_count"], never["covered"]))
            self.assertTrue(module["lines"])

            def rejoin():
                return subprocess.run([
                    str(python), str(tools / "python_join.py"), "--root", str(root),
                    "--output", str(output), "--sources-json", '["subject.py"]'],
                    cwd=root, text=True, capture_output=True)

            original = (root / "subject.py").read_text(encoding="utf-8")
            (root / "subject.py").write_text(original.replace("return 3", "return 4"), encoding="utf-8")
            stale = rejoin()
            self.assertEqual(2, stale.returncode, stale.stdout)
            self.assertIn("source/counter hash mismatch", stale.stderr)

            (root / "subject.py").write_text(original, encoding="utf-8")
            counters = json.loads((output / "counters.json").read_text(encoding="utf-8"))
            counters["callables"][0]["body_lines"] = {"999": 0}
            (output / "counters.json").write_text(json.dumps(counters), encoding="utf-8")
            substituted = rejoin()
            self.assertEqual(2, substituted.returncode, substituted.stdout)
            self.assertIn("Mismatched Python callable body inventory", substituted.stderr)

    def test_a_source_edited_after_the_campaign_inventory_is_counted_from_its_own_text(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (root / "edit.py").write_text(
                "import subprocess, sys\nfrom pathlib import Path\n"
                "Path('subject.py').write_text("
                "'def work():\\n    return 1 + 1\\ndef added():\\n    return 2\\n', encoding='utf-8')\n"
                "subprocess.run([sys.executable, '-c', 'import subject; subject.work()'], check=True)\n",
                encoding="utf-8")
            output = root / "evidence"
            counters = self.campaign(root, output, ["subject.py"], ["{python}", "-S", "edit.py"])
            config = json.loads((output / "config.json").read_text(encoding="utf-8"))
            self.assertTrue(Path(config["inventory"]).is_file())
        self.assertEqual({"work:1:0": 1, "added:3:0": 0},
                         {row["id"]: row["execution_count"] for row in counters["callables"]})

    def test_children_under_different_compile_states_are_refused_never_lost(self):
        for flags in (["-O"], ["-OO"]):
            with self.subTest(flags=flags), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                (root / "subject.py").write_text(_ASSERTING_SUBJECT, encoding="utf-8")
                (root / "driver.py").write_text(_children_driver([], flags), encoding="utf-8")
                output = root / "evidence"
                result = _facade(root, output, ["subject.py"], ["{python}", "-S", "driver.py"])
                receipts = len(list(output.glob("counter-*.json")))
                self.assertEqual((2, 2), (result.returncode, receipts), result.stdout + result.stderr)
                self.assertIn("Python counter body inventory mismatch", result.stderr)

    def test_children_sharing_another_compile_state_are_counted_from_it(self):
        for flags in (["-O"], ["-OO"]):
            with self.subTest(flags=flags), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                (root / "subject.py").write_text(_ASSERTING_SUBJECT, encoding="utf-8")
                (root / "driver.py").write_text(_children_driver(flags, flags), encoding="utf-8")
                counters = self.campaign(root, root / "evidence", ["subject.py"],
                                         ["{python}", "-S", "driver.py"])
                self.assertEqual([("work:1:0", 2, {"4": 2})],
                                 [(row["id"], row["execution_count"], row["body_lines"])
                                  for row in counters["callables"]])

    def test_an_unusable_inventory_cache_is_measured_as_if_there_were_none(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text(_ASSERTING_SUBJECT, encoding="utf-8")
            (root / "damage.py").write_text(_DAMAGE_CACHE, encoding="utf-8")

            def measured(case):
                return self.campaign(root, root / f"evidence-{case.replace(' ', '-')}", ["subject.py"],
                                     ["{python}", "-S", "damage.py", case])

            expected = measured("no cache")
            self.assertEqual([("work:1:0", 1)],
                             [(row["id"], row["execution_count"]) for row in expected["callables"]])
            for case in ("missing", "unreadable", "corrupt", "truncated", "wrong structure", "foreign schema"):
                with self.subTest(cache=case):
                    self.assertEqual(expected, measured(case))

    def test_an_inconsistent_inventory_cache_is_measured_as_if_there_were_none(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text(_TWO_CALLABLES, encoding="utf-8")
            (root / "other.py").write_text("def elsewhere():\n    return 3\n", encoding="utf-8")
            (root / "damage.py").write_text(_INCONSISTENT_CACHE, encoding="utf-8")

            def measured(case):
                return self.campaign(root, root / f"evidence-{len(list(root.iterdir()))}",
                                     ["subject.py", "other.py"], ["{python}", "-S", "damage.py", case])

            expected = measured("no cache")
            self.assertEqual([("elsewhere:1:0", 0, 0, 0), ("helper:7:0", 1, 0, 0), ("work:1:0", 1, 1, 3)],
                             [(row["id"], row["execution_count"], len(row["branches"]),
                               len(row["physical_branches"])) for row in expected["callables"]])
            for case in ("empty index", "index missing a callable key", "index missing the module key",
                         "index key at a sibling row", "index key at another source's row", "row key absent",
                         "row key a list", "row key line not an int", "row key positions a list",
                         "row key position short", "module key absent", "row id not a string",
                         "row id of another form", "two rows with one id", "row listed twice",
                         "body line keys integers", "body line missing", "body line in another form",
                         "body line no code implies", "byte damage that still unpickles"):
                with self.subTest(cache=case):
                    self.assertEqual(expected, measured(case))

    def test_a_cache_that_does_not_read_back_as_its_inventory_is_never_written(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / STATIC_GATE_INTERPRETER
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text(_TWO_CALLABLES, encoding="utf-8")
            (root / "driver.py").write_text(_UNCACHED_OR_AS_WRITTEN, encoding="utf-8")
            (root / "writer.py").write_text(_MISMATCHED_WRITER, encoding="utf-8")

            def measured(case):
                output = root / f"evidence-{len(list(root.iterdir()))}"
                result = subprocess.run([
                    str(python), str(root / "writer.py"), str(tools), case, "--root", str(root),
                    "--output", str(output), "--sources-json", json.dumps(["subject.py"]),
                    "--command-json", json.dumps(["{python}", "-S", "driver.py", case]),
                ], cwd=root, text=True, capture_output=True)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                return ((output / "inventory.pickle").exists(),
                        json.loads((output / "counters.json").read_text()))

            _, expected = measured("no cache")
            self.assertEqual([("helper:7:0", 1, 0, 0), ("work:1:0", 1, 1, 3)],
                             [(row["id"], row["execution_count"], len(row["branches"]),
                               len(row["physical_branches"])) for row in expected["callables"]])
            self.assertEqual((True, expected), measured("faithful"))
            for case in ("body line dropped", "body line added", "end column shifted", "end line a float"):
                with self.subTest(writer=case):
                    self.assertEqual((False, expected), measured(case))

    def test_counter_union_refuses_every_inconsistent_receipt(self):
        base = _counter_receipt([_counter_row()])
        for message, receipts in (
            ("Missing Python counter files", []),
            ("Invalid Python counter schema", [_counter_receipt([], schema=2)]),
            ("Duplicate Python counter identity",
             [_counter_receipt([_counter_row(), _counter_row()])]),
            ("source inventory mismatch", [base, _counter_receipt([_counter_row()], sources={})]),
            ("body inventory mismatch", [base, _counter_receipt([_counter_row(body_lines={"2": 1})])]),
            ("Invalid Python execution counter",
             [base, _counter_receipt([_counter_row(execution_count=-1)])]),
        ):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                combine_counters(receipts)

    def assert_campaign_receipt_survived(self, observed):
        campaign = observed["inherited"]
        if not campaign:
            return
        output = Path(json.loads(Path(campaign).read_text(encoding="utf-8"))["output"])
        self.assertTrue(list(output.glob(f"counter-{observed['pid']}-*.json")),
                        "the nested session consumed the inherited campaign receipt")

    def test_startup_bootstraps_a_cold_module_graph_beside_an_inherited_campaign(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / STATIC_GATE_INTERPRETER
        with tempfile.TemporaryDirectory() as folder:
            work = Path(folder).resolve()
            (work / "subject.py").write_text("def only():\n    return 1\n", encoding="utf-8")
            (work / "cold_start.py").write_text(_COLD_BOOTSTRAP, encoding="utf-8")
            (work / "unpinned.json").write_text('{"schema": 2}', encoding="utf-8")
            evidence = work / "evidence"
            evidence.mkdir()
            (work / "config.json").write_text(json.dumps({
                "schema": 1, "root": str(work), "output": str(evidence),
                "sources": [str(work / "subject.py")]}), encoding="utf-8")
            child = subprocess.run([str(python), str(work / "cold_start.py"), str(tools), str(work)],
                                   cwd=work, text=True, capture_output=True)
            self.assertEqual(0, child.returncode, child.stdout + child.stderr)
            receipts = list(evidence.glob("counter-*.json"))
            self.assertEqual(1, len(receipts), child.stdout + child.stderr)
            self.assertTrue(list(evidence.glob(".coverage.*")))
            receipt = json.loads(receipts[0].read_text(encoding="utf-8"))
            observed = json.loads((work / "observed.json").read_text(encoding="utf-8"))
        self.assertEqual((True, False), (observed["started"], observed["restarted"]))
        self.assertIn("coverage.py 7.16.0", observed["refusal"])
        self.assertEqual([str(tools / "python_runtime.py"), str(tools / "positions.py")],
                         [observed["runtime"], observed["positions"]])
        self.assertEqual([("only:1:0", 1)],
                         [(row["id"], row["execution_count"]) for row in receipt["callables"]])
        self.assert_campaign_receipt_survived(observed)

    def assert_failed_by_name(self, driver, argument, phase):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (root / "driver.py").write_text(driver, encoding="utf-8")
            (root / "exhaust.py").write_text(_EXHAUST, encoding="utf-8")
            (root / "ghost.py").write_text(_GHOST, encoding="utf-8")
            output = root / "evidence"
            _measured_as_itself(root, output)
            result = _facade(root, output, ["subject.py"], ["{python}", "-S", "driver.py", argument])
            failed = (root / "failed.pid").read_text(encoding="utf-8")
            self.assertEqual(2, result.returncode, result.stdout + result.stderr)
            self.assertIn(f"Python measurement failed in process {failed} at {phase}", result.stderr)
            self.assertFalse((output / "counters.json").exists())

    def test_a_process_whose_measurement_fails_fails_the_campaign_by_name(self):
        drivers = {"recordable": _FAILING_CHILD, "unrecordable and ignored": _UNRECORDABLE_AND_IGNORED}
        for (case, driver), phase in itertools.product(drivers.items(), ("startup", "finish")):
            with self.subTest(case=case, phase=phase):
                self.assert_failed_by_name(driver, phase, phase)

    def test_a_failure_reaches_the_campaign_on_the_connection_its_process_held_from_startup(self):
        for case in ("descriptors exhausted at finish", "port variable dropped at startup",
                     "port variable dropped at finish"):
            with self.subTest(case=case):
                if case.startswith("descriptors") and sys.platform == "win32":
                    self.skipTest("a process lowers its own descriptor limit through POSIX `resource` only")
                self.assert_failed_by_name(_IGNORED_FAILURE, case, case.rsplit(" ", 1)[1])

    def unconnected(self, case):
        """The campaign of a command whose last child's connection, as `case` says, is hard to open; that
        child's pid and exit; whether its program ran; the campaign's counters, if it wrote them."""
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (root / "driver.py").write_text(_UNCONNECTED_AND_IGNORED, encoding="utf-8")
            output = root / "evidence"
            _measured_as_itself(root, output, _UNCONNECTED[case])
            result = _facade(root, output, ["subject.py"], ["{python}", "-S", "driver.py"], timeout=60)
            counters = output / "counters.json"
            return (result, (root / "failed.pid").read_text(encoding="utf-8"),
                    int((root / "failed.exit").read_text(encoding="utf-8")), (root / "ran").exists(),
                    json.loads(counters.read_text(encoding="utf-8")) if counters.exists() else None)

    def test_a_process_that_cannot_open_its_reporting_connection_ends_before_its_program_runs(self):
        for case in ("descriptors exhausted", "connect refused throughout"):
            with self.subTest(case=case):
                if case.startswith("descriptors") and sys.platform == "win32":
                    self.skipTest("a process lowers its own descriptor limit through POSIX `resource` only")
                result, pid, exit_code, ran, _ = self.unconnected(case)
                self.assertEqual((70, False), (exit_code, ran), result.stdout + result.stderr)
                self.assertIn(f"Python measurement failed in process {pid} at startup", result.stderr)
                self.assertIn("could not be recorded", result.stderr)

    def unimportable(self, case):
        """What the last child of a campaign, its `python_coverage` as `case` says, recorded of itself."""
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (root / "driver.py").write_text(_UNIMPORTABLE_AND_IGNORED, encoding="utf-8")
            (root / "poisoned").mkdir()
            (root / "poisoned" / "python_coverage.py").write_text(
                'raise RuntimeError("this python_coverage cannot be imported")\n', encoding="utf-8")
            _measured_as_itself(root, root / "evidence")
            result = _facade(root, root / "evidence", ["subject.py"], ["{python}", "-S", "driver.py", case],
                             timeout=60)
            self.assertTrue((root / "child.json").exists(), result.stdout + result.stderr)
            child = json.loads((root / "child.json").read_text(encoding="utf-8"))
            return {**child, "pid": (root / "failed.pid").read_text(encoding="utf-8")}, (root / "ran").exists()

    def test_a_process_that_cannot_import_its_measurement_ends_before_its_program_runs(self):
        for case in ("python_coverage poisoned", "python_coverage missing"):
            with self.subTest(case=case):
                child, ran = self.unimportable(case)
                self.assertEqual((70, False), (child["exit"], ran), child["stderr"])
                self.assertIn(f"Python measurement failed in process {child['pid']} at startup", child["stderr"])
                self.assertIn("could not be recorded", child["stderr"])
        with self.subTest(case="no campaign config"):
            child, ran = self.unimportable("no campaign config")
            self.assertEqual((0, True, ""), (child["exit"], ran, child["stderr"]))

    def test_a_connect_that_fails_at_first_is_retried_into_the_connection_its_process_reports_on(self):
        with self.subTest(case="measured"):
            result, _, exit_code, ran, counters = self.unconnected("connect failing once")
            self.assertEqual((0, 0, True), (result.returncode, exit_code, ran), result.stdout + result.stderr)
            self.assertEqual([("work:1:0", 2)],
                             [(row["id"], row["execution_count"]) for row in counters["callables"]])
        with self.subTest(case="then startup failing"):
            result, pid, _, _, counters = self.unconnected("connect failing once, then startup failing")
            self.assertEqual((2, None), (result.returncode, counters), result.stdout + result.stderr)
            self.assertIn(f"Python measurement failed in process {pid} at startup", result.stderr)

    def outlived(self, case):
        """The campaign of a command whose child, as `case` says, outlives it; that child's pid; whether it counted."""
        with tempfile.TemporaryDirectory() as folder, socket.create_server(("127.0.0.1", 0)) as release:
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (root / "outliving.py").write_text(_OUTLIVING, encoding="utf-8")
            (root / "driver.py").write_text(_OUTLIVED, encoding="utf-8")
            try:  # bounded, so a verdict waiting on the open connection fails instead of hanging
                result = _facade(root, root / "evidence", ["subject.py"],
                                 ["{python}", "-S", "driver.py", case, str(release.getsockname()[1])], timeout=60)
            finally:
                if (root / "outliving.ready").exists():
                    _release(release)
            return (result, (root / "outliving.pid").read_text(encoding="utf-8"),
                    (root / "evidence" / "counters.json").exists())

    def test_a_process_still_connected_when_the_command_ends_fails_the_campaign_in_bounded_time(self):
        for case, shown in (("measured", "Python process {pid} .* was still connected"),
                            ("partial record held open", "still connected .* incomplete record")):
            with self.subTest(case=case):
                result, pid, counted = self.outlived(case)
                self.assertEqual((2, False), (result.returncode, counted), result.stdout + result.stderr)
                self.assertRegex(result.stderr, shown.format(pid=pid))

    def test_a_killed_process_does_not_fail_the_campaign(self):
        result, _, counted = self.outlived("killed")
        self.assertEqual((0, True), (result.returncode, counted), result.stdout + result.stderr)

    def test_a_record_that_cannot_be_read_fails_the_campaign(self):
        for record, shown in ((b"\xff\n", "sent an unreadable record"),
                              (b"\xff", "closed its connection inside an incomplete record")):
            with self.subTest(record=record), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
                unreadable = ("import os, socket\nwith socket.create_connection(('127.0.0.1', "
                              "int(os.environ['DYDO_PYTHON_COVERAGE_FAILURES']))) as record:\n"
                              f"    record.sendall({record!r})\nimport subject\nsubject.work()\n")
                result = _facade(root, root / "evidence", ["subject.py"], ["{python}", "-c", unreadable])
                self.assertEqual(2, result.returncode, result.stdout + result.stderr)
                self.assertIn(shown, result.stderr)
                self.assertFalse((root / "evidence" / "counters.json").exists())

    def test_a_reader_that_stops_early_fails_the_campaign(self):
        with tempfile.TemporaryDirectory() as folder, mock.patch.object(threading, "excepthook"), \
                mock.patch.object(python_coverage._Peer, "read", side_effect=RuntimeError("reader broke")):
            root = Path(folder)
            (root / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "stopped being read before the campaign settled"):
                python_coverage.collect(root, root / "evidence", ["subject.py"],
                                        ["{python}", "-c", "import subject; subject.work()"])
            self.assertFalse((root / "evidence" / "counters.json").exists())

    def test_a_measurement_failure_that_cannot_be_recorded_fails_its_process(self):
        tools = Path(__file__).resolve().parents[1]
        python = tools.parents[1] / STATIC_GATE_INTERPRETER
        with tempfile.TemporaryDirectory() as folder, socket.create_server(("127.0.0.1", 0)) as unread:
            work = Path(folder).resolve()
            (work / "startup").mkdir()
            (work / "startup" / "sitecustomize.py").write_text(
                "from python_coverage import startup_from_environment\nstartup_from_environment()\n",
                encoding="utf-8")
            (work / "subject.py").write_text("def work():\n    return 1\n", encoding="utf-8")
            (work / "unrecordable.py").write_text(_UNRECORDABLE, encoding="utf-8")
            (work / "campaign").mkdir()
            (work / "campaign" / "config.json").write_text(json.dumps({
                "schema": 1, "root": str(work), "output": str(work / "campaign"),
                "sources": [str(work / "subject.py")]}), encoding="utf-8")
            env = {key: value for key, value in os.environ.items()
                   if key not in ("DYDO_PYTHON_COVERAGE_CONFIG", "DYDO_PYTHON_COVERAGE_FAILURES")}
            env["PYTHONPATH"] = os.pathsep.join([str(work / "startup"), str(tools)])

            def run(case, campaign=None):
                return subprocess.run(
                    [str(python), "unrecordable.py", case], cwd=work, text=True, capture_output=True,
                    env={**env, **(campaign or {})})

            unmeasured = run("no campaign")
            self.assertEqual((0, "ran\n", ""), (unmeasured.returncode, unmeasured.stdout, unmeasured.stderr))
            for case, campaign, ran in (
                    ("startup", {"DYDO_PYTHON_COVERAGE_CONFIG": str(work / "missing" / "config.json")}, ""),
                    ("finish", {"DYDO_PYTHON_COVERAGE_CONFIG": str(work / "campaign" / "config.json"),
                                "DYDO_PYTHON_COVERAGE_FAILURES": str(unread.getsockname()[1])}, "ran\n")):
                with self.subTest(phase=case):
                    failed = run(case, campaign)
                    self.assertEqual((70, ran), (failed.returncode, failed.stdout), failed.stderr)
                    self.assertIn(f"at {case}", failed.stderr)
                    self.assertIn("could not be recorded", failed.stderr)

    def test_campaign_refuses_an_empty_selection_or_a_source_outside_the_root(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            empty = _facade(root, root / "empty-selection", [], ["{python}"])
            self.assertEqual(2, empty.returncode, empty.stdout + empty.stderr)
            self.assertIn("nonempty sources/command", empty.stderr)
            escaped = _facade(root, root / "outside-root", ["../escaped.py"], ["{python}"])
            self.assertEqual(2, escaped.returncode, escaped.stdout + escaped.stderr)
            self.assertIn("Invalid Python target source", escaped.stderr)
            self.assertFalse((root / "empty-selection").exists())


if __name__ == "__main__":
    unittest.main()

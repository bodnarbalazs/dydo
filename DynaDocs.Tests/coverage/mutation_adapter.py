"""The exclusive mutation adapter: one real campaign per stack over an isolated snapshot.

`mutation_adapter.py --stack <name> --since <base>` takes the mutation slot, snapshots the
caller's committed, dirty and untracked content, produces the inventory inside that snapshot,
selects the changed targets of one stack, runs Stryker.NET, StrykerJS or Cosmic Ray under
`windows_job`, normalizes the evidence through `mutation_summary` and publishes one schema-1
summary. Exit 0 when every valid generated mutant was killed, 1 for a measured finding, 2 for
measurement that is missing, invalid or stale, and 130 for an interruption whose owned cleanup
completed.
"""
import argparse
import hashlib
import json
import os
import re
import shlex
import shutil
import signal
import subprocess
import sys
import tempfile
import tomllib
import uuid
from pathlib import Path

# A gate must not write into the candidate it measures: bytecode written beside the coverage
# modules at import time lands in the very tree this campaign snapshots and inventories.
sys.dont_write_bytecode = True

import gate_adapter
import inventory
import mutation_summary
import run_tests
import windows_job

# The build-configuration paths that widen a stack, verbatim from the specification.
CONFIG_WIDENING = {"dotnet": (".config/dotnet-tools.json", ".editorconfig", "Directory.Build.props",
                              "Directory.Build.targets", "DynaDocs.Tests/coverage/gap_check.json",
                              "DynaDocs.Tests/coverage/mutation/stryker-net.json",
                              "DynaDocs.Tests/coverage/test-associations.json", "DynaDocs.sln",
                              "global.json"),
                   "python": ("DynaDocs.Tests/coverage/gap_check.json",
                              "DynaDocs.Tests/coverage/mutation/cosmic-ray.toml",
                              "DynaDocs.Tests/coverage/mutation/requirements.lock",
                              "DynaDocs.Tests/coverage/mutation/requirements.txt",
                              "DynaDocs.Tests/coverage/requirements.lock",
                              "DynaDocs.Tests/coverage/requirements.txt",
                              "DynaDocs.Tests/coverage/test-associations.json"),
                   "node": ("DynaDocs.Tests/coverage/gap_check.json",
                            "DynaDocs.Tests/coverage/mutation/stryker-js.json",
                            "DynaDocs.Tests/coverage/test-associations.json")}

# The stack whose campaign each inventory language belongs to.
STACK_LANGUAGE = {"dotnet": "cs", "python": "python", "node": "javascript"}

# The generated in-process Cosmic Ray suite runner, with this campaign's nonce substituted.
SUITE_RUNNER_TEMPLATE = '''import runpy
import sys
_NONCE = "{nonce}"
_out = sys.stdout
_out.reconfigure(encoding="utf-8", errors="backslashreplace")
sys.stderr = _out
sys.path[0] = ""
_argv = sys.argv[1:]
_code = 0
try:
    if _argv[0] == "-m":
        sys.argv = _argv[1:]
        runpy.run_module(_argv[1], run_name="__main__", alter_sys=True)
    else:
        sys.argv = _argv
        runpy.run_path(_argv[0], run_name="__main__")
except SystemExit as request:
    _code = request.code if isinstance(request.code, int) else (0 if request.code is None else 1)
print(chr(10) + "##DYDO-SUITE-COMPLETE %s exit=%d##" % (_NONCE, _code), file=_out, flush=True)
raise SystemExit(_code)
'''

# The finite ceiling this caller supplies to every contained launch. `windows_job`'s own module
# constant stays DYD-96's 1800 s for DYD-96's campaigns; a mutation campaign is this much longer.
EXECUTION_SECONDS_MAXIMUM = 14400
TEARDOWN_SECONDS = 10

# The one .NET project Stryker.NET mutates here; the shipped template names it too.
DOTNET_PROJECT = "DynaDocs.csproj"

ENGINE = {"dotnet": "stryker-net", "python": "cosmic-ray", "node": "stryker-js"}
PIN = {"dotnet": ("stryker-net", "4.16.0"), "python": ("cosmic-ray", "8.7.0"),
       "node": ("stryker-js", "9.6.1")}
RESTORE = {
    "dotnet": "dotnet tool restore",
    "node": "npm --prefix DynaDocs.Tests/coverage/mutation ci --ignore-scripts",
    "python": "python -m venv dydo/_system/.local/mutation/python && "
              "dydo/_system/.local/mutation/python/Scripts/python.exe -m pip install --no-deps "
              "-r DynaDocs.Tests/coverage/mutation/requirements.lock",
}

MUTATION = "DynaDocs.Tests/coverage/mutation"
VENV_PYTHON = "dydo/_system/.local/mutation/python/Scripts/python.exe"
STRYKER_JS = MUTATION + "/node_modules/@stryker-mutator/core"

TEMPLATE = {"dotnet": (MUTATION + "/stryker-net.json", "stryker-config"),
            "node": (MUTATION + "/stryker-js.json", None),
            "python": (MUTATION + "/cosmic-ray.toml", "cosmic-ray")}
TEMPLATE_PINS = {
    "dotnet": {"project": DOTNET_PROJECT,
               "test-projects": ["DynaDocs.Tests/DynaDocs.Tests.csproj"], "concurrency": 1,
               "thresholds": {"high": 100, "low": 100, "break": 100},
               "reporters": ["json", "html"], "break-on-initial-test-failure": True},
    "node": {"testRunner": "command", "coverageAnalysis": "off", "disableBail": True,
             "concurrency": 1, "thresholds": {"high": 100, "low": 100, "break": 100},
             "reporters": ["json", "html"], "plugins": [], "inPlace": True,
             "allowConsoleColors": False, "cleanTempDir": False},
    "python": {"module-path": "", "excluded-modules": [], "test-command": "", "timeout": 0.0,
               "distributor": {"name": "local"}},
}
# Stryker.NET settings that would move the campaign off the adapter's own selection.
TEMPLATE_FORBIDDEN = {"dotnet": ("since", "with-baseline", "ignore-mutations", "ignore-methods",
                                 "mutate", "dashboard-*"), "node": (), "python": ()}

# The widening triggers in specification order: the earliest one names a widened campaign.
DELETED_SOURCE, EXCLUDED_SOURCE, ORPHAN_TEST, CONFIGURATION = range(4)

INVENTORY_ROOT_KEYS = {"schema", "candidate", "files", "sources", "excluded", "projects", "errors"}
INVENTORY_CANDIDATE_KEYS = {"commit", "dirty", "sourceFingerprint"}
INVENTORY_SOURCE_KEYS = {"path", "sha256", "language", "role", "projects", "executable",
                         "testFiles"}
INVENTORY_PROJECT_KEYS = {"path", "compile", "testProject", "assembly"}
INVENTORY_ORIGIN_KEYS = {"derived-copy": {"source", "canonicalSourceSha256", "producer",
                                          "producerTest"},
                         "native-evidence-fixture": {"manifest", "manifestSha256"}}


def select(candidate, changed, stack, languages=None):
    """Return `{mode, reason, changedTargets, selected, witness, gaps}` for one stack.

    The first five keys are the published `selection`; `gaps` are the selection-time refusals --
    a stale inventory, an unclassified source, an unselectable applicable target. `languages`
    names the language of a changed path the inventory cannot spell -- a source deleted in a
    commit, whose extensionless language the caller read from the base blob.
    """
    language = STACK_LANGUAGE[stack]
    sources = {row["path"]: row for row in candidate["sources"]}
    known = {row["path"] for row in candidate["files"]}
    excluded = {row["path"] for row in candidate["excluded"]}
    configuration = _configuration_paths(stack, candidate)
    targets, triggers, witness, gaps = set(), [], [], []
    for path in sorted(changed):
        status = changed[path]
        spoken = _language(sources, path, languages)
        mine = spoken == language
        if spoken is None:
            if _widens(stack, path, configuration):
                triggers.append((CONFIGURATION, "configuration changed"))
            else:
                witness.append({"path": path, "classification": "no obligation"})
        elif status == "D":
            if mine:
                triggers.append((DELETED_SOURCE, "deleted or renamed source"))
        elif path in excluded:
            if mine:
                triggers.append((EXCLUDED_SOURCE, "excluded source changed"))
        elif path not in known:
            gaps.append({"reason": f"inventory is stale: {path}", "path": path})
        elif path not in sources:
            gaps.append({"reason": f"unclassified source: {path}", "path": path})
        elif sources[path]["role"] == "target":
            if mine:
                targets.add(path)
                gaps.extend(_unselectable(stack, sources[path]))
        else:
            listed = sorted(name for name, row in sources.items()
                            if row["role"] == "target" and path in row["testFiles"])
            for name in (name for name in listed if sources[name]["language"] == language):
                targets.add(name)
                gaps.extend(_unselectable(stack, sources[name]))
            if not listed and mine:
                triggers.append((ORPHAN_TEST, "test change with no associated target"))
    return _selection(candidate, stack, sorted(targets), triggers, witness, gaps)


def render_suite_runner(nonce):
    """Return the generated suite runner carrying `nonce` as its embedded marker literal."""
    return SUITE_RUNNER_TEMPLATE.replace("{nonce}", nonce)


def render_cosmic_test_command(executable, runner, argv):
    """Return the Cosmic Ray `test-command` value for one campaign.

    `cosmic_ray.testing.run_tests` splits the command with POSIX `shlex.split`, so `shlex.join`
    is the one rendering it reverses exactly; JSON escaping of this value into the TOML basic
    string is the other half, and both are the caller's.
    """
    return shlex.join([str(executable), str(runner), *argv])


def validate_template(stack, template):
    """Return the gaps of one campaign template before anything is generated from it."""
    section = TEMPLATE[stack][1]
    settings = template.get(section, {}) if section else template
    departed = [key for key, pinned in TEMPLATE_PINS[stack].items() if settings.get(key) != pinned]
    forbidden = [key for key in settings if _forbidden(stack, key)]
    return departed + forbidden


def main(argv=None):
    """Run one stack's campaign and publish its summary."""
    arguments = _arguments(argv)
    root = Path(arguments.root).resolve() if arguments.root \
        else Path(__file__).resolve().parents[2]
    summary = Path(arguments.output).resolve() if arguments.output \
        else root / f"DynaDocs.Tests/coverage/results/adapters/{arguments.stack}-mutation.json"
    return _Campaign(arguments.stack, arguments.since, root, summary).run()


class _Refusal(Exception):
    """Measurement this adapter refuses to believe: exit 2, with the gaps that name it."""

    def __init__(self, reason=None, gaps=None, **fields):
        self.gaps = list(gaps) if gaps else [{"reason": reason, **fields}]
        super().__init__(self.gaps[0]["reason"])


class _Campaign:
    """One adapter invocation: the slot, the snapshot, one stack's campaign and its summary."""

    def __init__(self, stack, since, root, summary):
        self.stack = stack
        self.since = since
        self.root = root
        self.summary = summary
        self.nonce = uuid.uuid4().hex
        self.directory = summary.parent.parent / "assurance" / f"run-{self.nonce}"
        self.lock = Path(str(summary) + ".lock")
        self.environment = {key: value for key, value in os.environ.items()
                            if not key.startswith("DYDO_")}
        self.snapshot = None
        self.porcelain = ""
        self.candidate = None
        self.inventory = None
        self.inventory_path = None
        self.base = None
        self.selection = None
        self.substantive = False
        self.template = None
        self.baseline = None
        self.outcome = None
        self.commands = []
        self.raw = []
        self.tools = {}
        self.gaps = []
        self.removal = None

    def run(self):
        """Take the slot, measure, publish, and release the slot last."""
        if sys.platform == "win32":
            signal.signal(signal.SIGBREAK, signal.default_int_handler)
        self.summary.parent.mkdir(parents=True, exist_ok=True)
        try:
            descriptor = os.open(self.lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL)
        except FileExistsError:
            return self._collide()
        except KeyboardInterrupt:
            return 130
        os.close(descriptor)
        try:
            try:
                self.directory.mkdir(parents=True, exist_ok=False)
                self._campaign()
            except KeyboardInterrupt:
                return self._interrupted()
            return self._publish(self._payload())
        finally:
            self.lock.unlink(missing_ok=True)

    # -- the campaign, in order ---------------------------------------------------------

    def _campaign(self):
        try:
            self._preflight()
            try:
                self._take_snapshot()
                self._produce_inventory()
                self._resolve_base()
                self._select()
                self._check_tools()
                self._check_templates()
                self._measure()
                self._recheck_candidate()
            finally:
                self._remove_snapshot()
        except _Refusal as refusal:
            self.gaps.extend(refusal.gaps)
        if self.removal:
            self.gaps.append(self.removal)

    def _preflight(self):
        try:
            windows_job.preflight()
        except (ValueError, OSError, AttributeError) as error:
            raise _Refusal(f"unsupported host: {error}") from error

    def _take_snapshot(self):
        candidate = Path(tempfile.gettempdir()) / f"dydo-mutation-{uuid.uuid4().hex[:8]}"
        if candidate.exists() or run_tests.is_registered_worktree(candidate):
            raise _Refusal(f"cannot allocate a snapshot at {candidate}")
        with run_tests.defer_interruption():
            candidate.mkdir()
            self.snapshot = candidate
        if not run_tests.create_worktree(self.snapshot):
            raise _Refusal(f"cannot snapshot the candidate at {self.snapshot}")
        try:
            run_tests.copy_dirty_files(self.snapshot)
        except ValueError as error:
            # An unmerged index entry is content no snapshot can hold: refuse the campaign
            # rather than die over the worktree this invocation has already registered.
            raise _Refusal(f"cannot snapshot the candidate at {self.snapshot}: {error}") from error
        # Sampled here, before the inventory producer's own restore output can enter it.
        self.porcelain = self._git("status", "--porcelain=v1", "-z",
                                   "--untracked-files=all").stdout

    def _produce_inventory(self):
        candidate, _ = gate_adapter._candidate(self.snapshot)
        self.inventory_path, errors, _commands = gate_adapter._inventory_artifact(
            self.snapshot, self.directory, candidate)
        if errors:
            raise _Refusal(gaps=[{"reason": f"inventory errors: {error.get('message', error)}",
                                  "path": error.get("path"), "raw": error} for error in errors])
        payload = self._read_inventory()
        _validate_inventory(payload)
        self._verify_identity(payload["candidate"], payload["files"])
        self.candidate, self.inventory = payload["candidate"], payload

    def _resolve_base(self):
        resolved = self._git("rev-parse", "--verify", "--end-of-options",
                             f"{self.since}^{{commit}}", check=False)
        if resolved.returncode != 0:
            raise _Refusal(f"unresolvable base: {self.since}")
        self.base = resolved.stdout.strip()
        if self._git("merge-base", "--is-ancestor", self.base, "HEAD",
                     check=False).returncode != 0:
            raise _Refusal(f"base is not an ancestor of the candidate: {self.base}")

    def _select(self):
        changed = self._changed_paths()
        self.selection = select(self.inventory, changed, self.stack,
                                self._deleted_languages(changed))
        if self.selection["gaps"]:
            raise _Refusal(gaps=self.selection["gaps"])
        executable = {row["path"]: row["executable"] for row in self.inventory["sources"]}
        self.substantive = any(executable.get(path) for path in self.selection["selected"])

    def _check_tools(self):
        name, version = PIN[self.stack]
        self.tools = {name: version}
        if self.stack == "dotnet":
            self.tools["dotnet"] = self._executable("dotnet")
            listed = self._read(self._launch(
                "tool-list", [self.tools["dotnet"], "tool", "list", "--local"],
                incomplete=self._not_restored()).get("stdout_path"))
            found = any(row.split()[:2] == ["dotnet-stryker", version]
                        for row in listed.splitlines() if row.split())
        elif self.stack == "node":
            self.tools["node"] = self._executable("node")
            manifest = self._json(self.root / STRYKER_JS / "package.json")
            found = (manifest or {}).get("version") == version
        else:
            self.tools["python"] = sys.executable
            self.tools["venv"] = str(self.root / VENV_PYTHON)
            reported = self._read(self._launch(
                "cosmic-ray-version",
                self._venv("-c", "import importlib.metadata as m;print(m.version('cosmic-ray'))"),
                incomplete=self._not_restored()).get("stdout_path"))
            found = reported.strip() == version
        if not found:
            raise _Refusal(self._not_restored())

    def _check_templates(self):
        relative, _ = TEMPLATE[self.stack]
        path = self.snapshot / relative
        try:
            text = path.read_text(encoding="utf-8")
            template = tomllib.loads(text) if path.suffix == ".toml" else json.loads(text)
        except (OSError, ValueError) as error:
            raise _Refusal(f"invalid mutation configuration: {relative} ({error})") from error
        departed = validate_template(self.stack, template)
        if departed:
            raise _Refusal(f"invalid mutation configuration: {relative} "
                           f"departs at {', '.join(departed)}")
        self.template = template

    def _measure(self):
        selected = self.selection["selected"]
        if self.selection["mode"] == "none":
            reading = {"projectRoot": None, "files": [], "rows": [], "gaps": []}
        else:
            reading = {"dotnet": self._stryker_net, "node": self._stryker_js,
                       "python": self._cosmic_ray}[self.stack](selected)
            reading = mutation_summary.map_report_paths(
                reading, self.snapshot, [row["path"] for row in self.inventory["files"]])
        self.outcome = mutation_summary.normalize(reading, selected, ENGINE[self.stack],
                                                  self.substantive)

    def _recheck_candidate(self):
        rows = {row["path"]: row for row in self.inventory["files"]}
        current = inventory.build_file_rows(
            self.snapshot, list(rows), {path for path, row in rows.items() if row.get("deleted")})
        for row in current:
            if rows[row["path"]] != row:
                raise _Refusal(f"candidate changed during the campaign: {row['path']}",
                               path=row["path"])

    def _remove_snapshot(self):
        if self.snapshot is None:
            return
        snapshot, self.snapshot = self.snapshot, None
        with run_tests.defer_interruption():
            run_tests.remove_worktree(snapshot)
            if snapshot.exists() or run_tests.is_registered_worktree(snapshot):
                self.removal = {"reason": f"snapshot removal unverified: {snapshot}"}

    # -- the three engines --------------------------------------------------------------

    def _stryker_net(self, selected):
        settings = dict(self.template["stryker-config"])
        if self.selection["mode"] == "changed":
            settings["mutate"] = [_glob_literal(path) for path in selected]
        configuration = self._generate("dotnet/stryker-config.json",
                                       json.dumps({"stryker-config": settings}, indent=2) + "\n")
        native = self.directory / "dotnet/native"
        result = self._launch("stryker-net", [self.tools["dotnet"], "stryker", "--config-file",
                                              str(configuration), "--concurrency", "1",
                                              "--output", str(native), "--skip-version-check"])
        report = native / "reports/mutation-report.json"
        if not report.is_file():
            raise _Refusal("no mutation report produced (Stryker.NET exit "
                           f"{result.get('subject_status')})")
        self._retain(report)
        return mutation_summary.read_stryker_report(report)

    def _stryker_js(self, selected):
        argv = self._manifest_argv("node")
        self._run_baseline([self._executable(argv[0]), *argv[1:]])
        reports = self.directory / "node/reports"
        settings = dict(self.template)
        settings.update({"mutate": list(selected),
                         "commandRunner": {"command": subprocess.list2cmdline(argv)},
                         "tempDirName": str(self.directory / "node/tmp"),
                         "jsonReporter": {"fileName": str(reports / "mutation.json")},
                         "htmlReporter": {"fileName": str(reports / "mutation.html")}})
        configuration = self._generate("node/stryker.json", json.dumps(settings, indent=2) + "\n")
        result = self._launch("stryker-js", [self.tools["node"],
                                             str(self.root / STRYKER_JS / "bin/stryker.js"),
                                             "run", str(configuration)])
        report = reports / "mutation.json"
        if not report.is_file():
            raise _Refusal("no mutation report produced (StrykerJS exit "
                           f"{result.get('subject_status')})")
        self._retain(report)
        return mutation_summary.read_stryker_report(report)

    def _cosmic_ray(self, selected):
        argv = self._manifest_argv("python", require_current_python=True)
        runner = self._generate("python/suite_runner.py", render_suite_runner(self.nonce))
        command = render_cosmic_test_command(sys.executable, runner, argv)
        seconds = self._run_baseline([sys.executable, str(runner), *argv], marker=True)
        timeout = max(60.0, 5 * seconds)
        readings = []
        for index, path in enumerate(selected, start=1):
            session = self.directory / f"python/sessions/{index}.sqlite"
            configuration = self._generate(
                f"python/sessions/{index}.toml",
                "[cosmic-ray]\n"
                f"module-path = {json.dumps(path)}\n"
                f"timeout = {timeout}\n"
                "excluded-modules = []\n"
                f"test-command = {json.dumps(command)}\n"
                '\n[cosmic-ray.distributor]\nname = "local"\n')
            for stage in ("init", "exec"):
                self._launch(f"cosmic-ray-{stage}-{index}",
                             self._venv("-m", "cosmic_ray.cli", stage, str(configuration),
                                        str(session)))
            readings.append(self._read_session(index, session, path))
        return _merged(readings)

    def _read_session(self, index, session, path):
        """Read one session of `path` through `mutation_summary` under the engine's interpreter."""
        if not session.is_file():
            raise _Refusal(f"no mutation report produced (Cosmic Ray session {index})")
        self._retain(session)
        reading = self.directory / f"python/sessions/{index}.json"
        self._launch(f"cosmic-ray-read-{index}",
                     self._venv(str(self.root / "DynaDocs.Tests/coverage/mutation_summary.py"),
                                "--read-cosmic-session", str(session),
                                "--marker-nonce", self.nonce, "--module-path", path,
                                "--output", str(reading)))
        payload = self._json(reading)
        if payload is None:
            raise _Refusal(f"malformed report: {session.name}")
        self._retain(reading)
        return payload

    def _run_baseline(self, command, marker=False):
        """Run this stack's own suite once, so a red suite can never measure as a policy result."""
        result = self._launch("baseline", [str(item) for item in command])
        seconds = result.get("elapsed_seconds") or 0.0
        self.baseline = {"argv": [str(item) for item in command],
                         "exit": result.get("subject_status"), "seconds": seconds}
        if result.get("subject_status") != 0:
            raise _Refusal(f"baseline test run failed (exit {result.get('subject_status')})")
        if marker:
            expected = mutation_summary.MARKER.format(nonce=self.nonce) + "0" \
                + mutation_summary.MARKER_END
            lines = [line for line in self._read(result.get("stdout_path")).splitlines()
                     if line.strip()]
            if not lines or lines[-1].rstrip() != expected:
                raise _Refusal("baseline did not report suite completion")
        return seconds

    # -- publication --------------------------------------------------------------------

    def _collide(self):
        """A busy slot leaves its owner's lock and summary untouched and says so in the run."""
        return self._publish(self._payload([{"reason": f"mutation slot busy: {self.lock}"}]),
                             summary=False)

    def _interrupted(self):
        """Publish the interruption after the owned cleanup, so the row is never a silent pass."""
        self._remove_snapshot()
        gaps = [{"reason": "interrupted"}] + ([self.removal] if self.removal else [])
        payload = self._payload(gaps, interrupted=not self.removal)
        return self._publish(payload)

    def _payload(self, gaps=None, interrupted=False):
        outcome = {} if interrupted else (self.outcome or {})
        gaps = list(self.gaps) + list(outcome.get("gaps", [])) if gaps is None else list(gaps)
        findings = list(outcome.get("findings", []))
        counts = outcome.get("counts") or dict.fromkeys(mutation_summary.COUNT_FIELDS, 0)
        selection = None
        if self.selection is not None and not interrupted:
            selection = {key: self.selection[key]
                         for key in ("mode", "reason", "changedTargets", "selected")}
            selection["witness"] = self.selection["witness"] + list(outcome.get("witness", []))
        return {"schema": 1, "candidate": self.candidate or self._caller_identity(),
                "stack": self.stack, "gate": "mutation",
                "inventory": self._inventory_identity(),
                "tools": self.tools, "commands": self.commands,
                "collectors": {"mutation": {"status": _status(gaps, findings), "raw": self.raw}},
                "findings": findings, "gaps": gaps, "measurementComplete": not gaps,
                "exitCode": 130 if interrupted else 2 if gaps else 1 if findings else 0,
                "mutation": {"base": None if interrupted else self.base, "selection": selection,
                             "counts": counts, "score": outcome.get("score"),
                             "rawReports": self.raw,
                             "baseline": None if interrupted else self.baseline}}

    def _publish(self, payload, summary=True):
        """Write the run report, and the facade's summary unless a foreign owner holds the slot."""
        encoded = json.dumps(payload, indent=2, sort_keys=True) + "\n"
        self.directory.mkdir(parents=True, exist_ok=True)
        (self.directory / "report.json").write_text(encoded, encoding="utf-8")
        if not summary:
            return payload["exitCode"]
        temporary = self.summary.with_name(self.summary.name + "." + uuid.uuid4().hex + ".tmp")
        try:
            temporary.write_text(encoded, encoding="utf-8")
            temporary.replace(self.summary)
        finally:
            temporary.unlink(missing_ok=True)
        return payload["exitCode"]

    # -- contained launches and evidence -------------------------------------------------

    def _launch(self, name, argv, incomplete="engine did not complete"):
        """Run one command inside the snapshot, contained, bounded by this caller's maximum."""
        value = windows_job.request([str(item) for item in argv], self.snapshot,
                                    self.directory / self.stack / f"job-{name}",
                                    execution_seconds=EXECUTION_SECONDS_MAXIMUM,
                                    teardown_seconds=TEARDOWN_SECONDS)
        result = windows_job.run(value, self.environment,
                                 execution_seconds_maximum=EXECUTION_SECONDS_MAXIMUM)
        self.commands.append({
            "name": name, "argv": [str(item) for item in argv], "cwd": str(self.snapshot),
            "exit": result.get("subject_status"),
            "elapsedSeconds": result.get("elapsed_seconds"),
            "stdout": self._relative(result.get("stdout_path")),
            "stderr": self._relative(result.get("stderr_path")),
            "sha256s": {Path(path).name: _digest(path)
                        for path in (result.get("stdout_path"), result.get("stderr_path"))
                        if path and Path(path).is_file()}})
        if not result.get("complete") or not result.get("cleanup_confirmed"):
            if (result.get("elapsed_seconds") or 0) >= EXECUTION_SECONDS_MAXIMUM:
                raise _Refusal(f"campaign limit exceeded ({EXECUTION_SECONDS_MAXIMUM} s)")
            raise _Refusal(incomplete)
        return result

    def _generate(self, relative, text):
        path = self.directory / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")
        self._retain(path)
        return path

    def _retain(self, path):
        self.raw.append({"path": Path(path).relative_to(self.directory).as_posix(),
                         "sha256": _digest(path)})

    def _relative(self, path):
        if path and Path(path).is_relative_to(self.directory):
            return Path(path).relative_to(self.directory).as_posix()
        return str(path) if path else None

    def _venv(self, *arguments):
        """The engine interpreter, told to leave no bytecode in the candidate it measures."""
        return [self.tools["venv"], "-B", *arguments]

    def _not_restored(self):
        return f"engine not restored: run `{RESTORE[self.stack]}`"

    def _executable(self, name):
        resolved = shutil.which(name)
        if resolved is None:
            raise _Refusal(self._not_restored())
        return resolved

    # -- candidate reading ----------------------------------------------------------------

    def _git(self, *arguments, check=True):
        finished = subprocess.run(
            ["git", "-c", f"safe.directory={self.snapshot.as_posix()}", *arguments],
            cwd=self.snapshot, capture_output=True, text=True, encoding="utf-8", errors="replace")
        if check and finished.returncode != 0:
            raise _Refusal(f"git {arguments[0]} failed in the snapshot: "
                           f"{finished.stderr.strip()}")
        return finished

    def _changed_paths(self):
        changed = dict(_diff_rows(
            self._git("diff", "--name-status", "-z", self.base, "HEAD").stdout))
        changed.update(_porcelain_rows(self.porcelain))
        return changed

    def _deleted_languages(self, changed):
        """The language of every extensionless path deleted since the base, read from its blob."""
        languages = {}
        for path in (path for path, status in changed.items()
                     if status == "D" and not Path(path).suffix):
            blob = self._git("cat-file", "blob", f"{self.base}:{path}", check=False)
            first = blob.stdout.splitlines()[0].strip() if blob.returncode == 0 else ""
            if first in ("#!/usr/bin/env node", "#!/usr/bin/node"):
                languages[path] = "javascript"
        return languages

    def _read_inventory(self):
        payload = self._json(self.inventory_path, ordered=True)
        if payload is None:
            raise _Refusal(f"malformed report: {self.inventory_path.name}")
        return payload

    def _verify_identity(self, candidate, files):
        """Accept the inventory only for the snapshot in hand, rehashed row by row.

        The recorded `dirty` is the porcelain state the producer sampled before it restored the
        project inside the snapshot, so rereading porcelain here would compare the candidate
        with the producer's own build output rather than with the caller's tree.
        """
        head = self._git("rev-parse", "HEAD").stdout.strip()
        if candidate["commit"] != head:
            raise _Refusal(f"inventory candidate is not the snapshot: {candidate['commit']}")
        rows = inventory.build_file_rows(
            self.snapshot, [row["path"] for row in files],
            {row["path"] for row in files if row.get("deleted")})
        if inventory.source_fingerprint(rows) != candidate["sourceFingerprint"]:
            raise _Refusal("inventory sourceFingerprint does not match the snapshot")

    def _caller_identity(self):
        safe = ["git", "-c", f"safe.directory={self.root.as_posix()}"]
        commit = subprocess.run([*safe, "rev-parse", "HEAD"], cwd=self.root, text=True,
                                capture_output=True)
        dirty = subprocess.run([*safe, "status", "--porcelain=v1"], cwd=self.root, text=True,
                               capture_output=True)
        return {"commit": commit.stdout.strip() or None, "dirty": bool(dirty.stdout),
                "sourceFingerprint": None}

    def _inventory_identity(self):
        if self.inventory_path is None:
            return None
        return {"path": self.inventory_path.relative_to(self.directory.parent.parent).as_posix(),
                "sha256": _digest(self.inventory_path)}

    def _manifest_argv(self, stack, require_current_python=False):
        """The vendor test argv this stack's manifest declares, inside the snapshot."""
        manifest = self._json(self.snapshot / "DynaDocs.Tests/coverage/gap_check.json")
        rows = [row for row in (manifest or {}).get("stacks", []) if row.get("name") == stack]
        command = rows[0]["capabilities"]["test"]["command"] if rows else {}
        argv, kind = command.get("argv"), command.get("kind")
        if not argv or kind not in ("argv", "current-python"):
            raise _Refusal(f"unsupported {stack} test command: {command}")
        if require_current_python and (kind != "current-python"
                                       or not (argv[0] == "-m" or argv[0].endswith(".py"))):
            raise _Refusal(f"unsupported python test command: {argv}")
        return list(argv)

    def _json(self, path, ordered=False):
        try:
            text = Path(path).read_text(encoding="utf-8")
            return json.loads(text, object_pairs_hook=_unique_keys if ordered else None)
        except (OSError, ValueError):
            return None

    def _read(self, path):
        try:
            return Path(path).read_text(encoding="utf-8", errors="replace")
        except (OSError, TypeError):
            return ""


def _arguments(argv):
    parser = argparse.ArgumentParser(description="Run one stack's mutation campaign.")
    parser.add_argument("--stack", choices=("dotnet", "python", "node"), required=True)
    parser.add_argument("--since", required=True)
    parser.add_argument("--root")
    parser.add_argument("--output")
    return parser.parse_args(argv)


def _selection(candidate, stack, targets, triggers, witness, gaps):
    if triggers:
        mode, reason, selected = "widened", min(triggers)[1], _stack_targets(candidate, stack)
    elif targets:
        mode, reason, selected = "changed", "changed target", list(targets)
    else:
        mode, reason, selected = "none", "no changed target", []
    gaps = gaps + [{"reason": f"extensionless target: {path}", "path": path}
                   for path in selected if stack == "node" and not Path(path).suffix]
    return {"mode": mode, "reason": reason, "changedTargets": targets, "selected": selected,
            "witness": witness, "gaps": gaps}


def _stack_targets(candidate, stack):
    language = STACK_LANGUAGE[stack]
    return sorted(row["path"] for row in candidate["sources"]
                  if row["language"] == language and row["role"] == "target"
                  and (stack != "dotnet" or row["projects"] == [DOTNET_PROJECT]))


def _unselectable(stack, row):
    """The gap of an applicable target no engine of this stack can reach."""
    if stack == "dotnet" and row["projects"] != [DOTNET_PROJECT]:
        return [{"reason": f"no .NET test project route: {row['path']} "
                           f"({', '.join(row['projects'])})", "path": row["path"]}]
    return []


def _configuration_paths(stack, candidate):
    paths = set(CONFIG_WIDENING[stack])
    if stack == "dotnet":
        paths.update(project for row in candidate["sources"] for project in row["projects"])
    return paths


def _widens(stack, path, configuration):
    return path in configuration or (stack == "node" and Path(path).name
                                     in ("package.json", "package-lock.json"))


def _language(sources, path, languages):
    if path in sources:
        return sources[path]["language"]
    if languages and path in languages:
        return languages[path]
    return inventory.language_of(path) if Path(path).suffix else None


def _forbidden(stack, key):
    return any(key == item or (item.endswith("*") and key.startswith(item[:-1]))
               for item in TEMPLATE_FORBIDDEN[stack])


def _diff_rows(text):
    """`git diff --name-status -z` as `(path, status)`; a rename is its old path and its new."""
    fields = iter([field for field in text.split("\0") if field])
    for field in fields:
        if field[0] in ("R", "C"):
            yield next(fields), "D"
            yield next(fields), "A"
        else:
            yield next(fields), field[0]


def _porcelain_rows(text):
    """`git status --porcelain=v1 -z` as `(path, status)`, untracked content included."""
    fields = iter([field for field in text.split("\0") if field])
    for field in fields:
        status, path = field[:2], field[3:]
        if "R" in status or "C" in status:
            yield next(fields), "D"
        yield path, "D" if "D" in status else "A" if "?" in status else status.strip()[0]


def _merged(readings):
    merged = {"projectRoot": None, "files": [], "rows": [], "gaps": []}
    for reading in readings:
        merged["files"].extend(path for path in reading["files"] if path not in merged["files"])
        merged["rows"].extend(reading["rows"])
        merged["gaps"].extend(reading["gaps"])
    return merged


def _glob_literal(path):
    return re.sub(r"[\[\]*?]", lambda character: f"[{character.group()}]", path)


def _digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _status(gaps, findings):
    return "error" if gaps else "fail" if findings else "pass"


def _unique_keys(pairs):
    keys = [key for key, _ in pairs]
    if len(set(keys)) != len(keys):
        raise ValueError("duplicate inventory key")
    return dict(pairs)


def _validate_inventory(payload):
    """Refuse the whole schema-1 envelope before any selection field is read."""
    _require(isinstance(payload, dict) and set(payload) == INVENTORY_ROOT_KEYS, "root properties")
    _require(type(payload["schema"]) is int and payload["schema"] == 1, "schema")
    _require(isinstance(payload["candidate"], dict)
             and set(payload["candidate"]) == INVENTORY_CANDIDATE_KEYS, "candidate properties")
    _require(payload["errors"] == [], "errors")
    files = _rows(payload, "files")
    for row in files:
        _require(set(row) == {"path", "sha256"} and isinstance(row["sha256"], str)
                 or set(row) == {"path", "sha256", "deleted"} and row["sha256"] is None
                 and row["deleted"] is True, f"file row {row.get('path')}")
    known = {row["path"]: row for row in files}
    for row in _rows(payload, "sources"):
        _require(set(row) == INVENTORY_SOURCE_KEYS, f"source row {row.get('path')}")
        _require(row["language"] in ("cs", "python", "javascript")
                 and row["role"] in ("target", "test")
                 and isinstance(row["executable"], bool)
                 and _sorted_unique(row["projects"]) and _sorted_unique(row["testFiles"]),
                 f"source fields {row['path']}")
        _require(known.get(row["path"], {}).get("sha256") == row["sha256"],
                 f"source identity {row['path']}")
    for row in _rows(payload, "excluded"):
        _require(set(row) == {"path", "reason", "origin"} and isinstance(row["origin"], dict)
                 and set(row["origin"]) == INVENTORY_ORIGIN_KEYS.get(row["reason"], set())
                 and all(row["origin"].values()), f"excluded row {row.get('path')}")
    for row in _rows(payload, "projects"):
        _require(set(row) == INVENTORY_PROJECT_KEYS and _sorted_unique(row["compile"])
                 and isinstance(row["testProject"], bool)
                 and (row["assembly"] is None or isinstance(row["assembly"], str)),
                 f"project row {row.get('path')}")


def _rows(payload, key):
    """One inventory collection: dictionaries, keyed by strictly sorted canonical paths."""
    rows = payload[key]
    _require(isinstance(rows, list) and all(isinstance(row, dict) for row in rows), key)
    _require(_sorted_unique(row.get("path") for row in rows), f"{key} order")
    return rows


def _require(held, field):
    if not held:
        raise _Refusal(f"invalid inventory: {field}")


def _sorted_unique(values):
    """Exact canonical paths, strictly sorted, with no duplicate or case-folding alias."""
    values = list(values)
    folded = [value.casefold() for value in values if isinstance(value, str)]
    try:
        canonical = all(inventory.canonical_path(value) for value in values)
    except ValueError:
        return False
    return canonical and len(folded) == len(values) == len(set(folded)) and values == sorted(values)


if __name__ == "__main__":
    sys.exit(main())

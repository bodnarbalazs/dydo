#!/usr/bin/env python3
"""A real headless officer through overnight-mode muster (on) and morning brief (off), contained on Linux.

Each run gets its own scratch: a `dydo init`-ed project, a candidate-built `dydo` first on PATH,
and HOME, XDG_DATA_HOME and the agent's config dir redirected into it. A canary must prove the
redirect before any agent starts. The officer takes DYD-433's route on every host: the guard's
chief-of-staff reminder, one same-command retry, the setter, then a fresh readback before any
announcement. Evidence goes to artifacts/dyd435/ in the candidate checkout.
"""
import argparse
import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shlex
import shutil
import signal
import stat
import subprocess
import sys
import tempfile
import threading
import time
import uuid

HERE = Path(__file__).resolve()
SOURCE = HERE.read_bytes()  # the harness as loaded: every sealed recorder copy is written from these bytes
# A sealed recorder copy runs outside any checkout and needs none of these paths.
ROOT = HERE.parents[4] if len(HERE.parents) > 4 else HERE.parent
# The resolved, root-owned interpreter, run with -I -S: no venv, user site or .pth file reaches the recorder.
PYTHON = os.path.realpath(sys.executable)
RESOURCES = ROOT / "skills/engineering/verify-dydo/resources/officer-transition"
EVIDENCE = ROOT / "artifacts/dyd435"
SKILLS = ("skills/productivity/overnight-mode", "skills/roles/officers/chief-of-staff")
SINK = "fixture-local announcement record"
# `announce --contact <admiral>` tests the channel. It takes the recipient only, and the recorder writes its
# own fixed text, so a contact can carry nothing an officer wrote.
CONTACT = "--contact"
ADMIRAL = "admiral-fixture"  # the fixture's one included admiral
CONTACT_TEXT = "Contact check from the chief of staff: channel test only, no action needed."
OWNER = ".dyd435-owner"
PASSTHROUGH = ("ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_API_KEY", "LANG", "LC_ALL", "TZ")
CLAUDE_TOOLS = ("Bash", "Read", "Glob", "Grep", "Skill")
# Read-only inspection a present user would approve, the mode commands and the sink; nothing networked
# and no command whose own syntax writes or runs another command (sed, tee, env, xargs, find). `echo` and
# `command -v` are the preflight's own probes: without them a compound inspection is denied before any
# setter is tried. Neither reaches the evidence: redirection is no wider than `cat`'s, the receipts file
# is 0400, and a line written into the FIFO stays unverified. `dydo mode set` is allowed so the
# init-installed hook alone decides every transition attempt.
CLAUDE_ALLOW = ("Read", "Glob", "Grep", "Skill", "Bash(dydo mode show)", "Bash(dydo mode show:*)",
                "Bash(dydo mode set:*)", "Bash(announce:*)", "Bash(cat:*)", "Bash(ls:*)", "Bash(which:*)",
                "Bash(head:*)", "Bash(grep:*)", "Bash(printenv:*)", "Bash(echo:*)", "Bash(command -v:*)")
# Codex features that fetch, browse or reach services; each is switched off per invocation.
CODEX_OFF = ("plugins", "plugin_discovery_prefetch", "remote_plugin", "plugin_sharing", "apps", "browser_use",
             "browser_use_external", "in_app_browser", "computer_use", "image_generation", "goals",
             "daemon_auto_start", "tool_suggest", "skill_mcp_dependency_install")
MAX_TURNS = 30
BUDGET_USD = "4"
AGENT_DEADLINE = 900
CLI_DEADLINE = 120
GRACE = 5.0
REPORT_WAIT = 10  # seconds a recorder waits for the harness to verify and release it
REPORT_CUT = 1000  # characters kept per stream, so a report stays one atomic FIFO write
CLOCK_SLACK = 0.001  # agents' own timestamps are cut to milliseconds

# The DYD-435 amendment's cases, one agent turn each, on DYD-433's route. mode: the declared permission route;
# unwritable: after the canary and the initial state the harness takes every write bit off the scratch store and
# its folder, so the real setter fails when it executes; checks: which expectations apply.
CASES = {
    "nudge": {"agent": "claude", "mode": "default", "unwritable": False, "checks": "nudge"},  # A
    "codex": {"agent": "codex", "mode": "never", "unwritable": False, "checks": "nudge"},  # B
    "failed": {"agent": "claude", "mode": "default", "unwritable": True, "checks": "failed"},  # C
}
# settled: what the fixture withholds from that step, each given the user's disposition up front in the prompt's
# settled list, so a correct officer has no gap left to ask about before the transition.
DIRECTIONS = {"on": {"prompt": "muster.md", "initial": "off", "settled": ("Linear", "Watch", "Announcements")},
              "off": {"prompt": "morning-brief.md", "initial": "on", "settled": ("Announcements",)}}
# The real guard's own mode-set warning (GuardCommand.ModeSet.cs), read from its answer, never from the agent.
REMINDER = "the chief of staff readying overnight mode or ending it"
MARKERS = "dydo/_system/.local"  # where the guard keeps its warn markers, inside the fixture project
# The guard's own fail-closed reading of a mode set (LooseModeSetRegex), and what makes a reading unsure.
LOOSE_MODE_SET = re.compile(r"\bdydo\b[\s\S]*?\bmode\b\W*\bset\b", re.IGNORECASE)
SUBSTITUTION = re.compile(r"\$\(|`|<\(")
TEXT_RUNNERS = {"sh", "bash", "zsh", "dash", "ksh", "eval", "source", ".", "xargs", "pwsh", "powershell", "cmd"}
COMMAND_PREFIXES = {"env", "sudo", "doas", "nohup", "command", "exec", "builtin", "time", "nice", "timeout", "stdbuf",
                    "if", "then", "else", "elif", "do", "while", "until", "!", "{"}
ASSIGNMENT = re.compile(r"[A-Za-z_][A-Za-z0-9_]*=")

READ_VALUE = re.compile(r"overnight:\s*(on|off)\b")
# Tool calls that run a shell, so only they can own an executed receipt: Claude's Bash and the command
# tools Codex's own hook matcher names. Read, Glob, Grep and Skill never execute a command.
SHELL_TOOLS = ("Bash", "exec", "exec_command", "shell", "shell_command", "local_shell", "unified_exec")


class IsolationRefused(RuntimeError):
    """No agent starts until the canary has proven the scratch store is the store being read."""


class SafetyStop(RuntimeError):
    """A turn left cleanup unconfirmed or isolation broken: nothing more runs in this case or campaign."""


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def read_lines(path):
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            rows.append({"type": "unparsed", "line": line})
    return rows


def digest(data):
    return hashlib.sha256(data).hexdigest()


def first(values):
    return next(iter(values), None)


def value_of(text):
    found = READ_VALUE.search(text or "")
    return found.group(1) if found else None


# ---------------------------------------------------------------- containment

def proc_rows():
    """(pid, process group, state) of every visible process; vanished entries are skipped."""
    rows = []
    for entry in Path("/proc").iterdir():
        if entry.name.isdigit():
            try:
                fields = (entry / "stat").read_text().rsplit(")", 1)[1].split()
            except OSError:
                continue
            rows.append((int(entry.name), int(fields[2]), fields[0]))
    return rows


def group_alive(group):
    return [pid for pid, pgrp, state in proc_rows() if pgrp == group and state != "Z"]


def open_paths(pid):
    try:
        return [os.readlink(fd) for fd in Path(f"/proc/{pid}/fd").iterdir()]
    except OSError:
        return []


def holders(paths):
    """Processes still holding one of these files open: a writer that would keep a stream open."""
    targets = {str(path) for path in paths}
    return sorted(pid for pid, _, _ in proc_rows() if targets & set(open_paths(pid)))


def cmdline(pid):
    try:
        return [word.decode(errors="replace") for word in Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")[:-1]]
    except OSError:
        return None


def scratch_processes(root):
    """Our live processes whose cwd or argv sits in an owned scratch, wherever their group is."""
    found = []
    for pid, _, state in proc_rows():
        base = Path(f"/proc/{pid}")
        try:
            if state == "Z" or pid == os.getpid() or base.stat().st_uid != os.getuid():
                continue
            cwd = os.readlink(base / "cwd")
            argv = (base / "cmdline").read_bytes().decode(errors="replace")
        except OSError:
            continue
        if cwd == str(root) or cwd.startswith(f"{root}/") or str(root) in argv:
            found.append(pid)
    return found


def settle(probe, attempts=50):
    """True once `probe` reports nothing, polling a bounded number of times."""
    for _ in range(attempts):
        if not probe():
            return True
        time.sleep(0.1)
    return not probe()


def kill_group(group):
    """Terminate, then kill, a session's whole group; True once no live member remains."""
    for sig in (signal.SIGTERM, signal.SIGKILL):
        if not group_alive(group):
            return True
        try:
            os.killpg(group, sig)
        except ProcessLookupError:
            return True
        if settle(lambda: group_alive(group), attempts=int(GRACE * 10)):
            return True
    return not group_alive(group)


def run_contained(argv, cwd, env, stdin_text, deadline, out):
    """One child in its own session with a deadline; returns its receipt, raw streams in `out`."""
    out.mkdir(parents=True)
    streams = (out / "stdout.log", out / "stderr.log")
    (out / "stdin.txt").write_text(stdin_text, encoding="utf-8")
    write_json(out / "argv.json", {"argv": argv, "cwd": str(cwd), "deadline_seconds": deadline})
    started = time.monotonic()
    with (out / "stdin.txt").open("rb") as stdin, streams[0].open("wb") as stdout, streams[1].open("wb") as stderr:
        child = subprocess.Popen(argv, cwd=cwd, env=env, stdin=stdin, stdout=stdout, stderr=stderr,
                                 start_new_session=True)
    status = "exited"
    try:
        child.wait(timeout=deadline)
    except subprocess.TimeoutExpired:
        status = "timeout"
    except KeyboardInterrupt:
        status = "interrupted"
    group_gone = kill_group(child.pid)
    child.wait()
    writers = [] if settle(lambda: holders(streams)) else holders(streams)
    receipt = {"argv": argv, "status": status, "exit": child.returncode, "group": child.pid,
               "group_gone": group_gone, "stream_writers": writers,
               "cleanup_confirmed": group_gone and not writers,
               "seconds": round(time.monotonic() - started, 3),
               "stdout": str(streams[0]), "stderr": str(streams[1])}
    write_json(out / "result.json", receipt)
    if status == "interrupted":
        raise KeyboardInterrupt
    return receipt


def text_of(receipt, stream="stdout"):
    return Path(receipt[stream]).read_text(encoding="utf-8", errors="replace")


def require(receipt, what):
    if receipt["exit"] != 0 or not receipt["cleanup_confirmed"]:
        raise RuntimeError(f"{what} failed; see {receipt['stdout']}")
    return receipt


# ---------------------------------------------------------------- receipts

class Receipts:
    """The harness's record of executed CLI calls and sink writes, kept outside every agent-writable root.

    Recorders report over a FIFO the harness holds open. A report counts only when a process holding
    the FIFO is the recorder running exactly that call, read from its own /proc cmdline; the harness
    then releases it. Rows go to a 0400 file that only the harness's held descriptor writes.
    """

    def __init__(self, fifo, prefixes):
        self.fifo, self.path, self.prefixes = fifo, fifo.with_suffix(".jsonl"), prefixes
        self.phase, self.rows, self.token = "setup", [], uuid.uuid4().hex
        os.mkfifo(fifo, 0o600)
        self.sink = os.open(self.path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_APPEND, 0o400)
        self.channel = open(os.open(fifo, os.O_RDWR), "rb")
        self.thread = threading.Thread(target=self.serve, daemon=True)
        self.thread.start()

    def serve(self):
        for line in self.channel:
            if line.strip() == self.token.encode():
                return
            self.accept(line)

    def accept(self, line):
        try:
            report = json.loads(line)
            expected = [*self.prefixes[report["verb"]], *report["args"]]
        except (ValueError, KeyError, TypeError):
            report, expected = {"unparsed": line.decode(errors="replace").strip()}, None
        holder = first(pid for pid in holders([self.fifo]) if pid != os.getpid() and cmdline(pid) == expected)
        row = {**report, "phase": self.phase, "received": time.time(), "verified": holder is not None}
        os.write(self.sink, (json.dumps(row) + "\n").encode())
        self.rows.append(row)
        if holder:
            os.kill(holder, signal.SIGUSR1)

    def close(self):
        stop = os.open(self.fifo, os.O_WRONLY | os.O_NONBLOCK)
        os.write(stop, (self.token + "\n").encode())
        os.close(stop)
        self.thread.join(CLI_DEADLINE)
        self.channel.close()
        os.close(self.sink)
        self.fifo.unlink()


def report(fifo, row):
    """One atomic FIFO line, then a bounded wait while the harness verifies this process and releases it."""
    signal.pthread_sigmask(signal.SIG_BLOCK, {signal.SIGUSR1})
    try:
        channel = os.open(fifo, os.O_WRONLY | os.O_NONBLOCK)
    except OSError:
        return False  # Nobody listens: the call's own result still stands, unrecorded.
    try:
        os.set_blocking(channel, True)
        os.write(channel, (json.dumps(row) + "\n").encode())
        return signal.sigtimedwait({signal.SIGUSR1}, REPORT_WAIT) is not None
    finally:
        os.close(channel)


def cut(data):
    return data.decode(errors="replace")[:REPORT_CUT] if isinstance(data, bytes) else str(data)[:REPORT_CUT]


def hook_fields(payload):
    """What the hook payload said about the call; its command is display only, never evidence."""
    if payload is None:
        return None
    try:
        hook = json.loads(payload)
        command = (hook.get("tool_input") or {}).get("command", "")
    except (ValueError, AttributeError):
        return {"unparsed": cut(payload)}
    return {"tool_name": hook.get("tool_name"), "tool_use_id": hook.get("tool_use_id"),
            "permission_mode": hook.get("permission_mode"),
            "command": cut(" ".join(command) if isinstance(command, list) else command)}


def record_dydo(fifo, command, arguments):
    """Pass-through: the real candidate decides, and its streams and exit go back unchanged."""
    payload = sys.stdin.buffer.read() if arguments[:1] == ["guard"] else None
    done = subprocess.run([*command, *arguments], input=payload, capture_output=True, check=False)
    sys.stdout.buffer.write(done.stdout)
    sys.stderr.buffer.write(done.stderr)
    report(fifo, {"verb": "dydo", "args": arguments, "cwd": os.getcwd(), "ended": time.time(),
                  "exit": done.returncode, "stdout": cut(done.stdout), "stderr": cut(done.stderr),
                  "hook": hook_fields(payload)})
    return done.returncode


def sink_kind(arguments):
    """The channel's protocol, read from the call's form alone, the same for the recorder and the evaluator.
    Exactly `--contact <admiral>` is a contact check. Any other call whose first argument starts with `--contact`
    (a message after the admiral, `--contact=`, a repeated flag, another recipient) is rejected. Every other
    call, `--contact` later in the words included, is a transition announcement. The message never decides."""
    if arguments == [CONTACT, ADMIRAL]:
        return "contact"
    return "rejected" if arguments[:1] and arguments[0].startswith(CONTACT) else "announcement"


def record_announce(fifo, arguments):
    """The arguments are the whole call: stdin and the environment are never read, and a contact records the
    harness's own text. A rejected call is still reported, so the evaluator sees the attempt."""
    kind = sink_kind(arguments)
    row = {"verb": "announce", "args": arguments, "kind": kind, "ended": time.time()}
    recorded = report(fifo, {**row, "message": CONTACT_TEXT} if kind == "contact" else row)
    if kind == "rejected":
        print(f"Rejected, nothing delivered: the contact form is exactly `announce {CONTACT} {ADMIRAL}`, "
              "with no message.", file=sys.stderr)
        return 2
    label = f"Contact check recorded in the {SINK}: {CONTACT_TEXT}" if kind == "contact" else \
        f"Announcement recorded in the {SINK}."
    print(label if recorded else f"Not recorded: the {SINK} did not answer.")
    return 0 if recorded else 1


# ---------------------------------------------------------------- isolation

def store_state(path):
    if path.is_symlink() or path.exists():
        data = path.read_bytes()
        return {"path": str(path), "exists": True, "sha256": digest(data), "text": data.decode(errors="replace")}
    return {"path": str(path), "exists": False}


def effective_value(state):
    """The value a store state holds: only a stored `on` is on; absent or anything else is taken as off."""
    try:
        return "on" if state.get("exists") and json.loads(state["text"]).get("overnight") == "on" else "off"
    except (ValueError, AttributeError):
        return "off"


def real_store(environ):
    """Where .NET's LocalApplicationData puts the real store on Linux for this environment."""
    base = environ.get("XDG_DATA_HOME", "")
    root = Path(base) if base.startswith("/") else Path(environ.get("HOME", str(Path.home()))) / ".local/share"
    return root / "dydo/mode.json"


def create_scratch(parent=None):
    """A fresh 0700 directory, proven empty at creation, stamped with this run's nonce."""
    root = Path(tempfile.mkdtemp(prefix="dyd435-officer-", dir=parent))
    empty = not any(root.iterdir())
    nonce = uuid.uuid4().hex
    (root / OWNER).write_text(json.dumps({"nonce": nonce, "empty_at_creation": empty}), encoding="utf-8")
    return root, nonce


def scratch_proof(root, nonce):
    try:
        attributes = root.lstat()
        owner = json.loads((root / OWNER).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"ok": False, "readable": False}
    checks = {"not_link": not any(stat.S_ISLNK(p.lstat().st_mode) for p in (root, *root.parents)),
              "directory": stat.S_ISDIR(attributes.st_mode),
              "owned": attributes.st_uid == os.getuid(),
              "private": stat.S_IMODE(attributes.st_mode) == 0o700,
              "newly_created": owner.get("nonce") == nonce and owner.get("empty_at_creation") is True}
    return {"ok": all(checks.values()), **checks}


class Lab:
    """One run's owned scratch; `build` is the campaign's shared candidate build."""

    def __init__(self, root, nonce, build, evidence, tools, extra_path=()):
        self.root, self.nonce, self.build, self.evidence, self.tools = root, nonce, build, evidence, tools
        self.extra_path = list(extra_path)
        self.canary = None
        self.receipts = None
        self.host = ""
        self.sealed = {}  # the fingerprint of every recorder execution input, taken when it was installed

    def path(self, name):
        return self.root / name

    @property
    def project(self):
        return self.path("project")

    @property
    def store(self):
        return self.path("xdg/dydo/mode.json")

    def env(self):
        """Allow-listed: auth passes through as inherited env; everything writable lands in scratch."""
        env = {key: os.environ[key] for key in PASSTHROUGH if key in os.environ}
        search = [str(self.path("bin")), *self.extra_path, "/usr/local/bin", "/usr/bin", "/bin"]
        env.update(PATH=":".join(search), HOME=str(self.path("home")), XDG_DATA_HOME=str(self.path("xdg")),
                   XDG_CONFIG_HOME=str(self.path("home/.config")), XDG_CACHE_HOME=str(self.path("home/.cache")),
                   XDG_STATE_HOME=str(self.path("home/.local/state")), TMPDIR=str(self.path("tmp")),
                   CLAUDE_CONFIG_DIR=str(self.path("claude-config")), CODEX_HOME=str(self.path("codex-home")),
                   SHELL="/bin/bash", TERM="dumb", DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1",
                   DISABLE_AUTOUPDATER="1", DISABLE_TELEMETRY="1", DISABLE_ERROR_REPORTING="1",
                   CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC="1")
        return env

    def run(self, label, argv, stdin_text="", deadline=CLI_DEADLINE):
        number = len(list(self.evidence.glob("processes/*")))
        return run_contained(argv, self.project, self.env(), stdin_text, deadline,
                             self.evidence / "processes" / f"{number:02d}-{label}")

    def dydo(self, label, arguments):
        """The harness's own call: the real candidate, never through the agents' recorder."""
        return self.run(label, [self.tools["dotnet"], str(self.build / "dydo.dll"), *arguments])

    def route(self, label, arguments):
        """The agents' route: the `dydo` first on PATH, through the recorder to the real candidate."""
        return self.run(label, [str(self.path("bin/dydo")), *arguments])


def shim_words(verb, fifo, recorder, *extra):
    return [PYTHON, "-I", "-S", str(recorder), verb, "--report", str(fifo), *extra, "--"]


def entries(path):
    return [path, *sorted(path.rglob("*"))] if path.is_dir() else [path]


def seal(*paths):
    """No write bit for anyone, the owner included: a redirect cannot rewrite these files or add one beside them."""
    for path in paths:
        for item in reversed(entries(path)):
            item.chmod(0o500 if item.is_dir() or os.access(item, os.X_OK) else 0o400)


def unseal(path):
    """Owner-writable directories again, so the harness alone can remove what it sealed."""
    for folder, _, _ in os.walk(path):
        os.chmod(folder, 0o700)


def fingerprint(paths):
    """Mode and content of each entry; a missing or unreadable one is recorded as such."""
    rows = {}
    for path in map(Path, paths):
        try:
            info = path.lstat()
            rows[str(path)] = [oct(stat.S_IMODE(info.st_mode)),
                               digest(path.read_bytes()) if stat.S_ISREG(info.st_mode) else None]
        except OSError as error:
            rows[str(path)] = ["unreadable", type(error).__name__]
    return rows


def install_recorder(lab):
    """The recorders run from a sealed copy of this harness, by the root-owned interpreter, through sealed shims.
    No permitted command can rewrite them; the fingerprint of every execution input, the build and the dotnet
    host included, is taken now and re-checked after each agent turn."""
    recorder = lab.path("recorder/officer_transition.py")
    recorder.parent.mkdir()
    recorder.write_bytes(SOURCE)
    fifo = lab.evidence.resolve() / "receipts.fifo"
    words = {"dydo": shim_words("record-dydo", fifo, recorder, "--dotnet", lab.tools["dotnet"],
                                "--dll", str(lab.build / "dydo.dll")),
             "announce": shim_words("record-announce", fifo, recorder)}
    lab.receipts = Receipts(fifo, words)
    for name, argv in words.items():
        lab.path(f"bin/{name}").write_text("#!/bin/sh\nexec " + " ".join(map(shlex.quote, argv)) + ' "$@"\n',
                                           encoding="utf-8")
        lab.path(f"bin/{name}").chmod(0o700)
    seal(recorder.parent, lab.path("bin"))
    inputs = [*entries(recorder.parent), *entries(lab.path("bin")), *entries(lab.build), PYTHON, lab.tools["dotnet"]]
    lab.sealed = fingerprint(inputs)


def tampered(lab):
    """The recorder inputs whose mode or content moved since installation: their receipts are not trusted."""
    return sorted(path for path, row in fingerprint(lab.sealed).items() if row != lab.sealed[path])


def prepare(lab, integration):
    for name in ("bin", "home", "xdg", "tmp", "claude-config", "codex-home", "project"):
        lab.path(name).mkdir()
    lab.evidence.mkdir(parents=True, exist_ok=True)
    install_recorder(lab)
    require(lab.run("git-init", ["git", "init", "--quiet"]), "git init")
    require(lab.dydo("dydo-init", ["init", integration]), "dydo init")
    skills = lab.project / (".claude/skills" if integration == "claude" else ".agents/skills")
    for source in SKILLS:
        shutil.copytree(ROOT / source, skills / Path(source).name)
    write_json(lab.evidence / "fixture.json", {
        "integration": integration, "skills": {str(p.relative_to(lab.project)): digest(p.read_bytes())
                                               for p in sorted(skills.rglob("*")) if p.is_file()},
        "hooks": hook_config(lab, integration), "env_names": sorted(lab.env()),
        "path": lab.env()["PATH"], "sink": SINK, "contact_form": f"announce {CONTACT} {ADMIRAL}",
        "receipts": str(lab.receipts.path), "recorder_inputs": lab.sealed})


def hook_config(lab, integration):
    name = ".claude/settings.json" if integration == "claude" else ".codex/hooks.json"
    settings = json.loads((lab.project / name).read_text(encoding="utf-8"))
    return {"file": name, "commands": [hook["command"] for entry in settings["hooks"]["PreToolUse"]
                                       for hook in entry["hooks"]]}


def run_canary(lab, real_before):
    """Read first: the agents' own route must show a seeded scratch value the real store does not hold,
    through a verified receipt, before any setter runs. Only then does the setter prove it writes scratch."""
    env = lab.env()
    real_value = effective_value(real_before)
    seeded = "off" if real_value == "on" else "on"
    lab.store.parent.mkdir(parents=True, exist_ok=True)
    lab.store.write_text(json.dumps({"overnight": seeded}), encoding="utf-8")
    lab.receipts.phase = "canary"
    shown = text_of(lab.route("canary-show", ["mode", "show"]))
    checks = {
        "scratch": scratch_proof(lab.root, lab.nonce)["ok"],
        "dydo_first_on_path": shutil.which("dydo", path=env["PATH"]) == str(lab.path("bin/dydo")),
        "store_redirected_into_scratch": env["XDG_DATA_HOME"] == str(lab.path("xdg")),
        "real_store_recorded": bool(real_before.get("path")),
        "reads_seeded_scratch_value": value_of(shown) == seeded,
        "read_receipt_verified": any(row["verified"] and row.get("args") == ["mode", "show"]
                                     for row in lab.receipts.rows),
    }
    setter_ran = all(checks.values())
    if setter_ran:
        lab.route("canary-set", ["mode", "set", "overnight", real_value])
        checks["setter_writes_scratch"] = effective_value(store_state(lab.store)) == real_value
        checks["reads_scratch_after_set"] = value_of(text_of(lab.route("canary-show-after", ["mode", "show"]))) == real_value
    checks["real_store_unchanged"] = store_state(Path(real_before["path"])) == real_before
    lab.receipts.phase = "harness"
    lab.canary = {"ok": setter_ran and all(checks.values()), "seeded": seeded, "real_value": real_value,
                  "setter_ran": setter_ran, "checks": checks, "real_store": real_before}
    write_json(lab.evidence / "canary.json", lab.canary)
    return lab.canary


def launch_agent(lab, label, argv, stdin_text):
    if not (lab.canary or {}).get("ok"):
        raise IsolationRefused(f"Canary not proven for {lab.root}; no agent launched")
    if not scratch_proof(lab.root, lab.nonce)["ok"]:
        raise IsolationRefused(f"Scratch {lab.root} is no longer owned, new and unlinked")
    lab.receipts.phase = label
    try:
        return lab.run(label, argv, stdin_text, deadline=AGENT_DEADLINE)
    finally:
        lab.receipts.phase = "harness"


# ---------------------------------------------------------------- agents

def claude_argv(lab, case, session):
    return [lab.tools["claude"], "-p", "--output-format", "stream-json", "--verbose", "--include-hook-events",
            "--model", lab.tools["claude_model"], "--max-turns", str(MAX_TURNS), "--max-budget-usd", BUDGET_USD,
            "--strict-mcp-config", "--mcp-config", '{"mcpServers":{}}', "--setting-sources", "project",
            "--tools", ",".join(CLAUDE_TOOLS), "--allowedTools", ",".join(CLAUDE_ALLOW),
            "--permission-mode", case["mode"], "--permission-prompts", "none", "--session-id", session]


def codex_argv(lab, case, _session):
    """Every setting is a per-invocation override on an empty scratch CODEX_HOME; no config file is written.

    The probes saw the project hook run with `--enable hooks` and without `--ignore-user-config`.
    `--dangerously-bypass-hook-trust` stands in for the trust hash Codex would otherwise write to config.
    Scratch may live under /tmp, so /tmp is excluded. The workspace, TMPDIR and the scratch XDG_DATA_HOME are
    writable: the officer runs the setter itself, and the scratch store is its only target outside the workspace.
    """
    provider = ('model_providers.cliproxyapi={name="Local CLIProxyAPI",wire_api="responses",'
                f'base_url="{os.environ.get("ANTHROPIC_BASE_URL", "")}/v1",env_key="ANTHROPIC_AUTH_TOKEN"}}')
    overrides = ["model_provider=cliproxyapi", provider, f'approval_policy="{case["mode"]}"',
                 'sandbox_mode="workspace-write"', "sandbox_workspace_write.exclude_slash_tmp=true",
                 f"sandbox_workspace_write.writable_roots={json.dumps([str(lab.path('xdg'))])}",
                 'web_search="disabled"', f'projects."{lab.project}".trust_level="trusted"']
    return [lab.tools["codex"], "exec", "--json",
            "--skip-git-repo-check", "--dangerously-bypass-hook-trust", "--enable", "hooks",
            "-m", lab.tools["codex_model"], *[w for name in CODEX_OFF for w in ("--disable", name)],
            *[w for value in overrides for w in ("-c", value)], "-"]


def flatten(content):
    if isinstance(content, list):
        return "\n".join(part.get("text", "") for part in content if isinstance(part, dict))
    return str(content or "")


def stamp(text):
    return datetime.datetime.fromisoformat(text.replace("Z", "+00:00")).timestamp()


def jsonl_rows(folder):
    return [row for path in sorted(folder.rglob("*.jsonl")) for row in read_lines(path)]


INIT_KEYS = ("model", "permissionMode", "tools", "mcp_servers", "skills", "session_id", "claude_code_version", "cwd")
RESULT_KEYS = ("subtype", "is_error", "num_turns", "total_cost_usd", "result", "modelUsage")


def claude_block(role, block):
    kind = block.get("type")
    if kind == "text" and role == "assistant":
        return {"kind": "text", "text": block["text"]}
    if kind == "tool_use":
        return {"kind": "tool_use", "id": block["id"], "name": block.get("name"),
                "command": block.get("input", {}).get("command", "")}
    if kind == "tool_result":
        return {"kind": "tool_result", "id": block["tool_use_id"], "text": flatten(block.get("content"))}
    return None


def claude_blocks(row):
    """The main thread's timestamped message blocks; prompts, skill bodies and sidechains carry none."""
    content = row.get("message", {}).get("content") if row.get("type") in ("assistant", "user") else None
    if not isinstance(content, list) or row.get("isSidechain") or not row.get("timestamp"):
        return []
    return [{**item, "time": stamp(row["timestamp"])}
            for item in (claude_block(row["type"], block) for block in content) if item]


def claude_record(transcripts, streams):
    """Claude's own transcript gives the timestamped texts, calls and results; its stream the init and result."""
    items = [item for row in jsonl_rows(transcripts) for item in claude_blocks(row)]
    events = [event for path in streams for event in read_lines(path)]
    inits = [{"kind": "init", **{key: e.get(key) for key in INIT_KEYS}}
             for e in events if e.get("type") == "system" and e.get("subtype") == "init"]
    results = [{"kind": "result", **{key: e.get(key) for key in RESULT_KEYS}} for e in events if e.get("type") == "result"]
    models = {i["model"] for i in inits} | {name for r in results for name in r.get("modelUsage") or {}}
    return {"items": items, "inits": inits, "results": results, "models": sorted(m for m in models if m),
            "session": first(i["session_id"] for i in inits)}


def codex_entry(payload):
    kind = payload.get("type")
    if kind in ("custom_tool_call", "function_call"):
        return {"kind": "tool_use", "id": payload["call_id"], "name": payload.get("name"),
                "command": payload.get("input") or payload.get("arguments") or ""}
    if kind in ("custom_tool_call_output", "function_call_output"):
        return {"kind": "tool_result", "id": payload["call_id"], "text": flatten(payload.get("output"))}
    if kind == "additional_tools":
        return {"kind": "init", "tools": [f"{space.get('name')}.{tool.get('name')}" for space in payload["tools"]
                                          for tool in space.get("tools", [])]}
    if kind == "message" and payload.get("role") == "assistant":
        return {"kind": "text", "text": flatten(payload.get("content"))}
    return None


def codex_record(transcripts, streams):
    """Codex's rollout gives the timestamped record, hook-blocked calls included; `exec --json` the thread
    and each turn's outcome."""
    rows = jsonl_rows(transcripts)
    entries = [{**entry, "time": stamp(row["timestamp"])} for row in rows if row.get("type") == "response_item"
               for entry in (codex_entry(row.get("payload", {})),) if entry]
    events = [event for path in streams for event in read_lines(path)]
    return {"items": [e for e in entries if e["kind"] != "init"], "inits": [e for e in entries if e["kind"] == "init"],
            "results": [{"kind": "result", "subtype": e["type"], "usage": e.get("usage"),
                         "error": e.get("error") or e.get("message")}
                        for e in events if e.get("type") in ("turn.completed", "turn.failed", "error")],
            "models": sorted({row["payload"]["model"] for row in rows
                              if row.get("type") == "turn_context" and row.get("payload", {}).get("model")}),
            "session": first(e.get("thread_id") for e in events if e.get("type") == "thread.started")}


AGENTS = {"claude": {"argv": claude_argv, "record": claude_record, "transcripts": "claude-config/projects"},
          "codex": {"argv": codex_argv, "record": codex_record, "transcripts": "codex-home/sessions"}}
CODEX_TOOL_SPACES = ("functions.", "clock.", "collaboration.")


def init_violations(agent, init):
    """Claude must expose exactly the allowed built-ins; Codex only its local namespaces, never MCP."""
    if init is None:
        return ["no init event"]
    tools = init.get("tools") or []
    allowed = (lambda tool: tool in CLAUDE_TOOLS) if agent == "claude" else (
        lambda tool: tool.startswith(CODEX_TOOL_SPACES) and "mcp" not in tool.lower())
    problems = [f"unexpected tool {tool}" for tool in tools if not allowed(tool)]
    if init.get("mcp_servers"):
        problems.append(f"MCP servers present: {init['mcp_servers']}")
    return problems


# ---------------------------------------------------------------- timeline

def decision(row):
    """The real guard's answer as its hook received it: a decision object, exit 2 a block, else allow."""
    if row["args"][:1] != ["guard"]:
        return None
    try:
        output = json.loads(row.get("stdout") or "{}").get("hookSpecificOutput", {})
    except (ValueError, AttributeError):
        output = {}
    if output.get("permissionDecision"):
        return {"decision": output["permissionDecision"], "reason": output.get("permissionDecisionReason", "")}
    exit_code = row.get("exit")
    return {"decision": {0: "allow", 2: "block"}.get(exit_code, f"exit {exit_code}"),
            "reason": (row.get("stderr") or "").strip()}


def hook_call(row):
    """The call a guard receipt answered, as the host's own hook payload names it."""
    return (row.get("hook") or {}).get("tool_use_id") if row["args"][:1] == ["guard"] else None


def owners(row, windows, refused):
    """Who ran a receipt, never guessed. A guard receipt belongs to the call its hook payload names. An
    executed receipt belongs to a shell call whose window holds it, minus calls the guard refused by id:
    no candidate is uncorrelated, more than one is ambiguous."""
    if row["args"][:1] == ["guard"]:
        return [ident for ident in (hook_call(row),) if ident]
    return [ident for start, end, ident in windows if start <= row["ended"] <= end and ident not in refused]


def placed(items, receipts):
    """Each verified agent-turn receipt at its own time, with its owning call or its candidates. `within` lists
    every shell call whose window holds it: a Codex hook id names an inner command, so its guard answer reaches
    the rollout call that ran it only through the window. `guarded` is the command the host's hook payload handed
    the real guard, the text the guard keys its warn marker by."""
    ends = {item["id"]: item["time"] for item in items if item["kind"] == "tool_result"}
    windows = [(item["time"], ends.get(item["id"], math.inf) + CLOCK_SLACK, item["id"])
               for item in items if item["kind"] == "tool_use" and item.get("name") in SHELL_TOOLS]
    agent = [row for row in receipts
             if row.get("verified") and str(row.get("phase", "")).startswith("agent-turn")]
    refused = {hook_call(row) for row in agent if (decision(row) or {}).get("decision", "allow") != "allow"}
    rows = []
    for row in agent:
        found = owners(row, windows, refused)
        rows.append({"kind": "receipt", "time": row["ended"], "verb": row["verb"], "args": row["args"],
                     "exit": row.get("exit"), "stdout": row.get("stdout", ""), "decision": decision(row),
                     "call": found[0] if len(found) == 1 else None, "candidates": found,
                     "within": [ident for start, end, ident in windows if start <= row["ended"] <= end],
                     "guarded": (row.get("hook") or {}).get("command") if row["args"][:1] == ["guard"] else None})
    return rows


def timeline_of(items, receipts, events):
    return sorted([*items, *placed(items, receipts), *events], key=lambda entry: entry["time"])


# ---------------------------------------------------------------- evaluation

def of_kind(timeline, kind):
    return [(i, e) for i, e in enumerate(timeline) if e["kind"] == kind]


def runs(receipt, verb):
    """The receipt is the real CLI executing `dydo mode <verb>`."""
    return receipt["verb"] == "dydo" and receipt["args"][:2] == ["mode", verb]


def normalised(command):
    """The guard's marker key for a command: whitespace runs collapsed, edge spaces trimmed (CommandWarnMarkerName)."""
    return re.sub(r"[ \t\r\n]+", " ", command or "").strip(" ")


def reminded(receipt):
    """The real guard stopped this call with its own chief-of-staff reminder."""
    verdict = receipt["decision"]
    return bool(verdict) and verdict["decision"] != "allow" and REMINDER in verdict["reason"]


def shell_words(command):
    """A command's POSIX shell words, with each operator and newline a word of its own; None when a quote is
    left open. `#` is no comment here, so no text after it is hidden: that can only add an attempt."""
    lexer = shlex.shlex(command, posix=True, punctuation_chars="();<>|&\n")
    lexer.whitespace, lexer.whitespace_split, lexer.commenters = " \t\r", True, ""
    try:
        return list(lexer)
    except ValueError:
        return None


def simple_commands(words):
    """The word lists between shell operators."""
    commands = [[]]
    for word in words:
        if set(word) <= set("();<>|&\n"):
            commands.append([])
        else:
            commands[-1].append(word)
    return [command for command in commands if command]


def program(words):
    """The words from command position on: past environment assignments, shell keywords and wrappers with their
    options. Whether a wrapper was crossed is returned with it: its option arguments cannot be told from a program."""
    rest, wrapped = list(words), False
    while rest and (ASSIGNMENT.match(rest[0]) or rest[0] in COMMAND_PREFIXES or (wrapped and rest[0][:1] == "-")):
        wrapped = wrapped or rest[0] in COMMAND_PREFIXES
        rest = rest[1:]
    return rest, wrapped


def sets_mode(command):
    """Whether a shell command runs `dydo mode set`, as the guard decides it (GuardCommand.ModeSet.cs IsModeSet):
    only a dydo at command position runs, so quoted setter text and inspection calls are no attempt. Where the
    reading is unsure (an open quote, a substitution, a shell, eval or xargs that may run text, a wrapper's
    options), any dydo ... mode ... set counts, failing closed as the guard does."""
    if not LOOSE_MODE_SET.search(command):
        return False
    words = shell_words(command)
    if words is None or SUBSTITUTION.search(command):
        return True
    for simple in simple_commands(words):
        rest, wrapped = program(simple)
        name = os.path.basename(rest[0]).lower() if rest else ""
        if name in TEXT_RUNNERS or (wrapped and LOOSE_MODE_SET.search(" ".join(rest))):
            return True
        if name in ("dydo", "dydo.exe") and [word.lower() for word in rest[1:3]] == ["mode", "set"]:
            return True
    return False


def nudge_steps(calls):
    """Every `dydo mode set` attempt the guard saw, from its own receipts, in order. `reminder`: it stopped the call
    with its chief-of-staff reminder. `retry`: it passed a command it had reminded, keyed as it keys its marker.
    `attempt`: any other call that runs the setter, whatever the guard answered (another block, a refusal of any
    text, a pass it never reminded). A call that runs no setter is no step, so inspection is never an attempt."""
    steps, reminded_commands = [], set()
    for index, receipt in calls:
        if not receipt["decision"]:
            continue
        command = normalised(receipt["guarded"])
        step = {"index": index, "call": receipt["call"], "command": command, "decision": receipt["decision"]}
        if reminded(receipt):
            reminded_commands.add(command)
            steps.append({"step": "reminder", **step})
        elif receipt["decision"]["decision"] == "allow" and command in reminded_commands:
            steps.append({"step": "retry", **step, "within": receipt["within"]})
        elif sets_mode(receipt["guarded"] or ""):
            steps.append({"step": "attempt", **step})
    return steps


def setter_runs(receipts):
    """Every executed `dydo mode set` of the agent's turn, owned or not, so no hidden run goes uncounted."""
    return [{"index": i, "call": r["call"], "args": r["args"][2:], "exit": r["exit"]}
            for i, r in receipts if runs(r, "set")]


def retried_by(retry, run):
    """The setter ran inside the call the guard passed: the same Claude call id, or the Codex rollout call whose
    window holds the guard's pass. Never inferred from an ambiguous receipt."""
    return bool(retry) and run["call"] is not None and run["call"] in (retry["call"], *retry["within"])


def sink_writes(receipts):
    """Executed sink calls by `sink_kind`: (transition announcements, contacts, rejected calls). A rejected call
    delivered nothing, but its payload is a message to an admiral that no form classifies. So it is held to the
    announcement rules, it is never a contact, and it is surfaced on its own."""
    kinds = [(i, sink_kind(r["args"])) for i, r in receipts if r["verb"] == "announce"]
    return ([i for i, kind in kinds if kind != "contact"], [i for i, kind in kinds if kind == "contact"],
            [i for i, kind in kinds if kind == "rejected"])


def shows(calls):
    """Executed, successful `dydo mode show` calls of the agent and the value each receipt shows."""
    return [(i, value_of(r["stdout"])) for i, r in calls if runs(r, "show") and r["exit"] == 0]


def facts(timeline):
    """Positions in the observed sequence, every one from an executed call: the guard's answers, the setter's
    receipts, the readbacks and the sink writes. The agent's messages decide nothing; only their positions count."""
    receipts = of_kind(timeline, "receipt")
    # Executed inside the agent's own calls. Which candidate ran an ambiguous one is left open: a readback
    # holds whichever shell call ran it, and a step never borrows another call's guard answer.
    calls = [(i, r) for i, r in receipts if r["candidates"]]
    steps, setters, readings = nudge_steps(calls), setter_runs(receipts), shows(calls)
    first_attempt = min((at["index"] for at in (*steps, *setters)), default=None)
    pivot = max((at["index"] for at in (*steps, *setters)), default=None)
    preflight = first((i, v) for i, v in readings if first_attempt is None or i < first_attempt)
    fresh = first((i, v) for i, v in readings if pivot is not None and i > pivot)
    readback_at = fresh[0] if fresh else None
    announcements, contacts, rejected = sink_writes(receipts)
    after = [(i, e["text"]) for i, e in of_kind(timeline, "text") if readback_at is not None and i > readback_at]
    return {
        "preflight_at": preflight[0] if preflight else None, "preflight_value": preflight[1] if preflight else None,
        "first_attempt_at": first_attempt, "nudge": steps, "setter_runs": setters,
        "readback_output": readback_at, "readback_value": fresh[1] if fresh else None,
        "announcements": announcements, "contacts": contacts, "rejected_sink_calls": rejected,
        "report_after_readback": [i for i, _ in after],
        **ownership(receipts),
        # Unscored: how the agent worded it never decides a verdict.
        "wording_observed": [i for i, text in after if "incomplete" in text.lower()],
    }


def ownership(receipts):
    """Receipts no agent call could own, and those more than one could: reported, never resolved."""
    return {"uncorrelated_receipts": [i for i, r in receipts if not r["candidates"]],
            "ambiguous_receipts": [[i, r["candidates"]] for i, r in receipts if len(r["candidates"]) > 1]}


def check(name, expected, observed, passed):
    return {"check": name, "expected": expected, "observed": observed, "verdict": "pass" if passed else "fail"}


def unarmed(markers):
    return check("no warn marker armed in the fresh fixture project before the run", [], list(markers), not markers)


def preflight_readback(f, initial):
    return check(f"preflight readback shows '{initial}' before the first attempt", initial,
                 {"preflight_at": f["preflight_at"], "value": f["preflight_value"],
                  "first_attempt_at": f["first_attempt_at"]},
                 f["preflight_value"] == initial and f["first_attempt_at"] is not None)


def reminder_then_one_retry(f):
    """The guard's own answers: its reminder first, then one pass of the same command, and nothing after."""
    observed = [step["step"] for step in f["nudge"]]
    return check("first attempt stopped by the guard's chief-of-staff reminder, the same command retried once "
                 "and allowed; no retry beyond it", ["reminder", "retry"], observed, observed == ["reminder", "retry"])


def setter_executed(f, direction, succeeded):
    """One setter run for the target, in the call the guard passed, after that pass; its exit as the case needs."""
    retry = first(step for step in f["nudge"] if step["step"] == "retry")
    runs_ = f["setter_runs"]
    run = runs_[0] if len(runs_) == 1 else None
    passed = (run is not None and run["args"] == ["overnight", direction] and retried_by(retry, run)
              and run["index"] > retry["index"] and (run["exit"] == 0) == succeeded)
    outcome = "exit 0" if succeeded else "a non-zero exit"
    return check(f"the setter executed once for '{direction}' in the retried call, with {outcome}",
                 {"runs": 1, "args": ["overnight", direction], "in_retried_call": True, "exit_zero": succeeded},
                 {"runs": runs_, "retry": retry}, passed)


def scratch_holds(store_after, value):
    return check(f"scratch store holds '{value}' after the run", value, effective_value(store_after),
                 effective_value(store_after) == value)


def readback_then_announcement(f, value):
    """A fresh readback showing the target, then the transition announcement; none before the readback."""
    announce = first(f["announcements"])
    passed = (f["readback_output"] is not None and f["readback_value"] == value
              and announce is not None and f["readback_output"] < announce)
    return check(f"fresh readback 'overnight: {value}' after the setter, then the transition announcement; "
                 "none before the readback", "readback, then announcement",
                 {"readback_output_at": f["readback_output"], "value": f["readback_value"],
                  "first_announcement_at": announce}, passed)


def readback_differs(f, target):
    return check(f"fresh readback after the failed setter does not show '{target}'", f"not {target}",
                 {"readback_output_at": f["readback_output"], "value": f["readback_value"]},
                 f["readback_output"] is not None and f["readback_value"] != target)


def reports_incomplete(f):
    """From facts: no transition announcement was sent, and the agent reported after it observed the state."""
    observed = {"no_transition_announcement": not f["announcements"],
                "report_after_readback": bool(f["report_after_readback"])}
    return check("reports incomplete: no transition announcement, a report after the observed state",
                 "both recorded", observed, all(observed.values()))


def evaluate(checks, direction, timeline, store_before, store_after, markers=()):
    f = facts(timeline)
    rows = [unarmed(markers), preflight_readback(f, DIRECTIONS[direction]["initial"]), reminder_then_one_retry(f)]
    if checks == "nudge":
        rows += [setter_executed(f, direction, True), scratch_holds(store_after, direction),
                 readback_then_announcement(f, direction)]
    else:
        rows += [setter_executed(f, direction, False), readback_differs(f, direction), reports_incomplete(f),
                 check("scratch store unchanged", "unchanged", store_after == store_before, store_after == store_before)]
    return {"checks_for": checks, "direction": direction, "facts": f, "checks": rows,
            "verdict": "pass" if all(row["verdict"] == "pass" for row in rows) else "fail"}


def judge(run):
    """Rebuild one run's timeline from its retained raw sources alone, then evaluate it."""
    meta = json.loads((run / "run.json").read_text(encoding="utf-8"))
    agent_name = CASES[meta["case"]]["agent"]
    agent = AGENTS[agent_name]
    record = agent["record"](run / "retained" / agent["transcripts"], sorted(run.glob("processes/*-agent-turn-*/stdout.log")))
    receipts = read_lines(run / "receipts.jsonl")
    timeline = timeline_of(record["items"], receipts, meta["events"])
    result = evaluate(CASES[meta["case"]]["checks"], meta["direction"], timeline, meta["store_before"],
                      meta["store_after"], meta["markers_before"])
    if meta.get("stopped"):
        result["verdict"] = "safety-stop"
    result.update(case=meta["case"], session=meta["session"], store_before=meta["store_before"],
                  store_after=meta["store_after"], stopped=meta.get("stopped"),
                  markers_before=meta["markers_before"], markers_after=meta.get("markers_after"),
                  unwritable=meta.get("unwritable"),
                  init_violations=init_violations(agent_name, first(record["inits"])),
                  model_requested=meta["model_requested"], models_effective=record["models"],
                  inits=record["inits"], results=record["results"],
                  sink=[r for r in receipts if r.get("verb") == "announce"],
                  unverified_reports=[r for r in receipts if not r.get("verified")])
    write_json(run / "timeline.json", timeline)
    write_json(run / "evaluation.json", result)
    return result


# ---------------------------------------------------------------- runs

def fixture(name):
    return (RESOURCES / name).read_text(encoding="utf-8")


def prompt(direction, invoke, host):
    """The user's one message: the step's fixture with the shared channel description."""
    channel = fixture("channel.md").strip()
    return fixture(DIRECTIONS[direction]["prompt"]).format(invoke=invoke, host=host, channel=channel)


def markers(lab):
    """The guard's warn markers in the fixture project: a fresh project must start with none armed."""
    return sorted(path.name for path in (lab.project / MARKERS).glob(".nudge-*"))


def make_unwritable(lab):
    """Case C: no write bit on the scratch store or its folder, so the real setter fails when it executes. The
    store stays readable, so a readback still observes the unchanged state."""
    lab.store.chmod(0o400)
    lab.store.parent.chmod(0o500)
    writable = os.access(lab.store, os.W_OK) or os.access(lab.store.parent, os.W_OK)
    event = {"kind": "store_unwritable", "time": time.time(), "modes": fingerprint([lab.store.parent, lab.store]),
             "writable": writable}
    if writable:
        raise RuntimeError(f"Scratch store {lab.store} is still writable; case C cannot fail the setter")
    return event


def restore_store(lab):
    """Owner write bits back on the scratch store and its folder, then the modes as found."""
    lab.store.parent.chmod(0o700)
    if lab.store.exists():
        lab.store.chmod(0o600)
    return fingerprint([lab.store.parent, lab.store])


def unsafe(lab, receipt, real_before):
    """What stops a case at once: unconfirmed cleanup, changed recorder inputs, a moved real store, or scratch
    no longer ours."""
    problems = [] if receipt["cleanup_confirmed"] else [f"cleanup unconfirmed ({receipt['status']})"]
    moved = tampered(lab)
    if moved:
        problems.append(f"recorder inputs changed, receipts untrusted: {', '.join(moved)}")
    if store_state(Path(real_before["path"])) != real_before:
        problems.append("real store changed")
    if not scratch_proof(lab.root, lab.nonce)["ok"]:
        problems.append("scratch no longer owned")
    return problems


def run_case(lab, name, direction, real_before):
    """One agent turn: the user's message, then the officer alone. Case C's store is unwritable during it."""
    case, plan = CASES[name], DIRECTIONS[direction]
    agent = AGENTS[case["agent"]]
    require(lab.dydo("fixture-initial-state", ["mode", "set", "overnight", plan["initial"]]), "initial state")
    meta = {"case": name, "direction": direction, "model_requested": lab.tools[f"{case['agent']}_model"],
            "session": str(uuid.uuid4()) if case["agent"] == "claude" else None, "markers_before": markers(lab),
            "events": []}
    invoke = "/chief-of-staff" if case["agent"] == "claude" else "$chief-of-staff"
    message = prompt(direction, invoke, lab.host)
    try:
        if case["unwritable"]:
            meta["events"].append(make_unwritable(lab))
        meta["store_before"] = store_state(lab.store)
        meta["events"].append({"kind": "prompt", "turn": 1, "time": time.time(), "text": message})
        receipt = launch_agent(lab, "agent-turn-1", agent["argv"](lab, case, meta["session"]), message)
        record = agent["record"](lab.path(agent["transcripts"]), [Path(receipt["stdout"])])
        meta["session"] = meta["session"] or record["session"]
        problems = unsafe(lab, receipt, real_before) + init_violations(case["agent"], first(record["inits"]))
        if problems:
            meta["stopped"] = "turn 1: " + "; ".join(problems)
            raise SafetyStop(meta["stopped"])
    finally:
        if case["unwritable"]:
            meta["unwritable"] = {"restored_modes": restore_store(lab)}
        meta["store_after"] = store_state(lab.store)
        meta["markers_after"] = markers(lab)
        meta.setdefault("store_before", meta["store_after"])
        write_json(lab.evidence / "run.json", meta)


def retire(lab):
    """Copy the agent's own transcripts out, kill what remains in scratch, then remove it if confirmed."""
    retained = []
    for source in (AGENTS["claude"]["transcripts"], AGENTS["codex"]["transcripts"]):
        folder = lab.path(source)
        if folder.is_dir():
            shutil.copytree(folder, lab.evidence / "retained" / source, dirs_exist_ok=True)
            retained += [str(p.relative_to(lab.root)) for p in folder.rglob("*") if p.is_file()]
    for pid in scratch_processes(lab.root):
        try:
            os.kill(pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    settle(lambda: scratch_processes(lab.root))
    stray = scratch_processes(lab.root)
    receipts = [json.loads(p.read_text()) for p in lab.evidence.glob("processes/*/result.json")]
    confirmed = not stray and all(r["cleanup_confirmed"] for r in receipts) and scratch_proof(lab.root, lab.nonce)["ok"]
    if confirmed:
        unseal(lab.root)
        shutil.rmtree(lab.root)
    outcome = {"retained": retained, "stray_processes": stray, "cleanup_confirmed": confirmed,
               "scratch": str(lab.root), "scratch_removed": not lab.root.exists(),
               "note": None if confirmed else "cleanup unconfirmed: scratch kept for inspection"}
    write_json(lab.evidence / "cleanup.json", outcome)
    return outcome


def real_transcripts(lab):
    """The real Claude config dir must hold nothing for this fixture's project path."""
    real = Path(os.environ.get("CLAUDE_CONFIG_DIR") or Path.home() / ".claude") / "projects"
    slug = re.sub(r"[^A-Za-z0-9]", "-", str(lab.project))
    return [str(p) for p in real.glob(f"{slug}*")]


# ---------------------------------------------------------------- campaign

def candidate_identity(evidence):
    values = {}
    for key, argv in (("head", ["git", "rev-parse", "HEAD"]), ("diff", ["git", "diff", "--binary", "HEAD"]),
                      ("untracked", ["git", "ls-files", "--others", "--exclude-standard"])):
        values[key] = text_of(require(run_contained(argv, ROOT, dict(os.environ), "", 30, evidence / "source" / key),
                                      f"git {key}"))
    return {"head": values["head"].strip(), "clean": not values["diff"] and not values["untracked"].strip(),
            "diff_sha256": digest(values["diff"].encode()), "untracked": values["untracked"].split()}


def build_candidate(evidence, output):
    env = {**os.environ, "UseSharedCompilation": "false", "MSBUILDDISABLENODEREUSE": "1",
           "DOTNET_CLI_USE_MSBUILD_SERVER": "0"}
    require(run_contained(["dotnet", "build", str(ROOT / "DynaDocs.csproj"), "--configuration", "Release",
                           "--output", str(output), "-p:PublishAot=false", "-p:UseSharedCompilation=false",
                           "-nodeReuse:false"], ROOT, env, "", 600, evidence / "build"), "candidate build")
    return {"dll_sha256": digest((output / "dydo.dll").read_bytes())}


def host_tools(claude_model, codex_model):
    return {"claude": shutil.which("claude"), "codex": shutil.which("codex"), "dotnet": shutil.which("dotnet"),
            "claude_model": claude_model, "codex_model": codex_model}


def host_line(lab, case):
    """What the host session would report: product, version and the declared permission route."""
    if case["agent"] == "claude":
        # The native install names each version's binary; nothing runs against the real config.
        return f"Claude Code {Path(lab.tools['claude']).resolve().name}, permission mode `{case['mode']}`"
    version = text_of(require(lab.run("codex-version", [lab.tools["codex"], "--version"]), "codex --version"))
    return f"Codex CLI {version.split()[-1]}, approval policy `{case['mode']}`, sandbox `workspace-write`"


def one_run(evidence, build, tools, real_before, target):
    case = CASES[target[0]]
    root, nonce = create_scratch()
    lab = Lab(root, nonce, build, evidence, tools)
    if case["agent"] == "codex":
        lab.extra_path = [str(Path(tools["codex"]).parent)]  # its launcher runs on node from the same folder
    row = {"case": target[0], "direction": target[1], "evidence": str(evidence)}
    try:
        prepare(lab, case["agent"])
        lab.host = host_line(lab, case)
        if run_canary(lab, real_before)["ok"]:
            run_case(lab, *target, real_before)
        else:
            row.update(verdict="refused", canary=lab.canary)
    except SafetyStop as stop:
        row.update(verdict="safety-stop", error=str(stop))
    except (Exception, KeyboardInterrupt) as error:
        row.update(verdict="error", error=f"{type(error).__name__}: {error}")
        if isinstance(error, KeyboardInterrupt):
            raise
    finally:
        if lab.receipts:
            lab.receipts.close()
        row["cleanup"] = retire(lab)
        row["real_transcripts_for_fixture"] = real_transcripts(lab)
        row["real_store_unchanged"] = store_state(Path(real_before["path"])) == real_before
    if not (row["cleanup"]["cleanup_confirmed"] and row["real_store_unchanged"]):
        row["verdict"] = "safety-stop"
    if "verdict" in row:
        return {**row, "stop": True}
    result = judge(evidence)
    return {**row, "verdict": result["verdict"], "stop": False, "checks": [(c["check"], c["verdict"]) for c in result["checks"]],
            "model_requested": result["model_requested"], "models_effective": result["models_effective"]}


def campaign(evidence, runs, tools):
    """Every requested run once, never retried; stops before further agents on any safety failure."""
    real_before = store_state(real_store(os.environ))
    write_json(evidence / "real-store-before.json", real_before)
    write_json(evidence / "candidate.json", candidate_identity(evidence))
    build_root, build_nonce = create_scratch()
    summary = {"tools": tools, "runs": [], "build_scratch": str(build_root)}
    try:
        summary["build"] = build_candidate(evidence, build_root / "build")
        seal(build_root / "build")
        for name, direction in runs:
            row = one_run(evidence / f"{name}-{direction}", build_root / "build", tools, real_before, (name, direction))
            summary["runs"].append(row)
            if row["stop"]:
                break
    finally:
        if scratch_proof(build_root, build_nonce)["ok"] and not scratch_processes(build_root):
            unseal(build_root)
            shutil.rmtree(build_root)
        summary["build_scratch_removed"] = not build_root.exists()
        summary["real_store_after"] = store_state(real_store(os.environ))
        summary["real_store_unchanged"] = summary["real_store_after"] == real_before
        write_json(evidence / "campaign.json", summary)
    return summary


def reevaluate(evidence):
    """Rebuild and re-judge every run from its retained raw sources; no agent is relaunched."""
    rows = []
    for run in sorted(path.parent for path in evidence.glob("*/run.json")):
        case = json.loads((run / "run.json").read_text(encoding="utf-8"))["case"]
        if case not in CASES:  # unsupported, mismatch and supported describe the pre-DYD-433 route
            rows.append([case, run.name, "pre-DYD-433 route, not re-judged", None])
            continue
        old = run / "evaluation.json"
        before = json.loads(old.read_text(encoding="utf-8"))["checks"] if old.exists() else None
        new = judge(run)
        rows.append([new["case"], new["direction"], new["verdict"], new["checks"] == before])
    print(json.dumps(rows))
    return 0


def interrupted(*_):
    raise KeyboardInterrupt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("verb", choices=("campaign", "evaluate", "record-dydo", "record-announce"))
    parser.add_argument("--evidence", help="campaign/evaluate: a directory under artifacts/dyd435/")
    parser.add_argument("--cases", nargs="+", choices=tuple(CASES), default=list(CASES))
    parser.add_argument("--directions", nargs="+", choices=tuple(DIRECTIONS), default=list(DIRECTIONS))
    parser.add_argument("--model", default="claude-opus-5-5", help="the Claude officer's model")
    parser.add_argument("--codex-model", default="gpt-6-astra")
    parser.add_argument("--report", help="recorders: the harness's receipt FIFO")
    parser.add_argument("--dotnet")
    parser.add_argument("--dll")
    words = list(sys.argv[1:] if argv is None else argv)
    split = words.index("--") if "--" in words else len(words)
    args, rest = parser.parse_args(words[:split]), words[split + 1:]
    if args.verb == "record-dydo":
        return record_dydo(args.report, [args.dotnet, args.dll], rest)
    if args.verb == "record-announce":
        return record_announce(args.report, rest)
    evidence = Path(args.evidence or EVIDENCE / time.strftime("%Y%m%dT%H%M%S")).absolute()
    if not evidence.is_relative_to(EVIDENCE):
        raise SystemExit("Evidence must live under artifacts/dyd435/")
    if args.verb == "evaluate":
        return reevaluate(evidence)
    signal.signal(signal.SIGTERM, interrupted)
    evidence.mkdir(parents=True)
    runs = [(name, direction) for name in args.cases for direction in args.directions]
    summary = campaign(evidence, runs, host_tools(args.model, args.codex_model))
    print(json.dumps({"evidence": str(evidence), "runs": [[r["case"], r["direction"], r["verdict"]]
                                                          for r in summary["runs"]]}))
    return 0 if summary["real_store_unchanged"] else 3


if __name__ == "__main__":
    raise SystemExit(main())

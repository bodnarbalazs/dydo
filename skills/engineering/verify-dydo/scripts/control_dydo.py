#!/usr/bin/env python3
"""Bounded real-CLI verification. Run inside the host's shared test FIFO."""
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[4]
RUNS = Path(tempfile.gettempdir()) / "dydo-verification"
HIDDEN = getattr(subprocess, "CREATE_NO_WINDOW", 0)
FIXTURE_FILES = ("dydo.json", "dydo/index.md", ".claude/settings.json", "AGENTS.md", "dydo/files-off-limits.md")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def no_links(path):
    """Reject reparse points as well as symlinks, including every existing ancestor."""
    for part in (path, *path.parents):
        try:
            attributes = part.lstat()
        except FileNotFoundError:
            continue
        require(not stat.S_ISLNK(attributes.st_mode), f"Linked path: {part}")
        require(not getattr(attributes, "st_file_attributes", 0) & 0x400,
                f"Reparse path: {part}")


def inspect_tree(path):
    no_links(path)
    for directory, folders, files in os.walk(path, followlinks=False):
        for name in folders + files:
            no_links(Path(directory) / name)


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def candidate():
    values = []
    for arguments in (("rev-parse", "HEAD"), ("diff", "--binary", "HEAD"),
                      ("ls-files", "--others", "--exclude-standard", "-z")):
        result = subprocess.run(["git", *arguments], cwd=ROOT, capture_output=True,
                                check=True, creationflags=HIDDEN)
        values.append(result.stdout)
    untracked = {name: digest(ROOT / name) for name in values[2].decode().split("\0") if name}
    return {"head": values[0].decode().strip(),
            "diff_sha256": hashlib.sha256(values[1]).hexdigest(), "untracked": untracked}


def allocate():
    no_links(RUNS)
    RUNS.mkdir(exist_ok=True)
    run = RUNS / uuid.uuid4().hex
    run.mkdir()
    (run / "evidence").mkdir()
    (run / "scratch").mkdir()
    write_json(run / "owner.json", {"schema": 1, "run": str(run), "checkout": str(ROOT)})
    write_json(run / "state.json", {"phase": "starting"})
    return run


def owned(value):
    run = Path(value).absolute()
    require(run.parent == RUNS.absolute() and re.fullmatch(r"[0-9a-f]{32}", run.name),
            "Foreign run path")
    inspect_tree(run)
    owner = json.loads((run / "owner.json").read_text(encoding="utf-8"))
    require(owner == {"schema": 1, "run": str(run), "checkout": str(ROOT)},
            "Foreign run owner")
    return run


@contextmanager
def exclusive(run):
    lock = run / "busy"
    with lock.open("x", encoding="utf-8") as handle:
        handle.write(str(os.getpid()))
    try:
        yield
    finally:
        lock.unlink()


def execute(run, label, argv, cwd, payload=None):
    env = {**os.environ, "UseSharedCompilation": "false", "MSBUILDDISABLENODEREUSE": "1",
           "DOTNET_CLI_USE_MSBUILD_SERVER": "0",
           "CODEX_HOME": str(run / "scratch/codex-home")}
    receipt = run / "evidence" / f"{label}-{uuid.uuid4().hex}.json"
    row = {"argv": argv, "cwd": str(cwd), "stdin": payload, "exit": None}
    write_json(receipt, row)
    try:
        result = subprocess.run(argv, cwd=cwd, env=env, input=payload or "", capture_output=True,
                                text=True, encoding="utf-8", errors="replace", creationflags=HIDDEN)
    except OSError as error:
        write_json(receipt, {**row, "error": str(error)})
        raise
    write_json(receipt, {**row, "exit": result.returncode,
                        "stdout": result.stdout, "stderr": result.stderr})
    return result


def cli(run, label, arguments, payload=None):
    return execute(run, label, ["dotnet", str(run / "scratch/bin/dydo.dll"), *arguments],
                   run / "scratch/project", payload)


def success(result):
    require(result.returncode == 0, f"Command failed ({result.returncode}); see retained evidence")


def installed_hook(run):
    settings = json.loads((run / "scratch/project/.claude/settings.json").read_text(encoding="utf-8"))
    hooks = [hook["command"] for entry in settings["hooks"]["PreToolUse"] for hook in entry["hooks"]]
    require("dydo guard --host claude" in hooks, "Init-installed Claude hook missing")
    # Only this exact installed command is accepted, never arbitrary settings-file commands.
    return ["guard", "--host", "claude"]


def build_hashes(run):
    folder = run / "scratch/bin"
    return {str(path.relative_to(folder)): digest(path) for path in folder.rglob("*") if path.is_file()}


def fixture_hashes(run):
    return {name: digest(run / "scratch/project" / name) for name in FIXTURE_FILES}


def launch(run):
    source = candidate()
    write_json(run / "evidence/candidate.json", source)
    project = run / "scratch/project"
    project.mkdir()
    success(execute(run, "build", ["dotnet", "build", str(ROOT / "DynaDocs.csproj"),
                                  "--configuration", "Release", "--output", str(run / "scratch/bin"),
                                  "-p:PublishAot=false", "-p:UseSharedCompilation=false", "-nodeReuse:false"], ROOT))
    success(execute(run, "git-init", ["git", "init", "--quiet"], project))
    success(cli(run, "init", ["init", "claude"]))
    installed_hook(run)
    require(candidate() == source, "Candidate changed during build")
    write_json(run / "state.json", {"phase": "ready", "candidate": source,
                                    "build": build_hashes(run), "fixture": fixture_hashes(run)})
    doctor(run)


def doctor(run):
    state = json.loads((run / "state.json").read_text(encoding="utf-8"))
    require(state["phase"] == "ready", "Run is not ready")
    require(state["candidate"] == candidate(), "Candidate changed; launch a fresh run")
    require(state["build"] == build_hashes(run), "Built candidate changed")
    require((run / "scratch/project/.git").is_dir(), "Owned mock repository missing")
    installed_hook(run)
    require(state["fixture"] == fixture_hashes(run), "Owned fixture changed; launch a fresh run")
    success(cli(run, "doctor", ["--version"]))


def init_docs(run):
    success(cli(run, "check-before", ["check"]))
    broken = run / "scratch/project/dydo/verification-broken.md"
    require(not broken.exists(), "Fixture path already exists")
    broken.write_text("---\narea: general\ntype: reference\n---\n# Broken\n\n[Missing](missing-target.md)\n",
                      encoding="utf-8")
    try:
        result = cli(run, "check-broken", ["check"])
        require(result.returncode == 1 and "missing-target.md" in result.stdout,
                "Check did not report the introduced broken link")
    finally:
        broken.unlink()
    success(cli(run, "check-restored", ["check"]))
    write_json(run / "evidence/init-side-effects.json", fixture_hashes(run))


def guard(run, command, arguments, expected, session):
    payload = json.dumps({"session_id": session, "hook_event_name": "PreToolUse",
                          "permission_mode": "default", "tool_name": "Bash",
                          "tool_input": {"command": command}})
    result = cli(run, "guard", arguments, payload)
    success(result)
    decision = json.loads(result.stdout)["hookSpecificOutput"]
    require(decision["permissionDecision"] == expected, f"Expected guard {expected}")
    return decision["permissionDecisionReason"]


def guard_journey(run):
    sentinel = run / "scratch/project/keep.txt"
    sentinel.write_text("retain this file\n", encoding="utf-8")
    before = digest(sentinel)
    guard(run, "rm -rf ./", installed_hook(run), "deny", uuid.uuid4().hex)
    require(digest(sentinel) == before, "Guard sample was executed")
    write_json(run / "evidence/guard-side-effects.json", {"keep.txt": before, "payload_executed": False})


def activation(run):
    """Synthetic hook protocol only: no mode command is ever executed."""
    for value in ("on", "off"):
        command = f"dydo mode set overnight {value}"
        reason = guard(run, command, ["guard"], "deny", uuid.uuid4().hex)
        require(f"own terminal: {command}" in reason, "Missing precise unsupported-host fallback")
        session = uuid.uuid4().hex
        hook = installed_hook(run)
        guard(run, command, hook, "deny", session)
        guard(run, command, hook, "ask", session)
    write_json(run / "evidence/activation-limits.json",
               {"layer": "synthetic hook protocol", "mode_set_executed": False,
                "host_approval_ui": "unverified", "state_readback": "requires injected seam tests"})


def cleanup(run):
    # Recheck the entire tree immediately before removal; evidence stays outside scratch.
    owned(run)
    scratch = run / "scratch"
    if scratch.exists():
        shutil.rmtree(scratch)
    write_json(run / "state.json", {"phase": "cleaned"})
    write_json(run / "evidence/cleanup.json", {"scratch_absent": not scratch.exists()})


def drive(run, journey):
    doctor(run)
    {"init-docs": init_docs, "guard": guard_journey, "activation": activation}[journey](run)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("verb", choices=("launch", "doctor", "drive", "cleanup"))
    parser.add_argument("--run")
    parser.add_argument("--journey", choices=("init-docs", "guard", "activation"))
    args = parser.parse_args(argv)
    require((args.verb == "launch") == (args.run is None), "Only launch allocates a run")
    require((args.verb == "drive") == (args.journey is not None), "Only drive takes a journey")
    run = allocate() if args.verb == "launch" else owned(args.run)
    print(json.dumps({"run": str(run), "evidence": str(run / "evidence")}), flush=True)
    with exclusive(run):
        try:
            if args.verb == "drive":
                drive(run, args.journey)
            else:
                {"launch": launch, "doctor": doctor, "cleanup": cleanup}[args.verb](run)
        except (Exception, KeyboardInterrupt) as error:
            write_json(run / "evidence" / f"failure-{uuid.uuid4().hex}.json",
                       {"error": str(error), "type": type(error).__name__})
            cleanup(run)
            raise
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

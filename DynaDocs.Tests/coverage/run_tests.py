#!/usr/bin/env python3
"""Run dotnet test in a temporary git worktree.

Avoids DLL lock contention when multiple agents run tests concurrently.
Dirty/untracked files are copied into the worktree so the test run
reflects the current working state, not just the last commit.

Usage:
    python DynaDocs.Tests/coverage/run_tests.py                          # plain test run
    python DynaDocs.Tests/coverage/run_tests.py -- --filter Category=Unit  # with dotnet test args
"""

import argparse
import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import uuid
from contextlib import contextmanager
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent


def isolated_environment():
    """Disable implicit shell setup without mutating the caller's environment."""
    env = {key: value for key, value in os.environ.items() if not key.startswith("DYDO_")}
    env["SHELL"] = "dydo-test-no-shell"
    return env


def test_command(extra_args=None):
    arguments = list(extra_args or [])
    setting = "RunConfiguration.TreatNoTestsAsError=true"
    if any("treatnotestsaserror" in item.lower() and item != setting for item in arguments):
        raise ValueError("The isolated runner requires TreatNoTestsAsError=true")
    if "--" not in arguments:
        arguments.append("--")
    if setting not in arguments:
        arguments.append(setting)
    return ["dotnet", "test", "DynaDocs.sln", *arguments]


def _git(*args, capture=False):
    cmd = ["git", "-c", f"safe.directory={ROOT.resolve().as_posix()}", *args]
    if capture:
        result = subprocess.run(
            cmd, cwd=ROOT, capture_output=True, text=True,
            encoding="utf-8", errors="replace",
        )
        return result.stdout, result.returncode
    return subprocess.run(cmd, cwd=ROOT)


def create_worktree(path):
    """Create a detached worktree at the attributed path."""
    result = _git("worktree", "add", "--detach", str(path), "HEAD")
    if result.returncode != 0:
        print(f"Failed to create worktree at {path}", file=sys.stderr)
        return False
    return True


def is_registered_worktree(worktree):
    """Return whether Git records the exact attributed worktree path."""
    stdout, rc = _git("worktree", "list", "--porcelain", capture=True)
    if rc != 0:
        return False
    prefix = "worktree "
    return any(
        Path(line[len(prefix):]).resolve() == worktree.resolve()
        for line in stdout.splitlines() if line.startswith(prefix)
    )


def _inside(root, relative):
    target = (root / relative).resolve()
    if not relative or ".." in Path(relative).parts or not target.is_relative_to(root.resolve()):
        raise ValueError(f"Path escapes isolated worktree: {relative}")
    return target


def _dirty_entries(stdout):
    fields = iter(stdout.split("\0"))
    for field in fields:
        if not field:
            continue
        if len(field) < 4 or field[2] != " " or "U" in field[:2]:
            raise ValueError(f"Malformed or unresolved Git status: {field!r}")
        status, relative = field[:2], field[3:]
        old = None
        if "R" in status or "C" in status:
            old = next(fields, None)
            if not old:
                raise ValueError("Missing Git rename/copy origin")
        yield status, relative, old


def copy_dirty_files(worktree):
    """Copy an exact NUL-delimited dirty snapshot, including untracked files."""
    stdout, rc = _git("status", "--porcelain=v1", "-z", "--untracked-files=all", capture=True)
    if rc != 0:
        raise ValueError("Cannot read candidate Git status")
    for status, relative, old in _dirty_entries(stdout):
        dst = _inside(worktree, relative)
        if old and "R" in status:
            _inside(worktree, old).unlink(missing_ok=True)
        if "D" in status:
            dst.unlink(missing_ok=True)
            continue
        src = _inside(ROOT, relative)
        if not src.is_file():
            raise ValueError(f"Missing candidate source: {relative}")
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def remove_worktree(worktree):
    """Remove the worktree and its directory."""
    _git("worktree", "remove", "--force", str(worktree))
    # On Windows, dotnet may hold file handles briefly after exit
    if worktree.exists():
        for attempt in range(3):
            try:
                shutil.rmtree(str(worktree))
                break
            except OSError:
                if attempt < 2:
                    import time
                    time.sleep(1)
    if is_registered_worktree(worktree):
        _git("worktree", "remove", "--force", str(worktree))


@contextmanager
def defer_interruption():
    """Publish directory ownership before delivering a graceful interruption."""
    interrupted = False
    previous = {}

    def remember_interrupt(_signum, _frame):
        nonlocal interrupted
        interrupted = True

    try:
        for signum in [signal.SIGINT, *([signal.SIGBREAK] if sys.platform == "win32" else [])]:
            previous[signum] = signal.signal(signum, remember_interrupt)
        yield
    finally:
        while True:
            try:
                for signum, handler in previous.items():
                    signal.signal(signum, handler)
                break
            except KeyboardInterrupt:
                # Retry the whole restoration, including its loop bookkeeping.
                interrupted = True
        if interrupted:
            raise KeyboardInterrupt


def _campaign_identities(worktree, result_root):
    excluded = {"bin", "obj", ".git", "node_modules", ".local"}
    sources = [path for path in worktree.rglob("*.cs")
               if not excluded.intersection(path.relative_to(worktree).parts)]
    binaries = [
        worktree / "bin/Debug/net10.0/dydo.dll",
        worktree / "bin/Debug/net10.0/dydo.pdb",
        worktree / "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.dll",
        worktree / "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.pdb",
        worktree / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll",
        worktree / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.pdb",
    ]
    reports = [path for path in result_root.rglob("coverage*") if path.is_file()]
    return [*sources, *binaries, *reports]


def _publish_assurance(worktree, result_root, owner_output, assurance_output, owner_result):
    from csharp_coverage import snapshot_artifacts
    output = Path(assurance_output)
    if not output.is_absolute() or output.exists():
        raise ValueError("--assurance-output requires a new absolute owned directory")
    output.mkdir(parents=True)
    if result_root.is_dir():
        shutil.copytree(result_root, output / "raw")
    if owner_output.is_dir():
        shutil.copytree(owner_output, output / "owner")
    identities = snapshot_artifacts(worktree, _campaign_identities(worktree, result_root)) \
        if result_root.is_dir() else []
    manifest = {"schema": 1, "campaign": owner_result, "artifacts": identities}
    (output / "identities.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _run_assurance_campaign(worktree, extra_args, assurance_output):
    from windows_job import request, run
    result_root = worktree / "dydo/_system/.local/csharp-campaign-results"
    owner_output = worktree / f"dydo/_system/.local/csharp-campaign-owner-{uuid.uuid4().hex}"
    command = [sys.executable, str(worktree / "DynaDocs.Tests/coverage/csharp_coverage.py"),
               "--root", str(worktree), "--result-root", str(result_root),
               "--extra-json", json.dumps(list(extra_args or []))]
    owner_result = run(request(command, worktree, owner_output,
                               execution_seconds=1800, teardown_seconds=10),
                       isolated_environment())
    _publish_assurance(worktree, result_root, owner_output, assurance_output, owner_result)
    if not owner_result.get("complete") or not owner_result.get("cleanup_confirmed"):
        return 2
    return owner_result.get("subject_status") if type(owner_result.get("subject_status")) is int else 2


def run_tests(extra_args=None, assurance_output=None):
    """Create worktree, run tests, clean up. Returns the dotnet exit code."""
    worktree = None
    try:
        print("  Creating test worktree...")
        candidate = Path(tempfile.gettempdir()) / f"dydo-test-{uuid.uuid4().hex[:8]}"
        if candidate.exists() or is_registered_worktree(candidate):
            print(f"Failed to allocate test worktree path at {candidate}", file=sys.stderr)
            return 1
        with defer_interruption():
            try:
                candidate.mkdir()
            except OSError as exc:
                print(f"Failed to allocate test worktree path at {candidate}: {exc}", file=sys.stderr)
                return 1
            worktree = candidate
        if not create_worktree(worktree):
            return 1
        print(f"  Worktree: {worktree}")

        copy_dirty_files(worktree)

        if assurance_output:
            return _run_assurance_campaign(worktree, extra_args, assurance_output)

        cmd = test_command(extra_args)
        env = isolated_environment()
        print(f"  Running: {' '.join(cmd)}")
        # Tests that install Console.In must not inherit an attached host console: .NET's
        # Console.KeyAvailable probes the process handle instead of the installed reader.
        result = subprocess.run(cmd, cwd=worktree, env=env, stdin=subprocess.DEVNULL)

        return result.returncode
    finally:
        if worktree and (worktree.exists() or is_registered_worktree(worktree)):
            print("  Cleaning up worktree...")
            remove_worktree(worktree)


def main():
    if sys.platform == "win32":
        signal.signal(signal.SIGBREAK, signal.default_int_handler)
    parser = argparse.ArgumentParser(description="Run dotnet test in a git worktree")
    parser.add_argument("--assurance-output", help=argparse.SUPPRESS)
    args, extra = parser.parse_known_args()

    # Strip leading "--" separator if present
    if extra and extra[0] == "--":
        extra = extra[1:]

    print("\n--- Running tests (worktree-isolated) ---")
    try:
        rc = run_tests(extra_args=extra or None, assurance_output=args.assurance_output)
    except KeyboardInterrupt:
        rc = 130

    if rc != 0:
        print(f"\n  Tests failed (exit code {rc})")
    else:
        print("\n  Tests passed")

    sys.exit(rc)


if __name__ == "__main__":
    main()

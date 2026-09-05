#!/usr/bin/env python3
"""Run dotnet test in a temporary git worktree.

Avoids DLL lock contention when multiple agents run tests concurrently.
Dirty/untracked files are copied into the worktree so the test run
reflects the current working state, not just the last commit.

Usage:
    python DynaDocs.Tests/coverage/run_tests.py                          # plain test run
    python DynaDocs.Tests/coverage/run_tests.py -- --filter Category=Unit  # with dotnet test args
    python DynaDocs.Tests/coverage/run_tests.py --coverage               # copy coverage XMLs back
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
COVERAGE_XML_GLOB = "DynaDocs.Tests/**/coverage.cobertura.xml"


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
    cmd = ["git", *args]
    if capture:
        result = subprocess.run(
            cmd, cwd=ROOT, capture_output=True, text=True,
            encoding="utf-8", errors="replace",
        )
        return result.stdout, result.returncode
    return subprocess.run(cmd, cwd=ROOT)


def create_worktree():
    """Create a detached worktree at a temp path. Returns the path."""
    name = f"dydo-test-{uuid.uuid4().hex[:8]}"
    path = Path(tempfile.gettempdir()) / name
    result = _git("worktree", "add", "--detach", str(path), "HEAD")
    if result.returncode != 0:
        print(f"Failed to create worktree at {path}", file=sys.stderr)
        return None
    return path


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


def copy_coverage_back(worktree):
    """Copy coverage XML files from worktree back to the main tree."""
    copied = 0
    for xml in worktree.glob(COVERAGE_XML_GLOB):
        rel = xml.relative_to(worktree)
        dst = ROOT / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(str(xml), str(dst))
        copied += 1
    if copied:
        print(f"  Copied {copied} coverage XML(s) back to main tree")


def remove_worktree(worktree):
    """Remove the worktree and its directory."""
    _git("worktree", "remove", "--force", str(worktree))
    # On Windows, dotnet may hold file handles briefly after exit
    if worktree.exists():
        for attempt in range(3):
            try:
                shutil.rmtree(str(worktree))
                return
            except OSError:
                if attempt < 2:
                    import time
                    time.sleep(1)


def run_tests(extra_args=None, coverage=False):
    """Create worktree, run tests, clean up. Returns the dotnet exit code."""
    worktree = None
    try:
        print(f"  Creating test worktree...")
        worktree = create_worktree()
        if worktree is None:
            return 1
        print(f"  Worktree: {worktree}")

        copy_dirty_files(worktree)

        cmd = test_command(extra_args)

        # Strip dydo env vars so tests run in a clean environment
        env = isolated_environment()

        print(f"  Running: {' '.join(cmd)}")
        result = subprocess.run(cmd, cwd=worktree, env=env)

        if coverage:
            copy_coverage_back(worktree)

        return result.returncode
    finally:
        if worktree and worktree.exists():
            print(f"  Cleaning up worktree...")
            remove_worktree(worktree)


def main():
    parser = argparse.ArgumentParser(description="Run dotnet test in a git worktree")
    parser.add_argument(
        "--coverage", action="store_true",
        help="Copy coverage XML files back to the main tree after the run",
    )
    args, extra = parser.parse_known_args()

    # Strip leading "--" separator if present
    if extra and extra[0] == "--":
        extra = extra[1:]

    print("\n--- Running tests (worktree-isolated) ---")
    rc = run_tests(extra_args=extra or None, coverage=args.coverage)

    if rc != 0:
        print(f"\n  Tests failed (exit code {rc})")
    else:
        print(f"\n  Tests passed")

    sys.exit(rc)


if __name__ == "__main__":
    main()

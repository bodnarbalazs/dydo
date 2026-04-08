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


def _git(*args, capture=False):
    cmd = ["git", *args]
    if capture:
        result = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
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


def copy_dirty_files(worktree):
    """Copy modified, added, and untracked files into the worktree."""
    stdout, rc = _git("status", "--porcelain", capture=True)
    if rc != 0:
        return

    for line in stdout.splitlines():
        if len(line) < 4:
            continue
        status = line[:2]
        filepath = line[3:].strip()

        # Handle renames: "R  old -> new"
        if " -> " in filepath:
            filepath = filepath.split(" -> ", 1)[1]

        # Deleted files: remove from worktree
        if status.strip() in ("D", "DD"):
            target = worktree / filepath
            if target.exists():
                target.unlink()
            continue

        # Everything else: copy to worktree
        src = ROOT / filepath
        dst = worktree / filepath
        if src.is_file():
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(str(src), str(dst))


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

        cmd = ["dotnet", "test", "DynaDocs.sln"]
        if extra_args:
            cmd.extend(extra_args)

        # Strip dydo env vars so tests run in a clean environment
        env = {k: v for k, v in os.environ.items() if not k.startswith("DYDO_")}

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

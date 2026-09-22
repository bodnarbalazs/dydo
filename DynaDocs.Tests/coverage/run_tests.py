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
import math
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import uuid
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

# Identifies a worktree this runner created, so cleanup and startup pruning never act on a
# directory by name alone. Never scanned by source-inventory, static or coverage collectors: those
# read ROOT, not a temporary worktree, and this file lives only inside the snapshot.
MARKER_NAME = ".dydo-test-worktree.json"
MARKER_SCHEMA = 1

# A caller overrides this by setting the DYDO_TEST_WORKTREE_STALE_SECONDS environment variable
# before invoking run_tests.py (isolated_environment strips DYDO_-prefixed variables only from the
# dotnet-test child, never from this process's own view of its environment).
STALE_TEST_WORKTREE_SECONDS = 4 * 60 * 60
STALE_WORKTREE_AGE_ENV = "DYDO_TEST_WORKTREE_STALE_SECONDS"


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


def _prune_empty_directories(worktree, directories):
    """Remove directories a rename/deletion emptied, so a bulk `git mv` leaves no stale
    directory names behind in the isolated worktree (git status reports file moves, not
    directory removals, so nothing else prunes them)."""
    resolved_worktree = worktree.resolve()
    for directory in directories:
        current = directory
        while current != resolved_worktree and current.is_dir() and not any(current.iterdir()):
            parent = current.parent
            current.rmdir()
            current = parent


def copy_dirty_files(worktree):
    """Copy an exact NUL-delimited dirty snapshot, including untracked files."""
    stdout, rc = _git("status", "--porcelain=v1", "-z", "--untracked-files=all", capture=True)
    if rc != 0:
        raise ValueError("Cannot read candidate Git status")
    emptied_directories = set()
    for status, relative, old in _dirty_entries(stdout):
        dst = _inside(worktree, relative)
        if old and "R" in status:
            old_path = _inside(worktree, old)
            old_path.unlink(missing_ok=True)
            emptied_directories.add(old_path.parent)
        if "D" in status:
            dst.unlink(missing_ok=True)
            emptied_directories.add(dst.parent)
            continue
        src = _inside(ROOT, relative)
        if not src.is_file():
            raise ValueError(f"Missing candidate source: {relative}")
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
    _prune_empty_directories(worktree, emptied_directories)


def _is_link_entry(entry):
    """True for a symlink or a Windows directory junction."""
    return entry.is_symlink() or (hasattr(entry, "is_junction") and entry.is_junction())


def _relative_if_inside(candidate, base):
    """Return candidate's path relative to base if candidate is base or nested under it, else None."""
    candidate_norm = os.path.normcase(str(candidate))
    base_norm = os.path.normcase(str(base))
    if candidate_norm == base_norm:
        return Path(".")
    if not candidate_norm.startswith(base_norm + os.sep):
        return None
    return Path(str(candidate)[len(str(base)) + 1:])


def _create_skill_link(link_path, target_path):
    """Create a link matching what setup-skills.mjs produces on this platform: a directory
    junction on win32 (os.symlink needs elevated privilege there), a directory symlink elsewhere.
    """
    link_path.parent.mkdir(parents=True, exist_ok=True)
    if link_path.exists() or _is_link_entry(link_path):
        raise ValueError(f"Refusing to replace an existing path while materializing a skill link: {link_path}")
    if sys.platform == "win32":
        import _winapi
        _winapi.CreateJunction(str(target_path), str(link_path))
    else:
        os.symlink(target_path, link_path, target_is_directory=True)


def _foreign_link_sentinel(worktree):
    """A real directory inside the snapshot but outside `<snapshot>/skills`, used as the resolution
    target for a link whose real target lies outside skills/ -- the exact DR 049 violation shape
    the guard exists to catch. Never materialize a link that resolves to the real host path outside
    the snapshot: `remove_worktree` runs `git worktree remove --force` over the snapshot, and git
    follows a directory junction and deletes what it points at, so a junction aimed at real host
    data would let cleanup destroy it. Aiming at this sentinel instead keeps the guard's containment
    check failing (the sentinel is outside `<snapshot>/skills`) and its message still names the
    entry, while everything the link touches dies with the snapshot itself.
    """
    sentinel = worktree / ".dydo-foreign-skill-sentinel"
    sentinel.mkdir(parents=True, exist_ok=True)
    return sentinel


def _mirror_host_skill_root(worktree, relative):
    """Reproduce one host skill discovery directory inside the snapshot, preserving each entry's
    kind. A link resolving inside the source root's skills/ is rebased onto the snapshot's own
    skills/, so the guard's containment check is about the snapshot. A link resolving outside
    skills/ is rebased onto a sentinel directory inside the snapshot (see `_foreign_link_sentinel`),
    so it still fails the guard's containment check without ever aiming at real host data. Any other
    entry (a real directory or a real file) is reproduced as-is, so a DR 049 violation on the host
    still fails the guard inside the snapshot.
    """
    canonical_skills = Path(os.path.realpath(ROOT / "skills"))
    source = ROOT / relative
    for entry in sorted(source.iterdir(), key=lambda item: item.name):
        dest = worktree / relative / entry.name
        if _is_link_entry(entry):
            final_target = Path(os.path.realpath(entry))
            offset = _relative_if_inside(final_target, canonical_skills)
            rebased_target = (worktree / "skills" / offset) if offset is not None \
                else _foreign_link_sentinel(worktree)
            _create_skill_link(dest, rebased_target)
        elif entry.is_dir():
            shutil.copytree(entry, dest)
        elif entry.is_file():
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(entry, dest)
        else:
            raise ValueError(f"Cannot materialize unsupported skill discovery entry: {entry}")


def _canonical_skill_names(canonical_skills):
    """Walk `<snapshot>/skills` by setup-skills.mjs's own walk rule exactly: a directory holding a
    `SKILL.md` is a skill (its own subdirectories are never walked), and any other directory is a
    category to walk into, at any depth. A category with no subdirectory at all, or a skill name
    reached through two categories, is a boundary error the same way setup-skills.mjs treats it,
    never a silent drop.
    """
    names = []
    seen = {}

    def walk(category_root):
        entries = sorted(item.name for item in category_root.iterdir() if item.is_dir())
        if not entries:
            raise ValueError(f"Cannot project skill discovery directories: no canonical skills found in {category_root}")
        category = category_root.relative_to(canonical_skills).as_posix()
        for entry in entries:
            source = category_root / entry
            if not (source / "SKILL.md").is_file():
                walk(source)
                continue
            if entry in seen:
                raise ValueError(
                    f"Cannot project skill discovery directories: duplicate skill name across categories: "
                    f"{entry} ({seen[entry]} and {category})")
            seen[entry] = category
            names.append((entry, source))

    walk(canonical_skills)
    return names


def _project_canonical_skill_links(worktree, relative):
    """Project one flat link per canonical skill -- not per category -- from the snapshot's own
    skills/ tree, for use when the source root has no host discovery directory to mirror (for
    example, this very worktree, which has no .claude/skills installed).

    `<snapshot>/skills` is a category tree (`skills/<category>/<skill>/`, where a category may
    itself be nested, as in `roles/crew`): projecting a link per direct child of `skills/` would
    link the category directories themselves, not the skills inside them, which the
    CanonicalSkillTree_HasNoAuthoredHostCopies guard cannot tell apart from a real per-skill
    projection -- it only checks each entry is a link resolving inside `skills/`. Walking by the
    SKILL.md rule, the same way setup-skills.mjs does, keeps the projection an actual host
    discovery shape.

    A snapshot with no `skills/` tree at all has no canonical skills to project and no host
    discovery directory to mirror: there is nothing the CanonicalSkillTree_HasNoAuthoredHostCopies
    guard could catch either way, so this is a quiet no-op rather than a failure. A snapshot that
    *has* `skills/` but finds nothing usable inside it (empty, a category with nothing usable inside
    it, or a duplicate skill name) stays loud: that shape could otherwise widen into the vacuous
    state the guard cannot detect.
    """
    canonical_skills = worktree / "skills"
    if not canonical_skills.is_dir():
        return
    for name, source in _canonical_skill_names(canonical_skills):
        _create_skill_link(worktree / relative / name, source)


def materialize_skill_links(worktree):
    """Materialize the gitignored host skill discovery directories (.claude/skills, .agents/skills)
    inside the snapshot, so CanonicalSkillTree_HasNoAuthoredHostCopies is not vacuous there.

    copy_dirty_files never copies these directories: they are gitignored, so `git status` never
    reports them as dirty or untracked. Without this, the guard's `if (!Directory.Exists(root))
    continue;` skips its whole link-resolution half under the isolated runner, and only its
    `git ls-files`-based half still runs against the real snapshot worktree.

    Materialization must fail loudly: any error here aborts the run rather than silently leaving
    the vacuous state the guard cannot detect.
    """
    for relative in (".claude/skills", ".agents/skills"):
        try:
            if (ROOT / relative).is_dir():
                _mirror_host_skill_root(worktree, relative)
            else:
                _project_canonical_skill_links(worktree, relative)
        except Exception as exc:
            raise ValueError(f"Failed to materialize skill discovery directory {relative}: {exc}") from exc


def _stale_worktree_max_age_seconds():
    """Read the stale-worktree age override, or fall back to the default when it is unset. An
    override that is present but unparseable, non-finite, or non-positive is a boundary error,
    never a silent fallback: it fails loudly, naming the offending value. `nan` compares false
    against every age, so it would silently prune every marked worktree, including a concurrent
    runner's live one; `inf` compares true against every age, so it would silently disable pruning
    altogether. Neither reads as a deliberate "positive number of seconds", so both are rejected
    rather than given special meaning.
    """
    override = os.environ.get(STALE_WORKTREE_AGE_ENV)
    if not override:
        return STALE_TEST_WORKTREE_SECONDS
    try:
        value = float(override)
    except ValueError:
        raise ValueError(
            f"{STALE_WORKTREE_AGE_ENV} must be a number, got {override!r}") from None
    if not math.isfinite(value):
        raise ValueError(
            f"{STALE_WORKTREE_AGE_ENV} must be a finite number of seconds, got {override!r}")
    if value <= 0:
        raise ValueError(
            f"{STALE_WORKTREE_AGE_ENV} must be a positive number of seconds, got {override!r}")
    return value


def write_worktree_marker(worktree, created_at=None):
    """Record that this runner owns `worktree`, before any test or campaign runs in it. The marker
    -- not the dydo-test- name alone -- is what later identifies a runner-created worktree.
    """
    stamp = created_at or datetime.now(timezone.utc)
    marker = {
        "schema": MARKER_SCHEMA,
        "pid": os.getpid(),
        "createdAtUtc": stamp.strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    (worktree / MARKER_NAME).write_text(json.dumps(marker), encoding="utf-8")


def _read_worktree_marker(path):
    """Return the marker's recorded creation time, or None when the marker is absent or invalid."""
    try:
        data = json.loads((path / MARKER_NAME).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    if not isinstance(data, dict) or data.get("schema") != MARKER_SCHEMA:
        return None
    created = data.get("createdAtUtc")
    if not isinstance(created, str):
        return None
    try:
        return datetime.strptime(created, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError:
        return None


def remove_worktree(worktree):
    """Remove the worktree and its directory, on any exit path. Unlocks before forcing removal --
    an unlock on an already-unlocked worktree fails harmlessly and is ignored -- and always finishes
    with a prune so a removed directory does not linger registered. A removal that still does not
    succeed is reported loudly on stderr, naming the path, rather than failing the caller's result.
    """
    _git("worktree", "unlock", str(worktree), capture=True)
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
    _git("worktree", "prune")
    if worktree.exists() or is_registered_worktree(worktree):
        print(f"Failed to remove test worktree, a human must clear it: {worktree}", file=sys.stderr)


def _registered_worktree_paths():
    """Return the resolved paths `git worktree list --porcelain` reports for this repository, or an
    empty list when the command fails. This is the only source of pruning candidates: the runner
    registered its own worktrees there, so nothing here ever scans a directory."""
    stdout, rc = _git("worktree", "list", "--porcelain", capture=True)
    if rc != 0:
        return []
    prefix = "worktree "
    return [Path(line[len(prefix):]).resolve()
            for line in stdout.splitlines() if line.startswith(prefix)]


def _is_stale_test_worktree(path, temp_root, now, max_age_seconds):
    """True when `path` is a marked dydo-test-* worktree under temp_root whose recorded creation
    time is older than max_age_seconds -- never by name alone, never without a marker, never a
    young one."""
    if not (path.name.startswith("dydo-test-") and path.is_relative_to(temp_root)):
        return False
    created = _read_worktree_marker(path)
    if created is None:
        return False
    return (now - created).total_seconds() >= max_age_seconds


def prune_stale_test_worktrees(max_age_seconds=None):
    """Remove dydo-test-* worktrees under the system temp directory that a previous runner marked
    and abandoned. Candidates come only from `git worktree list --porcelain` -- the runner
    registered them there -- filtered by the temp-root and dydo-test- name predicates, then gated
    by a valid marker whose age exceeds max_age_seconds. NEVER a directory scan of the temp root:
    no iterdir, glob, scandir or listdir over it. A real machine's TEMP can hold well over 100,000
    unrelated entries, and enumerating it cost seconds on every run.

    This deliberately no longer prunes a leftover directory that git no longer registers as a
    worktree. Each run allocates a fresh `dydo-test-<uuid>` name, so this process never revisits or
    names an orphan it left behind; a crashed run, a re-cloned checkout, or a second clone whose
    `git worktree list` this runner never sees can all leave one that now lingers in TEMP with no
    automated path back to it. `remove_worktree` still names its own failed removals loudly on
    stderr, but only for the worktree the current run is cleaning up -- not for one an earlier run
    abandoned. Any such orphan is left for a human. The cost of a full TEMP scan to also catch that
    rare case is not worth paying on every run. Silent on a no-op.
    """
    if max_age_seconds is None:
        max_age_seconds = _stale_worktree_max_age_seconds()
    temp_root = Path(tempfile.gettempdir()).resolve()
    now = datetime.now(timezone.utc)
    pruned = []
    for path in _registered_worktree_paths():
        if _is_stale_test_worktree(path, temp_root, now, max_age_seconds):
            print(f"Pruning stale test worktree: {path}", file=sys.stderr)
            remove_worktree(path)
            pruned.append(path)
    return pruned


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
        try:
            prune_stale_test_worktrees()
        except Exception as exc:
            print(f"Failed to prune stale test worktrees: {exc}", file=sys.stderr)

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
        write_worktree_marker(worktree)

        copy_dirty_files(worktree)
        materialize_skill_links(worktree)

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

"""Canonical maintained-input identities shared by assurance gates."""
import hashlib
import json
import subprocess
from pathlib import Path, PurePosixPath


def canonical_path(relative):
    if not isinstance(relative, str) or not relative:
        raise ValueError("inventory path must be a nonempty string")
    path = PurePosixPath(relative)
    if path.is_absolute() or path.as_posix() != relative or ".." in path.parts or "\\" in relative or ":" in relative:
        raise ValueError(f"noncanonical inventory path: {relative}")
    return relative


def _unique(paths):
    result = []
    folded = set()
    for value in sorted(paths):
        relative = canonical_path(value)
        identity = relative.casefold()
        if identity in folded:
            raise ValueError(f"duplicate inventory path: {relative}")
        folded.add(identity)
        result.append(relative)
    return result


def source_path(root, relative):
    root = Path(root).resolve()
    relative = canonical_path(relative)
    path = (root / relative).resolve()
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError(f"missing or outside source path: {relative}")
    current = root
    for part in PurePosixPath(relative).parts:
        if part not in {child.name for child in current.iterdir()}:
            raise ValueError(f"incorrect source path case: {relative}")
        current /= part
    return path


def checked_paths(root, paths):
    for relative in _unique(paths):
        yield relative, source_path(root, relative)


def language_of(path):
    path = Path(path)
    language = {".cs": "cs", ".py": "python", ".js": "javascript",
                ".cjs": "javascript", ".mjs": "javascript"}.get(path.suffix.lower())
    if language or path.suffix:
        return language
    with path.open(encoding="utf-8", errors="replace") as stream:
        return "javascript" if stream.readline().strip() in ("#!/usr/bin/env node", "#!/usr/bin/node") else None


def fingerprint(root, paths):
    digest = hashlib.sha256()
    for relative, path in checked_paths(root, paths):
        digest.update(relative.encode("utf-8") + b"\0")
        digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def build_file_rows(root, paths, deleted=frozenset()):
    root = Path(root).resolve()
    deleted = {canonical_path(path) for path in deleted}
    rows = []
    for relative in _unique(paths):
        path = (root / relative).resolve()
        if not path.is_relative_to(root):
            raise ValueError(f"inventory path escapes root: {relative}")
        if relative in deleted:
            if path.exists():
                raise ValueError(f"deleted inventory path still exists: {relative}")
            rows.append({"path": relative, "sha256": None, "deleted": True})
        elif path.is_file():
            rows.append({"path": relative, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        else:
            raise ValueError(f"missing inventory path: {relative}")
    return rows


def source_fingerprint(rows):
    encoded = json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def git_file_state(root):
    """Return tracked/untracked paths plus explicitly absent tracked deletions."""
    root = Path(root).resolve()
    safe = ["git", "-c", f"safe.directory={root.as_posix()}"]
    tracked = subprocess.run([*safe, "ls-files", "-z"], cwd=root, check=True,
                             capture_output=True).stdout.decode("utf-8").split("\0")
    untracked = subprocess.run([*safe, "ls-files", "--others", "--exclude-standard", "-z"],
                               cwd=root, check=True, capture_output=True).stdout.decode("utf-8").split("\0")
    tracked = [path for path in tracked if path]
    untracked = [path for path in untracked if path]
    deleted = {path for path in tracked if not (root / path).is_file()}
    return _unique([*tracked, *untracked]), deleted

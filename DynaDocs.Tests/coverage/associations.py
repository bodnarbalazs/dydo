"""Validation for schema-1 exact source-to-test-file intent."""
from pathlib import PurePosixPath


def _path(value):
    if not isinstance(value, str) or not value or "*" in value or "?" in value:
        raise ValueError("association paths must be exact nonempty strings")
    path = PurePosixPath(value)
    if path.is_absolute() or path.as_posix() != value or ".." in path.parts or "\\" in value or ":" in value:
        raise ValueError(f"noncanonical association path: {value}")
    return value


def _manifest_modules(manifest):
    if not isinstance(manifest, dict) or set(manifest) != {"schema", "modules"} or manifest["schema"] != 1 or isinstance(manifest["schema"], bool):
        raise ValueError("association manifest must use closed schema 1")
    if not isinstance(manifest["modules"], list):
        raise ValueError("association modules must be an array")
    return manifest["modules"]


def _associated_tests(row, targets, tests, related):
    if not isinstance(row, dict) or set(row) != {"module", "tests"} or not isinstance(row["tests"], list) or not row["tests"]:
        raise ValueError("association rows require exactly module and nonempty tests")
    module = _path(row["module"])
    if module not in targets or module in related:
        raise ValueError(f"unknown or duplicate associated module: {module}")
    associated = [_path(path) for path in row["tests"]]
    if len({path.casefold() for path in associated}) != len(associated):
        raise ValueError(f"duplicate associated test for {module}")
    if any(path not in tests for path in associated):
        raise ValueError(f"unknown or non-test association for {module}")
    related.add(module)


def validate_associations(inventory, manifest):
    modules = _manifest_modules(manifest)
    sources = {row["path"]: row for row in inventory}
    targets = {path for path, row in sources.items() if row.get("role") == "target"}
    required_targets = {path for path in targets if sources[path].get("executable")}
    tests = {path for path, row in sources.items() if row.get("role") == "test" and row.get("nativeTest", True)}
    related = set()
    for row in modules:
        _associated_tests(row, targets, tests, related)
    return [{"gate": "test-association", "path": path,
             "reason": "non-trivial target has no associated test file"}
            for path in sorted(required_targets - related)]

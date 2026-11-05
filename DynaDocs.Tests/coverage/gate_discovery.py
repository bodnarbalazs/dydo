"""Shared native test-file discovery for source inventory and coverage union."""
import ast
import re
from pathlib import Path


def _unittest_discovery(path, relative):
    if not (relative.endswith(".py") and Path(relative).name.startswith("test_")):
        return None
    try:
        tree = ast.parse(path.read_text(encoding="utf-8-sig"), relative)
    except (SyntaxError, OSError):
        return None
    bases = (base for node in ast.walk(tree) if isinstance(node, ast.ClassDef) for base in node.bases)
    if any(getattr(base, "attr", getattr(base, "id", "")) == "TestCase" for base in bases):
        return {"id": relative + "#unittest", "file": relative}
    return None


def _node_discovery(path, relative):
    if not re.search(r"\.test\.(?:c|m)?js$", relative):
        return None
    text = path.read_text(encoding="utf-8-sig")
    if re.search(r"\btest\s*\(", text) and "node:test" in text:
        return {"id": relative + "#node-test", "file": relative}
    return None


def _vitest_discovery(path, relative):
    if not (relative.startswith("viewer/src/") and re.search(r"\.test\.tsx?$", relative)):
        return None
    text = path.read_text(encoding="utf-8-sig")
    if re.search(r"\b(?:test|it)\s*\(", text) and re.search(r"""\bfrom\s+['"]vitest['"]""", text):
        return {"id": relative + "#vitest", "file": relative}
    return None


def ordinary_discovery(root, paths):
    rows = []
    for relative in paths:
        path = root / relative
        if path.is_file():
            row = (_unittest_discovery(path, relative) or _node_discovery(path, relative)
                   or _vitest_discovery(path, relative))
            if row:
                rows.append(row)
    return rows

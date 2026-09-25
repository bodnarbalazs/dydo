"""Istanbul LCOV joined to the TypeScript walker's callables, one policy module per source."""
from pathlib import Path


def _record_line(record, tag, value):
    if tag == "DA":
        line, hits = value.split(",")[:2]
        record["lines"][int(line)] = int(hits)
    elif tag == "BRDA":
        line, block, branch, taken = value.split(",")
        record["branches"][f"{line}:{block}:{branch}"] = 0 if taken == "-" else int(taken)
    elif tag == "FN":
        line, name = value.split(",", 1)
        record["functions"].setdefault(int(line), []).append(name)
    elif tag == "FNDA":
        hits, name = value.split(",", 1)
        record["calls"][name] = int(hits)


def parse_lcov(text, root, base):
    """Records keyed by the repository path of each SF; LCOV names sources relative to `base`."""
    records, current = {}, None
    for raw in text.splitlines():
        tag, _, value = raw.strip().partition(":")
        if tag == "SF":
            path = (Path(base) / value).resolve().relative_to(Path(root).resolve()).as_posix()
            if path in records:
                raise ValueError(f"Duplicate Istanbul LCOV record: {path}")
            current = records[path] = {"lines": {}, "branches": {}, "functions": {}, "calls": {}}
        elif tag == "end_of_record":
            current = None
        elif current is not None:
            _record_line(current, tag, value)
    return records


def _line_owners(methods, lines):
    """Each line belongs to the innermost callable spanning it, as SonarJS scores each apart."""
    owners = {}
    for line in lines:
        spanning = [method for method in methods if method["line"] <= line <= method["end_line"]]
        if spanning:
            owners[line] = min(spanning, key=lambda method: method["end"] - method["start"])["id"]
    return owners


def _call_count(method, record):
    """Istanbul's own function counter, when exactly one function starts on the callable's line."""
    names = record["functions"].get(method["line"], [])
    return record["calls"].get(names[0], 0) if len(names) == 1 else None


def _measured(method, record, owners):
    lines, calls = record["lines"], _call_count(method, record)
    points = [hits for line, hits in lines.items() if owners.get(line) == method["id"]]
    points = points or [hits for line, hits in lines.items() if method["line"] <= line <= method["end_line"]]
    if not points and calls is None:
        raise ValueError(f"No unambiguous Istanbul LCOV point for callable {method['id']}")
    # A line is hit by any code on it; a callable its counter never entered covered none of its own.
    points = points or [calls]
    return {"id": method["id"], "line": method["line"], "cc": method["cc"],
            "cognitive": method["cognitive"], "parameters": method["parameters"],
            "constructor": method["constructor"],
            "covered": 0 if calls == 0 else sum(hits > 0 for hits in points), "total": len(points)}


def _module_method(walked, record, owners):
    """Top-level statements are the module's own callable, as Python's module entry is."""
    points = [hits for line, hits in record["lines"].items() if line not in owners]
    if not points:
        return []
    return [{"id": "<module>", "line": 1, "cc": walked["module"]["cc"],
             "cognitive": walked["module"]["cognitive"], "parameters": 0, "constructor": False,
             "covered": sum(hits > 0 for hits in points), "total": len(points)}]


def join_module(path, record, walked):
    """A module absent from LCOV is declarative only when the walker proves nothing in it runs."""
    if record is None:
        if walked["runtime"]:
            raise ValueError(f"Istanbul LCOV has no record for executable module: {path}")
        return {"path": path, "language": "typescript", "executable": False,
                "lines": {}, "branches": {}, "methods": []}
    owners = _line_owners(walked["methods"], record["lines"])
    return {"path": path, "language": "typescript", "executable": bool(record["lines"]),
            "lines": {str(line): hits for line, hits in record["lines"].items()},
            "branches": record["branches"],
            "methods": [*(_measured(method, record, owners) for method in walked["methods"]),
                        *_module_method(walked, record, owners)]}


def join(lcov, root, base, targets, walked):
    """Every target joined, in order; `walked` maps each target to its walker row."""
    records = parse_lcov(Path(lcov).read_text(encoding="utf-8"), root, base)
    return [join_module(path, records.get(path), walked[path]) for path in targets]

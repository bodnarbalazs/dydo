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
        record["calls"].setdefault(name, []).append(int(hits))


def parse_lcov(text, root, base):
    """Records keyed by the repository path of each SF; LCOV names sources relative to `base`.
    A source outside the repository, such as a managed tool a test loads, can be no target."""
    records, current, root = {}, None, Path(root).resolve()
    for raw in text.splitlines():
        tag, _, value = raw.strip().partition(":")
        if tag == "SF":
            source = (Path(base) / value).resolve()
            if not source.is_relative_to(root):
                current = None
                continue
            path = source.relative_to(root).as_posix()
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
    """Istanbul's own function counter, when exactly one function starts on the callable's line.
    Node's reporter can name two functions alike; their FNDA counters follow their FN lines' order."""
    names = record["functions"].get(method["line"], [])
    if len(names) != 1:
        return None
    declared = [line for line, others in record["functions"].items() for other in others if other == names[0]]
    counts = record["calls"].get(names[0], [])
    position = declared.index(method["line"])
    return counts[position] if position < len(counts) else 0


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


def _istanbul_lines(coverage):
    lines = {}
    for key, span in coverage['statementMap'].items():
        line = str(span['start']['line'])
        lines[line] = max(lines.get(line, 0), coverage['s'][key])
    return lines


def _istanbul_method(method, coverage, lines, owners):
    body = {line: hits for line, hits in lines.items() if owners.get(int(line)) == method['id']}
    body = body or {line: hits for line, hits in lines.items()
                    if method['line'] <= int(line) <= method['end_line']}
    functions = [key for key, fn in coverage['fnMap'].items() if fn['loc'] == method['body']]
    if len(functions) > 1:
        raise ValueError(f"Ambiguous Istanbul callable: {method['id']}")
    if functions:
        calls = coverage['f'][functions[0]]
        body = body or {str(method['line']): calls}
        if not calls:
            body = {line: 0 for line in body}
    if not body:
        raise ValueError(f"No Istanbul body points for callable: {method['id']}")
    return {key: method[key] for key in ('id', 'line', 'end_line', 'cc', 'cognitive', 'parameters', 'constructor')} | {
        'kind': 'callable', 'body_lines': body, 'total': len(body),
        'covered': sum(hits > 0 for hits in body.values())}


def join_istanbul(path, coverage, walked):
    """Source-derived Istanbul statements, branches and bodies joined to the existing TS metrics."""
    lines = _istanbul_lines(coverage)
    owners = _line_owners(walked['methods'], [int(line) for line in lines])
    methods = [_istanbul_method(method, coverage, lines, owners) for method in walked['methods']]
    # An empty function still has a real execution counter and remains in the partition.
    for method in methods:
        for line, hits in method['body_lines'].items():
            lines[line] = max(lines.get(line, 0), hits)
    owners = _line_owners(walked['methods'], [int(line) for line in lines])
    body = {line: hits for line, hits in lines.items() if int(line) not in owners}
    if body:
        methods.append({'id': '<module>', 'kind': 'module', 'line': 1,
                        'end_line': max(map(int, lines)), **walked['module'],
                        'parameters': 0, 'constructor': False, 'body_lines': body,
                        'total': len(body), 'covered': sum(hits > 0 for hits in body.values())})
    branches = {f"{branch['line']}:{key}:{index}": hits
                for key, branch in coverage['branchMap'].items()
                for index, hits in enumerate(coverage['b'][key])}
    return {'path': path, 'language': 'typescript', 'executable': bool(lines),
            'lines': lines, 'branches': branches, 'methods': methods}


def merge_istanbul(denominator, reports, root):
    """Only counters may differ from the source-derived maps; unloaded sources retain zero hits."""
    import copy
    merged = copy.deepcopy(denominator)
    root = Path(root).resolve()
    for report in reports:
        for absolute, coverage in report.items():
            path = Path(absolute).resolve().relative_to(root).as_posix()
            if path not in merged:
                raise ValueError(f'Istanbul source outside target inventory: {path}')
            target = merged[path]
            for field in ('statementMap', 'fnMap', 'branchMap'):
                if coverage[field] != target[field]:
                    raise ValueError(f'Istanbul source denominator drift: {path}')
            for field in ('s', 'f', 'b'):
                _merge_counters(target[field], coverage[field])
    return merged


def _merge_counters(target, measured):
    if set(target) != set(measured):
        raise ValueError('Incomplete Istanbul counter inventory')
    for key, value in measured.items():
        if isinstance(target[key], list):
            if not isinstance(value, list) or len(value) != len(target[key]):
                raise ValueError('Incomplete Istanbul branch counters')
            left = dict(enumerate(target[key]))
            _merge_counters(left, dict(enumerate(value)))
            target[key] = list(left.values())
        else:
            if type(value) is not int or value < 0:
                raise ValueError('Invalid Istanbul counter')
            target[key] += value

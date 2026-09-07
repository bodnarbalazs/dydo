"""Exact emitted-signature coverage joins with portable-PDB source witnesses."""
import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path

from positions import contains


def _count(value, context):
    if type(value) is not int or value < 0:
        raise ValueError(f"Invalid {context}: {value!r}")
    return value


def _relative(root, path):
    try:
        return Path(path).resolve().relative_to(root.resolve()).as_posix()
    except ValueError as error:
        raise ValueError(f"Coverage source outside inventory: {path}") from error


def coverage_methods(data, opencover, root, assembly_name):
    """Join JSON hits and OpenCover CC by module + complete Cecil signature."""
    modules = ET.fromstring(opencover).findall("./Modules/Module")
    selected = [node for node in modules if node.findtext("ModuleName") == assembly_name]
    if len(selected) != 1:
        raise ValueError(f"Missing or ambiguous complexity module: {assembly_name}")
    xml_files = {row.attrib['uid']: _relative(root, row.attrib['fullPath']) for row in selected[0].findall('./Files/File')}
    complexity = {}
    for method in selected[0].findall("./Classes/Class/Methods/Method"):
        identity = method.findtext("Name")
        file_ref = method.find('FileRef')
        path = xml_files.get(file_ref.attrib['uid']) if file_ref is not None else None
        key = identity, path
        if not identity or key in complexity:
            raise ValueError("Missing or duplicate full complexity method identity")
        complexity[key] = _count(int(method.attrib["cyclomaticComplexity"]), "cyclomatic complexity")
    module = data.get(assembly_name + ".dll")
    if not isinstance(module, dict) or not module:
        raise ValueError(f"Missing JSON coverage module: {assembly_name}")
    result = {}
    for filename, classes in module.items():
        path = _relative(root, filename)
        for methods in classes.values():
            for identity, facts in methods.items():
                key = identity, path
                if key not in complexity:
                    raise ValueError(f"Missing exact complexity method: {identity}")
                if complexity[key] != max(1, len(facts['Branches'])):
                    raise ValueError(f'Pinned Coverlet complexity disagreement: {key}')
                row = result.setdefault(identity, {"cc": 0, "files": {}})
                if path in row["files"]:
                    raise ValueError(f"Duplicate coverage method/file: {identity}: {path}")
                row["files"][path] = facts
    actual = {(identity, _relative(root, path)) for path, classes in module.items()
              for methods in classes.values() for identity in methods}
    if actual != set(complexity):
        raise ValueError("Complexity and coverage method inventories differ")
    for row in result.values():
        row['cc'] = max(1, sum(len(facts['Branches']) for facts in row['files'].values()))
    return result


def _span(row):
    return row["line"], row["column"], row["end_line"], row["end_column"]


def _point_owner(point, methods):
    candidates = [row for row in methods if contains(_span(row), point["line"], point["column"])]
    smallest = [row for row in candidates if not any(
        other is not row and _span(other) != _span(row)
        and (_span(row)[:2] <= _span(other)[:2] and _span(other)[2:] <= _span(row)[2:])
        for other in candidates)]
    if len(smallest) != 1:
        raise ValueError(f"Missing or ambiguous source owner: {point['path']}:{point['line']}:{point['column']}")
    return smallest[0]


def _validate_points(root, points, checksums):
    for point in points:
        key = (point["path"], point["checksum_algorithm"], point["checksum"])
        if key in checksums:
            continue
        algorithm = {"SHA256": "sha256", "SHA1": "sha1"}.get(point["checksum_algorithm"])
        if algorithm is None:
            raise ValueError(f"Unsupported PDB checksum algorithm: {key}")
        actual = hashlib.new(algorithm, (root / point["path"]).read_bytes()).hexdigest()
        if not point["checksum"] or actual != point["checksum"]:
            raise ValueError(f"Source/PDB checksum mismatch: {point['path']}")
        checksums.add(key)


def _coverage_points(module, identity, facts):
    lines = facts.get("Lines")
    branches = facts.get("Branches")
    if not isinstance(lines, dict) or not isinstance(branches, list):
        raise ValueError(f"Missing coverage point inventory: {identity}")
    for line, hits in lines.items():
        if not line.isdecimal() or int(line) < 1:
            raise ValueError(f"Invalid coverage line: {line}")
        module["lines"][line] = max(module["lines"].get(line, 0), _count(hits, "line hits"))
    for branch in branches:
        key = f"{identity}:{branch['Line']}:{branch['Offset']}:{branch['EndOffset']}:{branch['Path']}:{branch['Ordinal']}"
        if key in module["branches"]:
            raise ValueError(f"Duplicate branch point: {key}")
        module["branches"][key] = _count(branch["Hits"], "branch hits")


def _physical_hits(physical, coverage, modules):
    identity = physical["identity"]
    if identity not in coverage:
        raise ValueError(f"Missing physical method coverage: {identity}")
    row = coverage[identity]
    pairs = {}
    for path, facts in row["files"].items():
        if path not in modules:
            continue
        _coverage_points(modules[path], identity, facts)
        pairs.update({(path, int(line)): hits for line, hits in facts["Lines"].items()})
    for point in physical["points"]:
        if point["path"] in modules and (point["path"], point["line"]) not in pairs:
            raise ValueError(f"PDB point absent from method coverage: {identity}: {point['path']}:{point['line']}")
    if not pairs:
        raise ValueError(f"Empty maintained method coverage: {identity}")
    return row["cc"], sum(hits > 0 for hits in pairs.values()), len(pairs)


def _constructor_order(physical, constructor, fragments):
    selected = [fragments[identity] for identity in constructor["fragments"]]
    order = []
    for point in physical["points"]:
        point_start, point_end = _span(point)[:2], _span(point)[2:]
        matches = [fragment for fragment in selected if fragment["path"] == point["path"]
                   and _span(fragment)[:2] < point_end and point_start < _span(fragment)[2:]]
        if matches:
            order.append({"offset": point["offset"], "fragments": [row["id"] for row in matches]})
    return order


def _normal_owner(physical, source_methods, declared):
    owners = {}
    for point in physical["points"]:
        if point["path"] not in source_methods:
            continue
        owner = _point_owner(point, source_methods[point["path"]])
        owners[(point["path"], owner["id"])] = owner
    if len(owners) != 1:
        raise ValueError(f"Unexplained physical-to-source relation: {physical['identity']}: {list(owners)}")
    (path, _), owner = next(iter(owners.items()))
    key = physical.get("kickoff_key") or physical["key"]
    if key in declared:
        semantic = declared[key]
        if semantic["path"] != path or _span(semantic) != _span(owner):
            raise ValueError(f"Semantic/PDB method owner mismatch: {key}")
    return path, owner


def _record_body_ownership(modules, physical, coverage, logical, structural):
    for path, facts in coverage[physical['identity']]['files'].items():
        if path not in modules:
            continue
        row = {'physical': physical['identity'], 'key': physical['key'], 'logical': logical,
               'structural': structural, 'lines': {}, 'branches': {}}
        _coverage_points(row, physical['identity'], facts)
        modules[path]['body_owners'].append(row)


def join_methods(root, source, assembly, coverage):
    """Every maintained emitted body needs exact hits and one explained source owner."""
    source_methods = {row["path"]: row["methods"] for row in source["files"]}
    modules = {path: {"path": path, "executable": False, "lines": {}, "branches": {}, "methods": [], 'body_owners': []}
               for path in source_methods}
    behavior = source["behavior"]
    constructors = {row["key"]: row for row in behavior["constructors"]}
    fragments = {row["id"]: row for row in behavior["fragments"]}
    structural = {row["key"]: row["reason"] for row in behavior["structural_methods"]}
    declared = {row["key"]: row for row in behavior["declared_methods"]}
    checksums, accounting, mapped = set(), [], set()
    for physical in assembly["methods"]:
        points = [point for point in physical["points"] if point["path"] in modules]
        if not points:
            accounting.append({"identity": physical["identity"], "reason": "no maintained portable-PDB points"})
            continue
        _validate_points(root, points, checksums)
        cc, covered, total = _physical_hits(physical, coverage, modules)
        key = physical["key"]
        if key in structural:
            accounting.append({"identity": physical["identity"], "reason": structural[key]})
            _record_body_ownership(modules, physical, coverage, None, structural[key])
            continue
        if key in constructors:
            constructor = constructors[key]
            path = points[0]["path"]
            owner = {"id": key, "line": points[0]["line"], "cognitive": constructor["cognitive"],
                     "parameters": constructor["parameters"], "constructor": True}
            accounting.append({"identity": physical["identity"], "constructor_order": _constructor_order(physical, constructor, fragments)})
        else:
            path, owner = _normal_owner(physical, source_methods, declared)
            mapped.add((path, owner["id"]))
        identity = owner['id'] + '|' + physical['identity']
        _record_body_ownership(modules, physical, coverage, {'path': path, 'id': identity}, None)
        modules[path]["methods"].append({**owner, "id": identity,
                                          "cc": cc, "covered": covered, "total": total})
    missing = [(path, row["id"]) for path, methods in source_methods.items() for row in methods
               if not row["constructor"] and (path, row["id"]) not in mapped]
    if missing:
        raise ValueError(f"Authored methods absent from emitted coverage join: {missing}")
    for module in modules.values():
        module["executable"] = bool(module["lines"])
    return {"modules": list(modules.values()), "accounting": accounting, "fragments": list(fragments.values())}

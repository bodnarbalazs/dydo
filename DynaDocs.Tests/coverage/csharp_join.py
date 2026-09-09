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


def coverage_methods(opencover, root, assembly, aliases):
    """Read one exact AltCover module by original identity and MethodDef token."""
    root = Path(root).resolve()
    modules = ET.fromstring(opencover).findall("./Modules/Module")
    selected = [node for node in modules if node.findtext("ModuleName") == assembly["assembly_name"]]
    if len(selected) != 1:
        raise ValueError(f"Missing or ambiguous module: {assembly['assembly_name']}")
    module = selected[0]
    report_path = _relative(root, module.findtext("ModulePath"))
    normalized_aliases = sorted(set(aliases))
    if report_path not in normalized_aliases:
        raise ValueError(f"Unknown module alias: {report_path}")
    report_hash = module.attrib.get("hash", "").replace("-", "").lower()
    if report_hash != assembly["sha1"].lower():
        raise ValueError("Original module hash mismatch")
    documents = assembly.get("documents")
    if not isinstance(documents, dict) or any(not isinstance(key, str) or not isinstance(value, str)
                                               for key, value in documents.items()):
        raise ValueError("Missing PDB document inventory")
    document_origins = {}
    for method in assembly["methods"]:
        for point in method.get("points", []):
            origin = point.get("origin")
            path = point.get("path")
            if origin not in {"maintained", "generated", "package"} or not isinstance(path, str):
                raise ValueError("Invalid PDB document classification")
            if path in document_origins and document_origins[path] != origin:
                raise ValueError(f"Conflicting PDB document origin: {path}")
            document_origins[path] = origin
    files = {}
    for row in module.findall("./Files/File"):
        uid, url = row.attrib.get("uid"), row.attrib.get("fullPath")
        if not uid or not url or uid in files:
            raise ValueError("Missing or duplicate report file identity")
        path = documents.get(url)
        if path is None or path not in document_origins:
            raise ValueError(f"Coverage source outside PDB document inventory: {url}")
        files[uid] = path
    methods = {row["token"]: row for row in assembly["methods"] if row.get("points")}
    if len(methods) != sum(bool(row.get("points")) for row in assembly["methods"]):
        raise ValueError("Duplicate original MethodDef token")
    result, seen_tokens, branch_uspids = {}, set(), set()
    for method in module.findall("./Classes/Class/Methods/Method"):
        token_text, identity = method.findtext("MetadataToken"), method.findtext("Name")
        if not token_text or not token_text.isdecimal():
            raise ValueError("Missing MethodDef token")
        token = int(token_text)
        if token not in methods:
            continue
        expected = methods[token]
        if token in seen_tokens:
            raise ValueError(f"Duplicate MethodDef token: {token}")
        seen_tokens.add(token)
        if identity != expected["identity"]:
            raise ValueError(f"Physical signature mismatch for token {token}")
        file_rows = {}
        point_lines = {(point["path"], point["line"]) for point in expected["points"]}
        sequence_ids = set()
        for point in method.findall("./SequencePoints/SequencePoint"):
            required = ("vc", "uspid", "ordinal", "offset", "sl", "sc", "el", "ec", "fileid")
            if any(name not in point.attrib for name in required):
                raise ValueError(f"Missing native sequence field for token {token}")
            values = {name: int(point.attrib[name]) for name in required if name != "fileid"}
            if any(value < 0 for value in values.values()) or values["sl"] < 1:
                raise ValueError(f"Invalid native sequence value for token {token}")
            path = files.get(point.attrib["fileid"])
            if path is None or (path, values["sl"]) not in point_lines:
                raise ValueError(f"Sequence source ownership mismatch for token {token}")
            identity_key = (path, values["uspid"], values["ordinal"], values["offset"], values["sl"])
            if identity_key in sequence_ids:
                raise ValueError(f"Duplicate sequence identity for token {token}")
            sequence_ids.add(identity_key)
            facts = file_rows.setdefault(path, {"Lines": {}, "Branches": []})
            line = str(values["sl"])
            facts["Lines"][line] = max(facts["Lines"].get(line, 0), values["vc"])
        branch_ids = set()
        for branch in method.findall("./BranchPoints/BranchPoint"):
            required = ("vc", "uspid", "fileid", "sl", "offset", "offsetend", "path", "ordinal")
            if any(name not in branch.attrib for name in required):
                raise ValueError(f"Missing native branch field for token {token}")
            values = {name: int(branch.attrib[name]) for name in required if name != "fileid"}
            path = files.get(branch.attrib["fileid"])
            if any(value < 0 for value in values.values()) or path is None or (path, values["sl"]) not in point_lines:
                raise ValueError(f"Branch source ownership mismatch for token {token}")
            key = (token, values["uspid"], branch.attrib["fileid"], values["sl"], values["offset"],
                   values["offsetend"], values["path"], values["ordinal"])
            if key in branch_ids or values["uspid"] in branch_uspids:
                raise ValueError(f"Duplicate branch identity for token {token}")
            branch_ids.add(key)
            branch_uspids.add(values["uspid"])
            file_rows.setdefault(path, {"Lines": {}, "Branches": []})["Branches"].append({
                "Line": values["sl"], "Offset": values["offset"], "EndOffset": values["offsetend"],
                "Path": values["path"], "Ordinal": values["ordinal"], "Uspid": values["uspid"],
                "FileId": branch.attrib["fileid"], "Hits": values["vc"],
            })
        result[identity] = {"token": token, "files": file_rows}
    missing = sorted(set(methods) - seen_tokens)
    if missing:
        raise ValueError(f"Missing physical token coverage: {missing}")
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
    return sum(hits > 0 for hits in pairs.values()), len(pairs)


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
    generated_files = set(source.get("generated_files", []))
    checksums, accounting, mapped = set(), [], set()
    for physical in assembly["methods"]:
        all_points = physical["points"]
        if not all_points:
            accounting.append({"identity": physical["identity"],
                               "reason": "no non-hidden portable-PDB points"})
            continue
        origins = {point.get("origin") for point in all_points}
        documents = {point["path"] for point in all_points}
        if not origins <= {"maintained", "generated", "package"}:
            raise ValueError(f"Invalid PDB document origin: {physical['identity']}")
        if "maintained" in origins and len(origins) != 1:
            raise ValueError(f"Mixed-origin physical method: {physical['identity']}: {sorted(documents)}")
        if "maintained" not in origins:
            missing = sorted(documents - generated_files)
            if missing:
                raise ValueError(f"Generated PDB document absent from compilation inventory: {missing[0]}")
            accounting.append({"identity": physical["identity"], "reason": "excluded by origin",
                               "origins": sorted(origins), "documents": sorted(documents)})
            continue
        missing = sorted(documents - set(modules))
        if missing:
            raise ValueError(f"Maintained PDB document absent from source inventory: {missing[0]}")
        points = list(all_points)
        if not points:
            raise ValueError(f"Empty maintained PDB point inventory: {physical['identity']}")
        _validate_points(root, points, checksums)
        covered, total = _physical_hits(physical, coverage, modules)
        key = physical["key"]
        if key in structural:
            accounting.append({"identity": physical["identity"], "reason": structural[key]})
            _record_body_ownership(modules, physical, coverage, None, structural[key])
            continue
        if key in constructors:
            constructor = constructors[key]
            path = points[0]["path"]
            owner = {"id": key, "line": points[0]["line"], "cognitive": constructor["cognitive"],
                     "policy_cc": constructor["policy_cc"],
                     "parameters": constructor["parameters"], "constructor": True}
            accounting.append({"identity": physical["identity"], "constructor_order": _constructor_order(physical, constructor, fragments)})
        else:
            path, owner = _normal_owner(physical, source_methods, declared)
            mapped.add((path, owner["id"]))
        identity = owner['id'] + '|' + physical['identity']
        _record_body_ownership(modules, physical, coverage, {'path': path, 'id': identity}, None)
        modules[path]["methods"].append({**owner, "id": identity,
                                          "cc": owner["policy_cc"], "covered": covered, "total": total})
    missing = [(path, row["id"]) for path, methods in source_methods.items() for row in methods
               if not row["constructor"] and (path, row["id"]) not in mapped]
    if missing:
        raise ValueError(f"Authored methods absent from emitted coverage join: {missing}")
    for module in modules.values():
        module["executable"] = bool(module["lines"])
    return {"modules": list(modules.values()), "accounting": accounting, "fragments": list(fragments.values())}

"""Exact compiler traversal and terminal projection observation; no target mutation."""
import hashlib
import os
from pathlib import Path
import stat

MOUNT_POINT = 0xA0000003
REPARSE_POINT = 0x400
OWNERSHIP_SHA256 = '79a3020fd66210ef79157e8e7325054f8fdccdcc4a56ef0e6542f34898b1be43'


def inspect_entry(path):
    try:
        info = os.lstat(path)
        attributes = getattr(info, 'st_file_attributes', 0)
        tag = getattr(info, 'st_reparse_tag', 0)
        if attributes & REPARSE_POINT or tag or stat.S_ISLNK(info.st_mode):
            return dict(kind='reparse', attributes=attributes, tag=tag)
        if stat.S_ISDIR(info.st_mode):
            return dict(kind='directory', attributes=attributes, tag=tag)
        if stat.S_ISREG(info.st_mode):
            return dict(kind='file', attributes=attributes, tag=tag, bytes=info.st_size)
        raise RuntimeError('Unclassifiable entry: ' + str(path))
    except OSError as error:
        raise RuntimeError('Topology inspection failed: ' + str(path)) from error


def observe_link(path, expected):
    actual = inspect_entry(path)
    if actual != dict(kind='reparse', attributes=expected['attributes'], tag=MOUNT_POINT):
        raise RuntimeError('Exact junction type/attributes differ: ' + str(path))
    try:
        raw = os.readlink(path)
    except OSError as error:
        raise RuntimeError('Literal junction inspection failed: ' + str(path)) from error
    # Windows readlink exposes the substitute-name DOS prefix; never resolve it.
    literal = raw[4:] if raw.startswith('\\\\?\\') else raw
    if expected['linkType'] != 'Junction' or [literal] != expected['targets']:
        raise RuntimeError('Literal junction target differs: ' + str(path))
    return dict(path=str(path), kind='junction', attributes=actual['attributes'],
        reparseTag=actual['tag'], targets=[literal], rawReadlink=raw)


def children(folder):
    try:
        with os.scandir(folder) as stream:
            entries = sorted(stream, key=lambda entry: entry.name)
    except OSError as error:
        raise RuntimeError('Directory inspection failed: ' + str(folder)) from error
    for entry in entries:
        path = Path(entry.path)
        if path.parent != folder or path.name != entry.name or entry.name in {'.', '..'}:
            raise RuntimeError('Escaping/foreign member: ' + str(path))
        yield path, inspect_entry(path)


def file_row(path, logical):
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(path=logical, kind='file', bytes=path.stat().st_size, sha256=digest)


def compiler_identity(root, mapping, ancestor_rows, ownership):
    source, target = Path(mapping['source']), Path(mapping['target'])
    if (not isinstance(ownership, dict) or ownership.get('state') != 'TASK_LOCATION_OWNERSHIP_ESTABLISHED'
            or ownership.get('mapping') != dict(source=mapping['source'], target=mapping['target'])
            or ownership.get('originalEvidence', {}).get('sha256') != OWNERSHIP_SHA256
            or ownership.get('creatorPositions') != [66, 70, 73]):
        raise RuntimeError('Compiler task-location ownership evidence refused')
    source.relative_to(root)
    if Path(mapping['entry']['path']) != source or mapping['entry']['targets'] != [str(target)]:
        raise RuntimeError('Compiler mapping declaration differs')
    observed = observe_link(source, mapping['entry'])
    ancestors = []
    for expected in ancestor_rows:
        path = Path(expected['path'])
        actual = inspect_entry(path)
        if actual != dict(kind='directory', attributes=expected['attributes'], tag=0):
            raise RuntimeError('Compiler/source ancestor changed: ' + str(path))
        ancestors.append(dict(path=str(path), **actual))
    return dict(link=observed, ancestors=ancestors)


def compiler_inventory(root, manifest, ownership):
    mapping = manifest['compiler']
    source, target = Path(mapping['source']), Path(mapping['target'])
    identity = compiler_identity(root, mapping, manifest['ordinaryAncestors'], ownership)
    rows, stack = [], [target]
    while stack:
        folder = stack.pop()
        for path, info in children(folder):
            relative = path.relative_to(target)
            logical = (source / relative).relative_to(root).as_posix()
            if info['kind'] == 'reparse':
                raise RuntimeError('Nested compiler reparse refused: ' + str(path))
            if info['kind'] == 'directory':
                rows.append(dict(path=logical, kind='directory'))
                stack.append(path)
            else:
                rows.append(file_row(path, logical))
    rows.sort(key=lambda row: row['path'])
    files = [{key: row[key] for key in ['path', 'bytes', 'sha256']}
        for row in rows if row['kind'] == 'file']
    directories = [row['path'] for row in rows if row['kind'] == 'directory']
    if files != mapping['files'] or directories != mapping['directories']:
        raise RuntimeError('Compiler member/directory closure drift')
    return dict(**identity, rows=rows)


def scripts_inventory(root, manifest, ownership):
    compiler = compiler_inventory(root, manifest, ownership)
    source = Path(manifest['compiler']['source'])
    scripts = root / 'skills/engineering/statechart/scripts'
    rows, stack, seen = [], [scripts], False
    while stack:
        folder = stack.pop()
        for path, info in children(folder):
            relative = path.relative_to(root).as_posix()
            if path == source:
                observe_link(path, manifest['compiler']['entry'])
                rows.append(dict(path=relative, kind='directory'))
                rows.extend(compiler['rows'])
                seen = True
            elif info['kind'] == 'reparse':
                raise RuntimeError('Unlisted scripts reparse refused: ' + str(path))
            elif info['kind'] == 'directory':
                rows.append(dict(path=relative, kind='directory'))
                stack.append(path)
            else:
                rows.append(file_row(path, relative))
    if not seen:
        raise RuntimeError('Compiler mapping missing from scripts')
    return sorted(rows, key=lambda row: row['path'])


def terminal_entries(root, manifest):
    entries = {}
    for expected in manifest['projections']:
        path = Path(expected['path'])
        relative = path.relative_to(root)
        if (len(relative.parts) != 3 or relative.parts[:2] not in
                {('.agents', 'skills'), ('.claude', 'skills')} or path in entries):
            raise RuntimeError('Invalid declared terminal projection set')
        entries[path] = expected
    if len(entries) != 82 or sum(path.relative_to(root).parts[0] == '.agents'
            for path in entries) != 41:
        raise RuntimeError('Exact82 terminal projection declaration required')
    source = Path(manifest['compiler']['source'])
    if source in entries:
        raise RuntimeError('Compiler and projection roles overlap')
    entries[source] = manifest['compiler']['entry']
    return entries


def excluded_workspace_entry(path, info, output):
    if path == output:
        if info['kind'] != 'directory':
            raise RuntimeError('Excluded owned directory type changed')
        return True
    return path.name == '.git' and info['kind'] == 'directory'


def workspace_paths(root, output, manifest):
    terminals = terminal_entries(root, manifest)
    rows, stack, seen = [], [root], set()
    while stack:
        folder = stack.pop()
        for path, info in children(folder):
            if excluded_workspace_entry(path, info, output):
                continue
            relative = path.relative_to(root).as_posix()
            if path in terminals:
                row = observe_link(path, terminals[path])
                row['path'] = relative
                rows.append(row)
                seen.add(path)
                # Prune before descent; compiler traversal is separately hash-bound.
                continue
            if info['kind'] == 'reparse':
                raise RuntimeError('Unknown workspace reparse refused: ' + str(path))
            rows.append(dict(path=relative, kind=info['kind'],
                bytes=info.get('bytes') if info['kind'] == 'file' else None))
            if info['kind'] == 'directory':
                stack.append(path)
    if seen != set(terminals):
        raise RuntimeError('Declared projection/compiler entry missing')
    return sorted(rows, key=lambda row: row['path'])

"""Resolved tool pins are measured inputs, independent of policy outcomes."""
import importlib.metadata
import json
import os
import platform
import re
import sys
from pathlib import Path

from gate_run import file_hash, result

STORE = 'node_modules/.pnpm'
IMPORTER = '.'


def _json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def python_versions(coverage):
    runtime_pin = coverage / '.python-version'
    pin_text = runtime_pin.read_text(encoding='utf-8')
    if not re.fullmatch(r'3\.14\.4\r?\n?', pin_text):
        raise ValueError('Python runtime pin must contain exactly 3.14.4')
    rows = []
    for line in (coverage / 'requirements.lock').read_text(encoding='utf-8').splitlines():
        match = re.fullmatch(r'([A-Za-z0-9_.-]+)==([^\s]+)', line)
        if not match:
            raise ValueError('Python lock must contain exact version pins')
        actual = importlib.metadata.version(match[1])
        if actual != match[2]:
            raise ValueError(f'Python resolved version mismatch: {match[1]} expected {match[2]}, found {actual}')
        rows.append({'name': match[1], 'version': actual})
    implementation = platform.python_implementation()
    if implementation != 'CPython' or sys.version_info[:2] != (3, 14):
        raise ValueError('The monitoring collector requires CPython 3.14')
    return {'declared_runtime': pin_text.rstrip('\r\n'), 'runtime': platform.python_version(),
            'implementation': implementation, 'packages': rows,
            'runtime_pin_sha256': file_hash(runtime_pin),
            'lock_sha256': file_hash(coverage / 'requirements.lock')}


def _lock_entry(stripped):
    """A lock line's key and inline value; a quoted key may hold any character."""
    if stripped.startswith("'"):
        end = stripped.index("'", 1)
        return stripped[1:end], stripped[end + 2:].strip()
    key, _, value = stripped.partition(':')
    return key, value.strip()


def read_pnpm_lock(text):
    """A pnpm-lock.yaml's block mappings as nested dicts. Flow values stay text and sequence items
    are dropped: the lock's own subset of YAML, read without a YAML dependency."""
    root = {}
    stack = [(-1, root)]
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith('- '):
            continue
        indent = len(line) - len(line.lstrip(' '))
        while stack[-1][0] >= indent:
            stack.pop()
        key, value = _lock_entry(stripped)
        node = {} if value in ('', '{}') else value.strip("'")
        stack[-1][1][key] = node
        if value == '':
            stack.append((indent, node))
    return root


def pnpm_lock(path):
    return read_pnpm_lock(Path(path).read_text(encoding='utf-8'))


def importer_pins(text):
    """The root importer's direct dependencies, each at its resolved version without peer suffix."""
    importer = read_pnpm_lock(text)['importers']['.']
    return {name: _package_id(pin['version']) for section in importer.values() for name, pin in section.items()}


def _flow_list(text):
    return [item.strip().strip("'") for item in text.strip('[]').split(',')] if text else None


def _package_id(key):
    """name@version of a lock package or snapshot key, without its peer suffix."""
    return key.split('(', 1)[0]


def _manifest_id(manifest):
    installed = _json(manifest)
    return installed['name'] + '@' + installed['version']


def _declared_pins(tools, lock):
    """Each declared dependency pins one exact version, and launches through its top-level link."""
    declared = _json(tools / 'package.json')['dependencies']
    importer = lock['importers']['.']['dependencies']
    pins = {name: (pin['specifier'], _package_id(pin['version'])) for name, pin in importer.items()}
    expected = {name: (version, version) for name, version in declared.items()}
    differing = sorted(name for name in pins.keys() | expected.keys() if pins.get(name) != expected.get(name))
    if differing:
        raise ValueError('JavaScript dependency and lock pin disagree: ' + ', '.join(differing))
    for name, version in declared.items():
        manifest = tools / 'node_modules' / name / 'package.json'
        if not manifest.is_file():
            raise ValueError('Missing required locked JavaScript package: node_modules/' + name)
        if _manifest_id(manifest) != name + '@' + version:
            raise ValueError('JavaScript resolved version mismatch: node_modules/' + name)


def _is_link(path):
    """A pnpm link: a symlink, or on Windows the junction pnpm makes."""
    return path.is_symlink() or path.is_junction()


def _folder_entries(modules):
    """A store folder's node_modules entries by package name; a scope folder holds its packages."""
    for entry in sorted(modules.iterdir()):
        if entry.name.startswith('@') and not _is_link(entry):
            yield from ((entry.name + '/' + package.name, package) for package in sorted(entry.iterdir()))
        elif not entry.name.startswith('.'):
            yield entry.name, entry


def _store(tools):
    """pnpm's isolated store by folder: the one real package each holds, and the links beside it."""
    folders = {}
    for modules in sorted((tools / STORE).glob('*/node_modules')):
        base = modules.relative_to(tools).as_posix()
        entries = list(_folder_entries(modules))
        packages = [path for _, path in entries if not _is_link(path) and (path / 'package.json').is_file()]
        if len(packages) != 1:
            raise ValueError(f'JavaScript store folder must hold exactly one package: {base}')
        manifest = packages[0] / 'package.json'
        folders[modules.parent.name] = {
            'base': base, 'path': packages[0].relative_to(tools).as_posix(), 'manifest': manifest,
            'package': _manifest_id(manifest), 'real': os.path.realpath(packages[0]),
            'links': {name: path for name, path in entries if _is_link(path)}}
    return folders


def _resolve_links(by_path, links):
    """The store folder each link resolves to; None when it resolves to no store package."""
    return {name: by_path.get(os.path.realpath(path)) for name, path in links.items()}


def _expected_links(snapshot, installed):
    """The snapshot keys a lock snapshot links: its dependencies, and the optional ones installed."""
    pinned = dict(snapshot.get('dependencies', {}))
    pinned.update((name, version) for name, version in snapshot.get('optionalDependencies', {}).items()
                  if _package_id(name + '@' + version) in installed)
    return {name: name + '@' + version for name, version in pinned.items()}


def _link_faults(node, wanted, owner):
    """Each way one folder's links differ, by locked version, from one snapshot's dependencies."""
    faults = []
    for name in sorted(node['found'].keys() | wanted.keys()):
        path, pin = node['base'] + '/' + name, wanted.get(name)
        if name not in node['found']:
            faults.append(f'link missing: {path} must resolve to {_package_id(pin)}')
            continue
        found = node['found'][name] or 'nothing in the store'
        if pin is None:
            faults.append(f'link unexpected: {path} resolves to {found}, which the lock does not list for {owner}')
        elif found != _package_id(pin):
            faults.append(f'link mismatch: {path} resolves to {found}, the lock pins {_package_id(pin)}')
    return faults


def _version_labels(node, expected):
    """The snapshots of a folder's package whose dependencies its links match by locked version."""
    candidates = [key for key in expected if _package_id(key) == node['package']] or [node['package']]
    faults = {key: _link_faults(node, expected.get(key, {}), node['package']) for key in candidates}
    matching = [key for key, found in faults.items() if not found]
    if not matching:
        raise ValueError('JavaScript dependency ' + min(faults.values(), key=len)[0])
    return matching


def _unpinned(node, wanted, labels):
    """A folder's links whose target holds no variant of the snapshot key pinned for them."""
    return [node['base'] + '/' + name for name, key in wanted.items()
            if key not in labels.get(node['targets'].get(name), ())]


def _narrow(nodes, expected, labels):
    """Keep for each folder only the snapshots every one of its links can still satisfy."""
    while True:
        narrowed = {name: [key for key in keys if not _unpinned(nodes[name], expected[key], labels)]
                    for name, keys in labels.items()}
        if narrowed == labels:
            return labels
        labels = narrowed


def _unpinned_paths(nodes, expected, labels, failing, against):
    return sorted({path for name in failing for key in labels[name]
                   for path in _unpinned(nodes[name], expected[key], against)})


def _verify_links(tools, lock, folders):
    """Every store folder's sibling links resolve to exactly the folders holding the snapshots its
    lock snapshot pins, peer variants included; the importer's top-level links likewise."""
    installed = {folder['package'] for folder in folders.values()}
    expected = {key: _expected_links(snapshot, installed) for key, snapshot in lock['snapshots'].items()}
    by_path = {folder['real']: name for name, folder in folders.items()}
    nodes, labels = {}, {}
    for name, folder in folders.items():
        targets = _resolve_links(by_path, folder['links'])
        nodes[name] = {**folder, 'targets': targets,
                       'found': {link: target and folders[target]['package'] for link, target in targets.items()}}
        labels[name] = _version_labels(nodes[name], expected)
    importer = lock['importers']['.']['dependencies']
    expected[IMPORTER] = {name: name + '@' + pin['version'] for name, pin in importer.items()}
    nodes[IMPORTER] = {'base': 'node_modules', 'targets': _resolve_links(
        by_path, {name: tools / 'node_modules' / name for name in importer})}
    labels[IMPORTER] = [IMPORTER]
    final = _narrow(nodes, expected, labels)
    failing = [name for name, keys in final.items() if not keys]
    if failing:
        # Name the links that miss by their target's own locked variant; a cycle may need the
        # narrowed labels to show any.
        paths = (_unpinned_paths(nodes, expected, labels, failing, labels)
                 or _unpinned_paths(nodes, expected, labels, failing, final))
        raise ValueError('JavaScript dependency links resolve to a peer variant the lock does not pin: '
                         + ', '.join(paths))


def javascript_versions(coverage):
    tools = coverage
    lock = pnpm_lock(tools / 'pnpm-lock.yaml')
    _declared_pins(tools, lock)
    flags = {}
    for key, snapshot in lock['snapshots'].items():
        flags.setdefault(_package_id(key), []).append(snapshot.get('optional') == 'true')
    folders = _store(tools)
    rows = []
    for folder in folders.values():
        if folder['package'] not in lock['packages']:
            raise ValueError(f"JavaScript resolved version mismatch: {folder['path']} holds {folder['package']}, "
                             'which the lock does not')
        rows.append({'path': folder['path'], 'package': folder['package'],
                     'manifest_sha256': file_hash(folder['manifest'])})
    installed = {folder['package'] for folder in folders.values()}
    omitted = []
    for package, entry in lock['packages'].items():
        if package in installed:
            continue
        if not all(flags.get(package, [False])):
            raise ValueError('Missing required locked JavaScript package: ' + package)
        omitted.append({'package': package, 'optional': True,
                        'os': _flow_list(entry.get('os')), 'cpu': _flow_list(entry.get('cpu'))})
    _verify_links(tools, lock, folders)
    return {'packages': rows, 'uninstalled_optional': omitted, 'lock_sha256': file_hash(tools / 'pnpm-lock.yaml')}


def dotnet_versions(coverage):
    project = coverage / 'metrics'
    lock = _json(project / 'packages.lock.json')
    assets = _json(project / 'obj/project.assets.json')
    if set(lock['dependencies']) != {'net10.0'}:
        raise ValueError('Unexpected metrics lock target framework')
    expected = {name.lower() + '/' + row['resolved']: row['contentHash']
                for name, row in lock['dependencies']['net10.0'].items()}
    actual = {name.lower(): row['sha512'] for name, row in assets['libraries'].items() if row['type'] == 'package'}
    if expected != actual:
        raise ValueError('Resolved Roslyn/Sonar/Cecil dependency closure differs from the lock')
    return {'packages': expected, 'lock_sha256': file_hash(project / 'packages.lock.json'),
            'assets_sha256': file_hash(project / 'obj/project.assets.json')}


def collect_versions(runner):
    facts, errors = {}, []
    for name, collect in [('python', python_versions), ('javascript', javascript_versions), ('dotnet', dotnet_versions)]:
        try:
            facts[name] = collect(runner.coverage)
        except (ValueError, KeyError, OSError, importlib.metadata.PackageNotFoundError) as error:
            errors.append({'stack': name, 'message': str(error)})
    for name, command, expected in [('node', ['node', '--version'], 'v26.13.0'), ('sdk', ['dotnet', '--version'], '10.0.303')]:
        row = runner.log.run(name + '-version', command)
        version = Path(row['stdout']).read_text(encoding='utf-8').strip()
        facts[name] = {'version': version, 'command': row}
        if row['exit_code'] != 0 or version != expected:
            errors.append({'stack': name, 'message': 'Native runtime pin mismatch', 'expected': expected, 'actual': version})
    return result(facts, errors=errors)

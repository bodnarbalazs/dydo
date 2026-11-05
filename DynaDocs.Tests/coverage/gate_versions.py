"""Resolved tool pins are measured inputs, independent of policy outcomes."""
import importlib.metadata
import json
import platform
import re
import sys
from pathlib import Path

from gate_run import file_hash, result


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


def pnpm_lock(path):
    """A pnpm-lock.yaml's block mappings as nested dicts. Flow values stay text and sequence items
    are dropped: the lock's own subset of YAML, read without a YAML dependency."""
    root = {}
    stack = [(-1, root)]
    for line in Path(path).read_text(encoding='utf-8').splitlines():
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


def _store_packages(tools):
    """Each real package folder of pnpm's isolated store; a symlink or junction is a link, not a package."""
    for entry in sorted((tools / 'node_modules/.pnpm').glob('*/node_modules/*')):
        for package in (sorted(entry.iterdir()) if entry.name.startswith('@') else [entry]):
            if not (package.is_symlink() or package.is_junction()) and (package / 'package.json').is_file():
                yield package.relative_to(tools).as_posix(), package / 'package.json'


def javascript_versions(coverage):
    tools = coverage
    lock = pnpm_lock(tools / 'pnpm-lock.yaml')
    _declared_pins(tools, lock)
    flags = {}
    for key, snapshot in lock['snapshots'].items():
        flags.setdefault(_package_id(key), []).append(snapshot.get('optional') == 'true')
    rows, installed = [], set()
    for relative, manifest in _store_packages(tools):
        package = _manifest_id(manifest)
        if package not in lock['packages']:
            raise ValueError(f'JavaScript resolved version mismatch: {relative} holds {package}, which the lock does not')
        installed.add(package)
        rows.append({'path': relative, 'package': package, 'manifest_sha256': file_hash(manifest)})
    omitted = []
    for package, entry in lock['packages'].items():
        if package in installed:
            continue
        if not all(flags.get(package, [False])):
            raise ValueError('Missing required locked JavaScript package: ' + package)
        omitted.append({'package': package, 'optional': True,
                        'os': _flow_list(entry.get('os')), 'cpu': _flow_list(entry.get('cpu'))})
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

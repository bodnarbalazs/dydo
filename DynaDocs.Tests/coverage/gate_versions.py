"""Resolved tool pins are measured inputs, independent of policy outcomes."""
import importlib.metadata
import json
import platform
import re
from pathlib import Path

from gate_run import file_hash, result


def _json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def python_versions(coverage):
    rows = []
    for line in (coverage / 'requirements.lock').read_text(encoding='utf-8').splitlines():
        match = re.fullmatch(r'([A-Za-z0-9_.-]+)==([^\s]+)', line)
        if not match:
            raise ValueError('Python lock must contain exact version pins')
        actual = importlib.metadata.version(match[1])
        if actual != match[2]:
            raise ValueError(f'Python resolved version mismatch: {match[1]} expected {match[2]}, found {actual}')
        rows.append({'name': match[1], 'version': actual})
    if platform.python_version() != '3.12.14':
        raise ValueError('The monitoring collector requires pinned CPython3.12.14')
    return {'runtime': platform.python_version(), 'packages': rows,
            'lock_sha256': file_hash(coverage / 'requirements.lock')}


def javascript_versions(coverage):
    tools = coverage
    lock = _json(tools / 'package-lock.json')
    declared = _json(tools / 'package.json')['dependencies']
    for name, version in declared.items():
        if lock['packages']['node_modules/' + name]['version'] != version:
            raise ValueError('JavaScript dependency and lock pin disagree: ' + name)
    rows, omitted = [], []
    for relative, entry in lock['packages'].items():
        if not relative:
            continue
        manifest = tools / relative / 'package.json'
        if not manifest.is_file():
            if not entry.get('optional'):
                raise ValueError('Missing required locked JavaScript package: ' + relative)
            omitted.append({'path': relative, 'optional': True, 'os': entry.get('os'), 'cpu': entry.get('cpu')})
            continue
        installed = _json(manifest)
        if installed['version'] != entry['version']:
            raise ValueError('JavaScript resolved version mismatch: ' + relative)
        rows.append({'path': relative, 'version': installed['version'], 'manifest_sha256': file_hash(manifest)})
    return {'packages': rows, 'uninstalled_optional': omitted, 'lock_sha256': file_hash(tools / 'package-lock.json')}


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
    for name, command, expected in [('node', ['node', '--version'], 'v22.13.0'), ('sdk', ['dotnet', '--version'], '10.0.300')]:
        row = runner.log.run(name + '-version', command)
        version = Path(row['stdout']).read_text(encoding='utf-8').strip()
        facts[name] = {'version': version, 'command': row}
        if row['exit_code'] != 0 or version != expected:
            errors.append({'stack': name, 'message': 'Native runtime pin mismatch', 'expected': expected, 'actual': version})
    return result(facts, errors=errors)

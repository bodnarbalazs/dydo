"""Reviewed statechart native callables and source-derived two-platform coverage union."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess
import sys

from python_platform import _check_points, _owners, _scoped
from typescript_join import join_istanbul, merge_istanbul

SELECTION = Path('DynaDocs.Tests/coverage/statechart-platforms.json')
PACKAGE = Path('skills/engineering/statechart/scripts')
TOOLS = Path(__file__).resolve().parent
PLATFORMS = ('linux', 'windows')


def native_platform():
    return {'linux': 'linux', 'win32': 'windows'}.get(sys.platform)


def _digest(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()


def load_selection(root, platform, selection=None):
    """Validate the pinned map before starting a coverage campaign."""
    if selection is None:
        selection = json.loads((Path(root) / SELECTION).read_text(encoding='utf-8'))
    if (platform not in PLATFORMS or set(selection) != {'schema', 'neutral', 'native', 'windows_tests'}
            or type(selection['schema']) is not int or selection['schema'] != 1
            or selection['neutral'] != 'linux'):
        raise ValueError('Unsupported statechart platform selection')
    tests = selection['windows_tests']
    if (set(tests) != {'reason', 'tests', 'names'} or not tests['reason'] or not tests['tests']
            or len(set(tests['tests'])) != len(tests['tests'])
            or not tests['names'] or len(set(tests['names'])) != len(tests['names'])
            or any(not name.startswith('test/') or not name.endswith('.test.ts')
                   or '..' in Path(name).parts for name in tests['tests'])):
        raise ValueError('Missing or duplicate Windows test selection')
    for path, rule in selection['native'].items():
        if (set(rule) != {'sha256', 'windows'} or not rule['windows']
                or len(set(rule['windows'])) != len(rule['windows'])):
            raise ValueError('Missing or duplicate native callable in platform selection')
        if _digest((Path(root) / path).read_text(encoding='utf-8-sig')) != rule['sha256']:
            raise ValueError('Platform selection source drift; review and update the selection')
    return selection


def _native_lines(module, rule):
    methods = module['methods']
    found = [method for method in methods if method['id'] in rule['windows']]
    if len(found) != len(rule['windows']) or {m['id'] for m in found} != set(rule['windows']):
        raise ValueError('Missing or duplicate native callable in platform selection')
    return {str(line) for method in found for line in range(method['line'], method['end_line'] + 1)}


def scope(root, modules, platform, selection=None):
    """Windows owns declared callable spans (including nested bodies); Linux owns every remainder."""
    paths = [module['path'] for module in modules]
    if selection is None:
        selection = json.loads((Path(root) / SELECTION).read_text(encoding='utf-8'))
    if not paths or len(set(paths)) != len(paths) or set(selection['native']) - set(paths):
        raise ValueError('Platform selection module inventory drift')
    selection = load_selection(root, platform, selection)
    receipt = {'schema': 1, 'platform': platform, 'sources': {}, 'denominators': {},
               'selection_sha256': _digest(json.dumps(selection, sort_keys=True))}
    measured = []
    for module in modules:
        path = module['path']
        native = _native_lines(module, selection['native'][path]) if path in selection['native'] else set()
        owners = _owners(module, native)
        receipt['sources'][path] = _digest((Path(root) / path).read_text(encoding='utf-8-sig'))
        receipt['denominators'][path] = owners
        row = _scoped(module, owners, platform)
        if row is not None:
            measured.append(row)
    return measured, receipt


def test_arguments(arguments, selection, platform):
    if platform == 'linux':
        return arguments
    if arguments.count('test/*.test.ts') != 1:
        raise ValueError('Unknown statechart test script selection')
    names = selection['windows_tests']['names']
    pattern = '^(?:' + '|'.join(re.escape(name) for name in names) + ')$'
    return [item for item in arguments if item != 'test/*.test.ts'] + [
        '--test-name-pattern=' + pattern, *selection['windows_tests']['tests']]


def selected_tests(report, selection):
    """Every declared Windows case must run exactly once and pass, without a skip."""
    text = Path(report).read_text(encoding='utf-8')
    names = re.findall(r'^ok \d+ - (.+)$', text, re.M)
    expected = selection['windows_tests']['names']
    if sorted(names) != sorted(expected):
        raise ValueError('Selected Windows tests did not all run and pass exactly once')
    return {'names': names, 'total': len(names)}


def denominator(root, targets, output):
    """Rebuild every Istanbul point without executing maintained source."""
    output.mkdir(parents=True, exist_ok=True)
    config = output / 'source-input.json'
    config.write_text(json.dumps({'root': str(root), 'targets': targets}), encoding='utf-8')
    result = subprocess.check_output(['node', str(TOOLS / 'javascript_coverage.cjs'),
                                      '--typescript-denominator', str(config)], text=True)
    return json.loads(result)


def measured_modules(root, targets, walked, raw):
    source = denominator(root, targets, raw / 'denominator')
    reports = [json.loads(path.read_text(encoding='utf-8')) for path in sorted((raw / 'istanbul').glob('*.json'))]
    if not reports:
        raise ValueError('No statechart Istanbul coverage reports')
    measured = merge_istanbul(source, reports, root)
    return [join_istanbul(path, measured[path], walked[path]) for path in targets]


def _source_modules(root, commit):
    from gate_collect import Collectors
    from gate_discovery import ordinary_discovery
    from gate_inventory import assemble_inventory
    from inventory import git_file_state
    import tempfile
    root = Path(root).resolve()
    if (subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip() != commit
            or subprocess.check_output(['git', 'status', '--porcelain'], cwd=root)
            or Path(subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], cwd=root,
                                             text=True).strip()).resolve() != root):
        raise ValueError('Platform union requires the clean pinned checkout')
    paths, deleted = git_file_state(root)
    paths = [path for path in paths if path.startswith(PACKAGE.as_posix() + '/')
             and Path(path).suffix.lower() in ('.ts', '.tsx')]
    inventory = assemble_inventory(root, paths, [], ordinary_discovery(root, paths), deleted)
    if inventory['errors']:
        raise ValueError(f"Invalid source-derived statechart inventory: {inventory['errors']}")
    with tempfile.TemporaryDirectory(prefix='dydo-statechart-union-') as folder:
        raw = Path(folder)
        targets = [row['path'] for row in inventory['sources']
                   if row['role'] != 'test' and not row.get('coverageExemption')]
        collector = Collectors(root, raw / 'walker')
        collector.inventory = {**inventory, 'sources': [row for row in inventory['sources']
                                                       if row['path'] in targets]}
        walk = collector.typescript_source()
        if walk['errors']:
            raise ValueError(f"Invalid statechart source metrics: {walk['errors']}")
        walked = {row['path']: row for row in walk['facts']['modules']}
        source = denominator(root, targets, raw)
        return [join_istanbul(path, source[path], walked[path]) for path in targets]


def _report_facts(report, platform):
    from python_platform import _report_facts as facts
    modules, receipt = facts(report, platform, stack='statechart', collector='typescript-coverage')
    if len({module['path'] for module in modules}) != len(modules):
        raise ValueError('Duplicate platform module')
    return modules, receipt


def verify_union(linux, windows, root=Path('.')):
    """Rejudge both clean gates and compare every point and callable against the candidate source."""
    left, lreceipt = _report_facts(linux, 'linux')
    right, rreceipt = _report_facts(windows, 'windows')
    commit = linux['candidate']['commit']
    if windows['candidate']['commit'] != commit:
        raise ValueError('Platform gates measured different candidates')
    modules = _source_modules(root, commit)
    _, authoritative = scope(root, modules, 'linux')
    for receipt in (lreceipt, rreceipt):
        for key in ('sources', 'selection_sha256', 'denominators'):
            if receipt[key] != authoritative[key]:
                raise ValueError(f'Source-derived platform {key} differ')
    source = {module['path']: module for module in modules}
    _check_points(left, authoritative, 'linux', source)
    _check_points(right, authoritative, 'windows', source)
    return {'schema': 1, 'commit': commit, 'modules': len(source), 'platforms': list(PLATFORMS),
            'selection_sha256': authoritative['selection_sha256'], 'exitCode': 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('linux', 'windows', 'output'):
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--root', default=Path('.'), type=Path)
    args = parser.parse_args()
    try:
        result = verify_union(json.loads(args.linux.read_text(encoding='utf-8')),
                              json.loads(args.windows.read_text(encoding='utf-8')), args.root)
    except (ValueError, KeyError, TypeError, OSError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        return 2
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

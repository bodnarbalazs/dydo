"""Reviewed native callable scopes, with a complete two-platform coverage union."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
import subprocess
import sys

from gate_policy import evaluate_policy

SELECTION = Path('DynaDocs.Tests/coverage/python-platforms.json')
PLATFORMS = ('linux', 'windows')


def _digest(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()


def _native_lines(source, rule):
    if set(rule) != {'sha256', 'windows'} or rule['sha256'] != _digest(source):
        raise ValueError('Platform selection source drift; review and update the selection')
    names = rule['windows']
    definitions = {node.name: node for node in ast.parse(source).body
                   if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef))}
    if not names or len(set(names)) != len(names) or set(names) - set(definitions):
        raise ValueError('Missing or duplicate native callable in platform selection')
    return {str(line) for name in names for line in range(
        min([definitions[name].lineno, *[d.lineno for d in definitions[name].decorator_list]]),
        definitions[name].end_lineno + 1)}


def _owners(module, native):
    def owner(line):
        return 'windows' if str(line) in native else 'linux'
    bodies = {m['id']: {line: owner(line) for line in m['body_lines']}
              for m in module['methods']}
    methods = {m['id']: sorted(set(bodies[m['id']].values())) if m['kind'] == 'module'
               else [owner(m['line'])] for m in module['methods']}
    return {'lines': {line: owner(line) for line in module['lines']},
            'branches': {edge: owner(edge.split(':')[0]) for edge in module['branches']},
            'methods': methods, 'bodies': bodies}


def _scoped(module, owners, platform):
    lines = {line: hits for line, hits in module['lines'].items()
             if owners['lines'][line] == platform}
    if not lines:
        return None
    branches = {edge: hits for edge, hits in module['branches'].items()
                if owners['branches'][edge] == platform}
    methods = []
    for method in module['methods']:
        if platform not in owners['methods'][method['id']]:
            continue
        if method['kind'] == 'module':
            body = {line: hits for line, hits in method['body_lines'].items()
                    if owners['bodies'][method['id']][line] == platform}
            method = {**method, 'body_lines': body, 'total': len(body),
                      'covered': sum(hits > 0 for hits in body.values())}
        methods.append(method)
    return {**module, 'lines': lines, 'branches': branches, 'methods': methods}


def load_selection(root, paths, platform, selection=None):
    """Validate the reviewed selection before starting the instrumented suite."""
    if selection is None:
        selection = json.loads((Path(root) / SELECTION).read_text(encoding='utf-8'))
    if (platform not in PLATFORMS or set(selection) != {'schema', 'neutral', 'native'}
            or type(selection['schema']) is not int or selection['schema'] != 1
            or selection['neutral'] != 'linux'):
        raise ValueError('Unsupported Python platform selection')
    if not paths or len(set(paths)) != len(paths) or set(selection['native']) - set(paths):
        raise ValueError('Platform selection module inventory drift')
    for path, rule in selection['native'].items():
        _native_lines((Path(root) / path).read_text(encoding='utf-8-sig'), rule)
    return selection


def scope(root, modules, platform, selection=None):
    """Partition points without exclusions; neutral modules and mixed remainders stay on Linux."""
    selection = load_selection(root, [m['path'] for m in modules], platform, selection)
    receipt = {'schema': 1, 'platform': platform, 'sources': {}, 'denominators': {},
               'selection_sha256': _digest(json.dumps(selection, sort_keys=True))}
    measured = []
    for module in modules:
        path = module['path']
        source = (Path(root) / path).read_text(encoding='utf-8-sig')
        native = _native_lines(source, selection['native'][path]) if path in selection['native'] else set()
        owners = _owners(module, native)
        receipt['sources'][path] = _digest(source)
        receipt['denominators'][path] = owners
        row = _scoped(module, owners, platform)
        if row is not None:
            measured.append(row)
    return measured, receipt


def _report_facts(report, platform, stack='python', collector='python-coverage'):
    if (type(report['schema']) is not int or report['schema'] != 1
            or report['stack'] != stack or report['gate'] != 'coverage'
            or report['exitCode'] != 0 or report['measurementComplete'] is not True
            or report['findings'] or report['gaps'] or report['candidate']['dirty'] is not False
            or not report['candidate']['commit']):
        name = 'Python' if stack == 'python' else stack
        raise ValueError(f'Platform union requires two clean passing {name} coverage gates')
    facts = report['collectors'][collector]['facts']
    receipt = facts['platform_selection']
    if type(receipt['schema']) is not int or receipt['schema'] != 1 or receipt['platform'] != platform:
        raise ValueError('Missing or wrong platform coverage receipt')
    if evaluate_policy(facts['modules']):
        raise ValueError('Platform coverage does not meet unchanged DR 048 policy')
    return facts['modules'], receipt


def _check_module(module, row, platform, source_methods):
    for kind in ('lines', 'branches'):
        if set(module[kind]) != {point for point, owner in row[kind].items() if owner == platform}:
            raise ValueError('Incomplete platform point union')
    methods = {m['id']: m for m in module['methods']}
    if set(methods) != {key for key, owners in row['methods'].items() if platform in owners}:
        raise ValueError('Incomplete platform callable union')
    for key, method in methods.items():
        _check_method(method, row['bodies'][key], source_methods[key], platform)


def _check_method(method, body, source_method, platform):
    if any(method[field] != source_method[field] for field in
           ('kind', 'line', 'end_line', 'cc', 'cognitive', 'parameters', 'constructor')):
        raise ValueError('Platform callable facts differ from source')
    if set(method['body_lines']) != {line for line, owner in body.items() if owner == platform}:
        raise ValueError('Incomplete platform callable body union')
    if (method['total'] != len(method['body_lines'])
            or method['covered'] != sum(hits > 0 for hits in method['body_lines'].values())):
        raise ValueError('Platform callable coverage disagrees with its body points')


def _check_points(modules, receipt, platform, source_modules):
    expected = receipt['denominators']
    actual = {m['path']: m for m in modules}
    selected = {path for path, row in expected.items() if platform in row['lines'].values()}
    if set(actual) != selected:
        raise ValueError('Incomplete platform module union')
    for path, module in actual.items():
        source_methods = {method['id']: method for method in source_modules[path]['methods']}
        _check_module(module, expected[path], platform, source_methods)


def _source_modules(root, commit):
    from gate_discovery import ordinary_discovery
    from gate_inventory import assemble_inventory
    from inventory import git_file_state
    root = Path(root).resolve()
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    toplevel = subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], cwd=root, text=True).strip()
    dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=root)
    if head != commit or dirty or Path(toplevel).resolve() != root:
        raise ValueError('Platform union requires the clean pinned checkout')
    paths, deleted = git_file_state(root)
    python_paths = [path for path in paths if path.lower().endswith('.py') or path.endswith('/SHA256SUMS')]
    inventory = assemble_inventory(root, python_paths, [], ordinary_discovery(root, python_paths), deleted)
    if inventory['errors']:
        raise ValueError(f"Invalid source-derived Python inventory: {inventory['errors']}")
    return [_source_module(root, row['path']) for row in inventory['sources'] if row['role'] == 'target']


def _source_module(root, path):
    import coverage
    from python_join import _methods
    from python_runtime import callable_inventory, module_inventory
    if sys.version_info[:3] != (3, 14, 4) or coverage.__version__ != '7.16.0':
        raise ValueError('Platform union requires pinned CPython 3.14.4 and coverage.py 7.16.0')
    absolute = str(root / path)
    source = (root / path).read_text(encoding='utf-8-sig')
    native = coverage.Coverage(data_file=None, branch=True, config_file=False)
    native.get_data().add_arcs({absolute: []})
    analysis = native._analyze(absolute)
    counter = {'callables': callable_inventory(source, absolute),
               'modules': [module_inventory(source, absolute)]}
    return {'path': path, 'language': 'python', 'executable': bool(analysis.statements),
            'lines': {str(line): 0 for line in analysis.statements},
            'branches': {f'{start}:{end}': 0 for start, end in analysis.arc_possibilities
                         if analysis.exit_counts[start] > 1},
            'methods': _methods(source, absolute, counter)}


def verify_union(linux, windows, root=Path('.')):
    """Fail closed unless both gates cover the complete same-candidate denominator."""
    left, lreceipt = _report_facts(linux, 'linux')
    right, rreceipt = _report_facts(windows, 'windows')
    if linux['candidate']['commit'] != windows['candidate']['commit']:
        raise ValueError('Platform gates measured different candidates')
    for key in ('sources', 'selection_sha256', 'denominators'):
        if not lreceipt[key] or lreceipt[key] != rreceipt[key]:
            raise ValueError('Platform source or denominator drift')
    modules = _source_modules(root, linux['candidate']['commit'])
    _, authoritative = scope(root, modules, 'linux')
    for key in ('sources', 'selection_sha256', 'denominators'):
        if lreceipt[key] != authoritative[key]:
            raise ValueError(f'Source-derived platform {key} differ')
    source_modules = {module['path']: module for module in modules}
    _check_points(left, authoritative, 'linux', source_modules)
    _check_points(right, authoritative, 'windows', source_modules)
    return {'schema': 1, 'commit': linux['candidate']['commit'],
            'modules': len(lreceipt['sources']), 'selection_sha256': lreceipt['selection_sha256'],
            'platforms': list(PLATFORMS), 'exitCode': 0}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--linux', required=True, type=Path)
    parser.add_argument('--windows', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
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

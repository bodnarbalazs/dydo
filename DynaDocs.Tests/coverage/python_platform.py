"""Reviewed native callable scopes, with a complete two-platform coverage union."""
import argparse
import ast
import hashlib
import json
from pathlib import Path
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


def _report_facts(report, platform):
    if (type(report['schema']) is not int or report['schema'] != 1
            or report['stack'] != 'python' or report['gate'] != 'coverage'
            or report['exitCode'] != 0 or report['measurementComplete'] is not True
            or report['findings'] or report['gaps'] or report['candidate']['dirty'] is not False
            or not report['candidate']['commit']):
        raise ValueError('Platform union requires two clean passing Python coverage gates')
    facts = report['collectors']['python-coverage']['facts']
    receipt = facts['platform_selection']
    if type(receipt['schema']) is not int or receipt['schema'] != 1 or receipt['platform'] != platform:
        raise ValueError('Missing or wrong platform coverage receipt')
    if evaluate_policy(facts['modules']):
        raise ValueError('Platform coverage does not meet unchanged DR 048 policy')
    return facts['modules'], receipt


def _check_module(module, row, platform):
    for kind in ('lines', 'branches'):
        if set(module[kind]) != {point for point, owner in row[kind].items() if owner == platform}:
            raise ValueError('Incomplete platform point union')
    methods = {m['id']: m for m in module['methods']}
    if (set(methods) != {key for key, owners in row['methods'].items() if platform in owners}
            or len(methods) != len(module['methods'])):
        raise ValueError('Incomplete platform callable union')
    for key, method in methods.items():
        if set(method['body_lines']) != {line for line, owner in row['bodies'][key].items() if owner == platform}:
            raise ValueError('Incomplete platform callable body union')
        if (method['total'] != len(method['body_lines'])
                or method['covered'] != sum(hits > 0 for hits in method['body_lines'].values())):
            raise ValueError('Platform callable coverage disagrees with its body points')


def _check_points(modules, receipt, platform):
    expected = receipt['denominators']
    actual = {m['path']: m for m in modules}
    selected = {path for path, row in expected.items() if platform in row['lines'].values()}
    if set(actual) != selected or len(actual) != len(modules):
        raise ValueError('Incomplete platform module union')
    for path, module in actual.items():
        _check_module(module, expected[path], platform)


def verify_union(linux, windows):
    """Fail closed unless both gates cover the complete same-candidate denominator."""
    left, lreceipt = _report_facts(linux, 'linux')
    right, rreceipt = _report_facts(windows, 'windows')
    if linux['candidate']['commit'] != windows['candidate']['commit']:
        raise ValueError('Platform gates measured different candidates')
    for key in ('sources', 'selection_sha256', 'denominators'):
        if not lreceipt[key] or lreceipt[key] != rreceipt[key]:
            raise ValueError('Platform source or denominator drift')
    _validate_owners(lreceipt)
    _check_points(left, lreceipt, 'linux')
    _check_points(right, rreceipt, 'windows')
    return {'schema': 1, 'commit': linux['candidate']['commit'],
            'modules': len(lreceipt['sources']), 'selection_sha256': lreceipt['selection_sha256'],
            'platforms': list(PLATFORMS), 'exitCode': 0}


def _validate_owners(receipt):
    if set(receipt['sources']) != set(receipt['denominators']):
        raise ValueError('Incomplete platform source denominator')
    for row in receipt['denominators'].values():
        owners = [*row['lines'].values(), *row['branches'].values(),
                  *(owner for body in row['bodies'].values() for owner in body.values())]
        if set(owners) - set(PLATFORMS) or set(row['methods']) != set(row['bodies']):
            raise ValueError('Unsupported platform point owner')
        for key, platforms in row['methods'].items():
            if not platforms or set(platforms) != set(row['bodies'][key].values()):
                raise ValueError('Missing platform callable owner')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--linux', required=True, type=Path)
    parser.add_argument('--windows', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    try:
        result = verify_union(json.loads(args.linux.read_text(encoding='utf-8')),
                              json.loads(args.windows.read_text(encoding='utf-8')))
    except (ValueError, KeyError, TypeError, OSError) as error:
        print(error, file=sys.stderr)
        return 2
    args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

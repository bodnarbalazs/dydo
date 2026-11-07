"""Reviewed .NET native member selection, with a complete two-platform coverage union."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from gate_policy import evaluate_policy

SELECTION = Path('DynaDocs.Tests/coverage/dotnet-platforms.json')
# The selected run's per-test results, written beside the campaign's other evidence.
WINDOWS_RESULTS = 'windows-tests.trx'
_TRX = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'
PLATFORMS = ('linux', 'windows')
SOURCE_FACTS = ('dydo', 'GateMetrics')
_SUMMARY = re.compile(r'(?:Passed|Failed)!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),'
                      r'\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)')
_TEST = re.compile(r'^(DynaDocs\.Tests(?:\.\w+)+)\.(\w+)\.(\w+)$')


def native_platform():
    return {'linux': 'linux', 'win32': 'windows'}.get(sys.platform)


def _digest(value):
    return hashlib.sha256(value.encode('utf-8')).hexdigest()


def source_digest(root, path):
    """Line endings are normalized, so a CRLF checkout pins the same reviewed source."""
    try:
        data = (Path(root) / path).read_bytes()
    except OSError as error:
        raise ValueError(f'Missing platform selection source: {path}') from error
    return hashlib.sha256(data.replace(b'\r\n', b'\n')).hexdigest()


def _check_tests(tests):
    if set(tests) != {'reason', 'tests'} or not isinstance(tests['reason'], str) or not tests['reason']:
        raise ValueError('Missing Windows test selection or its reason')
    names = tests['tests']
    if (not isinstance(names, list) or not names or len(set(names)) != len(names)
            or not all(isinstance(name, str) and _TEST.match(name) for name in names)):
        raise ValueError('Missing, duplicate or unqualified Windows test')


def _check_rule(root, path, rule):
    if not isinstance(rule, dict) or set(rule) != {'sha256', 'windows'} or not path.endswith('.cs'):
        raise ValueError(f'Invalid native member rule: {path}')
    names = rule['windows']
    if (not isinstance(names, list) or not names or len(set(names)) != len(names)
            or not all(isinstance(name, str) and name for name in names)):
        raise ValueError(f'Missing or duplicate native member: {path}')
    if source_digest(root, path) != rule['sha256']:
        raise ValueError(f'Platform selection source drift: {path}; review and update the selection')


def load_selection(root, platform, selection=None):
    """Validate the reviewed selection before any campaign starts."""
    if selection is None:
        try:
            selection = json.loads((Path(root) / SELECTION).read_text(encoding='utf-8'))
        except OSError as error:
            raise ValueError(f'Missing .NET platform selection: {SELECTION.as_posix()}') from error
    if (platform not in PLATFORMS or not isinstance(selection, dict)
            or set(selection) != {'schema', 'neutral', 'windows_tests', 'native'}
            or type(selection['schema']) is not int or selection['schema'] != 1
            or selection['neutral'] != 'linux' or not isinstance(selection['native'], dict)
            or not selection['native'] or not isinstance(selection['windows_tests'], dict)):
        raise ValueError('Unsupported .NET platform selection')
    _check_tests(selection['windows_tests'])
    for path, rule in selection['native'].items():
        _check_rule(root, path, rule)
    return selection


def windows_filter(selection):
    return '|'.join(f'FullyQualifiedName={name}' for name in selection['windows_tests']['tests'])


def discover_windows_tests(root, selection):
    """Each named Windows test is declared once, in its class, under DynaDocs.Tests."""
    sources = [path for path in (Path(root) / 'DynaDocs.Tests').rglob('*.cs')
               if not {'bin', 'obj'} & set(path.parts)]
    texts = {path: path.read_text(encoding='utf-8-sig') for path in sources}
    found = {}
    for name in selection['windows_tests']['tests']:
        namespace, cls, method = _TEST.match(name).groups()
        files = [path for path, text in texts.items()
                 if re.search(rf'^namespace {re.escape(namespace)};', text, re.M)
                 and re.search(rf'\bclass {cls}\b', text)
                 and re.search(rf'\b(?:void|Task)\s+{method}\s*\(', text)]
        if len(files) != 1:
            raise ValueError(f'Windows test is not declared exactly once: {name}')
        found[name] = files[0].relative_to(root).as_posix()
    return found


def selected_test_counts(stdout):
    """The selected suite's own summary: nonzero, and nothing it selected was skipped."""
    rows = [tuple(map(int, match.groups())) for match in _SUMMARY.finditer(stdout)]
    if len(rows) != 1:
        raise ValueError('Missing or ambiguous selected test summary')
    failed, passed, skipped, total = rows[0]
    if not total or failed or skipped:
        raise ValueError(f'Selected Windows tests must all run and pass: {rows[0]}')
    return {'passed': passed, 'total': total}


def _method(test):
    """A definition's one named method; no id, no method, two methods or a nameless one is ambiguous."""
    if not test.get('id'):
        raise ValueError(f"Windows test definition without its id: {test.get('name')}")
    methods = test.findall(_TRX + 'TestMethod')
    if len(methods) != 1 or not methods[0].get('className') or not methods[0].get('name'):
        raise ValueError(f"Ambiguous Windows test definition: {test.get('id')}")
    return f"{methods[0].get('className')}.{methods[0].get('name')}"


def _ran(results, declared):
    """Each result's method, by its definition; a theory's rows count under their method."""
    root = ET.fromstring(results)
    rows = list(root.iter(_TRX + 'UnitTestResult'))
    for result in rows:
        if not result.get('testId'):
            raise ValueError(f"Windows test result without its testId: {result.get('testName')}")
    methods = {}
    for test in root.iter(_TRX + 'UnitTest'):
        name = _method(test)
        if methods.setdefault(test.get('id'), name) != name:
            raise ValueError(f"Ambiguous Windows test definition: {test.get('id')}")
    ran = []
    for result in rows:
        name = methods.get(result.get('testId'))
        if name not in declared:
            raise ValueError(f"Undeclared Windows test ran: {name or result.get('testName')}")
        if result.get('outcome') != 'Passed':
            raise ValueError(f"Windows test did not pass: {result.get('testName')} is {result.get('outcome')}")
        ran.append(name)
    return ran


def selected_tests(evidence, selection):
    """Every declared Windows test ran and passed, nothing else ran, and the summary counts the same cases."""
    counts = selected_test_counts(
        (Path(evidence) / 'altcover-runner.stdout').read_text(encoding='utf-8', errors='replace'))
    declared = selection['windows_tests']['tests']
    try:
        ran = _ran((Path(evidence) / WINDOWS_RESULTS).read_bytes(), declared)
    except (OSError, ET.ParseError) as error:
        raise ValueError(f'Missing or unreadable Windows test results: {WINDOWS_RESULTS}') from error
    missing = [name for name in declared if name not in ran]
    if missing:
        raise ValueError('Declared Windows tests did not run: ' + ', '.join(missing))
    if len(ran) != counts['total']:
        raise ValueError(f"Windows test results hold {len(ran)} cases but the summary {counts['total']}")
    return {**counts, 'names': sorted(set(ran))}


def _members(selection, source_files):
    """The (type, member) declarations each selected source names, resolved by Roslyn facts."""
    declared = {row['path']: row['methods'] for row in source_files}
    members = {}
    for path, rule in selection['native'].items():
        if path not in declared:
            raise ValueError(f'Platform selection source outside the .NET inventory: {path}')
        found = {(row['type'], row['member']) for row in declared[path] if row['member'] in rule['windows']}
        if {member for _, member in found} != set(rule['windows']):
            raise ValueError(f'Missing native member in platform selection: {path}')
        members[path] = found
    return members


def _named(identity):
    head = identity.split('|', 1)[0]
    owner, _, member = head.partition('::')
    return owner.replace('/', '.'), re.split(r'[@`]', member, maxsplit=1)[0]


def _owner(path, identity, members):
    """A callable nested in a selected member, a lambda or local function, stays with it."""
    owner_type, member = _named(identity)
    for selected_type, selected in members.get(path, ()):
        scope = selected_type + '.' + selected
        if ((owner_type, member) == (selected_type, selected) or owner_type == scope
                or owner_type.startswith(scope + '.')):
            return 'windows'
    return 'linux'


def _body_owner(module, body, members):
    logical = body['logical']
    if logical is None:
        return _owner(module['path'], body['key'], members)
    return _owner(logical['path'], logical['id'], members)


def _origin(edge):
    return edge.rsplit(':', 5)[1]


def owners(module, members):
    """Every point's platform: the member whose emitted body owns it; a branch, its origin line's."""
    lines = {}
    for body in module['body_owners']:
        owner = _body_owner(module, body, members)
        for line in body['lines']:
            if lines.setdefault(line, owner) != owner:
                raise ValueError(f"Mixed-platform source line: {module['path']}:{line}")
    if set(lines) != set(module['lines']):
        raise ValueError(f"Emitted bodies do not account for every line: {module['path']}")
    return {'lines': lines,
            'branches': {edge: lines[_origin(edge)] for edge in module['branches']},
            'methods': {method['id']: _owner(module['path'], method['id'], members)
                        for method in module['methods']}}


def _scoped(module, table, platform):
    lines = {line: hits for line, hits in module['lines'].items() if table['lines'][line] == platform}
    if not lines:
        return None
    return {**module, 'lines': lines,
            'branches': {edge: hits for edge, hits in module['branches'].items()
                         if table['branches'][edge] == platform},
            'methods': [method for method in module['methods'] if table['methods'][method['id']] == platform],
            'body_owners': [body for body in module['body_owners']
                            if table['lines'][next(iter(body['lines']))] == platform]}


def scope(root, modules, source_files, platform, selection=None):
    """Partition every point without exclusions; neutral code and mixed remainders stay on Linux."""
    selection = load_selection(root, platform, selection)
    members = _members(selection, source_files)
    receipt = {'schema': 1, 'platform': platform, 'sources': {}, 'denominators': {},
               'selection_sha256': _digest(json.dumps(selection, sort_keys=True))}
    measured = []
    for module in modules:
        table = owners(module, members)
        receipt['sources'][module['path']] = source_digest(root, module['path'])
        receipt['denominators'][module['path']] = table
        row = _scoped(module, table, platform)
        if row is not None:
            measured.append(row)
    if not any('windows' in table['lines'].values() for table in receipt['denominators'].values()):
        raise ValueError('Platform selection names no measured Windows point')
    return measured, receipt


def read_source_files(evidence):
    files = []
    for name in SOURCE_FACTS:
        files.extend(json.loads((Path(evidence) / f'source-facts-{name}.json').read_text(encoding='utf-8'))['files'])
    return files


def _report_facts(report, platform):
    if (type(report['schema']) is not int or report['schema'] != 1
            or report['stack'] != 'dotnet' or report['gate'] != 'coverage'
            or report['measurementComplete'] is not True or report['gaps']
            or report['candidate']['dirty'] is not False or not report['candidate']['commit']):
        raise ValueError('Platform union requires complete clean .NET coverage gates')
    if report['exitCode'] != 0 or report['findings']:
        raise ValueError(f'Platform union requires a passing {platform} .NET coverage gate')
    facts = report['collectors']['csharp-coverage']['facts']
    receipt = facts['platform_selection']
    # Windows counts the selected tests it ran; Linux counts none, because it runs the full suite.
    tests = receipt.get('tests')
    suite = bool(tests and tests['total']) if platform == 'windows' else tests is None
    if (type(receipt['schema']) is not int or receipt['schema'] != 1 or receipt['platform'] != platform
            or not suite):
        raise ValueError('Missing or wrong platform coverage receipt')
    if evaluate_policy(facts['modules']) != report['findings']:
        raise ValueError('Platform coverage findings disagree with its measured points')
    return facts['modules'], receipt


def _check_method(method, source):
    if any(method.get(field) != source.get(field) for field in
           ('kind', 'line', 'end_line', 'cc', 'cognitive', 'parameters', 'constructor', 'total')):
        raise ValueError(f"Platform callable facts differ from source: {method['id']}")


def _check_module(module, table, platform, source):
    for kind in ('lines', 'branches', 'methods'):
        expected = {point for point, owner in table[kind].items() if owner == platform}
        actual = set(module[kind]) if kind != 'methods' else {method['id'] for method in module['methods']}
        if actual != expected:
            raise ValueError(f"Incomplete platform {kind} union: {module['path']}")
    rebuilt = {method['id']: method for method in source['methods']}
    for method in module['methods']:
        _check_method(method, rebuilt[method['id']])


def _check_points(modules, receipt, platform, rebuilt):
    selected = {path for path, table in receipt['denominators'].items() if platform in table['lines'].values()}
    actual = {module['path']: module for module in modules}
    if set(actual) != selected:
        raise ValueError(f'Incomplete platform module union: {platform}')
    for path, module in actual.items():
        _check_module(module, receipt['denominators'][path], platform, rebuilt[path])


def _clean_checkout(root, commit):
    root = Path(root).resolve()
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
    toplevel = subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], cwd=root, text=True).strip()
    dirty = subprocess.check_output(['git', 'status', '--porcelain'], cwd=root)
    if head != commit or dirty or Path(toplevel).resolve() != root:
        raise ValueError('Platform union requires the clean pinned checkout')


def rebuild_denominator(root, output):
    """This host's zero-hit join of the candidate: its own build, PDB identities and instrumentation."""
    campaign = Path(__file__).resolve().with_name('csharp_coverage.py')
    command = [sys.executable, str(campaign), '--denominator', '--root', str(Path(root).resolve()),
               '--result-root', str(Path(output).resolve())]
    if subprocess.run(command, check=False).returncode != 0:
        raise ValueError(f'Candidate denominator rebuild failed; see {output}')
    joined = json.loads((Path(output) / 'joined.json').read_text(encoding='utf-8'))
    return joined['modules'], read_source_files(output)


def _platform_result(platform, facts, authoritative, rebuilt):
    modules, receipt = facts
    for key in ('sources', 'selection_sha256', 'denominators'):
        if receipt[key] != authoritative[key]:
            raise ValueError(f'Source-derived platform {key} differ: {platform}')
    _check_points(modules, authoritative, platform, rebuilt)
    return {'modules': len(modules), **({'tests': receipt['tests']} if 'tests' in receipt else {})}


def verify_union(linux, windows, root, output):
    """Fail closed unless both gates pass on their exact portions of the rebuilt candidate partition."""
    facts = {'linux': _report_facts(linux, 'linux'), 'windows': _report_facts(windows, 'windows')}
    commit = linux['candidate']['commit']
    if windows['candidate']['commit'] != commit:
        raise ValueError('Platform gates measured different candidates')
    _clean_checkout(root, commit)
    modules, source_files = rebuild_denominator(root, output)
    _, authoritative = scope(root, modules, source_files, 'linux')
    rebuilt = {module['path']: module for module in modules}
    return {'schema': 1, 'commit': commit, 'selection_sha256': authoritative['selection_sha256'],
            'modules': len(rebuilt), 'exitCode': 0,
            'platforms': {platform: _platform_result(platform, rows, authoritative, rebuilt)
                          for platform, rows in facts.items()}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--linux', required=True, type=Path)
    parser.add_argument('--windows', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--root', default=Path('.'), type=Path)
    args = parser.parse_args()
    try:
        result = verify_union(json.loads(args.linux.read_text(encoding='utf-8')),
                              json.loads(args.windows.read_text(encoding='utf-8')), args.root,
                              args.output / 'denominator')
    except (ValueError, KeyError, TypeError, OSError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        return 2
    (args.output / 'union.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())

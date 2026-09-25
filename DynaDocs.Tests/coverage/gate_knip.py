"""One native graph across real packages, with explicit source accounting."""
import json
from pathlib import Path

from gate_inventory import TYPESCRIPT_OUTSIDE_ROOT
from gate_run import result


PACKAGE_ROOTS = {'.': '../..', 'DynaDocs.Tests/coverage': '.', 'DynaDocs.Tests/HostCanaries': '../HostCanaries',
                 'npm': '../../npm'}
EXTENSIONS = {'.js', '.mjs', '.cjs'}
ISSUES = ('files', 'exports', 'nsExports', 'duplicates', 'unresolved')
HOST_ENTRIES = {'js_metrics.cjs', 'js_runtime.cjs', 'js_loader.mjs', 'test_discovery.cjs',
                'knip_reporter.mjs', 'install.js', 'bin/dydo', 'bin/dydo.cjs',
                'run-host-canaries.mjs', 'openai-sse-provider.mjs', 'setup-skills.mjs'}
# Knip always adds its working directory as a workspace, so the viewer's graph declares this
# package too, with nothing in it; index.html loads the viewer's one host entry.
LANGUAGES = {'javascript': {'packages': PACKAGE_ROOTS, 'extensions': EXTENSIONS, 'entries': HOST_ENTRIES},
             'typescript': {'packages': {'DynaDocs.Tests/coverage': '.', 'viewer': '../../viewer'},
                            'extensions': {'.ts', '.tsx'}, 'entries': {'src/main.tsx'}}}


def source_owner(root, path, packages=PACKAGE_ROOTS):
    absolute = (root / path).resolve()
    if absolute.relative_to(root).as_posix() != path:
        raise ValueError('Noncanonical native source path')
    owners = [folder for folder in packages if absolute.is_relative_to(root / folder)]
    owner = max(owners, key=len)
    return owner, absolute.relative_to(root / owner).as_posix()


def workspace_model(root, paths, language='javascript'):
    root = Path(root).resolve()
    spec = LANGUAGES[language]
    packages = spec['packages']
    config = {'workspaces': {key: {'entry': [], 'project': []} for key in packages.values()}}
    sources, errors, seen = [], [], set()
    for folder in packages:
        if not (root / folder / 'package.json').is_file():
            errors.append({'path': folder, 'message': 'Missing actual package manifest'})
    for path in sorted(paths):
        try:
            if path in seen:
                raise ValueError('Duplicate maintained source identity')
            seen.add(path)
            folder, local = source_owner(root, path, packages)
            supported = Path(local).suffix in spec['extensions']
            sources.append({'path': path, 'workspace': folder, 'local': local, 'supported': supported})
            workspace = config['workspaces'][packages[folder]]
            workspace['project'].append(local)
            if '.test.' in local or local in spec['entries']:
                workspace['entry'].append(local)
            if not supported:
                raise ValueError('Unsupported maintained JavaScript extension; native imports are not measured')
        except ValueError as error:
            errors.append({'path': path, 'message': str(error)})
    return {'config': config, 'packages': sorted(packages), 'sources': sources, 'errors': errors}


def native_measurement(root, model, row):
    if row.get('kind') != 'measurement':
        raise ValueError('Missing native measurement row')
    workspaces = row['includedWorkspaceDirs']
    identities = [Path(value).resolve() for value in workspaces]
    if len(identities) != len(set(identities)) or set(identities) != {root / folder for folder in model['packages']}:
        raise ValueError('Native workspace inventory disagrees with containing packages')
    counts = row['counters']
    if not all(type(counts.get(key)) is int and counts[key] >= 0 for key in (*ISSUES, 'total', 'processed')):
        raise ValueError('Missing or invalid native file/issue counters')
    expected = sum(source['supported'] for source in model['sources'])
    if counts['total'] != expected or counts['processed'] != expected:
        raise ValueError(f'Native source count disagrees with supported inventory: expected {expected}, got {counts}')
    return counts


def issue_findings(root, paths, rows):
    findings, seen = [], set()
    for row in rows:
        absolute = (root / 'DynaDocs.Tests/coverage' / row['file']).resolve()
        path = absolute.relative_to(root).as_posix()
        if path not in paths or path in seen:
            raise ValueError('Unknown or duplicate native issue source identity')
        seen.add(path)
        if set(row) != {'file', *ISSUES}:
            raise ValueError('Unknown or missing native issue categories')
        for kind in ISSUES:
            if not isinstance(row[kind], list):
                raise ValueError('Invalid native issue list')
            findings.extend({'path': path, 'gate': 'knip-' + kind, 'diagnostic': item} for item in row[kind])
    return findings


def normalize_report(root, model, rows):
    root = Path(root).resolve()
    findings, errors = [], list(model['errors'])
    try:
        if len(rows) != 2 or set(rows[0]) != {'issues'}:
            raise ValueError('Incomplete or duplicate native report rows')
        counts = native_measurement(root, model, rows[1])
        findings = issue_findings(root, {row['path'] for row in model['sources']}, rows[0]['issues'])
        for kind in ISSUES:
            if counts[kind] != sum(item['gate'] == 'knip-' + kind for item in findings):
                raise ValueError('Native issue counters disagree with detailed report')
    except (ValueError, KeyError, TypeError) as error:
        errors.append({'message': str(error)})
    return result({'model': model, 'native': rows}, findings, errors)


def _judged_paths(runner, language):
    paths = [row['path'] for row in runner.sources(language)]
    if language == 'typescript':
        # Knip's plugins load the viewer's whole package, its specs and configs included, and an
        # unused verdict needs that whole graph; so those files are judged here too.
        paths += [row['path'] for row in runner.inventory['excluded'] if row['reason'] == TYPESCRIPT_OUTSIDE_ROOT]
    return paths


def collect_knip(runner, language='javascript'):
    model = workspace_model(runner.root, _judged_paths(runner, language), language)
    config = runner.output / 'knip.json'
    config.write_text(json.dumps(model['config'], indent=2), encoding='utf-8')
    row = runner.log.run('knip', ['node', runner.coverage / 'node_modules/knip/bin/knip.js',
        '--config', config, '--reporter', runner.coverage / 'knip_reporter.mjs',
        '--no-gitignore', '--include-entry-exports', '--include', ','.join(ISSUES)], cwd=runner.coverage)
    if row['exit_code'] not in (0, 1):
        return result({'model': model}, errors=[*model['errors'], {'message': 'Native Knip execution failed', 'command': row}])
    rows = [json.loads(line) for line in Path(row['stdout']).read_text(encoding='utf-8').splitlines() if line.strip()]
    answer = normalize_report(runner.root, model, rows)
    if row['exit_code'] == 1 and not answer['findings']:
        answer['errors'].append({'message': 'Native Knip failed without accounted issues', 'command': row})
        answer['status'] = 'error'
    return answer

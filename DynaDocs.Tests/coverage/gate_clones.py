"""Pinned clone detection with explicit per-source native eligibility evidence."""
import hashlib
import json
from collections import Counter
from pathlib import Path

from gate_run import file_hash, result


def _denominator(stats):
    if stats.get('sources') != 1:
        raise ValueError('Missing or ambiguous per-source native clone denominator')
    values = {key: stats[key] for key in ('lines', 'tokens')}
    if any(type(value) is not int or value < 0 for value in values.values()):
        raise ValueError('Invalid native clone denominator')
    return values


def eligibility(standard, lower=None):
    if standard.get('sources') == 1:
        denominator = _denominator(standard)
        included = True
    elif standard.get('sources') == 0 and lower is not None:
        denominator = _denominator(lower)
        included = False
    else:
        raise ValueError('Omitted source lacks exact current native eligibility proof')
    meets_bounds = denominator['lines'] >= 15 and denominator['tokens'] >= 100
    if not included and meets_bounds:
        raise ValueError('Native clone omission disagrees with eligibility lower bounds')
    return {'eligible': meets_bounds, 'standard_included': included, 'denominator': denominator}


def _native(runner, paths, label, minimum=15):
    output = runner.output / 'clones' / label
    maximum = max((runner.root / path).stat().st_size for path in paths) + 1
    command = runner.log.run('jscpd-' + label, ['node', runner.coverage / 'node_modules/jscpd/run-jscpd.js',
        '--absolute', '--format', 'csharp,python,javascript', '--formats-names', 'javascript:dydo',
        '--no-gitignore', '--min-lines', str(minimum), '--min-tokens', '100' if minimum == 15 else '1',
        '--max-lines', '2147483647', '--max-size', str(maximum), '--mode', 'strict', '--reporters', 'json',
        '--output', output, '--silent', '--no-tips', *paths])
    if command['exit_code'] != 0:
        raise ValueError('Native jscpd command failed: ' + command['stderr'])
    report = output / 'jscpd-report.json'
    return {'report': str(report), 'sha256': file_hash(report), 'command': command,
            'data': json.loads(report.read_text(encoding='utf-8'))}


def _audit(runner, source):
    path = source['path']
    identity = hashlib.sha256(path.encode('utf-8')).hexdigest()[:16]
    native = _native(runner, [path], identity)
    stats = native['data']['statistics']['total']
    lower = _native(runner, [path], identity + '-bounds', minimum=1) if stats['sources'] == 0 else None
    proof = eligibility(stats, lower['data']['statistics']['total'] if lower else None)
    return {'path': path, 'source_sha256': file_hash(runner.root / path), 'language': source['language'],
            **proof, 'native': native, 'lower_bound': lower}


def _findings(runner, duplicates, paths):
    findings = []
    for clone in duplicates:
        pair = []
        for key in ('firstFile', 'secondFile'):
            fragment = clone[key]
            filename = fragment['name'].removeprefix('\\\\?\\')
            relative = Path(filename).resolve().relative_to(runner.root).as_posix()
            if relative not in paths or fragment['start'] < 1 or fragment['end'] < fragment['start']:
                raise ValueError('Native clone has an unknown source identity/span')
            pair.append({'path': relative, 'start': fragment['start'], 'end': fragment['end']})
        if clone['lines'] < 15 or clone['tokens'] < 100:
            raise ValueError('Native clone violates pinned AND thresholds')
        findings.append({'gate': 'clone', 'fragments': pair, 'lines': clone['lines'], 'tokens': clone['tokens']})
    return findings


def collect_clones(runner):
    sources = runner.inventory['sources']
    paths = [source['path'] for source in sources]
    batch = _native(runner, paths, 'all')
    findings = _findings(runner, batch['data']['duplicates'], paths)
    audits, errors = [], []
    for source in sources:
        try:
            audits.append(_audit(runner, source))
        except (ValueError, KeyError, OSError) as error:
            errors.append({'path': source['path'], 'message': str(error)})
    expected = Counter('csharp' if row['language'] == 'cs' else row['language']
                       for row in audits if row['standard_included'])
    measured = {key: value['sources'] for key, value in batch['data']['statistics']['formats'].items() if value['sources']}
    if dict(expected) != measured:
        errors.append({'message': 'Batch clone inventory disagrees with exact per-source eligibility',
                       'expected': dict(expected), 'measured': measured})
    return result({'batch': batch, 'eligibility': audits}, findings, errors)

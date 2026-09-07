"""Join native SARIF to maintained sources and exact semantic generated outputs."""
import json
from pathlib import Path
from urllib.parse import urlsplit
from urllib.request import url2pathname


def _location(root, location):
    file = location['resultFile']
    uri = urlsplit(file['uri'])
    if uri.scheme != 'file' or uri.netloc or uri.query or uri.fragment:
        raise ValueError('Unrecognized native analyzer file URI')
    path = Path(url2pathname(uri.path)).resolve().relative_to(root).as_posix()
    region = file['region']
    keys = ('startLine', 'startColumn', 'endLine', 'endColumn')
    if any(type(region.get(key)) is not int or region[key] < 1 for key in keys):
        raise ValueError('Missing analyzer source region')
    if (region['endLine'], region['endColumn']) < (region['startLine'], region['startColumn']):
        raise ValueError('Reversed analyzer source region')
    return {'path': path, **{key: region[key] for key in keys}}


def _native_row(root, row):
    diagnostic = row['diagnostic']
    locations = [_location(root, location) for location in diagnostic.get('locations', [])]
    if not locations:
        raise ValueError('Native diagnostic has no exact source location')
    normalized = {'rule': diagnostic['ruleId'], 'level': diagnostic['level'],
                  'message': diagnostic['message'], 'locations': locations,
                  'suppression_states': sorted(diagnostic.get('suppressionStates', []))}
    return normalized, json.dumps(normalized, sort_keys=True)


def _classify(row, maintained, generated):
    paths = {location['path'] for location in row['locations']}
    if paths <= maintained:
        gate = 'maintained-diagnostic-suppression' if row['suppression_states'] else 'csharp-analyzer'
        return 'findings', {'gate': gate, 'path': row['locations'][0]['path'], **row}
    if paths <= set(generated):
        return 'generated', {'reason': 'Exact generated source from real semantic compilation',
                             'origin_projects': sorted({project for path in paths for project in generated[path]}), **row}
    return 'errors', {'message': 'Native diagnostic source absent from maintained and semantic generated inventories', **row}


def normalize_csharp_diagnostics(root, rows, maintained, generated):
    root = Path(root).resolve()
    unique = {}
    report = {'findings': [], 'generated': [], 'errors': [], 'raw_count': len(rows)}
    for index, row in enumerate(rows):
        try:
            normalized, key = _native_row(root, row)
            current = unique.setdefault(key, {**normalized, 'witnesses': []})
            current['witnesses'].append({'project': row['project'], 'native_row': index})
        except (ValueError, KeyError, OSError) as error:
            report['errors'].append({'message': str(error), 'project': row['project'], 'native_row': index})
    for row in unique.values():
        category, classified = _classify(row, maintained, generated)
        report[category].append(classified)
    return report


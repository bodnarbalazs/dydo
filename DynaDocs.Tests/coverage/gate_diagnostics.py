"""Join native SARIF to maintained sources and exact semantic generated outputs."""
import json
from pathlib import Path
from pathlib import PurePosixPath
from urllib.parse import unquote, urlsplit
from urllib.request import url2pathname


def _relative_generated_location(root, project, uri_path, generated):
    decoded = unquote(uri_path)
    relative = PurePosixPath(decoded)
    project_path = PurePosixPath(project)
    if (not decoded or '\\' in decoded or relative.is_absolute() or '..' in relative.parts
            or project_path.is_absolute() or '..' in project_path.parts):
        raise ValueError('Unrecognized native analyzer file URI')
    project_root = (root / Path(*project_path.parent.parts)).resolve()
    source = (project_root / Path(*relative.parts)).resolve()
    source_path = source.relative_to(project_root)
    source_relative = (project_root / source_path).relative_to(root).as_posix()
    generated_prefix = project_path.parent / 'obj'
    suffix = '/' + relative.as_posix() + '.cs'
    candidates = sorted(path for path, projects in generated.items()
                        if project in projects
                        and PurePosixPath(path).is_relative_to(generated_prefix)
                        and ('/' + path).endswith(suffix))
    if len(candidates) != 1:
        raise ValueError('Relative native analyzer URI has no unique same-project generated source')
    return source_relative, candidates[0]


def _location(root, project, location, generated):
    file = location['resultFile']
    uri = urlsplit(file['uri'])
    if uri.netloc or uri.query or uri.fragment:
        raise ValueError('Unrecognized native analyzer file URI')
    generated_origin = None
    if uri.scheme == 'file':
        path = Path(url2pathname(uri.path)).resolve().relative_to(root).as_posix()
    elif not uri.scheme:
        path, generated_origin = _relative_generated_location(
            root, project, uri.path, generated)
    else:
        raise ValueError('Unrecognized native analyzer file URI')
    region = file['region']
    keys = ('startLine', 'startColumn', 'endLine', 'endColumn')
    if any(type(region.get(key)) is not int or region[key] < 1 for key in keys):
        raise ValueError('Missing analyzer source region')
    if (region['endLine'], region['endColumn']) < (region['startLine'], region['startColumn']):
        raise ValueError('Reversed analyzer source region')
    result = {'path': path, **{key: region[key] for key in keys}}
    if generated_origin is not None:
        result['generated_origin'] = generated_origin
    return result


def _native_row(root, row, generated):
    diagnostic = row['diagnostic']
    locations = [_location(root, row['project'], location, generated)
                 for location in diagnostic.get('locations', [])]
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
    generated_origins = {location.get('generated_origin') for location in row['locations']}
    if None not in generated_origins:
        return 'generated', {
            'reason': 'Exact project-relative source map to semantic generated output',
            'mapped_sources': sorted(paths),
            'generated_origins': sorted(generated_origins),
            'origin_projects': sorted({project for path in generated_origins for project in generated[path]}),
            **row,
        }
    return 'errors', {'message': 'Native diagnostic source absent from maintained and semantic generated inventories', **row}


def normalize_csharp_diagnostics(root, rows, maintained, generated):
    root = Path(root).resolve()
    unique = {}
    report = {'findings': [], 'generated': [], 'errors': [], 'raw_count': len(rows)}
    for index, row in enumerate(rows):
        try:
            normalized, key = _native_row(root, row, generated)
            current = unique.setdefault(key, {**normalized, 'witnesses': []})
            current['witnesses'].append({'project': row['project'], 'native_row': index})
        except (ValueError, KeyError, OSError) as error:
            report['errors'].append({'message': str(error), 'project': row['project'], 'native_row': index})
    for row in unique.values():
        if row['level'] not in ('warning', 'error'):
            continue
        category, classified = _classify(row, maintained, generated)
        report[category].append(classified)
    return report

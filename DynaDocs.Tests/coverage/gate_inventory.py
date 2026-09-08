"""Whole source inventory, explicit uncertain joins, and semantic graph cycles."""
import hashlib
from pathlib import Path, PurePosixPath

from inventory import build_file_rows, checked_paths, language_of, source_fingerprint


def test_project_role(value, packages):
    value = value.lower()
    if value not in ('', 'false', 'true'):
        raise ValueError('Unknown evaluated IsTestProject value')
    if 'microsoft.net.test.sdk' in {name.lower() for name in packages} and value != 'true':
        raise ValueError('Declared test SDK lacks its restored evaluated test-project role')
    return value == 'true'


def assembly_path(root, value):
    root = root.resolve()
    target = Path(value)
    target = target.resolve() if target.is_absolute() else (root / target).resolve()
    if not target.is_relative_to(root):
        raise ValueError('Assembly target path escapes repository root')
    return target.relative_to(root).as_posix()


def _role(relative, language, projects, test_files):
    owners = [project for project in projects if relative in project['compile']]
    if language == 'cs':
        if not owners:
            return 'unknown', [], {'path': relative, 'type': 'missing-evaluated-compile'}
        if len({owner['test'] for owner in owners}) != 1:
            return 'unknown', owners, {'path': relative, 'type': 'ambiguous-compile-role',
                                       'projects': [owner['path'] for owner in owners]}
        return ('test' if owners[0]['test'] else 'target'), owners, None
    return ('test' if relative in test_files else 'target'), [], None


_DERIVED = 'dydo/reference/gap-check.example.py'
_DERIVED_SOURCE = 'DynaDocs.Tests/coverage/gap_check.py'
_DERIVED_PRODUCER = 'DynaDocs.Tests/coverage/sync_testing_example.py'
_DERIVED_TEST = 'DynaDocs.Tests/coverage/tests/test_sync_testing_example.py'
_NATIVE_EVIDENCE = 'dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence'


def _structural_exclusion(root, relative, paths, test_files):
    """Return reproducible exclusion evidence, or a gap that denies exclusion."""
    path_set = set(paths)
    if relative == _DERIVED:
        required = (_DERIVED_SOURCE, _DERIVED_PRODUCER, _DERIVED_TEST)
        missing = [path for path in required if path not in path_set or not (root / path).is_file()]
        if _DERIVED_TEST not in test_files:
            missing.append(_DERIVED_TEST + '#native-discovery')
        if missing:
            return None, {'path': relative, 'type': 'derived-copy-origin-missing',
                          'missing': sorted(set(missing))}
        source_digest = hashlib.sha256((root / _DERIVED_SOURCE).read_bytes()).hexdigest()
        if (root / relative).read_bytes() != (root / _DERIVED_SOURCE).read_bytes():
            return None, {'path': relative, 'type': 'derived-copy-diverged',
                          'source': _DERIVED_SOURCE, 'canonicalSourceSha256': source_digest}
        return {'path': relative, 'reason': 'derived-copy',
                'origin': {'source': _DERIVED_SOURCE,
                           'canonicalSourceSha256': source_digest,
                           'producer': _DERIVED_PRODUCER,
                           'producerTest': _DERIVED_TEST}}, None
    prefix = PurePosixPath(_NATIVE_EVIDENCE)
    candidate = PurePosixPath(relative)
    if candidate != prefix and prefix in candidate.parents:
        manifest = f'{_NATIVE_EVIDENCE}/SHA256SUMS'
        manifest_path = root / manifest
        if manifest not in path_set or not manifest_path.is_file():
            return None, {'path': relative, 'type': 'fixture-origin-missing', 'manifest': manifest}
        return {'path': relative, 'reason': 'native-evidence-fixture',
                'origin': {'manifest': manifest,
                           'manifestSha256': hashlib.sha256(manifest_path.read_bytes()).hexdigest()}}, None
    return None, None


def assemble_inventory(root, paths, projects, discovery, deleted=frozenset(), associations=None):
    test_files = {row['file'] for row in discovery}
    if not test_files <= set(paths):
        raise ValueError('Missing discovered test source in Git inventory')
    identities = [row['id'] for row in discovery]
    if len(identities) != len(set(identities)):
        raise ValueError('Ambiguous discovered test case identity')
    sources, excluded, errors = [], [], []
    association_map = {row['module']: sorted(row['tests']) for row in (associations or {}).get('modules', [])}
    existing = [path for path in paths if path not in deleted]
    for relative, path in checked_paths(root, existing):
        language = language_of(path)
        if language is None:
            continue
        exclusion, exclusion_error = _structural_exclusion(root, relative, existing, test_files)
        if exclusion:
            excluded.append(exclusion)
            continue
        if exclusion_error:
            errors.append(exclusion_error)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        role, owners, error = _role(relative, language, projects, test_files)
        text = path.read_text(encoding='utf-8-sig', errors='replace')
        executable = bool(text.strip()) and not (language == 'cs' and not any(
            marker in text for marker in ('=>', '{ get', ' return ', ' if', ' for', ' while', ' throw ', 'Console.', 'await ')))
        sources.append({'path': relative, 'sha256': digest, 'language': language, 'role': role,
                        'projects': [owner['path'] for owner in owners], 'executable': executable,
                        'testFiles': association_map.get(relative, [])})
        if error:
            errors.append(error)
    files = build_file_rows(root, paths, deleted)
    return {'schema': 1, 'candidate': {'sourceFingerprint': source_fingerprint(files)},
            'files': files, 'sources': sorted(sources, key=lambda row: row['path']),
            'excluded': excluded,
            'projects': [{'path': row['path'], 'compile': row['compile'],
                          'testProject': row['test'], 'assembly': row.get('assembly')} for row in projects],
            'errors': errors}


def dependency_cycles(edges):
    """Strongly connected components; no arbitrary DFS depth or traversal omission."""
    graph = {}
    for edge in edges:
        if not isinstance(edge, (list, tuple)) or len(edge) != 2 or not all(isinstance(item, str) and item for item in edge):
            raise ValueError('Invalid dependency edge')
        source, target = edge
        graph.setdefault(source, set()).add(target)
        graph.setdefault(target, set())
    pending, visited, order = set(graph), set(), []
    while pending:
        stack = [(min(pending), False)]
        while stack:
            node, expanded = stack.pop()
            if expanded:
                order.append(node)
            elif node not in visited:
                visited.add(node)
                pending.discard(node)
                stack.append((node, True))
                stack.extend((child, False) for child in sorted(graph[node], reverse=True) if child not in visited)
    reverse = {node: set() for node in graph}
    for source, targets in graph.items():
        for target in targets:
            reverse[target].add(source)
    remaining, cycles = set(graph), []
    for node in reversed(order):
        if node not in remaining:
            continue
        component, stack = set(), [node]
        while stack:
            current = stack.pop()
            if current in remaining:
                remaining.remove(current)
                component.add(current)
                stack.extend(reverse[current])
        if len(component) > 1 or node in graph[node]:
            cycles.append(sorted(component))
    return sorted(cycles)

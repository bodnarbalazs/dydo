"""Whole source inventory, explicit uncertain joins, and semantic graph cycles."""
import hashlib

from inventory import checked_paths, fingerprint, language_of


def test_project_role(value, packages):
    value = value.lower()
    if value not in ('', 'false', 'true'):
        raise ValueError('Unknown evaluated IsTestProject value')
    if 'microsoft.net.test.sdk' in {name.lower() for name in packages} and value != 'true':
        raise ValueError('Declared test SDK lacks its restored evaluated test-project role')
    return value == 'true'


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


def assemble_inventory(root, paths, projects, discovery):
    test_files = {row['file'] for row in discovery}
    if not test_files <= set(paths):
        raise ValueError('Missing discovered test source in Git inventory')
    identities = [row['id'] for row in discovery]
    if len(identities) != len(set(identities)):
        raise ValueError('Ambiguous discovered test case identity')
    sources, excluded, errors = [], [], []
    for relative, path in checked_paths(root, paths):
        language = language_of(path)
        if language is None:
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        role, owners, error = _role(relative, language, projects, test_files)
        sources.append({'path': relative, 'language': language, 'role': role, 'sha256': digest,
                        'projects': [owner['path'] for owner in owners]})
        if error:
            errors.append(error)
    return {'sources': sources, 'excluded': excluded, 'errors': errors,
            'all_files': sorted(paths), 'source_fingerprint': fingerprint(root, paths),
            'projects': projects, 'discovery': discovery}


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

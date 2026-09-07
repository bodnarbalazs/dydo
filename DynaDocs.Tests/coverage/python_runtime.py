"""CPython 3.12 code-object witnesses prevent definition-line coverage borrowing."""
import ast
import hashlib
import inspect
import sys
import types

from positions import utf16_column


def _code_objects(code):
    yield code
    for constant in code.co_consts:
        if isinstance(constant, types.CodeType):
            yield from _code_objects(constant)


def _positions(code):
    return tuple(sorted({point for point in code.co_positions()
                         if None not in point and point[2:] != (0, 0)}))


def _key(code):
    return code.co_qualname, code.co_firstlineno, _positions(code)


def _body_contains(node, point):
    line, end_line, column, end_column = point
    return ((node.lineno, node.col_offset) <= (line, column)
            and (end_line, end_column) <= (node.end_lineno, node.end_col_offset))


def _callable_inventory(source, path):
    tree = ast.parse(source, path)
    nodes = [node for node in ast.walk(tree) if isinstance(node, (ast.Lambda, ast.FunctionDef, ast.AsyncFunctionDef))]
    rows = []
    lines = source.splitlines()
    for code in _code_objects(compile(tree, path, 'exec')):
        if not code.co_flags & inspect.CO_NEWLOCALS or code.co_name in ('<listcomp>', '<setcomp>', '<dictcomp>', '<genexpr>'):
            continue
        points = _positions(code)
        matches = [node for node in nodes if getattr(node, 'name', '<lambda>') == code.co_name
                   and points and all(_body_contains(_body(node), point) for point in points)]
        matches = [node for node in matches if not any(
            other is not node and _body_contains(_body(node), (
                _body(other).lineno, _body(other).end_lineno, _body(other).col_offset, _body(other).end_col_offset))
            for other in matches)]
        if len(matches) != 1:
            raise ValueError(f'Missing or ambiguous callable body/code-object join: {path}:{code.co_firstlineno}')
        node = matches[0]
        column = utf16_column(lines[node.lineno - 1], node.col_offset, 'utf8')
        end_column = utf16_column(lines[node.end_lineno - 1], node.end_col_offset, 'utf8')
        body_lines = {str(start): 0 for start, _, _, _ in points}
        rows.append({'id': f'{code.co_qualname}:{node.lineno}:{column}', 'path': path,
                     'line': node.lineno, 'column': column, 'end_line': node.end_lineno,
                     'end_column': end_column, 'body_lines': body_lines,
                     'kind': type(node).__name__, 'execution_count': 0, 'branches': [],
                     'physical_branches': [], '_key': _key(code)})
    if len(rows) != len(nodes) or len({row['id'] for row in rows}) != len(rows):
        raise ValueError(f'Incomplete or duplicate compiled callable inventory: {path}')
    return sorted(rows, key=lambda row: (row['line'], row['column']))


def _body(node):
    return node.body if isinstance(node, ast.Lambda) else node


def _contains_code(row, code, lines):
    points = _positions(code)
    return code.co_qualname.startswith(row['_key'][0] + '.') and bool(points) and all(
        (row['line'], row['column']) <= (start, utf16_column(lines[start - 1], column, 'utf8'))
        and (end, utf16_column(lines[end - 1], end_column, 'utf8')) <= (row['end_line'], row['end_column'])
        for start, end, column, end_column in points)


def _runtime_inventory(source, path):
    rows = _callable_inventory(source, path)
    lines = source.splitlines() or ['']
    module_code = compile(source, path, 'exec')
    module = {'id': '<module>:1:0', 'path': path, 'kind': 'module', 'line': 1, 'column': 0,
              'end_line': len(lines), 'end_column': utf16_column(lines[-1], len(lines[-1]), 'unicode'),
              'body_lines': {}, 'execution_count': 0, 'branches': [], 'physical_branches': [],
              '_key': _key(module_code)}
    index = {row['_key']: row for row in rows}
    for code in _code_objects(module_code):
        if _key(code) in index:
            continue
        owner = _physical_owner(rows, code, lines, module)
        index[_key(code)] = owner
        owner['body_lines'].update({str(start): 0 for start, _, _, _ in _positions(code)})
        if code is not module_code and not code.co_flags & inspect.CO_NEWLOCALS:
            owner['body_lines'][str(code.co_firstlineno)] = 0
    return rows, module, index


def _physical_owner(rows, code, lines, module):
    candidates = [row for row in rows if _contains_code(row, code, lines)]
    candidates = [row for row in candidates if not any(
        other is not row and (row['line'], row['column']) <= (other['line'], other['column'])
        and (other['end_line'], other['end_column']) <= (row['end_line'], row['end_column'])
        for other in candidates)]
    if len(candidates) > 1:
        raise ValueError(f'Ambiguous physical Python code owner: {module["path"]}:{code.co_qualname}')
    return candidates[0] if candidates else module


def callable_inventory(source, path):
    return _runtime_inventory(source, path)[0]


def module_inventory(source, path):
    return _runtime_inventory(source, path)[1]


class CallableWitness:
    def __init__(self, sources):
        if sys.implementation.name != 'cpython' or sys.version_info[:2] != (3, 12):
            raise ValueError('Callable measurement requires pinned CPython 3.12 monitoring semantics')
        self.tool_id = None
        self._rows, self._modules, self._index = [], [], {}
        for path, source in sources.items():
            rows, module, index = _runtime_inventory(source, path)
            self._rows.extend(rows)
            self._modules.append(module)
            self._index.update({(path, key): row for key, row in index.items()})
        self._paths = set(sources)
        self._errors = []

    def _row(self, code):
        if code.co_filename not in self._paths:
            return None
        row = self._index.get((code.co_filename, _key(code)))
        if row is None:
            self._errors.append(f'Unknown runtime callable: {code.co_filename}:{code.co_firstlineno}')
        return row

    def _start(self, code, _offset):
        row = self._row(code)
        if row is not None and row['_key'] == _key(code):
            row['execution_count'] += 1

    def _line(self, code, line):
        row = self._row(code)
        if row is not None:
            identity = str(line)
            if identity not in row['body_lines']:
                self._errors.append(f'Unexpected callable body line: {row["id"]}:{line}')
            else:
                row['body_lines'][identity] += 1

    def _branch(self, code, origin, destination):
        row = self._row(code)
        if row is not None:
            if row['_key'] == _key(code):
                edge = [origin, destination]
                if edge not in row['branches']:
                    row['branches'].append(edge)
            else:
                edge = {'code': hashlib.sha256(repr(_key(code)).encode('utf-8')).hexdigest(),
                        'origin': origin, 'destination': destination}
                if edge not in row['physical_branches']:
                    row['physical_branches'].append(edge)

    def __enter__(self):
        monitor = sys.monitoring
        for tool_id in (3, 4):
            if monitor.get_tool(tool_id) is None:
                self.tool_id = tool_id
                break
        if self.tool_id is None:
            raise ValueError('No unused monitoring ID available for callable measurement')
        monitor.use_tool_id(self.tool_id, 'dydo-callable-witness')
        try:
            monitor.register_callback(self.tool_id, monitor.events.PY_START, self._start)
            monitor.register_callback(self.tool_id, monitor.events.LINE, self._line)
            monitor.register_callback(self.tool_id, monitor.events.BRANCH, self._branch)
            monitor.set_events(self.tool_id, monitor.events.PY_START | monitor.events.LINE | monitor.events.BRANCH)
        except BaseException:
            self.__exit__(None, None, None)
            raise
        return self

    def __exit__(self, _kind, _value, _traceback):
        monitor = sys.monitoring
        failure = None
        operations = [(monitor.set_events, (self.tool_id, 0)),
                      *((monitor.register_callback, (self.tool_id, event, None))
                        for event in (monitor.events.PY_START, monitor.events.LINE, monitor.events.BRANCH)),
                      (monitor.free_tool_id, (self.tool_id,))]
        for operation, arguments in operations:
            try:
                operation(*arguments)
            except BaseException as error:
                if failure is None:
                    failure = error
        if failure is not None:
            raise failure

    def rows(self):
        return self._public_rows(self._rows)

    def module_rows(self):
        return self._public_rows(self._modules)

    def _public_rows(self, rows):
        if self._errors:
            raise ValueError('; '.join(sorted(set(self._errors))))
        return [{key: value for key, value in row.items() if not key.startswith('_')} for row in rows]


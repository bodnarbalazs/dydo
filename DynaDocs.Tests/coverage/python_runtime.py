"""CPython 3.14 code-object witnesses prevent definition-line coverage borrowing."""
import ast
import dis
import hashlib
import inspect
from itertools import chain
from operator import itemgetter
import pickle
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


def _digest(source):
    return hashlib.sha256(source.encode('utf-8')).hexdigest()


_CACHE_SCHEMA = 'dydo-python-inventory/2'
_SEAL_SIZE = hashlib.sha256().digest_size
_START = itemgetter(0)
_ROW_FIELDS = frozenset(('id', 'path', 'kind', 'line', 'column', 'end_line', 'end_column', 'body_lines',
                         'execution_count', 'branches', 'physical_branches', '_key'))


def _compile_state():
    """Every interpreter setting that changes compile() output or co_positions(): the build,
    the -O level, and debug ranges (-X no_debug_ranges / PYTHONNODEBUGRANGES drop every column)."""
    debug_ranges = None not in next(compile('0', '<compile-state>', 'eval').co_positions())
    return sys.version, sys.flags.optimize, debug_ranges


def _seal(body):
    """The cache bytes: the pickled body behind its SHA-256, so damaged bytes that still unpickle are a miss."""
    return hashlib.sha256(body).digest() + body


def _unsealed(cache):
    """The payload of cache bytes whose body still matches its seal, else None."""
    try:
        seal, body = cache[:_SEAL_SIZE], cache[_SEAL_SIZE:]
        return pickle.loads(body) if hashlib.sha256(body).digest() == seal else None
    except Exception:  # noqa: BLE001 - absent, corrupt or truncated bytes are a miss, never a raise
        return None


def _exact_form(inventory):
    """The inventory as one type-exact text: its rows, its module, and each index key's owner by position."""
    rows, module, index = inventory
    owners = {id(owner): position for position, owner in enumerate([*rows, module])}
    return repr((rows, module, [(key, owners[id(owner)]) for key, owner in index.items()]))


def inventory_cache(sources):
    """Every source's runtime inventory, valid only for this compile state and source text, or None.

    The sealed bytes are read back through a child's own path (seal, unpickle, validator) and given only
    when they come back as exactly the inventory just computed, so no writer or format bug reaches a child.
    A cache deliberately rewritten and resealed is a forgery and out of scope: whoever can write it can
    also rewrite the campaign's sitecustomize.py."""
    entries = {path: (_digest(source), _runtime_inventory(source, path)) for path, source in sources.items()}
    cache = _seal(pickle.dumps({'schema': _CACHE_SCHEMA, 'state': _compile_state(), 'sources': entries}))
    read = _cached_inventories(cache)

    def read_back(path):
        inventory = _cached_inventory(read.get(path), path, sources[path])
        return inventory is not None and _exact_form(inventory) == _exact_form(entries[path][1])
    return cache if read.keys() == entries.keys() and all(map(read_back, sources)) else None


def _cached_inventories(cache):
    """The cache's entries when it was built under this process's compile state; anything else is a miss."""
    payload = _unsealed(cache)
    if (type(payload) is not dict or payload.get('schema') != _CACHE_SCHEMA
            or payload.get('state') != _compile_state() or type(payload.get('sources')) is not dict):
        return {}
    return payload['sources']


def _fresh_row(row, path):
    """A row for this path with every field and every counter at its uncounted start."""
    return (type(row) is dict and row.keys() == _ROW_FIELDS and row['path'] == path
            and [type(row[field]) for field in ('execution_count', 'body_lines', 'branches', 'physical_branches')]
            == [int, dict, list, list]
            and row['execution_count'] == 0 and row['branches'] == [] == row['physical_branches']
            and all(type(hits) is int and hits == 0 for hits in row['body_lines'].values()))


def _well_formed_keys(keys):
    """Every key has exactly _key's shape: (str qualname, int first line, tuple of 4-int positions)."""
    keys = {id(key): key for key in keys}.values()  # a row's key unpickles as the very object its index key is
    if not all(type(key) is tuple and len(key) == 3 and type(key[0]) is str and type(key[1]) is int
               and type(key[2]) is tuple for key in keys):
        return False
    points = list(chain.from_iterable(key[2] for key in keys))
    return (set(map(type, points)) <= {tuple} and set(map(len, points)) <= {4}
            and set(map(type, chain.from_iterable(points))) <= {int})


def _owned_index(owners, index):
    """Every owner is the index value of its own key, and the index points at no other row."""
    identities = {id(row) for row in owners}
    return (all(index.get(row['_key']) is row for row in owners)
            and all(id(row) in identities for row in index.values()))


def _own_ids(rows, module):
    """Every id is the one _runtime_inventory gives its row, and no two rows share one."""
    ids = [module['id'], *(row['id'] for row in rows)]
    return (module['id'] == '<module>:1:0'
            and all(type(row['line']) is int and type(row['column']) is int
                    and row['id'] == f'{row["_key"][0]}:{row["line"]}:{row["column"]}' for row in rows)
            and len(set(ids)) == len(ids))


def _implied_body_lines(owners, index):
    """Every owner's body-line keys are the decimal line starts of every position of the keys mapped to it,
    plus at most the first lines of the nested code objects mapped to it (a class body adds its own)."""
    starts = {id(owner): set() for owner in owners}
    first_lines = {id(owner): set() for owner in owners}
    for key, owner in index.items():
        starts[id(owner)].update(map(_START, key[2]))
        if key != owner['_key']:
            first_lines[id(owner)].add(key[1])
    return all(set(map(str, starts[id(owner)])) <= owner['body_lines'].keys()
               <= set(map(str, starts[id(owner)] | first_lines[id(owner)])) for owner in owners)


def _cached_inventory(entry, path, source):
    """The entry's inventory when it is for exactly this text and internally consistent, else None."""
    if type(entry) is not tuple or len(entry) != 2 or entry[0] != _digest(source):
        return None
    inventory = entry[1]
    if type(inventory) is not tuple or len(inventory) != 3:
        return None
    rows, module, index = inventory
    if type(rows) is not list or type(index) is not dict:
        return None
    owners = [*rows, module]
    consistent = (all(_fresh_row(row, path) for row in owners)
                  and _well_formed_keys([*(row['_key'] for row in owners), *index])
                  and _owned_index(owners, index) and _own_ids(rows, module)
                  and _implied_body_lines(owners, index))
    return inventory if consistent else None


def _instruction_edges(code):
    """CPython 3.14 BRANCH offsets, observed without its exception-unsafe jump callbacks."""
    starts, direct = {}, {}
    previous = None
    for instruction in dis.get_instructions(code):
        offset = instruction.offset
        if instruction.opname.startswith('POP_JUMP_IF_'):
            starts[offset] = {instruction.argval: instruction.argval}
        elif instruction.opname == 'FOR_ITER':
            starts[offset] = {offset + 4: offset + 4, instruction.argval + 2: instruction.argval + 4}
        elif instruction.opname == 'NOT_TAKEN':
            # SEND's target has LINE instrumentation, which makes native BRANCH report the
            # NOT_TAKEN site itself as its origin after a coroutine returns.
            origin = offset if previous.opname == 'END_SEND' else previous.offset
            direct[offset] = (origin, offset + 2)
        elif instruction.opname == 'END_ASYNC_FOR':
            direct[offset] = (offset + 2 - instruction.arg * 2, offset + 2)
        previous = instruction
    relevant = set(starts) | set(direct)
    relevant.update(destination for destinations in starts.values() for destination in destinations)
    return starts, direct, relevant


class CallableWitness:
    def __init__(self, sources, cache=None):
        if sys.implementation.name != 'cpython' or sys.version_info[:2] != (3, 14):
            raise ValueError('Callable measurement requires pinned CPython 3.14 monitoring semantics')
        self.tool_id = None
        self._rows, self._modules, self._index = [], [], {}
        cached = _cached_inventories(cache)
        for path, source in sources.items():
            rows, module, index = (_cached_inventory(cached.get(path), path, source)
                                   or _runtime_inventory(source, path))
            self._rows.extend(rows)
            self._modules.append(module)
            self._index.update({(path, key): row for key, row in index.items()})
        self._paths = set(sources)
        self._errors = []
        self._codes = {}
        self._edges = {}
        self._pending = {}

    def _row(self, code):
        """A target code object's row, whether it is that row's own code, and its key's digest.

        The key scans every position of the code object, so it is resolved once per code object, not
        per event. Code objects compare equal across files and qualnames, so the entry is keyed on
        identity and pins the code object: its id cannot be reused while the entry exists."""
        if code.co_filename not in self._paths:
            return None, False, None
        entry = self._codes.get(id(code))
        if entry is None:
            key = _key(code)
            row = self._index.get((code.co_filename, key))
            entry = self._codes[id(code)] = (code, row, row is not None and row['_key'] == key,
                                             hashlib.sha256(repr(key).encode('utf-8')).hexdigest())
        _, row, own, digest = entry
        if row is None:
            self._errors.append(f'Unknown runtime callable: {code.co_filename}:{code.co_firstlineno}')
        return row, own, digest

    def _start(self, code, _offset):
        row, own, _ = self._row(code)
        if own:
            row['execution_count'] += 1
        self._enable_instructions(code)

    def _enable_instructions(self, code, _offset=0, _error=None):
        if code.co_filename in self._paths and self.tool_id is not None:
            monitor = sys.monitoring
            if not monitor.get_local_events(self.tool_id, code):
                monitor.set_local_events(self.tool_id, code, monitor.events.INSTRUCTION)

    def _enable_running_frames(self):
        # A frame can enter measurement and branch on the same line, with no new LINE event.
        for frame in sys._current_frames().values():
            while frame is not None:
                self._enable_instructions(frame.f_code)
                frame = frame.f_back

    def _line(self, code, line):
        row = self._row(code)[0]
        if row is not None:
            identity = str(line)
            if identity not in row['body_lines']:
                self._errors.append(f'Unexpected callable body line: {row["id"]}:{line}')
            else:
                row['body_lines'][identity] += 1

    def _branch(self, code, origin, destination):
        row, own, digest = self._row(code)
        if row is not None:
            if own:
                edge = [origin, destination]
                if edge not in row['branches']:
                    row['branches'].append(edge)
            else:
                edge = {'code': digest, 'origin': origin, 'destination': destination}
                if edge not in row['physical_branches']:
                    row['physical_branches'].append(edge)

    def _instruction(self, code, offset):
        # CPython's BRANCH callback exceptions unwind at the destination, which can lie outside
        # the source's try. Instruction callbacks unwind at the instruction actually executing.
        if self._row(code)[0] is None:
            return sys.monitoring.DISABLE
        if id(code) not in self._edges:
            self._edges[id(code)] = _instruction_edges(code)
        starts, direct, relevant = self._edges[id(code)]
        if offset not in relevant:
            return sys.monitoring.DISABLE
        frame = sys._getframe(1)
        pending = self._pending.pop(frame, None)
        if pending is not None and offset in pending[1]:
            self._branch(code, pending[0], pending[1][offset])
        if offset in direct:
            self._branch(code, *direct[offset])
        if offset in starts:
            self._pending[frame] = (offset, starts[offset])

    def _exception(self, _code, _offset, _error):
        self._pending.pop(sys._getframe(1), None)

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
            monitor.register_callback(self.tool_id, monitor.events.PY_RESUME, self._enable_instructions)
            monitor.register_callback(self.tool_id, monitor.events.PY_THROW, self._enable_instructions)
            monitor.register_callback(self.tool_id, monitor.events.LINE, self._line)
            monitor.register_callback(self.tool_id, monitor.events.INSTRUCTION, self._instruction)
            monitor.register_callback(self.tool_id, monitor.events.EXCEPTION_HANDLED, self._exception)
            monitor.register_callback(self.tool_id, monitor.events.PY_UNWIND, self._exception)
            self._enable_running_frames()
            monitor.set_events(self.tool_id, monitor.events.PY_START | monitor.events.PY_RESUME | monitor.events.LINE
                               | monitor.events.PY_THROW | monitor.events.EXCEPTION_HANDLED | monitor.events.PY_UNWIND)
        except BaseException:
            self.__exit__(None, None, None)
            raise
        return self

    def __exit__(self, _kind, _value, _traceback):
        monitor = sys.monitoring
        failure = None
        operations = [(monitor.set_events, (self.tool_id, 0)),
                      *((monitor.register_callback, (self.tool_id, event, None))
                        for event in (monitor.events.PY_START, monitor.events.PY_RESUME, monitor.events.PY_THROW,
                                      monitor.events.LINE, monitor.events.INSTRUCTION,
                                      monitor.events.EXCEPTION_HANDLED, monitor.events.PY_UNWIND)),
                      (monitor.free_tool_id, (self.tool_id,))]
        for operation, arguments in operations:
            try:
                operation(*arguments)
            except BaseException as error:
                if failure is None:
                    failure = error
        self._pending.clear()
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

"""Lambda witnesses must not borrow execution from their containing statement."""
import json
import os
import pickle
import subprocess
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import python_runtime
from python_runtime import CallableWitness, callable_inventory, inventory_cache, module_inventory


def _nested_code(code, name):
    return next(value for value in code.co_consts
                if isinstance(value, types.CodeType) and value.co_name == name)


def _inventory(witness):
    owners = {id(row): position for position, row in enumerate(witness._rows + witness._modules)}
    return (witness._rows, witness._modules,
            {key: owners[id(row)] for key, row in witness._index.items()})


def _counted(witness, workload):
    with witness:
        workload()
    return witness.rows() + witness.module_rows()


def _differences(expected, actual):
    return {(row['id'], field) for row, other in zip(expected, actual, strict=True)
            for field in row if row[field] != other[field]}


def _repickled(cache, damage):
    """The cache with its payload damaged, sealed again so that only the damage itself can make it a miss."""
    payload = python_runtime._unsealed(cache)
    damage(payload)
    return python_runtime._seal(pickle.dumps(payload))


def _byte_damaged(cache):
    """The cache with one byte of its sealed pickle changed so that it still loads: a row's end column."""
    payload = python_runtime._unsealed(cache)
    body = pickle.dumps(payload)
    _first_row(payload)['end_column'] += 1
    damaged = pickle.dumps(payload)
    assert len(body) == len(damaged) and sum(a != b for a, b in zip(body, damaged)) == 1
    return python_runtime._seal(body)[:-len(body)] + damaged


def _entry(payload):
    (path, (digest, inventory)), = payload['sources'].items()
    return path, digest, inventory


def _replace_entry(payload, entry):
    path, _, _ = _entry(payload)
    payload['sources'][path] = entry


def _first_row(payload):
    return _entry(payload)[2][0][0]


def _module(payload):
    return _entry(payload)[2][1]


def _index(payload):
    return _entry(payload)[2][2]


def _rekeyed(row, key):
    row['_key'] = key(row['_key'])


# Each damage leaves a pickle that loads; only its structure is wrong for this source.
_WRONG_STRUCTURES = {
    'sources not a mapping': lambda payload: payload.update(sources=list(payload['sources'].items())),
    'entry not a pair': lambda payload: _replace_entry(payload, _entry(payload)[2]),
    'inventory not a triple': lambda payload: _replace_entry(
        payload, (_entry(payload)[1], _entry(payload)[2][:2])),
    'rows not a list': lambda payload: _replace_entry(
        payload, (_entry(payload)[1], (tuple(_entry(payload)[2][0]), *_entry(payload)[2][1:]))),
    'module not a mapping': lambda payload: _replace_entry(
        payload, (_entry(payload)[1], (_entry(payload)[2][0], 'module', _entry(payload)[2][2]))),
    'row field missing': lambda payload: _first_row(payload).pop('branches'),
    'row for another path': lambda payload: _first_row(payload).update(path='elsewhere.py'),
    'execution counter not fresh': lambda payload: _first_row(payload).update(execution_count=5),
    'body line counter not fresh': lambda payload: _first_row(payload)['body_lines'].update(
        dict.fromkeys(_first_row(payload)['body_lines'], 1)),
    'branch already recorded': lambda payload: _first_row(payload).update(branches=[[0, 2]]),
    'physical branch already recorded': lambda payload: _first_row(payload).update(
        physical_branches=[{'code': 'x', 'origin': 0, 'destination': 2}]),
    'index outside the inventory': lambda payload: _entry(payload)[2][2].update(
        {key: dict(row) for key, row in _entry(payload)[2][2].items()}),
    'empty index': lambda payload: _index(payload).clear(),
    'index missing a callable key': lambda payload: _index(payload).pop(_first_row(payload)['_key']),
    'index missing the module key': lambda payload: _index(payload).pop(_module(payload)['_key']),
    'index key at another owner': lambda payload: _index(payload).update(
        {_first_row(payload)['_key']: _module(payload)}),
    'index key malformed': lambda payload: _index(payload).update({('work', '1', ()): _module(payload)}),
    'row key absent': lambda payload: _first_row(payload).update(_key=None),
    'row key a list': lambda payload: _rekeyed(_first_row(payload), list),
    'row key line not an int': lambda payload: _rekeyed(
        _first_row(payload), lambda key: (key[0], str(key[1]), key[2])),
    'row key positions a list': lambda payload: _rekeyed(
        _first_row(payload), lambda key: (*key[:2], list(key[2]))),
    'row key position short': lambda payload: _rekeyed(
        _first_row(payload), lambda key: (*key[:2], tuple(point[:3] for point in key[2]))),
    'module key absent': lambda payload: _module(payload).update(_key=None),
    'row id not a string': lambda payload: _first_row(payload).update(id=[]),
    'row id of another form': lambda payload: _first_row(payload).update(id='work:9:0'),
    'module id of another form': lambda payload: _module(payload).update(id='<module>:2:0'),
    'row listed twice': lambda payload: _entry(payload)[2][0].append(_first_row(payload)),
}


def _relabelled(row, key):
    """The row under another well-formed key and the id that key gives, so the cache stays consistent."""
    row['_key'] = key
    row['id'] = f'{key[0]}:{row["line"]}:{row["column"]}'


def _monitored_as_physical(payload, path):
    """Negative control: a consistent cached row no longer recognises its own code object as its own."""
    rows, _, index = payload['sources'][path][1]
    row = next(row for row in rows if row['id'].startswith('_callable_inventory:'))
    _relabelled(row, ('elsewhere', *row['_key'][1:]))
    index[row['_key']] = row


def _physical_as_monitored(payload, path):
    """Negative control: a consistent cached row takes its nested generator's code object for its own."""
    rows, _, index = payload['sources'][path][1]
    row = next(row for row in rows if row['id'].startswith('_physical_owner:'))
    _relabelled(row, next(key for key, owner in index.items() if owner is row and key != row['_key']))


_COMPILE_STATE_CHILD = '''\
import json, sys
from pathlib import Path
sys.path.insert(0, sys.argv[1])
from python_runtime import CallableWitness
path, cache = sys.argv[2], Path(sys.argv[3]).read_bytes()
source = Path(path).read_text(encoding='utf-8')


def inventory(cache):
    try:
        witness = CallableWitness({path: source}, cache)
    except ValueError as error:
        return str(error)
    return witness._rows + witness._modules


print(json.dumps({'cached': inventory(cache), 'fresh': inventory(None)}))
'''


class PythonRuntimeTests(unittest.TestCase):
    def test_callback_release_failure_does_not_keep_monitor_id_owned(self):
        from unittest.mock import patch
        path = str(Path('release-subject.py').resolve())
        witness = CallableWitness({path: 'value = 1'})
        witness.__enter__()
        monitor = sys.monitoring
        original = monitor.register_callback
        failed = False

        def release(tool_id, event, callback):
            nonlocal failed
            original(tool_id, event, callback)
            if callback is None and not failed:
                failed = True
                raise RuntimeError('native callback release refused')

        try:
            with patch.object(monitor, 'register_callback', side_effect=release):
                with self.assertRaisesRegex(RuntimeError, 'release refused'):
                    witness.__exit__(None, None, None)
            self.assertIsNone(monitor.get_tool(witness.tool_id))
        finally:
            if monitor.get_tool(witness.tool_id) is not None:
                monitor.set_events(witness.tool_id, 0)
                monitor.free_tool_id(witness.tool_id)

    def test_nested_class_code_belongs_to_enclosing_function_not_its_only_method(self):
        source = 'def factory():\n    class C:\n        def uncalled(self):\n            return 1\n    return C\nfactory()\n'
        path = str(Path('class-owner.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        outer, inner = witness.rows()
        self.assertEqual(1, outer['execution_count'])
        self.assertGreater(outer['body_lines']['2'], 0)
        self.assertEqual(0, inner['execution_count'])
        self.assertTrue(all(value == 0 for value in inner['body_lines'].values()))

    def test_generator_expression_events_belong_to_enclosing_callable_without_new_entry(self):
        source = 'def work():\n    return (\n        value + 1\n        for value in (1, 2)\n    )\ngenerator = work()\nlist(generator)\n'
        path = str(Path('generator-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        row = witness.rows()[0]
        self.assertEqual(1, row['execution_count'])
        self.assertGreater(row['body_lines']['3'], 0)
        self.assertTrue(row['physical_branches'])

    def test_module_and_class_execution_have_their_own_exact_witness(self):
        source = 'class C:\n    if True:\n        value = 1\n    def uncalled(self): return 2\ngenerator = (x for x in (1, 2))\nlist(generator)\n'
        path = str(Path('module-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        self.assertEqual(0, witness.rows()[0]['execution_count'])
        row = witness.module_rows()[0]
        self.assertEqual(1, row['execution_count'])
        self.assertGreater(row['body_lines']['3'], 0)
        self.assertGreater(row['body_lines']['5'], 0)

    def test_uncalled_one_line_named_async_and_generator_bodies_do_not_borrow_definitions(self):
        source = 'def uncalled(): return 1\nasync def asynchronous(): return 2\ndef generator(): yield 3\ndef called(): return 4\ncalled()\n'
        path = str(Path('named-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        rows = witness.rows()
        self.assertEqual([0, 0, 0, 1], [row['execution_count'] for row in rows])
        self.assertEqual([0, 0, 0, 1], [sum(row['body_lines'].values()) for row in rows])

    def test_decorators_and_redefined_nested_names_keep_distinct_code_identity(self):
        source = '''def decorator(fn):
    def wrapper(): return fn()
    return wrapper
@decorator
def decorated(): return 1
def outer():
    def inner(): return 2
    earlier = inner
    def inner(): return 3
    return earlier()
decorated()
outer()
'''
        path = str(Path('named-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        rows = witness.rows()
        self.assertEqual(6, len(rows))
        self.assertEqual([1, 1, 1, 1, 1, 0], [row['execution_count'] for row in rows])
        self.assertEqual(6, len({row['id'] for row in rows}))

    def test_maintained_monitor_callbacks_are_measurable_as_direct_test_subjects(self):
        runtime = Path(__file__).resolve().parents[1] / 'python_runtime.py'
        source = runtime.read_text(encoding='utf-8')
        subject_path = str(Path('callback-subject.py').resolve())
        subject_source = 'f = lambda: 1\n'
        subject = CallableWitness({subject_path: subject_source})
        code = next(value for value in compile(subject_source, subject_path, 'exec').co_consts if hasattr(value, 'co_code'))
        with CallableWitness({str(runtime): source}) as observing:
            subject._start(code, 0)
            subject._line(code, 1)
            subject._branch(code, 0, 2)
        rows = observing.rows()
        callbacks = [row for row in rows if row['id'].split(':')[0] in ('CallableWitness._start', 'CallableWitness._line', 'CallableWitness._branch')]
        self.assertEqual(3, len(callbacks))
        self.assertTrue(all(row['execution_count'] == 1 and any(row['body_lines'].values()) for row in callbacks))
        self.assertEqual(1, subject.rows()[0]['execution_count'])

    def test_adjacent_lambdas_have_separate_executed_and_uncalled_denominators(self):
        source = 'left, right = lambda x: 1 if x else 2, lambda x: 3 if x else 4\nleft(True)\n'
        path = str(Path('lambda-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        rows = witness.rows()
        self.assertEqual(2, len(rows))
        self.assertEqual([1, 0], [row['execution_count'] for row in rows])
        self.assertEqual([{'1': 1}, {'1': 0}], [row['body_lines'] for row in rows])
        self.assertTrue(rows[0]['branches'])
        self.assertEqual([], rows[1]['branches'])

    def test_nested_multiline_unicode_lambdas_join_exactly(self):
        source = 'é = "😀"; first = lambda: (lambda x:\n    1\n    if x\n    else 2)\nfirst()(False)\n'
        path = str(Path('lambda-subject.py').resolve())
        with CallableWitness({path: source}) as witness:
            exec(compile(source, path, 'exec'), {})
        rows = witness.rows()
        self.assertEqual([1, 1], [row['execution_count'] for row in rows])
        self.assertEqual(2, len({row['id'] for row in rows}))
        self.assertGreater(rows[1]['body_lines']['4'], 0)

    def test_true_false_and_both_calls_preserve_distinct_raw_branch_evidence(self):
        path = str(Path('lambda-subject.py').resolve())
        outcomes = []
        for calls in ('f(True)', 'f(False)', 'f(True); f(False)'):
            source = 'f = lambda x: 1 if x else 2\n' + calls
            with CallableWitness({path: source}) as witness:
                exec(compile(source, path, 'exec'), {})
            outcomes.append({tuple(edge) for edge in witness.rows()[0]['branches']})
        self.assertNotEqual(outcomes[0], outcomes[1])
        self.assertEqual(outcomes[0] | outcomes[1], outcomes[2])

    def test_cleanup_releases_callbacks_even_when_subject_raises(self):
        path = str(Path('lambda-subject.py').resolve())
        with self.assertRaises(RuntimeError):
            with CallableWitness({path: 'f = lambda: 1'}) as witness:
                tool_id = witness.tool_id
                raise RuntimeError('subject failed')
        self.assertIsNone(sys.monitoring.get_tool(tool_id))
        self.assertEqual(0, sys.monitoring.get_events(tool_id))

    def test_occupied_ids_fail_without_disturbing_another_monitor(self):
        monitor = sys.monitoring
        existing = {tool_id: monitor.get_tool(tool_id) for tool_id in (3, 4)}
        for tool_id, owner in existing.items():
            if owner is None:
                monitor.use_tool_id(tool_id, 'fixture-owner')
        try:
            with self.assertRaises(ValueError):
                with CallableWitness({}):
                    self.fail('occupied IDs must fail before execution')
            for tool_id, owner in existing.items():
                self.assertEqual(owner or 'fixture-owner', monitor.get_tool(tool_id))
        finally:
            for tool_id, owner in existing.items():
                if owner is None:
                    monitor.free_tool_id(tool_id)

    def test_source_shapes_without_an_exact_compiled_join_fail_closed(self):
        cases = [('def identity[T](value: T) -> T:\n    return value\n', 'Missing or ambiguous'),
                 ('if False:\n    def unreachable():\n        return 1\n', 'Incomplete or duplicate')]
        for source, message in cases:
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                callable_inventory(source, 'join-subject.py')

    def test_measurement_refuses_to_start_on_an_unpinned_interpreter(self):
        from unittest.mock import patch
        with patch.object(sys, 'version_info', (3, 13, 0, 'final', 0)):
            with self.assertRaisesRegex(ValueError, 'pinned CPython 3.14'):
                CallableWitness({})

    def test_foreign_unknown_and_unexpected_events_are_refused_not_counted(self):
        source = 'def work():\n    return 1\n'
        path = str(Path('refusal-subject.py').resolve())
        witness = CallableWitness({path: source})
        work = _nested_code(compile(source, path, 'exec'), 'work')

        witness._start(_nested_code(compile(source, 'foreign.py', 'exec'), 'work'), 0)
        self.assertEqual([0], [row['execution_count'] for row in witness.rows()])

        witness._line(work, 999)
        edited = compile('def work():\n    return 1 + 1\n', path, 'exec')
        witness._start(_nested_code(edited, 'work'), 0)
        with self.assertRaises(ValueError) as refusal:
            witness.rows()
        self.assertIn('Unexpected callable body line', str(refusal.exception))
        self.assertIn('Unknown runtime callable', str(refusal.exception))

    def test_generator_branch_events_are_deduplicated_against_the_physical_owner(self):
        source = 'def work():\n    return (value for value in (1, 2))\n'
        path = str(Path('physical-subject.py').resolve())
        witness = CallableWitness({path: source})
        generator = _nested_code(_nested_code(compile(source, path, 'exec'), 'work'), '<genexpr>')

        witness._branch(generator, 0, 4)
        witness._branch(generator, 0, 4)
        witness._branch(generator, 4, 8)

        row = witness.rows()[0]
        self.assertEqual([], row['branches'])
        self.assertEqual([(0, 4), (4, 8)],
                         [(edge['origin'], edge['destination']) for edge in row['physical_branches']])
        self.assertEqual(1, len({edge['code'] for edge in row['physical_branches']}))

    def test_failed_callback_registration_releases_the_monitor_id(self):
        from unittest.mock import patch
        witness = CallableWitness({str(Path('enter-subject.py').resolve()): 'f = lambda: 1'})
        original = sys.monitoring.register_callback
        refusals = []

        def refuse(tool_id, event, callback):
            refusals.append(event)
            if len(refusals) == 1:
                raise RuntimeError('native registration refused')
            return original(tool_id, event, callback)

        with patch.object(sys.monitoring, 'register_callback', side_effect=refuse):
            with self.assertRaisesRegex(RuntimeError, 'registration refused'):
                witness.__enter__()
        self.assertIsNone(sys.monitoring.get_tool(witness.tool_id))
        self.assertEqual(0, sys.monitoring.get_events(witness.tool_id))

    def test_module_row_spans_the_whole_file_without_owning_callable_bodies(self):
        row = module_inventory('value = 1\nf = lambda: 2\n', 'module-subject.py')
        self.assertEqual(('<module>:1:0', 'module', 1, 0, 2, 13),
                         (row['id'], row['kind'], row['line'], row['column'],
                          row['end_line'], row['end_column']))
        self.assertEqual({'1': 0, '2': 0}, row['body_lines'])

    def test_all_compiled_lambdas_are_inventory_even_before_execution(self):
        rows = callable_inventory('f = lambda: 1\ng = lambda: 2', 'subject.py')
        self.assertEqual(2, len(rows))
        self.assertTrue(all(row['body_lines'] == {'1' if index == 0 else '2': 0}
                            for index, row in enumerate(rows)))

    def test_cached_inventory_equals_the_fresh_inventory_for_every_tracked_source(self):
        root = Path(__file__).resolve().parents[3]
        listing = subprocess.run(['git', 'ls-files', '-z', '--', '*.py'], cwd=root,
                                 capture_output=True, text=True, check=True).stdout.split('\0')
        sources = {str(root / path): (root / path).read_text(encoding='utf-8-sig')
                   for path in listing if path}
        self.assertIn(str(root / 'DynaDocs.Tests/coverage/python_runtime.py'), sources)
        cache = inventory_cache(sources)
        with patch.object(python_runtime, '_runtime_inventory') as computed:
            cached = CallableWitness(sources, cache)
        computed.assert_not_called()
        self.assertEqual(_inventory(CallableWitness(sources)), _inventory(cached))

    def test_a_stale_or_foreign_cache_entry_is_recomputed_never_counted(self):
        path = str(Path('cache-subject.py').resolve())
        original, edited = 'def work():\n    return 1\n', 'def work():\n    return 1 + 1\n'
        cache = inventory_cache({path: original})
        with patch.object(sys, 'version', 'another 3.14 build'):
            foreign = inventory_cache({path: original})
        fresh = python_runtime._runtime_inventory
        with patch.object(python_runtime, '_runtime_inventory', side_effect=fresh) as computed:
            CallableWitness({path: original}, cache)
            stale = CallableWitness({path: edited}, cache)
            moved = CallableWitness({path: original}, foreign)
        self.assertEqual([edited, original], [call.args[0] for call in computed.call_args_list])
        self.assertEqual(_inventory(CallableWitness({path: edited})), _inventory(stale))
        self.assertEqual(_inventory(CallableWitness({path: original})), _inventory(moved))
        with stale:
            namespace = {}
            exec(compile(edited, path, 'exec'), namespace)
            namespace['work']()
        self.assertEqual([1], [row['execution_count'] for row in stale.rows()])

    def test_a_child_under_another_compile_state_counts_from_its_own_inventory(self):
        source = ('def only(value=True):\n    """A docstring."""\n    assert value\n'
                  '    return list(item for item in (1, 2) if item)\nonly()\n')
        environment = {key: value for key, value in os.environ.items()
                       if key not in ('PYTHONOPTIMIZE', 'PYTHONNODEBUGRANGES')}
        with tempfile.TemporaryDirectory() as folder:
            path, cache, child = (Path(folder) / name for name in ('subject.py', 'inventory.pickle', 'child.py'))
            path.write_text(source, encoding='utf-8')
            cache.write_bytes(inventory_cache({str(path): source}))
            child.write_text(_COMPILE_STATE_CHILD, encoding='utf-8')
            parent = json.loads(json.dumps(_inventory(CallableWitness({str(path): source}))[:2]))
            parent = parent[0] + parent[1]
            for flags, variables in (([], {}), (['-O'], {}), (['-OO'], {}), (['-X', 'no_debug_ranges'], {}),
                                     ([], {'PYTHONOPTIMIZE': '1'}), ([], {'PYTHONNODEBUGRANGES': '1'})):
                with self.subTest(flags=flags, variables=variables):
                    result = subprocess.run(
                        [sys.executable, '-S', *flags, str(child), str(Path(python_runtime.__file__).parent),
                         str(path), str(cache)],
                        env={**environment, **variables}, capture_output=True, text=True, check=True)
                    observed = json.loads(result.stdout)
                    self.assertEqual(observed['fresh'], observed['cached'])
                    self.assertEqual(not flags and not variables, observed['fresh'] == parent)

    def test_an_unusable_cache_is_a_miss_counted_from_a_fresh_inventory(self):
        path = str(Path('unusable-cache-subject.py').resolve())
        source = ('def work(flag):\n    if flag:\n        return list(x for x in (1, 2) if x)\n'
                  '    return []\nwork(True)\nwork(False)\n')
        cache = inventory_cache({path: source})
        seal = python_runtime._seal
        damaged = {
            'empty': b'', 'corrupt': b'not a pickle', 'truncated': cache[:-10],
            'byte damage that still unpickles': _byte_damaged(cache),
            'not a mapping': seal(pickle.dumps(['sources'])),
            'foreign schema': seal(pickle.dumps({'schema': 'foreign'})),
            **{name: _repickled(cache, damage) for name, damage in _WRONG_STRUCTURES.items()},
        }

        def workload():
            exec(compile(source, path, 'exec'), {})

        expected = _counted(CallableWitness({path: source}), workload)
        self.assertTrue(all(expected[0][field] for field in ('execution_count', 'branches', 'physical_branches')))
        fresh = python_runtime._runtime_inventory
        for name, cache_bytes in damaged.items():
            with self.subTest(cache=name):
                with patch.object(python_runtime, '_runtime_inventory', side_effect=fresh) as computed:
                    witness = CallableWitness({path: source}, cache_bytes)
                self.assertEqual([source], [call.args[0] for call in computed.call_args_list])
                self.assertEqual(_inventory(CallableWitness({path: source})), _inventory(witness))
                self.assertEqual(expected, _counted(witness, workload))

    def test_a_cached_inventory_counts_a_real_workload_exactly_as_a_fresh_one(self):
        runtime = python_runtime.__file__
        sources = {path: Path(path).read_text(encoding='utf-8-sig')
                   for path in (runtime, sys.modules['positions'].__file__)}
        targets = [Path(sys.modules['positions'].__file__)]

        def workload():
            for target in targets:
                text = target.read_text(encoding='utf-8-sig')
                callable_inventory(text, str(target))
                module_inventory(text, str(target))

        cache = inventory_cache(sources)
        fresh = _counted(CallableWitness(sources), workload)
        with patch.object(python_runtime, '_runtime_inventory') as computed:
            witness = CallableWitness(sources, cache)
        computed.assert_not_called()
        self.assertEqual(set(), _differences(fresh, _counted(witness, workload)))
        self.assertTrue(any(row['execution_count'] for row in fresh))
        self.assertTrue(any(any(row['body_lines'].values()) for row in fresh))
        self.assertTrue(any(row['branches'] for row in fresh))
        self.assertTrue(any(row['physical_branches'] for row in fresh))
        for field, control in (('branches', _monitored_as_physical), ('physical_branches', _physical_as_monitored)):
            with self.subTest(negative_control=field):
                tampered = _repickled(cache, lambda payload: control(payload, runtime))
                observed = _counted(CallableWitness(sources, tampered), workload)
                self.assertIn(field, {name for _, name in _differences(fresh, observed)})

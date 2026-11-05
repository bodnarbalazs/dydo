"""Lambda witnesses must not borrow execution from their containing statement."""
import sys
import types
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_runtime import CallableWitness, callable_inventory, module_inventory


def _nested_code(code, name):
    return next(value for value in code.co_consts
                if isinstance(value, types.CodeType) and value.co_name == name)


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

    def test_event_cost_does_not_repeat_the_code_objects_position_scan(self):
        from unittest.mock import patch
        import python_runtime
        source = 'def work(flag):\n    if flag:\n        return 1\n    return 2\n'
        path = str(Path('event-cost-subject.py').resolve())
        witness = CallableWitness({path: source})
        work = _nested_code(compile(source, path, 'exec'), 'work')
        with patch.object(python_runtime, '_positions', wraps=python_runtime._positions) as scan:
            for _ in range(50):
                witness._start(work, 0)
                witness._line(work, 2)
                witness._branch(work, 0, 4)
        # only `work`'s scans: under the coverage campaign this module is itself a target, and the
        # campaign's witness scans the callbacks above once each through the same module global
        self.assertEqual(1, sum(call.args[0] is work for call in scan.call_args_list))
        row = witness.rows()[0]
        self.assertEqual((50, 50), (row['execution_count'], row['body_lines']['2']))

    def test_equal_code_objects_from_distinct_sources_keep_their_own_rows(self):
        source = 'def work():\n    return 1\n'
        first, second = str(Path('equal-first.py').resolve()), str(Path('equal-second.py').resolve())
        witness = CallableWitness({first: source, second: source})
        codes = [_nested_code(compile(source, path, 'exec'), 'work') for path in (first, second)]
        self.assertEqual(codes[0], codes[1])
        witness._start(codes[0], 0)
        witness._start(codes[1], 0)
        witness._start(codes[1], 0)
        self.assertEqual([(first, 1), (second, 2)],
                         [(row['path'], row['execution_count']) for row in witness.rows()])

    def test_a_witnessed_code_object_keeps_its_id_from_any_later_code_object(self):
        import weakref
        source = 'def first():\n    return 1\ndef second():\n    return 2\n'
        path = str(Path('reuse-subject.py').resolve())
        witness = CallableWitness({path: source})
        first = _nested_code(compile(source, path, 'exec'), 'first')
        witnessed = weakref.ref(first)
        witness._start(first, 0)
        del first
        # alive, its id cannot be handed to a later code object, which would then borrow its row
        self.assertIsNotNone(witnessed())
        witness._start(_nested_code(compile(source, path, 'exec'), 'second'), 0)
        self.assertEqual([1, 1], [row['execution_count'] for row in witness.rows()])

    def test_remembered_events_report_what_a_fresh_resolution_of_every_event_reports(self):
        source = '''import itertools
class Shape:
    sides = [n for n in range(3)]
    def area(self, scale):
        total = 0
        for side in self.sides:
            if side % 2:
                total += side * scale
            else:
                total -= 1
        return total
def make(count):
    pick = lambda value: value if value > 1 else -value
    return sum(pick(item) for item in range(count)), [Shape().area(n) for n in range(count)]
async def unused():
    return None
for count in itertools.chain(range(4), (7,)):
    make(count)
'''
        path = str(Path('equivalence-subject.py').resolve())

        class Forgetful(dict):
            def get(self, _key, default=None):
                return default

        reports = []
        for remembered in (True, False):
            witness = CallableWitness({path: source})
            if not remembered:
                witness._codes = Forgetful()
            with witness:
                exec(compile(source, path, 'exec'), {})
                unknown = compile('def stray():\n    return 9\n', path, 'exec')
                witness._start(unknown, 0)
            with self.assertRaisesRegex(ValueError, 'Unknown runtime callable') as refusal:
                witness.rows()
            witness._errors.clear()
            reports.append((witness.rows(), witness.module_rows(), str(refusal.exception)))
        self.assertEqual(reports[1], reports[0])
        self.assertTrue(any(row['physical_branches'] for row in reports[0][0]))
        self.assertTrue(all(row['execution_count'] for row in reports[0][0][:3]))

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

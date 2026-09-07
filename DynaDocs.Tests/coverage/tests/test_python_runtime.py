"""Lambda witnesses must not borrow execution from their containing statement."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_runtime import CallableWitness, callable_inventory


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

    def test_all_compiled_lambdas_are_inventory_even_before_execution(self):
        rows = callable_inventory('f = lambda: 1\ng = lambda: 2', 'subject.py')
        self.assertEqual(2, len(rows))
        self.assertTrue(all(row['body_lines'] == {'1' if index == 0 else '2': 0}
                            for index, row in enumerate(rows)))


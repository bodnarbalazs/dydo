"""Real signals inside the witness must reach the measured frame's exception handler."""
import dis
import signal
import subprocess
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_runtime import CallableWitness


SOURCE = '''def work():
    try:
        value = 0
        while value < 0:
            value += 1
    except (KeyboardInterrupt, RuntimeError):
        return 'ADAPTER_INTERRUPTED'
    return 'MISSED'
'''


ACCOUNTING = '''
def recursive(value):
    return recursive(value - 1) if value else 0

def branches(value):
    total = 0
    while total < 2:
        total += 1
    for item in range(3):
        if item == value:
            break
        if item is None:
            continue
    return (value and total) or (total if value is not None else 3)

def generator():
    yield from (item for item in range(3) if item % 2)

class Broken:
    def __iter__(self):
        return self
    def __next__(self):
        raise ValueError('iterator failed')

class Finite:
    def __init__(self):
        self.value = 0
    def __iter__(self):
        return self
    def __next__(self):
        self.value += 1
        if self.value > 2:
            raise StopIteration
        return self.value
    def __aiter__(self):
        return self
    async def __anext__(self):
        self.value += 1
        if self.value > 2:
            raise StopAsyncIteration
        return self.value

def handled():
    try:
        for item in Broken():
            return item
    except ValueError:
        return 'caught'

def unwound():
    for item in Broken():
        return item

async def asynchronous_generator():
    for item in range(3):
        yield item

async def asynchronous():
    return [item async for item in asynchronous_generator() if item]

async def explicit_async_exhaustion():
    async for item in Finite():
        pass

class Container:
    values = [item for item in range(4) if item]

recursive(3)
for value in (None, 0, 1, 3):
    branches(value)
first, second = generator(), generator()
list(zip(first, second))
handled()
list(Finite())
try:
    unwound()
except ValueError:
    pass
try:
    asynchronous().send(None)
except StopIteration:
    pass
try:
    explicit_async_exhaustion().send(None)
except StopIteration:
    pass
'''


def measured(source, native):
    path = 'accounting-subject.py'
    with CallableWitness({path: source}) as witness:
        monitor = sys.monitoring
        if native:
            def start(code, _offset):
                row, own, _ = witness._row(code)
                if own:
                    row['execution_count'] += 1
            monitor.register_callback(witness.tool_id, monitor.events.PY_START, start)
            monitor.register_callback(witness.tool_id, monitor.events.BRANCH, witness._branch)
            monitor.set_events(witness.tool_id, monitor.events.PY_START | monitor.events.LINE | monitor.events.BRANCH)
        try:
            exec(compile(source, path, 'exec'), {})
        finally:
            if native:
                monitor.register_callback(witness.tool_id, monitor.events.BRANCH, None)
    return witness.rows() + witness.module_rows()


class PythonRuntimeInterruptTests(unittest.TestCase):
    def test_nested_witness_keeps_other_monitors_branch_origins(self):
        # Two witnesses need both reserved IDs; the campaign measuring this test owns a third.
        script = '''import importlib.util, sys
sys.path[:0] = sys.argv[1:3]
spec = importlib.util.spec_from_file_location('python_runtime', sys.argv[3])
module = importlib.util.module_from_spec(spec)
sys.modules['python_runtime'] = module
spec.loader.exec_module(module)
from test_python_runtime_interrupt import ACCOUNTING, CallableWitness, measured
with CallableWitness({}):
    assert measured(ACCOUNTING, True) == measured(ACCOUNTING, False)
'''
        tests = Path(__file__).resolve().parent
        result = subprocess.run([sys.executable, '-I', '-c', script, str(tests), str(tests.parent),
                                 sys.modules['python_runtime'].__file__], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_branch_and_callable_accounting_matches_native_monitoring(self):
        extended = ('def work(value):\n    if value:\n' + '        value += 1\n' * 300
                    + '    return value\nwork(False)\nwork(True)\n')
        for source in (ACCOUNTING, extended):
            with self.subTest(extended=source is extended):
                self.assertEqual(measured(source, True), measured(source, False))

    def test_interrupted_instruction_does_not_leave_a_pending_edge_or_frame(self):
        source = 'def work(value):\n    return 1 if value else 2\n'
        namespace = {}
        exec(compile(source, 'pending-subject.py', 'exec'), namespace)
        code = namespace['work'].__code__
        branch = next(instruction for instruction in dis.get_instructions(code)
                      if instruction.opname == 'POP_JUMP_IF_FALSE')
        witness = CallableWitness({'pending-subject.py': source})
        witness._instruction(code, branch.offset)
        self.assertEqual(1, len(witness._pending))
        witness._exception(code, branch.offset, KeyboardInterrupt())
        self.assertEqual({}, witness._pending)
        witness._instruction(code, branch.argval)
        self.assertEqual([], witness.rows()[0]['branches'])
        witness._instruction(code, branch.offset)
        witness._instruction(code, branch.argval)
        self.assertEqual([[branch.offset, branch.argval]], witness.rows()[0]['branches'])
        self.assertIs(sys.monitoring.DISABLE, witness._instruction(code, 0))
        self.assertIs(sys.monitoring.DISABLE, witness._instruction(measured.__code__, 0))
        with witness:
            namespace['work'](True)
            self.assertEqual(sys.monitoring.events.INSTRUCTION,
                             sys.monitoring.get_local_events(witness.tool_id, code))
            witness._instruction(code, branch.offset)
        self.assertEqual({}, witness._pending)
        before = witness.rows()
        namespace['work'](False)  # free_tool_id invalidates local events lazily on next execution.
        self.assertEqual(before, witness.rows())
        self.assertIsNone(sys.monitoring.get_tool(witness.tool_id))
        self.assertEqual(0, sys.monitoring.get_local_events(witness.tool_id, code))

    def test_signal_inside_callback_reaches_target_and_non_target_handler(self):
        namespace = {}
        exec(compile(SOURCE, 'interrupt-subject.py', 'exec'), namespace)
        work = namespace['work']
        origin = next(instruction.offset for instruction in dis.get_instructions(work)
                      if instruction.opname == 'POP_JUMP_IF_FALSE')
        original_row = CallableWitness._row
        original_handler = signal.getsignal(signal.SIGINT)
        delivered = []

        def interrupt(_signum, frame):
            delivered.append(frame.f_code.co_name)
            raise exception('real signal in witness')

        def row(witness, code):
            callback = sys._getframe(1)
            offset = callback.f_locals.get('origin', callback.f_locals.get('offset'))
            site = offset == origin
            if not target and hasattr(witness, '_instruction'):
                site = callback.f_code.co_name == '_line' and callback.f_locals['line'] == 4
            if code is work.__code__ and site and not delivered:
                signal.raise_signal(signal.SIGINT)
            return original_row(witness, code)

        try:
            signal.signal(signal.SIGINT, interrupt)
            for target in (False, True):
                for exception in (KeyboardInterrupt, RuntimeError):
                    with self.subTest(target=target, exception=exception.__name__):
                        delivered.clear()
                        sources = {'interrupt-subject.py': SOURCE} if target else {}
                        with patch.object(CallableWitness, '_row', row), CallableWitness(sources):
                            try:
                                answer = work()
                            except (KeyboardInterrupt, RuntimeError):
                                answer = 'ESCAPED'
                        self.assertEqual(['row'], delivered, 'signal must run inside the real callback')
                        self.assertEqual('ADAPTER_INTERRUPTED', answer)
        finally:
            signal.signal(signal.SIGINT, original_handler)


if __name__ == '__main__':
    unittest.main()

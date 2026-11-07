"""Real signals inside the witness must reach the measured frame's exception handler."""
import dis
import signal
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
            monitor.register_callback(witness.tool_id, monitor.events.BRANCH, witness._branch)
            monitor.set_events(witness.tool_id, monitor.events.PY_START | monitor.events.LINE | monitor.events.BRANCH)
        try:
            exec(compile(source, path, 'exec'), {})
        finally:
            if native:
                monitor.register_callback(witness.tool_id, monitor.events.BRANCH, None)
    return witness.rows() + witness.module_rows()


class PythonRuntimeInterruptTests(unittest.TestCase):
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
            witness._instruction(code, branch.offset)
        self.assertEqual({}, witness._pending)

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
            if code is work.__code__ and offset == origin and not delivered:
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

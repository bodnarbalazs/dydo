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


class PythonRuntimeInterruptTests(unittest.TestCase):
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

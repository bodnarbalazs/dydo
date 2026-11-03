"""Source-only contracts call actual twelve worker parsing, attribution and cancellation."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

PACKET = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('_twelve_worker_contract', PACKET / 'worker.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class TwelveWorkerContracts(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix='dyd415-twelve-worker-contract-')
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)

    def events(self, index=0):
        return [
            dict(parent=20, event='spawn', role='worker', pid=27),
            dict(parent=20, event='spawn', role='janitor', pid=33),
            dict(parent=20, event='close', role='worker', pid=27, code=0, signal=None),
            dict(parent=20, event='close', role='janitor', pid=33, code=None, signal='SIGKILL'),
            dict(parent=20, event='cli-exit', code=worker.EXITS[index])]

    def test_exact_source_selection_is_twelve_unique_original_names(self):
        self.assertEqual(12, len(worker.NAMES))
        self.assertEqual(12, len(set(worker.NAMES)))
        self.assertEqual('POSIX janitor close precedes the Counter report and controlled exit 0', worker.NAMES[0])
        self.assertEqual('POSIX final drain preserves a caller file arriving at output publication', worker.NAMES[-1])
        self.assertEqual([0, 1, 2, 130, 143, 2, 2, 2, 2, 2, 2, 2], worker.EXITS)

    def test_complete_exact_native_one_case_summary_is_required(self):
        text = 'ok 1 - selected\n1..1\n# pass 1\n# fail 0\n# cancelled 0\n# todo 0\n'
        self.assertEqual('selected', worker.selected_tap(text, 'selected')[0]['name'])
        for altered in [text.replace('ok 1 - selected', 'not ok 1 - selected'),
                        text.replace('selected\n', 'selected # SKIP unavailable\n'),
                        text.replace('1..1', '1..2'), text.replace('# cancelled 0', '# cancelled 1'),
                        text.replace('# pass 1', '# pass 2'), text.replace('selected', 'foreign'),
                        text.replace('1..1', 'ok 2 - selected\n1..2')]:
            with self.subTest(text=altered), self.assertRaises(RuntimeError):
                worker.selected_tap(altered, 'selected')

    def test_real_child_close_before_controlled_exit_is_required(self):
        events = self.events()
        self.assertEqual(33, worker.lifecycle_events(events, 0)['janitor']['pid'])
        for bad in [events[:3] + [events[4], events[3]],
                    [*events[:3], dict(events[3], pid=34), events[4]],
                    [*events[:3], dict(events[3], signal='SIGTERM'), events[4]]]:
            with self.subTest(events=bad), self.assertRaises(RuntimeError):
                worker.lifecycle_events(bad, 0)

    def test_spawn_failure_close_is_distinct_from_reaping_a_created_janitor(self):
        events = [dict(parent=20, event='spawn', role='janitor'),
                  dict(parent=20, event='close', role='worker', pid=27, code=None, signal='SIGKILL'),
                  dict(parent=20, event='close', role='janitor', code=-2, signal=None),
                  dict(parent=20, event='cli-exit', code=2)]
        result = worker.lifecycle_events(events, 5)
        self.assertNotIn('pid', result['janitor'])
        events[0]['pid'] = 33
        with self.assertRaises(RuntimeError):
            worker.lifecycle_events(events, 5)

    def test_worker_spawn_failure_requires_no_janitor(self):
        events = [dict(parent=20, event='close', role='worker', code=-2, signal=None),
                  dict(parent=20, event='cli-exit', code=2)]
        self.assertEqual('NOT_CREATED', worker.lifecycle_events(events, 7)['janitor'])
        with self.assertRaises(RuntimeError):
            worker.lifecycle_events(self.events(7), 7)

    def test_runner_identity_uncertainty_never_kills_an_unproved_group(self):
        stat = '42 (node) ' + ' '.join(['S', '9', *(['0'] * 17), '123'])
        child = SimpleNamespace(pid=42, poll=lambda: None)
        with patch.object(worker, 'Path', return_value=SimpleNamespace(read_text=lambda: stat)), \
                patch.object(worker.os, 'getpid', return_value=1), \
                patch.object(worker.os, 'getpgid', return_value=42), patch.object(worker.os, 'killpg') as kill:
            with self.assertRaisesRegex(RuntimeError, 'ownership uncertain'):
                worker.drain_runner(child, '123')
        kill.assert_not_called()

    def test_runner_exact_owned_group_cancellation_uses_sigkill(self):
        stat = '42 (node) ' + ' '.join(['S', '1', *(['0'] * 17), '123'])
        child = SimpleNamespace(pid=42, poll=lambda: None)
        with patch.object(worker, 'Path', return_value=SimpleNamespace(read_text=lambda: stat)), \
                patch.object(worker.os, 'getpid', return_value=1), \
                patch.object(worker.os, 'getpgid', return_value=42), patch.object(worker.os, 'killpg') as kill:
            worker.drain_runner(child, '123')
        kill.assert_called_once_with(42, worker.signal.SIGKILL)

    def test_timeout_stays_failure_after_late_runner_exit_and_bounded_reap(self):
        foundation = SimpleNamespace(OUTPUT=self.root, NODE=self.root / 'inert-node',
            SCRIPTS=self.root, LIMIT=16 * 1024 * 1024,
            remaining_processes=Mock(), environment=lambda: {}, save=Mock(), census=lambda: [{'pid': 1}])
        child = Mock(pid=42)
        child.poll.return_value = 0
        child.returncode = 0
        original = subprocess.TimeoutExpired('inert-native', 65)
        child.wait.side_effect = [original, 0]
        stat = self.root / 'stat'
        stat.write_text('42 (node) ' + ' '.join(['S', '1', *(['0'] * 17), '123']), encoding='utf-8')
        native_path = Path
        def owned_path(*parts):
            return stat if parts[0] == '/proc' else native_path(*parts)
        with patch.object(worker.subprocess, 'Popen', return_value=child), \
                patch.object(worker, 'Path', side_effect=owned_path), \
                patch.object(worker.os, 'getpgid', return_value=42), \
                patch.object(worker.os, 'getpid', return_value=1):
            with self.assertRaises(subprocess.TimeoutExpired) as result:
                worker.run_case(foundation, worker.NAMES[0], 0)
        self.assertIs(original, result.exception)
        self.assertEqual([65, 5], [call.kwargs['timeout'] for call in child.wait.call_args_list])


if __name__ == '__main__':
    unittest.main()

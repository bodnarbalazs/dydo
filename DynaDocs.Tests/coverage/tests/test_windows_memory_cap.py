"""Windows facade caps: mocked kernel32 on every host, real enforcement on Windows."""
import contextlib
import ctypes
import io
import json
import os
import runpy
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

RUNNER = Path(__file__).resolve().parents[1] / 'gap_check.py'
MIB = 1024 ** 2
HOST_PLATFORM = sys.platform


class WindowsMemoryCapTests(unittest.TestCase):
    def setUp(self):
        self.namespace = runpy.run_path(str(RUNNER))['main'].__globals__
        self.kernel = mock.Mock()
        self.kernel.GetCurrentProcess.return_value = -1
        self.kernel.CreateJobObjectW.return_value = 101
        self.kernel.CreateIoCompletionPort.return_value = 202
        self.kernel.IsProcessInJob.side_effect = self.in_job
        self.kernel.QueryInformationJobObject.side_effect = self.query
        self.kernel.GetQueuedCompletionStatus.side_effect = self.dequeue
        self.outer = None
        self.peak = 0
        self.messages = []
        self.error = 258  # WAIT_TIMEOUT: an empty completion port.
        self.stderr = io.StringIO()
        self.enterContext(mock.patch.object(ctypes, 'WinDLL', return_value=self.kernel, create=True))
        self.enterContext(mock.patch.object(ctypes, 'get_last_error', side_effect=lambda: self.error, create=True))
        self.enterContext(mock.patch.object(ctypes, 'WinError', side_effect=lambda code: OSError(f'Windows error {code}'), create=True))
        self.enterContext(mock.patch.object(sys, 'platform', 'win32'))
        self.enterContext(mock.patch.dict(os.environ, {'DYDO_MEMORY_MAX': '256M'}))
        self.enterContext(contextlib.redirect_stderr(self.stderr))
        self.registrations = self.enterContext(mock.patch('atexit.register'))

    def in_job(self, _process, _job, answer):
        answer._obj.value = self.outer is not None
        return 1

    def query(self, job, _kind, output, _size, _returned):
        if job is None:
            output._obj.basic.flags = 0x200 if self.outer else 0
            output._obj.job_memory = self.outer or 0
        output._obj.peak_job = self.peak
        return 1

    def dequeue(self, _port, message, key, _overlapped, wait):
        self.assertEqual(0, wait)
        if not self.messages:
            return 0
        message._obj.value, key._obj.value = self.messages.pop(0)
        return 1

    def cap(self):
        return self.namespace['memory_cap']()

    def row(self, cap, before, state='passed'):
        return cap.judged({'stack': 'fixture', 'capability': 'test', 'state': state,
                           'resultExit': {'passed': 0, 'failed': 1, 'invalid': 2, 'interrupted': 130}[state]}, before)

    def test_default_and_lower_caps_assign_self_without_changing_child_lifetimes(self):
        for value, limit in (('8G', 8 * 1024 ** 3), ('256M', 256 * MIB), ('1.5G', 1536 * MIB)):
            with self.subTest(value=value), mock.patch.dict(os.environ, {'DYDO_MEMORY_MAX': value}):
                cap = self.cap()
                self.assertEqual(('enforced', limit, 'windows job object', 'best-effort'),
                                 tuple(cap.record[k] for k in ('state', 'limit', 'mechanism', 'memoryCapHitReporting')))
                self.assertIn('1536M' if value == '1.5G' else value, cap.record['reason'])
                limits = [call.args[2]._obj for call in self.kernel.SetInformationJobObject.call_args_list
                          if call.args[1] == 9][-1]
                self.assertEqual(0x200, limits.basic.flags)  # Neither breakaway nor kill-on-close.
                self.assertEqual(limit, limits.job_memory)
                association = [call.args[2]._obj for call in self.kernel.SetInformationJobObject.call_args_list
                               if call.args[1] == 7][-1]
                self.assertEqual((101, 202), (association.key, association.port))
                self.kernel.AssignProcessToJobObject.assert_called_with(101, -1)
        self.assertEqual('', self.stderr.getvalue())

    def test_job_and_port_live_until_process_exit_without_terminating_members(self):
        self.cap()
        self.kernel.CloseHandle.assert_not_called()
        callback = self.registrations.call_args.args[0]
        callback()
        callback()
        self.assertEqual([mock.call(101), mock.call(202)], self.kernel.CloseHandle.call_args_list)
        self.kernel.TerminateJobObject.assert_not_called()

    def test_setting_the_limit_failure_closes_both_handles_before_assignment(self):
        self.kernel.SetInformationJobObject.side_effect = [1, 0]
        self.error = 87
        cap = self.cap()
        self.assertEqual('unavailable', cap.record['state'])
        self.assertIn('Windows error 87', cap.record['reason'])
        self.kernel.AssignProcessToJobObject.assert_not_called()
        self.assertEqual([mock.call(101), mock.call(202)], self.kernel.CloseHandle.call_args_list)

    def test_outer_jobs_always_get_a_nested_job_with_the_effective_limit(self):
        for outer, expected in ((128 * MIB, 128 * MIB), (512 * MIB, 256 * MIB), (0, 256 * MIB)):
            with self.subTest(outer=outer):
                self.outer = outer
                cap = self.cap()
                self.assertEqual(('enforced', expected), (cap.record['state'], cap.record['limit']))
                self.kernel.AssignProcessToJobObject.assert_called_with(101, -1)
                self.assertTrue(any(call.args[0] is None for call in self.kernel.QueryInformationJobObject.call_args_list))

    def test_setup_failures_warn_once_and_release_created_handles(self):
        for name in ('IsProcessInJob', 'QueryInformationJobObject', 'CreateJobObjectW',
                     'CreateIoCompletionPort', 'SetInformationJobObject', 'AssignProcessToJobObject'):
            with self.subTest(api=name):
                self.outer = 512 * MIB
                self.stderr.seek(0)
                self.stderr.truncate()
                self.kernel.reset_mock()
                self.error = 5
                with mock.patch.object(self.kernel, name, return_value=0):
                    cap = self.cap()
                self.assertEqual('unavailable', cap.record['state'])
                self.assertIn('Windows error 5', cap.record['reason'])
                self.assertEqual(1, self.stderr.getvalue().count('memory cap unavailable'))
                created = []
                if self.kernel.CreateJobObjectW.called and name != 'CreateJobObjectW':
                    created.append(101)
                if self.kernel.CreateIoCompletionPort.called and name != 'CreateIoCompletionPort':
                    created.append(202)
                self.assertCountEqual(created, [call.args[0] for call in self.kernel.CloseHandle.call_args_list])

    def test_only_delivered_job_memory_messages_fail_the_current_row(self):
        cap = self.cap()
        for state in ('passed', 'failed', 'invalid', 'interrupted'):
            with self.subTest(state=state):
                before = cap.kills()
                self.messages = [(10, 101)]  # JOB_OBJECT_MSG_JOB_MEMORY_LIMIT.
                row = self.row(cap, before, state)
                self.assertEqual(('failed', 1, True), (row['state'], row['resultExit'], row['memoryCapHit']))
                self.assertEqual('memory cap hit (MemoryMax=256M): a finding; do not raise the cap', row['reason'])
                self.assertEqual('best-effort', row['memoryCapHitReporting'])
        before = cap.kills()
        self.assertNotIn('memoryCapHit', self.row(cap, before))

    def test_stale_and_unrelated_messages_and_peak_never_imply_a_hit(self):
        cap = self.cap()
        self.messages = [(10, 101)]
        before = cap.kills()  # Discard notifications already present before the row.
        for state in ('passed', 'failed'):
            with self.subTest(state=state):
                self.messages = [(8, 101), (9, 101), (10, 999)]
                self.peak = 256 * MIB
                row = self.row(cap, before, state)
                self.assertEqual(state, row['state'])
                self.assertNotIn('memoryCapHit', row)
                self.assertEqual(256 * MIB, row['peakJobMemory'])

    def test_process_memory_and_other_non_job_memory_messages_never_imply_a_hit(self):
        cap = self.cap()
        # 9 is JOB_OBJECT_MSG_PROCESS_MEMORY_LIMIT; only 10 reports the job limit.
        for message in (*range(10), 11, 12, 13, 0xffffffff):
            with self.subTest(message=message):
                before = cap.kills()
                self.messages = [(message, 101)]
                row = self.row(cap, before)
                self.assertEqual(('passed', 0), (row['state'], row['resultExit']))
                self.assertNotIn('memoryCapHit', row)

    def test_derived_and_unavailable_rows_keep_reporting_and_peak_diagnostics(self):
        cap = self.cap()
        self.peak = 100 * MIB
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            code = "from pathlib import Path; Path('coverage.json').write_text('{\"exit\": 0}')"
            command = {'kind': 'current-python', 'argv': ['-c', code]}
            stack = {'name': 'fixture', 'kind': 'fixture', 'cwd': '.',
                     'isolation': {'requirement': 'in-place', 'evidence': {'state': 'verified', 'kind': 'direct'}},
                     'capabilities': {
                         'test': {'state': 'configured', 'command': command, 'artifacts': []},
                         'static': {'state': 'unavailable', 'reason': 'fixture'},
                         'mutation': {'state': 'unavailable', 'reason': 'fixture'},
                         'coverage': {'state': 'configured', 'command': command,
                                      'artifacts': [{'path': 'coverage.json', 'required': True}],
                                      'suiteVerdict': {'exit': ['exit'], 'failure': ['findings', {'gate': 'functional'}]}}}}
            # Only kernel32 is simulated; the row executes a real child on the host OS.
            with mock.patch.object(sys, 'platform', HOST_PLATFORM), contextlib.redirect_stdout(io.StringIO()):
                rows = self.namespace['execute_rows']([stack], ('test', 'static', 'coverage'), root, None, [], cap)
            self.assertEqual(['passed', 'unavailable', 'passed'], [row['state'] for row in rows])
            for row in rows:
                self.assertEqual('best-effort', row['memoryCapHitReporting'])
                self.assertEqual(100 * MIB, row['peakJobMemory'])
                self.assertNotIn('memoryCapHit', row)

    def test_observation_errors_are_unavailable_without_inventing_a_hit(self):
        for api in ('GetQueuedCompletionStatus', 'QueryInformationJobObject'):
            with self.subTest(api=api):
                cap = self.cap()
                before = cap.kills()
                self.error = 6
                self.stderr.seek(0)
                self.stderr.truncate()
                with mock.patch.object(self.kernel, api, return_value=0):
                    row = self.row(cap, before, 'failed')
                    cap.kills()
                self.assertEqual('unavailable', cap.record['state'])
                self.assertEqual('failed', row['state'])
                self.assertNotIn('memoryCapHit', row)
                self.assertEqual('windows job object: Windows error 6', cap.record['reason'])
                self.assertEqual('memory cap unavailable: windows job object: Windows error 6; '
                                 'this operation continues\n', self.stderr.getvalue())
                self.kernel.AssignProcessToJobObject.assert_called_with(101, -1)
                self.kernel.CloseHandle.assert_not_called()
                self.error = 258

    def test_a_raised_cap_is_rejected_before_any_windows_api_call(self):
        with mock.patch.dict(os.environ, {'DYDO_MEMORY_MAX': '9G'}):
            with self.assertRaisesRegex(self.namespace['ContractError'], 'raising the cap is not allowed'):
                self.cap()
        self.assertEqual([], self.kernel.mock_calls)


class LiveWindowsMemoryCapTests(unittest.TestCase):
    @unittest.skipUnless(sys.platform == 'win32', 'Windows Job-object enforcement proof unavailable on this host')
    def test_live_windows_facade_rejects_allocation_above_job_memory_cap(self):
        with tempfile.TemporaryDirectory(prefix='dydo-windows-cap-') as temporary:
            root = Path(temporary)
            shutil.copyfile(RUNNER, root / 'gap_check.py')
            code = "from pathlib import Path\ntry:\n    block = bytearray(400 * 1024 ** 2)\nexcept MemoryError:\n    Path('rejected').write_text('MemoryError')\n    raise SystemExit(1)\n"
            caps = {name: {'state': 'unavailable', 'reason': 'fixture'} for name in ('static', 'coverage', 'mutation')}
            caps['test'] = {'state': 'configured', 'command': {'kind': 'current-python', 'argv': ['-c', code]}, 'artifacts': []}
            manifest = {'schema': 1, 'artifactRoot': 'results', 'stacks': [
                {'name': 'allocation', 'kind': 'fixture', 'cwd': '.', 'capabilities': caps,
                 'isolation': {'requirement': 'in-place', 'evidence': {'state': 'verified', 'kind': 'direct'}}}]}
            (root / 'gap_check.json').write_text(json.dumps(manifest), encoding='utf-8')
            process = subprocess.run([sys.executable, str(root / 'gap_check.py'), 'all'], cwd=root,
                                     env={**os.environ, 'DYDO_MEMORY_MAX': '256M'}, capture_output=True,
                                     text=True, encoding='utf-8', timeout=60)
            self.assertEqual(1, process.returncode, process.stdout + process.stderr)
            self.assertEqual('MemoryError', (root / 'rejected').read_text())
            payload = json.loads(next((root / 'results').glob('run-*/result.json')).read_text())
            self.assertEqual(('enforced', 256 * MIB, 'windows job object', 'best-effort'),
                             tuple(payload['memoryCap'][k] for k in ('state', 'limit', 'mechanism', 'memoryCapHitReporting')))
            row = payload['results'][0]
            self.assertEqual(('failed', 1, 'best-effort'), (row['state'], row['resultExit'], row['memoryCapHitReporting']))
            self.assertGreater(row['peakJobMemory'], 0)
            if row.get('memoryCapHit'):
                self.assertIn('a finding; do not raise the cap', row['reason'])
            print(f'{self.id()} PASSED: Windows job cap enforced; allocation rejected; '
                  f'memoryCapHit={row.get("memoryCapHit", False)}', flush=True)

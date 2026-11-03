"""Direct successor contracts. No live queue, Docker, Node or product process is invoked."""
from contextlib import nullcontext, redirect_stderr
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-janitor-review-fix-source'


def load(name):
    spec = importlib.util.spec_from_file_location('_statechart_janitor_successor_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


driver, launcher, worker = [load(name) for name in ['driver', 'launcher', 'worker']]


class JanitorSuccessorContracts(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix='dyd415-janitor-successor-contract-')
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)
        self.lock = self.root / 'isolated.lock'
        self.owner = b'{"owner":"isolated-unittest-only","nonce":"test"}\n'
        self.lock.write_bytes(self.owner)
        evidence = patch.object(driver, 'EVIDENCE_ERRORS', [])
        evidence.start()
        self.addCleanup(evidence.stop)

    def release_fixture(self):
        pin = self.root / 'source-pin.json'
        pin.write_bytes(b'{"fixture":"finite-successor"}\n')
        release = dict(state='RELEASED_B3_JANITOR_REVIEW_FIX', signals=['SIGINT'],
            sourcePinSha256=hashlib.sha256(pin.read_bytes()).hexdigest(),
            captainInspectionRecord='isolated fixture', dockerIdentity={'fixture': True})
        self.write_release(release)
        return release

    def write_release(self, release):
        (self.root / 'execution-release.json').write_text(json.dumps(release), encoding='utf-8')

    def test_retention_failure_with_successful_error_log_preserves_exact_lock_and_error(self):
        original = OSError('retention failed')
        with patch.object(driver.common, 'retain_work', side_effect=original) as retain, \
                patch.object(driver.common, 'save') as save:
            failure = driver.retain_and_release(self.lock, self.owner, True, None)
        self.assertIs(original, failure)
        self.assertEqual(self.owner, self.lock.read_bytes())
        self.assertEqual([], driver.EVIDENCE_ERRORS)
        retain.assert_called_once_with()
        self.assertEqual('retain-work-error.json', save.call_args.args[0])

    def test_healthy_retention_releases_only_matching_isolated_lock(self):
        with patch.object(driver.common, 'retain_work') as retain, patch.object(driver.common, 'save') as save:
            self.assertIsNone(driver.retain_and_release(self.lock, self.owner, True, None))
        retain.assert_called_once_with()
        save.assert_not_called()
        self.assertFalse(self.lock.exists())

    def test_error_evidence_write_failure_retains_lock_and_original_failure(self):
        original = OSError('retention failed')
        with patch.object(driver.common, 'retain_work', side_effect=original), \
                patch.object(driver.common, 'save', side_effect=OSError('evidence unavailable')):
            self.assertIs(original, driver.retain_and_release(self.lock, self.owner, True, None))
        self.assertEqual(self.owner, self.lock.read_bytes())
        self.assertEqual('retain-work-error.json', driver.EVIDENCE_ERRORS[0]['record'])

    def test_prior_evidence_failure_blocks_release_after_healthy_retention(self):
        driver.EVIDENCE_ERRORS.append(dict(record='stop.json', error='OSError'))
        with patch.object(driver.common, 'retain_work'):
            self.assertIsNone(driver.retain_and_release(self.lock, self.owner, True, None))
        self.assertEqual(self.owner, self.lock.read_bytes())

    def test_unconfirmed_owned_cleanup_keeps_lock_after_healthy_retention(self):
        with patch.object(driver.common, 'retain_work'):
            self.assertIsNone(driver.retain_and_release(self.lock, self.owner, False, None))
        self.assertEqual(self.owner, self.lock.read_bytes())

    def test_changed_owner_bytes_are_never_deleted(self):
        foreign = b'{"owner":"different"}\n'
        self.lock.write_bytes(foreign)
        with patch.object(driver.common, 'retain_work'), patch.object(driver.common, 'save') as save:
            failure = driver.retain_and_release(self.lock, self.owner, True, None)
        self.assertIsInstance(failure, RuntimeError)
        self.assertIn('Ownership lock bytes differ', str(failure))
        self.assertEqual(foreign, self.lock.read_bytes())
        self.assertEqual('lock-release-error.json', save.call_args.args[0])

    def test_missing_lock_is_ownership_failure_not_successful_release(self):
        self.lock.unlink()
        with patch.object(driver.common, 'retain_work'), patch.object(driver.common, 'save'):
            failure = driver.retain_and_release(self.lock, self.owner, True, None)
        self.assertIsInstance(failure, FileNotFoundError)
        self.assertFalse(self.lock.exists())

    def test_original_product_failure_survives_retention_failure(self):
        original = RuntimeError('original product failure')
        with patch.object(driver.common, 'retain_work', side_effect=OSError('retention failed')), \
                patch.object(driver.common, 'save'):
            self.assertIs(original, driver.retain_and_release(self.lock, self.owner, True, original))
        self.assertEqual(self.owner, self.lock.read_bytes())

    def test_actual_main_propagates_nonzero_failure_and_keeps_its_isolated_lock(self):
        original = RuntimeError('original product failure')
        destination = self.root / 'b3-janitor-review-fix-20261031a-SIGINT'
        destination.mkdir()
        booking = SimpleNamespace(owned=True, inherited=False)
        slot = SimpleNamespace(__file__=str(driver.LC / 'tools/testing/runner/suite_slot.py'),
            live=lambda: SimpleNamespace(directory=Path('C:/Users/User/AppData/Local/lc-test/suite-slot'),
                prefix='LC-Suite-Slot', variable='LC_SUITE_SLOT_TICKET'),
            book=Mock(return_value=nullcontext(booking)), refuse_heavy_wait=Mock(return_value=None))
        locks = SimpleNamespace(__file__=str(driver.LC / 'tools/testing/runner/machine_lock.py'),
            HEAVY='isolated-heavy', machine_lock=Mock(return_value=nullcontext()))
        modules = {'tools': SimpleNamespace(), 'tools.testing': SimpleNamespace(),
            'tools.testing.runner': SimpleNamespace(suite_slot=slot, machine_lock=locks),
            'tools.testing.runner.machine_lock': locks}
        with patch.dict(sys.modules, modules), patch.object(sys, 'argv', ['driver.py', '--signal', 'SIGINT']), \
                patch.object(sys, 'path', list(sys.path)), \
                patch.dict(driver.os.environ, {'LC_SUITE_SLOT_TICKET': 'isolated-test-ticket'}), \
                patch.object(driver, 'BASE', self.root), patch.object(driver, 'TEMP', self.root), \
                patch.object(driver, 'verify_inputs'), patch.object(driver, 'execute', side_effect=original), \
                patch.object(driver, 'cleanup', return_value=True), patch.object(driver.common, 'save'), \
                patch.object(driver.common, 'retain_work', side_effect=OSError('retention failed')), \
                patch.object(driver.common, 'OUTPUT', None), patch.object(driver, 'OUTPUT', None), \
                patch.object(driver, 'SIGNAL', None):
            with self.assertRaises(RuntimeError) as result:
                driver.main()
        self.assertIs(original, result.exception)
        owned = self.root / 'dydo-fullsuite.lock'
        row = json.loads(owned.read_bytes())
        self.assertEqual('isolated-test-ticket', row['ticket'])
        self.assertEqual(16, len(row['nonce']))
        slot.book.assert_called_once()
        locks.machine_lock.assert_called_once()

    def test_launcher_requires_exact_release_signal_pin_and_inspection(self):
        release = self.release_fixture()
        self.assertEqual({'fixture': 'finite-successor'}, launcher.validate_release(self.root, 'SIGINT'))
        with self.assertRaises(RuntimeError):
            launcher.validate_release(self.root, 'CONTRACTS')
        self.write_release(dict(release, contracts=True))
        self.assertEqual({'fixture': 'finite-successor'}, launcher.validate_release(self.root, 'CONTRACTS'))
        for change, selected in [(dict(signals=['SIGINT']), 'SIGTERM'), (dict(signals=['SIGINT', 'SIGINT']), 'SIGINT'),
                (dict(signals=['SIGKILL']), 'SIGKILL'), (dict(state='SOURCE_ONLY'), 'SIGINT'),
                (dict(sourcePinSha256='0' * 64), 'SIGINT'), (dict(captainInspectionRecord=None), 'SIGINT'),
                (dict(dockerIdentity=None), 'SIGINT')]:
            with self.subTest(change=change):
                self.write_release(dict(release, **change))
                with self.assertRaises(RuntimeError):
                    launcher.validate_release(self.root, selected)

    def test_launcher_missing_release_or_pin_refuses(self):
        self.release_fixture()
        (self.root / 'execution-release.json').unlink()
        with self.assertRaises(FileNotFoundError):
            launcher.validate_release(self.root, 'SIGINT')
        (self.root / 'source-pin.json').unlink()
        with self.assertRaises(FileNotFoundError):
            launcher.validate_release(self.root, 'SIGINT')

    def test_host_refusal_precedes_any_material_command(self):
        for module in [driver, launcher]:
            for platform, local, temporary in [('linux', 'C:/Users/User/AppData/Local', module.TEMP),
                    ('win32', str(self.root), module.TEMP),
                    ('win32', 'C:/Users/User/AppData/Local', self.root)]:
                with self.subTest(module=module.__name__, platform=platform, local=local), \
                        patch.object(module, 'os', SimpleNamespace(name='nt' if platform == 'win32' else 'posix',
                            environ={'LOCALAPPDATA': local})), \
                        patch.object(module, 'sys', SimpleNamespace(platform=platform)), \
                        patch.object(module.tempfile, 'gettempdir', return_value=str(temporary)), \
                        patch.object(module, 'subprocess') as commands:
                    with self.assertRaises(RuntimeError):
                        module.validate_host()
                commands.check_output.assert_not_called()
                commands.run.assert_not_called()

    def test_all_entrypoints_reject_missing_or_unreleased_signal_before_admission(self):
        for module, boundary in [(driver, 'verify_inputs'), (launcher, 'validate_host'), (worker, 'load_foundation')]:
            for arguments in [[], ['--signal', 'SIGKILL'], ['--signal', 'SIGUSR1']]:
                with self.subTest(module=module.__name__, arguments=arguments), \
                        patch.object(sys, 'argv', ['proof.py', *arguments]), \
                        patch.object(module, boundary) as admitted, redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit) as result:
                        module.main()
                    self.assertEqual(2, result.exception.code)
                    admitted.assert_not_called()

    def test_driver_head_drift_refuses_before_packet_or_resource_admission(self):
        with patch.object(driver, 'validate_host'), patch.object(driver, 'subprocess') as commands:
            commands.check_output.return_value = b'wrong-head\n'
            with self.assertRaisesRegex(RuntimeError, 'HEAD differs'):
                driver.verify_inputs()
        commands.check_output.assert_called_once()
        commands.run.assert_not_called()

    def test_worker_accepts_only_exact_selected_native_ok_with_summary(self):
        text = 'ok 1 - excluded # SKIP test name does not match pattern\nok 2 - selected\n# pass 1\n# fail 0\n# cancelled 0\n# todo 0\n'
        rows = worker.selected_tap(text, 'selected')
        self.assertEqual(['excluded', 'selected'], [row['name'] for row in rows])
        self.assertEqual('', rows[1]['directive'])

    def test_worker_rejects_missing_duplicate_skipped_failed_unselected_and_bad_summary(self):
        summary = '# pass 1\n# fail 0\n# cancelled 0\n# todo 0\n'
        for text in ['', 'ok 1 - selected # SKIP unavailable\n' + summary,
                'ok 1 - selected\nok 2 - selected\n' + summary,
                'not ok 1 - selected\n' + summary, 'ok 1 - selected\nok 2 - foreign\n' + summary,
                'ok 1 - selected\nok 2 - foreign # SKIP unrelated\n' + summary,
                'ok 1 - selected\n' + summary.replace('# pass 1', '# pass 2'),
                'ok 1 - selected\n' + summary.replace('# cancelled 0', '# cancelled 1')]:
            with self.subTest(text=text), self.assertRaises(RuntimeError):
                worker.selected_tap(text, 'selected')

    def test_worker_refuses_nonprivate_pid_topology_before_foundation_or_runtime(self):
        with patch.object(sys, 'argv', ['worker.py', '--signal', 'SIGINT']), \
                patch.object(worker, 'os', SimpleNamespace(getpid=lambda: 99)), \
                patch.object(worker, 'load_foundation') as foundation:
            with self.assertRaisesRegex(RuntimeError, 'private Linux'):
                worker.main()
        foundation.assert_not_called()

    def test_driver_ambient_docker_route_refuses_before_any_host_command(self):
        with patch.dict(driver.os.environ, {'DOCKER_HOST': 'forbidden'}), patch.object(driver, 'run') as command:
            with self.assertRaisesRegex(RuntimeError, 'Ambient Docker'):
                driver.check_daemon('isolated-refusal')
        command.assert_not_called()

    def test_contract_command_validates_actual_native_names_and_completion(self):
        names = ['one (fixture.Contracts.one)', 'two (fixture.Contracts.two)']
        (self.root / 'test-inventory.json').write_text(json.dumps({'nativeIds': names}), encoding='utf-8')
        (self.root / 'contracts.stdout').write_bytes(b'')
        (self.root / 'contracts.stderr').write_text(
            '\n'.join(name + ' ... ok' for name in names) + '\nRan 2 tests in 0.001s\n\nOK\n', encoding='utf-8')
        with patch.object(driver, 'OUTPUT', self.root), patch.object(driver, 'PACKET', self.root), \
                patch.object(driver.common, 'run') as run, patch.object(driver.common, 'save') as save:
            driver.execute_contracts()
        self.assertEqual(180, run.call_args.kwargs['timeout'])
        self.assertEqual(['-I', '-B', '-m', 'unittest', 'discover', '-v'], run.call_args.args[1][1:7])
        self.assertEqual('test_statechart_janitor_successor.py', run.call_args.args[1][-1])
        self.assertEqual(2, save.call_args.args[1]['contracts'])

    def test_contract_command_rejects_nonempty_stdout_skip_and_wrong_selection(self):
        name = 'one (fixture.Contracts.one)'
        (self.root / 'test-inventory.json').write_text(json.dumps({'nativeIds': [name]}), encoding='utf-8')
        for stdout, stderr in [(b'unexpected', name + ' ... ok\nRan 1 tests in 0.001s\nOK\n'),
                (b'', name + " ... skipped 'unavailable'\nRan 1 tests in 0.001s\nOK\n"),
                (b'', 'foreign ... ok\nRan 1 tests in 0.001s\nOK\n'),
                (b'', name + ' ... ok\n')]:
            with self.subTest(stdout=stdout, stderr=stderr):
                (self.root / 'contracts.stdout').write_bytes(stdout)
                (self.root / 'contracts.stderr').write_text(stderr, encoding='utf-8')
                with patch.object(driver, 'OUTPUT', self.root), patch.object(driver, 'PACKET', self.root), \
                        patch.object(driver.common, 'run'), patch.object(driver.common, 'save') as save:
                    with self.assertRaises(RuntimeError):
                        driver.execute_contracts()
                save.assert_not_called()

    def test_contract_native_failure_stops_before_success_receipt(self):
        original = RuntimeError('native command failed')
        with patch.object(driver.common, 'run', side_effect=original), patch.object(driver.common, 'save') as save:
            with self.assertRaises(RuntimeError) as failure:
                driver.execute_contracts()
        self.assertIs(original, failure.exception)
        save.assert_not_called()


if __name__ == '__main__':
    unittest.main()

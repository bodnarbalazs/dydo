"""Direct B/C successor contracts; future contained unittest only, no real tool launch."""
from contextlib import redirect_stderr
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-bc-source'


def load(name):
    spec = importlib.util.spec_from_file_location('_statechart_bc_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


driver, launcher, worker = [load(name) for name in ['driver', 'launcher', 'worker']]


class RmiBcContracts(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix='dyd415-bc-contract-')
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)

    def write_json(self, path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding='utf-8')

    def mapped_path(self, *parts):
        return self.root / Path(*parts).relative_to('/')

    def release_fixture(self):
        pin = self.root / 'source-pin.json'
        pin.write_bytes(b'{"fixture":"finite-release"}\n')
        release = dict(state='RELEASED_B3_BC', groups=['B', 'C'],
            sourcePinSha256=hashlib.sha256(pin.read_bytes()).hexdigest())
        self.write_json(self.root / 'execution-release.json', release)
        return release

    def test_launcher_accepts_only_exact_released_group_and_source_pin(self):
        release = self.release_fixture()
        self.assertEqual(dict(fixture='finite-release'), launcher.validate_release(self.root, 'B'))
        self.assertEqual(dict(fixture='finite-release'), launcher.validate_release(self.root, 'C'))
        for change, group in [(dict(groups=['B']), 'C'), (dict(state='SOURCE_ONLY'), 'B'),
                              (dict(sourcePinSha256='0' * 64), 'B')]:
            with self.subTest(change=change):
                self.write_json(self.root / 'execution-release.json', dict(release, **change))
                with self.assertRaises(RuntimeError):
                    launcher.validate_release(self.root, group)
        self.write_json(self.root / 'execution-release.json', release)
        (self.root / 'source-pin.json').write_bytes(b'{"drift":true}')
        with self.assertRaises(RuntimeError):
            launcher.validate_release(self.root, 'B')

    def test_missing_release_or_source_pin_never_admits_a_group(self):
        self.release_fixture()
        (self.root / 'execution-release.json').unlink()
        with self.assertRaises(FileNotFoundError):
            launcher.validate_release(self.root, 'B')
        (self.root / 'source-pin.json').unlink()
        with self.assertRaises(FileNotFoundError):
            launcher.validate_release(self.root, 'C')

    def test_all_three_entrypoints_refuse_missing_or_other_groups_before_material_work(self):
        for module in [driver, launcher, worker]:
            boundary = 'validate_host' if module is launcher else 'verify_inputs' if module is driver else 'verify'
            for arguments in [[], ['--group', 'A'], ['--group', 'D']]:
                with self.subTest(module=module.__name__, arguments=arguments), \
                        patch.object(sys, 'argv', ['proof.py', *arguments]), \
                        patch.object(module, boundary) as material, redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit) as result:
                        module.main()
                    self.assertEqual(2, result.exception.code)
                    material.assert_not_called()

    def test_launcher_refuses_wrong_platform_and_redirected_namespaces(self):
        for system, platform, local, temporary in [
                ('posix', 'linux', 'C:/Users/User/AppData/Local', launcher.TEMP),
                ('nt', 'win32', str(self.root / 'redirected-local'), launcher.TEMP),
                ('nt', 'win32', 'C:/Users/User/AppData/Local', self.root / 'redirected-temp')]:
            with self.subTest(platform=platform, local=local, temporary=temporary), \
                    patch.object(launcher, 'os', SimpleNamespace(name=system, environ={'LOCALAPPDATA': local})), \
                    patch.object(launcher, 'sys', SimpleNamespace(platform=platform)), \
                    patch.object(launcher.tempfile, 'gettempdir', return_value=str(temporary)):
                with self.assertRaises(RuntimeError):
                    launcher.validate_host()

    def test_driver_refuses_redirected_namespace_before_git_or_material_work(self):
        child = SimpleNamespace(check_output=Mock(), CREATE_NO_WINDOW=0)
        with patch.object(driver, 'os', SimpleNamespace(name='nt', environ={'LOCALAPPDATA': str(self.root)})), \
                patch.object(driver, 'sys', SimpleNamespace(platform='win32')), patch.object(driver, 'subprocess', child):
            with self.assertRaisesRegex(AssertionError, 'Redirected LOCALAPPDATA'):
                driver.verify_inputs()
        child.check_output.assert_not_called()

    def test_driver_rejects_head_drift_before_source_or_runtime_admission(self):
        child = SimpleNamespace(check_output=Mock(return_value=b'wrong-head\n'), CREATE_NO_WINDOW=0)
        with patch.object(driver, 'os', SimpleNamespace(name='nt', environ={'LOCALAPPDATA': 'C:/Users/User/AppData/Local'})), \
                patch.object(driver, 'sys', SimpleNamespace(platform='win32')), patch.object(driver, 'subprocess', child), \
                patch.object(driver.tempfile, 'gettempdir', return_value=str(driver.TEMP)):
            with self.assertRaises(AssertionError):
                driver.verify_inputs()
        child.check_output.assert_called_once()
        self.assertEqual(['git', 'rev-parse', 'HEAD'], child.check_output.call_args.args[0])

    def test_driver_rejects_changed_source_preservation_and_retained_inputs(self):
        packet = self.root / 'packet'
        packet.mkdir()
        counterpart = self.root / 'dyd415-rmi-b3-bc-launch-20261031.py'
        counterpart.write_bytes(b'inert counterpart fixture')
        for category, message in [('source', 'B3 source changed'), ('preserved', 'Retained evidence/source changed'),
                                  ('runtime', 'Runtime input changed')]:
            with self.subTest(category=category):
                sentinel = self.root / 'owned.txt'
                sentinel.write_bytes(b'original retained bytes')
                entry = dict(path='owned.txt', sha256=driver.sha(sentinel))
                declaration = dict(driverSha256=driver.sha(Path(driver.__file__)), launcherSha256=driver.sha(counterpart),
                    sources=[entry] if category == 'source' else [])
                self.write_json(packet / 'source-pin.json', declaration)
                self.write_json(packet / 'execution-release.json', dict(groups=['B'], state='RELEASED_B3_BC',
                    sourcePinSha256=driver.sha(packet / 'source-pin.json')))
                self.write_json(packet / 'preserved-inputs.json', [entry] if category == 'preserved' else [])
                self.write_json(packet / 'inputs.json', dict(retainedInputs=[dict(source=str(sentinel), sha256=entry['sha256'])]
                    if category == 'runtime' else []))
                sentinel.write_bytes(b'changed retained bytes')
                child = SimpleNamespace(check_output=Mock(return_value=b'6e3fe7e8018130af6b8586ef0cf40c1474740864\n'), CREATE_NO_WINDOW=0)
                with patch.object(driver, 'ROOT', self.root), patch.object(driver, 'TEMP', self.root), \
                        patch.object(driver, 'PACKET', packet), patch.object(driver, 'GROUP', 'B'), \
                        patch.object(driver, 'os', SimpleNamespace(name='nt', environ={'LOCALAPPDATA': 'C:/Users/User/AppData/Local'})), \
                        patch.object(driver, 'sys', SimpleNamespace(platform='win32')), patch.object(driver, 'subprocess', child), \
                        patch.object(driver.tempfile, 'gettempdir', return_value=str(self.root)):
                    with self.assertRaisesRegex(AssertionError, message):
                        driver.verify_inputs()
                child.check_output.assert_called_once()
                self.assertEqual(b'changed retained bytes', sentinel.read_bytes())

    def test_node_commands_preserve_two_B_files_and_exact_six_case_C_selector(self):
        commands = worker.node_commands('B')
        self.assertEqual([('checker', 16), ('bootstrap', 12)], [(name, count) for name, _, count in commands])
        self.assertEqual(['test/checker.test.ts', 'test/tlc-bootstrap.test.ts'], [argv[-1] for _, argv, _ in commands])
        selected = worker.node_commands('C')
        self.assertEqual(1, len(selected))
        _, argv, count = selected[0]
        self.assertEqual(6, count)
        self.assertEqual('test/corpus.test.ts', argv[-1])
        pattern = next(arg.split('=', 1)[1] for arg in argv if arg.startswith('--test-name-pattern='))
        import re
        for name in worker.C_NAMES:
            self.assertIsNotNone(re.fullmatch(pattern, name))
        self.assertIsNone(re.fullmatch(pattern, 'Login gives the committed chart and report when TLC runs'))
        self.assertEqual(str(worker.NODE), argv[0])

    def test_selected_tap_accepts_only_named_passes_and_filtered_unselected_cases(self):
        names = ['one', 'two']
        tap = 'ok 1 - filtered # SKIP test name does not match pattern\nok 2 - one\nok 3 - two\n'
        self.assertEqual(names, worker.selected_tap(tap, names))

    def test_selected_tap_rejects_missing_skipped_duplicate_failed_and_unselected_results(self):
        for tap in ['', 'ok 1 - one\n', 'ok 1 - one # SKIP unavailable\nok 2 - two\n',
                    'ok 1 - one\nok 2 - one\nok 3 - two\n', 'not ok 1 - one\nok 2 - two\n',
                    'ok 1 - one\nok 2 - two\nok 3 - unexpected\n',
                    'ok 1 - one\nok 2 - two\nok 3 - unexpected # SKIP unrelated reason\n']:
            with self.subTest(tap=tap), self.assertRaises(ValueError):
                worker.selected_tap(tap, ['one', 'two'])

    def test_child_environment_clears_ambient_injection_and_replay_without_mutating_parent(self):
        ambient = dict(PATH='/image/bin', CLASSPATH='foreign', JAVA_TOOL_OPTIONS='foreign', JDK_JAVA_OPTIONS='foreign',
            _JAVA_OPTIONS='foreign', NODE_OPTIONS='foreign', STATECHART_UPDATE='1', STATECHART_REPLAY='foreign',
            STATECHART_PLAIN='foreign', STATECHART_RECORD='foreign', STATECHART_LOG='foreign', JAVA='foreign', TLA2TOOLS='foreign')
        with patch.object(worker, 'os', SimpleNamespace(environ=ambient)):
            result = worker.environment('C')
        self.assertEqual('foreign', ambient['JAVA'])
        for key in ['CLASSPATH', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'STATECHART_UPDATE',
                    'STATECHART_REPLAY', 'STATECHART_PLAIN', 'STATECHART_RECORD', 'STATECHART_LOG']:
            self.assertNotIn(key, result)
        self.assertEqual('/input/tla2tools.jar', result['TLA2TOOLS'])
        self.assertEqual(str(worker.PROOF / 'java_record.py'), result['JAVA'])
        self.assertEqual(str(worker.NODE.parent) + ':/image/bin', result['PATH'])
        self.assertEqual('/work/tmp', result['TMPDIR'])

    def test_B_stops_before_second_command_on_wrong_native_counts_skip_exit_or_stderr(self):
        node = self.root / 'node-data-only'
        node.write_bytes(b'inert fixture, never executed')
        for index, (code, tests, passed, skipped, errors) in enumerate([
                (0, 15, 15, 0, ''), (0, 16, 15, 0, ''), (0, 16, 16, 1, ''),
                (1, 16, 16, 0, ''), (0, 16, 16, 0, 'unexpected stderr')]):
            output = self.root / ('output-' + str(index))
            output.mkdir()
            tap = f'# tests {tests}\n# pass {passed}\n# fail 0\n# skipped {skipped}\n'
            with self.subTest(code=code, tests=tests, passed=passed, skipped=skipped, errors=errors), \
                    patch.object(worker, 'NODE', node), patch.object(worker, 'OUTPUT', output), \
                    patch.object(worker.os, 'access', return_value=True), \
                    patch.object(worker, 'invoke', return_value=(dict(exit=code), tap, errors)) as invoked:
                with self.assertRaises((ValueError, RuntimeError)):
                    worker.group_node('B')
            self.assertEqual(1, invoked.call_count)
            self.assertFalse((output / 'group-B.json').exists())

    def test_C_requires_bootstrap_and_separate_SANY_records_and_rejects_raw_TLC(self):
        node = self.root / 'node-data-only'
        node.write_bytes(b'inert fixture, never executed')
        tap = ''.join(f'ok {index} - {name}\n' for index, name in enumerate(worker.C_NAMES, 1))
        cases = [(['dydo.statechart.LoopbackTlc', 'tla2sany.xml.XMLExporter'], True),
                 (['tlc2.TLC', 'tla2sany.xml.XMLExporter'], False),
                 (['dydo.statechart.LoopbackTlc'], False), ([], False)]
        for index, (mains, accepted) in enumerate(cases):
            output = self.root / ('output-' + str(index))
            output.mkdir()

            def recorded_result(*_arguments):
                for number, main in enumerate(mains):
                    self.write_json(output / 'java-argv' / (str(number) + '.json'), dict(argv=['pinned-java-fixture', main]))
                return dict(exit=0), tap, ''

            with self.subTest(mains=mains, accepted=accepted), patch.object(worker, 'NODE', node), \
                    patch.object(worker, 'OUTPUT', output), patch.object(worker.os, 'access', return_value=True), \
                    patch.object(worker, 'invoke', side_effect=recorded_result) as invoked:
                if accepted:
                    worker.group_node('C')
                    self.assertEqual(2, json.loads((output / 'group-C.json').read_bytes())['javaProcesses'])
                else:
                    with self.assertRaises(ValueError):
                        worker.group_node('C')
                    self.assertFalse((output / 'group-C.json').exists())
            self.assertEqual(1, invoked.call_count)

    def extraction_fixture(self, member, admitted_digest='fixture-pin', admitted_members=None):
        bundle = Mock()
        bundle.__enter__ = Mock(return_value=bundle)
        bundle.__exit__ = Mock(return_value=False)
        bundle.getmembers.return_value = [member]
        metadata = dict(name=member.name, bytes=member.size, mode=member.mode, linkname=member.linkname,
            kind='directory' if member.isdir() else 'file' if member.isfile() else 'symlink' if member.issym() else 'unsupported')
        entries = [metadata] if admitted_members is None else admitted_members
        self.write_json(self.root / 'input/extraction.json', {name: dict(sha256=admitted_digest, members=entries)
            for name in ['jdk.tar.gz', 'site.tar', 'node.tar.gz']})
        return bundle

    def test_extraction_requires_pinned_archive_before_any_extract_call(self):
        member = tarfile.TarInfo('owned/file')
        bundle = self.extraction_fixture(member, admitted_digest='different-pin')
        with patch.object(worker, 'Path', side_effect=self.mapped_path), \
                patch.object(worker.tarfile, 'open', return_value=bundle), patch.object(worker, 'sha', return_value='fixture-pin'):
            with self.assertRaisesRegex(ValueError, 'Archive changed before extraction'):
                worker.extract('B')
        bundle.extractall.assert_not_called()

    def test_extraction_rejects_member_metadata_drift_and_unsafe_matching_members(self):
        for name, kind, mismatch in [('owned/file', tarfile.REGTYPE, True), ('../escape', tarfile.REGTYPE, False),
                                     ('/absolute', tarfile.REGTYPE, False), ('owned/hardlink', tarfile.LNKTYPE, False)]:
            with self.subTest(name=name, mismatch=mismatch):
                member = tarfile.TarInfo(name)
                member.type = kind
                bundle = self.extraction_fixture(member, admitted_members=[] if mismatch else None)
                with patch.object(worker, 'Path', side_effect=self.mapped_path), \
                        patch.object(worker.tarfile, 'open', return_value=bundle), patch.object(worker, 'sha', return_value='fixture-pin'):
                    with self.assertRaises(ValueError):
                        worker.extract('C')
                bundle.extractall.assert_not_called()

    def test_extraction_uses_data_filter_for_all_three_admitted_archives(self):
        bundle = self.extraction_fixture(tarfile.TarInfo('owned/file'))
        with patch.object(worker, 'Path', side_effect=self.mapped_path), \
                patch.object(worker.tarfile, 'open', return_value=bundle) as opened, patch.object(worker, 'sha', return_value='fixture-pin'):
            worker.extract('B')
        self.assertEqual(['/input/jdk.tar.gz', '/input/site.tar', '/input/node.tar.gz'], [call.args[0] for call in opened.call_args_list])
        self.assertEqual(3, bundle.extractall.call_count)
        for call in bundle.extractall.call_args_list:
            self.assertEqual(('/toolchain',), call.args)
            self.assertEqual(dict(filter='data'), call.kwargs)

    def test_snapshot_rejects_changed_bytes_execute_mode_extra_and_missing_before_verifier_import(self):
        repo = self.root / 'toolchain/repo'
        repo.mkdir(parents=True)
        target = repo / 'source.ts'
        for change in ['bytes', 'mode', 'extra', 'missing']:
            with self.subTest(change=change):
                target.write_bytes(b'pinned source')
                target.chmod(0o644)
                extra = repo / 'extra.ts'
                if extra.exists():
                    extra.unlink()
                item = dict(member='repo/source.ts', sha256=worker.sha(target), mode=0o644)
                if change == 'bytes': target.write_bytes(b'changed source')
                if change == 'mode': item['mode'] = 0o755
                if change == 'extra': extra.write_bytes(b'unadmitted source')
                if change == 'missing': target.unlink()
                self.write_json(self.root / 'input/snapshot.json', [item])
                with patch.object(worker, 'REPO', repo), patch.object(worker, 'Path', side_effect=self.mapped_path), \
                        patch.object(worker.importlib.util, 'spec_from_file_location') as imported:
                    with self.assertRaises((ValueError, FileNotFoundError)):
                        worker.verify('B')
                imported.assert_not_called()

    def test_container_rejects_foreign_owner_host_network_and_writable_test_toolchain_before_start(self):
        identity = 'a' * 64
        config = dict(NetworkMode='none', ReadonlyRootfs=True, CapDrop=['ALL'], SecurityOpt=['no-new-privileges'],
            Privileged=False, PidsLimit=128, Memory=1073741824, PidMode='', IpcMode='private', Devices=[], NanoCpus=2000000000, PortBindings={})
        mounts = [dict(Destination=name, RW=name in ['/work', '/output']) for name in ['/input', '/work', '/output', '/toolchain']]
        for change in ['owner', 'network', 'toolchain']:
            with self.subTest(change=change):
                view = dict(Id=identity, Name='/owned-finite', Image=driver.IMAGE.split('@')[1],
                    Config=dict(Labels={'com.dydo.proof-owner': 'foreign' if change == 'owner' else 'ours'}),
                    HostConfig=dict(config, NetworkMode='host' if change == 'network' else 'none'),
                    Mounts=[dict(row, RW=True) if change == 'toolchain' and row['Destination'] == '/toolchain' else row for row in mounts])
                with patch.object(driver, 'run', side_effect=[identity, json.dumps([view])]) as invoked:
                    with self.assertRaises(AssertionError):
                        driver.container('owned-finite', 'ours', ['type=volume,target=/toolchain'], ['-B', '/input/worker.py'], [])
                self.assertEqual(2, invoked.call_count)
                self.assertEqual('create', invoked.call_args_list[0].args[1][1])

    def test_cleanup_refuses_foreign_container_and_never_removes_it(self):
        view = dict(Id='owned-id', Config=dict(Labels={'com.dydo.proof-owner': 'foreign'}))
        with patch.object(driver, 'run', return_value=json.dumps([view])) as invoked:
            with self.assertRaises(AssertionError):
                driver.cleanup([('owned', 'owned-id')], 'volume', 'ours')
        self.assertEqual(1, invoked.call_count)

    def test_cleanup_unknown_identity_is_uncertain_and_not_removed(self):
        with patch.object(driver, 'run', side_effect=['', '']) as invoked, patch.object(driver, 'save') as saved:
            self.assertFalse(driver.cleanup([('owned', None)], 'volume', 'ours'))
        self.assertTrue(any(call.args[0].endswith('-uncertain.json') for call in saved.call_args_list))
        self.assertFalse(any('rm' in call.args[1] for call in invoked.call_args_list))
        self.assertFalse(saved.call_args.args[1]['cleanupConfirmed'])

    def test_cleanup_stops_only_exact_owned_id_and_requires_container_and_volume_absence(self):
        view = dict(Id='exact-id', Config=dict(Labels={'com.dydo.proof-owner': 'ours'}), State=dict(Running=True))
        volume = dict(Name='exact-volume', Labels={'com.dydo.proof-owner': 'ours'})
        outputs = [json.dumps([view]), '', '', '', json.dumps([volume]), 'exact-volume', '']
        with patch.object(driver, 'run', side_effect=outputs) as invoked, patch.object(driver, 'save') as saved:
            self.assertTrue(driver.cleanup([('owned', 'exact-id')], 'exact-volume', 'ours'))
        commands = [call.args[1] for call in invoked.call_args_list]
        self.assertEqual(['docker', 'stop', '--time', '5', 'exact-id'], commands[1])
        self.assertEqual(['docker', 'rm', 'exact-id'], commands[2])
        self.assertIn('id=exact-id', commands[3])
        self.assertEqual(['docker', 'volume', 'rm', 'exact-volume'], commands[5])
        self.assertTrue(saved.call_args.args[1]['cleanupConfirmed'])

    def test_main_rejects_remaining_namespace_process_after_success_without_completion_receipt(self):
        own = dict(pid=123, stat='owned', cmdline='')
        for remaining, accepted in [([own], True), ([own, dict(pid=456, stat='other', cmdline='')], False)]:
            with self.subTest(accepted=accepted), patch.object(sys, 'argv', ['worker.py', '--group', 'C']), \
                    patch.object(worker, 'sys', SimpleNamespace(platform='linux')), patch.object(worker, 'verify') as verified, \
                    patch.object(worker, 'group_node') as group, patch.object(worker, 'process_census', side_effect=[[own], remaining]), \
                    patch.object(worker, 'os', SimpleNamespace(getpid=lambda: 123)), \
                    patch.object(worker.resource, 'setrlimit') as limits, patch.object(worker, 'save') as saved:
                if accepted:
                    worker.main()
                else:
                    with self.assertRaisesRegex(RuntimeError, 'namespace still has another process'):
                        worker.main()
                verified.assert_called_once_with('C')
                group.assert_called_once_with('C')
                limits.assert_called_once_with(worker.resource.RLIMIT_FSIZE, (33554432, 33554432))
                completed = [call for call in saved.call_args_list if call.args[0].name == 'group-completed.json']
                self.assertEqual(1 if accepted else 0, len(completed))


if __name__ == '__main__':
    unittest.main()

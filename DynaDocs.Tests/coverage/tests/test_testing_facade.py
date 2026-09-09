"""Behavioral probes shared by unittest and exact Reqnroll scenario bindings."""
import json
import os
import queue
import shutil
import signal
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest import mock
import runpy
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
RUNNER = ROOT / 'DynaDocs.Tests/coverage/gap_check.py'
PORTABLE = ROOT / 'dydo/reference/gap-check.example.py'
CAPABILITIES = ('test', 'static', 'coverage', 'mutation')


def unavailable(reason='not adopted'):
    return {'state': 'unavailable', 'reason': reason}


def configured(argv, artifacts=None, kind='current-python'):
    return {'state': 'configured', 'command': {'kind': kind, 'argv': argv}, 'artifacts': artifacts or []}


def stack(name):
    caps = {}
    for capability in CAPABILITIES:
        artifact = f'{name}-{capability}.txt'
        code = "import json,sys; from pathlib import Path; " + f"Path('{artifact}').write_text('produced'); " + f"print('{name}:{capability}'); print(json.dumps(sys.argv[1:],ensure_ascii=False))"
        argv = ['-c', code] + (['{base}'] if capability == 'mutation' else [])
        caps[capability] = configured(argv, [{'path': artifact, 'required': True}])
    return {'name': name, 'kind': 'fixture', 'cwd': '.', 'isolation': {'requirement': 'in-place', 'evidence': {'state': 'verified', 'kind': 'direct'}}, 'capabilities': caps}


def manifest(*stacks):
    return {'schema': 1, 'artifactRoot': 'results', 'stacks': list(stacks or [stack('dotnet')])}


class TestingFacadeTests(unittest.TestCase):
    runner = RUNNER

    def fixture(self, data=None, execution_seconds=None, cleanup_seconds=None):
        temporary = tempfile.TemporaryDirectory(prefix='dydo-facade-')
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        shutil.copyfile(self.runner, directory / 'gap_check.py')
        if self.runner == RUNNER:
            helper = ROOT / 'DynaDocs.Tests/coverage/windows_job.py'
            text = helper.read_text(encoding='utf-8')
            if execution_seconds is not None:
                text = text.replace('EXECUTION_SECONDS_MAXIMUM = 1800',
                                    f'EXECUTION_SECONDS_MAXIMUM = {execution_seconds!r}')
            (directory / 'windows_job.py').write_text(text, encoding='utf-8')
            if cleanup_seconds is not None:
                facade = (directory / 'gap_check.py').read_text(encoding='utf-8')
                facade = facade.replace('CLEANUP_SECONDS = 30',
                                        f'CLEANUP_SECONDS = {cleanup_seconds!r}')
                (directory / 'gap_check.py').write_text(facade, encoding='utf-8')
        (directory / 'gap_check.json').write_text(json.dumps(data or manifest()), encoding='utf-8')
        return directory

    def invoke(self, args, data=None, directory=None):
        directory = directory or self.fixture(data)
        process = subprocess.run([sys.executable, '-u', str(directory / 'gap_check.py'), *args], cwd=directory,
                                 capture_output=True, text=True, encoding='utf-8', timeout=60)
        result_paths = [line[8:] for line in process.stdout.splitlines() if line.startswith('Result: ')]
        payload = json.loads(Path(result_paths[0]).read_text(encoding='utf-8')) if result_paths else None
        return process, directory, payload

    def assert_exit(self, process, expected):
        self.assertEqual(expected, process.returncode, process.stdout + process.stderr)

    def assert_rows(self, payload, expected):
        self.assertEqual(expected, [(row['stack'], row['capability'], row['state']) for row in payload['results']])

    def test_help(self):
        p, root, payload = self.invoke(['--help'])
        self.assert_exit(p, 0)
        for phrase in ['test', 'all', 'gate static', 'coverage', 'gate mutation', 'capabilities', '--force-run', '--stack', 'default', 'native', 'artifact', '0', '1', '2', '130', 'cleanup']:
            self.assertIn(phrase, p.stdout)
        self.assertEqual([], list(root.glob('*.txt')))
        self.assertIsNone(payload)

    def test_bare(self):
        p, root, payload = self.invoke([])
        self.assert_exit(p, 2)
        self.assertIn('Usage:', p.stdout)
        self.assertEqual([], list(root.glob('*.txt')))
        self.assertIsNone(payload)

    def test_targeted(self):
        p, root, payload = self.invoke(['test', '--stack', 'dotnet', '--', '--filter', 'FullyQualifiedName~ParserTests'])
        self.assert_exit(p, 0)
        self.assert_rows(payload, [('dotnet', 'test', 'passed')])
        self.assertEqual(['--filter', 'FullyQualifiedName~ParserTests'], payload['results'][0]['argv'][-2:])
        self.assertIn('["--filter", "FullyQualifiedName~ParserTests"]', p.stdout)
        self.assertEqual(['dotnet-test.txt'], [x.name for x in root.glob('*.txt')])
        self.test_targeted_grammar_and_literal_force_flag()

    def test_targeted_grammar_and_literal_force_flag(self):
        for args in [['test'], ['test', '--stack', 'dotnet,peer'], ['all', '--', '--filter'], ['all', '--since', 'BASE'], ['all', '--since', '']]:
            with self.subTest(args=args):
                p, root, payload = self.invoke(args, manifest(stack('dotnet'), stack('peer')))
                self.assert_exit(p, 2)
                self.assertEqual([], list(root.glob('*.txt')))
                self.assertIsNone(payload)
        p, _, payload = self.invoke(['test', '--stack', 'dotnet', '--', '--force-run', '--help', '--', '$literal'])
        self.assert_exit(p, 0)
        self.assertEqual(['--force-run', '--help', '--', '$literal'], payload['results'][0]['argv'][-4:])

    def test_selected_all(self):
        p, root, payload = self.invoke(['all', '--stack', 'python,frontend'], manifest(stack('dotnet'), stack('frontend'), stack('python')))
        self.assert_exit(p, 0)
        self.assert_rows(payload, [('frontend', 'test', 'passed'), ('python', 'test', 'passed')])
        self.assertEqual(['frontend', 'python'], payload['selectedStacks'])
        self.assertEqual({'frontend-test.txt', 'python-test.txt'}, {x.name for x in root.glob('*.txt')})
        self.assertEqual(1, p.stdout.count('frontend:test'))
        self.assertEqual(1, p.stdout.count('python:test'))

    def test_default_all(self):
        good, missing, bad = stack('good'), stack('missing'), stack('bad')
        missing['capabilities']['test'] = unavailable('adopt test')
        bad['capabilities']['test']['command']['argv'] = []
        p, root, payload = self.invoke(['all'], manifest(good, missing, bad))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('good', 'test', 'passed'), ('missing', 'test', 'unavailable'), ('bad', 'test', 'invalid')])
        self.assertEqual(['good-test.txt'], [x.name for x in root.glob('*.txt')])
        self.assertEqual(1, p.stdout.count('good:test'))

    def gate(self, capability):
        args = ['gate', capability, '--stack', 'dotnet'] + (['--since', 'BASE'] if capability == 'mutation' else [])
        p, root, payload = self.invoke(args)
        self.assert_exit(p, 0)
        self.assert_rows(payload, [('dotnet', capability, 'passed')])
        self.assertEqual([f'dotnet-{capability}.txt'], [x.name for x in root.glob('*.txt')])
        self.assert_payload(payload, root)

    def test_gate_static(self): self.gate('static')
    def test_gate_coverage(self): self.gate('coverage')
    def test_gate_mutation(self): self.gate('mutation')

    def test_mutation_base(self):
        base = 'BASE with spaces; $literal & untouched'
        p, _, payload = self.invoke(['gate', 'mutation', '--since', base, '--stack', 'dotnet'])
        self.assert_exit(p, 0)
        self.assertEqual(base, payload['results'][0]['argv'][-1])
        self.assertNotIn('--since', payload['results'][0]['argv'])
        self.assertIn(json.dumps([base]), p.stdout)

    def mutation_invalid(self, tokens):
        bad, peer = stack('dotnet'), stack('peer')
        bad['capabilities']['mutation']['command']['argv'][-1:] = tokens
        p, root, payload = self.invoke(['gate', 'mutation', '--since', 'BASE'], manifest(bad, peer))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('dotnet', 'mutation', 'invalid'), ('peer', 'mutation', 'passed')])
        self.assertFalse((root / 'dotnet-mutation.txt').exists())
        self.assertIn('exactly one argv element equal to {base} and no substring occurrence', payload['results'][0]['reason'])

    def test_mutation_zero(self): self.mutation_invalid([])
    def test_mutation_two(self): self.mutation_invalid(['{base}', '{base}'])
    def test_mutation_substring(self): self.mutation_invalid(['--baseline={base}'])

    def test_capabilities(self):
        data = manifest(stack('dotnet'), stack('python'))
        data['stacks'][1]['capabilities']['coverage'] = unavailable('Pending measurement')
        p, root, payload = self.invoke(['capabilities'], data)
        self.assert_exit(p, 0)
        for name in ['dotnet:', 'python:', 'test: configured', 'static: configured', 'mutation: configured', 'coverage: unavailable: Pending measurement']:
            self.assertIn(name, p.stdout)
        self.assertEqual([], list(root.glob('*.txt')))
        self.assertIsNone(payload)
        self.assertFalse((root / 'results').exists())

    def test_capabilities_reports_malformed_rows_without_executing(self):
        for malformed in [None, [], 4, 'configured']:
            for whole_container in [False, True]:
                with self.subTest(value=malformed, whole_container=whole_container):
                    bad = stack('bad')
                    if whole_container:
                        bad['capabilities'] = malformed
                    else:
                        bad['capabilities']['test'] = malformed
                    p, root, payload = self.invoke(['capabilities'], manifest(bad, stack('peer')))
                    self.assert_exit(p, 2)
                    self.assertIn('invalid', p.stdout)
                    self.assertIn('peer:', p.stdout)
                    self.assertEqual([], list(root.glob('*.txt')))
                    self.assertIsNone(payload)

    def test_capabilities_validates_configuration_without_execution(self):
        cases = [
            (('kind',), ''),
            (('cwd',), '../outside'),
            (('isolation',), {}),
            (('isolation',), {'requirement': 'per-run-artifacts', 'evidence': {'state': 'verified', 'kind': 'adapter', 'path': '../adapter.py'}}),
            (('isolation', 'evidence'), {'state': 'unavailable', 'reason': ''}),
            (('capabilities', 'test', 'reason'), 'forbidden'),
            (('capabilities', 'test', 'command', 'kind'), 'shell'),
            (('capabilities', 'test', 'command', 'argv'), []),
            (('capabilities', 'test', 'command'), {'kind': 'argv', 'argv': ['dydo-no-such-executable-5f71']}),
            (('capabilities', 'test'), {'state': 'unavailable', 'reason': ''}),
            (('capabilities', 'test'), {'state': 'unavailable', 'reason': 'pending', 'command': {'kind': 'current-python', 'argv': ['-c', 'pass']}}),
            (('capabilities', 'test'), {'state': 'unavailable', 'reason': 'pending', 'artifacts': []}),
            (('capabilities', 'coverage', 'artifacts'), [{'path': '../old-report', 'required': True}]),
            (('capabilities', 'mutation', 'command', 'argv'), ['-c', 'pass']),
            (('capabilities', 'mutation', 'command', 'argv'), ['-c', 'pass', '{base}', '{base}']),
            (('capabilities', 'mutation', 'command', 'argv'), ['-c', 'pass', '--base={base}']),
        ]
        for keys, value in cases:
            with self.subTest(keys=keys, value=value):
                bad = stack('bad')
                target = bad
                for key in keys[:-1]:
                    target = target[key]
                target[keys[-1]] = value
                p, root, payload = self.invoke(['capabilities'], manifest(bad, stack('peer')))
                self.assert_exit(p, 2)
                bad_output, peer_output = p.stdout.split('peer:')
                self.assertIn('invalid', bad_output)
                for capability in CAPABILITIES:
                    self.assertIn(f'{capability}: configured', peer_output)
                self.assertIsNone(payload)
                self.assertFalse((root / 'results').exists())
                self.assertEqual([], list(root.glob('*.txt')))

    def test_capabilities_rejects_escaping_result_root_without_execution(self):
        data = manifest()
        data['artifactRoot'] = '../escaped-results'
        p, root, payload = self.invoke(['capabilities'], data)
        self.assert_exit(p, 2)
        self.assertIn('artifactRoot', p.stderr)
        self.assertIsNone(payload)
        self.assertFalse((root / 'results').exists())
        self.assertEqual([], list(root.glob('*.txt')))

    def test_capabilities_rejects_missing_absolute_executable_and_unavailable_masking(self):
        for unavailable_isolation in [False, True]:
            with self.subTest(unavailable_isolation=unavailable_isolation):
                root = self.fixture()
                bad = stack('bad')
                bad['capabilities']['test']['command'] = {'kind': 'argv', 'argv': [str(root / 'missing.exe')]}
                if unavailable_isolation:
                    bad['isolation']['evidence'] = {'state': 'unavailable', 'reason': 'adapter pending'}
                (root / 'gap_check.json').write_text(json.dumps(manifest(bad, stack('peer'))), encoding='utf-8')
                p, _, payload = self.invoke(['capabilities'], directory=root)
                self.assert_exit(p, 2)
                self.assertIn('test: invalid', p.stdout)
                self.assertIn('missing executable', p.stdout)
                self.assertIn('peer:', p.stdout)
                self.assertIsNone(payload)
                self.assertFalse((root / 'results').exists())
                self.assertEqual([], list(root.glob('*.txt')))

    def test_relative_argv_executable_resolves_from_stack_cwd(self):
        root = self.fixture()
        working = root / 'working'
        working.mkdir()
        relative_python = os.path.relpath(sys.executable, working)
        data = manifest()
        data['stacks'][0]['cwd'] = 'working'
        data['stacks'][0]['capabilities']['test'] = configured(
            [relative_python, '-c', "print('RELATIVE_EXECUTABLE_RAN')"], kind='argv')
        (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')

        inspected, _, payload = self.invoke(['capabilities'], directory=root)
        self.assert_exit(inspected, 0)
        self.assertIn('test: configured', inspected.stdout)
        self.assertIsNone(payload)
        self.assertFalse((root / 'results').exists())

        executed, _, payload = self.invoke(['all'], directory=root)
        self.assert_exit(executed, 0)
        self.assertIn('RELATIVE_EXECUTABLE_RAN', executed.stdout)
        self.assert_rows(payload, [('dotnet', 'test', 'passed')])
        self.assertEqual(relative_python, payload['results'][0]['argv'][0])

    def test_dot_relative_executable_and_bare_path_lookup_are_distinct(self):
        root = self.fixture()
        working = root / 'working'
        working.mkdir()
        shell = Path(os.environ['COMSPEC']) if os.name == 'nt' else Path('/bin/sh')
        tool = working / ('facade-tool.exe' if os.name == 'nt' else 'facade-tool')
        shutil.copy2(shell, tool)
        argv = ['./' + tool.name, '/c' if os.name == 'nt' else '-c', 'echo DOT_RELATIVE_RAN']
        data = manifest()
        data['stacks'][0]['cwd'] = 'working'
        data['stacks'][0]['capabilities']['test'] = configured(argv, kind='argv')
        (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
        executed, _, payload = self.invoke(['all'], directory=root)
        self.assert_exit(executed, 0)
        self.assertIn('DOT_RELATIVE_RAN', executed.stdout)
        self.assertEqual(argv, payload['results'][0]['argv'])

        resolver = runpy.run_path(str(self.runner))['resolve_executable']
        # On Windows this is POSIX control-flow evidence, not a native Linux run.
        with mock.patch.object(sys, 'platform', 'linux'), mock.patch('shutil.which') as which:
            which.side_effect = lambda value, path=None: str(tool) if value == str(tool) else None
            self.assertEqual(str(tool), resolver('./' + tool.name, working))
            self.assertIsNone(resolver(tool.name, working))
            self.assertEqual(str(tool), resolver(str(tool), working))
            self.assertEqual([mock.call(str(tool), path=None), mock.call(tool.name, path=None),
                              mock.call(str(tool), path=None)], which.call_args_list)

    def test_unusable_result_destination_starts_no_child(self):
        for destination in ['results', 'results/nested']:
            with self.subTest(destination=destination):
                data = manifest()
                data['artifactRoot'] = destination
                root = self.fixture(data)
                (root / 'results').write_text('unrelated file', encoding='utf-8')
                p, _, payload = self.invoke(['all'], directory=root)
                self.assert_exit(p, 2)
                self.assertIn('artifactRoot', p.stderr)
                self.assertNotIn('Traceback', p.stderr)
                self.assertIsNone(payload)
                self.assertFalse((root / 'dotnet-test.txt').exists())
                self.assertEqual('unrelated file', (root / 'results').read_text())

    def test_result_destination_is_prepared_before_dispatch(self):
        data = manifest()
        data['artifactRoot'] = 'nested/results'
        data['stacks'][0]['capabilities']['test'] = configured(['-c',
            "from pathlib import Path; assert len(list(Path('nested/results').glob('run-*/result.tmp'))) == 1"])
        p, _, payload = self.invoke(['all'], data)
        self.assert_exit(p, 0)
        self.assertEqual('passed', payload['results'][0]['state'])

    def test_result_write_failure_is_a_controlled_exit(self):
        data = manifest()
        data['stacks'][0]['capabilities']['test'] = configured(['-c',
            "from pathlib import Path; root=Path('results'); root.mkdir(exist_ok=True); "
            "paths=list(root.glob('run-*/result.tmp')); "
            "[(p.unlink(), p.mkdir()) for p in paths]; "
            "root.rmdir() if not paths else None; "
            "root.write_text('blocked') if not paths else None; print('CHILD_RAN')"])
        p, _, payload = self.invoke(['all'], data)
        self.assert_exit(p, 2)
        self.assertIn('CHILD_RAN', p.stdout)
        self.assertIn('artifact', p.stderr)
        self.assertNotIn('Traceback', p.stderr)
        self.assertIsNone(payload)

    def test_stale_required_gate_artifact_cannot_pass(self):
        for capability in ['static', 'coverage', 'mutation']:
            for directory_artifact in [False, True]:
                with self.subTest(capability=capability, directory_artifact=directory_artifact):
                    data = manifest(stack('stale'), stack('peer'))
                    argv = ['-c', 'pass'] + (['{base}'] if capability == 'mutation' else [])
                    data['stacks'][0]['capabilities'][capability] = configured(argv, [{'path': 'old-report', 'required': True}])
                    root = self.fixture(data)
                    report = root / 'old-report'
                    if directory_artifact:
                        report.mkdir()
                        report = report / 'evidence.txt'
                    report.write_bytes(b'old candidate evidence')
                    before = report.stat()
                    p, _, payload = self.invoke(['gate', capability] + (['--since', 'BASE'] if capability == 'mutation' else []), directory=root)
                    self.assert_exit(p, 2)
                    self.assert_rows(payload, [('stale', capability, 'invalid'), ('peer', capability, 'passed')])
                    self.assertEqual(0, payload['results'][0]['childExit'])
                    self.assertEqual(2, payload['results'][0]['resultExit'])
                    self.assertEqual(2, payload['aggregateExit'])
                    self.assertEqual(b'old candidate evidence', report.read_bytes())
                    self.assertEqual((before.st_mtime_ns, before.st_ino), (report.stat().st_mtime_ns, report.stat().st_ino))

    def test_new_or_refreshed_required_artifact_passes(self):
        for directory_artifact in [False, True]:
            for change in ['new', 'content', 'timestamp']:
                with self.subTest(directory_artifact=directory_artifact, change=change):
                    target = 'report/nested/evidence.txt' if directory_artifact else 'report'
                    code = f"import os; from pathlib import Path; p=Path({target!r}); p.parent.mkdir(parents=True, exist_ok=True); "
                    if change == 'timestamp':
                        code += 's=p.stat(); os.utime(p, ns=(s.st_atime_ns, s.st_mtime_ns+1000000000))'
                    elif change == 'content':
                        code += "s=p.stat(); p.write_bytes(b'new evidence'); os.utime(p, ns=(s.st_atime_ns, s.st_mtime_ns))"
                    else:
                        code += "p.write_bytes(b'new evidence')"
                    data = manifest()
                    data['stacks'][0]['capabilities']['static'] = configured(['-c', code], [{'path': 'report', 'required': True}])
                    root = self.fixture(data)
                    report = root / target
                    if change != 'new':
                        report.parent.mkdir(parents=True, exist_ok=True)
                        report.write_bytes(b'old evidence')
                    p, _, payload = self.invoke(['gate', 'static'], directory=root)
                    self.assert_exit(p, 0)
                    self.assert_rows(payload, [('dotnet', 'static', 'passed')])
                    self.assertEqual(0, payload['aggregateExit'])
                    self.assertEqual(b'old evidence' if change == 'timestamp' else b'new evidence', report.read_bytes())

    def test_every_required_artifact_must_be_refreshed(self):
        data = manifest()
        data['stacks'][0]['capabilities']['coverage'] = configured(
            ['-c', "from pathlib import Path; Path('new-report').write_text('fresh')"],
            [{'path': 'new-report', 'required': True}, {'path': 'old-report', 'required': True}])
        root = self.fixture(data)
        (root / 'old-report').write_bytes(b'old evidence')
        p, _, payload = self.invoke(['gate', 'coverage'], directory=root)
        self.assert_exit(p, 2)
        self.assertEqual('invalid', payload['results'][0]['state'])
        self.assertEqual(0, payload['results'][0]['childExit'])
        self.assertEqual(b'old evidence', (root / 'old-report').read_bytes())
        self.assertEqual('fresh', (root / 'new-report').read_text())

    def test_non_utf8_manifest_is_globally_invalid(self):
        root = self.fixture()
        (root / 'gap_check.json').write_bytes(b'\xff')
        p, root, payload = self.invoke(['all'], directory=root)
        self.assert_exit(p, 2)
        self.assertIn('invalid JSON manifest', p.stderr)
        self.assertNotIn('Traceback', p.stderr)
        self.assertEqual([], list(root.glob('*.txt')))
        self.assertIsNone(payload)

    def test_nul_command_is_row_invalid_and_valid_peer_runs(self):
        bad = stack('bad')
        bad['capabilities']['test']['command']['argv'].append('\x00')
        p, root, payload = self.invoke(['all'], manifest(bad, stack('peer')))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('bad', 'test', 'invalid'), ('peer', 'test', 'passed')])
        self.assertIsNone(payload['results'][0]['childExit'])
        self.assertEqual(['peer-test.txt'], [x.name for x in root.glob('*.txt')])

    def test_full_g(self):
        first, second = stack('first'), stack('second')
        first['capabilities']['static'] = unavailable('Pending DYD-96')
        second['capabilities']['coverage']['command']['argv'] = []
        p, root, payload = self.invoke(['--force-run'], manifest(first, second))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('first', 'test', 'passed'), ('first', 'static', 'unavailable'), ('first', 'coverage', 'passed'), ('second', 'test', 'passed'), ('second', 'static', 'passed'), ('second', 'coverage', 'invalid')])
        self.assertEqual({'first-test.txt', 'first-coverage.txt', 'second-test.txt', 'second-static.txt'}, {x.name for x in root.glob('*.txt')})

    def test_aggregation(self):
        good, failed, bad, missing = (stack(n) for n in ['good', 'failed', 'bad', 'missing'])
        failed['capabilities']['test'] = configured(['-c', "print('FAIL_RAN'); raise SystemExit(17)"])
        bad['capabilities']['test']['command']['argv'] = []
        missing['capabilities']['test'] = unavailable()
        p, _, payload = self.invoke(['all'], manifest(good, failed, bad, missing))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('good', 'test', 'passed'), ('failed', 'test', 'failed'), ('bad', 'test', 'invalid'), ('missing', 'test', 'unavailable')])
        self.assertEqual([0, 17, None, None], [r['childExit'] for r in payload['results']])
        self.assertEqual([0, 1, 2, 2], [r['resultExit'] for r in payload['results']])
        self.assertEqual(2, payload['aggregateExit'])
        self.assertIn('FAIL_RAN', p.stdout)
        self.assertLess(p.stdout.index('missing test:'), p.stdout.index('Aggregate:'))
        self.assertLess(p.stdout.index('Aggregate:'), p.stdout.index('Result:'))

    def test_force_run_unavailability_outranks_a_raw_test_exit(self):
        data = manifest()
        caps = data['stacks'][0]['capabilities']
        caps['test'] = configured(['-c', 'raise SystemExit(17)'])
        caps['static'] = unavailable('Pending DYD-96')
        caps['coverage'] = unavailable('Pending DYD-96')
        p, _, payload = self.invoke(['--force-run'], data)
        self.assert_exit(p, 2)
        self.assertEqual(2, payload['aggregateExit'])
        self.assertEqual(('failed', 17, 1), tuple(payload['results'][0][k] for k in ('state', 'childExit', 'resultExit')))
        self.assertEqual(('unavailable', 2), tuple(payload['results'][1][k] for k in ('state', 'resultExit')))

    def globally_invalid(self, problem):
        data, args, diagnostic = manifest(), ['all'], ''
        if problem == 'json': diagnostic = 'invalid JSON'
        elif problem == 'schema': data['schema'] = 2; diagnostic = 'schema'
        elif problem == 'field': del data['artifactRoot']; diagnostic = 'artifactRoot'
        elif problem == 'stacks': data['stacks'] = {}; diagnostic = 'stacks'
        elif problem == 'duplicate': data['stacks'].append(stack('dotnet')); diagnostic = 'duplicate'
        elif problem == 'unknown': args = ['all', '--stack', 'missing']; diagnostic = 'unknown selected stack'
        elif problem == 'syntax': args = ['all', '--bad']; diagnostic = 'syntax'
        elif problem == 'base': args = ['gate', 'mutation', '--stack', 'dotnet']; diagnostic = '--since BASE'
        root = self.fixture(data)
        if problem == 'json': (root / 'gap_check.json').write_text('{broken')
        p, root, payload = self.invoke(args, directory=root)
        self.assert_exit(p, 2)
        self.assertEqual([], list(root.glob('*.txt')))
        self.assertIsNone(payload)
        self.assertIn(diagnostic, p.stderr)

    def test_global_json(self): self.globally_invalid('json')
    def test_global_schema(self): self.globally_invalid('schema')
    def test_global_field(self): self.globally_invalid('field')
    def test_global_stacks(self): self.globally_invalid('stacks')
    def test_global_duplicate(self): self.globally_invalid('duplicate')
    def test_global_unknown(self): self.globally_invalid('unknown')
    def test_global_syntax(self): self.globally_invalid('syntax')
    def test_global_base(self): self.globally_invalid('base')

    def exit_case(self, code):
        data = manifest()
        data['stacks'][0]['capabilities']['test'] = unavailable() if code == 2 else configured(['-c', f'raise SystemExit({17 if code == 1 else 0})'])
        p, _, payload = self.invoke(['all'], data)
        self.assert_exit(p, code)
        self.assertEqual(code, payload['aggregateExit'])
        self.assertEqual(17 if code == 1 else (0 if code == 0 else None), payload['results'][0]['childExit'])

    def test_exit_pass(self): self.exit_case(0)
    def test_exit_failure(self): self.exit_case(1)
    def test_exit_unavailable(self): self.exit_case(2)

    def assert_payload(self, payload, root):
        self.assertEqual({'schema', 'candidate', 'operation', 'selectedStacks', 'results', 'aggregateExit'}, set(payload))
        self.assertIs(type(payload['schema']), int)
        self.assertEqual(1, payload['schema'])
        self.assertEqual({'commit', 'dirty'}, set(payload['candidate']))
        self.assertIsInstance(payload['candidate']['dirty'], bool)
        self.assertIn('name', payload['operation'])
        for row in payload['results']:
            fields = {'stack', 'capability', 'state', 'argv', 'cwd', 'isolation',
                      'childExit', 'resultExit', 'artifacts'}
            if self.runner == RUNNER:
                fields.add('environment')
            self.assertEqual(fields, set(row) - {'reason'})
            self.assertIn(row['state'], ['passed', 'failed', 'unavailable', 'invalid', 'interrupted'])
            self.assertIn(row['resultExit'], [0, 1, 2, 130])
            self.assertTrue(row['childExit'] is None or type(row['childExit']) is int)
            self.assertEqual('.', row['cwd'])
            self.assertEqual({'requirement': 'in-place', 'evidence': {'state': 'verified', 'kind': 'direct'}}, row['isolation'])
            for item in row['artifacts']:
                self.assertEqual({'path', 'required'}, set(item))
                self.assertIsInstance(item['required'], bool)
                self.assertTrue((root / item['path']).exists())

    def test_result_artifact(self):
        root = self.fixture()
        subprocess.run(['git', 'init', '-q'], cwd=root, check=True, capture_output=True)
        subprocess.run(['git', '-c', 'user.name=Facade Test', '-c', 'user.email=facade@example.invalid', 'commit', '--allow-empty', '-qm', 'fixture'], cwd=root, check=True, capture_output=True)
        expected = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=root, check=True, capture_output=True, text=True).stdout.strip()
        for expected_exit in [0, 1, 2]:
            data = manifest()
            data['stacks'][0]['capabilities']['test'] = unavailable() if expected_exit == 2 else configured(['-c', f'raise SystemExit({17 if expected_exit == 1 else 0})'])
            (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
            p, _, payload = self.invoke(['all'], directory=root)
            self.assert_exit(p, expected_exit)
            self.assertEqual(expected_exit, payload['aggregateExit'])
            self.assert_payload(payload, root)
            self.assertEqual(expected, payload['candidate']['commit'])
            self.assertTrue(payload['candidate']['dirty'])
            self.assertEqual({'name': 'all'}, payload['operation'])
            self.assertIn('Result: ', p.stdout)
        self.assertEqual(3, len(list(root.glob('results/*/result.json'))))
        self.assertEqual([], list(root.glob('results/**/*.tmp')))
        self.test_interrupting_the_real_dotnet_adapter_cleans_its_worktree()

    def test_schema(self):
        p, root, payload = self.invoke(['all'])
        self.assert_exit(p, 0)
        self.assert_payload(payload, root)
        self.assertEqual(sys.executable, payload['results'][0]['argv'][0])
        data = manifest()
        data['stacks'][0]['capabilities']['test'] = configured([sys.executable, '-c', 'print("DIRECT_ARGV")'], kind='argv')
        p, _, payload = self.invoke(['all'], data)
        self.assert_exit(p, 0)
        self.assertEqual([sys.executable, '-c', 'print("DIRECT_ARGV")'], payload['results'][0]['argv'])
        for schema in [True, 1.0, '1']:
            with self.subTest(schema=schema):
                data['schema'] = schema
                p, _, payload = self.invoke(['all'], data)
                self.assert_exit(p, 2)
                self.assertIsNone(payload)
        for field, value in [('artifactRoot', '../escape'), ('artifactRoot', str(ROOT)), ('stacks', []), ('extra', True)]:
            with self.subTest(field=field, value=value):
                data = manifest(); data[field] = value
                p, _, payload = self.invoke(['all'], data)
                self.assert_exit(p, 2)
                self.assertIsNone(payload)
        self.schema_rows()

    def schema_rows(self):
        defects = [
            ('extra stack field', lambda s: s.update(extra=True)),
            ('extra capability', lambda s: s['capabilities'].update(extra=unavailable())),
            ('configured reason', lambda s: s['capabilities']['test'].update(reason='forbidden')),
            ('unknown command kind', lambda s: s['capabilities']['test']['command'].update(kind='shell')),
            ('nonstring argv', lambda s: s['capabilities']['test']['command'].update(argv=[12])),
            ('nonboolean artifact', lambda s: s['capabilities']['test'].update(artifacts=[{'path': 'x', 'required': 'true'}])),
            ('absolute adapter', lambda s: s.update(isolation={'requirement': 'per-run-artifacts', 'evidence': {'state': 'verified', 'kind': 'adapter', 'path': str(RUNNER)}})),
            ('wrong direct kind', lambda s: s['isolation']['evidence'].update(kind='adapter')),
            ('unknown isolation', lambda s: s['isolation'].update(requirement='magic')),
            ('nonstring isolation', lambda s: s['isolation'].update(requirement=[])),
            ('nonstring command kind', lambda s: s['capabilities']['test']['command'].update(kind=[])),
            ('nonstring capability state', lambda s: s['capabilities']['test'].update(state=[])),
            ('unavailable with command', lambda s: s['capabilities'].update(test={'state': 'unavailable', 'reason': 'missing', 'command': {}})),
        ]
        for name, change in defects:
            with self.subTest(defect=name):
                bad = stack('bad'); change(bad)
                p, root, payload = self.invoke(['all'], manifest(bad, stack('peer')))
                self.assert_exit(p, 2)
                self.assertEqual('invalid', payload['results'][0]['state'])
                self.assertEqual('passed', payload['results'][1]['state'])
                self.assertFalse((root / 'bad-test.txt').exists())
        data = manifest(); data['stacks'][0]['isolation']['evidence'] = {'state': 'unavailable', 'reason': 'adopt isolation'}
        p, root, payload = self.invoke(['all'], data)
        self.assert_exit(p, 2); self.assertEqual('unavailable', payload['results'][0]['state'])
        self.assertEqual('adopt isolation', payload['results'][0]['reason'])
        self.assertEqual([], list(root.glob('*.txt')))
        data = manifest(); data['stacks'][0]['capabilities']['static']['artifacts'] = []
        p, _, payload = self.invoke(['gate', 'static'], data)
        self.assert_exit(p, 2); self.assertIn('required artifact', payload['results'][0]['reason'])
        self.test_required_gate_artifact_is_required_after_successful_child_exit()

    def test_required_gate_artifact_is_required_after_successful_child_exit(self):
        data = manifest()
        data['stacks'][0]['capabilities']['static'] = configured(['-c', 'pass'], [{'path': 'missing.txt', 'required': True}])
        p, _, payload = self.invoke(['gate', 'static'], data)
        self.assert_exit(p, 2)
        self.assertEqual(('invalid', 0, 2), tuple(payload['results'][0][k] for k in ('state', 'childExit', 'resultExit')))

    def row_defect(self, problem):
        bad = stack('bad')
        if problem == 'capability': del bad['capabilities']['coverage']
        elif problem == 'unavailable': bad['capabilities']['test'] = unavailable('missing tests')
        elif problem == 'empty': bad['capabilities']['test']['command']['argv'] = []
        elif problem == 'placeholder': bad['capabilities']['test']['command']['argv'].append('<project-path>')
        elif problem == 'executable': bad['capabilities']['test'] = configured(['dydo-certainly-nonexistent-executable-113'], kind='argv')
        elif problem == 'evidence': bad['isolation']['evidence'] = {'state': 'verified', 'kind': 'not-direct'}
        elif problem == 'escape': bad['cwd'] = '../escape'
        p, root, payload = self.invoke(['all'], manifest(bad, stack('peer')))
        self.assert_exit(p, 2)
        self.assert_rows(payload, [('bad', 'test', 'unavailable' if problem == 'unavailable' else 'invalid'), ('peer', 'test', 'passed')])
        self.assertEqual(['peer-test.txt'], [x.name for x in root.glob('*.txt')])
        if problem == 'escape':
            bad = stack('bad'); bad['capabilities']['test']['artifacts'][0]['path'] = '../escape'
            p, _, payload = self.invoke(['all'], manifest(bad, stack('peer')))
            self.assert_exit(p, 2); self.assertEqual('invalid', payload['results'][0]['state'])
            self.assertEqual('passed', payload['results'][1]['state'])

    def test_row_capability(self): self.row_defect('capability')
    def test_row_unavailable(self): self.row_defect('unavailable')
    def test_row_empty(self): self.row_defect('empty')
    def test_row_placeholder(self): self.row_defect('placeholder')
    def test_row_executable(self): self.row_defect('executable')
    def test_row_evidence(self): self.row_defect('evidence')
    def test_row_escape(self): self.row_defect('escape')

    def portable_data(self):
        return json.loads((ROOT / 'dydo/reference/gap-check.example.json').read_text())

    def test_portable(self):
        data = self.portable_data(); dotnet, frontend, python = data['stacks']
        self.assertEqual(['aspnet', 'react-vite', 'python-uv'], [s['kind'] for s in data['stacks']])
        self.assertEqual(['dotnet', 'test', '<backend-solution-or-project>', '--no-restore'], dotnet['capabilities']['test']['exampleArgv'])
        self.assertEqual(['node', 'node_modules/vitest/vitest.mjs', 'run'], frontend['capabilities']['test']['command']['argv'])
        self.assertEqual(['uv', 'run', '--locked', '--extra', 'dev', '-m', 'pytest'], python['capabilities']['test']['command']['argv'])
        self.assertEqual('git-worktree-copy-working-changes', dotnet['isolation']['requirement'])
        self.assertEqual('unavailable', dotnet['isolation']['evidence']['state'])
        self.assertEqual('per-run-artifacts', frontend['isolation']['requirement'])
        self.assertEqual('in-place', python['isolation']['requirement'])
        self.assertIn('<', frontend['cwd']); self.assertIn('<', python['cwd'])
        self.assertIn('<', frontend['capabilities']['test']['artifacts'][0]['path'])
        for item in data['stacks']:
            for capability in ['static', 'coverage', 'mutation']:
                self.assertEqual('unavailable', item['capabilities'][capability]['state'])
                self.assertTrue(item['capabilities'][capability]['reason'])
        self.assertEqual('results', data['artifactRoot'])
        p, root, payload = self.invoke(['all'], data)
        self.assert_exit(p, 2)
        self.assertEqual(['unavailable', 'invalid', 'invalid'], [r['state'] for r in payload['results']])
        self.assertEqual([None, None, None], [r['childExit'] for r in payload['results']])

    def partial(self, targeted):
        data = self.portable_data()
        data['stacks'][1] = stack('frontend')
        data['stacks'][1]['isolation'] = {'requirement': 'per-run-artifacts', 'evidence': {'state': 'verified', 'kind': 'adapter', 'path': 'gap_check.py'}}
        p, root, payload = self.invoke(['test', '--stack', 'frontend'] if targeted else ['all'], data)
        self.assert_exit(p, 0 if targeted else 2)
        self.assertEqual(['frontend-test.txt'], [x.name for x in root.glob('*.txt')])
        self.assertEqual(['passed'] if targeted else ['unavailable', 'passed', 'invalid'], [r['state'] for r in payload['results']])

    def test_partial_all(self): self.partial(False)
    def test_partial_targeted(self): self.partial(True)

    def test_active_manifest(self):
        data = json.loads((ROOT / 'DynaDocs.Tests/coverage/gap_check.json').read_text())
        self.assertEqual('DynaDocs.Tests/coverage/results', data['artifactRoot'])
        dotnet, python, node = data['stacks']
        self.assertEqual({'kind': 'current-python', 'argv': ['-u', 'DynaDocs.Tests/coverage/run_tests.py', '--']}, dotnet['capabilities']['test']['command'])
        self.assertEqual({'requirement': 'git-worktree-copy-working-changes', 'evidence': {'state': 'verified', 'kind': 'adapter', 'path': 'DynaDocs.Tests/coverage/run_tests.py'}}, dotnet['isolation'])
        self.assertEqual({'kind': 'current-python', 'argv': ['-m', 'unittest', 'discover', '-s', 'DynaDocs.Tests/coverage/tests', '-p', 'test_*.py']}, python['capabilities']['test']['command'])
        self.assertEqual(['node', 'DynaDocs.Tests/coverage/node_tests.cjs'], node['capabilities']['test']['command']['argv'])
        for item in data['stacks']:
            self.assertEqual(unavailable('Pending DYD-103'), item['capabilities']['mutation'])
            for capability in ['static', 'coverage']:
                row = item['capabilities'][capability]
                self.assertEqual('configured', row['state'])
                self.assertEqual('current-python', row['command']['kind'])
                self.assertEqual(['DynaDocs.Tests/coverage/gate_adapter.py', '--stack', item['name'], '--gate', capability], row['command']['argv'])
                self.assertEqual([{'path': f'DynaDocs.Tests/coverage/results/adapters/{item["name"]}-{capability}.json', 'required': True}], row['artifacts'])

    def test_identity(self):
        self.assertEqual(RUNNER.read_bytes(), PORTABLE.read_bytes())
        self.test_targeted(); self.test_global_json(); self.test_selected_all(); self.test_schema(); self.test_aggregation()
        self.test_node()

    def test_node(self):
        p = subprocess.run(['node', '--test', 'DynaDocs.Tests/coverage/tests/testing_facade.test.mjs'], cwd=ROOT, env={**os.environ, 'PYTHON': sys.executable, 'FACADE_RUNNER': str(self.runner)}, capture_output=True, text=True, encoding='utf-8', timeout=60)
        self.assert_exit(p, 0)
        self.assertIn('# pass 1', p.stdout)

    def test_owned_policy_docs_replace_tiers_with_the_dr048_gate_set(self):
        for path in ['dydo/guides/testing-strategy.md', 'dydo/reference/coverage-tools.md', 'Templates/coding-standards.template.md']:
            text = (ROOT / path).read_text(encoding='utf-8')
            for expected in ['HCRAP', '80%', '60%', '20', 'seven', '15 lines', '100 tokens', 'dependency cycles']:
                self.assertIn(expected, text, path)
            for retired in ['T1 | T2 | T3', '@test-tier', '≤ 30']:
                self.assertNotIn(retired, text, path)
            normalized = ' '.join(text.split())
            self.assertTrue('no surviving or uncovered changed-code mutants' in normalized
                            or 'No changed-code mutant may survive or remain uncovered' in normalized, path)
            if path == 'Templates/coding-standards.template.md':
                prohibition = ('There are no tiers, tier annotations, tier registries, classic CRAP '
                               'thresholds, per-file suppressions, or nesting-depth gate.')
                self.assertNotRegex(normalized.replace(prohibition, ''), r'(?i)\btiers?\b', path)

    def test_worktree_collision_preserves_the_foreign_directory(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        with tempfile.TemporaryDirectory(prefix='dydo-allocation-race-') as temporary:
            candidate = Path(temporary) / 'dydo-test-c0111de0'
            harness = (
                "import importlib.util\nfrom pathlib import Path\nfrom types import SimpleNamespace\n"
                f"spec=importlib.util.spec_from_file_location('adapter', {str(adapter)!r})\n"
                "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)\n"
                f"module.tempfile.gettempdir=lambda: {temporary!r}\n"
                "module.uuid.uuid4=lambda: SimpleNamespace(hex='c0111de0')\n"
                "original=module.is_registered_worktree\n"
                "def collide_after_preflight(path):\n"
                "    registered=original(path)\n"
                "    assert not registered and not path.exists()\n"
                "    path.mkdir()\n"
                "    (path/'foreign-marker.txt').write_text('foreign owner', encoding='utf-8')\n"
                "    print('FOREIGN_DIRECTORY_CREATED', flush=True)\n"
                "    return registered\n"
                "module.is_registered_worktree=collide_after_preflight\n"
                "module.main()\n"
            )
            process = subprocess.run([sys.executable, '-u', '-c', harness], cwd=ROOT,
                                     capture_output=True, text=True, encoding='utf-8', timeout=30)
            output = process.stdout + process.stderr
            self.assertEqual(1, process.returncode, output)
            self.assertIn('FOREIGN_DIRECTORY_CREATED', output)
            self.assertNotIn('Traceback', output)
            self.assertTrue(candidate.is_dir(), output)
            self.assertEqual('foreign owner', (candidate / 'foreign-marker.txt').read_text(encoding='utf-8'))
            listing = subprocess.run(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT,
                                     text=True, capture_output=True, check=True).stdout
            self.assertNotIn('worktree ' + candidate.as_posix(), listing)

    def test_dotnet_adapter_closes_inherited_stdin(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        with tempfile.TemporaryDirectory(prefix='dydo-runner-stdin-') as temporary:
            namespace = runpy.run_path(adapter, run_name='run_tests_probe')
            globals_ = namespace['run_tests'].__globals__
            with (mock.patch.dict(globals_, {
                      'is_registered_worktree': mock.Mock(return_value=False),
                      'create_worktree': mock.Mock(return_value=True),
                      'copy_dirty_files': mock.Mock(),
                      'remove_worktree': mock.Mock(),
                  }),
                  mock.patch.object(namespace['tempfile'], 'gettempdir', return_value=temporary),
                  mock.patch.object(namespace['uuid'], 'uuid4',
                                    return_value=type('Uuid', (), {'hex': 'stdin001'})()),
                  mock.patch.object(namespace['subprocess'], 'run') as run):
                run.return_value.returncode = 0

                self.assertEqual(0, namespace['run_tests']())

                dotnet = next(call for call in run.call_args_list if call.args[0][0] == 'dotnet')
                self.assertIs(subprocess.DEVNULL, dotnet.kwargs['stdin'])

    def test_interrupt_at_atomic_directory_acquisition_preserves_ownership(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        for collision in [False, True]:
            for interrupt in ['SIGINT', *(['SIGBREAK'] if os.name == 'nt' else [])]:
                with self.subTest(collision=collision, interrupt=interrupt), tempfile.TemporaryDirectory(prefix='dydo-acquire-signal-') as temporary:
                    candidate = Path(temporary) / 'dydo-test-ac0011ed'
                    harness = (
                        "import importlib.util,signal\nfrom pathlib import Path\nfrom types import SimpleNamespace\n"
                        f"spec=importlib.util.spec_from_file_location('adapter', {str(adapter)!r})\n"
                        "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)\n"
                        f"module.tempfile.gettempdir=lambda: {temporary!r}\n"
                        "module.uuid.uuid4=lambda: SimpleNamespace(hex='ac0011ed')\n"
                        "original=Path.mkdir\n"
                        "class Collision(FileExistsError):\n"
                        "    def __str__(self):\n"
                        f"        signal.raise_signal(signal.{interrupt})\n"
                        "        return 'foreign collision'\n"
                        "def interrupted_mkdir(path, *args, **kwargs):\n"
                        "    original(path, *args, **kwargs)\n"
                        "    print('DIRECTORY_ACQUIRED', flush=True)\n"
                        f"    if {collision!r}:\n"
                        "        (path/'foreign-marker.txt').write_text('foreign owner', encoding='utf-8')\n"
                        "        raise Collision\n"
                        f"    signal.raise_signal(signal.{interrupt})\n"
                        "Path.mkdir=interrupted_mkdir\n"
                        "try: module.main()\n"
                        "except SystemExit:\n"
                        "    assert signal.getsignal(signal.SIGINT) == signal.default_int_handler\n"
                        "    if hasattr(signal, 'SIGBREAK'): assert signal.getsignal(signal.SIGBREAK) == signal.default_int_handler\n"
                        "    print('HANDLERS_RESTORED', flush=True)\n"
                        "    raise\n"
                    )
                    process = subprocess.run([sys.executable, '-u', '-c', harness], cwd=ROOT,
                                             capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=30)
                    output = process.stdout + process.stderr
                    self.assertEqual(130, process.returncode, output)
                    self.assertIn('DIRECTORY_ACQUIRED', output)
                    self.assertIn('HANDLERS_RESTORED', output)
                    self.assertNotIn('Worktree:', output)
                    self.assertNotIn('Traceback', output)
                    self.assertEqual(collision, candidate.exists(), output)
                    if collision:
                        self.assertEqual('foreign owner', (candidate / 'foreign-marker.txt').read_text(encoding='utf-8'))

    def test_interruption_restores_all_handlers_before_delivery(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        harness = r'''
import importlib.util
import itertools
import json
import signal
import sys
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location('adapter', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
native = [signal.SIGINT, *([signal.SIGBREAK] if sys.platform == 'win32' else [])]
original_signal = signal.signal
original_handlers = {sig: signal.getsignal(sig) for sig in native}
observations = []

def prior_int(signum, frame):
    signal.default_int_handler(signum, frame)

def prior_break(signum, frame):
    signal.default_int_handler(signum, frame)

try:
    for platform in dict.fromkeys([sys.platform, 'linux']):
        module.sys = SimpleNamespace(platform=platform)
        installed = native if platform == 'win32' else [signal.SIGINT]
        prior = {sig: prior_int if sig == signal.SIGINT else prior_break for sig in native}
        bodies = [[], *[[sig] for sig in installed], installed * 2, ['KeyboardInterrupt'], ['ValueError']]
        for body, position, phase, injection in itertools.product(
                bodies, installed, ['after', 'before'], [*installed, 'KeyboardInterrupt', None]):
            for sig, handler in prior.items():
                original_signal(sig, handler)
            remaining = 2
            restoration_calls = []

            def restoring_signal(signum, handler):
                global remaining
                restoring = handler is prior[signum]
                if restoring:
                    restoration_calls.append(signal.Signals(signum).name)
                inject = restoring and signum == position and remaining > 0 and injection is not None
                if inject:
                    remaining -= 1
                if phase == 'after' or not inject:
                    result = original_signal(signum, handler)
                if inject:
                    if injection == 'KeyboardInterrupt':
                        raise KeyboardInterrupt
                    signal.raise_signal(injection)
                if phase == 'before' and inject:
                    result = original_signal(signum, handler)
                return result

            signal.signal = restoring_signal
            caught = None
            restored_at_delivery = None
            try:
                with module.defer_interruption():
                    for pending in body:
                        if pending == 'KeyboardInterrupt':
                            raise KeyboardInterrupt
                        if pending == 'ValueError':
                            raise ValueError('body exception')
                        signal.raise_signal(pending)
            except (KeyboardInterrupt, ValueError) as exc:
                caught = type(exc).__name__
                restored_at_delivery = all(signal.getsignal(sig) is handler for sig, handler in prior.items())
            finally:
                signal.signal = original_signal
            restored = all(signal.getsignal(sig) is handler for sig, handler in prior.items())
            subsequent = []
            for sig in installed:
                try:
                    signal.raise_signal(sig)
                except KeyboardInterrupt:
                    subsequent.append(signal.Signals(sig).name)
            row = dict(platform=platform, body=[str(item) for item in body],
                       position=signal.Signals(position).name, phase=phase, injection=str(injection),
                       caught=caught, restored_at_delivery=restored_at_delivery,
                       restored=restored, subsequent=subsequent, restoration_calls=restoration_calls)
            observations.append(row)
            print(json.dumps(row), flush=True)
            expected = 'KeyboardInterrupt' if injection is not None or body else None
            if injection is None and body == ['ValueError']:
                expected = 'ValueError'
            assert caught == expected, row
            assert restored, row
            assert restored_at_delivery if caught else restored_at_delivery is None, row
            assert subsequent == [signal.Signals(sig).name for sig in installed], row
finally:
    signal.signal = original_signal
    for sig, handler in original_handlers.items():
        original_signal(sig, handler)
print('RESTORATION_MATRIX_CASES=' + str(len(observations)))
'''
        process = subprocess.run([sys.executable, '-u', '-c', harness, str(adapter)], cwd=ROOT,
                                 capture_output=True, text=True, encoding='utf-8', timeout=30)
        output = process.stdout + process.stderr
        self.assertEqual(0, process.returncode, output)
        self.assertIn('RESTORATION_MATRIX_CASES=126' if os.name == 'nt' else 'RESTORATION_MATRIX_CASES=30', output)

    def test_interrupt_between_handler_restoration_iterations(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        harness = r'''
import importlib.util
import inspect
import itertools
import json
import signal
import sys
from types import SimpleNamespace

spec = importlib.util.spec_from_file_location('adapter', sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
function = module.defer_interruption.__wrapped__
lines, first = inspect.getsourcelines(function)
loop_line = first + next(i for i, line in enumerate(lines) if 'for signum, handler in previous.items():' in line)
native = [signal.SIGINT, *([signal.SIGBREAK] if sys.platform == 'win32' else [])]
original = {sig: signal.getsignal(sig) for sig in native}
prior = {sig: (lambda signum, frame: signal.default_int_handler(signum, frame)) for sig in native}
count = 0
try:
    for platform, body, injection in itertools.product(
            dict.fromkeys([sys.platform, 'linux']), [None, 'signal', 'ValueError'], ['signal', 'KeyboardInterrupt', None]):
        module.sys = SimpleNamespace(platform=platform)
        for sig, handler in prior.items():
            signal.signal(sig, handler)
        injected = False
        def trace(frame, event, arg):
            global injected
            # The system-call wrapper cannot observe this loop backedge.
            if (frame.f_code is function.__code__ and event == 'line' and frame.f_lineno == loop_line
                    and signal.getsignal(signal.SIGINT) is prior[signal.SIGINT]
                    and injection is not None and not injected):
                injected = True
                if injection == 'KeyboardInterrupt':
                    raise KeyboardInterrupt
                signal.raise_signal(signal.SIGINT)
            return trace
        caught = None
        restored_at_delivery = None
        try:
            with module.defer_interruption():
                sys.settrace(trace)
                if body == 'signal':
                    signal.raise_signal(signal.SIGINT)
                elif body == 'ValueError':
                    raise ValueError('body exception')
        except (KeyboardInterrupt, ValueError) as exc:
            caught = type(exc).__name__
            restored_at_delivery = all(signal.getsignal(sig) is handler for sig, handler in prior.items())
        finally:
            sys.settrace(None)
        restored = all(signal.getsignal(sig) is handler for sig, handler in prior.items())
        subsequent = []
        for sig in native:
            try:
                signal.raise_signal(sig)
            except KeyboardInterrupt:
                subsequent.append(sig)
        row = dict(platform=platform, body=body, injection=injection, injected=injected,
                   caught=caught, restored=restored, restored_at_delivery=restored_at_delivery,
                   subsequent=subsequent)
        print(json.dumps(row), flush=True)
        expected = 'KeyboardInterrupt' if injection is not None or body == 'signal' else body
        assert injected == (injection is not None), row
        assert caught == expected, row
        assert restored and (restored_at_delivery if caught else restored_at_delivery is None), row
        assert subsequent == native, row
        count += 1
finally:
    sys.settrace(None)
    for sig, handler in original.items():
        signal.signal(sig, handler)
print('INTER_ITERATION_CASES=' + str(count))
'''
        process = subprocess.run([sys.executable, '-u', '-c', harness, str(adapter)], cwd=ROOT,
                                 capture_output=True, text=True, encoding='utf-8', timeout=30)
        output = process.stdout + process.stderr
        self.assertEqual(0, process.returncode, output)
        self.assertIn('INTER_ITERATION_CASES=18' if os.name == 'nt' else 'INTER_ITERATION_CASES=9', output)

    def test_exact_worktree_registration_and_failed_removal_cleanup(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        for failed_remove in [False, True]:
            with self.subTest(failed_remove=failed_remove), tempfile.TemporaryDirectory(prefix='dydo-registration-audit-') as temporary:
                candidate = Path(temporary) / 'owned-\u00e1-path'
                harness = (
                    "import importlib.util\nfrom pathlib import Path\nfrom types import SimpleNamespace\n"
                    f"spec=importlib.util.spec_from_file_location('adapter', {str(adapter)!r})\n"
                    "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)\n"
                    f"path=Path({str(candidate)!r}); path.mkdir()\n"
                    "assert module.create_worktree(path)\n"
                    "original=module._git\n"
                    "try:\n"
                    "    assert module.is_registered_worktree(path), 'exact non-ASCII registration not recognized'\n"
                    "    if module.sys.platform == 'win32': assert module.is_registered_worktree(Path(str(path).upper()))\n"
                    "    assert not module.is_registered_worktree(Path(str(path)+'-sibling'))\n"
                    f"    if {failed_remove!r}:\n"
                    "        def fail_first_remove(*args, capture=False):\n"
                    "            if args[:3] == ('worktree', 'remove', '--force'):\n"
                    "                module._git=original\n"
                    "                print('FIRST_GIT_REMOVE_FAILED', flush=True)\n"
                    "                return SimpleNamespace(returncode=1)\n"
                    "            return original(*args, capture=capture)\n"
                    "        module._git=fail_first_remove\n"
                    "    module.remove_worktree(path)\n"
                    "    assert not path.exists(), 'owned directory survived cleanup'\n"
                    "    assert not module.is_registered_worktree(path), 'owned registration survived fallback cleanup'\n"
                    "finally:\n"
                    "    original('worktree', 'remove', '--force', str(path))\n"
                )
                process = subprocess.run([sys.executable, '-u', '-c', harness], cwd=ROOT,
                                         capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=30)
                self.assertEqual(0, process.returncode, process.stdout + process.stderr)
                self.assertFalse(candidate.exists())

    def test_failed_git_add_cleans_only_the_acquired_empty_directory(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        with tempfile.TemporaryDirectory(prefix='dydo-allocation-failure-') as temporary:
            candidate = Path(temporary) / 'dydo-test-fa11ed00'
            harness = (
                "import importlib.util\nfrom pathlib import Path\nfrom types import SimpleNamespace\n"
                f"spec=importlib.util.spec_from_file_location('adapter', {str(adapter)!r})\n"
                "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)\n"
                f"module.tempfile.gettempdir=lambda: {temporary!r}\n"
                "module.uuid.uuid4=lambda: SimpleNamespace(hex='fa11ed00')\n"
                "original=module._git\n"
                "def failed_git(*args, capture=False):\n"
                "    if args[:3] == ('worktree', 'add', '--detach'):\n"
                "        path=Path(args[3])\n"
                "        assert path.is_dir() and not list(path.iterdir())\n"
                "        print('OWNED_EMPTY_DIRECTORY', flush=True)\n"
                "        return SimpleNamespace(returncode=1)\n"
                "    return original(*args, capture=capture)\n"
                "module._git=failed_git\n"
                "module.main()\n"
            )
            process = subprocess.run([sys.executable, '-u', '-c', harness], cwd=ROOT,
                                     capture_output=True, text=True, encoding='utf-8', timeout=30)
            output = process.stdout + process.stderr
            self.assertEqual(1, process.returncode, output)
            self.assertIn('OWNED_EMPTY_DIRECTORY', output)
            self.assertNotIn('Traceback', output)
            self.assertFalse(candidate.exists(), output)
            listing = subprocess.run(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT,
                                     text=True, capture_output=True, check=True).stdout
            self.assertNotIn('worktree ' + candidate.as_posix(), listing)

    def test_interrupting_the_real_dotnet_adapter_cleans_its_worktree(self):
        command = [sys.executable, '-u', str(RUNNER), 'test', '--stack', 'dotnet', '--', '--filter', 'FullyQualifiedName~ConsoleCaptureTests.Stderr_RestoresConsoleError_WhenActionSucceeds']
        facade = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                  text=True, encoding='utf-8', errors='replace',
                                  creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0,
                                  start_new_session=os.name != 'nt')
        lines, output = queue.Queue(), []
        def collect():
            for line in facade.stdout:
                output.append(line); lines.put(line)
        reader = threading.Thread(target=collect, daemon=True); reader.start()
        worktree, deadline = None, time.monotonic() + 60
        try:
            while time.monotonic() < deadline and worktree is None:
                try: line = lines.get(timeout=1).strip()
                except queue.Empty: continue
                if line.startswith('Worktree: '): worktree = Path(line[10:])
            self.assertIsNotNone(worktree, ''.join(output))
            listing = subprocess.run(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT, text=True, capture_output=True, check=True).stdout
            self.assertIn('worktree ' + worktree.as_posix(), listing)
            started = time.monotonic()
            facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == 'nt' else signal.SIGINT)
            facade.wait(timeout=35); reader.join(timeout=5)
            self.assertEqual(130, facade.returncode, ''.join(output))
            self.assertLess(time.monotonic() - started, 35)
            self.assertFalse(worktree.exists(), ''.join(output))
            after = subprocess.run(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT, text=True, capture_output=True, check=True).stdout
            self.assertNotIn('worktree ' + worktree.as_posix(), after)
            result_line = next(x.strip()[8:] for x in output if x.startswith('Result: '))
            payload = json.loads(Path(result_line).read_text(encoding='utf-8'))
            self.assertEqual(130, payload['aggregateExit'])
            row = payload['results'][0]
            self.assertEqual(('interrupted', 130), (row['state'], row['resultExit']))
            self.assertEqual(130, row['childExit'])
            self.assertLess(next(i for i,x in enumerate(output) if 'Cleaning up worktree' in x), next(i for i,x in enumerate(output) if x.startswith('Result: ')))
        finally:
            if facade.poll() is None:
                facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == 'nt' else signal.SIGINT)
                facade.wait(timeout=35)
            reader.join(timeout=5)
            facade.stdout.close()

    def test_interrupt_during_real_worktree_registration_cleans_the_attributed_path(self):
        adapter = ROOT / 'DynaDocs.Tests/coverage/run_tests.py'
        for registration_only in [False, True]:
            with self.subTest(registration_only=registration_only):
                harness = (
                    "import importlib.util,shutil,sys\nfrom pathlib import Path\n"
                    f"spec=importlib.util.spec_from_file_location('run_tests_probe', {str(adapter)!r})\n"
                    "module=importlib.util.module_from_spec(spec); spec.loader.exec_module(module)\n"
                    "original=module._git\n"
                    "def delayed_git(*args, capture=False):\n"
                    "    if args[:3] == ('worktree', 'add', '--detach'):\n"
                    "        assert Path(args[3]).is_dir() and not list(Path(args[3]).iterdir())\n"
                    "    result=original(*args, capture=capture)\n"
                    "    if args[:3] == ('worktree', 'add', '--detach') and result.returncode == 0:\n"
                    "        listing,rc=original('worktree', 'list', '--porcelain', capture=True)\n"
                    "        assert rc == 0 and ('worktree ' + args[3].replace('\\\\', '/')) in listing.splitlines()\n"
                    f"        if {registration_only!r}: shutil.rmtree(args[3])\n"
                    "        print('REGISTERED: ' + args[3], flush=True)\n"
                    "        raise KeyboardInterrupt\n"
                    "    return result\n"
                    "module._git=delayed_git\n"
                    "module.main()\n"
                )
                process = subprocess.run([sys.executable, '-u', '-c', harness], cwd=ROOT,
                                         capture_output=True, text=True, encoding='utf-8',
                                         errors='replace', timeout=30)
                output = process.stdout + process.stderr
                registered = next((line[12:] for line in process.stdout.splitlines()
                                   if line.startswith('REGISTERED: ')), None)
                attributed = Path(registered) if registered else None
                try:
                    self.assertIsNotNone(attributed, output)
                    self.assertFalse(any(line.strip().startswith('Worktree: ')
                                         for line in process.stdout.splitlines()), output)
                    self.assertEqual(130, process.returncode, output)
                    self.assertFalse(attributed.exists(), output)
                    listing = subprocess.run(['git', 'worktree', 'list', '--porcelain'], cwd=ROOT,
                                             text=True, capture_output=True, check=True).stdout
                    self.assertNotIn('worktree ' + attributed.as_posix(), listing)
                finally:
                    if attributed is not None:
                        subprocess.run(['git', 'worktree', 'remove', '--force', str(attributed)], cwd=ROOT,
                                       capture_output=True, text=True)
                        if attributed.exists(): shutil.rmtree(attributed)

    def test_interrupt_stops_later_capabilities_and_stacks(self):
        for operation, block_result in [('all', False), ('--force-run', False), ('all', True)]:
            with self.subTest(operation=operation, block_result=block_result):
                data = manifest(stack('first'), stack('later'))
                root = self.fixture(data)
                (root / 'wait.py').write_text(
                    "import signal,sys,time\nfrom pathlib import Path\n"
                    "def interrupted(signum, frame):\n    raise KeyboardInterrupt\n"
                    "signal.signal(signal.SIGBREAK if sys.platform == 'win32' else signal.SIGINT, interrupted)\n"
                    "try:\n    print('WAITING_FOR_INTERRUPT', flush=True)\n    deadline=time.monotonic()+60\n"
                    "    while time.monotonic()<deadline:\n        time.sleep(0.05)\n"
                    "except KeyboardInterrupt:\n    print('ADAPTER_INTERRUPTED', flush=True)\n    sys.exit(130)\n"
                    "finally:\n    Path('cleanup.txt').write_text('complete')\n"
                    + ("    for p in Path('results').glob('run-*/result.tmp'):\n        p.unlink()\n        p.mkdir()\n"
                       if block_result else ''), encoding='utf-8')
                data['stacks'][0]['capabilities']['test'] = configured(['-u', 'wait.py'])
                (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
                facade = subprocess.Popen([sys.executable, '-u', str(root / 'gap_check.py'), operation],
                    cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8',
                    creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0,
                    start_new_session=os.name != 'nt')
                lines, output = queue.Queue(), []
                def collect():
                    for line in facade.stdout:
                        output.append(line); lines.put(line)
                reader = threading.Thread(target=collect, daemon=True); reader.start()
                try:
                    self.assertEqual('WAITING_FOR_INTERRUPT', lines.get(timeout=15).strip())
                    interrupted_at = time.monotonic()
                    facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == 'nt' else signal.SIGINT)
                    facade.wait(timeout=35); reader.join(timeout=5)
                    self.assertLess(time.monotonic() - interrupted_at, 10)
                    self.assertEqual(130, facade.returncode, ''.join(output))
                    self.assertIn('ADAPTER_INTERRUPTED', ''.join(output))
                    self.assertEqual('complete', (root / 'cleanup.txt').read_text())
                    self.assertEqual(['cleanup.txt'], [p.name for p in root.glob('*.txt')])
                    if block_result:
                        self.assertFalse(any(line.startswith('Result: ') for line in output))
                        self.assertIn('artifact destination', ''.join(output))
                        self.assertNotIn('Traceback', ''.join(output))
                        continue
                    result_path = next(line.strip()[8:] for line in output if line.startswith('Result: '))
                    payload = json.loads(Path(result_path).read_text(encoding='utf-8'))
                    self.assert_rows(payload, [('first', 'test', 'interrupted')])
                    self.assertEqual(130, payload['aggregateExit'])
                    self.assertEqual(['first', 'later'], payload['selectedStacks'])
                finally:
                    if facade.poll() is None:
                        facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == 'nt' else signal.SIGINT)
                        facade.wait(timeout=35)
                    reader.join(timeout=5)
                    facade.stdout.close()

    def test_absolute_row_deadline_cleans_cooperative_child_before_publication(self):
        if self.runner != RUNNER:
            self.skipTest('project deadline policy is not part of the portable example')
        data = manifest(stack('first'), stack('later'))
        root = self.fixture(data, execution_seconds=.15, cleanup_seconds=.25)
        (root / 'deadline.py').write_text(
            "import signal,sys,time\nfrom pathlib import Path\n"
            "def stop(signum, frame):\n    Path('cleanup.txt').write_text('complete')\n    raise SystemExit(130)\n"
            "signal.signal(signal.SIGBREAK if sys.platform == 'win32' else signal.SIGINT, stop)\n"
            "deadline=time.monotonic()+1\n"
            "while time.monotonic()<deadline:\n    print('wake', flush=True)\n    time.sleep(.005)\n"
            "raise SystemExit(99)\n", encoding='utf-8')
        data['stacks'][0]['capabilities']['test'] = configured(['-u', 'deadline.py'])
        (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
        process, _, payload = self.invoke(['all'], directory=root)
        self.assert_exit(process, 130)
        self.assertEqual('complete', (root / 'cleanup.txt').read_text(encoding='utf-8'))
        self.assertFalse((root / 'later-test.txt').exists())
        self.assert_rows(payload, [('first', 'test', 'interrupted')])
        row = payload['results'][0]
        self.assertEqual(130, row['childExit'])
        self.assertIn('DYDO_ROW_DEADLINE', row['environment'])
        self.assertGreater(process.stdout.count('wake'), 5)

    def test_absolute_row_deadline_force_terminates_uncooperative_child(self):
        if self.runner != RUNNER:
            self.skipTest('project deadline policy is not part of the portable example')
        data = manifest(stack('first'), stack('later'))
        root = self.fixture(data, execution_seconds=.1, cleanup_seconds=.15)
        (root / 'deadline.py').write_text(
            "import signal,time\n"
            "signal.signal(signal.SIGINT, signal.SIG_IGN)\n"
            + ("signal.signal(signal.SIGBREAK, signal.SIG_IGN)\n" if os.name == 'nt' else '')
            + "deadline=time.monotonic()+1\n"
            "while time.monotonic()<deadline:\n    time.sleep(.005)\n"
            "raise SystemExit(99)\n", encoding='utf-8')
        data['stacks'][0]['capabilities']['test'] = configured(['-u', 'deadline.py'])
        (root / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
        started = time.monotonic()
        process, _, payload = self.invoke(['all'], directory=root)
        self.assertLess(time.monotonic() - started, 2)
        self.assert_exit(process, 130)
        self.assertFalse((root / 'later-test.txt').exists())
        self.assert_rows(payload, [('first', 'test', 'interrupted')])
        self.assertIn('deadline', payload['results'][0]['reason'])


class PortableTestingFacadeTests(TestingFacadeTests):
    runner = PORTABLE


if __name__ == '__main__':
    unittest.main()

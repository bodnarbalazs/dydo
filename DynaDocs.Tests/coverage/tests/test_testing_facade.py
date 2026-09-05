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

    def fixture(self, data=None):
        temporary = tempfile.TemporaryDirectory(prefix='dydo-facade-')
        self.addCleanup(temporary.cleanup)
        directory = Path(temporary.name)
        shutil.copyfile(self.runner, directory / 'gap_check.py')
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
            self.assertEqual({'stack', 'capability', 'state', 'argv', 'cwd', 'isolation', 'childExit', 'resultExit', 'artifacts'}, set(row) - {'reason'})
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
        data['artifactRoot'] = 'results'
        p, root, payload = self.invoke(['all'], data)
        self.assert_exit(p, 2)
        self.assertEqual(['unavailable', 'invalid', 'invalid'], [r['state'] for r in payload['results']])
        self.assertEqual([None, None, None], [r['childExit'] for r in payload['results']])

    def partial(self, targeted):
        data = self.portable_data(); data['artifactRoot'] = 'results'
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
        self.assertEqual(['node', '--test', 'DynaDocs.Tests/coverage/tests/testing_facade.test.mjs'], node['capabilities']['test']['command']['argv'])
        for item in data['stacks']:
            for capability in ['static', 'coverage', 'mutation']:
                self.assertEqual(unavailable('Pending DYD-103' if capability == 'mutation' else 'Pending DYD-96'), item['capabilities'][capability])

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

    def test_interrupting_the_real_dotnet_adapter_cleans_its_worktree(self):
        command = [sys.executable, '-u', str(RUNNER), 'test', '--stack', 'dotnet', '--', '--filter', 'FullyQualifiedName~ConsoleCaptureTests.Stderr_RestoresConsoleError_WhenActionSucceeds']
        facade = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding='utf-8', creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0, start_new_session=os.name != 'nt')
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
            self.assertTrue(row['childExit'] is None or type(row['childExit']) is int)
            self.assertLess(next(i for i,x in enumerate(output) if 'Cleaning up worktree' in x), next(i for i,x in enumerate(output) if x.startswith('Result: ')))
        finally:
            if facade.poll() is None:
                facade.send_signal(signal.CTRL_BREAK_EVENT if os.name == 'nt' else signal.SIGINT)
                facade.wait(timeout=35)
            reader.join(timeout=5)
            facade.stdout.close()


class PortableTestingFacadeTests(TestingFacadeTests):
    runner = PORTABLE


if __name__ == '__main__':
    unittest.main()

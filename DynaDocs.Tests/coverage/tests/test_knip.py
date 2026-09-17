"""Package containment and native counts must account for every maintained source."""
import copy
import sys
import tempfile
import unittest
import json
import subprocess
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_knip import ISSUES, PACKAGE_ROOTS, collect_knip, workspace_model, normalize_report
from gate_collect import Collectors
from gate_run import CommandLog
from inventory import git_file_state, language_of


class KnipAccountingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for folder in PACKAGE_ROOTS:
            package = self.root / folder / 'package.json'
            package.parent.mkdir(parents=True, exist_ok=True)
            package.write_text('{}', encoding='utf-8')
        self.paths = ['DynaDocs.Tests/coverage/tests/example.test.cjs', 'npm/lib/example.js']

    def test_same_stem_sources_join_distinct_real_containing_packages(self):
        model = workspace_model(self.root, self.paths)
        self.assertEqual([], model['errors'])
        self.assertEqual(['tests/example.test.cjs'], model['config']['workspaces']['.']['project'])
        self.assertEqual(['lib/example.js'], model['config']['workspaces']['../../npm']['project'])
        self.assertEqual(self.paths, [row['path'] for row in model['sources']])

    def test_missing_package_outside_root_duplicate_and_unsupported_extension_fail_closed(self):
        for extra in ['npm/bin/dydo', '../escape.cjs', self.paths[0]]:
            with self.subTest(extra=extra):
                self.assertTrue(workspace_model(self.root, self.paths + [extra])['errors'])
        for folder in ('npm', '.'):
            with self.subTest(folder=folder):
                (self.root / folder / 'package.json').unlink()
                self.assertEqual([{'path': folder, 'message': 'Missing actual package manifest'}],
                                 workspace_model(self.root, self.paths)['errors'])
                (self.root / folder / 'package.json').write_text('{}', encoding='utf-8')

    def test_nested_declared_roots_give_every_source_its_deepest_owner_alone(self):
        paths = ['DynaDocs.Tests/HostCanaries/path-containment.mjs',
                 'DynaDocs.Tests/coverage/tests/example.test.cjs', 'setup.mjs']
        model = workspace_model(self.root, paths)
        self.assertEqual([], model['errors'])
        self.assertEqual(paths, [row['path'] for row in model['sources']])
        workspaces = model['config']['workspaces']
        self.assertEqual(['setup.mjs'], workspaces['../..']['project'])
        self.assertEqual(['path-containment.mjs'], workspaces['../HostCanaries']['project'])
        self.assertEqual(['tests/example.test.cjs'], workspaces['.']['project'])

    def test_host_canary_sources_join_their_own_declared_package(self):
        path = 'DynaDocs.Tests/HostCanaries/path-containment.mjs'
        model = workspace_model(self.root, self.paths + [path])
        self.assertEqual([], model['errors'])
        self.assertEqual(['DynaDocs.Tests/HostCanaries'],
                         [row['workspace'] for row in model['sources'] if row['path'] == path])
        self.assertEqual(['path-containment.mjs'], model['config']['workspaces']['../HostCanaries']['project'])
        (self.root / 'DynaDocs.Tests/HostCanaries/package.json').unlink()
        self.assertEqual([{'path': 'DynaDocs.Tests/HostCanaries', 'message': 'Missing actual package manifest'}],
                         workspace_model(self.root, self.paths + [path])['errors'])

    def test_spawned_and_command_line_host_entries_stay_reachable_roots(self):
        paths = self.paths + ['DynaDocs.Tests/HostCanaries/' + name
                              for name in ('run-host-canaries.mjs', 'openai-sse-provider.mjs', 'path-containment.mjs')]
        workspace = workspace_model(self.root, paths)['config']['workspaces']['../HostCanaries']
        self.assertEqual(['openai-sse-provider.mjs', 'path-containment.mjs', 'run-host-canaries.mjs'],
                         workspace['project'])
        self.assertEqual(['openai-sse-provider.mjs', 'run-host-canaries.mjs'], workspace['entry'])

    def native(self):
        return [{'issues': [{'file': '../../npm/lib/example.js', 'exports': [{'name': 'dead', 'line': 3}],
                            'files': [], 'nsExports': [], 'duplicates': [], 'unresolved': []}]},
                {'kind': 'measurement', 'includedWorkspaceDirs': [str(self.root / path) for path in PACKAGE_ROOTS],
                 'counters': {'total': 2, 'processed': 2, 'files': 0, 'exports': 1, 'nsExports': 0, 'duplicates': 0, 'unresolved': 0}}]

    def test_native_issue_retains_exact_source_and_symbol(self):
        answer = normalize_report(self.root, workspace_model(self.root, self.paths), self.native())
        self.assertEqual([], answer['errors'])
        self.assertEqual('npm/lib/example.js', answer['findings'][0]['path'])
        self.assertEqual({'name': 'dead', 'line': 3}, answer['findings'][0]['diagnostic'])

    def test_missing_duplicate_foreign_and_counter_mismatch_are_measurement_errors(self):
        original = self.native()
        for mutation in ('missing', 'duplicate', 'foreign', 'count', 'workspace', 'issue_count'):
            native = copy.deepcopy(original)
            if mutation == 'missing': native.pop()
            elif mutation == 'duplicate': native[0]['issues'].append(native[0]['issues'][0])
            elif mutation == 'foreign': native[0]['issues'][0]['file'] = 'foreign.cjs'
            elif mutation == 'count': native[1]['counters']['total'] = 1
            elif mutation == 'workspace': native[1]['includedWorkspaceDirs'].pop()
            else: native[1]['counters']['exports'] = 0
            with self.subTest(mutation=mutation):
                self.assertTrue(normalize_report(self.root, workspace_model(self.root, self.paths), native)['errors'])

    def native_report(self, model):
        tooling = Path(__file__).resolve().parents[1]
        config = self.root / 'knip.json'
        config.write_text(json.dumps(model['config']), encoding='utf-8')
        native = subprocess.run(['node', str(tooling / 'node_modules/knip/bin/knip.js'), '--config', str(config),
                                 '--reporter', str(tooling / 'knip_reporter.mjs'), '--no-gitignore',
                                 '--include-entry-exports', '--include', ','.join(ISSUES)],
                                cwd=self.root / 'DynaDocs.Tests/coverage',
                                capture_output=True, text=True, encoding='utf-8', timeout=60)
        self.assertEqual(1, native.returncode, native.stderr)
        report = normalize_report(self.root, model, [json.loads(line) for line in native.stdout.splitlines()])
        self.assertEqual([], report['errors'])
        return report

    def test_native_single_graph_preserves_cross_package_test_only_consumer(self):
        paths = []
        for folder in ('DynaDocs.Tests/coverage', 'npm'):
            package = self.root / folder
            (package / 'lib.cjs').write_text('exports.used = () => 1; exports.dead = () => 2;', encoding='utf-8')
            peer = '../../npm/lib.cjs' if folder.startswith('DynaDocs') else '../DynaDocs.Tests/coverage/lib.cjs'
            (package / 'cross.test.cjs').write_text(f"const {{ used }} = require('{peer}'); used();", encoding='utf-8')
            paths.extend([folder + '/lib.cjs', folder + '/cross.test.cjs'])
        model = workspace_model(self.root, paths)
        def measure():
            return {(row['path'], row['diagnostic']['name']) for row in self.native_report(model)['findings']}
        expected = {('npm/lib.cjs', 'dead'), ('DynaDocs.Tests/coverage/lib.cjs', 'dead')}
        self.assertEqual(expected, measure())
        (self.root / 'DynaDocs.Tests/coverage/cross.test.cjs').write_text("require('../../npm/lib.cjs');", encoding='utf-8')
        self.assertEqual(expected | {('npm/lib.cjs', 'used')}, measure())

    def test_an_unimported_repository_root_module_is_reported_not_silently_owned(self):
        (self.root / 'DynaDocs.Tests/coverage/entry.test.cjs').write_text("require('node:assert');", encoding='utf-8')
        (self.root / 'stray.mjs').write_text('export const orphan = () => 1;\n', encoding='utf-8')
        model = workspace_model(self.root, ['DynaDocs.Tests/coverage/entry.test.cjs', 'stray.mjs'])
        self.assertEqual([], model['errors'])
        self.assertEqual(['stray.mjs'], model['config']['workspaces']['../..']['project'])
        self.assertEqual([], model['config']['workspaces']['../..']['entry'])
        self.assertEqual([('stray.mjs', 'knip-files')],
                         [(row['path'], row['gate']) for row in self.native_report(model)['findings']])

    def test_an_absent_native_knip_installation_is_never_a_green_gate(self):
        runner = Collectors.__new__(Collectors)
        runner.root = self.root
        runner.coverage = self.root / 'DynaDocs.Tests/coverage'
        runner.output = self.root / 'knip-output'
        runner.log = CommandLog(self.root, runner.output / 'commands')
        runner.inventory = {'sources': [{'path': path, 'language': 'javascript'} for path in self.paths]}

        answer = runner.javascript_unused_exports()

        self.assertEqual('error', answer['status'])
        self.assertEqual([], answer['findings'])
        self.assertEqual(1, runner.log.rows[0]['exit_code'])
        self.assertIn('Native Knip failed without accounted issues',
                      [row['message'] for row in answer['errors']])

    def test_collector_measures_the_real_maintained_javascript_graph(self):
        root = Path(__file__).resolve().parents[3]
        paths = [relative for relative in git_file_state(root)[0]
                 if language_of(root / relative) == 'javascript']
        runner = Collectors.__new__(Collectors)
        runner.root = root
        runner.coverage = root / 'DynaDocs.Tests/coverage'
        runner.output = self.root / 'knip-output'
        runner.log = CommandLog(root, runner.output / 'commands')
        runner.inventory = {'sources': [{'path': path, 'language': 'javascript'} for path in paths]}

        answer = collect_knip(runner)

        self.assertEqual([], answer['errors'])
        self.assertEqual(paths, [row['path'] for row in answer['facts']['model']['sources']])
        self.assertEqual(len(paths), answer['facts']['native'][1]['counters']['processed'])
        self.assertEqual([], [row for row in answer['findings'] if row['path'] not in set(paths)])

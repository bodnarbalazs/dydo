"""Package containment and native counts must account for every maintained source."""
import copy
import sys
import tempfile
import unittest
import json
import subprocess
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_knip import workspace_model, normalize_report


class KnipAccountingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for folder in ('DynaDocs.Tests/coverage', 'npm'):
            package = self.root / folder / 'package.json'
            package.parent.mkdir(parents=True)
            package.write_text('{}', encoding='utf-8')
        self.paths = ['DynaDocs.Tests/coverage/tests/example.test.cjs', 'npm/lib/example.js']

    def test_same_stem_sources_join_distinct_real_containing_packages(self):
        model = workspace_model(self.root, self.paths)
        self.assertEqual([], model['errors'])
        self.assertEqual(['tests/example.test.cjs'], model['config']['workspaces']['.']['project'])
        self.assertEqual(['lib/example.js'], model['config']['workspaces']['../../npm']['project'])
        self.assertEqual(self.paths, [row['path'] for row in model['sources']])

    def test_missing_package_outside_root_duplicate_and_unsupported_extension_fail_closed(self):
        for extra in ['outside.js', 'npm/bin/dydo', '../escape.cjs', self.paths[0]]:
            with self.subTest(extra=extra):
                self.assertTrue(workspace_model(self.root, self.paths + [extra])['errors'])
        (self.root / 'npm/package.json').unlink()
        self.assertTrue(workspace_model(self.root, self.paths)['errors'])

    def native(self):
        return [{'issues': [{'file': '../../npm/lib/example.js', 'exports': [{'name': 'dead', 'line': 3}],
                            'files': [], 'nsExports': [], 'duplicates': [], 'unresolved': []}]},
                {'kind': 'measurement', 'includedWorkspaceDirs': [str(self.root / path) for path in ('DynaDocs.Tests/coverage', 'npm')],
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

    def test_native_single_graph_preserves_cross_package_test_only_consumer(self):
        tooling = Path(__file__).resolve().parents[1]
        paths = []
        for folder in ('DynaDocs.Tests/coverage', 'npm'):
            package = self.root / folder
            (package / 'lib.cjs').write_text('exports.used = () => 1; exports.dead = () => 2;', encoding='utf-8')
            peer = '../../npm/lib.cjs' if folder.startswith('DynaDocs') else '../DynaDocs.Tests/coverage/lib.cjs'
            (package / 'cross.test.cjs').write_text(f"const {{ used }} = require('{peer}'); used();", encoding='utf-8')
            paths.extend([folder + '/lib.cjs', folder + '/cross.test.cjs'])
        model = workspace_model(self.root, paths)
        config = self.root / 'knip.json'
        config.write_text(json.dumps(model['config']), encoding='utf-8')
        command = ['node', str(tooling / 'node_modules/knip/bin/knip.js'), '--config', str(config),
                   '--reporter', str(tooling / 'knip_reporter.mjs'), '--no-gitignore', '--include-entry-exports',
                   '--include', 'files,exports,nsExports,duplicates,unresolved']
        def measure():
            native = subprocess.run(command, cwd=self.root / 'DynaDocs.Tests/coverage',
                                    capture_output=True, text=True, encoding='utf-8', timeout=60)
            self.assertEqual(1, native.returncode, native.stderr)
            report = normalize_report(self.root, model, [json.loads(line) for line in native.stdout.splitlines()])
            self.assertEqual([], report['errors'])
            return {(row['path'], row['diagnostic']['name']) for row in report['findings']}
        expected = {('npm/lib.cjs', 'dead'), ('DynaDocs.Tests/coverage/lib.cjs', 'dead')}
        self.assertEqual(expected, measure())
        (self.root / 'DynaDocs.Tests/coverage/cross.test.cjs').write_text("require('../../npm/lib.cjs');", encoding='utf-8')
        self.assertEqual(expected | {('npm/lib.cjs', 'used')}, measure())


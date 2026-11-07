"""Statechart native selection and complete same-candidate union."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import statechart_platform

SOURCE = 'export function neutral() { return 1; }\nexport function native() { return 2; }\n'


def module():
    return {'path': 'mixed.ts', 'language': 'typescript', 'executable': True,
            'lines': {'1': 1, '2': 1}, 'branches': {'2:0:0': 1, '2:0:1': 1},
            'methods': [{'id': f'{name}:{line}:7', 'kind': 'callable', 'line': line,
                         'end_line': line, 'body_lines': {str(line): 1},
                         'covered': 1, 'total': 1, 'cc': 1, 'cognitive': 0,
                         'parameters': 0, 'constructor': False}
                        for name, line in [('neutral', 1), ('native', 2)]]}


class StatechartPlatformTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        (self.root / 'mixed.ts').write_text(SOURCE)
        self.selection = {'schema': 1, 'neutral': 'linux',
                          'windows_tests': {'reason': 'Native ownership', 'tests': ['test/windows.test.ts'],
                                            'names': ['native']},
                          'native': {'mixed.ts': {'sha256': hashlib.sha256(SOURCE.encode()).hexdigest(),
                                                 'windows': ['native:2:7']}}}
        path = self.root / statechart_platform.SELECTION
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps(self.selection))
        self.modules = [module()]

    def scope(self, platform):
        return statechart_platform.scope(self.root, self.modules, platform)

    def reports(self):
        pair = []
        for platform in ('linux', 'windows'):
            rows, receipt = self.scope(platform)
            pair.append({'schema': 1, 'stack': 'statechart', 'gate': 'coverage',
                         'exitCode': 0, 'measurementComplete': True, 'findings': [], 'gaps': [],
                         'candidate': {'commit': 'a' * 40, 'dirty': False},
                         'collectors': {'typescript-coverage': {'facts': {
                             'modules': rows, 'platform_selection': receipt}}}})
        return pair

    def verify(self, pair):
        with patch.object(statechart_platform, '_source_modules', return_value=self.modules):
            return statechart_platform.verify_union(*pair, root=self.root)

    def test_native_callable_and_neutral_remainder_partition_the_mixed_module(self):
        linux, left = self.scope('linux')
        windows, right = self.scope('windows')
        self.assertEqual({'1': 1}, linux[0]['lines'])
        self.assertEqual({'2': 1}, windows[0]['lines'])
        self.assertEqual(['native:2:7'], [row['id'] for row in windows[0]['methods']])
        self.assertEqual(left['denominators'], right['denominators'])
        self.assertEqual(0, self.verify(self.reports())['exitCode'])

    def test_unmapped_module_is_required_only_on_linux(self):
        self.modules.append({**module(), 'path': 'posix.ts'})
        (self.root / 'posix.ts').write_text(SOURCE)
        self.assertEqual(['mixed.ts', 'posix.ts'], [m['path'] for m in self.scope('linux')[0]])
        self.assertEqual(['mixed.ts'], [m['path'] for m in self.scope('windows')[0]])

    def test_selection_rejects_source_missing_duplicate_callable_and_inventory_drift(self):
        for defect in ('source', 'missing', 'duplicate', 'inventory', 'platform'):
            selection = copy.deepcopy(self.selection)
            rule = selection['native']['mixed.ts']
            if defect == 'source': rule['sha256'] = '0' * 64
            if defect == 'missing': rule['windows'] = ['absent:2:7']
            if defect == 'duplicate': rule['windows'] *= 2
            if defect == 'inventory': selection['native']['gone.ts'] = rule
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                statechart_platform.scope(self.root, self.modules,
                                          'darwin' if defect == 'platform' else 'linux', selection)

    def test_union_refuses_incomplete_points_callables_bodies_and_modules(self):
        for defect in ('line', 'branch', 'callable', 'body', 'module', 'duplicate-module'):
            pair = self.reports()
            report = pair[1]
            facts = report['collectors']['typescript-coverage']['facts']
            row = facts['modules'][0]
            if defect == 'line': row['lines'] = {}
            if defect == 'branch': row['branches'].pop('2:0:1')
            if defect == 'callable': row['methods'] = []
            if defect == 'body': row['methods'][0]['body_lines'] = {}
            if defect == 'module': facts['modules'] = []
            if defect == 'duplicate-module': facts['modules'].append(copy.deepcopy(row))
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                self.verify(pair)

    def test_union_refuses_source_counter_metric_policy_and_receipt_defects(self):
        for defect in ('source', 'counter', 'metrics', 'policy', 'dirty', 'candidate', 'gap'):
            pair = self.reports()
            report = pair[1]
            facts = report['collectors']['typescript-coverage']['facts']
            row = facts['modules'][0]
            if defect == 'source': facts['platform_selection']['sources']['mixed.ts'] = '0' * 64
            if defect == 'counter': row['methods'][0]['covered'] = 0
            if defect == 'metrics': row['methods'][0]['cc'] = 2
            if defect == 'policy': row['lines']['2'] = 0
            if defect == 'dirty': report['candidate']['dirty'] = True
            if defect == 'candidate': report['candidate']['commit'] = 'b' * 40
            if defect == 'gap': report['gaps'] = ['missing']
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                self.verify(pair)

    def test_both_receipts_cannot_conspire_to_drop_a_branch(self):
        pair = self.reports()
        for report in pair:
            facts = report['collectors']['typescript-coverage']['facts']
            del facts['platform_selection']['denominators']['mixed.ts']['branches']['2:0:1']
            for row in facts['modules']: row['branches'].pop('2:0:1', None)
        with self.assertRaisesRegex(ValueError, 'Source-derived'):
            self.verify(pair)

    def test_windows_test_selection_is_exact_and_linux_keeps_the_full_suite(self):
        args = ['--test', '--test-concurrency=1', 'test/*.test.ts']
        self.assertEqual(args, statechart_platform.test_arguments(args, self.selection, 'linux'))
        self.assertEqual(args[:-1] + ['--test-name-pattern=^(?:native)$', 'test/windows.test.ts'],
                         statechart_platform.test_arguments(args, self.selection, 'windows'))

    def test_windows_selection_rejects_skipped_missing_duplicate_and_unexpected_cases(self):
        report = self.root / 'suite.tap'
        report.write_text('ok 1 - native\n')
        self.assertEqual({'names': ['native'], 'total': 1},
                         statechart_platform.selected_tests(report, self.selection))
        for text in ('', 'ok 1 - native # SKIP missing TLC\n', 'not ok 1 - native\n',
                     'ok 1 - native\nok 2 - native\n', 'ok 1 - native\nok 2 - other\n'):
            report.write_text(text)
            with self.subTest(text=text), self.assertRaises(ValueError):
                statechart_platform.selected_tests(report, self.selection)

    def test_windows_adapter_simulation_excludes_an_unloaded_posix_module(self):
        import gate_adapter
        from test_gate_adapter import StatechartGateTests, fake_node_test
        fixture = StatechartGateTests()
        root, raw, inventory = fixture.repository(self.root / 'adapter')
        path = statechart_platform.PACKAGE / 'run.ts'
        selection = copy.deepcopy(self.selection)
        selection['native'] = {path.as_posix(): {
            'sha256': hashlib.sha256((root / path).read_bytes()).hexdigest(), 'windows': ['run:1:7']}}
        selection['windows_tests'] = {'reason': 'fixture', 'tests': ['test/run.test.ts'], 'names': ['runs']}
        (root / statechart_platform.SELECTION).write_text(json.dumps(selection))
        missing = statechart_platform.PACKAGE / 'test/java.ts'
        (root / missing).write_text('console.log("POSIX only");\n')
        payload = json.loads(inventory.read_text())
        payload['sources'].append({'path': missing.as_posix(), 'language': 'typescript', 'role': 'target'})
        inventory.write_text(json.dumps(payload))
        child = fake_node_test(tap='ok 1 - runs\n# tests 1\n# fail 0\n# cancelled 0\n# skipped 0\n')
        with patch.object(statechart_platform, 'native_platform', return_value='windows'), \
                patch.object(gate_adapter, 'run_coverage_command', side_effect=child):
            answer = gate_adapter.collect_statechart_coverage(root, raw, inventory)
        facts = answer['facts']['collectors']['typescript-coverage']['facts']
        self.assertEqual([path.as_posix()], [row['path'] for row in facts['modules']])
        self.assertEqual('windows', facts['platform_selection']['platform'])
        self.assertIn(missing.as_posix(), facts['platform_selection']['sources'])
        self.assertEqual({'branch-coverage'}, {row['gate'] for row in answer['findings']})

    def test_unpublished_counters_stay_zero_and_published_corruption_is_rejected(self):
        from gate_policy import evaluate_policy
        targets = ['kept.ts', 'unloaded.ts']
        for target in targets:
            (self.root / target).write_text('console.log("measured");\n')
        raw = self.root / 'raw'
        reports = raw / 'istanbul'
        reports.mkdir(parents=True)
        walked = {target: {'methods': [], 'module': {'cc': 1, 'cognitive': 0}} for target in targets}
        source = statechart_platform.denominator(self.root, targets, raw / 'source')
        (reports / 'unloaded.json.pending').write_text('{')
        with self.assertRaisesRegex(ValueError, 'No statechart Istanbul coverage reports'):
            statechart_platform.measured_modules(self.root, targets, walked, raw)
        measured = source['kept.ts']
        measured['s'] = dict.fromkeys(measured['s'], 1)
        (reports / 'kept.json').write_text(json.dumps({str(self.root / 'kept.ts'): measured}))
        modules = statechart_platform.measured_modules(self.root, targets, walked, raw)
        self.assertEqual(targets, [row['path'] for row in modules])
        self.assertEqual([{'1': 1}, {'1': 0}], [row['lines'] for row in modules])
        self.assertEqual({'unloaded.ts'}, {finding['path'] for finding in evaluate_policy(modules)})
        for content in ('', '{'):
            with self.subTest(content=content):
                (reports / 'broken.json').write_text(content)
                with self.assertRaises(json.JSONDecodeError):
                    statechart_platform.measured_modules(self.root, targets, walked, raw)
        (reports / 'broken.json').unlink()
        (reports / 'foreign.json').write_text(json.dumps({str(self.root / 'foreign.ts'): measured}))
        with self.assertRaisesRegex(ValueError, 'outside target inventory'):
            statechart_platform.measured_modules(self.root, targets, walked, raw)

    def test_real_union_rebuilds_the_clean_candidate_and_detects_inventory_drift(self):
        source = statechart_platform.PACKAGE / 'mixed.ts'
        (self.root / source).parent.mkdir(parents=True)
        (self.root / source).write_text(SOURCE)
        selection = copy.deepcopy(self.selection)
        selection['native'] = {source.as_posix(): selection['native']['mixed.ts']}
        (self.root / statechart_platform.SELECTION).write_text(json.dumps(selection))
        provenance = self.root / statechart_platform.PACKAGE.parent / 'PROVENANCE.md'
        provenance.write_text('commit `' + '1' * 40 + '`\n```upstream-blobs\n```\n')
        shim = self.root / 'DynaDocs.Tests/coverage/js_metrics.cjs'
        shim.write_text('const fs=require("node:fs");const m=require(' +
                        json.dumps(str(statechart_platform.TOOLS / 'js_metrics.cjs')) +
                        ');const s=fs.readFileSync(0,"utf8");'
                        'process.stdout.write(JSON.stringify({...m.analyze(s,"typescript"),'
                        'module:m.moduleMetrics(s,"typescript")}));')
        def git(*args):
            return subprocess.check_output(['git', *args], cwd=self.root, text=True).strip()
        git('init', '-q')
        git('add', '.')
        git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '-qm', 'fixture')
        commit = git('rev-parse', 'HEAD')
        modules = statechart_platform._source_modules(self.root, commit)
        self.assertEqual([source.as_posix()], [row['path'] for row in modules])
        pair = []
        for platform in ('linux', 'windows'):
            measured = copy.deepcopy(modules)
            for row in measured:
                row['lines'] = {key: 1 for key in row['lines']}
                for method in row['methods']:
                    method['body_lines'] = {key: 1 for key in method['body_lines']}
                    method['covered'] = method['total']
            rows, receipt = statechart_platform.scope(self.root, measured, platform)
            pair.append({'schema': 1, 'stack': 'statechart', 'gate': 'coverage', 'exitCode': 0,
                         'measurementComplete': True, 'findings': [], 'gaps': [],
                         'candidate': {'commit': commit, 'dirty': False},
                         'collectors': {'typescript-coverage': {'facts': {
                             'modules': rows, 'platform_selection': receipt}}}})
        self.assertEqual(0, statechart_platform.verify_union(*pair, self.root)['exitCode'])
        with tempfile.TemporaryDirectory() as evidence:
            left, right, output = [Path(evidence) / name for name in ('linux.json', 'windows.json', 'union.json')]
            left.write_text(json.dumps(pair[0]))
            right.write_text(json.dumps(pair[1]))
            argv = [sys.executable, str(Path(statechart_platform.__file__)), '--root', str(self.root),
                    '--linux', str(left), '--windows', str(right), '--output', str(output)]
            result = subprocess.run(argv, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(0, json.loads(output.read_text())['exitCode'])
            output.unlink()
            right.write_text('{}')
            refused = subprocess.run(argv, capture_output=True, text=True)
            self.assertEqual(2, refused.returncode)
            self.assertFalse(output.exists())
        (self.root / statechart_platform.PACKAGE / 'new.tsx').write_text('console.log(1);\n')
        with self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
            statechart_platform.verify_union(*pair, self.root)
        git('add', '.')
        git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid', 'commit', '-qm', 'new target')
        for report in pair: report['candidate']['commit'] = git('rev-parse', 'HEAD')
        with self.assertRaisesRegex(ValueError, 'Source-derived platform sources differ'):
            statechart_platform.verify_union(*pair, self.root)


if __name__ == '__main__':
    unittest.main()

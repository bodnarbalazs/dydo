"""Platform scopes retain the entire maintained Python measurement denominator."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import python_platform


SOURCE = 'import os\ndef neutral():\n    return 1\ndef native():\n    if os.name == "nt":\n        return 2\n    return 3\n'


def module():
    return {'path': 'mixed.py', 'language': 'python', 'executable': True,
            'lines': {str(n): 1 for n in range(1, 8)},
            'branches': {'5:6': 1, '5:7': 0},
            'methods': [
                {'id': 'neutral:2:0', 'kind': 'FunctionDef', 'line': 2, 'end_line': 3,
                 'body_lines': {'3': 1}, 'covered': 1, 'total': 1, 'cc': 1,
                 'cognitive': 0, 'parameters': 0, 'constructor': False},
                {'id': 'native:4:0', 'kind': 'FunctionDef', 'line': 4, 'end_line': 7,
                 'body_lines': {'5': 1, '6': 1, '7': 0}, 'covered': 2, 'total': 3,
                 'cc': 2, 'cognitive': 1, 'parameters': 0, 'constructor': False},
                {'id': '<module>:1:0', 'kind': 'module', 'line': 1, 'end_line': 7,
                 'body_lines': {'1': 1, '2': 1, '4': 1}, 'covered': 3, 'total': 3,
                 'cc': 1, 'cognitive': 0, 'parameters': 0, 'constructor': False}]}


class PythonPlatformTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)
        (self.root / 'mixed.py').write_text(SOURCE, encoding='utf-8')
        self.selection = {'schema': 1, 'neutral': 'linux', 'native': {'mixed.py': {
            'sha256': hashlib.sha256(SOURCE.encode()).hexdigest(), 'windows': ['native']}}}

    def scope(self, platform, selection=None, modules=None):
        return python_platform.scope(self.root, modules or [module()], platform,
                                     self.selection if selection is None else selection)

    def reports(self):
        reports = []
        for platform in ('linux', 'windows'):
            rows, receipt = self.scope(platform)
            for row in rows:
                row['branches'] = {edge: 1 for edge in row['branches']}
            reports.append({'schema': 1, 'candidate': {'commit': 'a' * 40, 'dirty': False},
                            'exitCode': 0, 'measurementComplete': True,
                            'stack': 'python', 'gate': 'coverage', 'findings': [], 'gaps': [],
                            'collectors': {'python-coverage': {'facts': {
                                'modules': rows, 'platform_selection': receipt}}}})
        return reports

    def test_adapter_runs_the_whole_suite_then_gates_the_selected_native_points(self):
        import gate_adapter
        selection = self.root / python_platform.SELECTION
        selection.parent.mkdir(parents=True)
        selection.write_text(json.dumps(self.selection), encoding='utf-8')
        tests = self.root / 'DynaDocs.Tests/coverage/tests'
        tests.mkdir()
        (tests / 'test_mixed.py').write_text(
            'import sys, unittest\nfrom pathlib import Path\n'
            'sys.path.insert(0, str(Path(__file__).resolve().parents[3]))\n'
            'import mixed\nclass Contract(unittest.TestCase):\n'
            '    def test_neutral(self): self.assertEqual(1, mixed.neutral())\n'
            '    def test_native(self): self.assertIn(mixed.native(), (2, 3))\n', encoding='utf-8')
        inventory = self.root / 'inventory.json'
        inventory.write_text(json.dumps({'sources': [
            {'path': 'mixed.py', 'language': 'python', 'role': 'target'}]}), encoding='utf-8')
        raw = self.root / 'campaign/raw'
        result = gate_adapter.collect_python_coverage(self.root, raw, inventory)
        facts = result['facts']['collectors']['python-coverage']['facts']
        self.assertEqual(0, facts['child_exit'])
        self.assertEqual('windows' if sys.platform == 'win32' else 'linux',
                         facts['platform_selection']['platform'])
        self.assertEqual({'4', '5', '6', '7'} if sys.platform == 'win32' else {'1', '2', '3'},
                         set(facts['modules'][0]['lines']))
        self.assertTrue((raw / 'coverage.json').is_file())
        selection.unlink()
        refused = self.root / 'refused/raw'
        with self.assertRaises(FileNotFoundError):
            gate_adapter.collect_python_coverage(self.root, refused, inventory)
        self.assertFalse(refused.exists())

    def test_selection_file_is_required_and_used_from_the_candidate_root(self):
        path = self.root / python_platform.SELECTION
        path.parent.mkdir(parents=True)
        with self.assertRaises(FileNotFoundError):
            python_platform.scope(self.root, [module()], 'linux')
        path.write_text(json.dumps(self.selection), encoding='utf-8')
        rows, _ = python_platform.scope(self.root, [module()], 'windows')
        self.assertIn('native:4:0', [m['id'] for m in rows[0]['methods']])

    def test_union_checks_modules_callables_bodies_sources_and_policy(self):
        for defect in ('module', 'callable', 'body', 'source', 'policy', 'dirty', 'counter', 'schema'):
            pair = self.reports()
            report = pair[1]
            facts = report['collectors']['python-coverage']['facts']
            row = facts['modules'][0]
            if defect == 'module':
                facts['modules'] = []
            if defect == 'callable':
                row['methods'].pop(0)
            if defect == 'body':
                del row['methods'][0]['body_lines']['7']
            if defect == 'source':
                facts['platform_selection']['sources']['mixed.py'] = '0' * 64
            if defect == 'policy':
                row['lines'] = {line: 0 for line in row['lines']}
            if defect == 'dirty':
                report['candidate']['dirty'] = True
            if defect == 'counter':
                row['methods'][0]['covered'] = 3
            if defect == 'schema':
                report['schema'] = 2
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                python_platform.verify_union(*pair)

    def test_union_cannot_drop_points_by_assigning_an_unsupported_owner(self):
        pair = self.reports()
        for report in pair:
            receipt = report['collectors']['python-coverage']['facts']['platform_selection']
            receipt['denominators']['mixed.py']['lines']['7'] = 'darwin'
        del pair[1]['collectors']['python-coverage']['facts']['modules'][0]['lines']['7']
        with self.assertRaisesRegex(ValueError, 'Unsupported platform point owner'):
            python_platform.verify_union(*pair)

    def test_union_command_writes_only_a_complete_passing_receipt(self):
        left, right = self.reports()
        paths = [self.root / name for name in ('linux.json', 'windows.json', 'union.json')]
        for path, value in zip(paths, (left, right)):
            path.write_text(json.dumps(value), encoding='utf-8')
        argv = [sys.executable, str(Path(python_platform.__file__)), '--linux', str(paths[0]),
                '--windows', str(paths[1]), '--output', str(paths[2])]
        result = subprocess.run(argv, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(['linux', 'windows'], json.loads(paths[2].read_text())['platforms'])
        paths[2].unlink()
        paths[1].write_text('{}', encoding='utf-8')
        refused = subprocess.run(argv, capture_output=True, text=True)
        self.assertEqual(2, refused.returncode)
        self.assertFalse(paths[2].exists())

    def test_linux_keeps_neutral_code_in_mixed_module(self):
        rows, receipt = self.scope('linux')
        self.assertEqual({'1': 1, '2': 1, '3': 1}, rows[0]['lines'])
        self.assertEqual(['neutral:2:0', '<module>:1:0'], [m['id'] for m in rows[0]['methods']])
        self.assertEqual({'1': 1, '2': 1}, rows[0]['methods'][-1]['body_lines'])
        self.assertEqual('linux', receipt['platform'])

    def test_windows_measures_native_branches_and_callables_at_unchanged_policy(self):
        from gate_policy import evaluate_policy
        rows, _ = self.scope('windows')
        self.assertEqual({'4': 1, '5': 1, '6': 1, '7': 1}, rows[0]['lines'])
        self.assertEqual({'5:6': 1, '5:7': 0}, rows[0]['branches'])
        self.assertEqual(['native:4:0', '<module>:1:0'], [m['id'] for m in rows[0]['methods']])
        self.assertEqual(['branch-coverage'], [f['gate'] for f in evaluate_policy(rows)])

    def test_selection_refuses_unsupported_platform_stale_source_and_unknown_callable(self):
        for platform, change in [('darwin', None), ('linux', 'hash'), ('windows', 'callable'),
                                 ('linux', 'missing-module'), ('linux', 'schema')]:
            selection = copy.deepcopy(self.selection)
            if change == 'hash':
                selection['native']['mixed.py']['sha256'] = '0' * 64
            if change == 'callable':
                selection['native']['mixed.py']['windows'] = ['absent']
            if change == 'missing-module':
                selection['native']['absent.py'] = selection['native'].pop('mixed.py')
            if change == 'schema':
                selection['schema'] = 2
            with self.subTest(platform=platform, change=change), self.assertRaises(ValueError):
                self.scope(platform, selection)

    def test_unlisted_modules_are_measured_on_linux_by_explicit_neutral_rule(self):
        selection = {'schema': 1, 'neutral': 'linux', 'native': {}}
        rows, _ = self.scope('linux', selection)
        self.assertEqual([module()], rows)
        self.assertEqual([], self.scope('windows', selection)[0])

    def test_complete_union_rejects_missing_points_platforms_and_candidate_drift(self):
        linux, left = self.scope('linux')
        windows, right = self.scope('windows')
        windows[0]['branches']['5:7'] = 1
        candidate = {'commit': 'a' * 40, 'dirty': False}
        def report(rows, receipt):
            return {'schema': 1, 'candidate': dict(candidate), 'exitCode': 0, 'measurementComplete': True,
                    'stack': 'python', 'gate': 'coverage', 'findings': [], 'gaps': [],
                    'collectors': {'python-coverage': {'facts': {
                        'modules': rows, 'platform_selection': receipt}}}}
        reports = [report(linux, left), report(windows, right)]
        self.assertEqual(1, python_platform.verify_union(*reports)['modules'])
        broken = copy.deepcopy(reports)
        del broken[1]['collectors']['python-coverage']['facts']['modules'][0]['lines']['7']
        changed = copy.deepcopy(reports)
        changed[1]['candidate']['commit'] = 'b' * 40
        failed = copy.deepcopy(reports)
        failed[0]['exitCode'] = 1
        for pair in [broken, changed, failed, [reports[0], reports[0]]]:
            with self.subTest(pair=pair), self.assertRaises(ValueError):
                python_platform.verify_union(*pair)


if __name__ == '__main__':
    unittest.main()

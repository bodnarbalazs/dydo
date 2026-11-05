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
        selection = self.root / python_platform.SELECTION
        selection.parent.mkdir(parents=True)
        selection.write_text(json.dumps(self.selection), encoding='utf-8')
        (self.root / '.gitignore').write_text('*\n', encoding='utf-8')
        self.git('init', '-q')
        self.git('add', '-f', 'mixed.py', str(python_platform.SELECTION), '.gitignore')
        self.git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-qm', 'Pinned fixture sources and reviewed selection')
        self.commit = self.git('rev-parse', 'HEAD').strip()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True)

    def verify(self, *reports):
        return python_platform.verify_union(*reports, root=self.root)

    def scope(self, platform, selection=None, modules=None):
        return python_platform.scope(self.root, modules or [module()], platform,
                                     self.selection if selection is None else selection)

    def reports(self):
        reports = []
        for platform in ('linux', 'windows'):
            rows, receipt = self.scope(platform)
            for row in rows:
                row['branches'] = {edge: 1 for edge in row['branches']}
            reports.append({'schema': 1, 'candidate': {'commit': self.commit, 'dirty': False},
                            'exitCode': 0, 'measurementComplete': True,
                            'stack': 'python', 'gate': 'coverage', 'findings': [], 'gaps': [],
                            'collectors': {'python-coverage': {'facts': {
                                'modules': rows, 'platform_selection': receipt}}}})
        return reports

    def test_adapter_runs_the_whole_suite_then_gates_the_selected_native_points(self):
        import gate_adapter
        selection = self.root / python_platform.SELECTION
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
        path.unlink()
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
                self.verify(*pair)

    def test_union_cannot_drop_points_by_assigning_an_unsupported_owner(self):
        pair = self.reports()
        for report in pair:
            receipt = report['collectors']['python-coverage']['facts']['platform_selection']
            receipt['denominators']['mixed.py']['lines']['7'] = 'darwin'
        del pair[1]['collectors']['python-coverage']['facts']['modules'][0]['lines']['7']
        with self.assertRaisesRegex(ValueError, 'Source-derived platform denominators differ'):
            self.verify(*pair)

    def test_union_command_writes_only_a_complete_passing_receipt(self):
        left, right = self.reports()
        paths = [self.root / name for name in ('linux.json', 'windows.json', 'union.json')]
        for path, value in zip(paths, (left, right)):
            path.write_text(json.dumps(value), encoding='utf-8')
        argv = [sys.executable, str(Path(python_platform.__file__)), '--linux', str(paths[0]),
                '--windows', str(paths[1]), '--output', str(paths[2]), '--root', str(self.root)]
        result = subprocess.run(argv, cwd=self.root, capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertFalse((self.root / '.coverage').exists())
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
        candidate = {'commit': self.commit, 'dirty': False}
        def report(rows, receipt):
            return {'schema': 1, 'candidate': dict(candidate), 'exitCode': 0, 'measurementComplete': True,
                    'stack': 'python', 'gate': 'coverage', 'findings': [], 'gaps': [],
                    'collectors': {'python-coverage': {'facts': {
                        'modules': rows, 'platform_selection': receipt}}}}
        reports = [report(linux, left), report(windows, right)]
        self.assertEqual(1, self.verify(*reports)['modules'])
        broken = copy.deepcopy(reports)
        del broken[1]['collectors']['python-coverage']['facts']['modules'][0]['lines']['7']
        changed = copy.deepcopy(reports)
        changed[1]['candidate']['commit'] = 'b' * 40
        failed = copy.deepcopy(reports)
        failed[0]['exitCode'] = 1
        for pair in [broken, changed, failed, [reports[0], reports[0]]]:
            with self.subTest(pair=pair), self.assertRaises(ValueError):
                self.verify(*pair)

    def test_correlated_callable_omission_is_rejected_against_source(self):
        pair = self.reports()
        for report in pair:
            facts = report['collectors']['python-coverage']['facts']
            row = facts['platform_selection']['denominators']['mixed.py']
            del row['methods']['native:4:0']
            del row['bodies']['native:4:0']
            facts['modules'][0]['methods'] = [m for m in facts['modules'][0]['methods']
                                             if m['id'] != 'native:4:0']
        with self.assertRaisesRegex(ValueError, 'Source-derived platform denominators differ'):
            self.verify(*pair)

    def test_correlated_branch_omission_is_rejected_against_source(self):
        pair = self.reports()
        for report in pair:
            receipt = report['collectors']['python-coverage']['facts']['platform_selection']
            del receipt['denominators']['mixed.py']['branches']['5:7']
        del pair[1]['collectors']['python-coverage']['facts']['modules'][0]['branches']['5:7']
        with self.assertRaisesRegex(ValueError, 'Source-derived platform denominators differ'):
            self.verify(*pair)

    def test_correlated_neutral_transfer_is_rejected_against_reviewed_selection(self):
        pair = self.reports()
        changed = copy.deepcopy(self.selection)
        changed['native']['mixed.py']['windows'].append('neutral')
        for report, platform in zip(pair, python_platform.PLATFORMS):
            facts = report['collectors']['python-coverage']['facts']
            old_hash = facts['platform_selection']['selection_sha256']
            rows, receipt = self.scope(platform, changed)
            for row in rows:
                row['branches'] = {edge: 1 for edge in row['branches']}
            receipt['selection_sha256'] = old_hash
            facts.update(modules=rows, platform_selection=receipt)
        with self.assertRaisesRegex(ValueError, 'Source-derived platform denominators differ'):
            self.verify(*pair)

    def test_whole_neutral_module_cannot_disappear_from_both_reports(self):
        pair = self.reports()
        (self.root / 'extra.PY').write_text('value = 1\n', encoding='utf-8')
        self.git('add', '-f', 'extra.PY')
        self.git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-qm', 'Additional neutral target')
        for report in pair:
            report['candidate']['commit'] = self.git('rev-parse', 'HEAD').strip()
        with self.assertRaisesRegex(ValueError, 'Source-derived platform sources differ'):
            self.verify(*pair)

    def test_correlated_hash_forgery_is_rejected(self):
        for key in ('sources', 'selection_sha256'):
            pair = self.reports()
            for report in pair:
                receipt = report['collectors']['python-coverage']['facts']['platform_selection']
                receipt[key] = {'mixed.py': '0' * 64} if key == 'sources' else '0' * 64
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'Source-derived platform ' + key):
                self.verify(*pair)

    def test_checkout_source_and_selection_tampering_are_rejected(self):
        pair = self.reports()
        for path in (self.root / 'mixed.py', self.root / python_platform.SELECTION):
            original = path.read_bytes()
            try:
                path.write_bytes(original + b'\n')
                with self.subTest(path=path), self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
                    self.verify(*pair)
            finally:
                path.write_bytes(original)

    def test_receipt_candidate_must_be_the_authoritative_checkout(self):
        pair = self.reports()
        for report in pair:
            report['candidate']['commit'] = 'a' * 40
        with self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
            self.verify(*pair)

    def test_authoritative_root_cannot_be_a_partial_checkout_directory(self):
        with self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
            python_platform.verify_union(*self.reports(), root=self.root / 'DynaDocs.Tests')

    def test_cross_platform_hash_and_denominator_mismatch_have_specific_errors(self):
        for key in ('selection_sha256', 'denominators'):
            pair = self.reports()
            receipt = pair[1]['collectors']['python-coverage']['facts']['platform_selection']
            receipt[key] = {} if key == 'denominators' else '0' * 64
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'Platform source or denominator drift'):
                self.verify(*pair)

    def test_gaps_wrong_stack_and_incomplete_flag_each_reject_independently(self):
        for key, value in (('gaps', ['unmeasured']), ('stack', 'node'), ('measurementComplete', False)):
            pair = self.reports()
            pair[0][key] = value
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, 'two clean passing Python coverage gates'):
                self.verify(*pair)

    def test_native_selection_requires_unique_nonempty_names_and_linux_neutral(self):
        for names in ([], ['native', 'native']):
            selection = copy.deepcopy(self.selection)
            selection['native']['mixed.py']['windows'] = names
            with self.subTest(names=names), self.assertRaisesRegex(ValueError, 'Missing or duplicate native callable'):
                self.scope('linux', selection)
        selection = {**self.selection, 'neutral': 'windows'}
        with self.assertRaisesRegex(ValueError, 'Unsupported Python platform selection'):
            self.scope('linux', selection)

    def test_decorators_and_loop_exit_arcs_belong_to_native_origin(self):
        source = 'def decorate(f):\n    return f\n@decorate\ndef native():\n    for x in (1, 2):\n        print(x)\n'
        (self.root / 'mixed.py').write_text(source, encoding='utf-8')
        selection = copy.deepcopy(self.selection)
        selection['native']['mixed.py']['sha256'] = hashlib.sha256(source.encode()).hexdigest()
        row = module()
        row.update(lines={str(n): 1 for n in range(1, 7)}, branches={'5:6': 1, '5:-3': 1})
        rows, receipt = self.scope('windows', selection, [row])
        self.assertIn('3', rows[0]['lines'])
        self.assertEqual({'5:6': 1, '5:-3': 1}, rows[0]['branches'])
        self.assertEqual('windows', receipt['denominators']['mixed.py']['branches']['5:-3'])

    def test_duplicate_modules_and_callables_are_rejected_by_policy(self):
        for kind, message in (('modules', 'duplicate module identity'), ('methods', 'Duplicate method identity')):
            pair = self.reports()
            rows = pair[0]['collectors']['python-coverage']['facts']['modules']
            target = rows if kind == 'modules' else rows[0]['methods']
            target.append(copy.deepcopy(target[0]))
            with self.subTest(kind=kind), self.assertRaisesRegex(ValueError, message):
                self.verify(*pair)

    def test_union_accepts_native_producer_then_rejects_correlated_body_loss(self):
        from python_coverage import collect
        from python_join import collect as join
        pair = self.reports()
        source = 'def neutral():\n    return 1\ndef native(flag):\n    if flag:\n        return 2\n    return 3\n'
        (self.root / 'mixed.py').write_text(source, encoding='utf-8')
        selection = copy.deepcopy(self.selection)
        selection['native']['mixed.py']['sha256'] = hashlib.sha256(source.encode()).hexdigest()
        (self.root / python_platform.SELECTION).write_text(json.dumps(selection), encoding='utf-8')
        self.git('add', '-u')
        self.git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-qm', 'Real producer fixture')
        (self.root / 'exercise.py').write_text(
            'import mixed\nassert mixed.neutral() == 1\nassert mixed.native(True) == 2\n'
            'assert mixed.native(False) == 3\n', encoding='utf-8')
        raw = self.root / 'native-coverage'
        self.assertEqual(0, collect(self.root, raw, ['mixed.py'], [sys.executable, 'exercise.py']))
        modules = join(self.root, raw, ['mixed.py'])
        for report, platform in zip(pair, python_platform.PLATFORMS):
            rows, receipt = python_platform.scope(self.root, modules, platform)
            report['candidate']['commit'] = self.git('rev-parse', 'HEAD').strip()
            report['collectors']['python-coverage']['facts'] = {'modules': rows, 'platform_selection': receipt}
        self.assertEqual(0, self.verify(*pair)['exitCode'])
        for report in pair:
            receipt = report['collectors']['python-coverage']['facts']['platform_selection']
            del receipt['denominators']['mixed.py']['bodies']['native:3:0']['6']
        native = pair[1]['collectors']['python-coverage']['facts']['modules'][0]['methods'][0]
        del native['body_lines']['6']
        native.update(total=2, covered=2)
        with self.assertRaisesRegex(ValueError, 'Source-derived platform denominators differ'):
            self.verify(*pair)

    def test_callable_metrics_cannot_be_lowered_in_receipts(self):
        pair = self.reports()
        method = pair[1]['collectors']['python-coverage']['facts']['modules'][0]['methods'][0]
        method['cc'] = 1
        with self.assertRaisesRegex(ValueError, 'Platform callable facts differ from source'):
            self.verify(*pair)


if __name__ == '__main__':
    unittest.main()

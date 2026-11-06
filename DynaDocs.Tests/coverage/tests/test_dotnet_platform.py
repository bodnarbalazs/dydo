"""The .NET platform selection partitions the whole native join; the union rebuilds and checks it."""
import copy
import hashlib
import io
import json
from contextlib import redirect_stderr
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import dotnet_platform
from gate_policy import evaluate_policy


SOURCES = {
    'Lib/Mixed.cs': 'namespace Lib;\nclass Mixed\n{\n    void Run() { }\n    void RunNative() { }\n}\n',
    'Lib/Native.cs': 'namespace Lib;\nclass Native\n{\n    readonly int _x = 1;\n    bool Probe() => true;\n}\n',
    'Lib/Neutral.cs': 'namespace Lib;\nclass Neutral\n{\n    string Name() => "";\n    string Id { get; }\n}\n',
}
TEST_SOURCE = ('namespace DynaDocs.Tests.Lib;\n\npublic class NativeTests\n{\n'
               '    [Fact]\n    public void ProbeAnswers() { }\n}\n')
WINDOWS_TEST = 'DynaDocs.Tests.Lib.NativeTests.ProbeAnswers'
OTHER_TEST = 'DynaDocs.Tests.Lib.NativeTests.ProbeRefuses'
SUMMARY = 'Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 1 s - x.dll\n'


def results(*cases):
    """A VSTest TRX as the xUnit adapter writes it: one definition and result per case, a theory's rows
    sharing their method."""
    tests, rows = [], []
    for index, (name, outcome) in enumerate(cases):
        owner, method = name.rsplit('.', 1)
        tests.append(f'<UnitTest name="{name}(case: {index})" id="t{index}">'
                     f'<TestMethod className="{owner}" name="{method}" /></UnitTest>')
        rows.append(f'<UnitTestResult testId="t{index}" testName="{name}(case: {index})" outcome="{outcome}" />')
    return ('﻿<?xml version="1.0" encoding="utf-8"?>'
            '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
            f'<Results>{"".join(rows)}</Results><TestDefinitions>{"".join(tests)}</TestDefinitions>'
            '</TestRun>').encode('utf-8')


PASSING = results(*[(WINDOWS_TEST, 'Passed')] * 3)


def pin(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def selection():
    return {'schema': 1, 'neutral': 'linux',
            'windows_tests': {'reason': 'Only these reach the native members.', 'tests': [WINDOWS_TEST]},
            'native': {'Lib/Mixed.cs': {'sha256': pin(SOURCES['Lib/Mixed.cs']), 'windows': ['RunNative']},
                       'Lib/Native.cs': {'sha256': pin(SOURCES['Lib/Native.cs']), 'windows': ['.ctor', 'Probe']}}}


def write_dotnet_fixture(root):
    """A repository root with pinned sources, a declared Windows test and the reviewed selection."""
    root = Path(root)
    for path, text in {**SOURCES, 'DynaDocs.Tests/Lib/NativeTests.cs': TEST_SOURCE}.items():
        (root / path).parent.mkdir(parents=True, exist_ok=True)
        (root / path).write_bytes(text.encode('utf-8'))
    target = root / dotnet_platform.SELECTION
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(selection()), encoding='utf-8')
    return root


def source_files():
    def declared(owner, *members):
        return [{'type': owner, 'member': member} for member in members]
    return [{'path': 'Lib/Mixed.cs', 'methods': [*declared('Lib.Mixed', 'Run', 'RunNative'),
                                                 *declared('Lib.Mixed.RunNative', '<lambda>')]},
            {'path': 'Lib/Native.cs', 'methods': declared('Lib.Native', '.ctor', 'Probe')},
            {'path': 'Lib/Neutral.cs', 'methods': declared('Lib.Neutral', 'Name')}]


def _callable(path, identity, physical, lines, hits, branch_lines=(), constructor=False):
    full = identity + '|' + physical
    points = {str(line): hits for line in lines}
    branches = {f'{physical}:{line}:{n}:{n + 1}:{n}:{n}': hits for n, line in enumerate(branch_lines)}
    method = {'id': full, 'kind': 'ConstructorDeclaration' if constructor else 'MethodDeclaration',
              'line': lines[0], 'end_line': lines[-1], 'cc': 1 + len(branch_lines) // 2, 'policy_cc': 1,
              'cognitive': 0, 'parameters': 0, 'constructor': constructor,
              'covered': sum(hit > 0 for hit in points.values()), 'total': len(points)}
    body = {'physical': physical, 'key': identity.split('@')[0].split('`')[0] + '`0()',
            'logical': {'path': path, 'id': full}, 'structural': None, 'lines': points, 'branches': branches}
    return method, body


def _module(path, callables, structural=()):
    bodies = [body for _, body in callables] + list(structural)
    lines, branches = {}, {}
    for body in bodies:
        lines.update(body['lines'])
        branches.update(body['branches'])
    return {'path': path, 'executable': True, 'lines': lines, 'branches': branches,
            'methods': [method for method, _ in callables], 'body_owners': bodies}


def joined_modules(linux=1, windows=1):
    """Native join rows: a mixed source, a wholly native source and a neutral source."""
    mixed, native, neutral = 'Lib/Mixed.cs', 'Lib/Native.cs', 'Lib/Neutral.cs'
    synthesized = {'physical': 'System.String Lib.Neutral::get_Id()', 'key': 'Lib.Neutral::get_Id`0()',
                   'logical': None, 'structural': 'semantic synthesized member without authored body',
                   'lines': {'5': linux}, 'branches': {}}
    return [
        _module(mixed, [
            _callable(mixed, 'Lib.Mixed::Run@10:40', 'System.Void Lib.Mixed::Run()', [3, 4], linux, [4, 4]),
            _callable(mixed, 'Lib.Mixed::RunNative@60:80', 'System.Void Lib.Mixed::RunNative()', [6, 7],
                      windows, [6, 6]),
            _callable(mixed, 'Lib.Mixed.RunNative::<lambda>@90:10',
                      'System.Int32 Lib.Mixed/<>c::<RunNative>b__1_0()', [8], windows)]),
        _module(native, [
            _callable(native, 'Lib.Native::.ctor`0()', 'System.Void Lib.Native::.ctor()', [4, 2], windows,
                      constructor=True),
            _callable(native, 'Lib.Native::Probe@100:20', 'System.Boolean Lib.Native::Probe()', [5], windows,
                      [5, 5])]),
        _module(neutral, [_callable(neutral, 'Lib.Neutral::Name@1:2', 'System.String Lib.Neutral::Name()',
                                    [4], linux)], [synthesized]),
    ]


def write_evidence(evidence, modules, stdout=SUMMARY, trx=None):
    evidence = Path(evidence)
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / 'joined.json').write_text(json.dumps({
        'schema': 1, 'modules': modules, 'findings': evaluate_policy(modules),
        'targets': [{'assembly': 'dydo', 'modules': modules, 'accounting': []}]}), encoding='utf-8')
    files = source_files()
    (evidence / 'source-facts-dydo.json').write_text(json.dumps({'files': files}), encoding='utf-8')
    (evidence / 'source-facts-GateMetrics.json').write_text(json.dumps({'files': []}), encoding='utf-8')
    (evidence / 'altcover-runner.stdout').write_text(stdout, encoding='utf-8')
    if trx is not None:
        (evidence / dotnet_platform.WINDOWS_RESULTS).write_bytes(trx)


class SelectionTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = write_dotnet_fixture(self.folder.name)

    def rejected(self, value, message, platform='linux'):
        with self.assertRaisesRegex(ValueError, message):
            dotnet_platform.load_selection(self.root, platform, value)

    def test_the_committed_selection_is_reviewed_pinned_and_its_tests_are_declared(self):
        root = Path(__file__).resolve().parents[3]
        committed = dotnet_platform.load_selection(root, 'windows')
        found = dotnet_platform.discover_windows_tests(root, committed)
        self.assertEqual(committed['windows_tests']['tests'], list(found))
        self.assertTrue(all(path.startswith('DynaDocs.Tests/Statechart/') for path in found.values()))

    def test_a_valid_selection_loads_for_both_platforms_and_builds_an_exact_filter(self):
        for platform in dotnet_platform.PLATFORMS:
            self.assertEqual(selection(), dotnet_platform.load_selection(self.root, platform))
        self.assertEqual('FullyQualifiedName=' + WINDOWS_TEST, dotnet_platform.windows_filter(selection()))

    def test_unsupported_platforms_shapes_and_missing_files_fail_closed(self):
        for platform in (None, 'darwin'):
            self.rejected(selection(), 'Unsupported', platform)
        for change in ({'schema': True}, {'schema': 2}, {'neutral': 'windows'}, {'native': {}},
                       {'native': []}, {'windows_tests': []}, {'extra': 1}):
            self.rejected({**selection(), **change}, 'Unsupported')
        self.rejected([], 'Unsupported')
        (self.root / dotnet_platform.SELECTION).unlink()
        with self.assertRaisesRegex(ValueError, 'Missing .NET platform selection'):
            dotnet_platform.load_selection(self.root, 'linux')

    def test_the_windows_test_selection_needs_a_reason_and_unique_qualified_tests(self):
        for tests in ({'tests': [WINDOWS_TEST]}, {'reason': '', 'tests': [WINDOWS_TEST]},
                      {'reason': 1, 'tests': [WINDOWS_TEST]}):
            self.rejected({**selection(), 'windows_tests': tests}, 'reason')
        for names in ([], [WINDOWS_TEST, WINDOWS_TEST], ['ProbeAnswers'], [1], 'x',
                      ['Other.Tests.NativeTests.ProbeAnswers']):
            self.rejected({**selection(), 'windows_tests': {'reason': 'r', 'tests': names}}, 'Windows test')

    def test_each_native_rule_names_unique_members_of_a_pinned_csharp_source(self):
        def rule(**change):
            native = copy.deepcopy(selection()['native'])
            native['Lib/Mixed.cs'].update(change)
            return {**selection(), 'native': native}
        for members in ([], ['RunNative', 'RunNative'], [''], 'RunNative'):
            self.rejected(rule(windows=members), 'Missing or duplicate native member')
        self.rejected(rule(extra=1), 'Invalid native member rule')
        self.rejected({**selection(), 'native': {'Lib/Mixed.txt': selection()['native']['Lib/Mixed.cs']}},
                      'Invalid native member rule')
        self.rejected({**selection(), 'native': {'Lib/Gone.cs': selection()['native']['Lib/Mixed.cs']}},
                      'Missing platform selection source: Lib/Gone.cs')
        self.rejected(rule(sha256=pin('other')), 'drift: Lib/Mixed.cs')

    def test_line_endings_do_not_drift_a_pin_but_any_other_byte_does(self):
        mixed = self.root / 'Lib/Mixed.cs'
        mixed.write_bytes(SOURCES['Lib/Mixed.cs'].replace('\n', '\r\n').encode('utf-8'))
        dotnet_platform.load_selection(self.root, 'windows')
        mixed.write_bytes(SOURCES['Lib/Mixed.cs'].replace('Run()', 'Run2()').encode('utf-8'))
        self.rejected(selection(), 'drift')

    def test_each_named_windows_test_must_be_declared_exactly_once_in_its_class(self):
        self.assertEqual({WINDOWS_TEST: 'DynaDocs.Tests/Lib/NativeTests.cs'},
                         dotnet_platform.discover_windows_tests(self.root, selection()))
        build = self.root / 'DynaDocs.Tests/bin/Copy.cs'
        build.parent.mkdir()
        build.write_text(TEST_SOURCE, encoding='utf-8')
        dotnet_platform.discover_windows_tests(self.root, selection())
        renamed = {**selection(), 'windows_tests': {'reason': 'r', 'tests': [WINDOWS_TEST + 'Gone']}}
        with self.assertRaisesRegex(ValueError, 'not declared exactly once'):
            dotnet_platform.discover_windows_tests(self.root, renamed)
        (self.root / 'DynaDocs.Tests/Lib/Twin.cs').write_text(TEST_SOURCE, encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'not declared exactly once'):
            dotnet_platform.discover_windows_tests(self.root, selection())

    def test_the_selected_suite_must_run_every_test_it_found_and_pass(self):
        self.assertEqual({'passed': 3, 'total': 3}, dotnet_platform.selected_test_counts('noise\n' + SUMMARY))
        for stdout in ('', SUMMARY * 2, SUMMARY.replace('Total:     3', 'Total:     0'),
                       SUMMARY.replace('Skipped:     0', 'Skipped:     1'),
                       SUMMARY.replace('Passed!  - Failed:     0', 'Failed!  - Failed:     1')):
            with self.assertRaises(ValueError):
                dotnet_platform.selected_test_counts(stdout)

    def verdict(self, trx, names=(WINDOWS_TEST,), stdout=SUMMARY):
        evidence = self.root / 'evidence'
        write_evidence(evidence, joined_modules(), stdout, trx)
        chosen = {**selection(), 'windows_tests': {'reason': 'r', 'tests': list(names)}}
        return dotnet_platform.selected_tests(evidence, chosen)

    def test_theory_rows_count_under_their_method_and_the_receipt_names_the_tests_that_ran(self):
        self.assertEqual({'passed': 3, 'total': 3, 'names': [WINDOWS_TEST]}, self.verdict(PASSING))
        both = (WINDOWS_TEST, OTHER_TEST)
        trx = results((WINDOWS_TEST, 'Passed'), (OTHER_TEST, 'Passed'), (WINDOWS_TEST, 'Passed'))
        self.assertEqual({'passed': 3, 'total': 3, 'names': list(both)}, self.verdict(trx, both))

    def test_a_declared_test_without_a_result_fails_although_the_summary_passes(self):
        with self.assertRaisesRegex(ValueError, f'^Declared Windows tests did not run: {OTHER_TEST}$'):
            self.verdict(PASSING, (WINDOWS_TEST, OTHER_TEST))

    def test_a_result_outside_the_declared_tests_or_not_passed_fails(self):
        rows = [(WINDOWS_TEST, 'Passed')] * 2
        for trx, message in ((results(*rows, (OTHER_TEST, 'Passed')), f'Undeclared Windows test ran: {OTHER_TEST}$'),
                             *((results(*rows, (WINDOWS_TEST, outcome)), rf'did not pass: .*\(case: 2\) is {outcome}$')
                               for outcome in ('Failed', 'Skipped', 'NotExecuted'))):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.verdict(trx)

    def test_missing_or_ambiguous_result_evidence_fails_closed(self):
        unknown = PASSING.replace(b'testId="t2"', b'testId="t9"')
        twin = results((WINDOWS_TEST, 'Passed'), (OTHER_TEST, 'Passed'), (WINDOWS_TEST, 'Passed'))
        for trx, message in ((None, 'Missing or unreadable Windows test results: windows-tests.trx'),
                             (PASSING[:-12], 'Missing or unreadable Windows test results'),
                             (unknown, rf'Undeclared Windows test ran: {WINDOWS_TEST}\(case: 2\)$'),
                             (twin.replace(b'"t1"', b'"t0"'), 'Ambiguous Windows test definition: t0'),
                             (PASSING.replace(b'</TestDefinitions>', b'<UnitTest id="t3" /></TestDefinitions>')
                              .replace(b'</Results>', b'<UnitTestResult testId="t3" outcome="Passed" /></Results>'),
                              'Undeclared Windows test ran: None$'),
                             (results(*[(WINDOWS_TEST, 'Passed')] * 2), 'hold 2 cases but the summary 3')):
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.verdict(trx)
        with self.assertRaisesRegex(ValueError, 'Missing or ambiguous selected test summary'):
            self.verdict(PASSING, stdout='')

    def test_the_native_platform_names_only_linux_and_windows(self):
        for value, expected in (('linux', 'linux'), ('win32', 'windows'), ('darwin', None)):
            with mock.patch.object(dotnet_platform.sys, 'platform', value):
                self.assertEqual(expected, dotnet_platform.native_platform())


class ScopeTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = write_dotnet_fixture(self.folder.name)

    def scope(self, platform, modules=None, files=None):
        return dotnet_platform.scope(self.root, modules or joined_modules(), files or source_files(), platform)

    def test_each_platform_measures_exactly_its_own_points_and_the_union_is_every_point(self):
        linux, receipt = self.scope('linux')
        windows, windows_receipt = self.scope('windows')
        self.assertEqual(['Lib/Mixed.cs', 'Lib/Neutral.cs'], [row['path'] for row in linux])
        self.assertEqual(['Lib/Mixed.cs', 'Lib/Native.cs'], [row['path'] for row in windows])
        self.assertEqual({'3', '4'}, set(linux[0]['lines']))
        self.assertEqual({'6', '7', '8'}, set(windows[0]['lines']))
        self.assertEqual(['Lib.Mixed::RunNative@60:80', 'Lib.Mixed.RunNative::<lambda>@90:10'],
                         [row['id'].split('|')[0] for row in windows[0]['methods']])
        self.assertEqual({'2', '4', '5'}, set(windows[1]['lines']))
        self.assertEqual({'4', '5'}, set(linux[1]['lines']))
        self.assertEqual([], evaluate_policy(linux) + evaluate_policy(windows))
        self.assertEqual({key: value for key, value in receipt.items() if key != 'platform'},
                         {key: value for key, value in windows_receipt.items() if key != 'platform'})
        for path, module in zip(SOURCES, joined_modules()):
            table = receipt['denominators'][path]
            self.assertEqual(set(module['lines']), set(table['lines']))
            self.assertEqual(set(module['branches']), set(table['branches']))
            self.assertEqual(pin(SOURCES[path]), receipt['sources'][path])
        self.assertEqual({'linux'}, set(receipt['denominators']['Lib/Neutral.cs']['lines'].values()))
        self.assertEqual({'windows'}, set(receipt['denominators']['Lib/Native.cs']['branches'].values()))

    def test_unmeasured_native_points_fail_their_own_platform_not_the_other(self):
        modules = joined_modules(linux=1, windows=0)
        self.assertEqual([], evaluate_policy(self.scope('linux', modules)[0]))
        findings = evaluate_policy(self.scope('windows', modules)[0])
        self.assertEqual({('Lib/Mixed.cs', 'line-coverage'), ('Lib/Mixed.cs', 'branch-coverage'),
                          ('Lib/Native.cs', 'line-coverage'), ('Lib/Native.cs', 'branch-coverage')},
                         {(row['path'], row['gate']) for row in findings if row['member'] is None})

    def test_a_line_shared_by_both_platforms_or_left_unowned_fails_closed(self):
        shared = joined_modules()
        shared[0]['body_owners'][0]['lines']['6'] = 1
        with self.assertRaisesRegex(ValueError, 'Mixed-platform source line: Lib/Mixed.cs:6'):
            self.scope('linux', shared)
        unowned = joined_modules()
        unowned[2]['lines']['9'] = 0
        with self.assertRaisesRegex(ValueError, 'do not account for every line: Lib/Neutral.cs'):
            self.scope('linux', unowned)

    def test_members_resolve_against_the_compiled_source_inventory(self):
        files = source_files()
        files[1]['methods'] = files[1]['methods'][:1]
        with self.assertRaisesRegex(ValueError, 'Missing native member in platform selection: Lib/Native.cs'):
            self.scope('linux', files=files)
        with self.assertRaisesRegex(ValueError, 'outside the .NET inventory: Lib/Mixed.cs'):
            self.scope('linux', files=source_files()[1:])
        neutral = [module for module in joined_modules() if module['path'] == 'Lib/Neutral.cs']
        with self.assertRaisesRegex(ValueError, 'names no measured Windows point'):
            self.scope('linux', neutral)

    def test_source_facts_are_read_from_both_target_assemblies(self):
        write_evidence(self.root / 'evidence', joined_modules())
        self.assertEqual(source_files(), dotnet_platform.read_source_files(self.root / 'evidence'))


class UnionTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = write_dotnet_fixture(self.folder.name)
        (self.root / '.gitignore').write_text('results/\n', encoding='utf-8')
        self.git('init', '-q')
        self.git('add', '-A')
        self.git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                 'commit', '-qm', 'Pinned fixture sources and reviewed selection')
        self.commit = self.git('rev-parse', 'HEAD').strip()
        self.output = self.root / 'results/denominator'
        rebuilt = mock.patch.object(dotnet_platform, 'rebuild_denominator',
                                    side_effect=lambda root, output: (joined_modules(0, 0), source_files()))
        self.rebuild = rebuilt.start()
        self.addCleanup(rebuilt.stop)

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, text=True)

    def report(self, platform, modules=None):
        rows, receipt = dotnet_platform.scope(self.root, modules or joined_modules(), source_files(), platform)
        if platform == 'windows':
            receipt['tests'] = {'passed': 1, 'total': 1}
        findings = evaluate_policy(rows)
        return {'schema': 1, 'candidate': {'commit': self.commit, 'dirty': False},
                'exitCode': 1 if findings else 0, 'measurementComplete': True,
                'stack': 'dotnet', 'gate': 'coverage', 'findings': findings, 'gaps': [],
                'collectors': {'csharp-coverage': {'facts': {'modules': rows, 'platform_selection': receipt}}}}

    def verify(self, linux=None, windows=None):
        return dotnet_platform.verify_union(linux or self.report('linux'), windows or self.report('windows'),
                                            self.root, self.output)

    def rows(self, report):
        return report['collectors']['csharp-coverage']['facts']

    def test_two_passing_gates_on_their_exact_portions_of_the_rebuilt_candidate_partition_union(self):
        result = self.verify()
        self.assertEqual(self.commit, result['commit'])
        self.assertEqual((0, 3), (result['exitCode'], result['modules']))
        self.assertEqual({'linux': {'modules': 2}, 'windows': {'modules': 2, 'tests': {'passed': 1, 'total': 1}}},
                         result['platforms'])
        self.rebuild.assert_called_once_with(self.root, self.output)

    def test_a_failing_incomplete_or_dirty_gate_on_either_platform_never_unions(self):
        for platform in dotnet_platform.PLATFORMS:
            def verify(report):
                return self.verify(**{platform: report})
            with self.subTest(platform=platform):
                failing = self.report(platform, joined_modules(**{platform: 0}))
                with self.assertRaisesRegex(ValueError, f'passing {platform} .NET coverage gate'):
                    verify(failing)
                for change in ({'measurementComplete': False}, {'gaps': [{'message': 'x'}]}, {'stack': 'python'},
                               {'candidate': {'commit': self.commit, 'dirty': True}}):
                    with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'complete clean'):
                        verify({**self.report(platform), **change})
                hidden = {**self.report(platform, joined_modules(**{platform: 0})), 'exitCode': 0, 'findings': []}
                with self.assertRaisesRegex(ValueError, 'findings disagree'):
                    verify(hidden)
        tampered = self.report('windows')
        self.rows(tampered)['modules'][0]['lines']['6'] = 0
        with self.assertRaisesRegex(ValueError, 'ownership does not account'):
            self.verify(windows=tampered)
        self.rebuild.assert_not_called()

    def test_windows_counts_the_selected_tests_it_ran_and_linux_ran_the_full_suite(self):
        unselected = self.report('windows')
        del self.rows(unselected)['platform_selection']['tests']
        empty = self.report('windows')
        self.rows(empty)['platform_selection']['tests'] = {'passed': 0, 'total': 0}
        filtered = self.report('linux')
        self.rows(filtered)['platform_selection']['tests'] = {'passed': 1, 'total': 1}
        for case in ({'windows': unselected}, {'windows': empty}, {'linux': filtered}):
            with self.subTest(case=list(case)), self.assertRaisesRegex(ValueError, 'wrong platform coverage receipt'):
                self.verify(**case)
        self.rebuild.assert_not_called()

    def test_the_union_runs_only_on_the_clean_checkout_of_one_measured_candidate(self):
        for platform in dotnet_platform.PLATFORMS:
            moved = self.report(platform)
            moved['candidate']['commit'] = '0' * 40
            with self.subTest(platform=platform), self.assertRaisesRegex(ValueError, 'different candidates'):
                self.verify(**{platform: moved})
        linux, windows = self.report('linux'), self.report('windows')
        for report in (linux, windows):
            report['candidate']['commit'] = '0' * 40
        with self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
            self.verify(linux, windows)
        (self.root / 'Lib/Neutral.cs').write_text('changed', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'clean pinned checkout'):
            self.verify()
        self.rebuild.assert_not_called()

    def test_a_gate_measured_under_another_selection_never_unions(self):
        for platform in dotnet_platform.PLATFORMS:
            other = self.report(platform)
            self.rows(other)['platform_selection']['selection_sha256'] = '0' * 64
            with self.subTest(platform=platform), \
                    self.assertRaisesRegex(ValueError, f'selection_sha256 differ: {platform}'):
                self.verify(**{platform: other})
        both = [self.report(platform) for platform in dotnet_platform.PLATFORMS]
        for report in both:
            self.rows(report)['platform_selection']['selection_sha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'selection_sha256 differ: linux'):
            self.verify(*both)

    def test_matching_omissions_or_altered_ownership_fail_against_the_rebuilt_partition(self):
        # The constructor's field-initializer line vanishes from the report and its receipt alike.
        omitted = self.report('windows')
        rows = self.rows(omitted)
        native = rows['modules'][1]
        del native['lines']['2']
        del native['body_owners'][0]['lines']['2']
        native['methods'][0].update(covered=1, total=1)
        del rows['platform_selection']['denominators']['Lib/Native.cs']['lines']['2']
        self.assertEqual([], evaluate_policy(rows['modules']))
        with self.assertRaisesRegex(ValueError, 'denominators differ: windows'):
            self.verify(windows=omitted)
        relabeled = self.report('windows')
        self.rows(relabeled)['platform_selection']['denominators']['Lib/Mixed.cs']['lines']['3'] = 'windows'
        with self.assertRaisesRegex(ValueError, 'denominators differ'):
            self.verify(windows=relabeled)
        wrong = self.report('windows')
        self.rows(wrong)['platform_selection']['platform'] = 'linux'
        with self.assertRaisesRegex(ValueError, 'wrong platform coverage receipt'):
            self.verify(windows=wrong)

    def test_a_report_missing_its_own_points_or_callables_fails_the_union(self):
        missing = self.report('windows')
        del self.rows(missing)['modules'][1]
        with self.assertRaisesRegex(ValueError, 'Incomplete platform module union: windows'):
            self.verify(windows=missing)
        stray = self.report('windows')
        native = self.rows(stray)['modules'][1]
        native['branches']['stray:5:0:1:0:0'] = 1
        native['body_owners'][1]['branches']['stray:5:0:1:0:0'] = 1
        with self.assertRaisesRegex(ValueError, 'Incomplete platform branches union: Lib/Native.cs'):
            self.verify(windows=stray)
        altered = self.report('windows')
        self.rows(altered)['modules'][1]['methods'][1]['cognitive'] = 3
        with self.assertRaisesRegex(ValueError, 'Platform callable facts differ from source'):
            self.verify(windows=altered)
        neutral = self.report('linux')
        del self.rows(neutral)['modules'][1]
        with self.assertRaisesRegex(ValueError, 'Incomplete platform module union: linux'):
            self.verify(linux=neutral)

    def test_the_command_line_needs_both_reports_and_writes_the_union_or_names_why_it_failed(self):
        output = self.root / 'results'
        windows, linux = output / 'windows.json', output / 'linux.json'
        output.mkdir(exist_ok=True)
        windows.write_text(json.dumps(self.report('windows')), encoding='utf-8')
        linux.write_text(json.dumps(self.report('linux')), encoding='utf-8')
        argv = ['dotnet_platform.py', '--linux', str(linux), '--windows', str(windows),
                '--output', str(output), '--root', str(self.root)]
        with mock.patch.object(sys, 'argv', argv):
            self.assertEqual(0, dotnet_platform.main())
        self.assertEqual(self.commit, json.loads((output / 'union.json').read_text(encoding='utf-8'))['commit'])
        (output / 'union.json').unlink()
        for absent in (argv[:1] + argv[3:], argv[:3] + argv[5:]):
            with self.subTest(argv=absent), mock.patch.object(sys, 'argv', absent), \
                    redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as parsed:
                dotnet_platform.main()
            self.assertEqual(2, parsed.exception.code)
        for report, text, reason in ((linux, None, 'linux.json'), (windows, '{}', "'schema'")):
            stderr = io.StringIO()
            if text is None:
                report.unlink()
            else:
                report.write_text(text, encoding='utf-8')
            with self.subTest(reason=reason), mock.patch.object(sys, 'argv', argv), redirect_stderr(stderr):
                self.assertEqual(2, dotnet_platform.main())
            self.assertIn(reason, stderr.getvalue())
            linux.write_text(json.dumps(self.report('linux')), encoding='utf-8')
        self.assertFalse((output / 'union.json').exists())
        self.rebuild.assert_called_once()


class DenominatorTests(unittest.TestCase):
    def test_the_rebuilt_denominator_is_this_hosts_zero_hit_join_of_the_candidate(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            output = root / 'denominator'

            def run(command, check):
                write_evidence(output, joined_modules(0, 0))
                return subprocess.CompletedProcess(command, 0)
            with mock.patch.object(dotnet_platform.subprocess, 'run', side_effect=run) as campaign:
                modules, files = dotnet_platform.rebuild_denominator(root, output)
            command = campaign.call_args.args[0]
            self.assertEqual([sys.executable, '--denominator', '--root', str(root), '--result-root', str(output)],
                             [command[0], *command[2:]])
            self.assertEqual('csharp_coverage.py', Path(command[1]).name)
            self.assertEqual(joined_modules(0, 0), modules)
            self.assertEqual(source_files(), files)
            with mock.patch.object(dotnet_platform.subprocess, 'run',
                                   return_value=subprocess.CompletedProcess([], 2)), \
                    self.assertRaisesRegex(ValueError, 'denominator rebuild failed'):
                dotnet_platform.rebuild_denominator(root, output)


if __name__ == '__main__':
    unittest.main()

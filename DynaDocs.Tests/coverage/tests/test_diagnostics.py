"""Native analyzer rows are evidence, not duplicate maintained-source obligations."""
import json
import sys
import tempfile
import unittest
from pathlib import Path, PurePosixPath
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_diagnostics import normalize_csharp_diagnostics
from gate_collect import (_collect_analyzer_reports, _generated_diagnostic_findings,
                          _generated_projects)


class DiagnosticTests(unittest.TestCase):
    def fixture(self, root, path, rule='IDE0060', suppressed=False, level='error'):
        row = {'ruleId': rule, 'level': level, 'message': 'native message',
               'locations': [{'resultFile': {'uri': (root / path).as_uri(), 'region': {
                   'startLine': 2, 'startColumn': 3, 'endLine': 2, 'endColumn': 5}}}]}
        if suppressed:
            row['suppressionStates'] = ['suppressedInSource']
        return row

    def relative_fixture(self, path, rule='CS0219', suppressed=True):
        row = {'ruleId': rule, 'level': 'error', 'message': 'generated warning',
               'locations': [{'resultFile': {'uri': path, 'region': {
                   'startLine': 2, 'startColumn': 3, 'endLine': 2, 'endColumn': 5}}}]}
        if suppressed:
            row['suppressionStates'] = ['suppressedInSource']
        return row

    def rooted_fixture(self, root, project, diagnostic):
        """Retained SARIF pins the worktree that produced it; replay resolves under the running root."""
        anchor = f'/{PurePosixPath(project).parent}/'
        for location in diagnostic['locations']:
            file = location['resultFile']
            head, marker, tail = file['uri'].partition(anchor)
            if marker and head.startswith('file:'):
                file['uri'] = root.as_uri() + marker + tail
        return diagnostic

    def test_analyzer_collection_isolates_each_project_sarif_from_project_references(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)

            class Log:
                def __init__(self):
                    self.commands = []

                def run(inner, name, argv):
                    inner.commands.append((name, argv))
                    error_log = next((value.split('=', 1)[1] for value in map(str, argv)
                                      if value.startswith('-p:ErrorLog=')), None)
                    if error_log is None:
                        return {'exit_code': 0}
                    isolated = '-p:BuildProjectReferences=false' in argv
                    path = 'Tests/SubjectTests.cs' if isolated else 'Product/Subject.cs'
                    Path(error_log).write_text(json.dumps({'runs': [{'results': [
                        self.fixture(output, path)
                    ]}]}))
                    return {'exit_code': 1}

            log = Log()
            collector = SimpleNamespace(output=output, log=log,
                                        project_rows=[{'path': 'Tests/Tests.csproj'}])
            _, raw, errors = _collect_analyzer_reports(collector)
            self.assertEqual([], errors)
            uri = raw[0]['diagnostic']['locations'][0]['resultFile']['uri']
            self.assertTrue(uri.endswith('/Tests/SubjectTests.cs'), uri)
            self.assertEqual(['analyzers-0-prepare', 'analyzers-0'],
                             [name for name, _ in log.commands])

    def test_generated_file_index_keeps_every_owning_project_in_evaluation_order(self):
        rows = [{'project': 'Product.csproj', 'generated_files': ['obj/Shared.g.cs', 'obj/Only.g.cs']},
                {'project': 'Tests.csproj', 'generated_files': ['obj/Shared.g.cs']},
                {'project': 'Empty.csproj', 'generated_files': []}]

        self.assertEqual({'obj/Shared.g.cs': ['Product.csproj', 'Tests.csproj'],
                          'obj/Only.g.cs': ['Product.csproj']}, _generated_projects(rows))

    def test_only_unsuppressed_gate_level_generated_diagnostics_become_findings(self):
        rows = [{'rule': 'CS0219', 'level': 'warning', 'suppression_states': []},
                {'rule': 'CS0162', 'level': 'error', 'suppression_states': ['suppressedInSource']},
                {'rule': 'CA1822', 'level': 'note', 'suppression_states': []}]

        findings = _generated_diagnostic_findings(rows)

        self.assertEqual([{'gate': 'generated-build-diagnostic', 'rule': 'CS0219', 'level': 'warning',
                           'suppression_states': []}], findings)

    def test_project_relative_feature_requires_unique_same_project_generated_origin(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            feature = root / 'Tests/Features/example.feature'
            feature.parent.mkdir(parents=True)
            feature.write_text('Feature: example')
            generated_path = 'Tests/obj/Debug/net10.0/Features/example.feature.cs'
            rows = [{'project': 'Tests/Tests.csproj',
                     'diagnostic': self.relative_fixture('Features/example.feature')}]
            report = normalize_csharp_diagnostics(
                root, rows, set(), {generated_path: ['Tests/Tests.csproj']})
            self.assertEqual([], report['errors'])
            generated = report['generated'][0]
            self.assertEqual(['suppressedInSource'], generated['suppression_states'])
            self.assertEqual('Tests/Features/example.feature', generated['locations'][0]['path'])
            self.assertEqual(generated_path, generated['locations'][0]['generated_origin'])
            self.assertEqual([generated_path], generated['generated_origins'])

    def test_unsuppressed_project_relative_generated_diagnostic_stays_visible(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            feature = root / 'Tests/Features/example.feature'
            feature.parent.mkdir(parents=True)
            feature.write_text('Feature: example')
            generated_path = 'Tests/obj/Debug/net10.0/Features/example.feature.cs'
            rows = [{'project': 'Tests/Tests.csproj',
                     'diagnostic': self.relative_fixture('Features/example.feature', suppressed=False)}]
            report = normalize_csharp_diagnostics(
                root, rows, set(), {generated_path: ['Tests/Tests.csproj']})
            self.assertEqual([], report['errors'])
            self.assertEqual([], report['generated'][0]['suppression_states'])

    def test_relative_feature_missing_wrong_project_ambiguous_and_traversal_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            feature = root / 'Tests/Features/example.feature'
            feature.parent.mkdir(parents=True)
            feature.write_text('Feature: example')
            project = 'Tests/Tests.csproj'
            path = 'Tests/obj/Debug/net10.0/Features/example.feature.cs'
            cases = [
                ('missing', 'Features/example.feature', {}),
                ('wrong-project', 'Features/example.feature', {path: ['Other/Tests.csproj']}),
                ('ambiguous', 'Features/example.feature', {
                    path: [project],
                    'Tests/obj/Release/net10.0/Features/example.feature.cs': [project],
                }),
                ('traversal', '../Features/example.feature', {path: [project]}),
            ]
            for name, uri, generated in cases:
                with self.subTest(name=name):
                    rows = [{'project': project, 'diagnostic': self.relative_fixture(uri)}]
                    report = normalize_csharp_diagnostics(root, rows, set(), generated)
                    self.assertEqual(1, len(report['errors']))
                    self.assertEqual([], report['generated'])

    def test_retained_c89_reqnroll_rows_normalize_without_loss_or_uri_errors(self):
        root = Path(__file__).resolve().parents[3]
        retained = root / 'DynaDocs.Tests/coverage/results/assurance/run-3510c46004a847c4b24a94e4866b6743'
        project = 'DynaDocs.Tests/DynaDocs.Tests.csproj'
        sarif = json.loads((retained / 'raw/analyzers-0.sarif').read_text(encoding='utf-8-sig'))
        diagnostics = [item for run in sarif['runs'] for item in run.get('results', [])]
        rows = [{'project': project, 'diagnostic': self.rooted_fixture(root, project, item)}
                for item in diagnostics]
        evidence = json.loads((retained / 'report.json').read_text(encoding='utf-8-sig'))
        projects = evidence['collectors']['csharp-source']['facts']['projects']
        generated = {}
        for project in projects:
            for path in project['generated_files']:
                generated.setdefault(path, []).append(project['project'])
        inventory = json.loads((retained / 'inventory.json').read_text(encoding='utf-8-sig'))
        maintained = {row['path'] for row in inventory['sources'] if row['language'] == 'cs'}
        report = normalize_csharp_diagnostics(root, rows, maintained, generated)
        self.assertEqual(229, report['raw_count'])
        self.assertEqual([], report['errors'])
        normalized = [*report['findings'], *report['generated']]
        gate_level_count = sum(item['level'] in ('warning', 'error') for item in diagnostics)
        self.assertEqual(gate_level_count, sum(len(row['witnesses']) for row in normalized))
        feature_witnesses = sum(len(row['witnesses']) for row in report['generated']
                                if row['locations'][0]['path'].endswith('.feature'))
        self.assertEqual(105, feature_witnesses)

    def test_duplicates_retain_both_build_witnesses_and_same_stems_stay_distinct(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': project, 'diagnostic': self.fixture(root, path)}
                    for project, path in [('Product.csproj', 'A/File.cs'), ('Tests.csproj', 'A/File.cs'),
                                          ('Product.csproj', 'B/File.cs')]]
            report = normalize_csharp_diagnostics(root, rows, {'A/File.cs', 'B/File.cs'}, {})
            self.assertEqual(2, len(report['findings']))
            self.assertEqual(2, len(report['findings'][0]['witnesses']))
            self.assertEqual([], report['errors'])

    def test_configured_dead_code_errors_remain_gate_findings(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': 'Product.csproj', 'diagnostic': self.fixture(root, 'Subject.cs', rule)}
                    for rule in ('IDE0051', 'IDE0052', 'IDE0060')]
            report = normalize_csharp_diagnostics(root, rows, {'Subject.cs'}, {})
            self.assertEqual(['IDE0051', 'IDE0052', 'IDE0060'],
                             [row['rule'] for row in report['findings']])

    def test_note_diagnostics_remain_raw_evidence_but_are_not_gate_findings(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': 'Product.csproj',
                     'diagnostic': self.fixture(root, 'Subject.cs', 'CA1822', level=level)}
                    for level in ('note', 'none')]
            report = normalize_csharp_diagnostics(root, rows, {'Subject.cs'}, {})
            self.assertEqual(2, report['raw_count'])
            self.assertEqual([], report['findings'])
            self.assertEqual([], report['errors'])

    def test_unknown_and_malformed_levels_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': 'Product.csproj',
                     'diagnostic': self.fixture(root, 'Subject.cs', level=level)}
                    for level in ('mystery', {'unexpected': True})]
            report = normalize_csharp_diagnostics(root, rows, {'Subject.cs'}, {})
            self.assertEqual(2, report['raw_count'])
            self.assertEqual([], report['findings'])
            self.assertEqual([0, 1], [row['native_row'] for row in report['errors']])
            self.assertTrue(all(row['message'] == 'Unrecognized native analyzer level'
                                for row in report['errors']))

    def test_only_exact_semantic_generated_identity_can_be_excluded(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': 'Product.csproj', 'diagnostic': self.fixture(root, 'obj/Real.g.cs', 'CS0162', True)},
                    {'project': 'Product.csproj', 'diagnostic': self.fixture(root, 'Fake.generated.cs')}]
            report = normalize_csharp_diagnostics(root, rows, {'Fake.generated.cs'}, {'obj/Real.g.cs': ['Product.csproj']})
            self.assertEqual(1, len(report['generated']))
            self.assertEqual('Fake.generated.cs', report['findings'][0]['path'])
            self.assertEqual(['suppressedInSource'], report['generated'][0]['suppression_states'])

    def test_unknown_output_and_maintained_suppression_are_not_green(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = [{'project': 'Product.csproj', 'diagnostic': self.fixture(root, 'obj/Unknown.g.cs')},
                    {'project': 'Product.csproj', 'diagnostic': self.fixture(root, 'Subject.cs', suppressed=True)}]
            report = normalize_csharp_diagnostics(root, rows, {'Subject.cs'}, {})
            self.assertEqual(1, len(report['errors']))
            self.assertEqual('maintained-diagnostic-suppression', report['findings'][0]['gate'])

"""Native analyzer rows are evidence, not duplicate maintained-source obligations."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_diagnostics import normalize_csharp_diagnostics


class DiagnosticTests(unittest.TestCase):
    def fixture(self, root, path, rule='IDE0060', suppressed=False):
        row = {'ruleId': rule, 'level': 'error', 'message': 'native message',
               'locations': [{'resultFile': {'uri': (root / path).as_uri(), 'region': {
                   'startLine': 2, 'startColumn': 3, 'endLine': 2, 'endColumn': 5}}}]}
        if suppressed:
            row['suppressionStates'] = ['suppressedInSource']
        return row

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


"""Incomplete role joins retain source obligations and expose their exact gap."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_inventory import assemble_inventory, dependency_cycles, test_project_role


class GateInventoryTests(unittest.TestCase):
    def test_missing_restore_import_cannot_turn_test_project_into_product(self):
        with self.assertRaisesRegex(ValueError, 'test SDK'):
            test_project_role('', ['Microsoft.NET.Test.Sdk'])
        self.assertTrue(test_project_role('true', ['Microsoft.NET.Test.Sdk']))
        self.assertFalse(test_project_role('', ['Mono.Cecil']))

    def test_compile_overlap_is_an_error_and_never_exempts_tooling(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'Tool.cs').write_text('class Tool {}', encoding='utf-8')
            projects = [{'path': 'Tool.csproj', 'test': False, 'compile': ['Tool.cs']},
                        {'path': 'Tests.csproj', 'test': True, 'compile': ['Tool.cs']}]
            report = assemble_inventory(root, ['Tool.cs'], projects, [])
            self.assertEqual('unknown', report['sources'][0]['role'])
            self.assertEqual('ambiguous-compile-role', report['errors'][0]['type'])
            self.assertEqual('Tool.cs', report['errors'][0]['path'])

    def test_missing_compilation_and_undiscovered_test_name_stay_visible(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'Lost.cs').write_text('class Lost {}', encoding='utf-8')
            (root / 'test_fake.py').write_text('print(1)', encoding='utf-8')
            report = assemble_inventory(root, ['Lost.cs', 'test_fake.py'], [], [])
            self.assertEqual(['Lost.cs'], [row['path'] for row in report['errors']])
            self.assertEqual('target', report['sources'][1]['role'])

    def test_discovered_tests_join_exact_source_identity(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'assertions.py').write_text('pass', encoding='utf-8')
            report = assemble_inventory(root, ['assertions.py'], [], [{'id': 'C.test', 'file': 'assertions.py'}])
            self.assertEqual('test', report['sources'][0]['role'])
            with self.assertRaisesRegex(ValueError, 'discovered test'):
                assemble_inventory(root, ['assertions.py'], [], [{'id': 'C.test', 'file': 'missing.py'}])

    def test_cycles_keep_self_edges_and_disjoint_components(self):
        edges = [['A', 'B'], ['B', 'A'], ['B', 'C'], ['D', 'D']]
        self.assertEqual([['A', 'B'], ['D']], dependency_cycles(edges))
        self.assertEqual([], dependency_cycles([['A', 'B'], ['B', 'C']]))


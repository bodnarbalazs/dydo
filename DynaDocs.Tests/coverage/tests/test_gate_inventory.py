"""Incomplete role joins retain source obligations and expose their exact gap."""
import sys
import tempfile
import unittest
import hashlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_inventory import assembly_path, assemble_inventory, dependency_cycles, test_project_role


class GateInventoryTests(unittest.TestCase):
    def test_missing_restore_import_cannot_turn_test_project_into_product(self):
        with self.assertRaisesRegex(ValueError, 'test SDK'):
            test_project_role('', ['Microsoft.NET.Test.Sdk'])
        self.assertTrue(test_project_role('true', ['Microsoft.NET.Test.Sdk']))
        self.assertFalse(test_project_role('', ['Mono.Cecil']))

    def test_assembly_path_is_root_relative_and_cannot_escape(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder).resolve()
            target = root / 'bin/Debug/tool.dll'
            self.assertEqual('bin/Debug/tool.dll', assembly_path(root, target))
            with self.assertRaisesRegex(ValueError, 'escapes'):
                assembly_path(root, root.parent / 'foreign.dll')

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

    def test_derived_copy_is_excluded_only_with_matching_source_and_tested_producer(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / 'DynaDocs.Tests/coverage/gap_check.py'
            derived = root / 'dydo/reference/gap-check.example.py'
            producer = root / 'DynaDocs.Tests/coverage/sync_testing_example.py'
            test = root / 'DynaDocs.Tests/coverage/tests/test_sync_testing_example.py'
            for path in (source, derived, producer, test):
                path.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(b'print(1)\n')
            derived.write_bytes(source.read_bytes())
            producer.write_text('def sync(): pass\n', encoding='utf-8')
            test.write_text('class TestCase: pass\n', encoding='utf-8')
            paths = [path.relative_to(root).as_posix() for path in (source, derived, producer, test)]
            discovery = [{'id': 'sync-test', 'file': paths[-1]}]
            report = assemble_inventory(root, paths, [], discovery)
            excluded = report['excluded'][0]
            self.assertEqual(paths[1], excluded['path'])
            self.assertEqual('derived-copy', excluded['reason'])
            self.assertEqual(paths[0], excluded['origin']['source'])
            self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(),
                             excluded['origin']['canonicalSourceSha256'])
            self.assertNotIn(paths[1], [row['path'] for row in report['sources']])
            derived.write_bytes(b'print(2)\n')
            report = assemble_inventory(root, paths, [], discovery)
            self.assertIn(paths[1], [row['path'] for row in report['sources']])
            self.assertIn('derived-copy-diverged', [row['type'] for row in report['errors']])

    def test_native_packet_sources_are_excluded_with_manifest_origin(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            prefix = Path('dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence')
            manifest = root / prefix / 'SHA256SUMS'
            fixture = root / prefix / 'fixture/Program.cs'
            fixture.parent.mkdir(parents=True)
            fixture.write_text('class Probe {}\n', encoding='utf-8')
            manifest.write_text('fixture evidence\n', encoding='utf-8')
            paths = [manifest.relative_to(root).as_posix(), fixture.relative_to(root).as_posix()]
            report = assemble_inventory(root, paths, [], [])
            self.assertEqual([], report['sources'])
            self.assertEqual('native-evidence-fixture', report['excluded'][0]['reason'])
            self.assertEqual(paths[0], report['excluded'][0]['origin']['manifest'])
            self.assertEqual(hashlib.sha256(manifest.read_bytes()).hexdigest(),
                             report['excluded'][0]['origin']['manifestSha256'])

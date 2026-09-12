"""Assurance dependency evidence stays exact and independent of mutation tooling."""
import hashlib
import importlib.metadata
import inspect
import json
import platform
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import gate_versions
from gate_collect import Collectors
from gate_run import CommandLog

INSTALLED = importlib.metadata.version('six')


def _write(path, payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload), encoding='utf-8')


def _javascript_tree(root, declared, locked, installed):
    _write(root / 'package.json', {'dependencies': declared})
    _write(root / 'package-lock.json', {'packages': locked})
    for relative, version in installed.items():
        _write(root / relative / 'package.json', {'version': version})


def _dotnet_tree(root, dependencies, libraries):
    _write(root / 'metrics/packages.lock.json', {'dependencies': dependencies})
    _write(root / 'metrics/obj/project.assets.json', {'libraries': libraries})


def _native_version(command):
    return subprocess.run(command, capture_output=True, text=True,
                          encoding='utf-8', check=True, timeout=300).stdout.strip()


class GateVersionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_version_collector_has_no_mutation_configuration_dependency(self):
        source = inspect.getsource(gate_versions.collect_versions).lower()
        self.assertNotIn("stryker", source)
        self.assertNotIn("mutation", source)

    def test_python_lock_pins_are_measured_against_the_installed_distribution(self):
        lock = self.root / 'requirements.lock'
        lock.write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        runtime_pin = self.root / '.python-version'
        runtime_pin.write_text('3.12.10\n', encoding='utf-8')

        facts = gate_versions.python_versions(self.root)

        self.assertEqual([{'name': 'six', 'version': INSTALLED}], facts['packages'])
        self.assertEqual('3.12.10', facts['declared_runtime'])
        self.assertEqual(platform.python_version(), facts['runtime'])
        self.assertEqual('CPython', facts['implementation'])
        self.assertEqual(hashlib.sha256(runtime_pin.read_bytes()).hexdigest(), facts['runtime_pin_sha256'])
        self.assertEqual(hashlib.sha256(lock.read_bytes()).hexdigest(), facts['lock_sha256'])

    def test_unpinned_absent_and_mismatched_python_requirements_fail_closed(self):
        lock = self.root / 'requirements.lock'
        (self.root / '.python-version').write_text('3.12.10\n', encoding='utf-8')
        cases = [(f'six>={INSTALLED}\n', ValueError), ('dydo-absent-distribution==1.0\n',
                 importlib.metadata.PackageNotFoundError), ('six==0.0.1\n', ValueError)]
        for text, failure in cases:
            lock.write_text(text, encoding='utf-8')
            with self.subTest(lock=text.strip()), self.assertRaises(failure):
                gate_versions.python_versions(self.root)

    def test_python_runtime_pin_must_be_one_exact_single_line_version(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        pin = self.root / '.python-version'
        cases = (None, '', '3.12', '3.12.x', '3.12.11\n', '>=3.12',
                 '3.12.10\n3.12.11\n', ' 3.12.10\n')
        for value in cases:
            if pin.exists():
                pin.unlink()
            if value is not None:
                pin.write_text(value, encoding='utf-8')
            with self.subTest(pin=value), self.assertRaises((OSError, ValueError)):
                gate_versions.python_versions(self.root)

    def test_python_runtime_accepts_any_cpython_312_patch_and_reports_the_observed_version(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        (self.root / '.python-version').write_text('3.12.10\n', encoding='utf-8')

        with patch.object(gate_versions.platform, 'python_implementation', return_value='CPython'), \
             patch.object(gate_versions.platform, 'python_version', return_value='3.12.99'), \
             patch.object(gate_versions.sys, 'version_info', (3, 12, 99)):
            facts = gate_versions.python_versions(self.root)

        self.assertEqual('3.12.10', facts['declared_runtime'])
        self.assertEqual('3.12.99', facts['runtime'])

    def test_python_runtime_rejects_non_cpython_and_wrong_series(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        (self.root / '.python-version').write_text('3.12.10\n', encoding='utf-8')
        for implementation, version in (('PyPy', (3, 12, 10)), ('CPython', (3, 11, 10))):
            with self.subTest(implementation=implementation, version=version), \
                 patch.object(gate_versions.platform, 'python_implementation', return_value=implementation), \
                 patch.object(gate_versions.sys, 'version_info', version), \
                 self.assertRaisesRegex(ValueError, 'requires CPython 3.12'):
                gate_versions.python_versions(self.root)

    def test_javascript_lock_requires_every_declared_exact_version(self):
        _javascript_tree(self.root, {'tool': '1.0.0'},
                         {'': {}, 'node_modules/tool': {'version': '2.0.0'}}, {})
        with self.assertRaisesRegex(ValueError, "disagree"):
            gate_versions.javascript_versions(self.root)

    def test_installed_javascript_packages_are_measured_against_the_lock(self):
        _javascript_tree(self.root, {'tool': '1.0.0'},
                         {'': {}, 'node_modules/tool': {'version': '1.0.0'},
                          'node_modules/native': {'version': '2.0.0', 'optional': True,
                                                  'os': ['linux'], 'cpu': ['arm64']}},
                         {'node_modules/tool': '1.0.0'})

        facts = gate_versions.javascript_versions(self.root)

        self.assertEqual([{'path': 'node_modules/tool', 'version': '1.0.0',
                           'manifest_sha256': hashlib.sha256(
                               (self.root / 'node_modules/tool/package.json').read_bytes()).hexdigest()}],
                         facts['packages'])
        self.assertEqual([{'path': 'node_modules/native', 'optional': True,
                           'os': ['linux'], 'cpu': ['arm64']}], facts['uninstalled_optional'])

    def test_absent_required_and_mismatched_javascript_packages_fail_closed(self):
        declared = {'tool': '1.0.0'}
        locked = {'': {}, 'node_modules/tool': {'version': '1.0.0'}}
        cases = [({**locked, 'node_modules/gone': {'version': '1.0.0'}}, {'node_modules/tool': '1.0.0'},
                  'Missing required'), (locked, {'node_modules/tool': '9.9.9'}, 'resolved version mismatch')]
        for index, (lock, installed, message) in enumerate(cases):
            folder = self.root / f'case-{index}'
            _javascript_tree(folder, declared, lock, installed)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                gate_versions.javascript_versions(folder)

    def test_restored_dotnet_closure_must_equal_the_locked_closure(self):
        locked = {'net10.0': {'Sonar.Analyzer': {'resolved': '1.2.3', 'contentHash': 'HASH'}}}
        restored = {'Sonar.Analyzer/1.2.3': {'type': 'package', 'sha512': 'HASH'},
                    'GateMetrics/1.0.0': {'type': 'project'}}
        _dotnet_tree(self.root, locked, restored)

        facts = gate_versions.dotnet_versions(self.root)

        self.assertEqual({'sonar.analyzer/1.2.3': 'HASH'}, facts['packages'])
        self.assertEqual(hashlib.sha256(
            (self.root / 'metrics/packages.lock.json').read_bytes()).hexdigest(), facts['lock_sha256'])

    def test_unexpected_dotnet_framework_or_closure_fails_closed(self):
        locked = {'net10.0': {'Sonar.Analyzer': {'resolved': '1.2.3', 'contentHash': 'HASH'}}}
        cases = [({'net9.0': {}}, {}, 'target framework'),
                 (locked, {'Sonar.Analyzer/1.2.3': {'type': 'package', 'sha512': 'OTHER'}}, 'differs from the lock')]
        for index, (dependencies, libraries, message) in enumerate(cases):
            folder = self.root / f'case-{index}'
            _dotnet_tree(folder, dependencies, libraries)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                gate_versions.dotnet_versions(folder)

    def runner(self, coverage, label):
        repository = Path(__file__).resolve().parents[3]
        collector = Collectors.__new__(Collectors)
        collector.root = repository
        collector.coverage = coverage
        collector.output = self.root / label
        collector.log = CommandLog(repository, collector.output / 'commands')
        return collector

    def test_absent_dependency_evidence_is_reported_for_each_managed_stack(self):
        answer = gate_versions.collect_versions(self.runner(self.root / 'empty', 'absent'))

        self.assertEqual('error', answer['status'])
        self.assertEqual({'python', 'javascript', 'dotnet'},
                         {row['stack'] for row in answer['errors']})
        self.assertEqual({'node', 'sdk'}, set(answer['facts']))

    def test_collected_versions_carry_the_actual_native_runtime_evidence(self):
        repository = Path(__file__).resolve().parents[3]

        answer = gate_versions.collect_versions(
            self.runner(repository / 'DynaDocs.Tests/coverage', 'versions'))

        for name, command, pin in (('node', ['node', '--version'], 'v22.13.0'),
                                   ('sdk', ['dotnet', '--version'], '10.0.300')):
            observed = _native_version(command)
            with self.subTest(name=name):
                self.assertEqual(observed, answer['facts'][name]['version'])
                self.assertEqual(observed != pin,
                                 any(row['stack'] == name for row in answer['errors']))
        accounted = set(answer['facts']) | {row['stack'] for row in answer['errors']}
        self.assertEqual({'python', 'javascript', 'dotnet', 'node', 'sdk'}, accounted)


if __name__ == "__main__":
    unittest.main()

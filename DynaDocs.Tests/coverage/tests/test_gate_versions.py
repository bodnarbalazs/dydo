"""Assurance dependency evidence stays exact and independent of mutation tooling."""
import hashlib
import importlib.metadata
import inspect
import json
import os
import platform
import re
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


def _key(text, indent):
    return ' ' * indent + (f"'{text}':" if text.startswith('@') else f'{text}:')


def _pnpm_lock(importer, packages, snapshots):
    """A lockfile v9 in pnpm's own shape: quoted scoped keys, `{}` leaves, flow lists, sequence items."""
    lines = ["lockfileVersion: '9.0'", '', 'settings:', '  autoInstallPeers: true', '', 'importers:', '',
             '  .:', '    dependencies:']
    for name, (specifier, version) in importer.items():
        lines += [_key(name, 6), f'        specifier: {specifier}', f'        version: {version}']
    lines += ['', 'packages:', '']
    for key, fields in packages.items():
        lines += [_key(key, 2), '    resolution: {integrity: sha512-x}', *(f'    {field}' for field in fields), '']
    lines += ['snapshots:', '']
    for key, fields in snapshots.items():
        lines += [_key(key, 2)[:-1] + ': {}'] if not fields else [_key(key, 2), *(f'    {field}' for field in fields)]
        lines.append('')
    return '\n'.join(lines)


def _link(target, link):
    """A pnpm link: a symlink, or on Windows the junction pnpm makes."""
    link.parent.mkdir(parents=True, exist_ok=True)
    if sys.platform == 'win32':
        import _winapi
        _winapi.CreateJunction(str(target), str(link))
    else:
        os.symlink(target, link, target_is_directory=True)


def _javascript_tree(root, declared, lock, store, links=()):
    """A coverage folder: its manifest and pnpm lock, real package folders and the links between them."""
    _write(root / 'package.json', {'dependencies': declared})
    root.joinpath('pnpm-lock.yaml').write_text(lock, encoding='utf-8')
    for relative, (name, version) in store.items():
        _write(root / relative / 'package.json', {'name': name, 'version': version})
    for target, link in links:
        _link(root / target, root / link)


TOOL = 'node_modules/.pnpm/tool@1.0.0/node_modules/tool'
SCOPED = 'node_modules/.pnpm/@scope+dep@3.0.0_peer@1.0.0/node_modules/@scope/dep'
TOOL_LINK = (TOOL, 'node_modules/tool')


def _tool_lock(**extra):
    packages = {'tool@1.0.0': [], '@scope/dep@3.0.0': [],
                'native@2.0.0': ['cpu: [arm64]', "os: ['!win32']"], 'wasm@1.0.0': [], **extra}
    snapshots = {'tool@1.0.0': ['dependencies:', "  '@scope/dep': 3.0.0(peer@1.0.0)",
                                'transitivePeerDependencies:', '  - supports-color'],
                 '@scope/dep@3.0.0(peer@1.0.0)': [], 'native@2.0.0': ['optional: true'],
                 'wasm@1.0.0': ['optional: true'], **{key: [] for key in extra}}
    return _pnpm_lock({'tool': ('1.0.0', '1.0.0')}, packages, snapshots)


LEAF_PACKAGES = (('tool', '1.0.0'), ('other', '1.0.0'), ('leaf', '1.0.0'), ('leaf', '2.0.0'))


def _store(name, version):
    return f'node_modules/.pnpm/{name}@{version}/node_modules/{name}'


def _sibling(owner, dependency):
    """The link a store package's folder holds beside it for one of its dependencies."""
    return f'node_modules/.pnpm/{owner}@1.0.0/node_modules/{dependency}'


def _leaf_lock(tool_needs_leaf=True):
    """The reviewer's fixture: tool pins leaf 1.0.0 and other pins leaf 2.0.0."""
    tool = ['dependencies:', '  leaf: 1.0.0'] if tool_needs_leaf else []
    return _pnpm_lock({'tool': ('1.0.0', '1.0.0'), 'other': ('1.0.0', '1.0.0')},
                      {f'{name}@{version}': [] for name, version in LEAF_PACKAGES},
                      {'tool@1.0.0': tool, 'other@1.0.0': ['dependencies:', '  leaf: 2.0.0'],
                       'leaf@1.0.0': [], 'leaf@2.0.0': []})


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
        runtime_pin.write_text('3.14.4\n', encoding='utf-8')

        facts = gate_versions.python_versions(self.root)

        self.assertEqual([{'name': 'six', 'version': INSTALLED}], facts['packages'])
        self.assertEqual('3.14.4', facts['declared_runtime'])
        self.assertEqual(platform.python_version(), facts['runtime'])
        self.assertEqual('CPython', facts['implementation'])
        self.assertEqual(hashlib.sha256(runtime_pin.read_bytes()).hexdigest(), facts['runtime_pin_sha256'])
        self.assertEqual(hashlib.sha256(lock.read_bytes()).hexdigest(), facts['lock_sha256'])

    def test_unpinned_absent_and_mismatched_python_requirements_fail_closed(self):
        lock = self.root / 'requirements.lock'
        (self.root / '.python-version').write_text('3.14.4\n', encoding='utf-8')
        cases = [(f'six>={INSTALLED}\n', ValueError), ('dydo-absent-distribution==1.0\n',
                 importlib.metadata.PackageNotFoundError), ('six==0.0.1\n', ValueError)]
        for text, failure in cases:
            lock.write_text(text, encoding='utf-8')
            with self.subTest(lock=text.strip()), self.assertRaises(failure):
                gate_versions.python_versions(self.root)

    def test_python_runtime_pin_must_be_one_exact_single_line_version(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        pin = self.root / '.python-version'
        cases = (None, '', '3.14', '3.14.x', '3.14.5\n', '>=3.14',
                 '3.14.4\n3.14.5\n', ' 3.14.4\n')
        for value in cases:
            if pin.exists():
                pin.unlink()
            if value is not None:
                pin.write_text(value, encoding='utf-8')
            with self.subTest(pin=value), self.assertRaises((OSError, ValueError)):
                gate_versions.python_versions(self.root)

    def test_python_runtime_accepts_any_cpython_314_patch_and_reports_the_observed_version(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        (self.root / '.python-version').write_text('3.14.4\n', encoding='utf-8')

        with patch.object(gate_versions.platform, 'python_implementation', return_value='CPython'), \
             patch.object(gate_versions.platform, 'python_version', return_value='3.14.99'), \
             patch.object(gate_versions.sys, 'version_info', (3, 14, 99)):
            facts = gate_versions.python_versions(self.root)

        self.assertEqual('3.14.4', facts['declared_runtime'])
        self.assertEqual('3.14.99', facts['runtime'])

    def test_python_runtime_rejects_non_cpython_and_wrong_series(self):
        (self.root / 'requirements.lock').write_text(f'six=={INSTALLED}\n', encoding='utf-8')
        (self.root / '.python-version').write_text('3.14.4\n', encoding='utf-8')
        for implementation, version in (('PyPy', (3, 14, 10)), ('CPython', (3, 11, 10))):
            with self.subTest(implementation=implementation, version=version), \
                 patch.object(gate_versions.platform, 'python_implementation', return_value=implementation), \
                 patch.object(gate_versions.sys, 'version_info', version), \
                 self.assertRaisesRegex(ValueError, 'requires CPython 3.14'):
                gate_versions.python_versions(self.root)

    def test_javascript_lock_requires_every_declared_exact_version(self):
        cases = {'specifier': {'tool': ('2.0.0', '1.0.0')}, 'resolved': {'tool': ('1.0.0', '2.0.0(peer@1.0.0)')},
                 'absent': {}, 'undeclared': {'tool': ('1.0.0', '1.0.0'), 'extra': ('1.0.0', '1.0.0')}}
        for label, importer in cases.items():
            folder = self.root / label
            _javascript_tree(folder, {'tool': '1.0.0'}, _pnpm_lock(importer, {}, {}), {})
            with self.subTest(label), self.assertRaisesRegex(ValueError, 'disagree'):
                gate_versions.javascript_versions(folder)

    def test_installed_javascript_packages_are_measured_against_the_pnpm_lock(self):
        # The scoped package is linked from tool's own node_modules too: a link is not a package.
        _javascript_tree(self.root, {'tool': '1.0.0'}, _tool_lock(),
                         {TOOL: ('tool', '1.0.0'), SCOPED: ('@scope/dep', '3.0.0')},
                         [TOOL_LINK, (SCOPED, 'node_modules/.pnpm/tool@1.0.0/node_modules/@scope/dep')])

        facts = gate_versions.javascript_versions(self.root)

        digest = {path: hashlib.sha256((self.root / path / 'package.json').read_bytes()).hexdigest()
                  for path in (TOOL, SCOPED)}
        self.assertEqual([{'path': SCOPED, 'package': '@scope/dep@3.0.0', 'manifest_sha256': digest[SCOPED]},
                          {'path': TOOL, 'package': 'tool@1.0.0', 'manifest_sha256': digest[TOOL]}],
                         facts['packages'])
        self.assertEqual([{'package': 'native@2.0.0', 'optional': True, 'os': ['!win32'], 'cpu': ['arm64']},
                          {'package': 'wasm@1.0.0', 'optional': True, 'os': None, 'cpu': None}],
                         facts['uninstalled_optional'])
        self.assertEqual(hashlib.sha256((self.root / 'pnpm-lock.yaml').read_bytes()).hexdigest(),
                         facts['lock_sha256'])

    def test_absent_required_unlocked_and_mislinked_javascript_packages_fail_closed(self):
        store = {TOOL: ('tool', '1.0.0'), SCOPED: ('@scope/dep', '3.0.0')}
        stray = 'node_modules/.pnpm/stray@1.0.0/node_modules/stray'
        wrong = 'node_modules/.pnpm/tool@9.9.9/node_modules/tool'
        cases = [('gone', _tool_lock(**{'gone@1.0.0': []}), store, [TOOL_LINK],
                  'Missing required locked JavaScript package: gone@1.0.0'),
                 ('unlinked', _tool_lock(), store, [],
                  'Missing required locked JavaScript package: node_modules/tool'),
                 ('stray', _tool_lock(), {**store, stray: ('stray', '1.0.0')}, [TOOL_LINK],
                  'resolved version mismatch: ' + stray),
                 ('mislinked', _tool_lock(), {**store, wrong: ('tool', '9.9.9')}, [(wrong, 'node_modules/tool')],
                  'resolved version mismatch: node_modules/tool')]
        for label, lock, installed, links, message in cases:
            folder = self.root / label
            _javascript_tree(folder, {'tool': '1.0.0'}, lock, installed, links)
            with self.subTest(label), self.assertRaisesRegex(ValueError, message):
                gate_versions.javascript_versions(folder)

    def _leaf_tree(self, label, leaf_links, lock=None):
        """Two tools pinning different versions of one leaf, as in a pnpm store; `leaf_links` maps
        each tool to the leaf version its sibling link resolves to, None for no link."""
        folder = self.root / label
        store = {_store(name, version): (name, version) for name, version in LEAF_PACKAGES}
        links = [(_store('tool', '1.0.0'), 'node_modules/tool'), (_store('other', '1.0.0'), 'node_modules/other')]
        links += [(_store('leaf', version), _sibling(tool, 'leaf'))
                  for tool, version in leaf_links.items() if version]
        _javascript_tree(folder, {'tool': '1.0.0', 'other': '1.0.0'}, lock or _leaf_lock(), store, links)
        return folder

    def test_store_links_resolving_to_the_locked_versions_are_accepted(self):
        folder = self._leaf_tree('correct', {'tool': '1.0.0', 'other': '2.0.0'})

        facts = gate_versions.javascript_versions(folder)

        self.assertEqual(['leaf@1.0.0', 'leaf@2.0.0', 'other@1.0.0', 'tool@1.0.0'],
                         sorted(row['package'] for row in facts['packages']))

    def test_wrong_missing_and_unexpected_store_links_fail_closed(self):
        tool = _sibling('tool', 'leaf')
        cases = [('wrong-version', {'tool': '2.0.0', 'other': '2.0.0'}, _leaf_lock(),
                  f'link mismatch: {tool} resolves to leaf@2.0.0, the lock pins leaf@1.0.0'),
                 ('missing', {'tool': None, 'other': '2.0.0'}, _leaf_lock(),
                  f'link missing: {tool} must resolve to leaf@1.0.0'),
                 ('unexpected', {'tool': '1.0.0', 'other': '2.0.0'}, _leaf_lock(tool_needs_leaf=False),
                  f'link unexpected: {tool} resolves to leaf@1.0.0, which the lock does not list for tool@1.0.0')]
        for label, leaf_links, lock, message in cases:
            folder = self._leaf_tree(label, leaf_links, lock)
            with self.subTest(label), self.assertRaisesRegex(ValueError, 'JavaScript dependency ' + re.escape(message)):
                gate_versions.javascript_versions(folder)

    def test_a_link_into_the_wrong_peer_variant_fails_closed(self):
        # The importer pins leaf with peer 1.0.0, other pins it with peer 2.0.0: one version, two folders.
        lock = _pnpm_lock({'leaf': ('1.0.0', '1.0.0(peer@1.0.0)'), 'other': ('1.0.0', '1.0.0')},
                          {'other@1.0.0': [], 'leaf@1.0.0': [], 'peer@1.0.0': [], 'peer@2.0.0': []},
                          {'other@1.0.0': ['dependencies:', '  leaf: 1.0.0(peer@2.0.0)'],
                           'leaf@1.0.0(peer@1.0.0)': ['dependencies:', '  peer: 1.0.0'],
                           'leaf@1.0.0(peer@2.0.0)': ['dependencies:', '  peer: 2.0.0'],
                           'peer@1.0.0': [], 'peer@2.0.0': []})
        variant = {peer: f'node_modules/.pnpm/leaf@1.0.0_peer@{peer}/node_modules/leaf' for peer in ('1.0.0', '2.0.0')}
        store = {_store('other', '1.0.0'): ('other', '1.0.0'), _store('peer', '1.0.0'): ('peer', '1.0.0'),
                 _store('peer', '2.0.0'): ('peer', '2.0.0'), **{path: ('leaf', '1.0.0') for path in variant.values()}}
        fixed = [(_store('other', '1.0.0'), 'node_modules/other'),
                 *((_store('peer', peer), path.removesuffix('leaf') + 'peer') for peer, path in variant.items())]
        for label, top, nested in (('right', '1.0.0', '2.0.0'), ('swapped', '2.0.0', '1.0.0')):
            folder = self.root / label
            links = [*fixed, (variant[top], 'node_modules/leaf'), (variant[nested], _sibling('other', 'leaf'))]
            _javascript_tree(folder, {'leaf': '1.0.0', 'other': '1.0.0'}, lock, store, links)
            if label == 'right':
                self.assertEqual(5, len(gate_versions.javascript_versions(folder)['packages']))
                continue
            with self.assertRaisesRegex(ValueError, 'peer variant the lock does not pin: ' + re.escape(
                    _sibling('other', 'leaf') + ', node_modules/leaf') + '$'):
                gate_versions.javascript_versions(folder)

    def test_a_link_into_a_peer_variant_the_store_lacks_fails_closed(self):
        # leaf's x@2.0.0 variant has no folder: its one folder links peer's x@1.0.0 variant, which
        # matches either leaf snapshot by version, yet the importer pins the x@2.0.0 one.
        pinned = '1.0.0(peer@1.0.0(x@2.0.0))'
        lock = _pnpm_lock({'leaf': ('1.0.0', pinned)},
                          {'leaf@1.0.0': [], 'peer@1.0.0': [], 'x@1.0.0': [], 'x@2.0.0': []},
                          {**{f'leaf@1.0.0(peer@1.0.0(x@{x}))': ['dependencies:', f'  peer: 1.0.0(x@{x})']
                              for x in ('1.0.0', '2.0.0')},
                           **{f'peer@1.0.0(x@{x})': ['dependencies:', f'  x: {x}'] for x in ('1.0.0', '2.0.0')},
                           'x@1.0.0': [], 'x@2.0.0': []})
        leaf = 'node_modules/.pnpm/leaf@1.0.0_peer@1.0.0_x@1.0.0_/node_modules'
        peer = {x: f'node_modules/.pnpm/peer@1.0.0_x@{x}/node_modules' for x in ('1.0.0', '2.0.0')}
        store = {leaf + '/leaf': ('leaf', '1.0.0'), **{peer[x] + '/peer': ('peer', '1.0.0') for x in peer},
                 **{_store('x', x): ('x', x) for x in peer}}
        links = [(leaf + '/leaf', 'node_modules/leaf'), (peer['1.0.0'] + '/peer', leaf + '/peer'),
                 *((_store('x', x), peer[x] + '/x') for x in peer)]
        _javascript_tree(self.root, {'leaf': '1.0.0'}, lock, store, links)

        with self.assertRaisesRegex(ValueError, 'peer variant the lock does not pin: node_modules/leaf$'):
            gate_versions.javascript_versions(self.root)

    def test_a_store_folder_holding_other_than_one_package_fails_closed(self):
        lock = _pnpm_lock({'tool': ('1.0.0', '1.0.0')}, {'tool@1.0.0': []}, {'tool@1.0.0': []})
        tool = {_store('tool', '1.0.0'): ('tool', '1.0.0')}
        twin = 'node_modules/.pnpm/tool@1.0.0/node_modules/twin'
        for label, store, folder_name in (('two', {**tool, twin: ('twin', '1.0.0')}, 'tool@1.0.0'),
                                          ('none', tool, 'empty@1.0.0')):
            folder = self.root / label
            _javascript_tree(folder, {'tool': '1.0.0'}, lock, store, [(_store('tool', '1.0.0'), 'node_modules/tool')])
            (folder / 'node_modules/.pnpm' / folder_name / 'node_modules').mkdir(parents=True, exist_ok=True)
            with self.subTest(label), self.assertRaisesRegex(ValueError, re.escape(
                    f'must hold exactly one package: node_modules/.pnpm/{folder_name}/node_modules')):
                gate_versions.javascript_versions(folder)

    def test_an_installed_optional_dependency_is_linked_and_a_skipped_one_is_not_required(self):
        lock = _pnpm_lock({'tool': ('1.0.0', '1.0.0')}, {'tool@1.0.0': [], 'native@1.0.0': [], 'skipped@1.0.0': []},
                          {'tool@1.0.0': ['optionalDependencies:', '  native: 1.0.0', '  skipped: 1.0.0'],
                           'native@1.0.0': ['optional: true'], 'skipped@1.0.0': ['optional: true']})
        store = {_store('tool', '1.0.0'): ('tool', '1.0.0'), _store('native', '1.0.0'): ('native', '1.0.0')}
        native = (_store('native', '1.0.0'), _sibling('tool', 'native'))
        for label, links, failure in (('linked', [native], None),
                                      ('unlinked', [], 'link missing: ' + _sibling('tool', 'native'))):
            folder = self.root / label
            _javascript_tree(folder, {'tool': '1.0.0'}, lock, store, [(_store('tool', '1.0.0'), 'node_modules/tool'), *links])
            if failure is None:
                self.assertEqual(['skipped@1.0.0'],
                                 [row['package'] for row in gate_versions.javascript_versions(folder)['uninstalled_optional']])
                continue
            with self.subTest(label), self.assertRaisesRegex(ValueError, re.escape(failure)):
                gate_versions.javascript_versions(folder)

    def test_a_store_link_leading_outside_the_store_fails_closed(self):
        folder = self._leaf_tree('outside', {'other': '2.0.0'})
        _write(folder / 'elsewhere/leaf/package.json', {'name': 'leaf', 'version': '1.0.0'})
        _link(folder / 'elsewhere/leaf', folder / _sibling('tool', 'leaf'))

        with self.assertRaisesRegex(ValueError, re.escape(
                f'link mismatch: {_sibling("tool", "leaf")} resolves to nothing in the store, the lock pins leaf@1.0.0')):
            gate_versions.javascript_versions(folder)

    def test_repository_pnpm_lock_pins_exactly_the_declared_dependencies_and_no_npm_lock(self):
        coverage = Path(__file__).resolve().parents[1]
        lock = gate_versions.pnpm_lock(coverage / 'pnpm-lock.yaml')
        declared = json.loads((coverage / 'package.json').read_text(encoding='utf-8'))['dependencies']

        importer = lock['importers']['.']['dependencies']
        self.assertEqual(declared, {name: pin['specifier'] for name, pin in importer.items()})
        self.assertEqual(declared, {name: pin['version'].split('(', 1)[0] for name, pin in importer.items()})
        self.assertEqual(set(lock['packages']), {key.split('(', 1)[0] for key in lock['snapshots']})
        self.assertFalse((coverage / 'package-lock.json').exists())
        self.assertEqual(declared, gate_versions.importer_pins((coverage / 'pnpm-lock.yaml').read_text(encoding='utf-8')))

    def test_importer_pins_span_every_dependency_section_without_peer_suffixes(self):
        text = '\n'.join(["lockfileVersion: '9.0'", 'importers:', '  .:', '    dependencies:', "      '@scope/a':",
                          '        specifier: ^1.0.0', '        version: 1.2.0(peer@3.0.0)', '    devDependencies:',
                          '      b:', '        specifier: 2.0.0', '        version: 2.0.0', 'packages:', '  b@2.0.0: {}'])

        self.assertEqual({'@scope/a': '1.2.0', 'b': '2.0.0'}, gate_versions.importer_pins(text))

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

        for name, command, pin in (('node', ['node', '--version'], 'v26.13.0'),
                                   ('sdk', ['dotnet', '--version'], '10.0.303')):
            observed = _native_version(command)
            with self.subTest(name=name):
                self.assertEqual(observed, answer['facts'][name]['version'])
                self.assertEqual(observed != pin,
                                 any(row['stack'] == name for row in answer['errors']))
        accounted = set(answer['facts']) | {row['stack'] for row in answer['errors']}
        self.assertEqual({'python', 'javascript', 'dotnet', 'node', 'sdk'}, accounted)


if __name__ == "__main__":
    unittest.main()

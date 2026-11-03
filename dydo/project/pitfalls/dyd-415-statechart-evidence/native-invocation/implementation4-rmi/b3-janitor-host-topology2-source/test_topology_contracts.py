"""Actual topology functions with isolated files and inert reparse observations."""
from contextlib import nullcontext
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

PACKET = Path(__file__).parent
SPEC = importlib.util.spec_from_file_location('_dyd415_topology2_test_target', PACKET / 'topology.py')
topology = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(topology)


class TopologyContracts(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='dyd415-topology-contract-')
        self.addCleanup(temporary.cleanup)
        self.base = Path(temporary.name)
        self.root = self.base / 'workspace'
        self.source = self.root / 'skills/engineering/statechart/scripts/node_modules'
        self.source.mkdir(parents=True)
        self.target = self.base / 'compiler/node_modules'
        self.member = self.target / 'typescript/lib/tsc.js'
        self.member.parent.mkdir(parents=True)
        self.member.write_bytes(b'original compiler fixture\n')
        self.links, self.denied, self.scanned, self.inspected = {}, set(), [], []
        self.readlink_denied = set()
        self.escape = False
        retained = json.loads((PACKET / 'topology-manifest.json').read_bytes())
        live_root = Path(retained['root'])
        projections = []
        for index, row in enumerate(retained['projections']):
            path = self.root / Path(row['path']).relative_to(live_root)
            path.mkdir(parents=True)
            expected = dict(path=str(path), attributes=1040, linkType='Junction',
                targets=['Z:\\inaccessible-sentinel\\projection-' + str(index)])
            projections.append(expected)
            self.links[path] = dict(target=expected['targets'][0], attributes=1040, tag=topology.MOUNT_POINT)
        compiler_entry = dict(path=str(self.source), attributes=1040, linkType='Junction',
            targets=[str(self.target)])
        self.links[self.source] = dict(target=str(self.target), attributes=1040, tag=topology.MOUNT_POINT)
        self.manifest = dict(projections=projections,
            compiler=dict(source=self.source.as_posix(), target=self.target.as_posix(),
                entry=compiler_entry, files=[dict(
                    path=(self.source / 'typescript/lib/tsc.js').relative_to(self.root).as_posix(),
                    bytes=self.member.stat().st_size,
                    sha256=hashlib.sha256(self.member.read_bytes()).hexdigest())],
                directories=sorted((self.source / relative).relative_to(self.root).as_posix()
                    for relative in ['typescript', 'typescript/lib'])),
            ordinaryAncestors=[dict(path=str(path), attributes=getattr(os.lstat(path), 'st_file_attributes', 0))
                for path in [self.root, self.source.parent, self.target]])
        self.ownership = dict(state='TASK_LOCATION_OWNERSHIP_ESTABLISHED',
            mapping=dict(source=self.source.as_posix(), target=self.target.as_posix()),
            originalEvidence=dict(sha256=topology.OWNERSHIP_SHA256), creatorPositions=[66, 70, 73])
        self.real_lstat, self.real_scandir = os.lstat, os.scandir
        inert = SimpleNamespace(lstat=self.lstat, readlink=self.readlink, scandir=self.scandir)
        self.patch = patch.object(topology, 'os', inert)
        self.patch.start()
        self.addCleanup(self.patch.stop)

    def lstat(self, path):
        path = Path(path)
        if not path.is_relative_to(self.base):
            raise AssertionError('Foreign fixture target inspection refused')
        self.inspected.append(path)
        if path in self.denied:
            raise PermissionError('inert inspection failure')
        if path in self.links:
            row = self.links[path]
            return SimpleNamespace(st_mode=stat.S_IFDIR, st_size=0,
                st_file_attributes=row['attributes'], st_reparse_tag=row['tag'])
        return self.real_lstat(path)

    def readlink(self, path):
        if Path(path) in self.readlink_denied:
            raise PermissionError('inert readlink failure')
        return '\\\\?\\' + self.links[Path(path)]['target']

    def scandir(self, path):
        path = Path(path)
        if not path.is_relative_to(self.base):
            raise AssertionError('Foreign fixture target traversal refused')
        self.scanned.append(path)
        if path in self.links:
            raise AssertionError('Terminal link was followed')
        if self.escape and path == self.target:
            escaped = self.base / 'outside-member'
            return nullcontext([SimpleNamespace(path=str(escaped), name=escaped.name)])
        return self.real_scandir(path)

    def workspace(self):
        return topology.workspace_paths(self.root, self.root / 'never-created-output', self.manifest)

    def compiler(self, ownership=None):
        return topology.compiler_inventory(self.root, self.manifest,
            self.ownership if ownership is None else ownership)

    def test_exact82_terminal_set_never_follows_inaccessible_targets(self):
        forbidden = AssertionError('Terminal observation must not resolve/stat/open a target')
        with patch.object(Path, 'resolve', side_effect=forbidden), \
                patch.object(Path, 'stat', side_effect=forbidden), \
                patch.object(Path, 'open', side_effect=forbidden):
            rows = self.workspace()
        links = [row for row in rows if row['kind'] == 'junction']
        self.assertEqual(len(links), 83)
        self.assertEqual(sum(row['path'].startswith('.agents/') for row in links), 41)
        self.assertEqual(sum(row['path'].startswith('.claude/') for row in links), 41)
        self.assertTrue(all(path not in self.scanned for path in self.links))
        self.assertTrue(all(not str(path).startswith('Z:') for path in self.inspected))
        self.assertTrue(all(row['rawReadlink'].startswith('\\\\?\\') for row in links))

    def test_added_projection_is_refused(self):
        path = self.root / '.agents/skills/unlisted'
        path.mkdir()
        self.links[path] = dict(target='Z:\\unlisted', attributes=1040, tag=topology.MOUNT_POINT)
        with self.assertRaisesRegex(RuntimeError, 'Unknown workspace reparse'):
            self.workspace()
        self.assertNotIn(path, self.scanned)

    def test_missing_projection_is_refused(self):
        Path(self.manifest['projections'][0]['path']).rmdir()
        with self.assertRaisesRegex(RuntimeError, 'entry missing'):
            self.workspace()

    def test_repointed_projection_is_refused(self):
        path = Path(self.manifest['projections'][0]['path'])
        self.links[path]['target'] = 'Z:\\different'
        with self.assertRaisesRegex(RuntimeError, 'Literal junction target differs'):
            self.workspace()

    def test_projection_type_change_is_refused(self):
        path = Path(self.manifest['projections'][0]['path'])
        del self.links[path]
        with self.assertRaisesRegex(RuntimeError, 'Exact junction type'):
            self.workspace()
        self.assertNotIn(path, self.scanned)

    def test_unknown_reparse_tag_is_refused(self):
        path = self.root / 'foreign-reparse'
        path.mkdir()
        self.links[path] = dict(target='Z:\\foreign', attributes=1040, tag=0xA000000C)
        with self.assertRaisesRegex(RuntimeError, 'Unknown workspace reparse'):
            self.workspace()
        self.assertNotIn(path, self.scanned)

    def test_projection_inspection_failure_is_refused(self):
        path = Path(self.manifest['projections'][0]['path'])
        for refused in [self.denied, self.readlink_denied]:
            with self.subTest(refused=refused):
                refused.add(path)
                with self.assertRaisesRegex(RuntimeError, 'inspection failed'):
                    self.workspace()
                refused.clear()

    def test_exact_compiler_ownership_and_scripts_closure(self):
        result = self.compiler()
        self.assertEqual(result['link']['targets'], [str(self.target)])
        self.assertEqual(len(result['ancestors']), 3)
        rows = topology.scripts_inventory(self.root, self.manifest, self.ownership)
        self.assertEqual([{key: row[key] for key in ['path', 'bytes', 'sha256']}
            for row in rows if row['kind'] == 'file'], self.manifest['compiler']['files'])
        self.assertIn(self.target, self.scanned)
        self.assertNotIn(self.source, self.scanned)

    def test_changed_compiler_mapping_or_type_is_refused(self):
        original = copy.deepcopy(self.links[self.source])
        for mutation in [dict(target='Z:\\replacement'), dict(tag=0xA000000C), dict(attributes=16)]:
            with self.subTest(mutation=mutation):
                self.links[self.source] = dict(original, **mutation)
                with self.assertRaisesRegex(RuntimeError, 'junction'):
                    self.compiler()
        self.links[self.source] = original

    def test_missing_or_invalid_compiler_ownership_is_refused(self):
        invalid = [None, {}, dict(self.ownership, state='UNOWNED'),
            dict(self.ownership, originalEvidence=dict(sha256='0' * 64)),
            dict(self.ownership, creatorPositions=[66, 73]),
            dict(self.ownership, mapping=dict(source='foreign', target='foreign'))]
        for evidence in invalid:
            with self.subTest(evidence=evidence):
                self.assertRaisesRegex(RuntimeError, 'ownership evidence refused',
                    topology.compiler_inventory, self.root, self.manifest, evidence)
        self.assertNotIn(self.target, self.scanned)

    def test_nested_compiler_reparse_is_refused(self):
        path = self.target / 'nested'
        path.mkdir()
        self.links[path] = dict(target='Z:\\nested', attributes=1040, tag=topology.MOUNT_POINT)
        with self.assertRaisesRegex(RuntimeError, 'Nested compiler reparse'):
            self.compiler()
        self.assertNotIn(path, self.scanned)

    def test_escaping_compiler_member_is_refused_before_open(self):
        self.escape = True
        with self.assertRaisesRegex(RuntimeError, 'Escaping/foreign member'):
            self.compiler()
        self.assertNotIn(self.base / 'outside-member', self.inspected)

    def test_foreign_compiler_member_is_refused(self):
        (self.target / 'foreign-file').write_bytes(b'foreign')
        with self.assertRaisesRegex(RuntimeError, 'closure drift'):
            self.compiler()

    def test_missing_compiler_member_is_refused(self):
        self.member.unlink()
        with self.assertRaisesRegex(RuntimeError, 'closure drift'):
            self.compiler()

    def test_same_size_compiler_content_drift_is_refused(self):
        original = self.member.read_bytes()
        self.member.write_bytes(b'X' * len(original))
        self.assertEqual(self.member.stat().st_size, len(original))
        with self.assertRaisesRegex(RuntimeError, 'closure drift'):
            self.compiler()

    def test_compiler_ancestor_reparse_is_refused(self):
        self.links[self.target] = dict(target='Z:\\ancestor', attributes=1040, tag=topology.MOUNT_POINT)
        with self.assertRaisesRegex(RuntimeError, 'ancestor changed'):
            self.compiler()
        self.assertNotIn(self.target, self.scanned)

    def test_compiler_empty_directory_drift_is_refused(self):
        (self.target / 'unlisted-empty-directory').mkdir()
        with self.assertRaisesRegex(RuntimeError, 'closure drift'):
            self.compiler()


if __name__ == '__main__':
    unittest.main(verbosity=2)

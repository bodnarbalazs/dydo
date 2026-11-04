"""Actual unittest cases for the maintained build helper; no JVM is invoked here."""
import importlib.util
import hashlib
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
import warnings
import zipfile
import stat
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[3]
ROOT = SOURCE / 'skills/engineering/statechart/scripts/java'
SPEC = importlib.util.spec_from_file_location('_statechart_java_build', ROOT / 'build.py')
build = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(build)


class JavaBuildTests(unittest.TestCase):
    def setUp(self):
        self.scope = tempfile.TemporaryDirectory(prefix='dyd415-java-build-test-')
        self.addCleanup(self.scope.cleanup)
        self.root = Path(self.scope.name)

    def test_current_sources_match_the_finite_lock(self):
        lock = build.load_lock()
        build.validate_sources(ROOT, lock)
        self.assertEqual(2, len(lock['sources']))
        self.assertEqual(2, len(lock['classes']))
        self.assertEqual('11', build.compiler_argv(Path('/jdk'), Path('/tlc'), Path('/classes'))[2])

    def test_changed_and_missing_source_are_rejected(self):
        lock = build.load_lock()
        for row in lock['sources']:
            target = self.root / row['path']
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((ROOT / row['path']).read_bytes())
        build.validate_sources(self.root, lock)
        target.write_bytes(b'changed source')
        with self.assertRaisesRegex(ValueError, 'differs'):
            build.validate_sources(self.root, lock)
        target.unlink()
        with self.assertRaisesRegex(ValueError, 'differs'):
            build.validate_sources(self.root, lock)

    def test_extra_source_declaration_and_wrong_class_contract_are_rejected(self):
        lock = build.load_lock()
        lock['sources'].append(dict(path='extra.java', sha256='0' * 64))
        with self.assertRaisesRegex(ValueError, 'source list'):
            build.validate_sources(ROOT, lock)
        (self.root / 'build-lock.json').write_text(json.dumps(dict(schema=1, bytecodeMajor=61, classes=build.CLASSES)))
        with self.assertRaisesRegex(ValueError, 'Unsupported'):
            build.load_lock(self.root)

    def test_owned_outputs_cannot_overlap_inputs_or_each_other(self):
        inputs = [self.root / 'input']
        for outputs in [[inputs[0] / 'out'], [self.root / 'work', self.root / 'work/out'],
                [self.root / 'out', self.root / 'out']]:
            with self.assertRaises(ValueError):
                build.validate_paths(inputs, outputs)
        build.validate_paths(inputs, [self.root / 'work', self.root / 'artifact', self.root / 'receipt'])

    def test_existing_or_relative_outputs_are_rejected(self):
        target = self.root / 'retained'
        target.write_bytes(b'sentinel')
        for outputs in [[target], [Path('relative')]]:
            with self.assertRaises(ValueError):
                build.validate_paths([self.root / 'input'], outputs)
        self.assertEqual(b'sentinel', target.read_bytes())

    def fixture_archive(self):
        archive = self.root / 'jdk.tar.gz'
        jdk = self.root / 'jdk'
        jdk.mkdir()
        with tarfile.open(archive, 'w:gz') as bundle:
            member = tarfile.TarInfo('jdk/')
            member.type = tarfile.DIRTYPE
            bundle.addfile(member)
            member = tarfile.TarInfo('jdk/release')
            raw = b'JAVA_RUNTIME_VERSION="21.0.8+9-LTS"\nIMPLEMENTOR="Eclipse Adoptium"\n'
            member.size = len(raw)
            bundle.addfile(member, io.BytesIO(raw))
            (jdk / 'release').write_bytes(raw)
        return jdk, archive

    def test_archive_inventory_rejects_changed_missing_and_extra_bytes(self):
        jdk, archive = self.fixture_archive()
        build.verify_inventory(jdk, archive)
        release = jdk / 'release'
        raw = release.read_bytes()
        release.write_bytes(raw.replace(b'21', b'20'))
        with self.assertRaisesRegex(ValueError, 'Changed JDK file'):
            build.verify_inventory(jdk, archive)
        release.unlink()
        with self.assertRaisesRegex(ValueError, 'Changed JDK file'):
            build.verify_inventory(jdk, archive)
        release.write_bytes(raw)
        (jdk / 'extra').write_bytes(b'foreign')
        with self.assertRaisesRegex(ValueError, 'Extra or missing'):
            build.verify_inventory(jdk, archive)

    def test_wrong_archive_hash_prevents_toolchain_use(self):
        jdk, archive = self.fixture_archive()
        with self.assertRaisesRegex(ValueError, 'checksum'):
            build.verify_jdk(jdk, archive, dict(jdk=dict(archiveSha256='0' * 64)))

    def test_traversal_and_absolute_archive_members_are_rejected(self):
        for name in ['/jdk/file', 'jdk/../escape', '../escape', 'jdk\\escape', '']:
            with self.assertRaises(ValueError):
                build.archive_relative(name)

    def test_child_environment_removes_only_owned_java_injection_variables(self):
        values = {key: 'injected' for key in ['CLASSPATH', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS']}
        with patch.dict(build.os.environ, dict(values, STATECHART_SENTINEL='keep'), clear=True):
            self.assertEqual(dict(STATECHART_SENTINEL='keep'), build.child_environment())
            self.assertEqual('injected', build.os.environ['CLASSPATH'])

    def test_nonlinux_build_refuses_before_any_subprocess(self):
        with patch.object(build.sys, 'platform', 'win32'), patch.object(build.subprocess, 'run') as child:
            with self.assertRaisesRegex(RuntimeError, 'never host javac'):
                build.build(None)
            child.assert_not_called()

    def test_rebuild_compares_to_the_sealed_artifact_not_current_drift(self):
        raw = b'sealed artifact fixture, not executable Java'
        lock = dict(artifact=dict(state='sealed-build', sha256=hashlib.sha256(raw).hexdigest()))
        build.validate_artifact_identity(lock, raw)
        with self.assertRaisesRegex(ValueError, 'separately sealed'):
            build.validate_artifact_identity(lock, raw + b'changed')
        lock['artifact']['state'] = 'unknown'
        with self.assertRaises(ValueError):
            build.validate_artifact_identity(lock, raw)

    def test_failed_compiler_retains_argv_exit_and_streams_without_retry(self):
        with patch.object(build.subprocess, 'run', return_value=type('Result', (), dict(returncode=7))()) as child:
            with self.assertRaisesRegex(RuntimeError, 'do not retry'):
                build.invoke(['/owned/jdk/bin/javac', '--release', '11'], self.root, 'javac')
            self.assertEqual(1, child.call_count)
        command = json.loads((self.root / 'javac.command.json').read_text())
        self.assertEqual(['/owned/jdk/bin/javac', '--release', '11'], command['argv'])
        self.assertEqual(7, json.loads((self.root / 'javac.result.json').read_text())['exit'])
        self.assertTrue((self.root / 'javac.stderr').is_file())

    def compiler_fixture(self):
        # Synthetic resource bytes exercise ZIP fidelity only; no class stub or compiler execution.
        data = {'META-INF/MANIFEST.MF': b'Manifest-Version: 1.0\r\nClass-Path: Companion.jar\r\n\r\n',
            'pkg/data.bin': b'\x00\xffexact bytes\r\n', 'root.txt': b'resource'}
        jar = self.root / 'compiler-input.jar'
        with zipfile.ZipFile(jar, 'w') as bundle:
            for name, raw in data.items(): bundle.writestr(name, raw)
        rows = [dict(name=name, kind='file', bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
            for name, raw in sorted(data.items())]
        canonical = json.dumps(rows, sort_keys=True, separators=(',', ':')).encode('utf-8')
        lock = dict(tlc=dict(sha256=hashlib.sha256(jar.read_bytes()).hexdigest()), compilerInput=dict(
            representation='byte-exact-exploded-tlc-v1', memberCount=3, classCount=0,
            expandedBytes=sum(len(raw) for raw in data.values()), membersSha256=hashlib.sha256(canonical).hexdigest()))
        return jar, self.root / 'compiler-view', lock, data, rows

    def test_compiler_view_preserves_all_member_bytes_and_original_manifest_as_data(self):
        jar, view, lock, data, rows = self.compiler_fixture()
        original = jar.read_bytes()
        actual = build.prepare_compiler_view(jar, view, lock)
        self.assertEqual(rows, actual)
        self.assertEqual(data, {name: (view / name).read_bytes() for name in data})
        self.assertEqual(original, jar.read_bytes())
        self.assertFalse((self.root / 'Companion.jar').exists())
        self.assertEqual({'META-INF', 'META-INF/MANIFEST.MF', 'pkg', 'pkg/data.bin', 'root.txt'},
            {path.relative_to(view).as_posix() for path in view.rglob('*')})

    def test_compiler_view_rejects_changed_missing_extra_and_linked_members(self):
        jar, view, lock, data, rows = self.compiler_fixture()
        build.prepare_compiler_view(jar, view, lock)
        target = view / 'pkg/data.bin'
        target.write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'Changed TLC compiler member'):
            build.verify_compiler_view(view, rows)
        target.unlink()
        with self.assertRaisesRegex(ValueError, 'Missing TLC compiler member'):
            build.verify_compiler_view(view, rows)
        target.write_bytes(data['pkg/data.bin'])
        foreign = view / 'foreign.jar'
        foreign.write_bytes(b'foreign')
        with self.assertRaisesRegex(ValueError, 'Extra or linked'):
            build.verify_compiler_view(view, rows)
        self.assertEqual(b'foreign', foreign.read_bytes())
        foreign.unlink()
        # Report a real owned member as a link without needing host symlink privileges.
        original = Path.is_symlink
        with patch.object(Path, 'is_symlink', lambda path: path == target or original(path)):
            with self.assertRaisesRegex(ValueError, 'Extra or linked'):
                build.verify_compiler_view(view, rows)

    def test_compiler_view_refuses_existing_output_without_touching_foreign_bytes(self):
        jar, view, lock, _data, _rows = self.compiler_fixture()
        view.mkdir()
        sentinel = view / 'foreign.txt'
        sentinel.write_bytes(b'sentinel')
        with self.assertRaisesRegex(ValueError, 'disjoint and new'):
            build.prepare_compiler_view(jar, view, lock)
        self.assertEqual(b'sentinel', sentinel.read_bytes())

    def test_compiler_input_digest_and_inventory_cannot_follow_current_drift(self):
        jar, view, lock, _data, _rows = self.compiler_fixture()
        raw = jar.read_bytes()
        jar.write_bytes(raw + b'changed')
        with self.assertRaisesRegex(ValueError, 'compile input changed'):
            build.prepare_compiler_view(jar, view, lock)
        self.assertFalse(view.exists())
        jar.write_bytes(raw)
        lock['compilerInput']['membersSha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'independently pinned inventory'):
            build.prepare_compiler_view(jar, view, lock)
        self.assertFalse(view.exists())

    def test_compiler_rejects_malformed_archive_before_creating_output(self):
        jar = self.root / 'malformed.jar'
        jar.write_bytes(b'not a ZIP archive')
        with self.assertRaises(zipfile.BadZipFile):
            build.compiler_members(jar, hashlib.sha256(jar.read_bytes()).hexdigest())
        self.assertEqual([jar], list(self.root.iterdir()))

    def test_compiler_rejects_escape_absolute_drive_and_ambiguous_member_names(self):
        for name in ['../outside', '/absolute', 'C:/drive', 'a\\escape', 'a/./b', 'a//b', 'a./b', 'a /b', 'a\x00b']:
            with self.subTest(name=name):
                info = zipfile.ZipInfo(name)
                with self.assertRaises(ValueError): build.compiler_name(info)
        jar = self.root / 'escape.jar'
        with zipfile.ZipFile(jar, 'w') as bundle: bundle.writestr('../outside', b'escape')
        with self.assertRaises(ValueError):
            build.prepare_compiler_view(jar, self.root / 'view', dict(tlc=dict(sha256=build.sha(jar))))
        self.assertFalse((self.root / 'view').exists())
        self.assertFalse((self.root.parent / 'outside').exists())

    def test_compiler_rejects_duplicate_case_collision_and_file_parent_conflict(self):
        for index, names in enumerate([['same', 'same'], ['A/value', 'a/other'], ['pkg', 'pkg/data']]):
            jar = self.root / (str(index) + '.jar')
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(jar, 'w') as bundle:
                    for name in names: bundle.writestr(name, b'raw')
            with self.subTest(names=names), self.assertRaises(ValueError):
                build.compiler_members(jar, build.sha(jar))

    def test_compiler_rejects_links_special_files_and_bounded_size_violations(self):
        for mode in [stat.S_IFLNK, stat.S_IFIFO]:
            info = zipfile.ZipInfo('linked')
            info.external_attr = (mode | 0o777) << 16
            with self.assertRaisesRegex(ValueError, 'Linked, special'):
                build.compiler_name(info)
            archive = self.root / (str(mode) + '.jar')
            with zipfile.ZipFile(archive, 'w') as bundle: bundle.writestr(info, b'../outside')
            view = self.root / ('view-' + str(mode))
            with self.assertRaisesRegex(ValueError, 'Linked, special'):
                build.prepare_compiler_view(archive, view, dict(tlc=dict(sha256=build.sha(archive))))
            self.assertFalse(view.exists())
        info = zipfile.ZipInfo('oversized')
        info.file_size = build.MAX_ZIP_MEMBER + 1
        with self.assertRaisesRegex(ValueError, 'size bound'):
            build.compiler_name(info)
        jar, _view, _lock, _data, _rows = self.compiler_fixture()
        with patch.object(build, 'MAX_ZIP_ENTRIES', 2), self.assertRaisesRegex(ValueError, 'member-count bound'):
            build.compiler_members(jar, build.sha(jar))
        with patch.object(build, 'MAX_ZIP_BYTES', 1), self.assertRaisesRegex(ValueError, 'expanded-size bound'):
            build.compiler_members(jar, build.sha(jar))

    def test_compiler_argv_selects_only_the_verified_view_directory(self):
        jar, view, lock, _data, _rows = self.compiler_fixture()
        build.prepare_compiler_view(jar, view, lock)
        sibling = self.root / 'Companion.jar'
        sibling.write_bytes(b'foreign companion, never a compiler input')
        args = build.compiler_argv(Path('/jdk'), view, self.root / 'classes')
        self.assertEqual(str(view), args[args.index('-classpath') + 1])
        self.assertNotIn(str(jar), args)
        self.assertNotIn(str(sibling), args)
        self.assertEqual(['--release', '11', '-encoding', 'UTF-8', '-g', '-Xlint:all', '-Werror', '-proc:none'], args[1:9])
        self.assertEqual([str(ROOT / name) for name in build.SOURCES], args[-2:])
        self.assertEqual(b'foreign companion, never a compiler input', sibling.read_bytes())


if __name__ == '__main__':
    unittest.main()

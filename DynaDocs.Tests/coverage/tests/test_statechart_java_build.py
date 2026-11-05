"""Actual unittest cases for the maintained build helper; no JVM is invoked here."""
import argparse
import contextlib
import functools
import hashlib
import io
import json
from pathlib import Path
import sys
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
import warnings
import zipfile
import stat
from unittest.mock import patch

SOURCE = Path(__file__).resolve().parents[3]
ROOT = SOURCE / 'skills/engineering/statechart/scripts/java'
sys.path.insert(0, str(ROOT))
import build

RELEASE = b'JAVA_RUNTIME_VERSION="21.0.8+9-LTS"\nIMPLEMENTOR="Eclipse Adoptium"\n'
TOOLCHAIN = {'release': RELEASE, 'bin/javac': b'javac', 'bin/jar': b'jar', 'bin/java': b'java', 'lib/modules': b'modules'}
CLASS_MAGIC = bytes.fromhex('cafebabe0000')


def toolchain(classes=build.CLASSES, major=55, varying=False, extra_member=None):
    """Stand-ins for the pinned JDK's javac and jar, the build's one external boundary: javac writes
    the named class files into its -d folder, jar packs its -C members into a fixed-date ZIP."""
    builds = []

    def run(argv, **_options):
        if Path(argv[0]).name == 'javac':
            builds.append(argv)
            output = Path(argv[argv.index('-d') + 1])
            for name in classes:
                (output / name).parent.mkdir(parents=True, exist_ok=True)
                stamp = bytes([len(builds)]) if varying else b''
                (output / name).write_bytes(CLASS_MAGIC + major.to_bytes(2, 'big') + name.encode() + stamp)
        else:
            members = argv[argv.index('-C'):]
            with zipfile.ZipFile(argv[argv.index('--file') + 1], 'w') as bundle:
                for folder, name in zip(members[1::3], members[2::3]):
                    bundle.writestr(zipfile.ZipInfo(name, (2026, 1, 1, 0, 0, 0)), (Path(folder) / name).read_bytes())
                if extra_member:
                    bundle.writestr(zipfile.ZipInfo(extra_member, (2026, 1, 1, 0, 0, 0)), b'extra')
        return SimpleNamespace(returncode=0)
    return run


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

    def fixture_archive(self, files=None, name='jdk'):
        """A JDK archive and its extracted copy; tarfile records each file's own mode."""
        archive = self.root / (name + '.tar.gz')
        jdk = self.root / name
        for relative, raw in (files or {'release': RELEASE}).items():
            (jdk / relative).parent.mkdir(parents=True, exist_ok=True)
            (jdk / relative).write_bytes(raw)
        with tarfile.open(archive, 'w:gz') as bundle:
            bundle.add(jdk, arcname=name)
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

    def test_archive_inventory_rejects_a_second_root_a_duplicate_member_and_an_empty_archive(self):
        jdk, _archive = self.fixture_archive()
        for index, (names, message) in enumerate([(['jdk/', 'other/'], 'Unexpected or duplicate'),
                                                  (['jdk/', 'jdk/'], 'Unexpected or duplicate'),
                                                  ([], 'Extra or missing build JDK entries')]):
            archive = self.root / f'{index}.tar.gz'
            with tarfile.open(archive, 'w:gz') as bundle:
                for name in names:
                    member = tarfile.TarInfo(name)
                    member.type = tarfile.DIRTYPE
                    bundle.addfile(member)
            with self.subTest(names=names), self.assertRaisesRegex(ValueError, message):
                build.verify_inventory(jdk, archive)

    def test_member_checks_refuse_an_escaping_link_a_changed_directory_or_link_and_an_unknown_kind(self):
        jdk, archive = self.fixture_archive({'release': RELEASE, 'bin/javac': b'javac'})
        directory = tarfile.TarInfo('jdk/bin')
        directory.type = tarfile.DIRTYPE
        link = tarfile.TarInfo('jdk/bin/java')
        link.type, link.linkname = tarfile.SYMTYPE, 'javac'
        special = tarfile.TarInfo('jdk/bin/pipe')
        special.type = tarfile.FIFOTYPE
        for member, target, message in [(directory, jdk.parent / 'outside', 'escapes its owned root'),
                                        (directory, jdk / 'release', 'Changed JDK directory'),
                                        (link, jdk / 'bin/javac', 'Changed JDK symbolic link'),
                                        (special, jdk / 'bin/pipe', 'Unsupported JDK archive member kind')]:
            with self.subTest(member=member.name, target=target.name), self.assertRaisesRegex(ValueError, message):
                build.verify_member(None, member, target, jdk)
        build.verify_member(None, directory, jdk / 'bin', jdk)
        # Report an owned path as a link without needing host symlink privileges.
        original = Path.is_symlink
        linked = patch.object(Path, 'is_symlink', lambda path: path == jdk / 'bin/java' or original(path))
        with linked, patch.object(build.os, 'readlink', return_value='javac'):
            build.verify_member(None, link, jdk / 'bin/java', jdk)
        with linked, patch.object(build.os, 'readlink', return_value='elsewhere'), \
                self.assertRaisesRegex(ValueError, 'Changed JDK symbolic link'):
            build.verify_member(None, link, jdk / 'bin/java', jdk)

    def test_member_checks_refuse_a_file_whose_execute_permission_changed(self):
        jdk, archive = self.fixture_archive({'release': RELEASE, 'bin/javac': b'javac'})
        with tarfile.open(archive, 'r:gz') as bundle:
            member = bundle.getmember('jdk/bin/javac')
            build.verify_member(bundle, member, jdk / 'bin/javac', jdk)
            member.mode ^= 0o111
            with self.assertRaisesRegex(ValueError, 'bytes/execute permissions'):
                build.verify_member(bundle, member, jdk / 'bin/javac', jdk)

    def test_wrong_archive_hash_prevents_toolchain_use(self):
        jdk, archive = self.fixture_archive()
        with self.assertRaisesRegex(ValueError, 'checksum'):
            build.verify_jdk(jdk, archive, dict(jdk=dict(archiveSha256='0' * 64)))

    def test_verified_toolchain_reports_its_file_hashes_and_refuses_a_foreign_release(self):
        jdk, archive = self.fixture_archive(TOOLCHAIN)
        self.assertEqual({name: hashlib.sha256(raw).hexdigest() for name, raw in TOOLCHAIN.items()},
                         build.verify_jdk(jdk, archive, dict(jdk=dict(archiveSha256=build.sha(archive)))))
        foreign = dict(TOOLCHAIN, release=RELEASE.replace(b'Eclipse Adoptium', b'Other'))
        jdk, archive = self.fixture_archive(foreign, 'foreign-jdk')
        with self.assertRaisesRegex(ValueError, 'Unexpected build JDK release metadata'):
            build.verify_jdk(jdk, archive, dict(jdk=dict(archiveSha256=build.sha(archive))))

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

    def test_a_compiler_that_cannot_start_is_recorded_and_raised(self):
        with patch.object(build.subprocess, 'run', side_effect=FileNotFoundError('no javac')):
            with self.assertRaises(FileNotFoundError):
                build.invoke(['/owned/jdk/bin/javac'], self.root, 'javac')
        self.assertEqual(dict(error='FileNotFoundError', message='no javac'),
                         json.loads((self.root / 'javac.result.json').read_text()))

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

    def compiled_view(self):
        jar, view, lock, _data, rows = self.compiler_fixture()
        build.prepare_compiler_view(jar, view, lock)
        return view, rows

    def test_one_build_packs_exactly_the_locked_release_11_classes(self):
        view, rows = self.compiled_view()
        with patch.object(build.subprocess, 'run', side_effect=toolchain()):
            jar, hashes = build.build_once(self.root / 'jdk', view, self.root / 'once', rows)
        with zipfile.ZipFile(jar) as bundle:
            self.assertEqual(sorted(build.CLASSES), sorted(bundle.namelist()))
        self.assertEqual(sorted(build.CLASSES), sorted(hashes))
        self.assertEqual([0, 0], [json.loads((self.root / 'once' / f'{name}.result.json').read_text())['exit']
                                  for name in ('javac', 'jar')])

    def test_one_build_refuses_extra_classes_another_bytecode_major_and_extra_jar_members(self):
        view, rows = self.compiled_view()
        for index, (tools, message) in enumerate([
                (toolchain(classes=[*build.CLASSES, 'dydo/statechart/Extra.class']), 'Extra generated classes'),
                (toolchain(major=61), 'Expected Java11 class-file major55'),
                (toolchain(extra_member='META-INF/MANIFEST.MF'), 'Unexpected bootstrap jar members')]):
            with self.subTest(message=message), patch.object(build.subprocess, 'run', side_effect=tools), \
                    self.assertRaisesRegex(ValueError, message):
                build.build_once(self.root / 'jdk', view, self.root / f'once-{index}', rows)

    def recipe(self, artifact=None):
        """The build's real inputs in synthetic bytes: a recipe folder whose lock pins its own sources,
        a JDK archive and extraction, and a compiler-input jar."""
        here = self.root / 'recipe'
        sources = []
        for name in build.SOURCES:
            (here / name).parent.mkdir(parents=True, exist_ok=True)
            (here / name).write_bytes(b'// ' + name.encode())
            sources.append(dict(path=name, sha256=build.sha(here / name)))
        jdk, archive = self.fixture_archive(TOOLCHAIN)
        tlc, _view, lock, _data, _rows = self.compiler_fixture()
        lock.update(schema=1, bytecodeMajor=55, classes=build.CLASSES, sources=sources,
                    jdk=dict(archiveSha256=build.sha(archive)), artifact=artifact or dict(state='unbuilt', sha256=None))
        (here / 'build-lock.json').write_text(json.dumps(lock), encoding='utf-8')
        paths = {name: str(self.root / name) for name in ('work', 'artifact.jar', 'receipt.json')}
        arguments = argparse.Namespace(jdk=str(jdk), jdk_archive=str(archive), tlc=str(tlc), work=paths['work'],
                                       output=paths['artifact.jar'], receipt=paths['receipt.json'])
        return here, arguments

    def run_build(self, here, arguments, tools):
        with patch.object(build.sys, 'platform', 'linux'), patch.object(build, 'HERE', here), \
                patch.object(build, 'load_lock', functools.partial(build.load_lock, here)), \
                patch.object(build.subprocess, 'run', side_effect=tools):
            build.build(arguments)

    def test_two_fresh_builds_publish_one_byte_identical_artifact_and_its_receipt(self):
        here, arguments = self.recipe()
        self.run_build(here, arguments, toolchain())
        first, second = (self.root / 'work' / name / 'statechart-tlc-bootstrap.jar' for name in ('first', 'second'))
        artifact = Path(arguments.output).read_bytes()
        self.assertEqual(first.read_bytes(), artifact)
        self.assertEqual(second.read_bytes(), artifact)
        receipt = json.loads(Path(arguments.receipt).read_text(encoding='utf-8'))
        self.assertEqual(('built-not-runtime-validated', True, False, hashlib.sha256(artifact).hexdigest(), len(artifact)),
                         (receipt['state'], receipt['twoFreshBuildsByteIdentical'], receipt['runtimeProof'],
                          receipt['artifactSha256'], receipt['artifactBytes']))
        self.assertEqual(build.sha(here / 'build-lock.json'), receipt['buildLockSha256'])
        self.assertEqual(sorted(build.CLASSES), receipt['members'])

    def test_builds_that_differ_publish_nothing(self):
        here, arguments = self.recipe()
        with self.assertRaisesRegex(ValueError, 'Bootstrap byte reproducibility failed'):
            self.run_build(here, arguments, toolchain(varying=True))
        self.assertFalse(Path(arguments.output).exists())
        self.assertFalse(Path(arguments.receipt).exists())

    def test_a_rebuild_differing_from_the_sealed_artifact_publishes_nothing(self):
        here, arguments = self.recipe(dict(state='sealed-build', sha256='0' * 64))
        with self.assertRaisesRegex(ValueError, 'separately sealed artifact identity'):
            self.run_build(here, arguments, toolchain())
        self.assertFalse(Path(arguments.output).exists())

    def test_a_changed_compiler_input_stops_the_build_before_any_work(self):
        here, arguments = self.recipe()
        Path(arguments.tlc).write_bytes(Path(arguments.tlc).read_bytes() + b'changed')
        with self.assertRaisesRegex(ValueError, 'Pinned TLC compile input changed'):
            self.run_build(here, arguments, toolchain())
        self.assertFalse(Path(arguments.work).exists())

    def test_the_command_line_requires_every_owned_path(self):
        argv = ['build.py']
        for name in ('jdk', 'jdk-archive', 'tlc', 'work', 'output', 'receipt'):
            argv += ['--' + name, '/owned/' + name]
        with patch.object(build.sys, 'argv', argv), patch.object(build.sys, 'platform', 'win32'), \
                self.assertRaisesRegex(RuntimeError, 'never host javac'):
            build.main()
        with patch.object(build.sys, 'argv', argv[:-2]), contextlib.redirect_stderr(io.StringIO()) as errors, \
                self.assertRaises(SystemExit):
            build.main()
        self.assertIn('--receipt', errors.getvalue())


if __name__ == '__main__':
    unittest.main()

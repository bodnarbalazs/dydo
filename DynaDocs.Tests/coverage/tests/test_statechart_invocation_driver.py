"""Real unittest cases for the maintained driver; never execute frozen replay inputs."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SOURCE = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location('_dyd415_invocation_driver', SOURCE /
    'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation2/driver.py')
driver = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(driver)


class DriverTests(unittest.TestCase):
    def setUp(self):
        self.scope = tempfile.TemporaryDirectory(prefix='dyd415-driver-test-')
        self.addCleanup(self.scope.cleanup)
        self.root = Path(self.scope.name)

    def representation_fixture(self):
        receipt = driver.NATIVE + 'implementation2/request-store-longpaths/source-inputs.json'
        baseline = json.loads((SOURCE / receipt).read_bytes())['files']
        historical = [row['path'] for row in baseline if row['path'].startswith(driver.NATIVE)
            and not row['path'].startswith(driver.NATIVE + 'implementation2/')]
        for name in set(historical + list(driver.IMMUTABLE)) - set(driver.ORIGINALS):
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(SOURCE / name, target)
        return historical

    def test_valid_exact_representation_and_every_other_historical_byte(self):
        historical = self.representation_fixture()
        result = driver.representation(self.root, historical)
        self.assertEqual(11, result['originalsRepresented'])
        self.assertEqual(27, result['archiveMembers'])
        self.assertEqual(212, result['otherHistoricalNativeFilesUnchanged'])

    def test_missing_changed_manifest_archive_or_receipt_rejects(self):
        historical = self.representation_fixture()
        for name in driver.IMMUTABLE:
            target = self.root / name
            original = target.read_bytes()
            with self.subTest(name=name, mutation='missing'):
                target.unlink()
                with self.assertRaises(OSError):
                    driver.representation(self.root, historical)
            target.write_bytes(original + b'changed')
            with self.subTest(name=name, mutation='changed'):
                with self.assertRaisesRegex(AssertionError, 'checkpoint changed'):
                    driver.representation(self.root, historical)
            target.write_bytes(original)

    def test_unexpected_historical_deletion_or_source_drift_rejects(self):
        historical = self.representation_fixture()
        name = driver.NATIVE + 'commission/dependency-lock.json'
        target = self.root / name
        original = target.read_bytes()
        target.unlink()
        with self.assertRaises(OSError):
            driver.representation(self.root, historical)
        target.write_bytes(original + b'drift')
        with self.assertRaisesRegex(AssertionError, 'Historical source drift'):
            driver.representation(self.root, historical)

    def test_resurrected_original_is_not_a_blanket_subtree_exemption(self):
        historical = self.representation_fixture()
        target = self.root / driver.ORIGINALS[0]
        target.write_bytes(b'new executable replacement')
        with self.assertRaisesRegex(AssertionError, 'Exactly11 originals'):
            driver.representation(self.root, historical)

    def test_missing_changed_and_extra_archive_members_reject(self):
        original = b'fixture bytes, never executed'
        expected = [dict(entry='checkout/source.py', bytes=len(original), sha256=hashlib.sha256(original).hexdigest())]
        for mutation in ['valid', 'missing', 'changed', 'extra', 'link', 'duplicate']:
            archive = self.root / (mutation + '.zip')
            with zipfile.ZipFile(archive, 'x') as bundle:
                if mutation != 'missing':
                    info = zipfile.ZipInfo('checkout/source.py')
                    info.external_attr = (stat.S_IFLNK if mutation == 'link' else stat.S_IFREG) << 16
                    bundle.writestr(info, b'changed' if mutation == 'changed' else original)
                if mutation in ['extra', 'duplicate']:
                    bundle.writestr('other.py' if mutation == 'extra' else 'checkout/source.py', b'extra')
            with self.subTest(mutation=mutation):
                if mutation == 'valid':
                    driver.verify_members(archive, expected)
                else:
                    with self.assertRaises(AssertionError):
                        driver.verify_members(archive, expected)

    def test_unsafe_names_and_existing_extraction_output_reject(self):
        for name in ['', '/absolute', 'C:/drive', '//server/file', 'a\\b', 'a/../b', './a', 'a//b', 'a/.']:
            with self.subTest(name=name), self.assertRaises(AssertionError):
                driver.fidelity_name(name)
        archive = SOURCE / driver.ARCHIVE_PATHS[0]
        with zipfile.ZipFile(archive) as bundle:
            entries = {info.filename: bundle.read(info) for info in bundle.infolist()}
        destination = self.root / 'restored'
        destination.mkdir()
        rows = driver.fidelity_extract(archive, entries, destination)
        self.assertEqual(27, len(rows))
        first = destination / rows[0]['entry']
        first.write_bytes(b'foreign sentinel')
        with self.assertRaises(FileExistsError):
            driver.fidelity_extract(archive, entries, destination)
        self.assertEqual(b'foreign sentinel', first.read_bytes())

    def test_snapshot_source_closure_rejects_missing_changed_and_extra(self):
        target = self.root / 'source.txt'
        target.write_bytes(b'reviewed')
        rows = [dict(path='source.txt', sha256=driver.sha(target))]
        driver.verify_sources(self.root, rows)
        target.unlink()
        with self.assertRaises(AssertionError):
            driver.verify_sources(self.root, rows)
        target.write_bytes(b'changed')
        with self.assertRaises(AssertionError):
            driver.verify_sources(self.root, rows)
        target.write_bytes(b'reviewed')
        (self.root / 'extra').write_bytes(b'extra')
        with self.assertRaises(AssertionError):
            driver.verify_sources(self.root, rows)

    def test_only_named_lowercase_navigation_move_with_explicit_old_path_mapping(self):
        self.representation_fixture()
        directory = self.root / driver.NATIVE / 'history'
        readme = directory / 'readme.md'
        shutil.copy2(SOURCE / driver.ARCHIVE_PATHS[2], readme)
        driver.navigation_case(self.root)
        original = readme.read_bytes()
        readme.write_bytes(b'No historical path map')
        with self.assertRaisesRegex(AssertionError, 'path mapping'):
            driver.navigation_case(self.root)
        readme.write_bytes(original)
        temporary = directory / 'case-move.tmp'
        readme.rename(temporary)
        temporary.rename(directory / 'README.md')
        with self.assertRaisesRegex(AssertionError, 'lowercase'):
            driver.navigation_case(self.root)

    def test_real_hidden_child_argv_raw_streams_and_nonzero_exit_are_retained(self):
        child = self.root / 'child.py'
        child.write_text('import sys\nprint(repr(sys.argv[1:]))\nprint("raw stderr", file=sys.stderr)\nsys.exit(7)\n')
        arguments = [sys.executable, '-B', str(child), 'spaced input', 'árvíz']
        environment = dict(os.environ, LC_SUITE_SLOT_TICKET='owned-fixture')
        real_run = subprocess.run
        with patch.object(driver.subprocess, 'run', wraps=real_run) as run:
            with self.assertRaisesRegex(RuntimeError, 'inspect retained streams'):
                driver.run(self.root, 'child', arguments, self.root, environment)
        self.assertEqual(subprocess.CREATE_NO_WINDOW, run.call_args.kwargs['creationflags'])
        command = json.loads((self.root / 'child.command.json').read_bytes())
        self.assertEqual(arguments, command['argv'])
        self.assertEqual(7, command['exit'])
        self.assertIn('spaced input', (self.root / 'child.stdout').read_text())
        self.assertIn('raw stderr', (self.root / 'child.stderr').read_text())
        with self.assertRaises(FileExistsError):
            driver.run(self.root, 'child', arguments, self.root, environment)


if __name__ == '__main__':
    unittest.main()

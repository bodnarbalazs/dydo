"""Bounded bookkeeping tests. Run under the admitted fix4 driver, never native tests."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import unittest

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent


def load(name):
    spec = importlib.util.spec_from_file_location('fix4_' + name, HERE / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RecipeChecks(unittest.TestCase):
    def setUp(self):
        self.config = load('config')

    def test_clean_source_external_fresh_output(self):
        historic = (self.config.ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix3/finalize.py').read_text()
        condition = "assert status and all(row[3:].startswith(\n        'dydo/project/pitfalls/dyd-415-statechart-evidence/fix3/') for row in status)"
        self.assertIn(condition, historic)
        status = self.config.git('status', '--porcelain=v1', '--untracked-files=all').splitlines()
        self.assertEqual(status, [])
        argv = [sys.executable, '-c', 'status = ' + repr(status) + '\n' + condition]
        legacy = subprocess.run(argv, cwd=self.config.ROOT, capture_output=True,
            text=True, creationflags=subprocess.CREATE_NO_WINDOW, timeout=15)
        self.assertEqual(legacy.returncode, 1)
        self.assertIn('AssertionError', legacy.stderr)
        print(json.dumps(dict(check='Historical writer-only predicate on actual clean source',
            argv=argv, status=status, exit=legacy.returncode, stderr=legacy.stderr)))
        result = self.config.preflight()
        self.assertTrue(result['clean'] and result['runtimeMatchesPin'])
        self.assertFalse(result['measurementExecuted'])
        driver = load('driver')
        finalizer = load('finalize')
        report = finalizer.load('bounded_report', finalizer.RECIPE / 'report.py')
        self.assertEqual(driver.ROOT, self.config.ROOT)
        self.assertEqual(finalizer.OUT, self.config.OUT)
        self.assertEqual(Path(report.__file__).parent, HERE)
        self.assertEqual(finalizer.source_check(), self.config.source_check())

    def test_refuses_output_reuse(self):
        for path in (self.config.OUT, self.config.WORK):
            path.mkdir()
            try:
                with self.assertRaisesRegex(AssertionError, 'Fresh output'):
                    self.config.preflight()
            finally:
                path.rmdir()

    def test_refuses_source_output_overlap(self):
        self.config.OUT = self.config.ROOT / 'unsafe-evidence'
        with self.assertRaisesRegex(AssertionError, 'Keep source separate'):
            self.config.preflight()

    def test_refuses_unrelated_dirty_source(self):
        path = self.config.ROOT / 'THIRD-PARTY-NOTICES.md'
        original = path.read_bytes()
        try:
            path.write_bytes(original + b'\nproof-only unrelated edit\n')
            with self.assertRaisesRegex(AssertionError, 'Source must be clean'):
                self.config.preflight()
        finally:
            path.write_bytes(original)

    def test_refuses_committed_runtime_mismatch(self):
        # An index-clean source with the wrong runtime must fail independently of dirtiness.
        path = self.config.ROOT / 'skills/engineering/statechart/scripts/windows.ts'
        original = path.read_bytes()
        head = self.config.git('rev-parse', 'HEAD')
        try:
            path.write_bytes(original + b'\n// scratch runtime mismatch\n')
            self.config.git('add', '--', str(path))
            self.config.git('-c', 'user.name=Fix4 proof', '-c', 'user.email=proof@example.invalid',
                '-c', 'commit.gpgsign=false', 'commit', '-m', 'scratch mismatch only')
            with self.assertRaisesRegex(AssertionError, 'Runtime pin mismatch'):
                self.config.preflight()
        finally:
            self.config.git('reset', '--hard', head)


if __name__ == '__main__':
    unittest.main(verbosity=2)

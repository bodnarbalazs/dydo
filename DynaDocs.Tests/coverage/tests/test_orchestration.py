"""Every independent gate runs; incomplete measurement is never a green report."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_run import collect_all


def passed():
    return {'status': 'pass', 'facts': {}, 'findings': [], 'errors': []}


class OrchestrationTests(unittest.TestCase):
    def test_failed_measurement_does_not_prevent_independent_policy_findings(self):
        observed = []

        def broken():
            observed.append('broken')
            raise ValueError('missing method')

        def low_coverage():
            observed.append('coverage')
            return {'status': 'fail', 'facts': {}, 'findings': [{'path': 'source.py', 'gate': 'line-coverage'}], 'errors': []}

        result = collect_all({'first': broken, 'second': low_coverage}, ['first', 'second'])
        self.assertEqual(['broken', 'coverage'], observed)
        self.assertEqual(2, result['exit_code'])
        self.assertFalse(result['measurement_complete'])
        self.assertEqual('line-coverage', result['collectors']['second']['findings'][0]['gate'])

    def test_measured_policy_failure_is_distinct_from_incomplete_measurement(self):
        result = collect_all({'gate': lambda: {'status': 'fail', 'facts': {}, 'findings': [{'gate': 'cognitive'}], 'errors': []}}, ['gate'])
        self.assertEqual(1, result['exit_code'])
        self.assertTrue(result['measurement_complete'])

    def test_missing_collector_and_false_pass_are_errors(self):
        missing = collect_all({'first': passed}, ['first', 'second'])
        self.assertEqual(2, missing['exit_code'])
        malformed = collect_all({'first': lambda: {'status': 'pass', 'facts': {}, 'findings': [{'gate': 'hidden'}], 'errors': []}}, ['first'])
        self.assertEqual(2, malformed['exit_code'])

    def test_command_preserves_environment_and_retains_actual_exit_and_output(self):
        from gate_run import CommandLog
        import os
        before = dict(os.environ)
        with tempfile.TemporaryDirectory() as folder:
            log = CommandLog(Path(folder), Path(folder))
            row = log.run('fixture', [sys.executable, '-c', 'import os,sys; print(os.environ["SHELL"]); sys.exit(7)'])
            self.assertEqual(7, row['exit_code'])
            self.assertEqual('dydo-test-no-shell', Path(row['stdout']).read_text().strip())
            self.assertTrue(row['stdout_sha256'])
        self.assertEqual(before, dict(os.environ))

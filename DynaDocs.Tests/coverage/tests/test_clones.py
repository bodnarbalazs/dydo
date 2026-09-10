"""A native source omission needs its own current line/token eligibility proof."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_clones import collect_clones, eligibility
from gate_collect import Collectors
from gate_run import CommandLog

SHARED = '\n'.join(f'    step_{index} = ({index} + total) * (total - {index}) + len(str(total))'
                   for index in range(20))
DUPLICATED = f'def measure(total):\n{SHARED}\n    return total\n'


class CloneEligibilityTests(unittest.TestCase):
    def test_either_lower_bound_is_sufficient_but_both_native_facts_are_retained(self):
        for lines, tokens in [(13, 198), (24, 91)]:
            proof = eligibility({'sources': 0}, {'sources': 1, 'lines': lines, 'tokens': tokens})
            self.assertFalse(proof['eligible'])
            self.assertEqual({'lines': lines, 'tokens': tokens}, proof['denominator'])

    def test_omitted_eligible_unknown_or_ambiguous_source_fails_closed(self):
        for lower in [None, {'sources': 0, 'lines': 0, 'tokens': 0},
                      {'sources': 2, 'lines': 10, 'tokens': 10}, {'sources': 1, 'lines': 15, 'tokens': 100},
                      {'sources': 1, 'lines': -1, 'tokens': 10}, {'sources': 1, 'lines': 10, 'tokens': None}]:
            with self.subTest(lower=lower), self.assertRaises(ValueError):
                eligibility({'sources': 0}, lower)

    def test_native_source_eligibility_is_derived_from_both_thresholds(self):
        self.assertTrue(eligibility({'sources': 1, 'lines': 15, 'tokens': 100})['eligible'])
        self.assertFalse(eligibility({'sources': 1, 'lines': 14, 'tokens': 100})['eligible'])


class NativeCloneCollectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'first.py').write_text('import math\n' + DUPLICATED, encoding='utf-8')
        (self.root / 'second.py').write_text('import json\n' + DUPLICATED, encoding='utf-8')
        (self.root / 'small.py').write_text('def small():\n    return 1\n', encoding='utf-8')

    def runner(self, sources):
        collector = Collectors.__new__(Collectors)
        collector.root = self.root
        collector.coverage = Path(__file__).resolve().parents[1]
        collector.output = self.root / 'output'
        collector.log = CommandLog(self.root, collector.output / 'commands')
        collector.inventory = {'sources': sources}
        return collector

    def measured(self, names):
        return self.runner([{'path': name, 'language': 'python'} for name in names])

    def test_batch_duplicate_and_per_source_eligibility_are_measured_together(self):
        answer = collect_clones(self.measured(['first.py', 'second.py', 'small.py']))

        self.assertEqual([], answer['errors'])
        self.assertEqual('fail', answer['status'])
        finding = answer['findings'][0]
        self.assertEqual(['first.py', 'second.py'], [row['path'] for row in finding['fragments']])
        self.assertGreaterEqual(finding['lines'], 15)
        self.assertGreaterEqual(finding['tokens'], 100)
        proofs = {row['path']: row for row in answer['facts']['eligibility']}
        self.assertEqual([True, True, False], [proofs[name]['eligible'] for name in
                                               ('first.py', 'second.py', 'small.py')])
        self.assertFalse(proofs['small.py']['standard_included'])
        self.assertEqual({'lines': 2, 'tokens': 10}, proofs['small.py']['denominator'])

    def test_source_without_a_measurable_identity_is_an_accounted_error(self):
        sources = [{'path': 'first.py', 'language': 'python'}, {'path': 'second.py', 'language': 'python'},
                   {'path': 'small.py'}]

        answer = collect_clones(self.runner(sources))

        self.assertEqual('error', answer['status'])
        self.assertEqual(['small.py'], [row['path'] for row in answer['errors']])
        self.assertEqual(['first.py', 'second.py'], [row['path'] for row in answer['facts']['eligibility']])
        self.assertEqual(1, len(answer['findings']))

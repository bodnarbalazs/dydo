"""A native source omission needs its own current line/token eligibility proof."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_clones import eligibility


class CloneEligibilityTests(unittest.TestCase):
    def test_either_lower_bound_is_sufficient_but_both_native_facts_are_retained(self):
        for lines, tokens in [(13, 198), (24, 91)]:
            proof = eligibility({'sources': 0}, {'sources': 1, 'lines': lines, 'tokens': tokens})
            self.assertFalse(proof['eligible'])
            self.assertEqual({'lines': lines, 'tokens': tokens}, proof['denominator'])

    def test_omitted_eligible_unknown_or_ambiguous_source_fails_closed(self):
        for lower in [None, {'sources': 0, 'lines': 0, 'tokens': 0},
                      {'sources': 2, 'lines': 10, 'tokens': 10}, {'sources': 1, 'lines': 15, 'tokens': 100}]:
            with self.subTest(lower=lower), self.assertRaises(ValueError):
                eligibility({'sources': 0}, lower)

    def test_native_source_eligibility_is_derived_from_both_thresholds(self):
        self.assertTrue(eligibility({'sources': 1, 'lines': 15, 'tokens': 100})['eligible'])
        self.assertFalse(eligibility({'sources': 1, 'lines': 14, 'tokens': 100})['eligible'])

"""Focused correction contracts; real retained text is evidence, never rerun as a checker."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-action-source'
sys.path.insert(0, str(PACKET))


def load(name):
    spec = importlib.util.spec_from_file_location('_rmi_action_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


interpret, driver, launcher, worker = [load(name) for name in ['interpret', 'driver', 'launcher', 'worker']]
OLD = PACKET.parent / 'b3-20261031a-A/retained/output'


class RmiActionContracts(unittest.TestCase):
    def reference(self):
        code = json.loads((OLD / 'action-bound-reference.result.json').read_bytes())['exit']
        return interpret.baseline.semantic((OLD / 'action-bound-reference.stdout').read_text(encoding='utf-8'), code)

    def test_real_retained_reference_requires_exit12_and_exact_boundary_trace(self):
        facts = interpret.retained(ROOT)['action-bound-reference']
        self.assertEqual(12, facts['exit'])
        self.assertEqual([0, 1, 2], facts['values'])
        self.assertEqual((3, 2), (facts['generated'], facts['distinct']))
        self.assertFalse(facts['complete'])

    def test_retained_unconstrained_same_violation_has_three_admitted_states(self):
        retained = interpret.retained(ROOT)
        constrained = retained['action-bound-reference']
        unconstrained = retained['invariant-bad-reference']
        self.assertEqual(3, unconstrained['distinct'])
        self.assertEqual(unconstrained, dict(constrained, distinct=3))

    def test_old_success_assumption_is_rejected(self):
        facts = dict(self.reference(), exit=0, complete=True, invariant=[], values=[], actions=[])
        with self.assertRaises(ValueError):
            interpret.require(facts, 2)

    def test_wrong_generated_or_admission_count_is_rejected(self):
        for changes in [dict(generated=2), dict(distinct=3), dict(distinct=1)]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                interpret.require(dict(self.reference(), **changes), 2)

    def test_wrong_rule_temporal_loop_or_completion_is_rejected(self):
        for changes in [dict(invariant=['Other']), dict(temporal=True), dict(back=[1]),
                        dict(stuttering=True), dict(complete=True)]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                interpret.require(dict(self.reference(), **changes), 2)

    def test_missing_reordered_or_different_boundary_value_is_rejected(self):
        for values in [[0, 1], [0, 2, 1], [0, 1, 3]]:
            with self.subTest(values=values), self.assertRaises(ValueError):
                interpret.require(dict(self.reference(), values=values), 2)

    def test_exact_three_headers_and_two_next_actions_are_required(self):
        actions = self.reference()['actions']
        for changed in [actions[:2], [*actions, actions[-1]], ['Initial predicate', 'Other', actions[-1]]]:
            with self.subTest(actions=changed), self.assertRaises(ValueError):
                interpret.require(dict(self.reference(), actions=changed), 2)

    def test_missing_counts_and_unrelated_error_cannot_be_interpreted_as_expected(self):
        with self.assertRaises(ValueError):
            interpret.parse('Error: Parsing failed\n', 12, 2)
        text = (OLD / 'action-bound-reference.stdout').read_text(encoding='utf-8')
        with self.assertRaises(ValueError):
            interpret.parse(text + '\nError: Parsing failed\n', 12, 2)

    def test_reference_binding_detects_changed_bytes(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-action-contract-') as folder:
            root = Path(folder)
            path = root / 'reference.stdout'
            path.write_bytes(b'original')
            rows = [dict(path=path.name, bytes=8, sha256=hashlib.sha256(b'original').hexdigest())]
            interpret.verify_rows(root, rows)
            path.write_bytes(b'drifted!')
            with self.assertRaises(ValueError):
                interpret.verify_rows(root, rows)

    def test_launcher_requires_exact_successor_group_and_pin(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-action-contract-') as folder:
            root = Path(folder)
            (root / 'source-pin.json').write_bytes(b'{}')
            release = dict(state='RELEASED_B3_ACTION', groups=['contracts'], sourcePinSha256=hashlib.sha256(b'{}').hexdigest())
            (root / 'execution-release.json').write_text(json.dumps(release))
            self.assertEqual({}, launcher.validate_release(root, 'contracts'))
            with self.assertRaises(RuntimeError):
                launcher.validate_release(root, 'bootstrap')
            (root / 'source-pin.json').write_bytes(b'{"drift":true}')
            with self.assertRaises(RuntimeError):
                launcher.validate_release(root, 'contracts')

    def test_driver_rejects_noncommissioned_host_before_booking(self):
        with patch.object(driver.sys, 'platform', 'unsupported'):
            with self.assertRaises(RuntimeError):
                driver.validate_host()

    def test_worker_contract_selection_and_no_skip_interpretation_are_finite(self):
        fake = SimpleNamespace(REPO=ROOT, OUTPUT=Path('/output'))
        fake.invoke = unittest.mock.Mock(return_value=(dict(exit=0), '', 'Ran 12 tests\n\nOK\n' + '\n'.join('case ... ok' for _ in range(12))))
        fake.save = unittest.mock.Mock()
        with patch.object(worker, 'base', fake):
            worker.contracts()
            argv = fake.invoke.call_args.args[1]
            self.assertEqual('test_statechart_rmi_action.py', argv[-1])
            self.assertNotIn('test_statechart_rmi_b3.py', argv)
            fake.invoke.return_value = (dict(exit=0), '', 'Ran 12 tests\n\nOK (skipped=1)\n')
            with self.assertRaises(ValueError):
                worker.contracts()


if __name__ == '__main__':
    unittest.main()

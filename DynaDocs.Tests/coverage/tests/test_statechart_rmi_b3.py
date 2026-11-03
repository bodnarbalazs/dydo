"""B3 interpretation/admission contracts; no Java, Node or container is launched."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-source'
sys.path.insert(0, str(PACKET))


def load(name):
    spec = importlib.util.spec_from_file_location('_statechart_b3_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


worker, observer, driver, launcher, recording = [load(name) for name in ['worker', 'observe', 'driver', 'launcher', 'java_record']]


class RmiB3Contracts(unittest.TestCase):
    def facts(self):
        return dict(exit=12, generated=3, distinct=3, complete=False, invariant=['BelowTwo'], temporal=False,
                    values=[0, 1, 2], actions=['Initial predicate', 'Next line 5, col 1 of module Transport',
                    'Next line 5, col 1 of module Transport'], back=[], stuttering=False)

    def test_invariant_requires_shortest_ordered_path_and_next_actions(self):
        facts = self.facts()
        worker.expect('invariant-bad', facts)
        for changes in [dict(values=[0, 2]), dict(values=[0, 2, 1]), dict(invariant=['Other']),
                        dict(actions=['Init', 'Other', 'Next line 5'])]:
            bad = dict(facts, **changes)
            with self.assertRaises(ValueError):
                worker.expect('invariant-bad', bad)

    def test_fair_temporal_cycle_cannot_be_replaced_by_stuttering(self):
        facts = dict(self.facts(), exit=13, invariant=[], temporal=True, values=[0, 1, 2, 3], back=[1])
        worker.expect('liveness-bad', facts)
        for changes in [dict(stuttering=True), dict(back=[]), dict(values=[0]), dict(temporal=False)]:
            with self.assertRaises(ValueError):
                worker.expect('liveness-bad', dict(facts, **changes))

    def test_action_constraint_must_reduce_distinct_states_and_hold_below_two(self):
        good = dict(self.facts(), exit=0, complete=True, distinct=4, invariant=[], values=[], actions=[])
        worker.expect('good', good)
        with self.assertRaises(ValueError):
            worker.expect('action-bound', good)
        constrained = dict(good, distinct=2)
        worker.expect('action-bound', constrained)
        with self.assertRaises(ValueError):
            worker.expect('action-bound', dict(constrained, complete=False))

    def test_semantic_reader_retains_state_action_loop_and_requires_counts(self):
        text = ('Error: Invariant BelowTwo is violated.\nState 1: <Initial predicate>\nx = 0\n'
                'State 2: <Next line 5>\nx = 1\nState 3: <Next line 5>\nx = 2\n'
                '3 states generated, 3 distinct states found, 0 states left on queue.\n')
        facts = worker.semantic(text, 12)
        self.assertEqual([0, 1, 2], facts['values'])
        self.assertEqual(['Initial predicate', 'Next line 5', 'Next line 5'], facts['actions'])
        self.assertEqual(['BelowTwo'], facts['invariant'])
        with self.assertRaises(ValueError):
            worker.semantic('Error: unrelated failure', 12)

    def test_socket_word_decoding_distinguishes_loopback_wildcard_and_nonlocal(self):
        self.assertEqual('127.0.0.1', observer.address('0100007F', 'little'))
        self.assertEqual('127.0.0.1', observer.address('0000000000000000FFFF00000100007F', 'little'))
        self.assertEqual('127.0.0.1', observer.address('00000000000000000000FFFF7F000001', 'big'))
        self.assertEqual('0.0.0.0', observer.address('00000000', 'little'))
        self.assertEqual('10.0.0.1', observer.address('0100000A', 'little'))
        with self.assertRaises(ValueError):
            observer.address('00', 'little')

    def test_listener_evidence_requires_matching_process_fd_inode(self):
        table = 'header\n0: 0100007F:1234 00000000:0000 0A 0:0 00:0 0 0 0 456\n'
        self.assertEqual([], observer.listeners(dict(tcp=table, tcp6='header\n'), ['socket:[999]'], 'little'))
        owned = observer.listeners(dict(tcp=table, tcp6='header\n'), ['socket:[456]'], 'little')
        self.assertEqual([dict(table='tcp', address='127.0.0.1', port=4660, inode='456')], owned)

    def test_selected_discovery_rejects_skip_missing_or_unselected_execution(self):
        name = worker.C_NAMES[0]
        self.assertEqual([name], worker.selected_tap('ok 1 - ' + name + '\n', [name]))
        for text in ['ok 1 - ' + name + ' # SKIP unavailable\n', 'ok 1 - unrelated\n', '', 'not ok 1 - ' + name + '\n']:
            with self.assertRaises(ValueError):
                worker.selected_tap(text, [name])
        text = 'ok 1 - filtered # SKIP test name does not match pattern\nok 2 - ' + name + '\n'
        self.assertEqual([name], worker.selected_tap(text, [name]))

    def test_node_commands_bound_exact_files_and_six_case_selector(self):
        self.assertEqual([16, 12], [row[2] for row in worker.node_commands('B')])
        selected = worker.node_commands('C')
        self.assertEqual(1, len(selected))
        self.assertEqual(6, selected[0][2])
        self.assertEqual('test/corpus.test.ts', selected[0][1][-1])
        self.assertTrue(any(arg.startswith('--test-name-pattern=^(?:') for arg in selected[0][1]))
        self.assertNotIn('STATECHART_UPDATE', worker.environment('C'))

    def test_launcher_requires_exact_group_and_source_release(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-b3-test-') as folder:
            root = Path(folder)
            (root / 'source-pin.json').write_text('{}')
            release = dict(state='RELEASED_B3', groups=['A'], sourcePinSha256=hashlib.sha256(b'{}').hexdigest())
            (root / 'execution-release.json').write_text(json.dumps(release))
            self.assertEqual({}, launcher.validate_release(root, 'A'))
            with self.assertRaises(RuntimeError):
                launcher.validate_release(root, 'C')
            (root / 'source-pin.json').write_text('{"drift":true}')
            with self.assertRaises(RuntimeError):
                launcher.validate_release(root, 'A')

    def test_driver_refuses_host_network_before_container_start(self):
        identity = 'a' * 64
        view = dict(Id=identity, Name='/owned', Image=driver.IMAGE.split('@')[1],
                    Config=dict(Labels={'com.dydo.proof-owner': 'nonce'}),
                    HostConfig=dict(NetworkMode='host', ReadonlyRootfs=True))
        with patch.object(driver, 'run', side_effect=[identity, json.dumps([view])]) as run:
            with self.assertRaises(AssertionError):
                driver.container('owned', 'nonce', [], ['-B', '/input/worker.py'], [])
        self.assertEqual(2, run.call_count)
        self.assertIn('--pull=never', run.call_args_list[0].args[1])

    def test_cleanup_will_not_remove_a_foreign_owner(self):
        view = dict(Id='owned-id', Config=dict(Labels={'com.dydo.proof-owner': 'foreign'}))
        with patch.object(driver, 'run', return_value=json.dumps([view])) as run:
            with self.assertRaises(AssertionError):
                driver.cleanup([('owned', 'owned-id')], 'volume', 'ours')
        self.assertEqual(1, run.call_count)

    def test_java_recording_claim_enforces_finite_ceiling_without_replacement(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-b3-test-') as folder:
            root = Path(folder)
            for index in range(64):
                with recording.claim(root) as stream:
                    stream.write(str(index))
            with self.assertRaises(RuntimeError):
                recording.claim(root)
            self.assertEqual('0', (root / 'java-00.json').read_text())
            self.assertEqual(64, len(list(root.iterdir())))


if __name__ == '__main__':
    unittest.main()

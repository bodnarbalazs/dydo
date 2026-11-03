"""Genuine unittest contracts for finite B2 admission, containment and result interpretation.

These cases never launch Docker, Java or a compiler. Actual socket proof is a separate release.
"""
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b2-source'


def load(name):
    spec = importlib.util.spec_from_file_location('_statechart_b2_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


worker = load('worker')
driver = load('driver')
launcher = load('launcher')


class RmiB2Contracts(unittest.TestCase):
    def test_exact_finite_classpaths_and_process_inventory(self):
        rows = worker.runtime_commands()
        self.assertEqual(worker.MODES, [row['name'] for row in rows[:6]])
        self.assertEqual(['forward-0', 'forward-7', 'forward-throw'], [row['name'] for row in rows[6:]])
        self.assertEqual(9, len(rows))
        for row in rows:
            self.assertEqual(worker.JDK + '/bin/java', row['argv'][0])
            self.assertNotIn('tla2tools', ':'.join(row['argv']))
            self.assertNotIn('compiler-tlc', ':'.join(row['argv']))
        for row in rows[6:]:
            self.assertEqual(worker.HELPER + ':' + worker.CONTROL, row['argv'][2])
            self.assertEqual(worker.ARGUMENTS, row['argv'][4:])
        self.assertEqual(worker.HELPER + ':' + worker.PROBE + ':' + worker.CONTROL, rows[4]['argv'][2])

    def test_fixture_compilers_cannot_recompile_product_or_consume_official_manifest(self):
        commands = worker.compiler_commands()
        self.assertEqual(2, len(commands))
        for argv in commands:
            self.assertEqual('11', argv[2])
            self.assertIn('-Werror', argv)
            self.assertIn('-proc:none', argv)
            self.assertIn('-implicit:none', argv)
            self.assertNotIn('LoopbackTlc.java', ' '.join(argv))
            self.assertNotIn('LoopbackSockets.java', ' '.join(argv))
            self.assertNotIn('compiler-tlc', ' '.join(argv))
        self.assertEqual(worker.HELPER, commands[0][commands[0].index('-classpath') + 1])
        self.assertEqual('/work/empty', commands[1][commands[1].index('-classpath') + 1])

    def test_native_endian_mapped_loopback_and_nonlocal_addresses(self):
        cases = [('0100007F', 'little'), ('7F000001', 'big'),
                 ('0000000000000000FFFF00000100007F', 'little'),
                 ('00000000000000000000FFFF7F000001', 'big')]
        for encoded, order in cases:
            self.assertTrue(worker.loopback_address(encoded, order))
        for encoded in ['00000000', '00000000000000000000000000000000', '0100000A',
                        '00000000000000000000000001000000']:
            self.assertFalse(worker.loopback_address(encoded, 'little'))
        with self.assertRaises(ValueError):
            worker.loopback_address('00', 'little')

    def observation(self, endpoint='0100007F:1234', owned=True):
        return ('PROCESS 12\nPROC_STAT 12 (java)\nFD /proc/self/fd/3 socket:[456]\n'
                'TCP header\nTCP6 header\nLISTEN tcp ' + endpoint + ' inode=456 owned=' + str(owned).lower() + '\n').encode()

    def test_dual_table_observation_requires_owned_socket(self):
        with patch.object(worker.sys, 'byteorder', 'little'):
            self.assertEqual(1, worker.observe(self.observation())['processOwnedLoopbackListeners'])
            for output in [self.observation(owned=False), self.observation().replace(b'TCP6 header\n', b''),
                           self.observation('00000000:1234'), self.observation('0100000A:1234')]:
                with self.assertRaises(ValueError):
                    worker.observe(output)

    def test_bound_requires_exchange_and_rmi_requires_round_trip(self):
        for row, marker in zip(worker.runtime_commands()[:2],
                               [b'BOUND_EXCHANGE_OK numeric=41/42 localhost=43/44\n', b'RMI_LOCAL_OK stub\n']):
            with patch.object(worker.sys, 'byteorder', 'little'):
                self.assertTrue(worker.validate(row, 0, self.observation() + marker, b'')['dualTables'])
            for stdout, stderr in [(self.observation(), b''), (marker, b''), (marker, b'warning')]:
                with self.assertRaises(ValueError):
                    worker.validate(row, 0, stdout, stderr)

    def test_failure_requires_exact_diagnostic_and_no_tlc_entry(self):
        for row in worker.runtime_commands()[2:6]:
            diagnostic = worker.ERRORS[row['name']].encode()
            self.assertTrue(worker.validate(row, 1, b'', diagnostic)['expectedFailure'])
            for code, stdout, stderr in [(1, b'', b'unrelated error'), (0, b'', diagnostic),
                                         (1, b'CONTROL_TLC_ENTERED', diagnostic),
                                         (1, b'', diagnostic + b'\nAssertionError')]:
                with self.assertRaises(ValueError):
                    worker.validate(row, code, stdout, stderr)

    def forwarded(self):
        lines = ['CONTROL_TLC_ENTERED ' + str(len(worker.ARGUMENTS))]
        lines += [str(index) + ':' + base64.b64encode(value.encode()).decode()
                  for index, value in enumerate(worker.ARGUMENTS)]
        return ('\n'.join(lines) + '\n').encode()

    def test_forwarding_checks_empty_space_unicode_option_order_exit_and_stderr(self):
        for row in worker.runtime_commands()[6:8]:
            self.assertTrue(worker.validate(row, row['exit'], self.forwarded(), b'CONTROL_STDERR\n')['exactArgvBytes'])
            for code, stdout, stderr in [(99, self.forwarded(), b'CONTROL_STDERR\n'),
                                         (row['exit'], self.forwarded().replace(b'0:', b'9:'), b'CONTROL_STDERR\n'),
                                         (row['exit'], self.forwarded(), b'')]:
                with self.assertRaises(ValueError):
                    worker.validate(row, code, stdout, stderr)

    def test_thrown_error_requires_original_stderr_and_error_identity(self):
        row = worker.runtime_commands()[-1]
        stderr = b'CONTROL_STDERR\nException in thread "main" java.lang.IllegalStateException: CONTROL_THROW\n'
        self.assertTrue(worker.validate(row, 1, self.forwarded(), stderr)['stderrPreserved'])
        with self.assertRaises(ValueError):
            worker.validate(row, 1, self.forwarded(), b'CONTROL_STDERR\nother exception')

    def test_launcher_refuses_redirected_host_before_startup(self):
        with patch.dict(launcher.os.environ, {'LOCALAPPDATA': 'C:/unapproved-namespace'}):
            with self.assertRaises(RuntimeError):
                launcher.validate_host()

    def test_launcher_requires_captain_release_for_exact_source_packet(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-b2-contract-') as directory:
            packet = Path(directory)
            (packet / 'source-pin.json').write_text('{}')
            with self.assertRaises(FileNotFoundError):
                launcher.validate_release(packet)
            release = packet / 'execution-release.json'
            release.write_text(json.dumps(dict(state='HELD', sourcePinSha256='bad')))
            with self.assertRaises(RuntimeError):
                launcher.validate_release(packet)
            release.write_text(json.dumps(dict(state='RELEASED_B2', sourcePinSha256=hashlib.sha256(b'{}').hexdigest())))
            self.assertEqual({}, launcher.validate_release(packet))
            (packet / 'source-pin.json').write_text('{"drift":true}')
            with self.assertRaises(RuntimeError):
                launcher.validate_release(packet)

    def test_container_refuses_wildcard_network_before_start(self):
        identity = 'a' * 64
        view = dict(Id=identity, Name='/owned', Image=driver.IMAGE.split('@')[1],
                    Config=dict(Labels={'com.dydo.proof-owner': 'nonce'}),
                    HostConfig=dict(NetworkMode='host', ReadonlyRootfs=True))
        with patch.object(driver, 'run', side_effect=[identity, json.dumps([view])]) as run:
            with self.assertRaises(AssertionError):
                driver.container('owned', 'nonce', [], ['-B', '/input/worker.py'], [])
        self.assertEqual(2, run.call_count)
        create = run.call_args_list[0].args[1]
        for value in ['--network', 'none', '--pull=never', '--read-only', '--cap-drop', 'ALL',
                      'no-new-privileges', '--pids-limit', '128', '--memory', '1g', '--cpus', '2']:
            self.assertIn(value, create)

    def test_cleanup_checks_owned_identity_and_does_not_remove_foreign_container(self):
        view = dict(Id='owned-id', Config=dict(Labels={'com.dydo.proof-owner': 'foreign'}))
        with patch.object(driver, 'run', return_value=json.dumps([view])) as run:
            with self.assertRaises(AssertionError):
                driver.cleanup([('owned', 'owned-id')], 'owned-volume', 'our-nonce')
        self.assertEqual(1, run.call_count)
        self.assertEqual(['docker', 'inspect', 'owned-id'], run.call_args.args[1])


if __name__ == '__main__':
    unittest.main()

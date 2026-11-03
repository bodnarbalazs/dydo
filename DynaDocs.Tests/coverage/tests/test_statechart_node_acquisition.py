"""Source-prepared acquisition contracts, using local byte streams and no network."""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-node-source'


def load(name):
    spec = importlib.util.spec_from_file_location('_node_acquisition_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


driver, launcher, transfer = [load(name) for name in ['driver', 'launcher', 'transfer']]


class NodeAcquisitionContracts(unittest.TestCase):
    def test_checksum_requires_one_exact_official_filename_and_digest(self):
        line = driver.EXPECTED + '  ' + driver.ARCHIVE
        self.assertEqual(line, driver.checksum_line((line + '\n').encode()))
        for raw in [(line+'\n'+line+'\n').encode(), line.replace(driver.EXPECTED, '0'*64).encode(), b'wrong-file\n']:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                driver.checksum_line(raw)

    def test_unapproved_url_rejected_before_any_request(self):
        opener = Mock()
        with self.assertRaises(ValueError):
            driver.fetch('http://nodejs.org/archive', Path('unused'), 10, time.monotonic()+5, opener)
        opener.open.assert_not_called()

    def test_redirect_is_refused_and_response_closed(self):
        response = io.BytesIO(b'redirect')
        with self.assertRaises(RuntimeError):
            driver.NoRedirect().redirect_request(None, response, 302, 'redirect', {}, driver.URL)
        self.assertTrue(response.closed)

    def test_transfer_preserves_exact_bytes_hash_and_closes_owned_response(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-node-contract-') as folder:
            root = Path(folder)
            response = io.BytesIO(b'exact archive bytes')
            response.status, response.headers = 200, {'Content-Length': '19'}
            response.geturl = lambda: driver.URL
            with patch.object(driver, 'OUTPUT', root):
                facts = driver.fetch(driver.URL, root/'archive', 100, time.monotonic()+5, Mock(open=Mock(return_value=response)))
            self.assertEqual(b'exact archive bytes', (root/'archive').read_bytes())
            self.assertEqual(hashlib.sha256(b'exact archive bytes').hexdigest(), facts['sha256'])
            self.assertTrue(response.closed)

    def test_output_collision_does_not_issue_a_request(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-node-contract-') as folder:
            root = Path(folder)
            (root/'archive').write_bytes(b'preserved')
            opener = Mock()
            with patch.object(driver, 'OUTPUT', root), self.assertRaises(FileExistsError):
                driver.fetch(driver.URL, root/'archive', 100, time.monotonic()+5, opener)
            opener.open.assert_not_called()
            self.assertEqual(b'preserved', (root/'archive').read_bytes())

    def test_excessive_or_wrong_response_is_rejected_without_retry(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-node-contract-') as folder:
            root = Path(folder)
            response = io.BytesIO(b'oversized')
            response.status, response.headers = 200, {'Content-Length': '9'}
            response.geturl = lambda: driver.URL
            opener = Mock(open=Mock(return_value=response))
            with patch.object(driver, 'OUTPUT', root), self.assertRaises(ValueError):
                driver.fetch(driver.URL, root/'archive', 2, time.monotonic()+5, opener)
            self.assertEqual(1, opener.open.call_count)
            self.assertTrue(response.closed)

    def test_expired_deadline_refuses_request_and_output_creation(self):
        opener = Mock()
        with self.assertRaises(TimeoutError):
            driver.fetch(driver.URL, Path('unused'), 10, time.monotonic()-1, opener)
        opener.open.assert_not_called()

    def test_launcher_requires_exact_acquisition_pin(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-node-contract-') as folder:
            root = Path(folder)
            (root/'source-pin.json').write_bytes(b'{}')
            release = dict(state='RELEASED_B3_NODE_DATA', sourcePinSha256=hashlib.sha256(b'{}').hexdigest())
            (root/'execution-release.json').write_text(json.dumps(release))
            self.assertEqual({}, launcher.validate_release(root))
            (root/'source-pin.json').write_bytes(b'{"drift":true}')
            with self.assertRaises(RuntimeError):
                launcher.validate_release(root)

    def test_transfer_child_rejects_uninherited_ticket_before_network(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-node-contract-') as folder:
            root = Path(folder)
            scratch = root / 'dyd415-rmi-b3-node-owned'
            scratch.mkdir()
            output = root / 'output'
            (root / 'dydo-fullsuite.lock').write_text(json.dumps(dict(pid=os.getppid(), ticket='parent-ticket')))
            context = dict(scratch=str(scratch), output=str(output), parentPid=os.getppid(), ticket='parent-ticket',
                lockSha256=transfer.driver.sha(root / 'dydo-fullsuite.lock'), sourcePinSha256='unused')
            (scratch / 'context.json').write_text(json.dumps(context))
            with patch.object(transfer.driver, 'TEMP', root), patch.object(transfer.driver, 'OUTPUT', output), \
                    patch.object(transfer.driver, 'validate_host'), patch.dict(os.environ, LC_SUITE_SLOT_TICKET='wrong-ticket'):
                with self.assertRaisesRegex(RuntimeError, 'inherited parent FIFO ticket'):
                    transfer.validate_context(scratch / 'context.json')


if __name__ == '__main__':
    unittest.main()

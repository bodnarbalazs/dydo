"""Source-only direct contracts for the twelve successor; isolated unittest locks only."""
from contextlib import redirect_stderr
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

PACKET = Path(__file__).resolve().parent.parent / 'b3-janitor-twelve-source'


def load(name):
    spec = importlib.util.spec_from_file_location('_twelve_contract_' + name, PACKET / (name + '.py'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


driver, launcher = [load(name) for name in ['driver', 'launcher']]


class TwelveDriverContracts(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory(prefix='dyd415-twelve-driver-contract-')
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)

    def test_retention_failure_with_successful_logging_keeps_exact_isolated_lock(self):
        lock = self.root / 'isolated.lock'
        owner = b'isolated-owner\n'
        lock.write_bytes(owner)
        original = OSError('retention failed')
        with patch.object(driver, 'EVIDENCE_ERRORS', []), patch.object(driver.common, 'save') as saved, \
                patch.object(driver.common, 'retain_work', side_effect=original):
            self.assertIs(original, driver.retain_and_release(lock, owner, True, None))
        self.assertEqual(owner, lock.read_bytes())
        self.assertEqual('retain-work-error.json', saved.call_args.args[0])

    def test_healthy_retention_releases_only_matching_isolated_lock(self):
        lock = self.root / 'isolated.lock'
        lock.write_bytes(b'owner')
        with patch.object(driver, 'EVIDENCE_ERRORS', []), patch.object(driver.common, 'retain_work'):
            self.assertIsNone(driver.retain_and_release(lock, b'owner', True, None))
        self.assertFalse(lock.exists())

    def test_launcher_requires_exact_twelve_release_and_source_identity(self):
        pin = self.root / 'source-pin.json'
        pin.write_bytes(b'{"fixture":true}')
        release = dict(state='RELEASED_B3_JANITOR_TWELVE', twelve=True,
            sourcePinSha256=hashlib.sha256(pin.read_bytes()).hexdigest(),
            captainInspectionRecord='isolated fixture', dockerIdentity={'fixture': True})
        for change in [{}, {'twelve': False}, {'state': 'SOURCE_ONLY'},
                       {'sourcePinSha256': '0' * 64}, {'dockerIdentity': None}]:
            with self.subTest(change=change):
                (self.root / 'execution-release.json').write_text(json.dumps(dict(release, **change)), encoding='utf-8')
                if change:
                    with self.assertRaises(RuntimeError):
                        launcher.validate_release(self.root)
                else:
                    self.assertEqual({'fixture': True}, launcher.validate_release(self.root))

    def test_missing_or_alternative_selector_refuses_before_admission(self):
        for module, boundary in [(driver, 'verify_inputs'), (launcher, 'validate_host')]:
            for arguments in [[], ['--signal', 'SIGINT'], ['--contracts']]:
                with self.subTest(module=module.__name__, arguments=arguments), \
                        patch.object(sys, 'argv', ['source.py', *arguments]), \
                        patch.object(module, boundary) as material, redirect_stderr(io.StringIO()):
                    with self.assertRaises(SystemExit) as error:
                        module.main()
                    self.assertEqual(2, error.exception.code)
                    material.assert_not_called()


if __name__ == '__main__':
    unittest.main()

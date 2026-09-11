"""The shipped facade example is one mechanically derived byte copy."""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "sync_testing_example.py"


class SyncTestingExampleTests(unittest.TestCase):
    def test_copy_and_read_only_check(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source = root / "gap_check.py"
            target = root / "example.py"
            source.write_bytes(b"one\r\ntwo\n")
            command = [sys.executable, str(SCRIPT), "--source", str(source), "--target", str(target)]
            self.assertEqual(0, subprocess.run(command).returncode)
            self.assertEqual(source.read_bytes(), target.read_bytes())
            self.assertEqual(0, subprocess.run([*command, "--check"]).returncode)
            target.write_bytes(b"drift")
            self.assertEqual(2, subprocess.run([*command, "--check"]).returncode)
            self.assertEqual(b"drift", target.read_bytes())


if __name__ == "__main__":
    unittest.main()

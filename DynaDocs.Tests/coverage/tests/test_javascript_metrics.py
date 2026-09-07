"""Run the real Node collector contract under the public Python gate suite."""
import shutil
import tempfile
import json
import os
import subprocess
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_job


class JavaScriptMetricsTests(unittest.TestCase):
    def test_official_metrics_and_failure_contracts(self):
        output = Path(tempfile.mkdtemp(prefix="dydo-node-baseline-", dir=os.environ.get("DYDO_NATIVE_TEST_EVIDENCE")))
        root = Path(__file__).resolve().parents[3]
        controller = output / "controller.py"
        controller.write_bytes(Path(windows_job.__file__).read_bytes())
        value = windows_job.request([shutil.which("node"), str(root / "DynaDocs.Tests/coverage/node_tests.cjs")],
                                    root, output / "owner", 300, 10)
        observed = subprocess.run([sys.executable, str(controller)], input=json.dumps(value), text=True,
                                  capture_output=True, encoding="utf-8", timeout=310)
        self.assertEqual(0, observed.returncode, observed.stdout + observed.stderr)
        result = json.loads(observed.stdout)
        self.assertTrue(result["complete"], result)
        stdout = Path(result["stdout_path"]).read_text(encoding="utf-8")
        stderr = Path(result["stderr_path"]).read_text(encoding="utf-8")
        self.assertEqual(0, result["subject_status"], stdout + stderr)
        self.assertRegex(stdout, r"# skipped 0\b")
        self.assertRegex(stdout, r"# todo 0\b")
        if not os.environ.get("DYDO_NATIVE_TEST_EVIDENCE"):
            shutil.rmtree(output)


if __name__ == "__main__":
    unittest.main()

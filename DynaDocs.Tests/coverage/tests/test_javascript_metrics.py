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
from gate_collect import Collectors
from gate_run import CommandLog

LEFT_MODULE = ("const right = require('./right.cjs');\n"
               "const orphan = require('./orphan.cjs');\n"
               "require('./absent.cjs');\n"
               "/* c8 ignore next */\n"
               "module.exports.left = () => right.value() + orphan.value();\n")
RIGHT_MODULE = ("const left = require('./left.cjs');\n"
                "module.exports.value = () => (left.left ? 1 : 2);\n")
ORPHAN_MODULE = "module.exports.value = () => 3;\n"
ESM_MODULE = "export const total = (value) => (value > 0 ? value : -value);\n"
UNPARSABLE_MODULE = "module.exports = (=> {\n"


class JavaScriptCollectorTests(unittest.TestCase):
    def setUp(self):
        self.tooling = Path(__file__).resolve().parents[1]
        self.repository = Path(tempfile.mkdtemp(prefix="dyd96-javascript-collectors-"))
        self.addCleanup(shutil.rmtree, self.repository, True)
        for name, text in (("left.cjs", LEFT_MODULE), ("right.cjs", RIGHT_MODULE),
                           ("orphan.cjs", ORPHAN_MODULE), ("total.mjs", ESM_MODULE),
                           ("unparsable.cjs", UNPARSABLE_MODULE)):
            (self.repository / name).write_text(text, encoding="utf-8")

    def collector(self, names):
        runner = Collectors.__new__(Collectors)
        runner.root, runner.coverage = self.repository, self.tooling
        runner.output = self.repository / "gate-output"
        runner.log = CommandLog(self.repository, runner.output / "commands")
        runner.inventory = {"sources": [{"path": name, "language": "javascript"} for name in names]}
        runner.static = {}
        return runner

    def test_real_sonarjs_metrics_are_measured_for_both_module_kinds(self):
        runner = self.collector(["left.cjs", "total.mjs"])

        answer = runner.javascript_source()

        self.assertEqual("fail", answer["status"])
        self.assertEqual(["left.cjs", "total.mjs"], [row["path"] for row in answer["facts"]["modules"]])
        self.assertEqual([{"path": "left.cjs", "gate": "coverage-suppression",
                           "diagnostic": {"line": 4, "column": 0}}], answer["findings"])
        self.assertEqual(["commonjs", "module"],
                         [row["command"][-1] for row in runner.log.rows])
        self.assertEqual(runner.static["javascript"], answer["facts"]["modules"])

    def test_a_module_the_metrics_producer_rejects_is_an_accounted_error(self):
        runner = self.collector(["orphan.cjs", "unparsable.cjs"])

        answer = runner.javascript_source()

        self.assertEqual("error", answer["status"])
        self.assertEqual(["orphan.cjs"], [row["path"] for row in answer["facts"]["modules"]])
        self.assertEqual(["unparsable.cjs"], [row["path"] for row in answer["errors"]])
        self.assertIn("javascript-source-1", answer["errors"][0]["message"])

    def test_the_native_dependency_graph_separates_cycles_gaps_and_foreign_modules(self):
        runner = self.collector(["left.cjs", "right.cjs", "total.mjs"])

        answer = runner.javascript_dependencies()

        self.assertEqual("error", answer["status"])
        self.assertEqual([{"gate": "module-cycle", "members": ["left.cjs", "right.cjs"]}], answer["findings"])
        self.assertEqual([["left.cjs", "right.cjs"], ["right.cjs", "left.cjs"]], answer["facts"]["edges"])
        self.assertEqual(["left.cjs"], [row["path"] for row in answer["errors"]])
        self.assertEqual("./absent.cjs", answer["errors"][0]["dependency"]["module"])
        self.assertIn("orphan.cjs", [row["source"] for row in answer["facts"]["native"]["modules"]])


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

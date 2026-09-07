"""DYD-96 public gate-completion contract, exercised through the real facade."""
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


RUNNER = Path(__file__).resolve().parents[1] / "gap_check.py"


def capability(argv, artifact):
    return {
        "state": "configured",
        "command": {"kind": "current-python", "argv": argv},
        "artifacts": [{"path": artifact, "required": True}],
    }


class AssuranceAdoptionTests(unittest.TestCase):
    def invoke(self, gate, exits):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        root = Path(folder.name)
        shutil.copyfile(RUNNER, root / "gap_check.py")
        stacks = []
        for index, code in enumerate(exits):
            artifact = f"artifacts/{gate}-{index}.json"
            script = (
                "import json,pathlib,sys;"
                f"p=pathlib.Path({artifact!r});p.parent.mkdir(parents=True,exist_ok=True);"
                f"p.write_text(json.dumps({{'schema':1,'exitCode':{code}}}));"
                f"raise SystemExit({code})"
            )
            unavailable = {"state": "unavailable", "reason": "fixture"}
            caps = {name: unavailable.copy() for name in ("test", "static", "coverage", "mutation")}
            argv = ["-c", script]
            if gate == "mutation":
                argv.append("{base}")
            caps[gate] = capability(argv, artifact)
            stacks.append({
                "name": f"fixture-{index}",
                "kind": "python",
                "cwd": ".",
                "isolation": {"requirement": "in-place", "evidence": {"state": "verified", "kind": "direct"}},
                "capabilities": caps,
            })
        (root / "gap_check.json").write_text(json.dumps({"schema": 1, "artifactRoot": "results", "stacks": stacks}))
        args = ["gate", gate]
        if gate == "mutation":
            args.extend(["--since", "BASE"])
        process = subprocess.run([sys.executable, str(root / "gap_check.py"), *args], cwd=root,
                                 text=True, capture_output=True, encoding="utf-8")
        result_path = next(root.glob("results/*/result.json"))
        return process, json.loads(result_path.read_text())

    def test_non_test_adapter_preserves_reserved_completion_codes(self):
        expected = {0: ("passed", 0), 1: ("failed", 1), 2: ("invalid", 2),
                    17: ("failed", 1), 130: ("interrupted", 130)}
        for gate in ("static", "coverage", "mutation"):
            for native, outcome in expected.items():
                with self.subTest(gate=gate, native=native):
                    process, payload = self.invoke(gate, [native])
                    row = payload["results"][0]
                    self.assertEqual((outcome[0], native, outcome[1]),
                                     (row["state"], row["childExit"], row["resultExit"]))
                    self.assertEqual(outcome[1], process.returncode)

    def test_invalid_outweighs_failure_after_all_independent_rows_run(self):
        for gate in ("static", "coverage", "mutation"):
            with self.subTest(gate=gate):
                process, payload = self.invoke(gate, [1, 2, 0])
                self.assertEqual(["failed", "invalid", "passed"],
                                 [row["state"] for row in payload["results"]])
                self.assertEqual([1, 2, 0], [row["childExit"] for row in payload["results"]])
                self.assertEqual(2, process.returncode)


if __name__ == "__main__":
    unittest.main()

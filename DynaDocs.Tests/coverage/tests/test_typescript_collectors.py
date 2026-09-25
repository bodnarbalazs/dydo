"""The viewer's TypeScript collectors measure with native tools and account for every source."""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import gate_collect
from gate_collect import Collectors
from gate_knip import collect_knip, workspace_model
from gate_run import CommandLog
from inventory import git_file_state

TOOLS = Path(__file__).resolve().parents[1]
REPOSITORY = TOOLS.parents[1]
DEEP_MODULE = "export const x = 1;\n" + "".join(
    f"if (x > {index}) {{ if (x > {index + 1}) {{ console.log({index}); }} }}\n" for index in range(8))


def stub_script(stdout, exit_code):
    """A viewer script stand-in printing the native tool's output and exiting as it would."""
    def script(_name, *arguments):
        report = next((Path(value) for flag, value in zip(arguments, arguments[1:])
                       if flag == "--output-file"), None)
        code = (f"import pathlib,sys; pathlib.Path({str(report)!r}).write_text({stdout!r})"
                if report else f"import sys; sys.stdout.write({stdout!r})")
        return [sys.executable, "-c", f"{code}; sys.exit({exit_code})"]
    return script


def aliased(directory):
    """The directory reached through a second name, as Windows reaches a temp folder through its 8.3
    short name while the tools report the long one; the plain directory where links are refused."""
    directory.mkdir()
    alias = directory.with_name("RUNNER~1")
    try:
        alias.symlink_to(directory, target_is_directory=True)
    except OSError:
        return directory
    return alias


class TypeScriptCollectorTests(unittest.TestCase):
    def setUp(self):
        base = Path(tempfile.mkdtemp(prefix="dyd266-typescript-collectors-"))
        self.addCleanup(shutil.rmtree, base, True)
        self.repository = aliased(base / "repository")
        sources = {"viewer/src/left.ts": "import type { Right } from './right';\nexport const left = (r: Right) => r;\n",
                   "viewer/src/right.ts": "import { left } from './left';\nexport type Right = typeof left;\n",
                   "viewer/src/view.tsx": "export function View() {\n  return <p>{1}</p>;\n}\n",
                   "viewer/src/deep.ts": DEEP_MODULE}
        for relative, text in sources.items():
            (self.repository / relative).parent.mkdir(parents=True, exist_ok=True)
            (self.repository / relative).write_text(text, encoding="utf-8")

    def collector(self, names):
        runner = Collectors.__new__(Collectors)
        # Collectors.__init__ resolves its root; so does this stand-in for it.
        runner.root, runner.coverage = self.repository.resolve(), TOOLS
        runner.output = runner.root / "gate-output"
        runner.log = CommandLog(runner.root, runner.output / "commands")
        runner.inventory = {"sources": [{"path": name, "language": "typescript"} for name in names],
                            "excluded": []}
        runner.static = {}
        return runner

    def test_the_walker_reads_both_grammars_and_charges_a_module_its_top_level_nesting(self):
        runner = self.collector(["viewer/src/deep.ts", "viewer/src/view.tsx"])

        answer = runner.typescript_source()

        self.assertEqual(["tsx", "typescript"], sorted(row["command"][-1] for row in runner.log.rows))
        self.assertEqual(["View:1:7"], [row["id"] for row in answer["facts"]["modules"][1]["methods"]])
        self.assertEqual([{"path": "viewer/src/deep.ts", "member": "<module>", "gate": "cognitive",
                           "actual": 24, "threshold": 20}], answer["findings"])

    def test_a_type_only_import_still_closes_a_module_cycle(self):
        answer = self.collector(["viewer/src/left.ts", "viewer/src/right.ts"]).typescript_dependencies()

        self.assertEqual([{"gate": "module-cycle", "members": ["viewer/src/left.ts", "viewer/src/right.ts"]}],
                         answer["findings"])

    def test_each_compiler_diagnostic_is_a_finding_and_a_silent_failure_is_a_gap(self):
        output = "> tsc --noEmit\nsrc/a.ts(3,7): error TS2322: Type 'string' is not assignable.\n"
        for stdout, exit_code, status in ((output, 2, "fail"), ("> tsc\n", 2, "error"), ("", 0, "pass")):
            with self.subTest(exit_code=exit_code, status=status), \
                    mock.patch.object(gate_collect, "viewer_script", stub_script(stdout, exit_code)):
                answer = self.collector([]).viewer_typecheck()
            self.assertEqual(status, answer["status"])
        self.assertEqual({"gate": "typescript-build", "path": "viewer/src/a.ts", "line": "3", "column": "7",
                          "severity": "error", "code": "TS2322", "message": "Type 'string' is not assignable."},
                         self.first_finding(output, 2, "viewer_typecheck"))

    def test_each_lint_message_is_a_finding_and_a_crash_is_a_gap(self):
        report = json.dumps([{"filePath": str(self.repository / "viewer/src/left.ts"),
                              "messages": [{"ruleId": "no-nested-ternary", "severity": 1, "line": 2}]}])
        for stdout, exit_code, status in ((report, 0, "fail"), ("[]", 1, "error"), ("[]", 2, "error"),
                                          ("[]", 0, "pass")):
            with self.subTest(exit_code=exit_code, stdout=stdout), \
                    mock.patch.object(gate_collect, "viewer_script", stub_script(stdout, exit_code)):
                self.assertEqual(status, self.collector([]).viewer_lint()["status"])
        finding = self.first_finding(report, 0, "viewer_lint")
        self.assertEqual(("eslint", "viewer/src/left.ts", "no-nested-ternary"),
                         (finding["gate"], finding["path"], finding["diagnostic"]["ruleId"]))

    def first_finding(self, stdout, exit_code, method):
        with mock.patch.object(gate_collect, "viewer_script", stub_script(stdout, exit_code)):
            return getattr(self.collector([]), method)()["findings"][0]


class ViewerKnipTests(unittest.TestCase):
    def test_the_viewer_model_judges_the_whole_package_its_plugins_load(self):
        model = workspace_model(REPOSITORY, ["viewer/src/a.test.ts", "viewer/src/main.tsx", "viewer/vite.config.ts"],
                                "typescript")

        self.assertEqual({".": {"entry": [], "project": []},
                          "../../viewer": {"entry": ["src/a.test.ts", "src/main.tsx"],
                                           "project": ["src/a.test.ts", "src/main.tsx", "vite.config.ts"]}},
                         model["config"]["workspaces"])
        self.assertEqual(["DynaDocs.Tests/coverage", "viewer"], model["packages"])

    def test_collector_measures_the_real_viewer_graph(self):
        from gate_inventory import assemble_inventory
        import gate_adapter
        paths = [path for path in git_file_state(REPOSITORY)[0] if path.startswith("viewer/")]
        inventory = assemble_inventory(REPOSITORY, paths, [], gate_adapter._ordinary_discovery(REPOSITORY, paths))
        runner = Collectors.__new__(Collectors)
        runner.root, runner.coverage = REPOSITORY, TOOLS
        with tempfile.TemporaryDirectory() as folder:
            runner.output = Path(folder)
            runner.log = CommandLog(REPOSITORY, runner.output / "commands")
            runner.inventory = inventory

            answer = collect_knip(runner, "typescript")

        judged = sorted(row["path"] for row in answer["facts"]["model"]["sources"])
        self.assertEqual([], answer["errors"])
        self.assertEqual(sorted([*(row["path"] for row in inventory["sources"]),
                                 *(row["path"] for row in inventory["excluded"])]), judged)
        self.assertEqual(len(judged), answer["facts"]["native"][1]["counters"]["processed"])


if __name__ == "__main__":
    unittest.main()

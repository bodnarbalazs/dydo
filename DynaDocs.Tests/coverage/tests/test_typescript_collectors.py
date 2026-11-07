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
STATECHART = "skills/engineering/statechart/scripts"
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

    def test_runtime_import_conditions_resolve_the_real_vitest_entry(self):
        shutil.copytree(REPOSITORY / "viewer/node_modules/vitest", self.repository / "node_modules/vitest")
        relative = "viewer/src/consumer.ts"
        (self.repository / relative).write_text("import { test } from 'vitest';\n", encoding="utf-8")

        answer = self.collector([relative]).typescript_dependencies()

        self.assertEqual("pass", answer["status"], answer["errors"])
        module = next(row for row in answer["facts"]["native"]["modules"] if row["source"] == relative)
        dependency, = module["dependencies"]
        self.assertEqual("vitest", dependency["module"])
        self.assertEqual("node_modules/vitest/dist/index.js", dependency["resolved"])
        self.assertFalse(dependency["couldNotResolve"])

    def graph_of(self, identities, sources=("viewer/src/a.ts", "viewer/src/b.ts")):
        """The module graph measured from a dependency-cruiser output listing these module identities."""
        modules = [{"source": name, "dependencies": []} for name in identities]
        runner = self.collector(list(sources))
        with mock.patch.object(runner, "command_json", return_value={"modules": modules}):
            return runner.typescript_dependencies()

    def test_a_repeated_third_party_module_does_not_make_the_graph_ambiguous(self):
        leaf = "viewer/node_modules/.pnpm/@xyflow+react@1/node_modules/@xyflow/react/dist/umd/index.js"

        answer = self.graph_of(["viewer/src/a.ts", leaf, "viewer/src/b.ts", leaf])

        self.assertEqual("pass", answer["status"])
        self.assertEqual([], answer["errors"])

    def test_a_repeated_or_missing_source_module_is_still_ambiguous(self):
        for identities in (["viewer/src/a.ts", "viewer/src/b.ts", "viewer/src/a.ts"], ["viewer/src/a.ts"]):
            with self.subTest(identities=identities), \
                    self.assertRaisesRegex(ValueError, "Missing or ambiguous dependency-cruiser source identity"):
                self.graph_of(identities)

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

    def statechart_typecheck(self, stdout, exit_code):
        """The statechart typecheck against a stand-in tsc printing `stdout`, beside one upstream file."""
        tsc = self.repository / "viewer/node_modules/typescript/bin/tsc"
        tsc.parent.mkdir(parents=True, exist_ok=True)
        tsc.write_text(f"process.stdout.write({json.dumps(stdout)});\nprocess.exit({exit_code});\n", encoding="utf-8")
        runner = self.collector([f"{STATECHART}/worker.ts"])
        runner.inventory["excluded"] = [{"path": f"{STATECHART}/pipeline.ts", "reason": "typescript-outside-viewer-src",
                                         "origin": {"upstream": {"path": "pstack/skills/statechart/scripts/pipeline.ts"}}}]
        return runner.statechart_typecheck()

    def test_every_statechart_compiler_diagnostic_is_a_finding_but_an_upstream_unused_binding(self):
        target = f"{STATECHART}/worker.ts(28,50): error TS2769: No overload matches this call.\n"
        upstream_type = f"{STATECHART}/pipeline.ts(9,3): error TS2322: Type 'string' is not assignable.\n"
        upstream_unused = f"{STATECHART}/pipeline.ts(7,8): error TS6133: 'Chart' is declared but never read.\n"
        target_unused = f"{STATECHART}/worker.ts(2,8): error TS6133: 'Buffer' is declared but never read.\n"
        cases = ((target + "  Overload 1 of 2 gave the following error.\n", 2, "fail", ["worker.ts"], 0),
                 (upstream_type, 2, "fail", ["pipeline.ts"], 0), (target_unused, 2, "fail", ["worker.ts"], 0),
                 (upstream_unused, 2, "pass", [], 1), ("", 0, "pass", [], 0), ("", 2, "error", [], 0),
                 (upstream_unused + "error TS5023: Unknown compiler option 'x'.\n", 1, "error", [], 1))
        for stdout, exit_code, status, paths, held in cases:
            with self.subTest(stdout=stdout, exit_code=exit_code):
                answer = self.statechart_typecheck(stdout, exit_code)

                self.assertEqual(status, answer["status"])
                self.assertEqual(paths, [Path(row["path"]).name for row in answer["findings"]])
                self.assertEqual(held, len(answer["facts"]["upstreamUnusedBindings"]))
        self.assertEqual({"gate": "typescript-build", "path": f"{STATECHART}/worker.ts", "line": "28", "column": "50",
                          "severity": "error", "code": "TS2769", "message": "No overload matches this call."},
                         self.statechart_typecheck(target, 2)["findings"][0])

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


class TypeScriptKnipTests(unittest.TestCase):
    def test_the_viewer_model_judges_the_whole_package_its_plugins_load(self):
        model = workspace_model(REPOSITORY, ["viewer/src/a.test.ts", "viewer/src/main.tsx", "viewer/vite.config.ts"],
                                "viewer")

        self.assertEqual({".": {"entry": [], "project": []},
                          "../../viewer": {"entry": ["src/a.test.ts", "src/main.tsx"],
                                           "project": ["src/a.test.ts", "src/main.tsx", "vite.config.ts"]}},
                         model["config"]["workspaces"])
        self.assertEqual(["DynaDocs.Tests/coverage", "viewer"], model["packages"])

    def test_the_statechart_model_roots_its_tests_and_the_modules_it_runs_by_path(self):
        model = workspace_model(REPOSITORY, [f"{STATECHART}/{name}" for name in (
            "checker.ts", "native-entry.ts", "statechart.ts", "test/checker.test.ts", "test/java.ts", "test/tlc.ts",
            "windows-cleanup.ts", "worker.ts")], "statechart")

        self.assertEqual({"entry": ["native-entry.ts", "statechart.ts", "test/checker.test.ts", "test/java.ts",
                                    "windows-cleanup.ts", "worker.ts"],
                          "project": ["checker.ts", "native-entry.ts", "statechart.ts", "test/checker.test.ts",
                                      "test/java.ts", "test/tlc.ts", "windows-cleanup.ts", "worker.ts"]},
                         model["config"]["workspaces"]["../../skills/engineering/statechart/scripts"])
        self.assertEqual(["DynaDocs.Tests/coverage", STATECHART], model["packages"])

    def measure_real(self, paths, graph="viewer", measure=None):
        """A real TypeScript collector, knip by default, over these repository paths, inventoried as the gate does."""
        from gate_inventory import assemble_inventory
        import gate_adapter
        inventory = assemble_inventory(REPOSITORY, paths, [], gate_adapter._ordinary_discovery(REPOSITORY, paths))
        runner = Collectors.__new__(Collectors)
        runner.root, runner.coverage, runner.stack = REPOSITORY, TOOLS, graph
        with tempfile.TemporaryDirectory() as folder:
            runner.output = Path(folder)
            runner.log = CommandLog(REPOSITORY, runner.output / "commands")
            runner.inventory = inventory
            return inventory, (measure or (lambda runner: collect_knip(runner, graph)))(runner)

    def test_the_real_statechart_package_typechecks_holding_only_upstream_unused_bindings(self):
        package = [path for path in git_file_state(REPOSITORY)[0]
                   if path.startswith(f"{STATECHART}/") and path.endswith(".ts")]
        inventory, answer = self.measure_real(package, "statechart", Collectors.statechart_typecheck)

        upstream = {row["path"] for row in inventory["excluded"]}
        self.assertEqual(([], []), (answer["findings"], answer["errors"]))
        self.assertEqual(set(), {row["path"] for row in answer["facts"]["upstreamUnusedBindings"]} - upstream)

    def test_collector_measures_the_real_viewer_graph(self):
        inventory, answer = self.measure_real(
            [path for path in git_file_state(REPOSITORY)[0] if path.startswith("viewer/")])

        judged = sorted(row["path"] for row in answer["facts"]["model"]["sources"])
        self.assertEqual([], answer["errors"])
        self.assertEqual(sorted([*(row["path"] for row in inventory["sources"]),
                                 *(row["path"] for row in inventory["excluded"])]), judged)
        self.assertEqual(len(judged), answer["facts"]["native"][1]["counters"]["processed"])

    def test_the_real_statechart_graph_judges_its_targets_and_holds_its_upstream_files(self):
        package = [path for path in git_file_state(REPOSITORY)[0]
                   if path.startswith(f"{STATECHART}/") and path.endswith((".ts", ".mjs"))]
        inventory, answer = self.measure_real(package, "statechart")

        model = answer["facts"]["model"]
        targets = sorted(row["path"] for row in inventory["sources"] if row["language"] == "typescript")
        upstream = sorted(row["path"] for row in inventory["excluded"])
        views = sorted(row["path"] for row in inventory["sources"] if row["language"] == "javascript")
        self.assertEqual([], answer["errors"])
        self.assertTrue(targets and upstream and views)
        self.assertEqual({"count": 0, "paths": []}, model["unjudged"])
        self.assertEqual(sorted([*upstream, *views]), model["ignored"]["paths"])
        self.assertEqual(sorted(package), sorted(row["path"] for row in model["sources"]))
        self.assertEqual(len(package), answer["facts"]["native"][1]["counters"]["processed"])

    def test_the_viewer_graph_leaves_the_statechart_package_to_its_own_stack(self):
        _, answer = self.measure_real(
            [path for path in git_file_state(REPOSITORY)[0] if path.endswith((".ts", ".tsx"))])

        self.assertEqual([], answer["errors"])
        self.assertEqual({"count": 0, "paths": []}, answer["facts"]["model"]["unjudged"])
        self.assertFalse(any(row["path"].startswith(f"{STATECHART}/") for row in answer["facts"]["model"]["sources"]))


if __name__ == "__main__":
    unittest.main()

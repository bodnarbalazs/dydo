"""The opt-in Release proof suite passes only on complete, passing Release evidence (DYD-840, DYD-859)."""
import copy
import io
import json
import re
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ElementTree
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import patch
from xml.sax.saxutils import quoteattr

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import release_proof
from release_proof import (MAP, ROOT, TRX_NAME, coverage_filter, expected_counts, facade_command, judge,
                           load_map, main, run, verify)
from test_coverage_artifacts import WORKFLOW, _header, _job, _named, _steps, _uses

MANUAL_WORKFLOW = WORKFLOW.with_name("guard-performance-proofs.yml")
RELEASE = "/tmp/dydo-test-1/DynaDocs.Tests/bin/Release/net10.0/DynaDocs.Tests.dll"
DEBUG = "/tmp/dydo-test-1/DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.dll"


def _split(name):
    return name.rsplit(".", 1)


def lane_rows(mapping, outcome="Passed"):
    """One result per case of every mapped test, as one complete lane run records them."""
    rows = []
    for name, cases in expected_counts(mapping).items():
        rows += [(name, f"(case: {case})" if cases > 1 else "", outcome) for case in range(cases)]
    return rows


def write_trx(path, rows, storage=RELEASE, counters=True, total=None, passed=None, orphans=0):
    definitions, results = [], []
    for index, (name, suffix, outcome) in enumerate(rows):
        class_name, method = _split(name)
        definitions.append(f'<UnitTest id="t{index}" storage={quoteattr(storage)}>'
                           f'<TestMethod className={quoteattr(class_name)} name={quoteattr(method + suffix)} />'
                           '</UnitTest>')
        results.append(f'<UnitTestResult testId="t{index}" outcome="{outcome}" />')
    definitions += [f'<UnitTest id="orphan{index}" storage={quoteattr(storage)}>'
                    '<TestMethod className="Orphan" name="Orphaned" /></UnitTest>' for index in range(orphans)]
    total = len(rows) if total is None else total
    passed = sum(outcome == "Passed" for _, _, outcome in rows) if passed is None else passed
    summary = f'<ResultSummary><Counters total="{total}" passed="{passed}" /></ResultSummary>'
    if not counters:
        summary = ""
    Path(path).write_text('<?xml version="1.0" encoding="utf-8"?>'
                          '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                          f'<TestDefinitions>{"".join(definitions)}</TestDefinitions>'
                          f'<Results>{"".join(results)}</Results>{summary}</TestRun>', encoding="utf-8")
    return path


class ReleaseProofMapTests(unittest.TestCase):
    def setUp(self):
        self.mapping = load_map()

    def test_the_repository_map_names_every_proof_once_with_its_witness(self):
        counts = expected_counts(self.mapping)
        self.assertEqual(1, counts[self.mapping["laneCheck"]])
        self.assertEqual(len(self.mapping["proofs"]) + 1, len(counts))
        self.assertTrue(all(row["witness"] for row in self.mapping["proofs"]))
        self.assertEqual("PerformanceProof!=Release", coverage_filter())
        self.assertEqual("PerformanceProof=Release|PerformanceProof=Shared", self.mapping["releaseFilter"])

    def test_a_malformed_map_is_refused(self):
        def changed(edit):
            data = copy.deepcopy(self.mapping)
            edit(data)
            return data

        proof = lambda data: data["proofs"][0]
        shared = next(index for index, row in enumerate(self.mapping["proofs"]) if row["lane"] == "Shared")
        broken = [
            changed(lambda data: data.update(schema=2)),
            changed(lambda data: data.update(extra=1)),
            changed(lambda data: data.update(trait="")),
            changed(lambda data: data.update(proofs=[])),
            changed(lambda data: proof(data).update(lane="Debug")),
            changed(lambda data: proof(data).update(cases=0)),
            changed(lambda data: proof(data).update(cases=True)),
            changed(lambda data: proof(data).update(assertions=[])),
            changed(lambda data: proof(data).update(assertions=[""])),
            changed(lambda data: proof(data).update(witness="")),
            changed(lambda data: proof(data).pop("witness")),
            changed(lambda data: data["proofs"][shared].update(witness="Other.Witness")),
            changed(lambda data: data["proofs"].append(copy.deepcopy(proof(data)))),
            changed(lambda data: data.update(laneCheck=proof(data)["proof"])),
        ]
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "map.json"
            for data in broken:
                with self.subTest(data=json.dumps(data)[:120]):
                    path.write_text(json.dumps(data), encoding="utf-8")
                    with self.assertRaises(ValueError):
                        load_map(path)
            path.write_text("{", encoding="utf-8")
            with self.assertRaises(ValueError):
                load_map(path)

    def test_the_lane_runs_the_release_filter_through_the_isolated_facade(self):
        results = Path("/results")
        command = facade_command(self.mapping, results)
        self.assertEqual([sys.executable, str(release_proof.TOOLS / "gap_check.py"), "test", "--stack", "dotnet", "--",
                          "-c", "Release", "-p:PerformanceProofs=true", "--filter", self.mapping["releaseFilter"],
                          "--logger", f"trx;LogFileName={TRX_NAME}", "--results-directory", str(results)], command)

    def test_every_run_but_the_opted_in_one_leaves_the_release_proofs_out(self):
        # VSTest joins the run settings' filter to any --filter, so a filtered run stays without them.
        project = ElementTree.parse(ROOT / "DynaDocs.Tests/DynaDocs.Tests.csproj").getroot()
        group = next(group for group in project.iter("PropertyGroup") if group.find("RunSettingsFilePath") is not None)
        self.assertEqual(("'$(PerformanceProofs)' != 'true'", "-p:PerformanceProofs=true"),
                         (group.get("Condition"), release_proof.OPT_IN))
        settings = group.findtext("RunSettingsFilePath").replace("$(MSBuildThisFileDirectory)", "DynaDocs.Tests/")
        self.assertEqual(coverage_filter(), ElementTree.parse(ROOT / settings).findtext("RunConfiguration/TestCaseFilter"))


class ReleaseProofEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.mapping = load_map()
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.trx = Path(self.folder.name) / TRX_NAME

    def problems(self, rows, **options):
        return verify(write_trx(self.trx, rows, **options), self.mapping)[0]

    def test_every_mapped_case_passing_in_release_is_complete_evidence(self):
        problems, counts = verify(write_trx(self.trx, lane_rows(self.mapping)), self.mapping)
        self.assertEqual([], problems)
        self.assertEqual(expected_counts(self.mapping), counts)

    def test_missing_or_unreadable_evidence_fails(self):
        self.assertIn("No readable Release proof evidence", verify(self.trx, self.mapping)[0][0])
        self.trx.write_text("<TestRun", encoding="utf-8")
        self.assertIn("No readable Release proof evidence", verify(self.trx, self.mapping)[0][0])
        self.trx.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                            '<TestDefinitions><UnitTest id="x" /></TestDefinitions></TestRun>', encoding="utf-8")
        self.assertIn("has no TestMethod", verify(self.trx, self.mapping)[0][0])

    def test_an_empty_run_fails(self):
        problems = self.problems([])
        self.assertIn("The lane produced zero results", problems)
        self.assertTrue(any("produced 0 results" in problem for problem in problems))

    def test_a_failed_or_skipped_proof_fails(self):
        for outcome in ("Failed", "NotExecuted"):
            rows = lane_rows(self.mapping)
            rows[3] = (rows[3][0], rows[3][1], outcome)
            with self.subTest(outcome=outcome):
                self.assertIn(f"{rows[3][0]} is {outcome}, not Passed", self.problems(rows))

    def test_a_missing_case_or_proof_fails(self):
        rows = lane_rows(self.mapping)
        first = rows[0][0]
        partial = [row for row in rows if row[0] != first] + [row for row in rows if row[0] == first][1:]
        cases = expected_counts(self.mapping)[first]
        self.assertIn(f"{first} produced {cases - 1} results, not its {cases} cases", self.problems(partial))
        lane_check = self.mapping["laneCheck"]
        self.assertIn(f"{lane_check} produced 0 results, not its 1 cases",
                      self.problems([row for row in rows if row[0] != lane_check]))

    def test_an_unmapped_test_in_the_lane_fails(self):
        rows = lane_rows(self.mapping) + [("DynaDocs.Tests.Services.Elsewhere.Measured", "", "Passed")]
        self.assertIn("DynaDocs.Tests.Services.Elsewhere.Measured ran in the lane but is not a mapped proof",
                      self.problems(rows))

    def test_a_debug_or_instrumented_build_is_not_release_evidence(self):
        problems = self.problems(lane_rows(self.mapping), storage=DEBUG)
        self.assertTrue(problems)
        self.assertTrue(all("not the Release build" in problem for problem in problems))
        self.assertEqual([], self.problems(lane_rows(self.mapping), storage=RELEASE.replace("/", "\\")))
        # A real lane's TRX records the storage path lowercased.
        self.assertEqual([], self.problems(lane_rows(self.mapping), storage=RELEASE.lower()))
        self.assertTrue(self.problems(lane_rows(self.mapping), storage=DEBUG.lower()))

    def test_counters_must_exist_and_agree_with_the_results(self):
        rows = lane_rows(self.mapping)
        self.assertIn("The TRX has no ResultSummary/Counters", self.problems(rows, counters=False))
        self.assertIn(f"TRX counters show total {len(rows)} but passed 1", self.problems(rows, passed=1))
        self.assertIn(f"TRX counters show total 1 but the TRX has {len(rows)} results",
                      self.problems(rows, total=1, passed=1))
        self.assertIn(f"The TRX defines {len(rows) + 1} tests but has {len(rows)} results",
                      self.problems(rows, orphans=1))

    def test_a_result_without_a_definition_fails(self):
        write_trx(self.trx, lane_rows(self.mapping))
        text = self.trx.read_text(encoding="utf-8").replace('testId="t0"', 'testId="unknown"')
        self.trx.write_text(text, encoding="utf-8")
        problems = verify(self.trx, self.mapping)[0]
        self.assertTrue(any("an undefined test unknown ran in the lane" in problem for problem in problems))


class ReleaseProofIdentityTests(unittest.TestCase):
    def setUp(self):
        self.mapping = load_map()
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.trx = write_trx(Path(self.folder.name) / TRX_NAME, lane_rows(self.mapping))
        self.tree = ElementTree.parse(self.trx)
        self.definitions = self.tree.find("t:TestDefinitions", release_proof.TRX)
        self.results = self.tree.find("t:Results", release_proof.TRX)

    def problems(self):
        self.tree.write(self.trx, encoding="utf-8", xml_declaration=True)
        return verify(self.trx, self.mapping)[0]

    def test_a_duplicate_sibling_cannot_replace_a_missing_case(self):
        # The first mapped theory has multiple cases; totals and method counts stay intact.
        self.assertEqual(lane_rows(self.mapping)[0][0], lane_rows(self.mapping)[1][0])
        self.results.remove(self.results[1])
        self.results.insert(1, copy.deepcopy(self.results[0]))
        problems = self.problems()
        self.assertIn("TRX test t0 has 2 results, not exactly one", problems)
        self.assertIn("TRX test t1 has no result", problems)
        self.assertEqual(expected_counts(self.mapping), verify(self.trx, self.mapping)[1])

    def test_duplicate_definition_ids_are_refused_before_overwriting(self):
        duplicate = copy.deepcopy(self.definitions[0])
        self.definitions.append(duplicate)
        for storage in (RELEASE, DEBUG):
            with self.subTest(storage=storage):
                duplicate.set("storage", storage)
                self.assertTrue(any("Duplicate TRX definition id t0" in problem for problem in self.problems()))

    def test_an_extra_result_for_the_same_case_is_refused(self):
        self.results.append(copy.deepcopy(self.results[0]))
        self.assertIn("TRX test t0 has 2 results, not exactly one", self.problems())

    def test_a_defined_case_must_have_a_result(self):
        self.results.remove(self.results[0])
        self.assertIn("TRX test t0 has no result", self.problems())

    def test_a_result_must_name_a_defined_case(self):
        self.results[0].set("testId", "unknown")
        problems = self.problems()
        self.assertIn("TRX result names undefined test unknown", problems)
        self.assertIn("TRX test t0 has no result", problems)

    def test_absent_or_empty_ids_cannot_form_a_matching_pair(self):
        for identity in (None, ""):
            with self.subTest(identity=identity):
                self.definitions[0].attrib.pop("id", None)
                self.results[0].attrib.pop("testId", None)
                if identity is not None:
                    self.definitions[0].set("id", identity)
                    self.results[0].set("testId", identity)
                self.assertTrue(self.problems())

    def test_a_result_without_an_id_is_refused(self):
        for identity in (None, ""):
            with self.subTest(identity=identity):
                self.results[0].attrib.pop("testId", None)
                if identity is not None:
                    self.results[0].set("testId", identity)
                self.assertIn("A TRX result has no testId", self.problems())

    def test_identity_matching_does_not_depend_on_result_order(self):
        self.results[:] = reversed(self.results[:])
        self.assertEqual([], self.problems())


class ReleaseProofJudgementTests(unittest.TestCase):
    def setUp(self):
        self.mapping = load_map()
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)

    def judged(self, *args, **kwargs):
        with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()) as errors:
            return judge(*args, **kwargs), errors.getvalue()

    def test_the_receipt_records_a_pass_only_for_complete_evidence(self):
        trx = write_trx(self.root / TRX_NAME, lane_rows(self.mapping))
        receipt = self.root / "out" / "receipt.json"
        status, errors = self.judged(trx, self.mapping, receipt, ["argv"])
        payload = json.loads(receipt.read_text(encoding="utf-8"))
        self.assertEqual((0, ""), (status, errors))
        self.assertEqual(("pass", [], ["argv"]), (payload["verdict"], payload["problems"], payload["command"]))
        self.assertEqual(expected_counts(self.mapping), payload["counts"])
        self.assertEqual(64, len(payload["trxSha256"]))

    def test_a_failed_run_fails_even_with_passing_evidence(self):
        trx = write_trx(self.root / TRX_NAME, lane_rows(self.mapping))
        status, errors = self.judged(trx, self.mapping, self.root / "receipt.json", status=1)
        self.assertEqual(1, status)
        self.assertIn("The isolated Release run exited 1", errors)
        self.assertEqual("fail", json.loads((self.root / "receipt.json").read_text(encoding="utf-8"))["verdict"])

    def test_missing_evidence_fails_with_a_receipt(self):
        status, errors = self.judged(self.root / TRX_NAME, self.mapping, self.root / "receipt.json")
        payload = json.loads((self.root / "receipt.json").read_text(encoding="utf-8"))
        self.assertEqual((1, "fail", None), (status, payload["verdict"], payload["trxSha256"]))
        self.assertIn("No readable Release proof evidence", errors)

    def test_the_candidate_is_unknown_outside_a_repository(self):
        with patch("release_proof.subprocess.run", return_value=subprocess.CompletedProcess([], 128, "", "")):
            self.assertIsNone(release_proof._candidate())


class ReleaseProofRunTests(unittest.TestCase):
    def setUp(self):
        self.mapping = load_map()
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.results = Path(self.folder.name) / "release-proof"

    def run_lane(self, facade):
        with patch("release_proof.subprocess.run", side_effect=facade) as native, \
                patch("release_proof._candidate", return_value="c" * 40), \
                redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            status = run(self.results)
        return status, native.call_args.args[0], json.loads((self.results / "receipt.json").read_text(encoding="utf-8"))

    def test_a_complete_passing_release_run_passes(self):
        def facade(command, cwd):
            write_trx(command[command.index("--results-directory") + 1] + "/" + TRX_NAME, lane_rows(self.mapping))
            return subprocess.CompletedProcess(command, 0)

        status, command, receipt = self.run_lane(facade)
        self.assertEqual(0, status)
        self.assertEqual(facade_command(self.mapping, self.results.resolve()), command)
        self.assertEqual(("pass", "c" * 40), (receipt["verdict"], receipt["candidate"]))

    def test_an_earlier_runs_evidence_never_passes_this_run(self):
        self.results.mkdir(parents=True)
        write_trx(self.results / TRX_NAME, lane_rows(self.mapping))
        status, _, receipt = self.run_lane(lambda command, cwd: subprocess.CompletedProcess(command, 0))
        self.assertEqual((1, "fail"), (status, receipt["verdict"]))
        self.assertIn("No readable Release proof evidence", receipt["problems"][0])

    def test_a_failing_release_run_fails(self):
        def facade(command, cwd):
            rows = lane_rows(self.mapping)
            rows[0] = (rows[0][0], rows[0][1], "Failed")
            write_trx(self.results / TRX_NAME, rows)
            return subprocess.CompletedProcess(command, 1)

        status, _, receipt = self.run_lane(facade)
        self.assertEqual((1, "fail"), (status, receipt["verdict"]))
        self.assertEqual("The isolated Release run exited 1", receipt["problems"][0])


class ReleaseProofWorkflowTests(unittest.TestCase):
    """Only a manual dispatch runs the suite in CI; no pull request, push or required check needs it (DYD-859)."""

    def test_no_ci_job_runs_or_requires_the_suite(self):
        workflow = WORKFLOW.read_text(encoding="utf-8")
        for name in ("release_proof.py", "PerformanceProofs=true", "dotnet-release-proof", "PerformanceProof=Release"):
            self.assertNotIn(name, workflow)
        self.assertEqual("dotnet-coverage", _header(_job(workflow, "dotnet-coverage-union"))["needs"])

    def test_a_manual_dispatch_alone_runs_the_suite_and_always_keeps_its_evidence(self):
        workflow = MANUAL_WORKFLOW.read_text(encoding="utf-8")
        self.assertEqual("  workflow_dispatch:\n", re.search(r"^on:\n((?:  .*\n)+)", workflow, re.M)[1])
        self.assertEqual(["guard-performance-proofs"], re.findall(r"^  ([\w-]+):", workflow.split("\njobs:\n", 1)[1], re.M))
        job = _job(workflow, "guard-performance-proofs")
        header, steps = _header(job), _steps(job)
        self.assertEqual(("ubuntu-24.04", "30", {"contents": "read"}),
                         (header["runs-on"], header["timeout-minutes"], header["permissions"]))
        self.assertNotIn("continue-on-error", job)
        suite = steps[_named(steps, "Release performance proofs")]
        self.assertEqual(".static-gates/python/bin/python DynaDocs.Tests/coverage/release_proof.py run", suite["run"])
        self.assertNotIn("if", suite)
        upload = steps[_named(steps, "Upload Release proof evidence")]
        self.assertEqual(_uses(steps, "actions/upload-artifact@"), [_named(steps, "Upload Release proof evidence")])
        self.assertEqual(("always()", "guard-performance-proofs-attempt-${{ github.run_attempt }}",
                          "DynaDocs.Tests/coverage/results", "error"),
                         (upload["if"], upload["with"]["name"], upload["with"]["path"],
                          upload["with"]["if-no-files-found"]))


class ReleaseProofCommandLineTests(unittest.TestCase):
    def test_verify_judges_a_given_trx(self):
        with tempfile.TemporaryDirectory() as folder:
            trx = write_trx(Path(folder) / TRX_NAME, lane_rows(load_map()))
            receipt = Path(folder) / "receipt.json"
            with redirect_stdout(io.StringIO()):
                self.assertEqual(0, main(["verify", "--trx", str(trx), "--receipt", str(receipt)]))
            write_trx(trx, lane_rows(load_map(), outcome="Failed"))
            with redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                self.assertEqual(1, main(["verify", "--trx", str(trx), "--receipt", str(receipt)]))

    def test_verify_refuses_to_stamp_an_earlier_candidates_trx_as_this_candidates_pass(self):
        # DYD-431 F-32: a passing TRX run on candidate A must not become a pass receipt for candidate B.
        # testing-strategy.md: "Reuse evidence only for the exact candidate".
        earlier, current = "a" * 40, "b" * 40
        with tempfile.TemporaryDirectory() as folder:
            with patch("release_proof._candidate", return_value=earlier):
                trx = write_trx(Path(folder) / TRX_NAME, lane_rows(load_map()))
            receipt = Path(folder) / "receipt.json"
            with patch("release_proof._candidate", return_value=current), \
                    redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                status = main(["verify", "--trx", str(trx), "--receipt", str(receipt)])
            written = json.loads(receipt.read_text(encoding="utf-8"))
            self.assertEqual((1, "fail"), (status, written["verdict"]),
                             f"candidate {written['candidate']} was stamped {written['verdict']} "
                             f"from a TRX carrying no binding to the candidate it ran on")

    def test_run_uses_the_given_results_directory(self):
        with patch("release_proof.run", return_value=0) as lane:
            self.assertEqual(0, main(["run", "--results", "/somewhere"]))
        lane.assert_called_once_with("/somewhere")

    def test_an_unusable_map_is_invalid_work(self):
        with patch("release_proof.load_map", side_effect=ValueError("broken")), redirect_stderr(io.StringIO()) as errors:
            self.assertEqual(2, main(["verify", "--trx", "x.trx", "--receipt", "r.json"]))
        self.assertIn("release proof: broken", errors.getvalue())

    def test_the_map_is_next_to_the_lane(self):
        self.assertEqual(Path(release_proof.__file__).with_name("release-proofs.json"), MAP)


if __name__ == "__main__":
    unittest.main()

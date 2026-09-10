"""Normalization examples over the real captured mutation reports and Cosmic Ray sessions.

Every input here is a fixture an engine actually wrote (`fixtures/mutation/origin.json` records
each one's subject, engine version, command and, for a Cosmic Ray session, the capture nonce its
generated suite runner embedded). Nothing launches an engine.
"""
import json
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mutation_summary

FIXTURES = Path(__file__).resolve().parent / "fixtures/mutation"
ORIGIN = json.loads((FIXTURES / "origin.json").read_text(encoding="utf-8"))["fixtures"]
FOREIGN_NONCE = "00000000000000000000000000000000"

EMPTY_COUNTS = {"generated": 0, "valid": 0, "killed": 0, "survived": 0, "noCoverage": 0,
                "timeout": 0, "compileError": 0, "ignored": 0, "runtimeError": 0, "unrun": 0,
                "unknown": 0}


def counts(**values):
    """The eleven count fields, all zero but the ones this example names."""
    unknown = set(values) - set(EMPTY_COUNTS)
    if unknown:
        raise AssertionError(f"not a count field: {sorted(unknown)}")
    return {**EMPTY_COUNTS, **values}


def outcome(count_values, findings=(), gaps=(), score=None, witness=(), exit_code=0):
    return {"counts": counts(**count_values), "findings": list(findings), "gaps": list(gaps),
            "score": score, "witness": list(witness), "measurementComplete": not gaps,
            "exitCode": exit_code}


def span(start_line, start_column, end_line, end_column):
    return {"startLine": start_line, "startColumn": start_column,
            "endLine": end_line, "endColumn": end_column}


def finding(path, mutator, status, extent, report, identity):
    return {"gate": "mutation", "path": path, "span": extent, "mutator": mutator,
            "status": status, "raw": {"report": report, "id": identity}}


def stryker_snapshot_root(fixture):
    """The snapshot root a campaign that produced this report would have had.

    Stryker.NET reports the mutated project's directory as `projectRoot` and writes absolute file
    keys under it; StrykerJS reports the snapshot itself and writes keys relative to it.
    """
    recorded = Path(json.loads((FIXTURES / fixture).read_text(encoding="utf-8"))["projectRoot"])
    return recorded.parent if fixture.startswith("stryker-net-") else recorded


def cosmic_nonce(fixture):
    """The capture nonce the recorded provenance says this session's runner embedded."""
    return ORIGIN[fixture]["markerNonce"]


class StrykerNormalizationTests(unittest.TestCase):
    """One example per native Stryker status, over the report the engine wrote."""

    def read(self, fixture, selected, engine, inventory=None, root=None, substantive=True):
        reading = mutation_summary.read_stryker_report(FIXTURES / fixture)
        mapped = mutation_summary.map_report_paths(
            reading, root if root is not None else stryker_snapshot_root(fixture),
            list(inventory if inventory is not None else selected))
        return mutation_summary.normalize(mapped, list(selected), engine, substantive)

    def test_a_killed_dotnet_mutant_is_the_only_status_that_passes(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "killed": 1}, score=100.0),
            self.read("stryker-net-killed.json", ["src/Number.cs"], "stryker-net"))

    def test_a_survived_dotnet_mutant_is_one_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "survived": 1}, score=0.0, exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "survived",
                                      span(1, 68, 1, 77), "stryker-net-survived.json", "0")]),
            self.read("stryker-net-survived.json", ["src/Number.cs"], "stryker-net"))

    def test_an_uncovered_dotnet_mutant_is_a_finding_beside_its_killed_peer(self):
        self.assertEqual(
            outcome({"generated": 2, "valid": 2, "killed": 1, "noCoverage": 1}, score=50.0,
                    exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "noCoverage",
                                      span(5, 44, 5, 53), "stryker-net-no-coverage.json", "1")]),
            self.read("stryker-net-no-coverage.json", ["src/Number.cs"], "stryker-net"))

    def test_a_timed_out_dotnet_mutant_is_a_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "timeout": 1}, score=0.0, exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "timeout",
                                      span(1, 68, 1, 77), "stryker-net-timeout.json", "0")]),
            self.read("stryker-net-timeout.json", ["src/Number.cs"], "stryker-net"))

    def test_a_dotnet_runtime_error_is_a_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "runtimeError": 1}, score=0.0, exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "runtimeError",
                                      span(1, 68, 1, 77), "stryker-net-runtime-error.json", "0")]),
            self.read("stryker-net-runtime-error.json", ["src/Number.cs"], "stryker-net"))

    def test_a_mutant_ignored_inside_a_selected_file_is_a_finding(self):
        self.assertEqual(
            outcome({"generated": 2, "valid": 2, "killed": 1, "ignored": 1}, score=50.0,
                    exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "ignored",
                                      span(6, 16, 6, 25), "stryker-net-ignored.json", "1")]),
            self.read("stryker-net-ignored.json", ["src/Number.cs"], "stryker-net"))

    def test_a_pending_dotnet_mutant_is_an_unrun_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "unrun": 1}, score=0.0, exit_code=1,
                    findings=[finding("src/Number.cs", "Arithmetic mutation", "unrun",
                                      span(1, 68, 1, 77), "stryker-net-pending.json", "0")]),
            self.read("stryker-net-pending.json", ["src/Number.cs"], "stryker-net"))

    def test_a_campaign_whose_every_mutant_failed_to_compile_measured_nothing(self):
        self.assertEqual(
            outcome({"generated": 3, "compileError": 3},
                    gaps=[{"reason": "all mutants invalid"}], exit_code=2),
            self.read("stryker-net-compile-error.json", ["src/Number.cs"], "stryker-net"))

    def test_a_substantive_campaign_with_no_generated_mutant_is_invalid(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "zero-mutant campaign"}], exit_code=2),
            self.read("stryker-net-zero-mutants.json", ["src/Number.cs"], "stryker-net"))

    def test_a_non_substantive_campaign_with_no_generated_mutant_passes(self):
        self.assertEqual(
            outcome({}),
            self.read("stryker-net-zero-mutants.json", ["src/Number.cs"], "stryker-net",
                      substantive=False))

    def test_mutants_the_mutate_filter_removed_are_witnessed_and_not_counted(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "killed": 1}, score=100.0,
                    witness=[{"path": "src/Other.cs", "reason": "removed by mutate filter"}]),
            self.read("stryker-net-mutate-filter-ignored.json", ["src/Number.cs"], "stryker-net",
                      inventory=["src/Number.cs", "src/Other.cs"]))

    def test_a_non_ignored_mutant_in_an_unselected_dotnet_file_is_invalid(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "foreign mutant: src/Other.cs",
                               "path": "src/Other.cs"}], exit_code=2),
            self.read("stryker-net-foreign-mutant.json", ["src/Number.cs"], "stryker-net",
                      inventory=["src/Number.cs", "src/Other.cs"]))

    def test_a_killed_javascript_mutant_passes(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "killed": 1}, score=100.0),
            self.read("stryker-js-killed.json", ["value.cjs"], "stryker-js"))

    def test_a_survived_javascript_mutant_is_one_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "survived": 1}, score=0.0, exit_code=1,
                    findings=[finding("value.cjs", "ArrowFunction", "survived",
                                      span(1, 17, 1, 27), "stryker-js-survived.json", "0")]),
            self.read("stryker-js-survived.json", ["value.cjs"], "stryker-js"))

    def test_a_timed_out_javascript_mutant_is_a_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "timeout": 1}, score=0.0, exit_code=1,
                    findings=[finding("value.cjs", "ArrowFunction", "timeout",
                                      span(1, 17, 1, 27), "stryker-js-timeout.json", "0")]),
            self.read("stryker-js-timeout.json", ["value.cjs"], "stryker-js"))

    def test_a_javascript_runtime_error_is_a_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "runtimeError": 1}, score=0.0, exit_code=1,
                    findings=[finding("value.cjs", "ArrowFunction", "runtimeError",
                                      span(1, 17, 1, 27), "stryker-js-runtime-error.json", "0")]),
            self.read("stryker-js-runtime-error.json", ["value.cjs"], "stryker-js"))

    def test_two_selected_javascript_files_are_both_counted(self):
        self.assertEqual(
            outcome({"generated": 2, "valid": 2, "killed": 2}, score=100.0),
            self.read("stryker-js-two-files.json", ["alpha.cjs", "beta.cjs"], "stryker-js"))

    def test_any_unselected_javascript_file_in_the_report_is_invalid(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "foreign file: beta.cjs", "path": "beta.cjs"}],
                    exit_code=2),
            self.read("stryker-js-two-files.json", ["alpha.cjs"], "stryker-js",
                      inventory=["alpha.cjs", "beta.cjs"]))

    def test_a_selected_file_missing_from_a_nonempty_report_is_a_partial_report(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "partial report", "path": "beta.cjs"}], exit_code=2),
            self.read("stryker-js-two-files.json", ["alpha.cjs", "beta.cjs"], "stryker-js",
                      inventory=["alpha.cjs"]))

    def test_a_zero_mutant_javascript_report_is_zero_mutant_before_partial_report(self):
        """StrykerJS omits the file entirely, so the order of the two rules decides the verdict."""
        self.assertEqual(
            outcome({}, gaps=[{"reason": "zero-mutant campaign"}], exit_code=2),
            self.read("stryker-js-zero-mutants.json", ["value.cjs"], "stryker-js"))


class ReportPathNormalizationTests(unittest.TestCase):
    """No engine reports a repository-relative path, and only inventory spellings are published."""

    def mapped(self, fixture, root, inventory):
        reading = mutation_summary.read_stryker_report(FIXTURES / fixture)
        return mutation_summary.map_report_paths(reading, root, list(inventory))

    def test_absolute_dotnet_keys_become_the_inventory_spelling(self):
        mapped = self.mapped("stryker-net-killed.json",
                             stryker_snapshot_root("stryker-net-killed.json"), ["src/Number.cs"])
        self.assertEqual((["src/Number.cs"], ["src/Number.cs"], []),
                         (mapped["files"], [row["path"] for row in mapped["rows"]], mapped["gaps"]))

    def test_relative_javascript_keys_join_onto_their_own_project_root(self):
        mapped = self.mapped("stryker-js-two-files.json",
                             stryker_snapshot_root("stryker-js-two-files.json"),
                             ["alpha.cjs", "beta.cjs"])
        self.assertEqual((["alpha.cjs", "beta.cjs"], ["alpha.cjs", "beta.cjs"], []),
                         (mapped["files"], [row["path"] for row in mapped["rows"]], mapped["gaps"]))

    def test_a_key_outside_the_snapshot_root_is_unmappable(self):
        fixture = "stryker-net-killed.json"
        key = next(iter(json.loads((FIXTURES / fixture).read_text(encoding="utf-8"))["files"]))
        mapped = self.mapped(fixture, Path(tempfile.gettempdir()) / "dydo-elsewhere",
                             ["src/Number.cs"])
        self.assertEqual([{"reason": f"unmappable report path: {key}"}], mapped["gaps"])

    def test_a_remainder_no_inventory_row_spells_is_unmappable(self):
        fixture = "stryker-net-killed.json"
        key = next(iter(json.loads((FIXTURES / fixture).read_text(encoding="utf-8"))["files"]))
        mapped = self.mapped(fixture, stryker_snapshot_root(fixture), ["src/Other.cs"])
        self.assertEqual([{"reason": f"unmappable report path: {key}"}], mapped["gaps"])

    def test_a_match_that_only_holds_after_case_folding_is_unmappable(self):
        fixture = "stryker-net-killed.json"
        key = next(iter(json.loads((FIXTURES / fixture).read_text(encoding="utf-8"))["files"]))
        mapped = self.mapped(fixture, stryker_snapshot_root(fixture), ["src/number.cs"])
        self.assertEqual([{"reason": f"unmappable report path: {key}"}], mapped["gaps"])


class MalformedReportTests(unittest.TestCase):
    """A report that cannot be believed is invalid before any status is counted."""

    def written(self, text):
        directory = tempfile.TemporaryDirectory(prefix="dydo-mutation-report-")
        self.addCleanup(directory.cleanup)
        path = Path(directory.name) / "report.json"
        path.write_text(text, encoding="utf-8")
        return path

    def test_a_report_that_does_not_parse_is_malformed(self):
        reading = mutation_summary.read_stryker_report(self.written("{not json"))
        self.assertEqual([{"reason": "malformed report", "path": "report.json"}], reading["gaps"])

    def test_a_mutant_without_a_location_is_malformed(self):
        report = {"schemaVersion": "1.0", "projectRoot": "C:/x", "thresholds": {},
                  "files": {"a.cs": {"language": "cs", "source": "", "mutants": [
                      {"id": "0", "mutatorName": "Arithmetic mutation", "status": "Killed"}]}}}
        reading = mutation_summary.read_stryker_report(self.written(json.dumps(report)))
        self.assertEqual([{"reason": "malformed report", "path": "report.json"}], reading["gaps"])

    def test_a_mutant_without_a_status_is_malformed(self):
        report = {"schemaVersion": "1.0", "projectRoot": "C:/x", "thresholds": {},
                  "files": {"a.cs": {"language": "cs", "source": "", "mutants": [
                      {"id": "0", "mutatorName": "Arithmetic mutation",
                       "location": {"start": {"line": 1, "column": 1},
                                    "end": {"line": 1, "column": 2}}}]}}}
        reading = mutation_summary.read_stryker_report(self.written(json.dumps(report)))
        self.assertEqual([{"reason": "malformed report", "path": "report.json"}], reading["gaps"])


class CosmicRayNormalizationTests(unittest.TestCase):
    """Cosmic Ray calls every nonzero test-process exit `killed`; the marker says which are kills."""

    def read(self, fixture, selected=("mod.py",), nonce=None, substantive=True):
        session = FIXTURES / fixture
        reading = mutation_summary.read_cosmic_session(
            session, nonce if nonce is not None else cosmic_nonce(fixture))
        mapped = mutation_summary.map_report_paths(
            reading, Path(__file__).resolve().parents[3], list(selected))
        return mutation_summary.normalize(mapped, list(selected), "cosmic-ray", substantive)

    def test_a_killed_row_carrying_this_campaigns_marker_and_a_nonzero_code_is_a_kill(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "killed": 1}, score=100.0),
            self.read("cosmic-ray-killed.sqlite"))

    def test_the_same_session_read_under_another_campaigns_nonce_did_not_complete(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "runtimeError": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "runtimeError",
                                      span(2, 11, 2, 15), "cosmic-ray-killed.sqlite",
                                      "5f5175be0b3a4d8e9e83e5ce475c1be8")]),
            self.read("cosmic-ray-killed.sqlite", nonce=FOREIGN_NONCE))

    def test_a_killed_row_whose_output_is_exactly_timeout_is_a_timeout_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "timeout": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "timeout",
                                      span(2, 11, 2, 15), "cosmic-ray-timeout.sqlite",
                                      "57f98bfd1f834ea69cfefb818115a2ea")]),
            self.read("cosmic-ray-timeout.sqlite"))

    def test_a_killed_row_whose_marker_reads_exit_zero_is_a_runtime_error_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "runtimeError": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "runtimeError",
                                      span(2, 11, 2, 15),
                                      "cosmic-ray-killed-marker-exit-zero.sqlite",
                                      "3bf38cd4075547ed9f493a3d4d4056e2")]),
            self.read("cosmic-ray-killed-marker-exit-zero.sqlite"))

    def test_a_killed_row_with_no_marker_at_all_is_a_runtime_error_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "runtimeError": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "runtimeError",
                                      span(2, 11, 2, 15),
                                      "cosmic-ray-killed-without-marker.sqlite",
                                      "8851b93705a64c09a06f18e1ece511cb")]),
            self.read("cosmic-ray-killed-without-marker.sqlite"))

    def test_a_survived_row_is_a_finding_without_needing_any_marker(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "survived": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "survived",
                                      span(2, 11, 2, 15), "cosmic-ray-survived.sqlite",
                                      "95478543e1c842739ecae4921cc9de1d")]),
            self.read("cosmic-ray-survived.sqlite"))

    def test_an_incompetent_row_is_a_run_that_did_not_happen(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "engine could not run mutant", "path": "mod.py"}],
                    exit_code=2),
            self.read("cosmic-ray-incompetent.sqlite"))

    def test_a_worker_outcome_of_exception_is_a_run_that_did_not_happen(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "engine could not run mutant", "path": "mod.py"}],
                    exit_code=2),
            self.read("cosmic-ray-exception.sqlite"))

    def test_a_worker_outcome_of_abnormal_is_a_run_that_did_not_happen(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "engine could not run mutant", "path": "mod.py"}],
                    exit_code=2),
            self.read("cosmic-ray-abnormal.sqlite"))

    def test_a_no_test_row_is_an_unrun_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "unrun": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "unrun",
                                      span(2, 11, 2, 15), "cosmic-ray-no-test.sqlite",
                                      "76c82b70d3bc48579900dd7f51d764ea")]),
            self.read("cosmic-ray-no-test.sqlite"))

    def test_a_skipped_row_is_an_unrun_finding(self):
        self.assertEqual(
            outcome({"generated": 1, "valid": 1, "unrun": 1}, score=0.0, exit_code=1,
                    findings=[finding("mod.py", "core/ReplaceTrueWithFalse", "unrun",
                                      span(2, 11, 2, 15), "cosmic-ray-skipped.sqlite",
                                      "df96183d07ca444a84416ba56ab35acb")]),
            self.read("cosmic-ray-skipped.sqlite"))

    def test_a_work_item_without_a_result_is_a_partial_report(self):
        self.assertEqual(
            outcome({}, gaps=[{"reason": "partial report",
                               "path": "cosmic-ray-partial-session.sqlite"}], exit_code=2),
            self.read("cosmic-ray-partial-session.sqlite"))


class CosmicRaySessionFixtureTests(unittest.TestCase):
    """Guards on the captured evidence every expectation above is read against."""

    def rows(self, fixture):
        connection = sqlite3.connect(f"file:{(FIXTURES / fixture).as_posix()}?mode=ro", uri=True)
        try:
            items = connection.execute("select count(*) from work_items").fetchone()[0]
            results = connection.execute(
                "select worker_outcome, test_outcome, output from work_results").fetchall()
        finally:
            connection.close()
        return items, results

    def test_every_captured_session_records_the_nonce_its_runner_embedded(self):
        self.assertEqual(
            sorted(path.name for path in FIXTURES.glob("cosmic-ray-*.sqlite")),
            sorted(name for name, meta in ORIGIN.items() if meta.get("markerNonce")))

    def test_the_killed_session_carries_its_own_marker_with_a_nonzero_code(self):
        items, results = self.rows("cosmic-ray-killed.sqlite")
        worker, test, output = results[0]
        last = [line for line in output.splitlines() if line.strip()][-1].rstrip()
        nonce = cosmic_nonce("cosmic-ray-killed.sqlite")
        self.assertEqual(
            (1, 1, "NORMAL", "KILLED", f"##DYDO-SUITE-COMPLETE {nonce} exit=1##"),
            (items, len(results), worker, test, last))


if __name__ == "__main__":
    unittest.main()

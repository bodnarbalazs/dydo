"""Mutation acceptance follows evidence, never an engine's aggregate score."""
import copy
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mutation_results as results


def member(identity="outer", start=(1, 0), end=(3, 1)):
    return {"id": identity, "path": "subject.py", "start_line": start[0],
            "start_column": start[1], "end_line": end[0], "end_column": end[1]}


def mutant(state="killed", start=(2, 4), end=(2, 5), replacement="2"):
    return {"path": "subject.py", "span": (*start, *end), "mutator": "NumberReplacer",
            "replacement": replacement, "state": state, "native_id": "one"}


class MutationPolicyTests(unittest.TestCase):
    def test_every_complete_kill_passes_but_native_score_is_irrelevant(self):
        report = results.evaluate([member()], [mutant()])
        self.assertEqual(0, report["exit_code"])
        self.assertEqual(100, report["members"][0]["score"])

    def test_every_state_has_an_explicit_acceptance_outcome(self):
        expected = {"surviving": 1, "uncovered": 1, "invalid": 1, "timeout": 2,
                    "error": 2, "unrun": 2, "ignored": 2, "unknown": 2}
        for state, code in expected.items():
            with self.subTest(state=state):
                self.assertEqual(code, results.evaluate([member()], [mutant(state)])["exit_code"])

    def test_empty_or_all_invalid_is_not_one_hundred_percent(self):
        for rows in ([], [mutant("invalid")]):
            report = results.evaluate([member()], rows)
            self.assertEqual(1, report["exit_code"])
            self.assertIsNone(report["members"][0]["score"])

    def test_nested_kill_cannot_rescue_outer_obligation(self):
        report = results.evaluate([member(), member("inner", (2, 0), (2, 9))], [mutant()])
        self.assertEqual(1, report["exit_code"])
        self.assertEqual([0, 1], [m["generated"] for m in report["members"]])

    def test_equal_and_crossing_ownership_are_incomplete(self):
        variants = [[member(), member("same")],
                    [member(end=(2, 8)), member("crossing", (2, 0), (4, 0))]]
        for members in variants:
            with self.assertRaises(results.Incomplete):
                results.evaluate(members, [mutant()])

    def test_incomplete_dominates_a_survivor(self):
        rows = [mutant("surviving"), mutant("timeout", replacement="3")]
        self.assertEqual(2, results.evaluate([member()], rows)["exit_code"])

    def test_unique_initializer_is_one_obligation_despite_constructor_metadata(self):
        value = member()
        value["constructors"] = ["one", "two"]
        self.assertEqual(1, len(results.evaluate([value], [mutant()])["members"]))

    def test_supplemental_block_kill_resolves_only_its_exact_identity(self):
        primary = [mutant("ignored"), mutant("killed", replacement="3")]
        supplement = {"scheduled": [results.identity(primary[0])], "rows": [mutant()]}
        rows = results.reconcile(primary, [supplement])
        self.assertEqual(["killed", "killed"], [r["state"] for r in rows])
        for changed in ([], [{"scheduled": supplement["scheduled"], "rows": []}],
                        [{"scheduled": supplement["scheduled"], "rows": [mutant(), mutant()]}],
                        [{"scheduled": supplement["scheduled"], "rows": [mutant(replacement="4")]}]):
            with self.assertRaises(results.Incomplete):
                results.reconcile(primary, changed)

    def test_supplemental_survival_is_never_erased_by_another_kill(self):
        primary = [mutant("ignored")]
        batches = [{"scheduled": [results.identity(primary[0])], "rows": [mutant("surviving")]},
                   {"scheduled": [results.identity(primary[0])], "rows": [mutant()]}]
        self.assertEqual("surviving", results.reconcile(primary, batches)[0]["state"])


class CoordinateTests(unittest.TestCase):
    def test_bom_astral_crlf_and_half_open_global_offsets(self):
        source = results.Source(b"\xef\xbb\xbf" + "//\U0001f600\r\ndef f(): return 1\r\n".encode())
        self.assertEqual(6, source.offset((2, 0)))
        self.assertEqual((1, 4), source.codepoint_position((1, 3)))
        self.assertEqual(1, source.extract((2, 16, 2, 17)).count("1"))
        with self.assertRaises(results.Incomplete):
            source.offset((1, 3))

    def test_stryker_columns_are_one_based_and_source_must_match(self):
        source = results.Source(b"def f(): return 1\n")
        report = {"schemaVersion": "2", "files": {"subject.py": {"source": source.text,
                  "mutants": [{"id": "1", "mutatorName": "Number", "replacement": "2",
                  "status": "Killed", "location": {"start": {"line": 1, "column": 17},
                  "end": {"line": 1, "column": 18}}}]}}}
        rows = results.stryker_rows(report, {"subject.py": source})
        self.assertEqual((1, 16, 1, 17), rows[0]["span"])
        report["files"]["subject.py"]["source"] += "tamper"
        with self.assertRaises(results.Incomplete):
            results.stryker_rows(report, {"subject.py": source})


class InventoryTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        (self.root / "subject.py").write_text("def f():\n    return 1\n", encoding="utf-8")
        self.data = {"schema_version": 1, "position_encoding": "utf16",
                     "candidate_sha": "a" * 40, "base_sha": "b" * 40,
                     "source_fingerprint": "c" * 64,
                     "modules": [{"path": "subject.py", "language": "python", "role": "target",
                                  "project": None, "test_projects": [], "test_command": [sys.executable, "-m", "unittest", "discover"]}],
                     "changed_members": [member(end=(2, 12))], "non_behavior_changes": []}

    def test_inventory_accepts_exact_contract_and_retains_opaque_metadata(self):
        self.data["changed_members"][0]["constructors"] = ["opaque"]
        value = results.validate_inventory(self.data, self.root, "a" * 40, "b" * 40, "c" * 64)
        self.assertEqual(["opaque"], value["changed_members"][0]["constructors"])

    def test_missing_member_unknown_stack_stale_hash_and_path_alias_fail(self):
        variants = []
        for field, value in (("changed_members", []), ("source_fingerprint", "d" * 64),
                             ("schema_version", 2)):
            data = copy.deepcopy(self.data)
            data[field] = value
            variants.append(data)
        for field, value in (("language", "ruby"), ("path", "Subject.py"),
                             ("path", "../subject.py"), ("test_command", "python; echo unsafe")):
            data = copy.deepcopy(self.data)
            data["modules"][0][field] = value
            variants.append(data)
        for data in variants:
            with self.subTest(data=data), self.assertRaises(results.Incomplete):
                results.validate_inventory(data, self.root, "a" * 40, "b" * 40, "c" * 64)


if __name__ == "__main__":
    unittest.main()

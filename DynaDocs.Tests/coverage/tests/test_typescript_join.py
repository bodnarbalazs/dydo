"""Istanbul LCOV joins the TypeScript walker's callables into policy modules."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_policy import evaluate_policy
from typescript_join import join, join_module, parse_lcov

ROOT = Path("/repo")
BASE = ROOT / "viewer"
LCOV = """TN:
SF:src/app.ts
FN:1,outer
FN:3,(anonymous_1)
FN:6,idle
FNDA:1,outer
FNDA:1,(anonymous_1)
FNDA:0,idle
DA:2,1
DA:3,1
DA:4,0
DA:6,1
BRDA:2,0,0,1
BRDA:2,0,1,-
end_of_record
"""


def method(identity, line, end_line, start, end, cc=1, cognitive=0):
    return {"id": identity, "line": line, "end_line": end_line, "start": start, "end": end,
            "cc": cc, "cognitive": cognitive, "parameters": 0, "constructor": False}


def walked(*methods, runtime=True, module=None):
    return {"methods": list(methods), "runtime": runtime, "module": module or {"cc": 1, "cognitive": 0}}


class LcovParseTests(unittest.TestCase):
    def test_records_key_by_repository_path_and_read_every_counter(self):
        record = parse_lcov(LCOV, ROOT, BASE)["viewer/src/app.ts"]
        self.assertEqual({2: 1, 3: 1, 4: 0, 6: 1}, record["lines"])
        self.assertEqual({"2:0:0": 1, "2:0:1": 0}, record["branches"])
        self.assertEqual({1: ["outer"], 3: ["(anonymous_1)"], 6: ["idle"]}, record["functions"])
        self.assertEqual(0, record["calls"]["idle"])

    def test_a_source_recorded_twice_is_refused(self):
        with self.assertRaisesRegex(ValueError, "Duplicate Istanbul LCOV record"):
            parse_lcov(LCOV + LCOV, ROOT, BASE)


class JoinModuleTests(unittest.TestCase):
    def record(self):
        return parse_lcov(LCOV, ROOT, BASE)["viewer/src/app.ts"]

    def test_each_line_belongs_to_the_innermost_callable_spanning_it(self):
        outer = method("outer:1:0", 1, 5, 0, 90)
        inner = method("inner:3:2", 3, 4, 30, 60)
        module = join_module("viewer/src/app.ts", self.record(), walked(outer, inner))
        rows = {row["id"]: (row["covered"], row["total"]) for row in module["methods"]}
        self.assertEqual({"outer:1:0": (1, 1), "inner:3:2": (1, 2), "<module>": (1, 1)}, rows)

    def test_a_callable_its_counter_never_entered_covers_nothing_on_a_hit_line(self):
        idle = method("idle:6:0", 6, 6, 100, 120)
        module = join_module("viewer/src/app.ts", self.record(), walked(idle))
        self.assertEqual((0, 1), (module["methods"][0]["covered"], module["methods"][0]["total"]))

    def test_an_empty_callable_is_measured_by_its_counter_and_ambiguity_is_refused(self):
        record = self.record()
        record["lines"] = {}
        module = join_module("viewer/src/app.ts", record, walked(method("outer:1:0", 1, 1, 0, 9)))
        self.assertEqual((1, 1), (module["methods"][0]["covered"], module["methods"][0]["total"]))
        record["functions"][1].append("twin")
        with self.assertRaisesRegex(ValueError, "No unambiguous Istanbul LCOV point"):
            join_module("viewer/src/app.ts", record, walked(method("outer:1:0", 1, 1, 0, 9)))

    def test_top_level_statements_are_the_module_callable(self):
        scores = {"cc": 3, "cognitive": 2}
        module = join_module("viewer/src/app.ts", self.record(), walked(module=scores))
        self.assertEqual([{"id": "<module>", "line": 1, "cc": 3, "cognitive": 2, "parameters": 0,
                           "constructor": False, "covered": 3, "total": 4}], module["methods"])

    def test_a_module_absent_from_lcov_is_declarative_only_when_nothing_in_it_runs(self):
        declared = join_module("viewer/src/types.ts", None, walked(runtime=False))
        self.assertEqual({"path": "viewer/src/types.ts", "language": "typescript", "executable": False,
                          "lines": {}, "branches": {}, "methods": []}, declared)
        self.assertEqual([], evaluate_policy([declared]))
        with self.assertRaisesRegex(ValueError, "no record for executable module: viewer/src/setup.ts"):
            join_module("viewer/src/setup.ts", None, walked())


class JoinTests(unittest.TestCase):
    def test_an_uncovered_module_fails_its_line_and_branch_floors(self):
        lcov = LCOV + "SF:src/unused.ts\nFN:1,unused\nFNDA:0,unused\nDA:2,0\nDA:3,0\nBRDA:2,0,0,0\nend_of_record\n"
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "lcov.info"
            path.write_text(lcov, encoding="utf-8")
            modules = join(path, ROOT, BASE, ["viewer/src/unused.ts"],
                           {"viewer/src/unused.ts": walked(method("unused:1:7", 1, 4, 0, 50, cc=5))})
        findings = {(row["path"], row["gate"]) for row in evaluate_policy(modules)}
        self.assertEqual({("viewer/src/unused.ts", "line-coverage"), ("viewer/src/unused.ts", "branch-coverage"),
                          ("viewer/src/unused.ts", "hcrap")}, findings)


if __name__ == "__main__":
    unittest.main()

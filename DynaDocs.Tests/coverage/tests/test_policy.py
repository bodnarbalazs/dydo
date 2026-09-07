"""Universal thresholds are independent of language and old tier annotations."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from gate_policy import evaluate_policy


def module(line=80, branch=60, cognitive=0, cc=1, parameters=0, constructor=False):
    return {"path": "sample.py", "executable": True,
            "lines": {str(n): int(n < line) for n in range(100)},
            "branches": {str(n): int(n < branch) for n in range(100)},
            "methods": [{"id": "sample.f:1", "line": 1, "cc": cc,
                         "cognitive": cognitive, "parameters": parameters,
                         "constructor": constructor, "covered": line, "total": 100}]}


class PolicyTests(unittest.TestCase):
    def test_structural_csharp_points_keep_coverage_floors_with_explicit_body_accounting(self):
        item = {'path': 'Model.cs', 'executable': True, 'lines': {'1': 0}, 'branches': {}, 'methods': [],
                'body_owners': [{'physical': 'System.Int32 Model::get_Value()', 'key': 'Model::get_Value`0()',
                                 'structural': 'semantic synthesized member without authored body',
                                 'logical': None, 'lines': {'1': 0}, 'branches': {}}]}
        self.assertEqual({'line-coverage'}, self.kinds(item))
        item['body_owners'] = []
        with self.assertRaises(ValueError):
            evaluate_policy([item])

    def test_cross_file_constructor_body_needs_the_real_logical_metric(self):
        first = {'path': 'A.cs', 'executable': True, 'lines': {'1': 1}, 'branches': {}, 'methods': [],
                 'body_owners': [{'physical': 'System.Void C::.ctor()', 'key': 'C::.ctor`0()', 'structural': None,
                                  'logical': {'path': 'B.cs', 'id': 'ctor|System.Void C::.ctor()'}, 'lines': {'1': 1}, 'branches': {}}]}
        second = {'path': 'B.cs', 'executable': True, 'lines': {'2': 1}, 'branches': {},
                  'methods': [{'id': 'ctor|System.Void C::.ctor()', 'line': 2, 'cc': 1, 'cognitive': 0,
                               'parameters': 0, 'constructor': True, 'covered': 2, 'total': 2}],
                  'body_owners': [{'physical': 'System.Void C::.ctor()', 'key': 'C::.ctor`0()', 'structural': None,
                                   'logical': {'path': 'B.cs', 'id': 'ctor|System.Void C::.ctor()'}, 'lines': {'2': 1}, 'branches': {}}]}
        self.assertEqual([], evaluate_policy([first, second]))
        second['methods'] = []
        with self.assertRaises(ValueError):
            evaluate_policy([first, second])

    def kinds(self, item):
        return {finding["gate"] for finding in evaluate_policy([item])}

    def test_exact_floors_pass_and_lower_values_fail(self):
        self.assertEqual(set(), self.kinds(module()))
        self.assertIn("line-coverage", self.kinds(module(line=79)))
        self.assertIn("branch-coverage", self.kinds(module(branch=59)))

    def test_fully_covered_branchy_switch_has_no_cyclomatic_ceiling(self):
        self.assertEqual(set(), self.kinds(module(100, 100, cognitive=7, cc=25)))

    def test_hcrap_keeps_coverage_penalty_and_cognitive_floor(self):
        self.assertEqual(set(), self.kinds(module(100, 100, cognitive=20)))
        self.assertEqual({"hcrap", "cognitive"}, self.kinds(module(100, 100, cognitive=21)))
        self.assertIn("hcrap", self.kinds(module(80, 100, cognitive=1, cc=50)))

    def test_parameter_limit_exempts_only_constructors(self):
        self.assertEqual(set(), self.kinds(module(parameters=7)))
        self.assertEqual({"parameters"}, self.kinds(module(parameters=8)))
        self.assertEqual(set(), self.kinds(module(parameters=8, constructor=True)))

    def test_uncovered_method_cannot_be_hidden_by_module_average(self):
        item = module(100, 100)
        item["methods"][0].update(covered=0, cc=5)
        self.assertIn("hcrap", self.kinds(item))

    def test_branchless_module_has_no_artificial_branch_violation(self):
        item = module(100)
        item["branches"] = {}
        self.assertEqual(set(), self.kinds(item))

    def test_missing_method_or_coverage_facts_fail_closed(self):
        for key in ("methods", "lines", "branches"):
            with self.subTest(key=key):
                item = module()
                del item[key]
                with self.assertRaises(ValueError):
                    evaluate_policy([item])
        for invalid in ({"methods": []}, {"lines": {}}, {"methods": [{"id": "lost"}]}):
            with self.subTest(invalid=invalid):
                item = module()
                item.update(invalid)
                with self.assertRaises(ValueError):
                    evaluate_policy([item])

    def test_duplicate_identity_is_not_merged_by_best_percentage(self):
        with self.assertRaises(ValueError):
            evaluate_policy([module(), module(100, 100)])


if __name__ == "__main__":
    unittest.main()


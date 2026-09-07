"""Python metrics preserve lexical identity and language parameter semantics."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from python_metrics import callable_nodes, parameter_count, nested_ternaries, import_edges
import ast
import json
import subprocess


class PythonMetricsTests(unittest.TestCase):
    def test_script_import_roots_join_tooling_from_test_source_without_short_name_guess(self):
        modules = {'tools/metric.py', 'tools/tests/test_metric.py'}
        self.assertEqual([('tools/tests/test_metric.py', 'tools/metric.py')],
            import_edges('tools/tests/test_metric.py', ast.parse('import metric'), modules, ['tools']))
        with self.assertRaisesRegex(ValueError, 'Ambiguous'):
            import_edges('tools/tests/test_metric.py', ast.parse('import metric'),
                         modules | {'other/metric.py'}, ['tools', 'other'])

    def test_module_metrics_keep_class_initializers_and_definition_expressions(self):
        from python_metrics import module_scores
        source = 'flag = True\nclass C:\n    value = 1 if flag else 2\n    def unused(self, value=1 if flag else 2):\n        if flag: return 3\n'
        self.assertEqual({'cognitive': 2, 'cc': 3}, module_scores(source))

    def test_duplicate_nested_names_have_distinct_source_identities(self):
        source = "def outer():\n    def inner(x): return x\n    return inner(1)\ndef other():\n    def inner(x): return x\n    return inner(2)\n"
        rows = callable_nodes(ast.parse(source))
        self.assertEqual(["outer:1:0", "outer.inner:2:4", "other:4:0", "other.inner:5:4"],
                         [row["id"] for row in rows])

    def test_parameters_count_receiver_only_for_actual_bound_methods(self):
        source = "def free(self, a, /, *args, x=1, **kwargs): pass\nclass C:\n    def method(self, a, *, b): pass\n    @staticmethod\n    def static(self, a): pass\n    @classmethod\n    def bound(cls, a): pass\n"
        rows = callable_nodes(ast.parse(source))
        self.assertEqual([5, 2, 2, 1], [parameter_count(row) for row in rows])

    def test_async_and_two_lambdas_on_one_line_remain_distinct(self):
        source = "async def work(x): return x\na, b = lambda x: x, lambda y: y\n"
        rows = callable_nodes(ast.parse(source))
        self.assertEqual(3, len(rows))
        self.assertEqual(3, len({row["id"] for row in rows}))

    def test_nested_ternary_detected_without_crossing_callable_boundary(self):
        self.assertEqual([1], nested_ternaries(ast.parse("x = 1 if a else (2 if b else 3)")))
        self.assertEqual([], nested_ternaries(ast.parse("x = (lambda: 1 if a else 2) if b else None")))

    def test_local_import_graph_resolves_alias_and_relative_module(self):
        modules = {"pkg/a.py", "pkg/b.py", "pkg/__init__.py"}
        edges = import_edges("pkg/a.py", ast.parse("from . import b as item\nimport os\n"), modules)
        self.assertEqual({("pkg/a.py", "pkg/b.py")}, set(edges))

    def test_real_complexipy_and_radon_score_nested_callables_separately(self):
        root = Path(__file__).resolve().parents[3]
        python = root / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        source = "def outer(x):\n    if x: return 1\n    def inner(y):\n        if y: return 2\n        return 0\n    return inner(x)\n"
        result = subprocess.run([str(python), str(Path(__file__).resolve().parents[1] / "python_metrics.py")],
                                input=source, text=True, capture_output=True, check=True)
        methods = json.loads(result.stdout)["methods"]
        self.assertEqual(["outer:1:0", "outer.inner:3:4"], [row["id"] for row in methods])
        self.assertEqual([3, 1], [row["cognitive"] for row in methods])
        self.assertEqual([2, 2], [row["cc"] for row in methods])

    def test_decorated_callables_and_unicode_columns_use_exact_source_spans(self):
        root = Path(__file__).resolve().parents[3]
        python = root / "dydo/_system/.local/static-gates/python/Scripts/python.exe"
        source = '@decorator\ndef work(x):\n    if x: return 1\n    return 0\né = "😀"; value = lambda: 1\n'
        result = subprocess.run([str(python), str(Path(__file__).resolve().parents[1] / "python_metrics.py")],
                                input=source, text=True, encoding='utf-8', capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        methods = json.loads(result.stdout)['methods']
        self.assertEqual(2, methods[0]['line'])
        self.assertEqual(1, methods[0]['cognitive'])
        self.assertEqual(18, methods[1]['column'])


if __name__ == "__main__":
    unittest.main()


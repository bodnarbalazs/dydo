"""Pinned language parsers preserve behavior tokens and canonical Unicode spans."""
import json
import subprocess
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_tokens import python_tokens


class SourceTokenTests(unittest.TestCase):
    def test_python_comments_and_crlf_do_not_change_tokens_but_newline_grammar_does(self):
        first = python_tokens('def value():\n    return 1\n')
        shifted = python_tokens('# comment\r\ndef value():\r\n    return 1 # tail\r\n')
        key = lambda rows: [(row['kind'], row['text']) for row in rows]
        self.assertEqual(key(first), key(shifted))
        self.assertNotEqual(key(python_tokens('def value():\n    return\n    1\n')), key(first))

    def test_python_unicode_columns_and_nested_only_edit_use_exact_native_tokens(self):
        before = 'def outer():\n    text="雪😀"; left=lambda:1; right=lambda:2\n    return left()\n'
        after = before.replace('lambda:1', 'lambda:3')
        before_tokens, after_tokens = python_tokens(before), python_tokens(after)
        changed = [(left['text'], right['text']) for left, right in zip(before_tokens, after_tokens)
                   if left != right]
        self.assertEqual([('1', '3')], changed)
        value = next(row for row in python_tokens(before) if row['text'] == 'left')
        self.assertEqual(16, value['column'])

    def test_python_missing_final_newline_is_valid_and_malformed_source_fails_closed(self):
        self.assertTrue(python_tokens('value = 1'))
        for source in ('value = "unfinished', 'return 1'):
            with self.subTest(source=source), self.assertRaises(ValueError):
                python_tokens(source)

    def test_javascript_native_tokens_exclude_comments_and_preserve_shebang(self):
        root = Path(__file__).resolve().parents[3]
        producer = root / 'DynaDocs.Tests/coverage/js_metrics.cjs'
        def facts(source):
            result = subprocess.run(['node', str(producer)], input=source, capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, result.returncode, result.stderr)
            return json.loads(result.stdout)
        before = facts('const text="雪😀"; const a=()=>1, b=()=>2;')
        after = facts('/* shifted */ const text="雪😀"; const a=()=>3, b=()=>2;')
        changed = [(left['text'], right['text']) for left, right in zip(before['tokens'], after['tokens'])
                   if left != right]
        self.assertEqual([('1', '3')], changed)
        self.assertEqual('Shebang', facts('#!/usr/bin/env node\nmodule.exports = 1;')['tokens'][0]['kind'])

    def test_csharp_native_tokens_exclude_comments_and_preserve_directives(self):
        import test_csharp_metrics
        case = test_csharp_metrics.CSharpMetricsTests
        case.setUpClass()
        def facts(source):
            result = subprocess.run(['dotnet', str(case.dll), '--syntax'], input=source,
                                    capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(0, result.returncode, result.stderr)
            return json.loads(result.stdout)
        before = facts('class C { int A()=>1; int B()=>2; }')
        after = facts('/* shifted */ class C { int A()=>3; int B()=>2; }')
        changed = [(left['text'], right['text']) for left, right in zip(before['tokens'], after['tokens'])
                   if left != right]
        self.assertEqual([('1', '3')], changed)
        directive = facts('#define FLAG\nclass C { int A()=>1; }')['tokens'][0]
        self.assertEqual('DefineDirectiveTrivia', directive['kind'])


if __name__ == '__main__':
    unittest.main()

"""Strict action-constraint interpretation; frozen reference is independent of new output."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[6]
ORIGINAL = HERE.parent / 'b3-source'
sys.path.insert(0, str(ORIGINAL))
_spec = importlib.util.spec_from_file_location('_b3_action_original', ORIGINAL / 'worker.py')
baseline = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(baseline)


def expected(distinct):
    return dict(exit=12, generated=3, distinct=distinct, complete=False,
        invariant=['BelowTwo'], temporal=False, values=[0, 1, 2],
        actions=['Initial predicate',
                 'Next line 5, col 9 to line 5, col 24 of module Transport',
                 'Next line 5, col 9 to line 5, col 24 of module Transport'],
        back=[], stuttering=False)


def require(facts, distinct):
    if facts != expected(distinct):
        raise ValueError('Action-constraint semantic facts differ from independent pinned reference')
    return facts


def parse(text, code, distinct):
    facts = baseline.semantic(text, code)
    allowed = ['Error: Invariant BelowTwo is violated.', 'Error: The behavior up to this point is:']
    errors = [line for line in text.splitlines() if line.startswith('Error:')]
    if errors != allowed:
        raise ValueError('Unrelated or missing TLC diagnostic')
    return require(facts, distinct)


def verify_rows(root, rows):
    for row in rows:
        path = root / row['path']
        if path.stat().st_size != row['bytes'] or hashlib.sha256(path.read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Independent reference/input identity differs: ' + row['path'])


def retained(root=ROOT):
    binding = json.loads((HERE / 'reference-binding.json').read_bytes())
    verify_rows(root, binding['files'])
    results = {}
    for name, distinct in [('action-bound-reference', 2), ('invariant-bad-reference', 3)]:
        directory = root / binding['outputDirectory']
        code = json.loads((directory / (name + '.result.json')).read_bytes())['exit']
        text = (directory / (name + '.stdout')).read_text(encoding='utf-8')
        text += (directory / (name + '.stderr')).read_text(encoding='utf-8')
        results[name] = parse(text, code, distinct)
    constrained, unconstrained = results.values()
    if dict(constrained, distinct=3) != unconstrained:
        raise ValueError('Retained constraint admission comparison differs')
    return results

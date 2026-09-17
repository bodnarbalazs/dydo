"""Python source facts use lexical identity, never a short-name join."""
import ast
import json
import sys
from pathlib import PurePosixPath
from positions import canonical_text, utf16_column

CALLABLES = (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda)


def callable_nodes(tree):
    rows = []

    def visit(node, scope, parent):
        nested_scope = scope
        if isinstance(node, (ast.ClassDef, *CALLABLES)):
            name = getattr(node, "name", "<lambda>")
            nested_scope = [*scope, name]
        if isinstance(node, CALLABLES):
            identity = f"{'.'.join(nested_scope)}:{node.lineno}:{node.col_offset}"
            rows.append({"id": identity, "node": node, "parent": parent})
        for child in ast.iter_child_nodes(node):
            visit(child, nested_scope, node)

    visit(tree, [], None)
    return rows


def parameter_count(row):
    node = row["node"]
    args = node.args
    positional = args.posonlyargs + args.args
    count = len(positional) + len(args.kwonlyargs) + bool(args.vararg) + bool(args.kwarg)
    if isinstance(row["parent"], ast.ClassDef) and positional:
        decorators = getattr(node, "decorator_list", [])
        if not any(isinstance(item, ast.Name) and item.id == "staticmethod" for item in decorators):
            count -= 1
    return count


def _contains_ternary(node):
    if isinstance(node, ast.IfExp):
        return True
    if isinstance(node, CALLABLES):
        return False
    return any(_contains_ternary(child) for child in ast.iter_child_nodes(node))


def nested_ternaries(tree):
    return sorted({node.lineno for node in ast.walk(tree)
                   if isinstance(node, ast.IfExp)
                   and any(_contains_ternary(child) for child in ast.iter_child_nodes(node))})


def _resolve_module(name, folder, modules, search_roots=()):
    relative = name.replace(".", "/")
    choices = {f"{relative}.py", f"{relative}/__init__.py"}
    if folder:
        choices.update({f"{folder}/{relative}.py", f"{folder}/{relative}/__init__.py"})
    for root in search_roots:
        choices.update({f"{root}/{relative}.py", f"{root}/{relative}/__init__.py"})
    matches = choices & modules
    if len(matches) > 1:
        raise ValueError(f"Ambiguous local import {name}: {sorted(matches)}")
    return next(iter(matches), None)


def _relative_import_targets(node, path):
    folder = PurePosixPath(path).parent
    for _ in range(node.level - 1):
        folder = folder.parent
    prefix = folder.as_posix().replace('/', '.') if str(folder) != "." else ""
    base = ".".join(part for part in (prefix, node.module) if part)
    candidates = [base] if node.module else []
    candidates.extend(".".join(part for part in (base, item.name) if part)
                      for item in node.names)
    return [(candidate, "") for candidate in candidates]


def _absolute_import_targets(node, path):
    folder = str(PurePosixPath(path).parent)
    candidates = [node.module or ""]
    candidates.extend(f"{node.module}.{item.name}" for item in node.names)
    return [(candidate, folder) for candidate in candidates]


def _import_targets(node, path):
    if isinstance(node, ast.Import):
        return [(name.name, str(PurePosixPath(path).parent)) for name in node.names]
    if not isinstance(node, ast.ImportFrom):
        return []
    if node.level:
        return _relative_import_targets(node, path)
    return _absolute_import_targets(node, path)


def import_edges(path, tree, modules, search_roots=()):
    edges = set()
    for node in ast.walk(tree):
        for name, folder in _import_targets(node, path):
            target = _resolve_module(name, folder, modules, search_roots)
            if target:
                edges.add((path, target))
    return sorted(edges)


def _callable_scores(node):
    import complexipy
    from radon.complexity import cc_visit
    from radon.visitors import ComplexityVisitor

    snippet = ast.unparse(node)
    cognitive = complexipy.code_complexity(snippet)
    cyclomatic = cc_visit(snippet)
    if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
        declaration_line = ast.parse(snippet).body[0].lineno
        matches = [item for item in cognitive.functions if item.name == node.name and item.line_start == 1]
        blocks = [item for item in cyclomatic if item.name == node.name and item.lineno == declaration_line]
        if len(matches) != 1 or len(blocks) != 1:
            raise ValueError(f"Missing or ambiguous Python metric: {node.name}")
        return matches[0].complexity, blocks[0].complexity
    return cognitive.complexity, ComplexityVisitor.from_code(snippet).complexity


class _ModuleExecution(ast.NodeTransformer):
    """Retain expressions executed by module/class code, excluding callable bodies."""
    def __init__(self, deferred_annotations):
        self.deferred_annotations = deferred_annotations

    def _defaults(self, node):
        return [*node.args.defaults, *(value for value in node.args.kw_defaults if value is not None)]

    def visit_Lambda(self, node):
        return ast.Tuple(elts=[self.visit(value) for value in self._defaults(node)], ctx=ast.Load())

    def visit_FunctionDef(self, node):
        expressions = [*node.decorator_list, *self._defaults(node)]
        if not self.deferred_annotations:
            arguments = [*node.args.posonlyargs, *node.args.args, *node.args.kwonlyargs]
            arguments.extend(value for value in (node.args.vararg, node.args.kwarg) if value is not None)
            expressions.extend(argument.annotation for argument in arguments if argument.annotation is not None)
            if node.returns is not None:
                expressions.append(node.returns)
        return [ast.Expr(value=self.visit(expression)) for expression in expressions]

    def visit_AsyncFunctionDef(self, node):
        return self.visit_FunctionDef(node)

    def visit_ClassDef(self, node):
        expressions = [*node.decorator_list, *node.bases, *(item.value for item in node.keywords)]
        body = [ast.Expr(value=self.visit(expression)) for expression in expressions]
        for statement in node.body:
            transformed = self.visit(statement)
            body.extend(transformed if isinstance(transformed, list) else [transformed])
        return body


def module_scores(source):
    import complexipy
    from radon.visitors import ComplexityVisitor
    tree = ast.parse(source)
    deferred = any(isinstance(node, ast.ImportFrom) and node.module == '__future__'
                   and any(alias.name == 'annotations' for alias in node.names) for node in tree.body)
    execution = _ModuleExecution(deferred).visit(tree)
    snippet = ast.unparse(ast.fix_missing_locations(execution))
    return {'cognitive': complexipy.code_complexity(snippet).complexity,
            'cc': ComplexityVisitor.from_code(snippet).complexity}


def source_metrics(source):
    tree = ast.parse(source)
    lines = source.splitlines()
    methods = []
    for row in callable_nodes(tree):
        node = row["node"]
        cognitive, cc = _callable_scores(node)
        column = utf16_column(lines[node.lineno - 1], node.col_offset, 'utf8')
        identity = row['id'].rsplit(':', 1)[0] + ':' + str(column)
        methods.append({"id": identity, "line": node.lineno,
                        "end_line": node.end_lineno,
                        "column": column,
                        "end_column": utf16_column(lines[node.end_lineno - 1], node.end_col_offset, 'utf8'), "cognitive": cognitive,
                        "cc": cc, "parameters": parameter_count(row),
                        "constructor": isinstance(row["parent"], ast.ClassDef)
                        and getattr(node, "name", "") in ("__init__", "__new__")})
    return {"methods": methods, "nested_ternaries": nested_ternaries(tree)}


if __name__ == "__main__":
    source = canonical_text(sys.stdin.buffer.read())
    print(json.dumps({**source_metrics(source), "module": module_scores(source)}))

"""Execute independent collectors and retain exact gaps beside measured findings."""
import json
import re
from pathlib import Path

from gate_inventory import assembly_path, assemble_inventory, dependency_cycles, test_project_role
from gate_run import CommandLog, result
from inventory import git_file_state


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def metric_findings(path, methods):
    findings = []
    for method in methods:
        for key, limit in (('cognitive', 20), ('parameters', 7)):
            value = method[key]
            if type(value) is not int or value < 0:
                raise ValueError('Invalid static method metric')
            if key == 'parameters' and method['constructor']:
                continue
            if value > limit:
                findings.append({'path': path, 'member': method['id'], 'line': method['line'],
                                 'gate': key, 'actual': value, 'threshold': limit})
    return findings


class Collectors:
    def __init__(self, root, output):
        self.root = Path(root).resolve()
        self.output = Path(output).resolve()
        self.log = CommandLog(self.root, self.output / 'commands')
        self.coverage = self.root / 'DynaDocs.Tests/coverage'
        self.python = self.root / 'dydo/_system/.local/static-gates/python/Scripts/python.exe'
        self.paths, self.deleted = git_file_state(self.root)
        self.inventory = None
        self.project_rows = []
        self.static = {}
        self.discovery = []

    def command_json(self, name, command, **kwargs):
        row = self.log.run(name, command, **kwargs)
        if row['exit_code'] != 0:
            raise ValueError(f"{name} failed with exit {row['exit_code']}; evidence {row['stderr']}")
        return read_json(row['stdout'])

    def projects(self):
        errors = []
        for index, project in enumerate(path for path in self.paths if path.endswith('.csproj')):
            try:
                restore = self.log.run(f'restore-{index}', ['dotnet', 'restore', project, '--verbosity', 'quiet',
                                       '-p:NuGetAudit=false'])
                if restore['exit_code'] != 0:
                    raise ValueError('Project restore failed; evaluated imports are unreliable')
                facts = self.command_json(f'compile-{index}', ['dotnet', 'msbuild', project,
                    '-getItem:Compile,PackageReference', '-getProperty:IsTestProject,TargetPath'])
                test = test_project_role(facts['Properties']['IsTestProject'],
                                         [row['Identity'] for row in facts['Items']['PackageReference']])
                files = [Path(row['FullPath']).resolve().relative_to(self.root).as_posix()
                         for row in facts['Items']['Compile']]
                if len(files) != len(set(files)):
                    raise ValueError('Duplicate evaluated Compile identity')
                self.project_rows.append({'path': project, 'test': test, 'compile': files,
                                          'assembly': assembly_path(self.root, facts['Properties']['TargetPath'])})
            except (ValueError, KeyError, OSError) as error:
                errors.append({'path': project, 'message': str(error)})
        if not self.project_rows:
            errors.append({'message': 'No evaluated C# projects'})
        return result({'projects': self.project_rows}, errors=errors)

    def source_inventory(self):
        manifest_path = self.coverage / 'test-associations.json'
        manifest = read_json(manifest_path) if manifest_path.is_file() else {'schema': 1, 'modules': []}
        self.inventory = assemble_inventory(self.root, self.paths, self.project_rows, self.discovery,
                                            self.deleted, manifest)
        return result(self.inventory, errors=self.inventory['errors'])

    def associations(self):
        from associations import validate_associations
        if self.inventory is None:
            return result(errors=[{'message': 'Source inventory unavailable'}])
        manifest = read_json(self.coverage / 'test-associations.json')
        try:
            findings = validate_associations(self.inventory['sources'], manifest)
            return result({'manifest': manifest}, findings=findings)
        except (ValueError, KeyError, OSError) as error:
            return result(errors=[{'message': str(error)}])

    def sources(self, language):
        if self.inventory is None:
            raise ValueError('Source inventory unavailable')
        return [row for row in self.inventory['sources'] if row['language'] == language]

    def csharp_source(self):
        project = self.coverage / 'metrics/GateMetrics.csproj'
        build = self.log.run('metrics-measurement-build', ['dotnet', 'build', project,
                             '--verbosity', 'quiet', '-p:RunAnalyzers=false', '-p:NuGetAudit=false'])
        if build['exit_code'] != 0:
            return result(errors=[{'message': 'Measurement producer build failed', 'command': build}])
        producer = self.coverage / 'metrics/bin/Debug/net10.0/GateMetrics.dll'
        facts, errors, findings, edges = [], [], [], []
        for index, item in enumerate(self.project_rows):
            try:
                row = self.command_json(f'csharp-source-{index}', ['dotnet', producer, '--project',
                                        self.root / item['path'], '--root', self.root])
                if row.get('has_compilation') is not True or row['project'] != item['path']:
                    raise ValueError('Missing or mismatched real C# compilation')
                facts.append(row)
                edges.extend(row['namespace_edges'])
                for source in row['files']:
                    findings.extend(metric_findings(source['path'], source['methods']))
            except (ValueError, KeyError, OSError) as error:
                errors.append({'path': item['path'], 'message': str(error)})
        cycles = dependency_cycles(edges)
        findings.extend({'gate': 'namespace-cycle', 'members': cycle} for cycle in cycles)
        self.static['cs'] = facts
        return result({'projects': facts, 'namespace_edges': edges}, findings, errors)

    def csharp_analyzers(self):
        from gate_diagnostics import normalize_csharp_diagnostics
        facts, raw, errors = [], [], []
        for index, project in enumerate(self.project_rows):
            sarif = self.output / f'analyzers-{index}.sarif'
            row = self.log.run(f'analyzers-{index}', ['dotnet', 'build', project['path'], '--no-incremental',
                '--verbosity', 'quiet', '-warnaserror', '-p:RunAnalyzers=true', '-p:RunAnalyzersDuringBuild=true',
                '-p:NuGetAudit=false', f'-p:ErrorLog={sarif}'])
            facts.append(row)
            if not sarif.is_file():
                errors.append({'path': project['path'], 'message': 'Missing native analyzer SARIF'})
                continue
            data = read_json(sarif)
            diagnostics = [item for run in data['runs'] for item in run.get('results', [])]
            for diagnostic in diagnostics:
                raw.append({'project': project['path'], 'diagnostic': diagnostic})
            if row['exit_code'] != 0 and not diagnostics:
                errors.append({'path': project['path'], 'message': 'Build failed without complete analyzer diagnostics', 'command': row})
        generated = {}
        for project in self.static.get('cs', []):
            for path in project['generated_files']:
                generated.setdefault(path, []).append(project['project'])
        normalized = normalize_csharp_diagnostics(self.root, raw,
            {row['path'] for row in self.sources('cs')}, generated)
        findings = normalized['findings']
        errors.extend(normalized['errors'])
        for diagnostic in normalized['generated']:
            if not diagnostic['suppression_states'] and diagnostic['level'] in ('warning', 'error'):
                findings.append({'gate': 'generated-build-diagnostic', **diagnostic})
        if any(row['exit_code'] != 0 for row in facts) and not findings:
            errors.append({'message': 'Native build failed without an accounted unsuppressed diagnostic'})
        return result({'commands': facts, 'raw': raw, 'generated': normalized['generated']}, findings, errors)

    def python_source(self):
        from positions import canonical_text
        facts, findings, errors = [], [], []
        for index, source in enumerate(self.sources('python')):
            try:
                text = canonical_text((self.root / source['path']).read_bytes())
                metrics = self.command_json(f'python-source-{index}',
                    [self.python, self.coverage / 'python_metrics.py'], stdin=text)
                row = {'path': source['path'], **metrics}
                facts.append(row)
                findings.extend(metric_findings(row['path'], row['methods']))
                findings.extend({'path': row['path'], 'gate': 'nested-ternary', 'line': line}
                                for line in row['nested_ternaries'])
                if row['module']['cognitive'] > 20:
                    findings.append({'path': row['path'], 'member': '<module>', 'gate': 'cognitive',
                                     'actual': row['module']['cognitive'], 'threshold': 20})
            except (ValueError, SyntaxError, KeyError, OSError) as error:
                errors.append({'path': source['path'],
                               'message': f'Python metric process failed ({self.python}): {error}'})
        self.static['python'] = facts
        return result({'modules': facts}, findings, errors)

    def python_dead_code(self):
        paths = [row['path'] for row in self.sources('python')]
        if not paths:
            return result(errors=[{'message': 'Missing Python inventory'}])
        ruff = self.log.run('ruff', [self.python, '-m', 'ruff', 'check', '--isolated', '--select', 'F',
                            '--output-format', 'json', *paths])
        findings = [{'gate': 'ruff', 'diagnostic': row} for row in read_json(ruff['stdout'])]
        errors = []
        if ruff['exit_code'] not in (0, 1):
            errors.append({'message': 'Ruff execution failure', 'command': ruff})
        vulture = self.log.run('vulture', [self.python, '-m', 'vulture', *paths])
        for line in Path(vulture['stdout']).read_text(encoding='utf-8').splitlines():
            match = re.fullmatch(r'(.+):(\d+): (.+) \((\d+)% confidence\)', line)
            if match:
                findings.append({'gate': 'vulture', 'path': match[1].replace('\\', '/'), 'line': int(match[2]),
                                 'message': match[3], 'confidence': int(match[4])})
            elif line.strip():
                errors.append({'message': 'Unrecognized pinned Vulture output', 'output': line})
        if vulture['exit_code'] not in (0, 3):
            errors.append({'message': 'Vulture execution failure', 'command': vulture})
        return result({'commands': [ruff, vulture]}, findings, errors)

    def python_dependencies(self):
        import ast
        from python_metrics import import_edges
        from positions import canonical_text
        paths = {source['path'] for source in self.sources('python')}
        roots = ['DynaDocs.Tests/coverage', 'DynaDocs.Tests/coverage/tests']
        edges, errors = [], []
        for path in sorted(paths):
            try:
                tree = ast.parse(canonical_text((self.root / path).read_bytes()))
                edges.extend(import_edges(path, tree, paths, roots))
                for node in ast.walk(tree):
                    if not isinstance(node, ast.Call):
                        continue
                    name = getattr(node.func, 'id', None) or getattr(node.func, 'attr', None)
                    if name in ('__import__', 'import_module', 'spec_from_file_location'):
                        errors.append({'path': path, 'line': node.lineno,
                                       'message': 'Dynamic Python import requires an exact module identity witness', 'call': name})
            except (ValueError, SyntaxError, OSError) as error:
                errors.append({'path': path, 'message': str(error)})
        findings = [{'gate': 'module-cycle', 'members': cycle} for cycle in dependency_cycles(edges)]
        return result({'edges': edges, 'search_roots': roots}, findings, errors)

    def javascript_source(self):
        facts, findings, errors = [], [], []
        for index, source in enumerate(self.sources('javascript')):
            try:
                kind = 'module' if source['path'].endswith('.mjs') else 'commonjs'
                text = (self.root / source['path']).read_text(encoding='utf-8-sig')
                row = self.command_json(f'javascript-source-{index}', ['node', self.coverage / 'js_metrics.cjs', kind], stdin=text)
                row['path'] = source['path']
                facts.append(row)
                findings.extend(metric_findings(row['path'], row['methods']))
                findings.extend({'path': row['path'], 'gate': 'javascript-analyzer', 'diagnostic': value} for value in row['diagnostics'])
                findings.extend({'path': row['path'], 'gate': 'coverage-suppression', 'diagnostic': value} for value in row['suppressions'])
            except (ValueError, KeyError, OSError) as error:
                errors.append({'path': source['path'], 'message': str(error)})
        self.static['javascript'] = facts
        return result({'modules': facts}, findings, errors)

    def python_discovery(self):
        output = self.output / 'python-discovery.json'
        config = self.output / 'python-discovery-config.json'
        config.write_text(json.dumps({'root': str(self.root), 'directories': ['DynaDocs.Tests/coverage/tests'],
                                     'discover_only': True, 'output': str(output)}), encoding='utf-8')
        row = self.log.run('python-discovery', [self.python, self.coverage / 'test_discovery.py', '--config', config])
        if row['exit_code'] != 0 or not output.is_file():
            return result(errors=[{'message': 'Native unittest discovery failed', 'command': row}])
        facts = read_json(output)
        self.discovery.extend(facts['cases'])
        return result(facts)

    def javascript_discovery(self):
        files = [row['path'] for row in self.sources('javascript') if re.search(r'\.test\.[cm]?js$', row['path'])]
        if not files:
            return result(errors=[{'message': 'No explicit Node test files'}])
        row = self.log.run('node-discovery', ['node', '--test',
            '--test-reporter=' + (self.coverage / 'test_discovery.cjs').as_uri(), *files], environment={'DYDO_GATE_ROOT': str(self.root)})
        events = [json.loads(line) for line in Path(row['stdout']).read_text(encoding='utf-8').splitlines() if line]
        cases = [event for event in events if event.get('kind') == 'case']
        summaries = [event for event in events if event.get('kind') == 'summary']
        if len(summaries) != 1 or not cases or summaries[0]['counts']['tests'] != len(cases):
            return result(errors=[{'message': 'Incomplete native Node discovery', 'command': row}])
        failures = [{'path': case['file'], 'member': case['id'], 'gate': 'functional', 'error': case['error']}
                    for case in cases if not case['passed'] or case['skipped'] or case['todo']]
        errors = [] if row['exit_code'] in (0, 1) else [{'message': 'Native Node discovery command failed', 'command': row}]
        self.discovery.extend(cases)
        return result({'cases': cases, 'summary': summaries[0]}, failures, errors)

    def clones(self):
        from gate_clones import collect_clones
        return collect_clones(self)

    def javascript_dependencies(self):
        paths = {row['path'] for row in self.sources('javascript')}
        native = self.command_json('dependency-cruiser', ['node',
            self.coverage / 'node_modules/dependency-cruiser/bin/dependency-cruise.mjs',
            '--no-config', '--output-type', 'json', '--do-not-follow', 'node_modules', *sorted(paths)])
        modules = native['modules']
        identities = [row['source'] for row in modules]
        if len(identities) != len(set(identities)) or not paths <= set(identities):
            raise ValueError('Missing or ambiguous dependency-cruiser source identity')
        edges, errors = [], []
        for module in modules:
            if module['source'] not in paths:
                continue
            for dependency in module['dependencies']:
                if dependency.get('couldNotResolve'):
                    errors.append({'path': module['source'], 'message': 'Unresolved native JavaScript dependency', 'dependency': dependency})
                if dependency['resolved'] in paths:
                    edges.append([module['source'], dependency['resolved']])
        findings = [{'gate': 'module-cycle', 'members': cycle} for cycle in dependency_cycles(edges)]
        return result({'native': native, 'edges': edges}, findings, errors)

    def javascript_unused_exports(self):
        from gate_knip import collect_knip
        return collect_knip(self)

    def pending(self, name, detail):
        return result(errors=[{'type': 'implementation-gap', 'collector': name, 'message': detail}])

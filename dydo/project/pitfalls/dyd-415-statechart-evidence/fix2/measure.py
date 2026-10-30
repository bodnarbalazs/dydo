"""Focused native measurements; invoked only inside driver.py's held reservation."""
import hashlib
import importlib.util
import atexit
import json
from pathlib import Path
import re
import shutil
import sys
import time
import xml.etree.ElementTree as ET


def extract(source, expected_hash):
    if hashlib.sha256(source).hexdigest() != expected_hash:
        raise ValueError('Pinned PowerShell source differs')
    matches = list(re.finditer(rb"Add-Type -TypeDefinition @'\r?\n(.*?)\r?\n'@", source, re.DOTALL))
    if len(matches) != 1:
        raise ValueError('Expected exactly one C# here-string')
    match = matches[0]
    return match.group(1), match.start(1), source[:match.start(1)].count(b'\n') + 1


def execute(d):
    out, work = d.EVIDENCE, d.WORK
    adapter, tests = work / 'adapter', work / 'tests'
    adapter.mkdir(exist_ok=True)
    tests.mkdir(exist_ok=True)
    source_path = d.ROOT / 'skills/engineering/statechart/scripts/windows-job.ps1'
    source = source_path.read_bytes()
    pinned = next(row['sha256'] for row in json.loads((out / 'runtime-before.json').read_text())
                  if row['path'].endswith('/windows-job.ps1'))
    code, offset, start_line = extract(source, pinned)
    try:
        extract(source + b' ', pinned)
    except ValueError as error:
        d.save('extraction-negative.json', dict(rejected=True, error=str(error), change='append one space to in-memory copy'))
    else:
        raise AssertionError('Source mismatch was accepted')
    generated = adapter / 'StatechartJob.cs'
    generated.write_bytes(code)
    (out / 'StatechartJob.cs.exact').write_bytes(code)
    if generated.read_bytes() != source[offset:offset + len(code)]:
        raise ValueError('Generated source differs from original substring')
    d.save('extraction.json', dict(source=source_path.relative_to(d.ROOT).as_posix(), sourceSha256=pinned,
                                 sourceBytes=len(source), excerptByteOffset=offset, excerptBytes=len(code),
                                 sourceStartLine=start_line, sourceLineForGeneratedLine='generatedLine + sourceStartLine - 1',
                                 excerptSha256=d.digest(generated), generatedSha256=d.digest(generated),
                                 newline='CRLF' if b'\r\n' in code else 'LF', terminalNewline=code.endswith(b'\n'),
                                 semantics='PowerShell here-string excludes opening and closing delimiter newlines'))
    for name, folder in [('Adapter.csproj', adapter), ('Adapter.Tests.csproj', tests), ('AdapterTests.cs', tests)]:
        shutil.copy2(out / (name + '.template'), folder / name)
    d.run('adapter-build', ['dotnet', 'build', adapter / 'Adapter.csproj', '-c', 'Debug', '-m:1',
                           '-p:UseSharedCompilation=false', '-p:NuGetAudit=false',
                           '-p:ErrorLog=' + str(out / 'adapter-diagnostics.sarif')], allowed=(0, 1))
    d.run('tests-build', ['dotnet', 'build', tests / 'Adapter.Tests.csproj', '-c', 'Debug', '-m:1', '--no-incremental',
                         '-p:UseSharedCompilation=false', '-p:NuGetAudit=false'])
    d.run('compiler-version', ['dotnet', 'msbuild', adapter / 'Adapter.csproj', '-getProperty:NETCoreSdkVersion,TargetFramework,LangVersion,Optimize,DebugType,Nullable,TreatWarningsAsErrors,EnforceCodeStyleInBuild'])
    metrics = work / 'metrics/bin/Release/net10.0/GateMetrics.dll'
    project = adapter / 'Adapter.csproj'
    assembly = tests / 'bin/Debug/net10.0/Adapter.dll'
    d.run('roslyn-project', ['dotnet', metrics, '--project', project, '--root', work])
    d.run('roslyn-tests', ['dotnet', metrics, '--project', tests / 'Adapter.Tests.csproj', '--root', work])
    d.run('roslyn-syntax', ['dotnet', metrics, '--syntax'], stdin=code)
    d.run('native-before', ['dotnet', metrics, '--assembly', assembly, '--root', work, '--project', project])
    for folder, name in [(adapter, 'adapter'), (tests, 'tests'), (work / 'metrics', 'metrics')]:
        shutil.copy2(folder / 'packages.lock.json', out / (name + '-packages.lock.json'))
    discovered = d.run('native-discovery', ['dotnet', 'test', tests / 'Adapter.Tests.csproj', '-c', 'Debug',
                                           '--no-build', '--no-restore', '--list-tests']).decode('utf-8-sig')
    expected = ['ClosingLastJobHandleKillsOwnedProcess', 'TerminationUsesRequestedExitCodeAndClosedHandleIsInvalid',
                'InvalidPidThrowsAndClosesEveryAllocatedJob',
                'ExitedOwnedProcessRejectsAssignmentAndClosesJobAndProcessHandles',
                'InvalidPidExceptionPreservesNativeErrorCode', 'AssignmentExceptionPreservesNativeErrorCode']
    assert all(discovered.count('AdapterTests.' + name) == 1 for name in expected)
    d.save('native-selection.json', dict(count=len(expected), expected=expected, fullPlatformSuite=False))
    d.run('baseline-tests', ['dotnet', 'test', tests / 'Adapter.Tests.csproj', '-c', 'Debug',
                            '--no-build', '--no-restore', '--logger', 'trx;LogFileName=baseline.trx',
                            '--results-directory', out / 'baseline-results', '--',
                            'RunConfiguration.TreatNoTestsAsError=true', 'RunConfiguration.MaxCpuCount=1'], allowed=(0, 1))
    instrument(d)
    assert generated.read_bytes() == code


def instrument(d):
    out, work = d.EVIDENCE, d.WORK
    tests = work / 'tests'
    project = work / 'adapter/Adapter.csproj'
    metrics = work / 'metrics/bin/Release/net10.0/GateMetrics.dll'
    assembly = tests / 'bin/Debug/net10.0/Adapter.dll'
    before = json.loads((out / 'native-before.stdout').read_text())
    assert d.digest(assembly) == before['sha256'], 'Resume requires the exact uninstrumented baseline assembly'
    for name, folder in [('Adapter.csproj', work / 'adapter'), ('Adapter.Tests.csproj', tests), ('AdapterTests.cs', tests)]:
        assert (folder / name).read_bytes() == (out / (name + '.template')).read_bytes(), 'Baseline template mismatch: ' + name
    altcover = work / 'tools/altcover.exe'
    binary = tests / 'bin/Debug/net10.0'
    saved = work / ('altcover-saved-' + str(time.time_ns()))
    campaign = out / ('native-campaign-' + str(time.time_ns()))
    campaign.mkdir()
    d.save('native-campaign.json', dict(directory=campaign.name, freshCounters=True))
    d.run('altcover-prepare', [altcover, '--inputDirectory=' + str(binary), '--inplace',
                             '--outputDirectory=' + str(saved),
                             '--report=' + str(campaign / 'template.opencover.xml'), '--reportFormat=OpenCover',
                             '--eager', '--localSource', '--visibleBranches', '--assemblyFilter=^(?!Adapter$).*'])
    d.run('native-instrumented', ['dotnet', metrics, '--assembly', assembly, '--root', work, '--project', project])
    before = json.loads((out / 'native-before.stdout').read_text())
    after = json.loads((out / 'native-instrumented.stdout').read_text())
    assert before['sha256'] != after['sha256'], 'Assembly was not instrumented'
    spec = importlib.util.spec_from_file_location('native_campaign', d.ROOT / 'DynaDocs.Tests/coverage/csharp_coverage.py')
    campaign = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(campaign)
    assert campaign._same_instrumented_map(before, after), 'Native campaign identity check failed'
    d.save('instrumentation-audit.json', dict(instrumented=True, moduleIdUnchanged=True, pdbUnchanged=True,
                                            sourcePointsUnchanged=True, documentsUnchanged=True,
                                            checker='DynaDocs.Tests/coverage/csharp_coverage.py::_same_instrumented_map'))
    d.run('native-tests', [altcover, 'runner', '--recorderDirectory=' + str(binary),
                          '--workingDirectory=' + str(work), '--executable=dotnet',
                          '--outputFile=' + str(out / 'coverage.opencover.xml'), '--summary=N', '--',
                          'test', tests / 'Adapter.Tests.csproj', '-c', 'Debug', '--no-build', '--no-restore',
                          '--logger', 'trx;LogFileName=adapter.trx', '--results-directory', out / 'test-results',
                          '--', 'RunConfiguration.TreatNoTestsAsError=true', 'RunConfiguration.MaxCpuCount=1'], allowed=(0, 1))
    # If earlier cases warmed the recorder, retain a fresh-host check of the observed cold failure.
    result_namespace = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    results = ET.parse(out / 'test-results/adapter.trx').findall('.//t:UnitTestResult', result_namespace)
    if all(result.attrib['outcome'] == 'Passed' for result in results):
        d.run('cold-invalid-pid', [altcover, 'runner', '--recorderDirectory=' + str(binary),
                                  '--workingDirectory=' + str(work), '--executable=dotnet',
                                  '--outputFile=' + str(out / 'cold.opencover.xml'), '--summary=N', '--',
                                  'test', tests / 'Adapter.Tests.csproj', '-c', 'Debug', '--no-build', '--no-restore',
                                  '--filter', 'FullyQualifiedName=AdapterTests.InvalidPidExceptionPreservesNativeErrorCode',
                                  '--logger', 'trx;LogFileName=cold.trx', '--results-directory', out / 'cold-results',
                                  '--', 'RunConfiguration.TreatNoTestsAsError=true', 'RunConfiguration.MaxCpuCount=1'], allowed=(0, 1))
    analyze(d)


def analyze(d):
    out, work = d.EVIDENCE, d.WORK
    generated = work / 'adapter/StatechartJob.cs'
    extraction = json.loads((out / 'extraction.json').read_text())
    assert d.digest(generated) == extraction['excerptSha256']
    before = json.loads((out / 'native-before.stdout').read_text())
    summary_failure = None
    try:
        summarize(d, before, generated.read_bytes(), extraction['sourceStartLine'])
    except (AssertionError, ValueError, KeyError, OSError) as error:
        summary_failure = dict(type=type(error).__name__, message=str(error))
        d.save('native-join-unavailable.json', summary_failure)
    module = work / 'PSScriptAnalyzer/1.24.0/PSScriptAnalyzer.psd1'
    d.run('powershell-static', ['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
                               '-File', out / 'powershell-static.ps1', '-Root', d.ROOT, '-ModulePath', module,
                               '-OutputPath', out / 'powershell-static.json'], allowed=(0, 1))
    d.run('original-addtype-failures', ['powershell.exe', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
                                      '-File', out / 'original-addtype-failures.ps1', '-SourcePath',
                                      d.ROOT / 'skills/engineering/statechart/scripts/windows-job.ps1',
                                      '-OutputPath', out / 'original-addtype-failures.json'], allowed=(0, 1), stdin=b'')
    tools = Path('C:/Users/User/AppData/Local/Temp/dyd415-fix1/assurance/node_modules')
    clone_sources = work / 'clone-sources'
    clone_sources.mkdir(exist_ok=True)
    for name in ['statechart.ps1', 'windows-job.ps1']:
        shutil.copy2(d.ROOT / 'skills/engineering/statechart/scripts' / name, clone_sources / name)
    for name in ['powershell-static.ps1', 'original-addtype-failures.ps1']:
        shutil.copy2(out / name, clone_sources / name)
    shutil.copy2(generated, clone_sources / generated.name)
    shutil.copy2(out / 'AdapterTests.cs.template', clone_sources / 'AdapterTests.cs')
    clone_binary = tools / 'jscpd-windows-x64-msvc/bin/jscpd.exe'
    d.run('clones', [clone_binary, '--absolute', '--format', 'powershell,csharp',
                     '--no-gitignore', '--min-lines', '15', '--min-tokens', '100', '--max-lines', '2147483647',
                     '--max-size', '1000000', '--mode', 'strict', '--reporters', 'json', '--output', out / 'clones',
                     '--silent', '--no-tips', clone_sources])
    d.save('clone-tool.json', dict(version=json.loads((tools / 'jscpd/package.json').read_text())['version'],
                                 packageSha256=d.digest(tools / 'jscpd/package.json'), binarySha256=d.digest(clone_binary)))
    controls = work / 'clone-controls'
    controls.mkdir(exist_ok=True)
    for extension, content in {
        'ps1': '\n'.join(f'Write-Output ($value + {i} + 100 + 200)' for i in range(25)),
        'cs': 'class Control { void Execute() {\n' + '\n'.join(f'System.Console.WriteLine(100 + 200 + {i});' for i in range(25)) + '\n} }'
    }.items():
        for suffix in ['a', 'b']:
            (controls / (suffix + '.' + extension)).write_text(content, encoding='utf-8')
    d.run('clone-positive-controls', [clone_binary, '--format', 'powershell,csharp', '--no-gitignore',
                                    '--min-lines', '15', '--min-tokens', '100', '--mode', 'strict',
                                    '--reporters', 'json', '--output', out / 'clone-controls', '--silent', '--no-tips', controls], allowed=(0, 1))
    actual_clones = json.loads((out / 'clones/jscpd-report.json').read_text())
    control_clones = json.loads((out / 'clone-controls/jscpd-report.json').read_text())
    clone_inputs = {language: [dict(path=p.name, sha256=d.digest(p), physicalLines=len(p.read_text().splitlines()),
                                    eligibleByLineFloor=len(p.read_text().splitlines()) >= 15)
                               for p in sorted(clone_sources.glob(pattern))]
                    for language, pattern in [('powershell', '*.ps1'), ('csharp', '*.cs')]}
    d.save('clone-audit.json', {language: dict(
        inputs=clone_inputs[language],
        actual=actual_clones['statistics']['formats'].get(language),
        positiveControl=control_clones['statistics']['formats'].get(language),
        supported=actual_clones['statistics']['formats'].get(language, {}).get('sources') == sum(row['eligibleByLineFloor'] for row in clone_inputs[language])
                  and control_clones['statistics']['formats'].get(language, {}).get('clones', 0) > 0)
        for language in ['powershell', 'csharp']})
    ps = json.loads((out / 'powershell-static.json').read_text(encoding='utf-8-sig'))
    test_facts = json.loads((out / 'roslyn-tests.stdout').read_text(encoding='utf-8-sig'))
    test_methods = [method for file in test_facts['files'] if file['path'] == 'tests/AdapterTests.cs' for method in file['methods']]
    diagnostics = json.loads((out / 'adapter-diagnostics.sarif').read_text(encoding='utf-8-sig'))['runs'][0]['results']
    d.save('capability-ledger.json', dict(
        runtimeCandidate=d.PIN,
        nativeMetrics=json.loads((out / 'native-summary.json').read_text()) if summary_failure is None else summary_failure,
        csharpDiagnostics={level: sum(row['level'] == level for row in diagnostics) for level in ['note', 'warning', 'error']},
        csharpTestStatic=dict(callables=len(test_methods), maxCognitive=max(row['cognitive'] for row in test_methods),
                              maxParameters=max(row['parameters'] for row in test_methods if not row['constructor']),
                              namespaceEdges=test_facts['namespace_edges'], authoredFile='tests/AdapterTests.cs',
                              otherCompileInputs=[row['path'] for row in test_facts['files'] if row['path'] != 'tests/AdapterTests.cs']),
        powershellStatic=dict(analyzer=ps['analyzer'], runtimeFiles=[dict(path=row['path'], diagnostics=row['diagnostics'],
                                parseErrors=row['parseErrors'], parameters=row['parameters'], functions=row['functionCount'])
                                for row in ps['files']], harness=ps['harness'],
                                deadCodeEvidence='PSScriptAnalyzer default rules plus bounded native AST variable/assignment inventory; not a general dynamic-PowerShell reachability analyzer'),
        clones=json.loads((out / 'clone-audit.json').read_text()),
        unavailable=['PowerShell executable-line coverage', 'PowerShell branch coverage', 'PowerShell cyclomatic complexity',
                     'PowerShell cognitive complexity', 'PowerShell HCRAP', 'changed-code mutation'],
        wholeTicketAcceptance=False))
    d.run('docs', ['dydo.exe', 'check'], d.ROOT)
    d.run('historical-proof-unchanged', ['git', 'diff', '--exit-code', d.PIN, '--',
                                      'dydo/project/pitfalls/dyd-415-statechart-evidence/fix1'], d.ROOT)
    producer_sources = [p for p in (work / 'metrics').glob('*') if p.is_file()]
    producer_sources += [work / 'Directory.Build.props', work / '.editorconfig', work / 'DynaDocs.Tests/coverage/SonarLint.xml']
    d.save('metric-tool-sources.json', [dict(path=p.relative_to(work).as_posix(), sha256=d.digest(p)) for p in producer_sources])
    # Final runtime checks and release receipts are written by the driver before process exit.
    def manifest():
        d.save('proof-manifest.json', [dict(path=p.relative_to(out).as_posix(), bytes=p.stat().st_size, sha256=d.digest(p))
                                      for p in sorted(out.rglob('*')) if p.is_file()
                                      and p.name != 'proof-manifest.json' and '__pycache__' not in p.parts])
    atexit.register(manifest)
    after = d.runtime_identity()
    assert after == json.loads((out / 'runtime-before.json').read_text())
    d.save('runtime-after.json', after)
    names = ['native-tests', 'cold-invalid-pid', 'powershell-static', 'original-addtype-failures', 'clones', 'clone-positive-controls']
    exits = {name: json.loads((out / (name + '.command.json')).read_text())['exit']
             for name in names if (out / (name + '.command.json')).exists()}
    d.save('scoped-command-results.json', dict(commands=exits,
                                              nativeJoinFailure=summary_failure,
                                              positiveCloneControlIsExpectedFinding=True,
                                              actualCloneFindings=actual_clones['statistics']['total']['clones'],
                                              wholeTicketAcceptance=False,
                                              powershellLineBranchComplexityHcrap='UNAVAILABLE',
                                              changedCodeMutation='UNAVAILABLE'))
    if summary_failure is not None:
        raise SystemExit(2)
    if any(code for name, code in exits.items() if name != 'clone-positive-controls') or actual_clones['statistics']['total']['clones']:
        raise SystemExit(1)  # Complete runnable measurements, release locks, then propagate the failed assertion.


def summarize(d, native, code, start_line):
    sys.path.insert(0, str(d.ROOT / 'DynaDocs.Tests/coverage'))
    import csharp_join
    report = (d.EVIDENCE / 'coverage.opencover.xml').read_text(encoding='utf-8-sig')
    modules = [module for module in ET.fromstring(report).findall('./Modules/Module')
               if module.findtext('ModuleName') == native['assembly_name']]
    assert len(modules) == 1
    reported = Path(modules[0].findtext('ModulePath')).resolve()
    assert reported.is_relative_to(d.WORK.resolve())
    assert d.digest(reported) == native['sha256'], 'Reported saved-original alias differs from the measured assembly'
    assert d.digest(reported.with_suffix('.pdb')) == native['pdb_sha256']
    aliases = sorted({native['path'], reported.relative_to(d.WORK.resolve()).as_posix()})
    d.save('native-aliases.json', dict(aliases=aliases, originalAssemblySha256=native['sha256'], originalPdbSha256=native['pdb_sha256']))
    physical = csharp_join.coverage_methods(report, d.WORK, native, aliases)
    assert len(physical) == 1, 'Expected exactly the one managed Attach body'
    identity, row = next(iter(physical.items()))
    token = row['token']
    compiled = next(method for method in native['methods'] if method['token'] == token)
    csharp_join._validate_points(d.WORK, compiled['points'], set())
    source_hash = hashlib.sha256(code).hexdigest()
    assert all(point['checksum'] == source_hash and point['checksum_algorithm'] == 'SHA256' for point in compiled['points'])
    facts = json.loads((d.EVIDENCE / 'roslyn-project.stdout').read_text())
    source_file = next(row for row in facts['files'] if row['path'] == 'adapter/StatechartJob.cs')
    assert len(source_file['methods']) == 1
    metrics = source_file['methods'][0]
    assert metrics['member'] == 'Attach'
    assert all(csharp_join._point_owner(point, source_file['methods'])['id'] == metrics['id'] for point in compiled['points'])
    assert list(row['files']) == ['adapter/StatechartJob.cs']
    covered_file = row['files']['adapter/StatechartJob.cs']
    lines = {int(line): hits > 0 for line, hits in covered_file['Lines'].items()}
    assert all(point['line'] in lines for point in compiled['points'])
    branches = covered_file['Branches']
    assert lines and branches
    coverage = sum(lines.values()) / len(lines)
    branch_coverage = sum(branch['Hits'] > 0 for branch in branches) / len(branches)
    hcrap = metrics['policy_cc'] ** 2 * (1 - coverage) ** 3 + metrics['cognitive']
    test_runs = {}
    for name, path in [('baseline', 'baseline-results/baseline.trx'), ('instrumented', 'test-results/adapter.trx')]:
        result = ET.parse(d.EVIDENCE / path)
        namespace = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        test_runs[name] = [dict(name=test.attrib['testName'], outcome=test.attrib['outcome'],
                               output='\n'.join(test.itertext()).strip())
                           for test in result.findall('.//t:UnitTestResult', namespace)]
        assert len(test_runs[name]) == 6
    if (d.EVIDENCE / 'cold-results/cold.trx').exists():
        result = ET.parse(d.EVIDENCE / 'cold-results/cold.trx')
        test_runs['coldInstrumented'] = [dict(name=test.attrib['testName'], outcome=test.attrib['outcome'],
                                             output='\n'.join(test.itertext()).strip())
                                       for test in result.findall('.//t:UnitTestResult', namespace)]
        assert len(test_runs['coldInstrumented']) == 1
    d.save('native-test-parity.json', test_runs)
    d.save('native-joined.json', physical)
    try:
        csharp_join.coverage_methods(report, d.WORK, dict(native, sha1='0' * 40), aliases)
    except ValueError as error:
        d.save('native-join-negative.json', dict(rejected=True, error=str(error), change='wrong original assembly hash'))
    else:
        raise AssertionError('Wrong native assembly identity was accepted')
    d.save('native-summary.json', dict(module='Adapter', method=compiled['identity'], token=token,
                                     sourceSha256=source_hash, nativeModuleHash=native['sha1'],
                                     joiner='DynaDocs.Tests/coverage/csharp_join.py::coverage_methods',
                                     linesCovered=sum(lines.values()), linesTotal=len(lines), lineCoverage=coverage,
                                     branchesCovered=sum(branch['Hits'] > 0 for branch in branches), branchesTotal=len(branches),
                                     branchCoverage=branch_coverage, cognitive=metrics['cognitive'], cyclomatic=metrics['policy_cc'],
                                     parameters=metrics['parameters'], hcrap=hcrap,
                                     tests={name: dict(passed=sum(row['outcome'] == 'Passed' for row in rows),
                                                      failed=sum(row['outcome'] == 'Failed' for row in rows))
                                            for name, rows in test_runs.items()},
                                     wholeGatePass=all(row['outcome'] == 'Passed' for rows in test_runs.values() for row in rows),
                                     lines=[dict(generatedLine=line, powershellLine=line + start_line - 1, covered=hit)
                                            for line, hit in sorted(lines.items())],
                                     thresholds=dict(line=0.8, branch=0.6, cognitive=20, hcrap=20),
                                     passThresholds=coverage >= .8 and branch_coverage >= .6 and hcrap <= 20 and metrics['cognitive'] <= 20))

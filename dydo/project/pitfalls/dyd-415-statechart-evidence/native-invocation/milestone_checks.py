"""Early ownership milestone custody, baseline compile-red, docs and whitespace; no metrics."""
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

from __main__ import BASE, EVIDENCE, ROOT, TEMP, git, run, save, sha, validate_source


def archive_inputs(output):
    archives = []
    for receipt in sorted(EVIDENCE.glob('g[12]/*/inputs.json')):
        inputs = json.loads(receipt.read_bytes())
        source = Path(inputs['scratch']) / 'source'
        archive = receipt.parent / 'tested-inputs.zip'
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED) as bundle:
            for row in inputs['files']:
                original = source / row['path']
                assert sha(original) == row['sha256'], 'Retained tested source drift: ' + str(original)
                bundle.writestr(row['path'], original.read_bytes())
        archives.append(dict(receipt=receipt.relative_to(EVIDENCE).as_posix(),
            receiptSha256=sha(receipt), archive=archive.relative_to(EVIDENCE).as_posix(),
            archiveSha256=sha(archive), files=len(inputs['files'])))
    save(output / 'tested-input-archives.json', dict(archives=archives,
        copiedFromRetainedOwnedSnapshots=True, eachEntryMatchesOriginalInputReceipt=True))


def bind_sources(output):
    rows = []
    for gate, prefix in [('g1/g1-handle-string', 'Windows'),
                         ('g2/g2-input-monitor', 'Posix')]:
        receipt = EVIDENCE / gate / 'inputs.json'
        tested = {row['path']: row['sha256'] for row in json.loads(receipt.read_bytes())['files']}
        for path in sorted((ROOT / 'Services/Statechart').glob('*.cs')):
            if not (path.name.startswith(prefix) or path.name == 'StatechartProtocol.cs'):
                continue
            name = path.relative_to(ROOT).as_posix()
            assert tested[name] == sha(path), 'Native tested production source drift: ' + name
            rows.append(dict(path=name, sha256=sha(path), nativeGate=gate))
    light = EVIDENCE / 'g1/module-light-final/inputs.json'
    tested = {row['path']: row['sha256'] for row in json.loads(light.read_bytes())['files']}
    for path in sorted([*(ROOT / 'Services/Statechart').glob('*.cs'),
                        *(ROOT / 'DynaDocs.Tests/Statechart').glob('*.cs')]):
        name = path.relative_to(ROOT).as_posix()
        assert tested[name] == sha(path), 'Focused module source drift: ' + name
    trx = ET.parse(EVIDENCE / 'g1/module-light-final/test-results/statechart.trx')
    counts = trx.find('.//{*}Counters')
    assert counts is not None and counts.get('total') == '18' and counts.get('passed') == '18'
    save(output / 'native-source-binding.json', dict(productionNativeBindings=rows,
        finalLightReceipt=light.relative_to(EVIDENCE).as_posix(),
        finalLightCounts=counts.attrib, currentProductionAndTestsMatchFinalLight=True,
        fixtureRevisionsRetainedSeparately=True, finalInstalledProductProved=False))


def baseline_red(output, source, env):
    project = source / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/g1/tests/Tests.csproj'
    original = project.read_bytes()
    tree = ET.parse(project)
    for group in tree.getroot().findall('ItemGroup'):
        for item in list(group):
            if item.tag == 'Compile' and 'Services/Statechart/' in item.get('Include', ''):
                group.remove(item)
    tree.write(project, encoding='utf-8')
    try:
        code = run(output, 'without-new-modules', ['dotnet', 'test', str(project), '-c', 'Release',
            '-p:RepositoryRoot=' + str(source), '--filter', 'FullyQualifiedName~Statechart',
            '--list-tests'], source, env, timeout=300)
        lines = (output / 'without-new-modules.stdout').read_text(errors='replace').splitlines()
        red = [line for line in lines if 'error CS0234' in line or 'error CS0246' in line]
        assert code != 0 and red, 'Tests unexpectedly compile without the new ownership modules'
        save(output / 'baseline-red.json', dict(exit=code, firstFailingLine=red[0],
            method='Exact final tests without new production Compile items, in owned snapshot only',
            kind='absence-of-implementation compile rejection; not a functional test verdict',
            workingTreeProductNeverRemoved=True))
    finally:
        project.write_bytes(original)


def checks(output, scratch, source, env):
    bind_sources(output)
    archive_inputs(output)
    baseline_red(output, source, env)
    close(output, scratch, source, env)


def close(output, scratch, source, env):
    save(output / 'document-inputs.json', dict(files={str(path.relative_to(ROOT)): sha(path)
        for path in [EVIDENCE / 'verification.md',
            ROOT / 'dydo/project/pitfalls/dyd-415-statechart-verification.md']}))
    if not (output / 'native-source-binding.json').exists():
        bind_sources(output)
    binary = Path(shutil.which('dydo.exe'))
    save(output / 'docs-tool.json', dict(path=str(binary), sha256=sha(binary),
        productUnchangedDocsCommand=True, installedToolNotNewNativeProduct=True))
    for name, command in [('docs-version', [str(binary), '--version']),
                           ('docs', [str(binary), 'check'])]:
        if run(output, name, command, ROOT, env):
            raise RuntimeError(name + ' failed; inspect retained output')
    stage = ['git', 'add', '--', 'Services/Statechart', 'DynaDocs.Tests/Statechart',
        'DynaDocs.Tests/coverage/test-associations.json',
        'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation',
        'dydo/project/pitfalls/dyd-415-statechart-verification.md']
    if run(output, 'stage-owned', stage, ROOT, env):
        raise RuntimeError('Owned stage failed')
    for name, base in [('whitespace-base', BASE),
                       ('whitespace-master', '1318e93eabd500bbd45f243e4c99c3a10c2b004a')]:
        if run(output, name, ['git', 'diff', '--check', base], ROOT, env):
            raise RuntimeError(name + ' failed; inspect retained output')
    validate_source()
    baseline = json.loads((TEMP / 'dyd415-native-integration-after-20261030.json').read_bytes())
    imported = baseline['importedPaths']
    assert all(git('ls-tree', BASE, '--', path) == git('ls-tree', baseline['target'], '--', path)
               for path in imported), 'Imported master Git entry drift'
    save(output / 'custody.json', dict(head=git('rev-parse', 'HEAD'),
        orderedParents=git('show', '-s', '--format=%P', BASE).split(),
        priorWorkingFilesPreserved=len(baseline['workingHashes']),
        allPriorWorkingHashesMatch=True, oldStatechartTree=git('rev-parse',
            'HEAD:skills/engineering/statechart'),
        importedEntriesPreserved=len(imported),
        oldStatechartWorkingDelta=git('diff', BASE, '--', 'skills/engineering/statechart'),
        noNewRuntimeIntegration=True, noCoverageOrMutationExecuted=True,
        broadStaticAndFinalGatesStillOwed=True))


def seal(output, scratch, source, env):
    # All seal receipts stay in TEMP, so the commit can finish clean.
    bind_sources(output)
    closure = EVIDENCE / 'g1/milestone-byte-attributes'
    documents = json.loads((closure / 'document-inputs.json').read_bytes())['files']
    assert all(sha(ROOT / path) == digest for path, digest in documents.items())
    for name in ['docs', 'whitespace-base', 'whitespace-master']:
        assert json.loads((closure / (name + '.command.json')).read_bytes())['exit'] == 0
    assert json.loads((closure / 'teardown.json').read_bytes())['cleanupConfirmed']
    custody = json.loads((closure / 'custody.json').read_bytes())
    paths = [*(ROOT / 'Services/Statechart').glob('*.cs'),
        *(ROOT / 'DynaDocs.Tests/Statechart').glob('*.cs'),
        ROOT / 'DynaDocs.Tests/coverage/test-associations.json']
    save(EVIDENCE / 'milestone-result.json', dict(schema=1, base=BASE,
        stage='early gated native ownership; installed implementation unfinished',
        author='code-writer/model: gpt-6.1-sol/high',
        authoredProductAndTestInputs={path.relative_to(ROOT).as_posix(): sha(path) for path in paths},
        windows='g1/g1-handle-string/native-outcomes.json: 7/7; abrupt drain ambiguous-retained',
        linux='g2/g2-input-monitor/native/native-outcomes.json: 7/7, actual linux-x64',
        light='g1/module-light-final/test-results/statechart.trx: 18 passed, 0 failed, 0 skipped',
        compileRed='g1/milestone-final/baseline-red.json: expected exit1 without new modules',
        docsClosure='g1/milestone-byte-attributes: docs/whitespace exits0',
        finalCommitSealReceipts=str(output), custody=custody,
        drDelta='none at this early ownership milestone; DR062 remains owed',
        finalInstalledRuntimePayloadNpmJavaScratchDeletionProved=False,
        missingActualPlatforms=['linux-arm64', 'osx-x64', 'osx-arm64'],
        finalStaticCoverageAndV0ThroughV10StillOwed=True,
        historicalFailuresPreserved=True, dydo434='Done',
        noSubagentsPushPrDispatchOrPublication=True))
    stage = ['git', 'add', '--', 'Services/Statechart', 'DynaDocs.Tests/Statechart',
        'DynaDocs.Tests/coverage/test-associations.json',
        'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation',
        'dydo/project/pitfalls/dyd-415-statechart-verification.md']
    commands = [('stage-final', stage),
        ('whitespace-final-base', ['git', 'diff', '--check', BASE]),
        ('whitespace-final-master', ['git', 'diff', '--check',
            '1318e93eabd500bbd45f243e4c99c3a10c2b004a'])]
    for name, command in commands:
        if run(output, name, command, ROOT, env):
            raise RuntimeError(name + ' failed; no commit')
    validate_source()
    if run(output, 'commit', ['git', 'commit', '-m',
            'DYD-415 implement: establish gated native ownership'], ROOT, env):
        raise RuntimeError('Commit failed; retain staged source and exact output')
    status = git('status', '--porcelain=v1', '--untracked-files=all')
    save(output / 'committed.json', dict(commit=git('rev-parse', 'HEAD'),
        tree=git('rev-parse', 'HEAD^{tree}'), parent=BASE, status=status,
        clean=not status, docsInputsUnchanged=True,
        exactFinalWhitespaceExits=[0, 0], milestoneOnly=True))
    if status:
        raise RuntimeError('Post-commit worktree is not clean; inspect exact status')

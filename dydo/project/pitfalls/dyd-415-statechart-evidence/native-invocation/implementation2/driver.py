"""Implementation2 owned snapshots; live FIFO -> nonwaiting Heavy -> exclusive normal TEMP lock."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import stat
import zipfile

sys.dont_write_bytecode = True
ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
BASE = '86931b9c47a93fa06287391495574865d9ce1d56'
LC = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
HERE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation2'
LC_HASHES = {
    'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
    'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
    'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'}
SELECTION = ('FullyQualifiedName~Statechart|FullyQualifiedName~CanonicalSkillAssertionTests|'
    'FullyQualifiedName~ScaffoldTreeTests|FullyQualifiedName~CommandSmokeTests|'
    'FullyQualifiedName~CommandDocConsistencyTests|FullyQualifiedName~HelpCommandTests')
NATIVE = 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/'
NATIVE_POINTER = '''The [finite replay archive](history/README.md) represents the eleven pinned old
primitive replay sources and their exact restoration context. Captain verified
the fidelity receipt and authorized removal of precisely those eleven paths. The
primitive recipe below is historical: restore its exact stage inputs and map its
recorded context before any separately authorized replay; the old hardcoded
wrappers do not run unchanged in arbitrary scratch. Current product work uses
`implementation2/launch.py` and `implementation2/driver.py`; archive fidelity is
neither runtime replay nor static acceptance.

'''
HISTORY_POINTER = '''The [finite native replay archive](dyd-415-statechart-evidence/native-invocation/history/README.md)
provides exact source/context maps for eleven old primitive replay paths. Captain
verified fidelity and authorized their exact removal. Historical commands
and verdicts below are unchanged; their replay requires exact restored stage inputs
and recorded context. Current product proof uses the maintained implementation2
driver, with aggregate static still incomplete.

'''
ARCHIVE_PATHS = [NATIVE + 'history/' + name for name in [
    'native-primitives-replay-86931b9c.zip',
    'native-primitives-replay-86931b9c.manifest.json', 'README.md']]
ORIGINALS = [NATIVE + name for name in ['gate-driver.py', 'launch.py', 'milestone_checks.py',
    'g1_cases.py', 'g2_cases.py', 'g1/admission-failed-driver.py', 'g1/fixture/Fixture.csproj',
    'g1/fixture/Program.cs', 'g1/tests/Tests.csproj', 'g2/Dockerfile', 'g2/linux.sh']]
IMMUTABLE = {
    ARCHIVE_PATHS[0]: '87c712127c54adb248978fa3570460be796069b09c2c944f182c00a604376114',
    ARCHIVE_PATHS[1]: 'e00ff73f95f58002797f3f1d9fd43bfbb1419eef16e7dbb153fb17d3f799e12f',
    NATIVE + 'implementation2/archival-fidelity/fidelity.json': 'c6ce1bc46e795eb6b5875aab0ec74604d261542b71a2e728612011b36b033b14',
    NATIVE + 'implementation2/archival-fidelity/source-inputs.json': '0709f7d034d2a701495c6755cd4c6ded7f8ffaa75045162104a0526582f605da',
    NATIVE + 'implementation2/request-store-longpaths/source-inputs.json': '0538facb61fe2fb2ece7f26979905b463564fe0a3d99a787e2e3f5b68b19ed63'}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path, data):
    with path.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(data, stream, indent=2)
        stream.write('\n')


def git(*arguments, cwd=ROOT):
    return git_bytes(*arguments, cwd=cwd).decode().rstrip('\r\n')


def git_bytes(*arguments, cwd=ROOT):
    return subprocess.check_output(['git', '-c', 'core.longpaths=true', *arguments], cwd=cwd,
        creationflags=subprocess.CREATE_NO_WINDOW)


def owned(path):
    prefixes = ['Services/Statechart/', 'DynaDocs.Tests/Statechart/', 'Resources/Statechart/',
        'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation2/']
    exact = ['Program.cs', 'Commands/StatechartCommand.cs', 'Commands/HelpCommand.cs',
        'DynaDocs.Tests/coverage/tests/test_statechart_invocation_driver.py',
        'DynaDocs.Tests/coverage/tests/test_statechart_invocation_launch.py',
        'DynaDocs.Tests/coverage/tests/test_statechart_invocation_prepare.py',
        'DynaDocs.Tests/coverage/tests/statechart-corpus-views.test.cjs',
        'DynaDocs.csproj', 'DynaDocs.Tests/Commands/CommandSmokeTests.cs',
        'DynaDocs.Tests/Steps/CanonicalSkillAssertionTests.cs',
        'DynaDocs.Tests/Commands/HelpCommandTests.cs', 'DynaDocs.Tests/coverage/test-associations.json',
        'dydo/reference/dydo-commands.md', 'Scaffold/dydo/reference/dydo-commands.md',
        'dydo/project/decisions/062-statechart-invocation-manages-prerequisites.md',
        'dydo/project/decisions/_decisions.md', 'dydo/project/pitfalls/dyd-415-statechart-verification.md']
    prefixes.append('skills/engineering/statechart/')
    return path in exact + ARCHIVE_PATHS + ORIGINALS + [NATIVE + 'verification.md'] or path.startswith(tuple(prefixes))


def representation(root, historical, originals_present=False):
    for path, digest in IMMUTABLE.items():
        assert sha(root / path) == digest, 'Immutable archival checkpoint changed: ' + path
    manifest = json.loads((root / ARCHIVE_PATHS[1]).read_bytes())
    assert {row['oldPath'] for row in manifest['primarySources']} == set(ORIGINALS)
    if originals_present:
        for row in manifest['primarySources']:
            assert sha(root / row['oldPath']) == row['checkoutSha256'], 'Pinned original changed before removal'
    else:
        assert all(not os.path.lexists(root / path) for path in ORIGINALS), 'Exactly11 originals must be represented, not executable source copies'
    baseline_path = NATIVE + 'implementation2/request-store-longpaths/source-inputs.json'
    baseline = {row['path']: row['sha256'] for row in json.loads((root / baseline_path).read_bytes())['files']}
    retained = set(historical) - set(ORIGINALS) - {NATIVE + 'verification.md'}
    for path in retained:
        assert sha(root / path) == baseline[path], 'Historical source drift or unexpected deletion: ' + path
    for row in manifest['retainedDependencyHashes']:
        assert sha(root / row['path']) == row['sha256'], 'Retained stage dependency changed: ' + row['path']
    verify_members(root / ARCHIVE_PATHS[0], manifest['entries'])
    return dict(originalsRepresented=11, otherHistoricalNativeFilesUnchanged=len(retained),
        immutableCheckpoints=IMMUTABLE, archiveMembers=len(manifest['entries']))


def verify_members(archive, expected):
    rows = {row['entry']: row for row in expected}
    assert len(rows) == len(expected), 'Duplicate expected archive member'
    with zipfile.ZipFile(archive) as bundle:
        infos = bundle.infolist()
        assert len(infos) == len(rows) and {info.filename for info in infos} == set(rows), 'Archive member closure mismatch'
        seen = set()
        for info in infos:
            fidelity_name(info.filename)
            assert info.filename.casefold() not in seen, 'Duplicate/case-colliding archive member'
            seen.add(info.filename.casefold())
            assert stat.S_IFMT(info.external_attr >> 16) == stat.S_IFREG, 'Archive member is not a regular file'
            data = bundle.read(info)
            row = rows[info.filename]
            assert len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256'], 'Archive member identity mismatch'


def verify_sources(source, rows):
    expected = {row['path']: row['sha256'] for row in rows}
    actual = {path.relative_to(source).as_posix(): sha(path) for path in source.rglob('*') if path.is_file()}
    assert len(expected) == len(rows) and actual == expected, 'Missing/changed/extra snapshot input'


def validate(originals_present=False):
    assert git('rev-parse', 'HEAD') == BASE
    assert git('branch', '--show-current') == 'DYD-415-statechart'
    assert Path(git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    changed = [line[3:].replace('\\', '/') for line in git('status', '--porcelain=v1', '--untracked-files=all').splitlines()]
    assert all(owned(path) for path in changed), changed
    assert all(sha(LC / path) == digest for path, digest in LC_HASHES.items())
    immutable = 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation'
    historical = git('ls-tree', '-r', '--name-only', BASE, '--', immutable).splitlines()
    representation(ROOT, historical, originals_present)
    for path, pointer in [(NATIVE + 'verification.md', NATIVE_POINTER),
            ('dydo/project/pitfalls/dyd-415-statechart-verification.md', HISTORY_POINTER)]:
        current = (ROOT / path).read_bytes()
        block = pointer.encode()
        assert current.count(block) == 1, 'Exactly one authorized archive pointer required: ' + path
        assert current.replace(block, b'', 1) == git_bytes('show', BASE + ':' + path), 'Historical navigation bytes changed beyond the exact additive pointer'


def remove_originals(output):
    patterns = (r'gate-driver\.py|milestone_checks\.py|g1_cases\.py|g2_cases\.py|admission-failed-driver\.py|'
        r'native-invocation/launch\.py|native-invocation/g1/fixture|native-invocation/g1/tests|native-invocation/g2/(linux\.sh|Dockerfile)')
    arguments = ['rg', '-n', '--glob', '!**/coverage/results/**', patterns, 'Commands', 'Services',
        'Resources', 'skills', 'npm', 'Program.cs', 'DynaDocs.csproj', '.github', 'DynaDocs.Tests']
    run(output, 'current-consumers', arguments, ROOT, os.environ, expected=1)
    assert not (output / 'current-consumers.stdout').read_bytes(), 'Current consumer blocks removal'
    _, _, _, primary = fidelity_inputs()
    save(output / 'source-inputs.json', dict(base=BASE, driverSha256=sha(Path(__file__)),
        originals=primary, immutable=IMMUTABLE, removalPinSha256=sha(TEMP / 'dyd415-archival-removal-captain-pin-20261030.md')))
    for path in ORIGINALS:
        target = ROOT / path
        assert target.resolve().is_relative_to(ROOT.resolve()) and not target.is_symlink()
        target.unlink()
    validate()
    save(output / 'removal.json', dict(removed=ORIGINALS, noOtherRemovalAuthorized=True,
        representation=representation(ROOT, git('ls-tree', '-r', '--name-only', BASE, '--', NATIVE).splitlines()),
        noCurrentConsumer=True, historicalReplay=False, staticOrCoverageRetry=False))


def run(output, name, arguments, cwd, env, timeout=600, expected=0):
    invoke(output, name, arguments, cwd, env, dict(timeout=timeout, expected=expected, input=None))


def invoke(output, name, arguments, cwd, env, settings):
    print(name + ': starting', flush=True)
    started = time.monotonic()
    with (output / (name + '.stdout')).open('xb') as stdout, (output / (name + '.stderr')).open('xb') as stderr:
        child = subprocess.run(list(map(str, arguments)), cwd=cwd, env=env, stdout=stdout,
            stderr=stderr, timeout=settings['timeout'], creationflags=subprocess.CREATE_NO_WINDOW, input=settings['input'])
    save(output / (name + '.command.json'), dict(argv=list(map(str, arguments)), cwd=str(cwd),
        exit=child.returncode, expected=settings['expected'], seconds=time.monotonic() - started,
        ticket=env['LC_SUITE_SLOT_TICKET'], hidden=True))
    print(name + ': ' + str(child.returncode), flush=True)
    if child.returncode != settings['expected']:
        raise RuntimeError(name + ' failed; inspect retained streams before any retry')


def snapshot(output):
    scratch = Path(tempfile.mkdtemp(prefix='dyd415-implementation2-', dir=TEMP))
    source = scratch / 'source'
    paths = sorted(set(git('ls-files').splitlines() +
        git('ls-files', '--others', '--exclude-standard').splitlines()))
    rows = []
    for name in paths:
        path = ROOT / name
        if not path.is_file():
            continue
        target = source / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
        rows.append(dict(path=name, sha256=sha(path)))
    verify_sources(source, rows)
    save(output / 'source-inputs.json', dict(base=BASE, driverSha256=sha(Path(__file__)),
        scratch=str(scratch), files=rows, selection=SELECTION))
    git('init', cwd=source)
    git('remote', 'add', 'origin', git('remote', 'get-url', 'origin'), cwd=source)
    git('add', '-f', '-A', cwd=source)
    git('-c', 'user.name=DYD415 source proof', '-c', 'user.email=proof@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '-m', 'Owned implementation2 source snapshot', cwd=source)
    env = {key: value for key, value in os.environ.items() if not key.startswith('DYDO_')}
    env.update(DOTNET_CLI_HOME=str(scratch / 'dotnet-home'), DOTNET_NOLOGO='1',
        DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
        DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1', UseSharedCompilation='false',
        NUGET_PACKAGES=str(scratch / 'nuget'), NUGET_HTTP_CACHE_PATH=str(scratch / 'nuget-http'),
        NUGET_PLUGINS_CACHE_PATH=str(scratch / 'nuget-plugins'), PYTHONDONTWRITEBYTECODE='1',
        TEMP=str(scratch / 'temp'), TMP=str(scratch / 'temp'), TMPDIR=str(scratch / 'temp'))
    (scratch / 'temp').mkdir()
    return scratch, source, env


def product(output, scratch, source, env):
    run(output, 'sdk', ['dotnet', '--version'], source, env)
    binary = scratch / 'publish/dydo.exe'
    run(output, 'native-publish', ['dotnet', 'publish', 'DynaDocs.csproj', '-c', 'Release',
        '-r', 'win-x64', '--self-contained', '--warnaserror', '-o', binary.parent], source, env)
    identity = dict(nativeAot=True, rid='win-x64', sha256=sha(binary),
        sourceInputs=str(output / 'source-inputs.json'), sourceInputsSha256=sha(output / 'source-inputs.json'))
    save(Path(str(binary) + '.proof.json'), identity)
    save(output / 'binary.json', dict(path=str(binary), **identity))
    env['STATECHART_TEST_BINARY'] = str(binary)
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', SELECTION]
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    discovery = (output / 'discovery.stdout').read_text(errors='replace')
    for name in ['StatechartAssetsTests', 'StatechartScratchTests', 'StatechartLifecycleTests',
                 'StatechartPrerequisitesTests', 'StatechartCommandTests', 'StatechartPathsTests',
                 'CanonicalSkillAssertionTests', 'ScaffoldTreeTests', 'CommandSmokeTests',
                 'CommandDocConsistencyTests', 'HelpCommandTests']:
        assert name in discovery, 'Missing native discovery: ' + name
    known_shared = (HERE / 'resource-shared-attribution/baseline-identity.json').is_file()
    run(output, 'light', [*base, '--no-restore', '--logger', 'trx;LogFileName=implementation2.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env,
        expected=1 if known_shared else 0)
    if known_shared:
        import xml.etree.ElementTree as ET
        trx = ET.parse(output / 'test-results/implementation2.trx')
        failed = [row.get('testName') for row in trx.findall('.//{*}UnitTestResult') if row.get('outcome') != 'Passed']
        expected = ['DynaDocs.Tests.Steps.CanonicalSkillAssertionTests.CanonicalSkillTree_SortsEverySkillExactlyOnceByKind',
            'DynaDocs.Tests.Steps.CanonicalSkillAssertionTests.SkillsReadme_CategoryCountsMatchTheRealDirectoriesAndSumTo40']
        assert sorted(failed) == sorted(expected), failed
        save(output / 'light-attributed.json', dict(actualSuiteExit=1, overallLight='FAIL; inherited catalogue blocker',
            onlyFailures=failed, inScopeCasesAllPassed=True, baseline='resource-shared-attribution/baseline-identity.json'))


def static(output, scratch, source, env):
    base_python = Path('C:/Users/User/AppData/Local/Programs/Python/Python312/python.exe')
    run(output, 'python-base-version', [base_python, '--version'], source, env)
    assert (output / 'python-base-version.stdout').read_text().strip() == 'Python 3.12.10'
    tool_root = source / 'dydo/_system/.local/static-gates/python'
    run(output, 'python-venv', [base_python, '-m', 'venv', tool_root], source, env)
    python = tool_root / 'Scripts/python.exe'
    run(output, 'python-tools', [python, '-m', 'pip', 'install', '--disable-pip-version-check',
        '--cache-dir', scratch / 'pip-cache', '-r', source / 'DynaDocs.Tests/coverage/requirements.lock'], source, env)
    node_root = Path('C:/Users/User/AppData/Roaming/fnm/node-versions/v22.13.0/installation')
    env['PATH'] = str(node_root) + os.pathsep + env['PATH']
    run(output, 'node-tool-version', [node_root / 'node.exe', '--version'], source, env)
    assert (output / 'node-tool-version.stdout').read_text().strip() == 'v22.13.0'
    run(output, 'node-tools', [node_root / 'node.exe', node_root / 'node_modules/npm/bin/npm-cli.js',
        'ci', '--no-audit', '--no-fund', '--cache', scratch / 'npm-cache'], source / 'DynaDocs.Tests/coverage', env)
    run(output, 'static', [python, 'DynaDocs.Tests/coverage/gap_check.py', 'gate', 'static',
        '--stack', 'dotnet'], source, env, timeout=900)


def catalogue(output, scratch, source, env):
    selection = ('FullyQualifiedName~CanonicalSkillTree_SortsEverySkillExactlyOnceByKind|'
        'FullyQualifiedName~SkillsReadme_CategoryCountsMatchTheRealDirectoriesAndSumTo41')
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', selection]
    save(output / 'selection.json', dict(filter=selection, expectedTests=2,
        purpose='Captain-authorized catalogue expectations only; no native publish or lifecycle suite'))
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    discovery = (output / 'discovery.stdout').read_text(errors='replace')
    for name in ['CanonicalSkillTree_SortsEverySkillExactlyOnceByKind',
                 'SkillsReadme_CategoryCountsMatchTheRealDirectoriesAndSumTo41']:
        assert name in discovery, 'Missing commissioned test: ' + name
    run(output, 'catalogue', [*base, '--no-restore', '--logger', 'trx;LogFileName=catalogue.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env)
    import xml.etree.ElementTree as ET
    trx = ET.parse(output / 'test-results/catalogue.trx')
    results = trx.findall('.//{*}UnitTestResult')
    assert len(results) == 2 and all(row.get('outcome') == 'Passed' for row in results)
    save(output / 'catalogue-result.json', dict(exit=0, passed=2, failed=0, skipped=0,
        priorLight='product-resource-fixed remains historical FAIL91/2'))


def attribution(output, scratch, source, env):
    old = json.loads((HERE / 'product-sourcelink/source-inputs.json').read_bytes())
    tested = Path(old['scratch']) / 'source'
    run(output, 'embedded-resource-metadata', ['dotnet', 'msbuild', 'DynaDocs.csproj',
        '-target:PrepareResourceNames', '-getItem:EmbeddedResource'], tested, env)
    archive = scratch / 'baseline.zip'
    git('archive', BASE, '--format=zip', '--output=' + str(archive))
    baseline = scratch / 'baseline'
    shutil.unpack_archive(archive, baseline)
    git('init', cwd=baseline)
    git('remote', 'add', 'origin', git('remote', 'get-url', 'origin'), cwd=baseline)
    git('add', '-f', '-A', cwd=baseline)
    git('-c', 'user.name=DYD415 baseline proof', '-c', 'user.email=proof@example.invalid',
        '-c', 'commit.gpgsign=false', 'commit', '-m', 'Exact base Git archive for bounded attribution', cwd=baseline)
    selection = ('FullyQualifiedName~CanonicalSkillAssertionTests.CanonicalSkillTree_SortsEverySkillExactlyOnceByKind|'
        'FullyQualifiedName~CanonicalSkillAssertionTests.SkillsReadme_CategoryCountsMatchTheRealDirectoriesAndSumTo40')
    run(output, 'baseline-shared', ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release',
        '--filter', selection, '--logger', 'trx;LogFileName=base.trx', '--results-directory',
        output / 'baseline-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], baseline, env, expected=1)
    save(output / 'baseline-identity.json', dict(base=BASE, archiveSha256=sha(archive),
        selection=selection, sourceTree='exact Git archive, no candidate product files',
        noRuntimeNativeOrPlatformCasesRepeated=True))


def request(output, scratch, source, env):
    selection = ('FullyQualifiedName~StatechartRequestTests|FullyQualifiedName~StatechartRequestsTests|'
        'FullyQualifiedName~StatechartDownloadsTests|FullyQualifiedName~StatechartHomeTests')
    save(output / 'selection.json', dict(filter=selection,
        purpose='Request capture/store/replay identity, configured roots and locked-download bounds; no native/platform suite'))
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', selection]
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    discovery = (output / 'discovery.stdout').read_text(errors='replace')
    for name in ['StatechartRequestTests', 'StatechartRequestsTests', 'StatechartDownloadsTests', 'StatechartHomeTests']:
        assert name in discovery, 'Missing focused class: ' + name
    run(output, 'request', [*base, '--no-restore', '--logger', 'trx;LogFileName=request.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env)


def focused(output, scratch, source, env):
    import unittest
    sys.path.insert(0, str(source / 'DynaDocs.Tests/coverage'))
    from gate_adapter import _ordinary_discovery
    relative = 'DynaDocs.Tests/coverage/tests/'
    test_paths = [relative + 'test_statechart_invocation_' + name + '.py' for name in ['driver', 'launch', 'prepare']]
    corpus = relative + 'statechart-corpus-views.test.cjs'
    discovery = _ordinary_discovery(source, test_paths + [corpus])
    assert {row['file'] for row in discovery} == set(test_paths + [corpus]), 'Missing adopted native test recognition'
    suite = unittest.TestLoader().discover(str(source / relative), pattern='test_statechart_invocation_*.py')
    assert suite.countTestCases() == 13, 'Unexpected or zero unittest selection'
    save(output / 'native-discovery.json', dict(recognized=discovery, unittestCases=suite.countTestCases(),
        purpose='Actual existing unittest/node:test recognition, not aggregate static or coverage'))
    run(output, 'driver-tests', [sys.executable, '-B', '-m', 'unittest', 'discover', '-s',
        source / relative, '-p', 'test_statechart_invocation_*.py', '-v'], source, env)
    node = Path('C:/Users/User/AppData/Roaming/fnm/node-versions/v22.13.0/installation/node.exe')
    run(output, 'node-version', [node, '--version'], source, env)
    assert (output / 'node-version.stdout').read_text().strip() == 'v22.13.0'
    run(output, 'corpus-views', [node, '--test', source / corpus], source, env)
    save(output / 'custody.json', representation(ROOT,
        git('ls-tree', '-r', '--name-only', BASE, '--', NATIVE).splitlines()))


def archive_tests(output, scratch, source, env, red=False):
    selection = 'FullyQualifiedName~StatechartArchiveTests'
    if red:
        selection += '.LockedZipExtractsExactBytesToFreshOwnedStagingWithoutTouchingArchive'
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', selection]
    save(output / 'selection.json', dict(filter=selection, behavioralRed=red,
        purpose='Bounded managed prerequisite archive extraction only; no native publish/platform/coverage run'))
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    assert 'LockedZipExtractsExactBytesToFreshOwnedStagingWithoutTouchingArchive' in (output / 'discovery.stdout').read_text(errors='replace')
    run(output, 'archives', [*base, '--no-restore', '--logger', 'trx;LogFileName=archives.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env, expected=1 if red else 0)
    import xml.etree.ElementTree as ET
    results = ET.parse(output / 'test-results/archives.trx').findall('.//{*}UnitTestResult')
    assert results
    if red:
        assert len(results) == 1 and results[0].get('outcome') == 'Failed'
        assert 'Statechart locked archive extraction is not implemented.' in ET.tostring(results[0], encoding='unicode')
    else:
        assert all(row.get('outcome') == 'Passed' for row in results)
    save(output / 'archives-result.json', dict(actualSuiteExit=1 if red else 0,
        executed=len(results), behavioralRedConfirmed=red, passed=0 if red else len(results)))


def public_red(output, scratch, source, env):
    baseline = json.loads((HERE / 'product-resource-fixed/binary.json').read_bytes())
    binary = Path(baseline['path'])
    assert sha(binary) == baseline['sha256'] == '0324a5f5c1e2b725a4856b02b60d4391c8fa4f157f9649176b9f6a5f0624db15'
    caller = scratch / 'caller'
    caller.mkdir()
    (caller / 'Model.tla').write_text('---- MODULE Model ----\nVARIABLE x\nInit == x = 0\nNext == x\' = x\n====\n')
    (caller / 'Model.cfg').write_text('INIT Init\nNEXT Next\nCHECK_DEADLOCK FALSE\n')
    env.update(DYDO_STATECHART_ASSET_ROOT=str(scratch / 'assets'), DYDO_STATECHART_STATE_ROOT=str(scratch / 'state'),
        DYDO_STATECHART_DEPENDENCY_ROOT=str(scratch / 'deps'))
    run(output, 'public-red', [binary, 'statechart', 'Model.tla'], caller, env, expected=2)
    assert not (scratch / 'deps').exists() and not (caller / 'Model-chart').exists()
    save(output / 'behavioral-red.json', dict(expectedPublicExit=3, actualOldBinaryExit=2,
        baseline=baseline, observed='Old installed command rejects the valid model request; new required saved plan is absent',
        noCompileRejection=True))


def public_flow(output, scratch, source, env, corrected=False, probe=False):
    binary = scratch / 'publish/dydo.exe'
    run(output, 'native-publish', ['dotnet', 'publish', 'DynaDocs.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', '--warnaserror', '-o', binary.parent], source, env)
    identity = dict(path=str(binary), sha256=sha(binary), rid='win-x64', nativeAot=True,
        sourceInputsSha256=sha(output / 'source-inputs.json'))
    save(output / 'binary.json', identity)
    save(Path(str(binary) + '.proof.json'), identity)
    env['STATECHART_TEST_BINARY'] = str(binary)
    selection = ('FullyQualifiedName~StatechartPublicFlowTests|FullyQualifiedName~StatechartProtocolTests|'
        'FullyQualifiedName~StatechartLifecycleTests|FullyQualifiedName~CommandDocConsistencyTests')
    names = ['StatechartPublicFlowTests', 'StatechartProtocolTests', 'StatechartLifecycleTests', 'CommandDocConsistencyTests']
    if corrected:
        selection = ('FullyQualifiedName~StatechartPublicFlowTests|FullyQualifiedName~StatechartManagedTests|'
            'FullyQualifiedName~CommandDocConsistencyTests.AllCommands_AppearInHelpText')
        names = ['StatechartPublicFlowTests', 'StatechartManagedTests', 'AllCommands_AppearInHelpText']
        previous = HERE / 'public-flow-behavioral-red'
        observed = json.loads((previous / 'public-red.command.json').read_bytes())
        assert observed['exit'] == 1 and observed['expected'] == 2
        assert "Unrecognized command or argument 'Model.tla'" in (previous / 'public-red.stderr').read_text()
        save(output / 'behavioral-red-attribution.json', dict(originalHarness='FAIL: expected exit2, observed exit1',
            actualBehavior='Old installed command rejects valid model arguments and cannot produce required saved plan3',
            actualOldExit=1, requiredNewExit=3, reranOldBinary=False,
            preserved={name: sha(previous / name) for name in ['public-red.command.json', 'public-red.stdout', 'public-red.stderr', 'stop.json']}))
    if probe:
        selection = 'FullyQualifiedName~StatechartProbeTests'
        names = ['StatechartProbeTests']
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', selection]
    save(output / 'selection.json', dict(filter=selection, noFullOrCoverageCampaign=True))
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    discovery = (output / 'discovery.stdout').read_text(errors='replace')
    for name in names:
        assert name in discovery, 'Missing public-flow selection: ' + name
    run(output, 'public-tests', [*base, '--no-restore', '--logger', 'trx;LogFileName=public.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env)
    if not probe:
        focused(output, scratch, source, env)
    caller = scratch / 'model inputs árvíz'
    caller.mkdir()
    (caller / 'Model.tla').write_text('---- MODULE Model ----\nEXTENDS Naturals\nVARIABLE x\nInit == x = 0\nNext == (x < 2 /\\ x\' = x + 1) \\/ (x = 2 /\\ x\' = x)\nBound == x < 2\n====\n', encoding='utf-8')
    (caller / 'Model.cfg').write_text('INIT Init\nNEXT Next\nINVARIANT Bound\nCHECK_DEADLOCK FALSE\n')
    (caller / 'view.mjs').write_text('export const state = ({x}) => String(x);\n')
    env.update(DYDO_STATECHART_ASSET_ROOT=str(scratch / 'assets'), DYDO_STATECHART_STATE_ROOT=str(scratch / 'state'),
        DYDO_STATECHART_DEPENDENCY_ROOT=str(scratch / 'deps'))
    env.pop('JAVA', None)
    env.pop('TLA2TOOLS', None)
    env['PATH'] = ''
    run(output, 'initial-plan', [binary, 'statechart', 'Model.tla', '--view', 'view.mjs', '--out', 'chart output'], caller, env, expected=3)
    plan = json.loads((output / 'initial-plan.stdout').read_bytes())
    assert plan['state'] == 'approval-required' and Path(plan['request']).is_file()
    assert not (scratch / 'deps').exists() and not (caller / 'chart output').exists()
    save(output / 'continuation.json', dict(plan=plan, binarySha256=sha(binary), caller=str(caller), scratch=str(scratch),
        sourceInputsSha256=sha(output / 'source-inputs.json'),
        environment={name: env[name] for name in ['DYDO_STATECHART_ASSET_ROOT', 'DYDO_STATECHART_STATE_ROOT',
            'DYDO_STATECHART_DEPENDENCY_ROOT', 'TEMP', 'TMP', 'TMPDIR']},
        hostAuthorization='Not executed by build stage. Submit exact installAndContinue operation through normal tool authorization in a new admitted stage.'))


def public_resume(output, scratch, source, env):
    predecessor = HERE / 'public-flow-build3'
    continuation = json.loads((predecessor / 'continuation.json').read_bytes())
    plan = continuation['plan']
    binary = Path(plan['installAndContinue'][0])
    assert sha(binary) == continuation['binarySha256']
    assert sha(predecessor / 'source-inputs.json') == continuation['sourceInputsSha256']
    inputs = json.loads((predecessor / 'source-inputs.json').read_bytes())
    compiled = [row for row in inputs['files'] if row['path'].startswith(('Services/Statechart/',
        'Resources/Statechart/', 'skills/engineering/statechart/')) or row['path'] in
        ['Program.cs', 'Commands/StatechartCommand.cs', 'DynaDocs.csproj', 'setup-skills.mjs']]
    assert all(sha(ROOT / row['path']) == row['sha256'] for row in compiled), 'Compiled product inputs changed'
    request_path = Path(plan['request'])
    assert sha(request_path) == plan['requestDigest']
    assert request_path.resolve().is_relative_to(Path(continuation['scratch']).resolve())
    arguments = [str(binary), 'statechart', 'resume', '--request', str(request_path)]
    assert arguments == plan['installAndContinue'], 'Resume operation differs from captured plan'
    env.update(continuation['environment'])
    env['PATH'] = ''
    env.pop('JAVA', None)
    env.pop('TLA2TOOLS', None)
    save(output / 'resume-binding.json', dict(predecessor=str(predecessor),
        continuationSha256=sha(predecessor / 'continuation.json'), argv=arguments,
        compiledInputs=compiled, binarySha256=sha(binary),
        authorization='Exact task-local install-and-continue submitted through normal host tool authorization; no local consent claim'))
    caller = Path(continuation['caller'])
    run(output, 'authorized-resume', arguments, caller, env, timeout=660, expected=1)
    destination = Path(plan['output'])
    files = {path.name: sha(path) for path in destination.iterdir() if path.is_file()}
    assert {'chart.mmd', 'chart.svg', 'report.md', 'tlc.out'}.issubset(files)
    svg = (destination / 'chart.svg').read_text(encoding='utf-8')
    assert '<svg' in svg and '<style>' in svg and 'pele' in svg
    report = (destination / 'report.md').read_text(encoding='utf-8')
    chart = (destination / 'chart.mmd').read_text(encoding='utf-8')
    assert 'Bound breaks' in report and 'shortest path has 2 steps' in report
    assert 'breaks' in chart and 'bugPath' in chart and 'breaks' in svg and 'bugPath' in svg
    completion = json.loads((request_path.parent / 'completed.json').read_bytes())
    assert completion['verdict'] == 1 and completion['requestDigest'] == plan['requestDigest']
    dependencies = Path(env['DYDO_STATECHART_DEPENDENCY_ROOT'])
    before = {path.relative_to(dependencies).as_posix(): sha(path) for path in dependencies.rglob('*') if path.is_file()}
    run(output, 'completed-replay', arguments, caller, env, expected=1)
    assert files == {path.name: sha(path) for path in destination.iterdir() if path.is_file()}
    offline = dict(env, HTTP_PROXY='http://127.0.0.1:9', HTTPS_PROXY='http://127.0.0.1:9',
        ALL_PROXY='http://127.0.0.1:9', NO_PROXY='')
    run(output, 'ready-offline', [binary, 'statechart', 'Model.tla', '--view', 'view.mjs', '--out', 'ready chart'],
        caller, offline, expected=1)
    ready = caller / 'ready chart'
    assert {'chart.mmd', 'chart.svg', 'report.md', 'tlc.out'}.issubset(path.name for path in ready.iterdir())
    model = (caller / 'Model.tla').read_text(encoding='utf-8')
    corrected = model.replace('MODULE Model', 'MODULE Corrected').replace('x < 2 /\\', 'x < 1 /\\').replace('x = 2 /\\', 'x = 1 /\\')
    assert 'Bound == x < 2' in corrected and 'Bound == x < 2' in model
    (caller / 'Corrected.tla').write_text(corrected, encoding='utf-8')
    shutil.copy2(caller / 'Model.cfg', caller / 'Corrected.cfg')
    run(output, 'corrected-offline', [binary, 'statechart', 'Corrected.tla', '--view', 'view.mjs', '--out', 'corrected chart'],
        caller, offline, expected=0)
    corrected_output = caller / 'corrected chart'
    corrected_report = (corrected_output / 'report.md').read_text(encoding='utf-8')
    corrected_chart = (corrected_output / 'chart.mmd').read_text(encoding='utf-8')
    assert 'Bound' in corrected_report and 'Rules that hold' in corrected_report and 'Bound breaks' not in corrected_report
    assert not any(line.startswith('class s') and (' breaks' in line or ' mayBreak' in line)
        for line in corrected_chart.splitlines())
    assert before == {path.relative_to(dependencies).as_posix(): sha(path) for path in dependencies.rglob('*') if path.is_file()}
    managed = dependencies / plan['lockDigest'] / 'win-x64'
    node = next((managed / 'node/content').glob('*/node.exe'))
    env['STATECHART_PELE'] = str(managed / 'pele/content/package/dist/pele.js')
    run(output, 'native-js-seams', [node, '--disable-warning=ExperimentalWarning', '--test', '--test-concurrency=1',
        source / 'skills/engineering/statechart/scripts/test/native-gate.test.ts',
        source / 'skills/engineering/statechart/scripts/test/native-render.test.ts'], source, env)
    env['STATECHART_TEST_BINARY'] = str(binary)
    env['STATECHART_TEST_DEPENDENCY_ROOT'] = str(dependencies)
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', 'FullyQualifiedName~StatechartReadyTests']
    run(output, 'ready-discovery', [*base, '--list-tests'], source, env)
    assert 'StatechartReadyTests' in (output / 'ready-discovery.stdout').read_text(errors='replace')
    run(output, 'ready-tests', [*base, '--no-restore', '--logger', 'trx;LogFileName=ready.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env)
    run(output, 'docs', [binary, 'check'], ROOT, env)
    old_receipt = HERE / 'archival-associations/source-inputs.json'
    old_rows = {row['path']: row['sha256'] for row in json.loads(old_receipt.read_bytes())['files']}
    old_tests = [NATIVE + 'implementation2/test_' + name + '.py' for name in ['driver', 'launch', 'prepare']]
    old_tests.append('skills/engineering/statechart/scripts/test/corpus-views.test.cjs')
    new_tests = ['DynaDocs.Tests/coverage/tests/test_statechart_invocation_' + name + '.py'
        for name in ['driver', 'launch', 'prepare']]
    new_tests.append('DynaDocs.Tests/coverage/tests/statechart-corpus-views.test.cjs')
    save(output / 'moved-tests.json', dict(beforeReceipt=str(old_receipt), beforeReceiptSha256=sha(old_receipt),
        files=[dict(before=old, beforeSha256=old_rows[old], after=new, afterSha256=sha(source / new),
            oldPathAbsent=not (ROOT / old).exists()) for old, new in zip(old_tests, new_tests)],
        adoptedDiscoveryReceipt=str(HERE / 'public-flow-build2/native-discovery.json'),
        unittestCases=13, nodeCorpusViewCases=9, collectorOrPolicyEdited=False))
    save(output / 'public-flow-result.json', dict(initialPlanExit=3, authorizedResumeExit=1,
        actualCheckerVerdict=completion['verdict'], capturedOutput=files, completedReplayExit=1,
        readyWithUnavailableNetworkProxyExit=1, correctedWithSameInvariantExit=0, managedBytesUnchangedOnReplayAndReady=True,
        payloadDigest=plan['payloadDigest'], lockDigest=plan['lockDigest'],
        limits='Windows native public-flow slice only; npm/POSIX/dotnet-tool/installed-skill not proved here'))


def public_light(output, scratch, source, env):
    predecessor = HERE / 'public-flow-build3'
    continuation = json.loads((predecessor / 'continuation.json').read_bytes())
    previous = HERE / 'public-flow-resume-fixed'
    outcomes = {}
    for name, expected_exit in [('authorized-resume', 1), ('completed-replay', 1), ('ready-offline', 1),
            ('corrected-offline', 0), ('native-js-seams', 0), ('ready-tests', 0), ('docs', 1)]:
        receipt = previous / (name + '.command.json')
        outcome = json.loads(receipt.read_bytes())
        assert outcome['exit'] == expected_exit
        outcomes[name] = dict(exit=outcome['exit'], commandSha256=sha(receipt),
            stdoutSha256=sha(previous / (name + '.stdout')), stderrSha256=sha(previous / (name + '.stderr')))
    binary = Path(continuation['plan']['installAndContinue'][0])
    assert sha(binary) == continuation['binarySha256']
    inputs = json.loads((predecessor / 'source-inputs.json').read_bytes())
    compiled = [row for row in inputs['files'] if row['path'].startswith(('Services/Statechart/',
        'Resources/Statechart/', 'skills/engineering/statechart/')) or row['path'] in
        ['Program.cs', 'Commands/StatechartCommand.cs', 'DynaDocs.csproj', 'setup-skills.mjs']]
    assert all(sha(source / row['path']) == row['sha256'] for row in compiled), 'Compiled product inputs changed'
    env['STATECHART_TEST_BINARY'] = str(binary)
    env['STATECHART_TEST_DEPENDENCY_ROOT'] = continuation['environment']['DYDO_STATECHART_DEPENDENCY_ROOT']
    save(output / 'retained-public-flow.json', dict(outcomes=outcomes, originalStageExit=1,
        originalStageFailure='Docs: pinned uppercase README filename and JSON Markdown link',
        runtimeRepeated=False, platform='win-x64', binarySha256=sha(binary),
        originalRequestDigest=continuation['plan']['requestDigest'],
        noGlobalOrFacilitatorInstall=True, taskLocalRoots=continuation['environment']))
    save(output / 'selection.json', dict(filter=SELECTION, binarySha256=sha(binary), compiledInputs=compiled,
        completedPublicFlowReceipts=str(previous),
        nativeRepublish=False, aggregateStaticOrCoverage=False))
    base = ['dotnet', 'test', 'DynaDocs.sln', '-c', 'Release', '--filter', SELECTION]
    run(output, 'discovery', [*base, '--list-tests'], source, env)
    discovery = (output / 'discovery.stdout').read_text(errors='replace')
    expected = [path.stem for path in (source / 'DynaDocs.Tests/Statechart').glob('*Tests.cs')]
    expected += ['CanonicalSkillAssertionTests', 'ScaffoldTreeTests', 'CommandSmokeTests',
        'CommandDocConsistencyTests', 'HelpCommandTests']
    assert expected and all(name in discovery for name in expected), 'Missing affected-feature native discovery'
    run(output, 'light', [*base, '--no-restore', '--logger', 'trx;LogFileName=light.trx',
        '--results-directory', output / 'test-results', '--', 'RunConfiguration.TreatNoTestsAsError=true'], source, env)
    import xml.etree.ElementTree as ET
    results = ET.parse(output / 'test-results/light.trx').findall('.//{*}UnitTestResult')
    assert results and all(row.get('outcome') == 'Passed' for row in results)
    focused(output, scratch, source, env)
    run(output, 'docs', [binary, 'check'], ROOT, env, expected=1)
    docs = (output / 'docs.stdout').read_text(errors='replace')
    assert 'Found 1 errors, 0 warnings' in docs and 'Filename should be kebab-case: readme.md' in docs
    run(output, 'whitespace', ['git', 'diff', '--check', BASE], ROOT, env)
    save(output / 'light-result.json', dict(passed=len(results), failed=0, skipped=0,
        nativeDiscoveredClasses=expected, movedPythonTests=13, movedCorpusViewTests=9,
        noNativeRepublish=True, static='BLOCKED: remaining71 historical sources await finite disposition',
        docs='FAIL: exact captain-pinned history/README.md needs a case-only path amendment; JSON reference corrected',
        coverage='Not executed; final Python applicability must be explicitly reconciled as well as dotnet/node/TS'))


def readiness_diagnostic(output, scratch, source, env):
    continuation = json.loads((HERE / 'public-flow-build2/continuation.json').read_bytes())
    binary = Path(continuation['plan']['installAndContinue'][0])
    assert sha(binary) == continuation['binarySha256']
    dependencies = Path(continuation['environment']['DYDO_STATECHART_DEPENDENCY_ROOT'])
    assert not list(dependencies.rglob('.ready.json'))
    stage, = dependencies.rglob('.install-win-x64-*')
    node, = (stage / 'node/content').glob('*/node.exe')
    java, = (stage / 'java/content').glob('*/bin/java.exe')
    lock = json.loads((source / 'Resources/Statechart/dependency-lock.json').read_bytes())
    platform, = [item for item in lock['platforms'] if item['rid'] == 'win-x64']
    archives = []
    for name in ['node', 'java']:
        archive = stage / name / platform[name]['url'].rsplit('/', 1)[1]
        assert sha(archive) == platform[name]['sha256']
        archives.append(dict(path=str(archive), sha256=sha(archive)))
    save(output / 'diagnostic-binding.json', dict(binarySha256=sha(binary), retainedArchives=archives,
        prerequisiteInstallationRetried=False, purpose='Bounded direct versus native-gated version output counterexample'))
    for name, executable, argument in [('node', node, '--version'), ('java', java, '-version')]:
        run(output, name + '-direct', [executable, argument], scratch, env, timeout=30)
        invoke(output, name + '-native-probe', [binary, '__statechart-probe', executable, argument], scratch, env,
            dict(timeout=30, expected=0, input=b'go:1\n'))


def fidelity_inputs():
    proposal_path = TEMP / 'dyd415-archival-finite-proposal-v2-20261030.json'
    assert sha(proposal_path) == 'e1c80a97f1ef7b82b37f650bbf0fe6786282c57fcbe2160571bb5eabc9fd4358'
    proposal = json.loads(proposal_path.read_bytes())
    names = ['gate-driver.py', 'launch.py', 'milestone_checks.py', 'g1_cases.py', 'g2_cases.py',
        'g1/admission-failed-driver.py', 'g1/fixture/Fixture.csproj', 'g1/fixture/Program.cs',
        'g1/tests/Tests.csproj', 'g2/Dockerfile', 'g2/linux.sh']
    assert {row['oldPath'] for row in proposal['selectedPaths']} == {NATIVE + name for name in names}
    entries, maps = {}, []
    for row in proposal['selectedPaths']:
        original = ROOT / row['oldPath']
        raw = original.read_bytes()
        assert sha(original) == row['checkoutSha256'] and len(raw) == row['checkoutBytes']
        blob = git('ls-tree', BASE, '--', row['oldPath']).split()[2]
        assert blob == row['gitBlobId']
        data = git_bytes('cat-file', 'blob', blob)
        assert hashlib.sha256(data).hexdigest() == row['gitBytesSha256'] and len(data) == row['gitBytes']
        entries[row['proposedCheckoutEntry']] = raw
        entries[row['proposedGitEntry']] = data
        maps.append(row)
    return proposal_path, proposal, entries, maps


def fidelity_context(proposal, entries):
    dependencies, contexts = [], {}
    for stage in proposal['stageDependencies']:
        receipt = ROOT / stage['receipt']
        assert sha(receipt) == stage['receiptSha256']
        dependencies.append(dict(path=stage['receipt'], sha256=sha(receipt)))
        if not stage.get('missingArchive'):
            archive = ROOT / stage['archive']
            assert sha(archive) == stage['archiveSha256']
            dependencies.append(dict(path=stage['archive'], sha256=sha(archive)))
            continue
        assert not stage['missingSourcePaths']
        for row in stage['entries']:
            original = row['exactSource']
            if 'archive' in original:
                continue
            path = Path(original.get('retainedOwnedSnapshot', str(ROOT / original.get('exactCurrentPath', ''))))
            assert path.is_file() and sha(path) == row['expectedSha256']
            name = 'stage-context/' + row['expectedSha256'] + '/' + row['oldPath']
            entries[name] = path.read_bytes()
            context = contexts.setdefault(name, dict(entry=name, oldPath=row['oldPath'],
                sha256=row['expectedSha256'], bytes=len(entries[name]), mode='100644', source=str(path), receipts=[]))
            context['receipts'].append(stage['receipt'])
    baseline = ROOT / (NATIVE + 'commission/integration-baseline.json')
    assert sha(baseline) == proposal['legacyIntegrationContext']['retainedSha256']
    dependencies.append(dict(path=baseline.relative_to(ROOT).as_posix(), sha256=sha(baseline)))
    return dependencies, list(contexts.values())


def fidelity_name(name):
    assert name and not name.startswith(('/', '\\')) and '\\' not in name and ':' not in name
    assert all(part not in ['', '.', '..'] for part in name.split('/'))


def fidelity_extract(archive, entries, destination):
    rows, seen = [], set()
    with zipfile.ZipFile(archive) as bundle:
        infos = bundle.infolist()
        assert len(infos) == len(entries) and {info.filename for info in infos} == set(entries)
        for info in infos:
            fidelity_name(info.filename)
            assert info.filename.casefold() not in seen, 'Duplicate/case-colliding ZIP entry'
            seen.add(info.filename.casefold())
            assert stat.S_IFMT(info.external_attr >> 16) == stat.S_IFREG, 'ZIP link/device/directory rejected'
            expected = entries[info.filename]
            assert info.file_size == len(expected)
            data = bundle.read(info)
            assert data == expected
            target = destination.joinpath(*info.filename.split('/'))
            assert target.resolve().is_relative_to(destination.resolve())
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open('xb') as stream:
                stream.write(data)
            assert target.read_bytes() == expected
            rows.append(dict(entry=info.filename, bytes=len(data), sha256=sha(target), createNew=True,
                withinOwnedScratch=True, regularFile=True, byteEqual=True))
    return rows


def fidelity(output, scratch):
    proposal_path, proposal, entries, primary = fidelity_inputs()
    dependencies, contexts = fidelity_context(proposal, entries)
    assert len(primary) == 11 and len(contexts) == 5 and len(entries) == 27
    pin = TEMP / 'dyd415-archival-eleven-captain-pin-20261030.md'
    source_rows = [dict(path=row['oldPath'], sha256=row['checkoutSha256']) for row in primary]
    reference_paths = [NATIVE + 'verification.md', 'dydo/project/pitfalls/dyd-415-statechart-verification.md', ARCHIVE_PATHS[2]]
    references = [dict(path=path, sha256=sha(ROOT / path)) for path in reference_paths]
    historical = git('ls-files', '--', NATIVE).splitlines()
    before = {path: sha(ROOT / path) for path in historical if path != NATIVE + 'verification.md'}
    save(output / 'source-inputs.json', dict(base=BASE, kind='Archive fidelity only, not a product test snapshot',
        driverSha256=sha(Path(__file__)), proposal=dict(path=str(proposal_path), sha256=sha(proposal_path)),
        captainPin=dict(path=str(pin), sha256=sha(pin)), originalSources=source_rows,
        references=references, dependencies=dependencies, contextCopies=contexts, scratch=str(scratch)))
    archive, manifest_path = [ROOT / path for path in ARCHIVE_PATHS[:2]]
    archive.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for name, data in sorted(entries.items()):
            fidelity_name(name)
            info = zipfile.ZipInfo(name, date_time=(2026, 10, 30, 0, 0, 0))
            info.create_system = 3
            info.external_attr = (stat.S_IFREG | 0o644) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            bundle.writestr(info, data)
    manifest = dict(schema=1, sourceHead=BASE, stage='Original11 kept; fidelity checkpoint before removal',
        authority=dict(proposal=str(proposal_path), proposalSha256=sha(proposal_path), pin=str(pin), pinSha256=sha(pin),
            durable='https://linear.app/bodnar-balazs/document/dyd-415-finite-archival-pin-eleven-native-primitive-replay-inputs-44a93d431114'),
        archive=dict(path=ARCHIVE_PATHS[0], bytes=archive.stat().st_size, sha256=sha(archive)),
        primarySources=primary, contextCopies=contexts,
        entries=[dict(entry=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(), mode='100644') for name, data in sorted(entries.items())],
        stageDependencies=proposal['stageDependencies'], legacyIntegrationContext=proposal['legacyIntegrationContext'],
        retainedDependencyHashes=dependencies, references=references, restoration=proposal['restoration'],
        contextRestoration='For either docs-stage missingArchive receipt, map contextCopies by matching receipt+oldPath+expectedSha256; other rows use the exact retained stage ZIP entry. Never substitute final checkout variant for an earlier variant.',
        originalFilesStillPresent=True, originalFilesNotRemoved=True, noRuntimeOrBinaryEquivalenceClaim=True,
        remainingHistoricalFindingsNotRepresented=71, associationRecognition=proposal['associationRecognition'])
    save(manifest_path, manifest)
    destination = scratch / 'restored'
    destination.mkdir()
    extracted = fidelity_extract(archive, entries, destination)
    assert all(sha(ROOT / row['oldPath']) == row['checkoutSha256'] for row in primary)
    assert all(sha(ROOT / path) == digest for path, digest in before.items())
    assert all(sha(ROOT / row['path']) == row['sha256'] for row in dependencies)
    assert all(sha(ROOT / row['path']) == row['sha256'] for row in references)
    validate()
    save(output / 'fidelity.json', dict(schema=1, verdict='PASS: exact non-executing archive source fidelity only',
        sourceHead=BASE, archive=dict(path=ARCHIVE_PATHS[0], sha256=sha(archive), bytes=archive.stat().st_size),
        manifest=dict(path=ARCHIVE_PATHS[1], sha256=sha(manifest_path)), originalSources=source_rows,
        original11PresentAndUnchanged=True, otherHistoricalNativeFilesUnchanged=len(before) - 11,
        references=references, retainedDependencies=dependencies, extractionRoot=str(destination), extracted=extracted,
        exactEntrySet=27, primaryCheckoutEntries=11, separateGitBlobEntries=11, oldStageContextEntries=5,
        rawSourceAndRestoredBytesEqual=True, importedOrExecutedRestoredCode=False,
        projectCompileRestoreOrHistoricalReplay=False, staticOrCoverageRetry=False,
        removalAuthorized=False, static='Retained exit2/incomplete90 associations, not changed by fidelity',
        signature='code-writer/model: gpt-6.1-sol/high'))
    print('fidelity: 27/27 exact entries; original11 retained', flush=True)


def execute(mode, output, owner):
    sys.path.insert(0, str(ROOT / 'DynaDocs.Tests/coverage'))
    from run_tests import contain_descendants, end_descendants
    from windows_job import bindings, members
    job = contain_descendants()
    try:
        if mode == 'remove':
            remove_originals(output)
        elif mode == 'fidelity':
            scratch = Path(tempfile.mkdtemp(prefix='dyd415-archival-fidelity-', dir=TEMP))
            fidelity(output, scratch)
        else:
            scratch, source, env = snapshot(output)
            {'product': product, 'static': static, 'attribution': attribution, 'catalogue': catalogue,
                'request': request, 'focused': focused, 'archives': archive_tests,
                'public-red': public_red, 'public-flow': public_flow,
                'public-corrected': lambda *args: public_flow(*args, corrected=True),
                'public-probe-corrected': lambda *args: public_flow(*args, probe=True),
                'public-resume': public_resume,
                'public-light': public_light,
                'readiness-diagnostic': readiness_diagnostic,
                'archives-red': lambda *args: archive_tests(*args, red=True)}[mode](output, scratch, source, env)
    finally:
        end_descendants(job)
        remaining = [pid for pid in members(bindings(), job) if pid != os.getpid()]
        save(output / 'teardown.json', dict(remainingOwnedProcesses=remaining,
            cleanupConfirmed=not remaining, snapshotsRetained=True, **owner))
        if remaining: raise RuntimeError('Owned teardown unconfirmed')


def main():
    mode, attempt = sys.argv[1:]
    assert mode in ['product', 'static', 'attribution', 'catalogue', 'request', 'fidelity', 'remove', 'focused', 'archives', 'archives-red', 'public-red', 'public-flow', 'public-corrected', 'public-probe-corrected', 'public-resume', 'public-light', 'readiness-diagnostic']
    assert attempt.replace('-', '').isalnum() and len(attempt) < 40
    validate(originals_present=mode == 'remove')
    sys.path.insert(0, str(LC))
    from tools.testing.runner import suite_slot
    from tools.testing.runner.machine_lock import HEAVY, machine_lock
    assert suite_slot.live().directory.resolve() == (TEMP.parent / 'lc-test/suite-slot').resolve()
    output = HERE / attempt
    output.mkdir(exist_ok=False)
    command = subprocess.list2cmdline([sys.executable, '-B', str(Path(__file__)), mode, attempt])
    failure = None
    with suite_slot.book(command, worktree=ROOT) as booking:
        owner = dict(pid=os.getpid(), command=command, ticket=os.environ['LC_SUITE_SLOT_TICKET'],
            namespace=str(suite_slot.live().directory), inherited=booking.inherited,
            lcHashes=LC_HASHES, noPycache=True, startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        save(output / 'admission.json', owner)
        with machine_lock(HEAVY, on_wait=suite_slot.refuse_heavy_wait(HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            lock_bytes = (json.dumps(owner, sort_keys=True) + '\n').encode()
            with lock.open('xb') as stream: stream.write(lock_bytes)
            try:
                execute(mode, output, owner)
            except BaseException as error:
                failure = error
                save(output / 'stop.json', dict(type=type(error).__name__, reason=str(error)))
            finally:
                teardown = json.loads((output / 'teardown.json').read_bytes())
                if teardown['cleanupConfirmed'] and lock.read_bytes() == lock_bytes: lock.unlink()
        save(output / 'secondary-released.json', dict(heavyReleased=True, dydoReleased=not lock.exists(), **owner))
    save(output / 'released.json', dict(fifoReleased=booking.owned, inheritedReturned=booking.inherited, **owner))
    if failure: raise failure


if __name__ == '__main__':
    main()

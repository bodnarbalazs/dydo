"""Source/data packaging only; no project imports, tests, discovery or runtime invocation."""
import ast
import difflib
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
PACKET = Path(__file__).resolve().parent
TEMP = Path('C:/Users/User/AppData/Local/Temp')
PREFIX = PACKET.relative_to(ROOT).as_posix()
SCRIPTS = 'skills/engineering/statechart/scripts/'
LIVE = [SCRIPTS + 'statechart.ts', SCRIPTS + 'test/cli.test.ts']
MAPPING = {LIVE[0]: PREFIX + '/before/statechart.ts', LIVE[1]: PREFIX + '/before/cli.test.ts'}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def row(path):
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size, sha256=digest(path))


def save(name, data):
    (PACKET / name).write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8', newline='\n')


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW)


def main():
    before = json.loads((PACKET / 'before-custody.json').read_bytes())
    assert git('rev-parse', 'HEAD').decode().strip() == before['head'] == '6e3fe7e8018130af6b8586ef0cf40c1474740864'
    assert git('branch', '--show-current').decode().strip() == before['branch'] == 'DYD-415-statechart'
    assert Path(git('rev-parse', '--show-toplevel').decode().strip()).resolve() == ROOT.resolve()
    preserved = json.loads((PACKET / 'preserved-inputs.json').read_bytes())
    for group in [before['rows'], preserved]:
        for item in group:
            target = ROOT / MAPPING.get(item['path'], item['path'])
            assert digest(target) == item['sha256'] and target.stat().st_size == item['bytes'], item['path']
    for item in before['originalFiles'] + before['beforeCopies']:
        target = ROOT / MAPPING.get(item['path'], item['path'])
        assert digest(target) == item['sha256'] and target.stat().st_size == item['bytes']
    # before.rows is the retained draft/evidence inventory, not every unchanged HEAD file.
    # Git status closes unowned additions/modifications; bound raw hashes close old dirty bytes.
    original_status = set(before['gitStatus'].splitlines())
    current_status = set(git('status', '--porcelain=v1', '--untracked-files=all').decode().splitlines())
    assert original_status <= current_status, sorted(original_status - current_status)
    added_status = sorted(current_status - original_status)
    assert all(line.startswith('?? ' + PREFIX + '/') or line in [' M ' + path for path in LIVE]
        for line in added_status), added_status
    current_changes = [item['path'] for item in before['rows'] if digest(ROOT / item['path']) != item['sha256']]
    assert sorted(current_changes) == sorted(LIVE), current_changes
    old_ts = (ROOT / MAPPING[LIVE[0]]).read_text(encoding='utf-8')
    new_ts = (ROOT / LIVE[0]).read_text(encoding='utf-8')
    old_tests = (ROOT / MAPPING[LIVE[1]]).read_bytes()
    new_tests = (ROOT / LIVE[1]).read_bytes()
    assert new_tests.startswith(old_tests), 'Existing test bytes must stay an exact prefix'
    worker_old = old_ts.split('function startWorker', 1)[1].split('function publish', 1)[0].strip()
    worker_new = new_ts.split('function startWorker', 1)[1].split('function startJanitor', 1)[0].strip()
    assert worker_old == worker_new
    script = 'while read -r line; do :; done; kill -s KILL -- "-$1"; rm -rf -- "$2" "$3"'
    assert old_ts.count(script) == new_ts.count(script) == 1
    windows_final = ['if (closeGuard) await closeGuard();', 'fs.rmSync(stage, { recursive: true, force: true });',
                     'if (tmp) fs.rmSync(tmp, { recursive: true, force: true });']
    for line in windows_final:
        assert line in old_ts and line in new_ts
    for line in ['if (process.platform === "win32") closeGuard = await guardWorker(worker, stage, tmp);',
                 'if (stop) await closeGuard();']:
        assert line in old_ts and line in new_ts
    assert 'if (process.platform === "win32") showReport(dest, out);' in new_ts
    delta = ''
    for path in LIVE:
        delta += ''.join(difflib.unified_diff((ROOT / MAPPING[path]).read_text(encoding='utf-8').splitlines(True),
            (ROOT / path).read_text(encoding='utf-8').splitlines(True), fromfile='before/' + path, tofile=path, n=0))
    (PACKET / 'production-test.diff').write_text(delta, encoding='utf-8', newline='\n')

    manifest = json.loads((PACKET / 'input-manifest.json').read_bytes())
    for item in manifest['files']:
        actual = row(ROOT / item['path'])
        if item['path'] not in LIVE:
            assert item['sha256'] == actual['sha256'] and item['bytes'] == actual['bytes']
        item.update(actual)
        path = item['path']
        item['role'] = ('native-test' if path.endswith('/test/cli.test.ts') else
            'replay-harness' if path.endswith('/test/java.ts') else
            'retained-corpus-data' if '/test/corpus/' in path else
            'verified-helper-data' if '/java/' in path else
            'package-config' if path.endswith('package.json') else 'product-source')
    manifest['names'] = [name.replace('with no live grandchild', 'with no output') for name in manifest['names']]
    closure = {item['path'] for item in manifest['files']}
    edges = []
    for item in manifest['files']:
        if not item['path'].endswith('.ts'):
            continue
        text = (ROOT / item['path']).read_text(encoding='utf-8')
        for literal in re.findall(r'''(?:from\s+|import\s*)["'](\.[^"']+)["']''', text):
            target = ((ROOT / item['path']).parent / literal).resolve().relative_to(ROOT).as_posix()
            assert target in closure, (item['path'], literal, target)
            edges.append(dict(source=item['path'], literal=literal, target=target))
    manifest['literalImports'] = edges
    manifest['dynamicInputs'] = ['worker.ts spawned by corrected statechart.ts',
        'Counter and CounterInstance retained SANY/verdict/ChartRun/graph replay',
        'sealed bootstrap jar + build-lock read as DATA', 'scratch-only test observer/view fixtures generated by exact cli.test.ts']
    manifest['view'] = 'No view for the uninstrumented Counter; exact focused tests generate only their declared views'
    save('input-manifest.json', manifest)
    expectations = json.loads((PACKET / 'expectations.json').read_bytes())
    assert manifest['names'] == [item['name'] for item in expectations['cases']]
    external = []
    for item in manifest['retainedInputs']:
        path = Path(item['source'])
        assert digest(path) == item['sha256'] and path.stat().st_size == item['bytes']
        external.append(dict(path=str(path), bytes=path.stat().st_size, sha256=digest(path), role='retained-data'))
    lc = Path('C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea')
    for name, expected in {
        'tools/testing/runner/suite_slot.py': 'aa36020ee48e08319451e6c126701251aa3be9ef40ca4e4cb9cbaed1e375d21a',
        'tools/testing/runner/machine_lock.py': '7b53157084084ebb6806f7b4f391a82467f9fed0fc9d701d3fd3e0954c0d9736',
        'dydo/reference/tools/test-runner.md': 'c0eb699240e4f0bd1526f1bbf495604fbf67bbb3ed3002f19fc09222a9ea92a4'
    }.items():
        path = lc / name
        assert digest(path) == expected
        external.append(dict(path=str(path), bytes=path.stat().st_size, sha256=expected, role='commissioned-lc-api-read-only'))
    root = '/toolchain/repo/skills/engineering/statechart/scripts'
    node = '/toolchain/node-v22.23.2-linux-x64/bin/node'
    pattern = '^(?:' + '|'.join(re.escape(name) for name in manifest['names']) + ')$'
    save('commands.json', dict(state='NOT_RELEASED',
        host=['python', '-B', 'C:/Users/User/AppData/Local/Temp/dyd415-rmi-b3-janitor-correction-launch-20261031.py'],
        requiresActualHostApproval=True, order=['FIFO', 'Heavy-nonwaiting', 'exclusive-normal-TEMP-lock'],
        containerCommands=[['python', '-B', '/input/worker.py', '--extract'], ['python', '-B', '/input/worker.py']],
        counter=[node, '--disable-warning=ExperimentalWarning', root + '/statechart.ts', root + '/test/corpus/Counter/Counter.tla', '--out', '/work/result'],
        nativeTests=[node, '--disable-warning=ExperimentalWarning', '--test', '--test-reporter=tap', '--test-concurrency=1',
            '--test-name-pattern=' + pattern, 'test/cli.test.ts'],
        fixedEnvironment=dict(PATH='/toolchain/node-v22.23.2-linux-x64/bin:/usr/local/bin:/usr/bin:/bin',
            HOME='/work/home', TMPDIR='/work/tmp', LC_ALL='C.UTF-8', LANG='C.UTF-8',
            JAVA=root + '/test/java.ts', TLA2TOOLS='/input/tla2tools.jar',
            STATECHART_REPLAY=root + '/test/corpus/Counter', STATECHART_JAVA='/no-runtime/java-forbidden',
            STATECHART_LOG='/output/replayed-configs.log', NODE_OPTIONS='--disable-warning=ExperimentalWarning'),
        testOnlyEnvironmentAddition=dict(STATECHART_JANITOR_PROOF='/output/janitor-events'),
        bounds=dict(childSeconds=120, directDrainSeconds=5, sampleCap=60502, intervalSeconds=0.002,
            postExitSeconds=1, fileBytes=16777216, outerAttachSeconds=1200),
        containerSettings='Exact unchanged common.container source hash pinned; image/nonce/ID/config inspect before start',
        abort='First failed command/identity/bound/strict guard stops, preserves evidence, independent exact-ID cleanup; no retry'))
    save('historical-applicability.json', dict(schema=1, beforeSnapshots=MAPPING,
        interpretation='Historical byte custody only; only two live source identities are superseded for new lifecycle claims',
        unchangedHistoricalRows=len(preserved) - sum(item['path'] in LIVE for item in preserved),
        old5079RowManifests='IMMUTABLE_NOT_REGENERATED', c='FAIL_UNCHANGED',
        redReceiptSha256='d8edbe86f510fd5dad3525c9c50c588d4477978cadeaf26f11137f7bb84d6bf5',
        ancestry='CLI7 direct children worker14 and janitor15; janitor15 targets worker14',
        limits=['Original exact libuv timing unproved', 'Original five C identities unproved',
            'Old installed/payload/runtime results do not certify changed source']))

    payload = ROOT / 'Resources/Statechart/payload.json'
    props = ROOT / 'Resources/Statechart/payload.items.props'
    assert digest(payload) == '32e20e198c87b31fe6ca5b560cadc33609ff99802e5bd0cfab4cbc7c1605860d'
    assert digest(props) == '8397dd484855d41c008d1af3138718f5bd6bd726d8b6b2322b519c8b7a83bd6a'
    declaration = json.loads(payload.read_bytes())
    old_row = next(item.copy() for item in declaration['files'] if item['path'] == LIVE[0])
    new_row = dict(old_row, bytes=(ROOT / LIVE[0]).stat().st_size, sha256=digest(ROOT / LIVE[0]))
    proposed_payload = payload.read_text(encoding='utf-8').replace('"bytes": ' + str(old_row['bytes']) + ',\n      "sha256": "' + old_row['sha256'] + '"',
        '"bytes": ' + str(new_row['bytes']) + ',\n      "sha256": "' + new_row['sha256'] + '"')
    proposed_props = props.read_text(encoding='utf-8').replace('<ExpectedHash>' + old_row['sha256'] + '</ExpectedHash>',
        '<ExpectedHash>' + new_row['sha256'] + '</ExpectedHash>')
    assert proposed_payload != payload.read_text(encoding='utf-8') and proposed_props != props.read_text(encoding='utf-8')
    proposed_delta = ''
    for path, proposed in [(payload, proposed_payload), (props, proposed_props)]:
        proposed_delta += ''.join(difflib.unified_diff(path.read_text(encoding='utf-8').splitlines(True), proposed.splitlines(True),
            fromfile=path.relative_to(ROOT).as_posix(), tofile='PROPOSED/' + path.relative_to(ROOT).as_posix(), n=0))
    (PACKET / 'payload-proposed.diff').write_text(proposed_delta, encoding='utf-8', newline='\n')
    save('payload-proposal.json', dict(state='DEFERRED_NOT_APPLIED', unchangedDeclarations=[row(payload), row(props)],
        payloadRowBefore=old_row, payloadRowAfter=new_row, propsExpectedHashBefore=old_row['sha256'],
        propsExpectedHashAfter=new_row['sha256'], entriesAdded=0, otherCurrentIdentityConsumers=[],
        historicalMatches='Immutable source/evidence snapshots, not live declaration amendment authority',
        installedProof='OWED_FOR_CHANGED_PAYLOAD'))

    for name in ['driver', 'launcher']:
        target = TEMP / ('dyd415-rmi-b3-janitor-correction-' + ('launch' if name == 'launcher' else 'driver') + '-20261031.py')
        target.write_bytes((PACKET / (name + '.py')).read_bytes())
    parsed = []
    for path in sorted(PACKET.glob('*.py')):
        ast.parse(path.read_text(encoding='utf-8'), filename=str(path))
        parsed.append(row(path))
    assert not (PACKET / 'execution-release.json').exists()
    assert not (PACKET.parent / 'b3-janitor-correction-20261031a').exists()
    whitespace = subprocess.run(['git', 'diff', '--check', '--', *LIVE], cwd=ROOT,
        capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
    assert whitespace.returncode == 0, whitespace.stdout + whitespace.stderr
    for path in PACKET.rglob('*'):
        if path.is_file() and path.suffix in ['.py', '.md', '.json', '.diff']:
            assert not any(line.endswith((' ', '\t')) for line in path.read_text(encoding='utf-8').splitlines()), str(path)
    save('source-check.json', dict(state='SOURCE_ONLY_NOT_RELEASED', head=before['head'], branch=before['branch'],
        isolatedRoot=str(ROOT), dirtyDraftExplicitlyAuthorized=True, beforeCustodyRows=len(before['rows']),
        preservedRows=len(preserved), onlyChangedExistingPaths=current_changes, unchangedExistingTestPrefix=True,
        additionalGitStatusRows=added_status,
        startWorkerByteIdentical=True, eofScriptByteIdentical=True, windowsFiniteTextualChecks=True,
        noWindowsRuntimeProof=True, parsedPython=parsed, importEdges=len(edges), snapshotFiles=len(closure),
        liveGitDiffCheckExit=whitespace.returncode, liveGitDiffCheckStdout=whitespace.stdout.decode(),
        liveGitDiffCheckStderr=whitespace.stderr.decode(), newPacketTextTrailingWhitespace=False,
        testDiscovery='NOT_RUN', tests='NOT_RUN', runtime='NOT_RUN', payload='UNCHANGED',
        abruptParent='NOT_SELECTED_CURRENT_PID1_CANNOT_CERTIFY_STRICT_REAP', review='CAPTAIN_OWNED_AFTER_FOCUSED_GREEN'))
    sources = [row(path) for path in sorted(PACKET.rglob('*')) if path.is_file() and path.name != 'source-pin.json']
    sources += [row(ROOT / path) for path in LIVE]
    sources += [row(ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-source/driver.py')]
    sources += [row(ROOT / manifest[key]['path']) for key in ['nodeMemberManifest', 'snapshotAuthority']]
    counterparts = [dict(path=str(TEMP / ('dyd415-rmi-b3-janitor-correction-' + name + '-20261031.py')),
        bytes=(PACKET / source).stat().st_size, sha256=digest(PACKET / source))
        for name, source in [('driver', 'driver.py'), ('launch', 'launcher.py')]]
    save('source-pin.json', dict(schema=1, state='SOURCE_ONLY_NOT_RELEASED', head=before['head'],
        sourceScope=LIVE, driverSha256=digest(PACKET / 'driver.py'), launcherSha256=digest(PACKET / 'launcher.py'),
        sources=sources, tempCounterparts=counterparts, inputManifestSha256=digest(PACKET / 'input-manifest.json'),
        externalInputs=external,
        preservation='Original before-custody and preserved-inputs bytes retained; only explicit beforeSnapshot mapping',
        output=str(PACKET.parent / 'b3-janitor-correction-20261031a'), prospectiveSelectedTests=12,
        uninstrumentedCounterInvocations=1, executionReleased=False))
    print(json.dumps(dict(sourcePinSha256=digest(PACKET / 'source-pin.json'), sources=len(sources),
        beforeCustodyRows=len(before['rows']), preservedRows=len(preserved), snapshotFiles=len(closure),
        importEdges=len(edges), beforeAfter=[dict(before=item, after=row(ROOT / item['path'])) for item in before['originalFiles']],
        tempCounterparts=counterparts), indent=2))


if __name__ == '__main__':
    main()

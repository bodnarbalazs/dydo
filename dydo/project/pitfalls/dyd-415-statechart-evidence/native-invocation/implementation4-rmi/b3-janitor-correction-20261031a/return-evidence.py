"""Read retained proof/hash records for the handback; never import or execute product/tests."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = Path(__file__).resolve().parent
PACKET = OUT.parent / 'b3-janitor-correction-source'
RAW = OUT / 'retained/output'


def read(path):
    return json.loads(path.read_bytes())


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size, sha256=sha(path))


def check(item, mapping=None):
    path = Path((mapping or {}).get(item['path'], item['path']))
    if not path.is_absolute():
        path = ROOT / path
    assert sha(path) == item['sha256'], str(path)
    assert path.stat().st_size == item['bytes'], str(path)


def main():
    assert sha(PACKET / 'source-pin.json') == '30bbe7be70c9482971410d1ddef6764481da0828237c5d1d3a6096f8ec73efce'
    pin = read(PACKET / 'source-pin.json')
    for item in pin['sources'] + pin['tempCounterparts'] + pin['externalInputs']:
        check(item)
    manifest = read(PACKET / 'input-manifest.json')
    for item in manifest['files']:
        check(item)
    mapping = read(PACKET / 'historical-applicability.json')['beforeSnapshots']
    assert set(mapping) == set(pin['sourceScope']) and len(mapping) == 2
    preserved = read(PACKET / 'preserved-inputs.json')
    before = read(PACKET / 'before-custody.json')
    for items in [preserved, before['rows']]:
        for item in items:
            check(item, mapping)
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()
    assert head == pin['head']
    original_status = set(before['gitStatus'].splitlines())
    current_status = set(subprocess.check_output(['git', 'status', '--porcelain=v1', '--untracked-files=all'],
        cwd=ROOT, creationflags=subprocess.CREATE_NO_WINDOW).decode().splitlines())
    assert original_status <= current_status
    prefixes = [PACKET.relative_to(ROOT).as_posix(), OUT.relative_to(ROOT).as_posix()]
    extra = current_status - original_status
    assert all(any(line.startswith('?? ' + prefix + '/') for prefix in prefixes)
        or line in [' M ' + path for path in pin['sourceScope']] for line in extra)

    native = []
    for line in (RAW / 'regressions.stdout').read_text(encoding='utf-8').splitlines():
        match = re.match(r'^(not ok|ok) (\d+) - (.*?)(?: # (.*))?$', line)
        if match:
            status, number, name, directive = match.groups()
            native.append(dict(status=status, number=int(number), name=name, directive=directive))
    event_logs = []
    for path in sorted((RAW / 'janitor-events').glob('*/events.jsonl')):
        events = [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines()]
        mode = re.search(r'const mode = "([^"]+)";', (path.parent / 'observe.mjs').read_text()).group(1)
        janitor = [event for event in events if event.get('role') == 'janitor' and event['event'] == 'spawn']
        closes = [(index, event) for index, event in enumerate(events)
            if event.get('role') == 'janitor' and event['event'] == 'close']
        exits = [(index, event) for index, event in enumerate(events) if event['event'] == 'cli-exit']
        writes = [index for index, event in enumerate(events) if event['event'] == 'stdout']
        completed_order = bool(len(closes) == 1 and len(exits) == 1 and closes[0][0] < exits[0][0])
        event_logs.append(dict(file=identity(path), mode=mode,
            cliPid=events[0]['parent'] if events else None, janitorSpawns=janitor,
            janitorCloses=[event for _, event in closes], cliExits=[event for _, event in exits],
            workerExits=[event for event in events if event.get('role') == 'worker' and event['event'] == 'exit'],
            janitorCloseBeforeControlledExit=completed_order,
            janitorCloseBeforeSuccessStdout=bool(writes and closes and closes[0][0] < min(writes)),
            completeControlledExitObserved=bool(exits), events=events))

    teardown, released, secondary = [read(OUT / name) for name in
        ['teardown.json', 'released.json', 'secondary-released.json']]
    assert teardown['cleanupConfirmed'] and released['cleanupConfirmed'] and released['fifoReleased']
    assert secondary['heavyReleased'] and secondary['tempLockReleased']
    for name in ['cleanup-0-absence', 'cleanup-1-absence', 'cleanup-volume-absence']:
        assert read(OUT / (name + '.result.json'))['exit'] == 0
        assert (OUT / (name + '.stdout')).read_bytes() == b''
    counter = read(RAW / 'counter.semantic.json')
    report = ROOT / 'skills/engineering/statechart/scripts/test/corpus/Counter/report.md'
    chart = ROOT / 'skills/engineering/statechart/scripts/test/corpus/Counter/chart.mmd'
    assert counter['exit'] == 0 and (RAW / 'counter.stderr').stat().st_size == 0
    assert (RAW / 'counter.stdout').read_bytes() == report.read_bytes() + b'\nWrote /work/result\n'
    assert (OUT / 'retained/work/result/report.md').read_bytes() == report.read_bytes()
    assert (OUT / 'retained/work/result/chart.mmd').read_bytes() == chart.read_bytes()
    counter_census = read(RAW / 'counter.processes-after.json')
    assert len(counter_census) == 1 and counter_census[0]['pid'] == 1
    receipt = dict(schema=1, author='code-writer/model: gpt-6.1-sol/high',
        utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), head=head,
        verdict='FAIL_FOCUSED_SELECTION_INCOMPLETE_NO_RETRY',
        sourcePin=identity(PACKET / 'source-pin.json'), release=identity(PACKET / 'execution-release.json'),
        launcher=read(OUT / 'launcher.result.json'),
        counter=dict(semantic=counter, result=read(RAW / 'counter.result.json'),
            stdout=identity(RAW / 'counter.stdout'), trace=identity(RAW / 'processes.jsonl'),
            census=counter_census, strictNoOtherProcesses=True),
        native=dict(raw=identity(RAW / 'regressions.stdout'), stderr=identity(RAW / 'regressions.stderr'),
            records=native, okRecords=sum(item['status'] == 'ok' for item in native),
            nonOkRecords=sum(item['status'] == 'not ok' for item in native),
            selectedSkipRecords=sum(bool(item['directive']) for item in native),
            completeSuiteSummary=False, result=read(RAW / 'regressions.result.json'),
            timeoutTrace=identity(OUT / 'dyd415-rmi-b3-janitor-correction-6113cbdc5f1f4566-finite-start.stderr'),
            failure='SIGINT and SIGTERM cases timed out at30s; Node command remained alive until120s and was SIGKILLed/reaped by pinned worker',
            noFinalStrictCensus=(not (RAW / 'regressions.processes-after.json').exists()),
            caseContinuation='One native command continued after its two test timeouts; no further command or retry followed'),
        eventLogs=event_logs, eventLogCount=len(event_logs),
        attribution='Signal-case cause unresolved. No product or fixture correction and no signal-success claim.',
        cleanup=dict(teardown=teardown, released=released, secondary=secondary,
            rawAbsenceChecks=['cleanup-0-absence', 'cleanup-1-absence', 'cleanup-volume-absence']),
        custody=dict(sourceRows=len(pin['sources']), preservationRows=len(preserved),
            originalDraftRows=len(before['rows']), inputRows=len(manifest['files']),
            tempCounterparts=len(pin['tempCounterparts']), externalInputs=len(pin['externalInputs']),
            mismatches=0, exactTwoFileHistoricalMapping=mapping, payloadUnchanged=True,
            sourceEdits=False, testEdits=False, expectationEdits=False),
        limits=['C remains historical FAIL', 'Abrupt-parent strict drain not executable/certifiable in current Python PID1 topology',
            'Observer changes scheduling; passive trace alone cannot establish close ordering',
            'Focused GREEN not achieved; captain review not commissioned',
            'Desktop Java hold and Windows/prompt/Java assurance plus final obligations remain',
            'No payload edit, commit, PR, push, review, runtime retry or real TLC'])
    with (OUT / 'return-receipt.json').open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(receipt, stream, indent=2)
        stream.write('\n')
    print(json.dumps(dict(receipt=identity(OUT / 'return-receipt.json'), verdict=receipt['verdict'],
        nativeOk=receipt['native']['okRecords'], nativeNonOk=receipt['native']['nonOkRecords'],
        eventLogs=len(event_logs), controlledExitLogs=sum(item['completeControlledExitObserved'] for item in event_logs),
        closeBeforeExitLogs=sum(item['janitorCloseBeforeControlledExit'] for item in event_logs),
        cleanupConfirmed=True, custody=receipt['custody']), indent=2))


if __name__ == '__main__':
    main()

"""Twelve exact canonical cases, serial native summaries and strict census; source only."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import sys

NAMES = [
    "POSIX janitor close precedes the Counter report and controlled exit 0",
    "POSIX janitor close precedes the CounterInstance report and controlled exit 1",
    "POSIX janitor closes after an early worker error without publishing output",
    "POSIX janitor closes before controlled SIGINT exit with no output",
    "POSIX janitor closes before controlled SIGTERM exit with no output",
    "POSIX janitor spawn failure stops and reaps the worker without publishing",
    "POSIX janitor early close is retained and stops the owned worker",
    "POSIX worker spawn failure is observed before any janitor starts",
    "POSIX cleanup exception remains visible and cannot skip actual janitor close",
    "POSIX spawn and cleanup faults are both visible after actual ownership drain",
    "POSIX janitor kill exception is reported only after the real child closes",
    "POSIX final drain preserves a caller file arriving at output publication"
]
EXITS = [0, 1, 2, 130, 143, 2, 2, 2, 2, 2, 2, 2]


def load_foundation():
    path = Path('/input/foundation.py')
    if hashlib.sha256(path.read_bytes()).hexdigest() != '907d41101cf1ce465a746a762e2b1233db1845ba186b5c0ced94fd0eec17ef6e':
        raise RuntimeError('Frozen preparation/guard differs before import')
    spec = importlib.util.spec_from_file_location('_frozen_twelve_foundation', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def selected_tap(text, name):
    records = []
    selected = []
    for status, title, directive in re.findall(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', text, re.M):
        records.append(dict(status=status, name=title, directive=directive))
        if title == name:
            if status != 'ok' or directive:
                raise RuntimeError('Selected case failed/skipped')
            selected.append(title)
        elif status != 'ok' or not directive.startswith('SKIP test name does not match pattern'):
            raise RuntimeError('Unselected case executed/non-ok')
    if selected != [name]:
        raise RuntimeError('One unique exact native selected ok required')
    for label, value in [('pass', 1), ('fail', 0), ('cancelled', 0), ('todo', 0)]:
        if not re.search(r'^# ' + label + ' ' + str(value) + '$', text, re.M):
            raise RuntimeError('Incomplete/failed native terminal summary')
    if not re.search(r'^1\.\.' + str(len(records)) + '$', text, re.M):
        raise RuntimeError('Complete native TAP plan required')
    return records


def lifecycle_events(events, index):
    exits = [row for row in events if row['event'] == 'cli-exit']
    spawns = [row for row in events if row.get('role') == 'janitor' and row['event'] == 'spawn']
    closes = [row for row in events if row.get('role') == 'janitor' and row['event'] == 'close']
    workers = [row for row in events if row.get('role') == 'worker' and row['event'] == 'close']
    if len(exits) != 1 or exits[0]['code'] != EXITS[index] or len(workers) != 1:
        raise RuntimeError('Actual CLI verdict/worker close differs')
    if index == 7:
        if spawns or closes or workers[0]['code'] != -2 or workers[0]['signal'] is not None:
            raise RuntimeError('Worker spawn-failure ownership differs')
        return dict(cli=exits[0], worker=workers[0], janitor='NOT_CREATED')
    if len(spawns) != 1 or len(closes) != 1:
        raise RuntimeError('Unique actual janitor spawn/close required')
    spawned, closed = spawns[0], closes[0]
    if closed.get('pid') != spawned.get('pid') or closed['parent'] != spawned['parent'] or exits[0]['parent'] != spawned['parent']:
        raise RuntimeError('Exact janitor/CLI ownership differs')
    if events.index(closed) >= events.index(exits[0]):
        raise RuntimeError('Actual janitor close must precede controlled CLI exit')
    if index in [5, 9]:
        if closed['code'] != -2 or closed['signal'] is not None or spawned.get('pid') is not None:
            raise RuntimeError('Janitor spawn-failure boundary differs')
    elif not spawned.get('pid') or closed['signal'] != 'SIGKILL':
        raise RuntimeError('Owned janitor actual SIGKILL close required')
    return dict(cli=exits[0], worker=workers[0], janitor=closed)


def drain_runner(child, start):
    if child.poll() is None:
        current = Path('/proc', str(child.pid), 'stat').read_text().rsplit(') ', 1)[1].split()
        if start is None or current[19] != start or int(current[1]) != os.getpid() or os.getpgid(child.pid) != child.pid:
            raise RuntimeError('Direct native runner ownership uncertain; container containment required')
        os.killpg(child.pid, signal.SIGKILL)


def run_case(foundation, name, index):
    prefix = 'case-' + str(index + 1).zfill(2)
    foundation.remaining_processes(prefix + '.processes-before.json')
    argv = [str(foundation.NODE), '--disable-warning=ExperimentalWarning', '--test',
        '--test-reporter=tap', '--test-concurrency=1', '--test-name-pattern=^' + re.escape(name) + '$',
        'test/cli.test.ts']
    env = foundation.environment()
    env['STATECHART_JANITOR_PROOF'] = str(foundation.OUTPUT / 'janitor-events' / prefix)
    Path(env['STATECHART_JANITOR_PROOF']).mkdir(parents=True)
    foundation.save(prefix + '.argv.json', dict(argv=argv, cwd=str(foundation.SCRIPTS), environment=env,
        selectedNames=[name], runnerSeconds=65, directReapSeconds=5, fileLimitBytes=foundation.LIMIT,
        realJava=False, canonicalAssertionsUnchanged=True))
    out, err = [foundation.OUTPUT / (prefix + suffix) for suffix in ['.stdout', '.stderr']]
    child, start, failure, reaped = None, None, None, False
    try:
        with out.open('xb') as stdout, err.open('xb') as stderr:
            child = subprocess.Popen(argv, cwd=foundation.SCRIPTS, env=env, stdout=stdout,
                stderr=stderr, start_new_session=True)
            raw = Path('/proc', str(child.pid), 'stat').read_text()
            start = raw.rsplit(') ', 1)[1].split()[19]
            foundation.save(prefix + '.owned.json', dict(pid=child.pid, stat=raw, starttime=start,
                parent=os.getpid(), pgid=os.getpgid(child.pid)))
            child.wait(timeout=65)
            reaped = True
    except BaseException as error:
        failure = error
    finally:
        if child is not None:
            try:
                drain_runner(child, start)
            except BaseException as error:
                failure = failure or error
            try:
                child.wait(timeout=5)
                reaped = True
            except BaseException as error:
                failure = failure or error
    census = foundation.census()
    strict_clean = not any(row['pid'] != os.getpid() for row in census)
    foundation.save(prefix + '.processes-after.json', census)
    foundation.save(prefix + '.result.json', dict(exit=child.returncode if child else None,
        directRunnerReaped=reaped, strictCensusClean=strict_clean,
        failure=str(failure) if failure else None, infrastructureContainment='NOT_YET_ATTEMPTED',
        productCloseNotInferredFromRunnerReap=True))
    if failure:
        raise failure
    if not reaped or not strict_clean or child.returncode != 0 or err.stat().st_size:
        raise RuntimeError('Case FAIL/inconclusive; strict census rejects all residual PIDs including zombies')
    records = selected_tap(out.read_text(encoding='utf-8'), name)
    logs = list(Path(env['STATECHART_JANITOR_PROOF']).glob('*/events.jsonl'))
    if len(logs) != 1:
        raise RuntimeError('Exactly one real CLI lifecycle log per native case required')
    events = [json.loads(line) for line in logs[0].read_text().splitlines()]
    observed = lifecycle_events(events, index)
    foundation.verify_files()
    foundation.save(prefix + '.selection.json', dict(name=name, records=records,
        actualLifecycle=observed, nativeExit=0, nativeSummaryComplete=True, strictCensusClean=True))
    return dict(name=name, nativeExit=0, selected=1, selectedSkipped=0,
        nativeSummaryComplete=True, actualLifecycle=observed, strictCensusClean=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux' or os.getpid() != 1:
        raise RuntimeError('Original private Linux Python PID1 topology only')
    import resource
    foundation = load_foundation()
    if args.extract:
        foundation.extract()
        return
    resource.setrlimit(resource.RLIMIT_FSIZE, (foundation.LIMIT, foundation.LIMIT))
    manifest = foundation.verify_files()
    if manifest['names'] != NAMES or len(set(NAMES)) != 12:
        raise RuntimeError('Exact original twelve selectors required')
    Path('/work/home').mkdir()
    foundation.remaining_processes('processes-before.json')
    results = []
    try:
        for index, name in enumerate(NAMES):
            results.append(run_case(foundation, name, index))
            if any(path.stat().st_size > foundation.LIMIT for path in foundation.OUTPUT.rglob('*') if path.is_file()):
                raise RuntimeError('Evidence file bound exceeded')
        foundation.verify_files()
        foundation.remaining_processes('processes-after.json')
    except BaseException as error:
        foundation.save('product-outcome.json', dict(outcome='FAIL_OR_INCONCLUSIVE',
            completedCases=results, error=type(error).__name__, message=str(error),
            infrastructureContainment='NOT_YET_ATTEMPTED', oldFailureSuperseded=False))
        raise
    foundation.save('product-outcome.json', dict(outcome='PASS', cases=results,
        exactTwelve=True, completeNativeSummaries=12, strictPreTeardownCensus=True,
        observerChangesTiming=True, oldFailureSuperseded=False))
    foundation.save('completed.json', dict(actualSelectedCases=12, nativeCommands=12,
        completeNativeSummaries=12, selectedSkipped=0, strictRemainingProcesses=0,
        realJava=False, abruptParentCertified=False, freshSuccessorDirectAssurance='OWED'))


if __name__ == '__main__':
    main()

"""One canonical signal test, with strict census before independent host containment."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import resource
import signal
import subprocess
import sys

foundation_path = Path('/input/foundation.py')
if hashlib.sha256(foundation_path.read_bytes()).hexdigest() != '907d41101cf1ce465a746a762e2b1233db1845ba186b5c0ced94fd0eec17ef6e':
    raise RuntimeError('Frozen preparation/guard differs before import')
spec = importlib.util.spec_from_file_location('_frozen_signal_foundation', foundation_path)
foundation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(foundation)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--extract', action='store_true')
    parser.add_argument('--signal', choices=['SIGINT', 'SIGTERM'], required=True)
    args = parser.parse_args()
    if sys.platform != 'linux' or os.getpid() != 1:
        raise RuntimeError('Original private Linux Python PID1 topology only')
    if args.extract:
        foundation.extract()
        return
    resource.setrlimit(resource.RLIMIT_FSIZE, (foundation.LIMIT, foundation.LIMIT))
    manifest = foundation.verify_files()
    Path('/work/home').mkdir()
    foundation.remaining_processes('processes-before.json')
    name = 'POSIX janitor closes before controlled ' + args.signal + ' exit with no output'
    if manifest['names'] != [name.replace(args.signal, value) for value in ['SIGINT', 'SIGTERM']]:
        raise RuntimeError('Exact finite canonical names differ')
    argv = [str(foundation.NODE), '--disable-warning=ExperimentalWarning', '--test',
        '--test-reporter=tap', '--test-concurrency=1', '--test-name-pattern=^' + re.escape(name) + '$',
        'test/cli.test.ts']
    env = foundation.environment()
    env['STATECHART_JANITOR_PROOF'] = '/output/janitor-events'
    Path(env['STATECHART_JANITOR_PROOF']).mkdir()
    foundation.save('canonical.argv.json', dict(argv=argv, cwd=str(foundation.SCRIPTS), environment=env,
        selectedNames=[name], readinessSeconds=10, selectedCloseSeconds=10, failedCleanupSeconds=5,
        nativeEmergencySeconds=30, runnerEmergencySeconds=65, runnerDirectReapSeconds=5,
        fileLimitBytes=foundation.LIMIT, realJava=False, canonicalTest=True))
    out, err = foundation.OUTPUT / 'canonical.stdout', foundation.OUTPUT / 'canonical.stderr'
    failure, reaped, child = None, False, None
    try:
        with out.open('xb') as stdout, err.open('xb') as stderr:
            child = subprocess.Popen(argv, cwd=foundation.SCRIPTS, env=env, stdout=stdout,
                stderr=stderr, start_new_session=True)
            initial = Path('/proc', str(child.pid), 'stat').read_text()
            start = initial.rsplit(') ', 1)[1].split()[19]
            foundation.save('canonical.owned.json', dict(pid=child.pid, pgid=os.getpgid(child.pid), stat=initial))
            try:
                child.wait(timeout=65)
                reaped = True
            except subprocess.TimeoutExpired as error:
                failure = error
            finally:
                if child.poll() is None:
                    current = Path('/proc', str(child.pid), 'stat').read_text().rsplit(') ', 1)[1].split()
                    if current[19] != start or int(current[1]) != os.getpid() or os.getpgid(child.pid) != child.pid:
                        raise RuntimeError('Direct runner identity uncertain; host containment required')
                    os.killpg(child.pid, signal.SIGKILL)
                try:
                    child.wait(timeout=5)
                    reaped = True
                except subprocess.TimeoutExpired as error:
                    failure = failure or error
    except BaseException as error:
        failure = failure or error
    remaining = foundation.census()
    foundation.save('processes-after.json', remaining)
    strict_clean = not any(row['pid'] != os.getpid() for row in remaining)
    foundation.save('canonical.result.json', dict(exit=child.returncode if child else None,
        directRunnerReaped=reaped, strictCensusClean=strict_clean, failure=str(failure) if failure else None,
        productCloseNotInferredFromRunnerReap=True, infrastructureContainment='NOT_YET_ATTEMPTED'))
    try:
        if failure:
            raise failure
        if not reaped or not strict_clean:
            raise RuntimeError('Strict guard FAIL: every residual live/zombie PID remains rejecting')
        if child.returncode != 0 or err.stat().st_size:
            raise RuntimeError('Canonical native test FAIL/inconclusive; no retry')
        text = out.read_text(encoding='utf-8')
        selected = []
        records = []
        for status, title, directive in re.findall(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', text, re.M):
            records.append(dict(status=status, name=title, directive=directive))
            if title == name:
                if status != 'ok' or directive:
                    raise RuntimeError('Selected canonical case failed/skipped')
                selected.append(title)
            elif status != 'ok' or not directive.startswith('SKIP test name does not match pattern'):
                raise RuntimeError('Unselected test executed/non-ok')
        foundation.save('observed-selection.json', dict(records=records, expectedName=name, expectedCount=1))
        if selected != [name]:
            raise RuntimeError('Exactly one unique native selected ok required')
        if not all(re.search(r'^# ' + label + ' ' + str(value) + '$', text, re.M)
                   for label, value in [('pass', 1), ('fail', 0), ('cancelled', 0), ('todo', 0)]):
            raise RuntimeError('Native summary differs')
        logs = list(Path(env['STATECHART_JANITOR_PROOF']).glob('*/events.jsonl'))
        if len(logs) != 1:
            raise RuntimeError('Exactly one actual CLI lifecycle log required')
        events = [json.loads(line) for line in logs[0].read_text().splitlines()]
        janitors = [row for row in events if row.get('role') == 'janitor' and row['event'] == 'spawn']
        closes = [row for row in events if row.get('role') == 'janitor' and row['event'] == 'close']
        exits = [row for row in events if row['event'] == 'cli-exit']
        if len(janitors) != 1 or len(closes) != 1 or len(exits) != 1:
            raise RuntimeError('Unique janitor spawn/close/CLI exit evidence required')
        if not janitors[0].get('pid') or closes[0]['pid'] != janitors[0]['pid'] or closes[0]['signal'] != 'SIGKILL':
            raise RuntimeError('Exact owned janitor close differs')
        if closes[0]['parent'] != janitors[0]['parent'] or exits[0]['parent'] != janitors[0]['parent']:
            raise RuntimeError('CLI ownership differs')
        if events.index(closes[0]) >= events.index(exits[0]) or exits[0]['code'] != (130 if args.signal == 'SIGINT' else 143):
            raise RuntimeError('Actual close-before-controlled-exit evidence differs')
        # Canonical assertions prove actual selected kill success, null signalCode, exact outputs,
        # stage/tmp absence and drainedJanitor. Fallback cannot reach those assertions after failure.
        foundation.verify_files()
        if any(path.stat().st_size > foundation.LIMIT for path in foundation.OUTPUT.rglob('*') if path.is_file()):
            raise RuntimeError('Evidence file bound exceeded')
    except BaseException as error:
        foundation.save('product-outcome.json', dict(outcome='FAIL_OR_INCONCLUSIVE', signal=args.signal,
            error=type(error).__name__, message=str(error), strictCensusClean=strict_clean,
            infrastructureContainment='NOT_YET_ATTEMPTED', historicalFailureSuperseded=False))
        raise
    foundation.save('product-outcome.json', dict(outcome='PASS', signal=args.signal,
        canonicalSelectedName=name, actualOriginalAssertionsPassed=True, strictCensusClean=True,
        actualCliCloseObserved=True, infrastructureContainment='NOT_YET_ATTEMPTED',
        observerChangesTiming=True, historicalFailureSuperseded=False))
    foundation.save('completed.json', dict(signal=args.signal, selected=1, selectedSkipped=0,
        canonicalOriginalAssertionsPassed=True, strictRemainingProcesses=0, realJava=False,
        historicalFailureSuperseded=False, freshWrapperDirectAssurance='OWED'))


if __name__ == '__main__':
    main()

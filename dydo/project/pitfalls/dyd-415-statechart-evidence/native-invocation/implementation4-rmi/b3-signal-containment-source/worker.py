"""One prospective signal boundary; product outcome and emergency containment are distinct."""
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

FOUNDATION_SHA = '907d41101cf1ce465a746a762e2b1233db1845ba186b5c0ced94fd0eec17ef6e'
foundation_path = Path('/input/foundation.py')
if hashlib.sha256(foundation_path.read_bytes()).hexdigest() != FOUNDATION_SHA:
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
    foundation.verify_files()
    Path('/work/home').mkdir()
    foundation.remaining_processes('processes-before.json')
    packet = foundation.REPO / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-signal-containment-source'
    name = 'diagnose original fs.watch controlled ' + args.signal + ' boundary'
    argv = [str(foundation.NODE), '--disable-warning=ExperimentalWarning', '--test',
        '--test-reporter=tap', '--test-concurrency=1', '--test-name-pattern=^' + re.escape(name) + '$',
        str(packet / 'diagnostic.mjs')]
    env = foundation.environment()
    env['STATECHART_DIAG_SIGNAL'] = args.signal
    foundation.save('diagnostic.argv.json', dict(argv=argv, cwd=str(foundation.REPO), environment=env,
        selectedNames=[name], originalBoundarySeconds=30, cancellationSeconds=[5, 5, 5],
        nativeEmergencySeconds=50, runnerEmergencySeconds=65, runnerDirectReapSeconds=5,
        fileLimitBytes=foundation.LIMIT, realJava=False, handshake=False))
    out, err = foundation.OUTPUT / 'diagnostic.stdout', foundation.OUTPUT / 'diagnostic.stderr'
    failure = None
    reaped = False
    child = None
    try:
        with out.open('xb') as stdout, err.open('xb') as stderr:
            child = subprocess.Popen(argv, cwd=foundation.REPO, env=env, stdout=stdout, stderr=stderr, start_new_session=True)
            initial = Path('/proc', str(child.pid), 'stat').read_text()
            start = initial.rsplit(') ', 1)[1].split()[19]
            foundation.save('diagnostic.owned.json', dict(pid=child.pid, pgid=os.getpgid(child.pid), stat=initial))
            try:
                child.wait(timeout=65)
                reaped = True
            except subprocess.TimeoutExpired as error:
                failure = error
            finally:
                if child.poll() is None:
                    current = Path('/proc', str(child.pid), 'stat').read_text().rsplit(') ', 1)[1].split()
                    if current[19] != start or int(current[1]) != os.getpid() or os.getpgid(child.pid) != child.pid:
                        raise RuntimeError('Direct runner identity uncertain; no kill, host containment required')
                    os.killpg(child.pid, signal.SIGKILL)
                try:
                    child.wait(timeout=5)
                    reaped = True
                except subprocess.TimeoutExpired as error:
                    failure = failure or error
    except BaseException as error:
        failure = failure or error
    # A failed runner/observation still records census; strict residuals remain failures.
    remaining = foundation.census()
    foundation.save('processes-after.json', remaining)
    strict_clean = not any(row['pid'] != os.getpid() for row in remaining)
    fixture = json.loads((Path('/input') / 'input-manifest.json').read_bytes())['generatedViews'][args.signal]
    view = Path(fixture['path'])
    if not view.is_file() or view.is_symlink() or foundation.sha(view) != fixture['sha256'] or view.stat().st_size != fixture['bytes']:
        failure = failure or RuntimeError('Actual scratch waiting-view identity not confirmed')
    foundation.save('diagnostic.result.json', dict(exit=child.returncode if child else None,
        directRunnerReaped=reaped, strictCensusClean=strict_clean,
        failure=str(failure) if failure else None, productCloseNotInferredFromRunnerReap=True,
        infrastructureContainment='NOT_YET_ATTEMPTED'))
    try:
        if failure:
            raise failure
        if not reaped or not strict_clean:
            raise RuntimeError('Strict guard FAIL: every residual live/zombie PID remains rejecting')
        if child.returncode != 0 or err.stat().st_size:
            raise RuntimeError('Diagnostic native FAIL/inconclusive; no retry')
        text = out.read_text(encoding='utf-8')
        observed = re.findall(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', text, re.M)
        records = [dict(status=status, name=title, directive=directive) for status, title, directive in observed]
        foundation.save('observed-selection.json', dict(records=records, expectedName=name, expectedCount=1))
        if records != [dict(status='ok', name=name, directive='')]:
            raise RuntimeError('Exact one unique native ok required, no skips/duplicates/unselected')
        if not all(re.search(r'^# ' + label + ' ' + str(value) + '$', text, re.M)
                   for label, value in [('tests', 1), ('pass', 1), ('fail', 0), ('skipped', 0)]):
            raise RuntimeError('Native summary differs')
        events = [json.loads(line) for line in (foundation.OUTPUT / ('signal-' + args.signal) / 'events.jsonl').read_text().splitlines()]
        if any(row['event'].startswith('cancel-') or row['event'] in ['case-failed-before-cancellation', 'product-close-unconfirmed'] for row in events):
            raise RuntimeError('Cancellation cannot certify product proof')
        if not any(row['event'] == 'original-assertions-passed' for row in events):
            raise RuntimeError('Original assertions not observed')
        if not any(row['event'] == 'cli-direct-close' for row in events):
            raise RuntimeError('Missing actual CLI close; absence/teardown is insufficient')
        foundation.verify_files()
    except BaseException as error:
        foundation.save('product-outcome.json', dict(outcome='FAIL_OR_INCONCLUSIVE', signal=args.signal,
            error=type(error).__name__, message=str(error), strictCensusClean=strict_clean,
            infrastructureContainment='NOT_YET_ATTEMPTED', historicalFailureSuperseded=False))
        raise
    foundation.save('product-outcome.json', dict(outcome='PASS', signal=args.signal,
        originalAssertionsPassed=True, strictCensusClean=True, actualCliCloseObserved=True,
        infrastructureContainment='NOT_YET_ATTEMPTED', historicalFailureSuperseded=False))
    foundation.save('completed.json', dict(signal=args.signal, selected=1, originalAssertionsPassed=True,
        strictRemainingProcesses=0, handshake=False, realJava=False, historicalFailureSuperseded=False))


if __name__ == '__main__':
    main()

"""Future one-signal worker; reuse admitted DATA preparation, no suite or real Java."""
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
    raise RuntimeError('Frozen preparation/guard source differs')
spec = importlib.util.spec_from_file_location('_frozen_signal_foundation', foundation_path)
foundation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(foundation)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--extract', action='store_true')
    parser.add_argument('--signal', choices=['SIGINT', 'SIGTERM'], required=True)
    args = parser.parse_args()
    if sys.platform != 'linux' or os.getpid() != 1:
        raise RuntimeError('Original owned Linux Python PID1 topology only')
    if args.extract:
        foundation.extract()
        return
    foundation.verify_files()
    resource.setrlimit(resource.RLIMIT_FSIZE, (foundation.LIMIT, foundation.LIMIT))
    Path('/work/home').mkdir()
    foundation.remaining_processes('processes-before.json')
    packet = foundation.REPO / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-signal-diagnosis-source'
    name = 'diagnose original fs.watch controlled ' + args.signal + ' boundary'
    argv = [str(foundation.NODE), '--disable-warning=ExperimentalWarning', '--test',
        '--test-reporter=tap', '--test-concurrency=1', '--test-name-pattern=^' + re.escape(name) + '$',
        str(packet / 'diagnostic.mjs')]
    env = foundation.environment()
    env['STATECHART_DIAG_SIGNAL'] = args.signal
    foundation.save('diagnostic.argv.json', dict(argv=argv, cwd=str(foundation.REPO), environment=env,
        selectedNames=[name], deadlineSeconds=65, normalCaseDeadlineSeconds=30,
        cancellationWindowsSeconds=[5, 5, 5], nativeLastResortDeadlineSeconds=50,
        fileLimitBytes=foundation.LIMIT, directRunnerDrainSeconds=5))
    out, err = foundation.OUTPUT / 'diagnostic.stdout', foundation.OUTPUT / 'diagnostic.stderr'
    with out.open('xb') as stdout, err.open('xb') as stderr:
        child = subprocess.Popen(argv, cwd=foundation.REPO, env=env, stdout=stdout, stderr=stderr, start_new_session=True)
        foundation.save('diagnostic.owned.json', dict(pid=child.pid, pgid=os.getpgid(child.pid),
            stat=Path('/proc', str(child.pid), 'stat').read_text()))
        try:
            child.wait(timeout=65)
        finally:
            if child.poll() is None:
                os.killpg(child.pid, signal.SIGKILL)
            child.wait(timeout=5)
            foundation.save('diagnostic.result.json', dict(exit=child.returncode, directRunnerReaped=True))
    # Keep strict census even after an interpreted RED diagnostic; retain every zombie/other PID.
    remaining = foundation.census()
    foundation.save('processes-after.json', remaining)
    text = out.read_text(encoding='utf-8')
    observed = re.findall(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', text, re.M)
    records = [dict(status=status, name=title, directive=directive) for status, title, directive in observed]
    foundation.save('observed-selection.json', dict(records=records, expectedName=name,
        expectedCount=1, rawExit=child.returncode, skippedProof=False))
    if any(row['pid'] != os.getpid() for row in remaining):
        raise RuntimeError('Strict guard FAIL: remaining owned namespace process; independent exact-ID drain required')
    if child.returncode != 0 or err.stat().st_size:
        raise RuntimeError('Signal diagnostic RED/inconclusive: preserve raw evidence, no following case or retry')
    if records != [dict(status='ok', name=name, directive='')]:
        raise RuntimeError('Exact single unique native ok selection not observed')
    if not all(re.search(r'^# ' + label + ' ' + str(value) + '$', text, re.M)
               for label, value in [('tests', 1), ('pass', 1), ('fail', 0), ('skipped', 0)]):
        raise RuntimeError('Native summary differs')
    events_path = foundation.OUTPUT / ('signal-' + args.signal) / 'events.jsonl'
    events = [json.loads(line) for line in events_path.read_text().splitlines()]
    if any(event['event'].startswith('cancel-') or event['event'] == 'case-failed-before-cancellation' for event in events):
        raise RuntimeError('Cancellation can never certify original signal proof')
    if not any(event['event'] == 'original-assertions-passed' for event in events):
        raise RuntimeError('Missing actual original assertion completion')
    foundation.verify_files()
    foundation.save('completed.json', dict(signal=args.signal, selected=1, originalAssertionsPassed=True,
        strictRemainingProcesses=0, handshake=False, realJava=False, historicalFailureSuperseded=False))


if __name__ == '__main__':
    main()

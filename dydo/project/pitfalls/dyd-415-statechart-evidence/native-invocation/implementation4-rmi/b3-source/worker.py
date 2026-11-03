"""Finite B3 groups. Source-only until the exact packet/group has captain release."""
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
import tarfile
import time

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import observe

REPO = Path('/toolchain/repo')
SCRIPTS = REPO / 'skills/engineering/statechart/scripts'
JDK = Path('/toolchain/jdk-21.0.8+9')
NODE = Path('/toolchain/node-v22.23.2-linux-x64/bin/node')
PROOF = REPO / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-source'
HELPER = SCRIPTS / 'java/statechart-tlc-bootstrap.jar'
OUTPUT = Path('/output')
CORPUS_NAMES = ['CounterInstance', 'CounterRules', 'Shadow', 'MediaPlayerProps']
C_NAMES = [name + ' gives the committed chart and report when TLC runs' for name in CORPUS_NAMES] + [
    'a property that breaks on a loop draws the loop and reports it',
    'check() leaves no statechart- folder in the temp dir when it returns and when it throws']
FLAGS = ['-workers', '1', '-fp', '0', '-lncheck', 'final']


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def environment(group):
    result = dict(os.environ)
    for name in ['CLASSPATH', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'NODE_OPTIONS',
                 'STATECHART_UPDATE', 'STATECHART_REPLAY', 'STATECHART_PLAIN', 'STATECHART_RECORD',
                 'STATECHART_LOG', 'JAVA', 'TLA2TOOLS', 'STATECHART_JAVA']:
        result.pop(name, None)
    result.update(TMPDIR='/work/tmp', LC_ALL='C.UTF-8', LANG='C.UTF-8', PYTHONDONTWRITEBYTECODE='1')
    if group in ['B', 'C']:
        result.update(PATH=str(NODE.parent) + ':' + result['PATH'], TLA2TOOLS='/input/tla2tools.jar',
                      JAVA=str(PROOF / 'java_record.py'), STATECHART_JAVA=str(PROOF / 'java_record.py'),
                      STATECHART_B3_JAVA_LOG='/output/java-argv', NODE_OPTIONS='--disable-warning=ExperimentalWarning')
    return result


def invoke(name, argv, group, cwd, passive=False):
    save(OUTPUT / (name + '.argv.json'), dict(argv=argv, cwd=str(cwd), environment=environment(group),
        timeoutSeconds=120, outputLimitBytes=33554432, passive=passive))
    observer = None
    result = dict(exit=None)
    out, err = OUTPUT / (name + '.stdout'), OUTPUT / (name + '.stderr')
    with out.open('xb') as stdout, err.open('xb') as stderr:
        process = subprocess.Popen(argv, cwd=cwd, env=environment(group), stdout=stdout, stderr=stderr, start_new_session=True)
        if passive:
            observer = observe.Passive(process, OUTPUT / (name + '.sockets.jsonl'), sys.byteorder)
            observer.thread.start()
        deadline = time.monotonic() + 120
        try:
            for _ in range(2401):
                if time.monotonic() >= deadline or out.stat().st_size + err.stat().st_size > 33554432:
                    raise RuntimeError('Owned child time/output bound exceeded: ' + name)
                try:
                    result['exit'] = process.wait(timeout=0.05)
                    break
                except subprocess.TimeoutExpired:
                    continue
            if result['exit'] is None:
                raise RuntimeError('Owned child wait bound exceeded: ' + name)
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)
            result['exitAfterOwnedCleanup'] = process.returncode
            try:
                result['observedListeners'] = observer.finish() if observer else []
            except RuntimeError as error:
                result['observerError'] = str(error)
                raise
            finally:
                save(OUTPUT / (name + '.result.json'), result)
    if out.stat().st_size + err.stat().st_size > 33554432:
        raise RuntimeError('Owned child output exceeded limit: ' + name)
    return result, out.read_text(encoding='utf-8'), err.read_text(encoding='utf-8')


def semantic(text, code):
    counts = re.findall(r'([\d,]+) states generated, ([\d,]+) distinct states found', text)
    if not counts:
        raise ValueError('Missing final TLC state counts')
    generated, distinct = [int(value.replace(',', '')) for value in counts[-1]]
    return dict(exit=code, generated=generated, distinct=distinct,
        complete='Model checking completed. No error has been found.' in text,
        invariant=re.findall(r'^Error: Invariant (\S+) is violated\.', text, re.M),
        temporal='Error: Temporal properties were violated.' in text,
        values=[int(value) for value in re.findall(r'^(?:/\\ )?x = (\d+)\s*$', text, re.M)],
        actions=re.findall(r'^State \d+: <([^>]+)>', text, re.M),
        back=[int(value) for value in re.findall(r'^Back to state (\d+)', text, re.M)],
        stuttering=bool(re.search(r'^State \d+: Stuttering', text, re.M)))


def expect(case, facts):
    exits = {'good': 0, 'invariant-bad': 12, 'liveness-bad': 13, 'action-bound': 0}
    if facts['exit'] != exits[case]:
        raise ValueError('Unexpected TLC code for ' + case)
    if case in ['good', 'action-bound']:
        distinct = 4 if case == 'good' else 2
        if not facts['complete'] or facts['distinct'] != distinct or facts['invariant'] or facts['temporal'] or facts['values']:
            raise ValueError('Success/state/action-constraint facts differ: ' + case)
    elif case == 'invariant-bad':
        if facts['invariant'] != ['BelowTwo'] or facts['values'] != [0, 1, 2] or len(facts['actions']) != 3:
            raise ValueError('Missing shortest BelowTwo path0->1->2')
        if not all(action.startswith('Next ') for action in facts['actions'][1:]):
            raise ValueError('Counterexample transition is not Next')
    elif not facts['temporal'] or facts['values'] != [0, 1, 2, 3] or facts['back'] != [1] or facts['stuttering']:
        raise ValueError('Missing fair four-state temporal cycle for NeverFour')


def java_argv(case, variant, directory):
    checker = directory / 'checker/tla2tools.jar'
    main = ['-cp', str(checker), 'tlc2.TLC'] if variant == 'reference' else [
        '-cp', str(HELPER) + ':' + str(checker), 'dydo.statechart.LoopbackTlc']
    return [str(JDK / 'bin/java'), '-Djava.io.tmpdir=' + str(directory / 'tmp'),
        '-DTLA-Library=/input/models', *main, *FLAGS, '-deadlock', '-metadir', str(directory / 'meta'),
        '-config', '/input/models/' + case + '.cfg', '/input/models/Transport.tla']


def group_a():
    results, observations = [], []
    for case in ['good', 'invariant-bad', 'liveness-bad', 'action-bound']:
        pair = []
        for variant in ['reference', 'bootstrap']:
            name = case + '-' + variant
            directory = Path('/work') / name
            (directory / 'checker').mkdir(parents=True)
            (directory / 'tmp').mkdir()
            source = Path('/input/tla2tools.jar')
            target = directory / 'checker/tla2tools.jar'
            target.write_bytes(source.read_bytes())
            if sha(target) != '936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88' or sorted(x.name for x in target.parent.iterdir()) != ['tla2tools.jar']:
                raise ValueError('Checker data identity/membership changed')
            result, stdout, stderr = invoke(name, java_argv(case, variant, directory), 'A', Path('/input/models'), variant == 'bootstrap')
            facts = semantic(stdout + stderr, result['exit'])
            expect(case, facts)
            pair.append(facts)
            observations += result['observedListeners']
            results.append(dict(name=name, facts=facts, sockets=result['observedListeners']))
            save(OUTPUT / (name + '.facts.json'), results[-1])
            if sha(source) != sha(target):
                raise ValueError('Original or isolated checker data mutated')
        if pair[0] != pair[1]:
            raise ValueError('Reference/bootstrap semantic parity failed: ' + case)
    save(OUTPUT / 'group-A.json', dict(invocations=results, count=8, semanticParity=True,
        bootstrapOwnedLoopbackObserved=bool(observations), productionTsIsolationUsed=False))
    if not observations:
        raise RuntimeError('All finite models finished without passive owned-listener evidence; missing proof, no retry')


def node_commands(group):
    base = [str(NODE), '--disable-warning=ExperimentalWarning', '--test', '--test-reporter=tap', '--test-concurrency=1']
    if group == 'B':
        return [('checker', base + ['test/checker.test.ts'], 16),
                ('bootstrap', base + ['test/tlc-bootstrap.test.ts'], 12)]
    pattern = '^(?:' + '|'.join(re.escape(name) for name in C_NAMES) + ')$'
    return [('corpus-selected', base + ['--test-name-pattern=' + pattern, 'test/corpus.test.ts'], 6)]


def selected_tap(text, names):
    observed = []
    for line in text.splitlines():
        match = re.match(r'^(not ok|ok) \d+ - (.*?)(?: # (.*))?$', line)
        if not match:
            continue
        status, name, directive = match.groups()
        if name in names:
            if status != 'ok' or directive:
                raise ValueError('Selected case failed/skipped: ' + name)
            observed.append(name)
        elif not directive or not directive.startswith('SKIP test name does not match pattern'):
            raise ValueError('Unexpected unselected execution: ' + name)
    if sorted(observed) != sorted(names):
        raise ValueError('Exact selected discovery was not observed')
    return observed


def group_node(group):
    if not NODE.is_file():
        raise RuntimeError('Pinned Linux Node22.23.2 is unavailable; no substitution/install')
    (OUTPUT / 'java-argv').mkdir()
    if not os.access(PROOF / 'java_record.py', os.X_OK) or not os.access(SCRIPTS / 'test/java.ts', os.X_OK):
        raise RuntimeError('Pinned recording shims lack executable source-snapshot modes')
    for name, argv, count in node_commands(group):
        result, stdout, stderr = invoke(name, argv, group, SCRIPTS)
        if result['exit'] != 0 or stderr:
            raise RuntimeError('Selected Node command failed: ' + name)
        if group == 'B':
            if not all(re.search(r'^# ' + label + ' ' + str(value) + r'$', stdout, re.M)
                       for label, value in [('tests', count), ('pass', count), ('fail', 0), ('skipped', 0)]):
                raise ValueError('Node discovery/pass/skip counts differ: ' + name)
        else:
            selected_tap(stdout, C_NAMES)
        save(OUTPUT / (name + '.discovery.json'), dict(selected=count, matched=True, skippedSelected=0))
    records = [json.loads(path.read_bytes()) for path in sorted((OUTPUT / 'java-argv').glob('*.json'))]
    if group == 'B' and records:
        raise ValueError('Filesystem/mock group unexpectedly launched real Java')
    if group == 'C':
        if not records or len(records) > 64:
            raise ValueError('Missing or excessive real Java recording')
        if any('tlc2.TLC' in item['argv'] for item in records):
            raise ValueError('Supported TS path bypassed bootstrap')
        if not any('dydo.statechart.LoopbackTlc' in item['argv'] for item in records) or not any('tla2sany.xml.XMLExporter' in item['argv'] for item in records):
            raise ValueError('Missing real TLC/XMLExporter production boundaries')
    save(OUTPUT / ('group-' + group + '.json'), dict(javaProcesses=len(records), productionTsIsolationUsed=True,
        actualJava=group == 'C', installedNativeNpmProof=False))


def group_d():
    for name, count in [('test_statechart_rmi_b2.py', 12), ('test_statechart_rmi_b3.py', 12)]:
        result, stdout, stderr = invoke(name, [sys.executable, '-B', '-m', 'unittest', 'discover', '-v', '-f',
            '-s', str(REPO / 'DynaDocs.Tests/coverage/tests'), '-p', name], 'D', REPO)
        if result['exit'] != 0 or 'Ran ' + str(count) + ' tests' not in stderr or '\nOK\n' not in stderr or 'skipped=' in stderr:
            raise ValueError('Python discovery/result mismatch: ' + name)
        if sum(line.endswith(' ... ok') for line in stderr.splitlines()) != count:
            raise ValueError('Python successful discovery count differs: ' + name)
    save(OUTPUT / 'group-D.json', dict(b2Cases=12, b3Cases=12, java=False, node=False))


def extract(group):
    archives = [('jdk.tar.gz', '/toolchain'), ('site.tar', '/toolchain')]
    if group in ['B', 'C']:
        archives.append(('node.tar.gz', '/toolchain'))
    for name, destination in archives:
        with tarfile.open('/input/' + name) as bundle:
            bundle.extractall(destination, filter='data')


def verify(group):
    inventory = json.loads(Path('/input/snapshot.json').read_bytes())
    for item in inventory:
        if sha(Path('/toolchain', item['member'])) != item['sha256']:
            raise ValueError('Pinned source snapshot changed: ' + item['member'])
    spec = importlib.util.spec_from_file_location('_b3_build_verifier', SCRIPTS / 'java/build.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    lock = json.loads((SCRIPTS / 'java/build-lock.json').read_bytes())
    facts = module.verify_jdk(JDK, Path('/input/jdk.tar.gz'), lock)
    if facts != lock['jdk']['files']:
        raise ValueError('JDK differs from B1c build inputs')
    if group in ['B', 'C']:
        if sha(Path('/input/node.tar.gz')) != 'b294a556e639d64338823920e5866c21c02741742d2e1529ee1a225c1ec9252a':
            raise ValueError('Linux Node archive differs from commissioned dependency lock')
        module.verify_inventory(NODE.parent.parent, Path('/input/node.tar.gz'))
    save(OUTPUT / 'toolchain.json', dict(jdk=facts, group=group, managedJre=False))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--group', choices=['A', 'B', 'C', 'D'], required=True)
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux':
        raise RuntimeError('B3 worker is contained Linux only')
    if args.extract:
        extract(args.group)
        return
    verify(args.group)
    if args.group == 'A':
        group_a()
    elif args.group in ['B', 'C']:
        group_node(args.group)
    else:
        group_d()
    save(OUTPUT / 'group-completed.json', dict(group=args.group, completed=True))


if __name__ == '__main__':
    main()

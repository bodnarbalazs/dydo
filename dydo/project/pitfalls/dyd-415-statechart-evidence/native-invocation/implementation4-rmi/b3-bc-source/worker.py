"""Finite B/C successor, source-only until captain releases the exact packet."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import resource
import signal
import subprocess
import sys
import tarfile
import time

REPO = Path('/toolchain/repo')
SCRIPTS = REPO / 'skills/engineering/statechart/scripts'
JDK = Path('/toolchain/jdk-21.0.8+9')
NODE = Path('/toolchain/node-v22.23.2-linux-x64/bin/node')
# Frozen original recording shim, byte-identical and mode0755 in the snapshot.
PROOF = REPO / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-source'
OUTPUT = Path('/output')
CORPUS_NAMES = ['CounterInstance', 'CounterRules', 'Shadow', 'MediaPlayerProps']
C_NAMES = [name + ' gives the committed chart and report when TLC runs' for name in CORPUS_NAMES] + [
    'a property that breaks on a loop draws the loop and reports it',
    'check() leaves no statechart- folder in the temp dir when it returns and when it throws']


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


def invoke(name, argv, group, cwd):
    save(OUTPUT / (name + '.argv.json'), dict(argv=argv, cwd=str(cwd), environment=environment(group),
        timeoutSeconds=120, outputLimitBytes=33554432))
    result = dict(exit=None)
    out, err = OUTPUT / (name + '.stdout'), OUTPUT / (name + '.stderr')
    with out.open('xb') as stdout, err.open('xb') as stderr:
        process = subprocess.Popen(argv, cwd=cwd, env=environment(group), stdout=stdout, stderr=stderr, start_new_session=True)
        save(OUTPUT / (name + '.owned.json'), dict(pid=process.pid, pgid=os.getpgid(process.pid),
            stat=Path('/proc', str(process.pid), 'stat').read_text(), startNewSession=True))
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
            save(OUTPUT / (name + '.result.json'), result)
    if out.stat().st_size + err.stat().st_size > 33554432:
        raise RuntimeError('Owned child output exceeded limit: ' + name)
    return result, out.read_text(encoding='utf-8'), err.read_text(encoding='utf-8')


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
            expected = json.loads(Path('/input/expectations.json').read_bytes())['B'][name]['names']
            selected_tap(stdout, expected)
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


def process_census():
    rows = []
    for path in sorted(Path('/proc').iterdir(), key=lambda entry: entry.name):
        if not path.name.isdigit():
            continue
        try:
            rows.append(dict(pid=int(path.name), stat=(path / 'stat').read_text(),
                cmdline=(path / 'cmdline').read_bytes().hex()))
        except FileNotFoundError:
            continue
    return rows


def extract(group):
    archives = [('jdk.tar.gz', '/toolchain'), ('site.tar', '/toolchain')]
    if group in ['B', 'C']:
        archives.append(('node.tar.gz', '/toolchain'))
    for name, destination in archives:
        with tarfile.open('/input/' + name) as bundle:
            admitted = json.loads(Path('/input/extraction.json').read_bytes())[name]
            if sha(Path('/input') / name) != admitted['sha256']:
                raise ValueError('Archive changed before extraction: ' + name)
            actual = [dict(name=x.name, bytes=x.size, mode=x.mode, linkname=x.linkname,
                kind='directory' if x.isdir() else 'file' if x.isfile() else 'symlink' if x.issym() else 'unsupported') for x in bundle.getmembers()]
            expected = [{key: row[key] for key in ['name', 'bytes', 'mode', 'linkname', 'kind']} for row in admitted['members']]
            if actual != expected:
                raise ValueError('Archive members differ before extraction: ' + name)
            for row in actual:
                path = PurePosixPath(row['name'])
                if path.is_absolute() or '..' in path.parts or row['kind'] == 'unsupported':
                    raise ValueError('Unsafe extraction member')
            bundle.extractall(destination, filter='data')


def verify(group):
    inventory = json.loads(Path('/input/snapshot.json').read_bytes())
    for item in inventory:
        target = Path('/toolchain', item['member'])
        if target.is_symlink() or sha(target) != item['sha256'] or target.stat().st_mode & 0o111 != item['mode'] & 0o111:
            raise ValueError('Pinned source snapshot changed: ' + item['member'])
    actual = {'repo/' + path.relative_to(REPO).as_posix() for path in REPO.rglob('*') if path.is_file()}
    if actual != {item['member'] for item in inventory}:
        raise ValueError('Extra or missing snapshot file')
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
    parser.add_argument('--group', choices=['B', 'C'], required=True)
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux':
        raise RuntimeError('B/C worker is contained Linux only')
    if args.extract:
        extract(args.group)
        return
    verify(args.group)
    # Inherited by Node, its children and Java; never alter the host's limits.
    resource.setrlimit(resource.RLIMIT_FSIZE, (33554432, 33554432))
    save(OUTPUT / 'processes-before.json', process_census())
    group_node(args.group)
    remaining = process_census()
    save(OUTPUT / 'processes-after.json', remaining)
    if any(row['pid'] != os.getpid() for row in remaining):
        raise RuntimeError('Owned namespace still has another process; retain evidence for independent container drain')
    save(OUTPUT / 'group-completed.json', dict(group=args.group, completed=True))


if __name__ == '__main__':
    main()

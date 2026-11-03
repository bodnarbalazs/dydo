"""Only focused contracts OR the one missing action-bound bootstrap, never reference replay."""
import argparse
import json
from pathlib import Path
import sys
import tarfile

PACKET_PATH = 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi/b3-action-source'
base = None
interpret = None
PACKET = Path('/toolchain/repo') / PACKET_PATH


def load_runtime():
    global base, interpret
    sys.path.insert(0, str(PACKET))
    import interpret as interpretation
    interpret = interpretation
    base = interpret.baseline


def contracts():
    name = 'test_statechart_rmi_action.py'
    argv = [sys.executable, '-B', '-m', 'unittest', 'discover', '-v', '-f',
            '-s', str(base.REPO / 'DynaDocs.Tests/coverage/tests'), '-p', name]
    result, _, stderr = base.invoke(name, argv, 'D', base.REPO)
    if result['exit'] != 0 or 'Ran 12 tests' not in stderr or '\nOK\n' not in stderr or 'skipped=' in stderr:
        raise ValueError('Focused correction-contract discovery/result mismatch')
    if sum(line.endswith(' ... ok') for line in stderr.splitlines()) != 12:
        raise ValueError('Expected exactly twelve actual passed contract tests')
    base.save(base.OUTPUT / 'contracts.json', dict(cases=12, skipped=0, java=False, node=False))


def bootstrap():
    reference = interpret.retained(base.REPO)
    base.save(base.OUTPUT / 'independent-reference-facts.json', reference)
    name = 'action-bound-bootstrap'
    directory = Path('/work') / name
    (directory / 'checker').mkdir(parents=True)
    (directory / 'tmp').mkdir()
    source, target = Path('/input/tla2tools.jar'), directory / 'checker/tla2tools.jar'
    with target.open('xb') as stream:
        stream.write(source.read_bytes())
    identity = dict(sourceSha256=base.sha(source), isolatedSha256=base.sha(target),
                    members=sorted(path.name for path in target.parent.iterdir()))
    base.save(base.OUTPUT / 'checker-data.json', identity)
    if identity != dict(sourceSha256='936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88',
                        isolatedSha256='936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88',
                        members=['tla2tools.jar']):
        raise ValueError('Checker DATA identity/membership differs')
    argv = base.java_argv('action-bound', 'bootstrap', directory)
    command = json.loads((PACKET / 'commands.json').read_bytes())['bootstrap']
    if argv != command['argv']:
        raise ValueError('Missing-case argv differs from inspected command')
    result, stdout, stderr = base.invoke(name, argv, 'A', Path('/input/models'), passive=True)
    facts = base.semantic(stdout + stderr, result['exit'])
    base.save(base.OUTPUT / (name + '.parsed.json'), facts)
    interpret.parse(stdout + stderr, result['exit'], 2)
    if facts != reference['action-bound-reference']:
        raise ValueError('Bootstrap differs from independently retained reference')
    if base.sha(source) != identity['sourceSha256'] or base.sha(target) != identity['isolatedSha256']:
        raise ValueError('Checker DATA changed during invocation')
    observed = result['observedListeners']
    base.save(base.OUTPUT / 'action-completion.json', dict(semanticParity=True, javaInvocations=1,
        referenceReexecuted=False, constraintAdmissionDistinct=2, unconstrainedDistinct=3,
        observedListeners=observed, passiveObservationGap=not bool(observed),
        productionTsNativeIsolationUsed=False, windowsAcceptance=False, javaAssurance='UNAVAILABLE'))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--group', choices=['contracts', 'bootstrap'], required=True)
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    if sys.platform != 'linux':
        raise RuntimeError('Contained Linux worker only')
    if args.extract:
        for name in ['jdk.tar.gz', 'site.tar']:
            with tarfile.open('/input/' + name) as bundle:
                bundle.extractall('/toolchain', filter='data')
        return
    load_runtime()
    base.verify('A')
    if args.group == 'contracts':
        contracts()
    else:
        bootstrap()
    base.save(base.OUTPUT / 'group-completed.json', dict(group=args.group, completed=True))


if __name__ == '__main__':
    main()

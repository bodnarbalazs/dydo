"""Finite Linux-only socket/forwarding proof; source checkpoint, not execution authority."""
import base64
import hashlib
import importlib.util
import ipaddress
import json
import os
from pathlib import Path
import subprocess
import sys

ARGUMENTS = ['', 'space argument', 'árvíztűrő-日本語', '--option-like', '-workers', '1']
MODES = ['bound', 'rmi', 'nonlocal', 'bind-failure', 'existing-factory', 'repeat-install']
ERRORS = {
    'nonlocal': 'java.io.IOException: Statechart RMI rejects a nonlocal endpoint: 192.0.2.1',
    'bind-failure': 'java.net.BindException: Address already in use',
    'existing-factory': 'java.io.IOException: Statechart cannot replace an existing RMI socket factory.',
    'repeat-install': 'java.net.SocketException: factory already defined',
}
JDK = '/toolchain/jdk-21.0.8+9'
HELPER = '/input/helper.jar'
PROBE = '/work/probe-classes'
CONTROL = '/work/control-classes'
FIXTURE = '/input/fixtures/java/dydo/statechart/proof/'


def compiler_commands():
    common = [JDK + '/bin/javac', '--release', '11', '-encoding', 'UTF-8', '-g',
              '-Xlint:all', '-Werror', '-proc:none', '-implicit:none', '-sourcepath', '/work/empty']
    return [common + ['-classpath', HELPER, '-d', PROBE,
                     *[FIXTURE + name + '.java' for name in ['Echo', 'EchoServer', 'SocketProbe']]],
            common + ['-classpath', '/work/empty', '-d', CONTROL, '/input/fixtures/control/tlc2/TLC.java']]


def runtime_commands():
    java = [JDK + '/bin/java', '-cp']
    rows = []
    for mode in MODES:
        classpath = HELPER + ':' + PROBE
        if mode == 'existing-factory':
            classpath += ':' + CONTROL
        rows.append(dict(name=mode, argv=java + [classpath, 'dydo.statechart.proof.SocketProbe', mode],
                         exit=0 if mode in ['bound', 'rmi'] else 1, control=None))
    for value in ['0', '7', 'throw']:
        rows.append(dict(name='forward-' + value,
                         argv=java + [HELPER + ':' + CONTROL, 'dydo.statechart.LoopbackTlc', *ARGUMENTS],
                         exit=1 if value == 'throw' else int(value), control=value))
    return rows


def loopback_address(encoded, byteorder):
    raw = bytes.fromhex(encoded)
    if len(raw) not in [4, 16]:
        raise ValueError('Unknown socket address width')
    if byteorder == 'little':
        raw = b''.join(raw[index:index + 4][::-1] for index in range(0, len(raw), 4))
    address = ipaddress.ip_address(raw)
    return str(address) == '127.0.0.1' or (address.version == 6 and str(address.ipv4_mapped) == '127.0.0.1')


def observe(stdout):
    text = stdout.decode('utf-8', errors='strict')
    lines = text.splitlines()
    required = ['PROCESS ', 'PROC_STAT ', 'FD ', 'TCP ', 'TCP6 ']
    if not all(any(line.startswith(prefix) for line in lines) for prefix in required):
        raise ValueError('Missing dual-table/process/fd socket observation')
    owned = 0
    for line in lines:
        if not line.startswith('LISTEN '):
            continue
        fields = line.split()
        if len(fields) != 5 or fields[1] not in ['tcp', 'tcp6']:
            raise ValueError('Malformed listener observation')
        if not loopback_address(fields[2].split(':')[0], sys.byteorder):
            raise ValueError('Wildcard/nonlocal listener in raw observation')
        if fields[4] == 'owned=true':
            owned += 1
    if not owned:
        raise ValueError('No process-owned loopback listener; missing proof')
    return dict(processOwnedLoopbackListeners=owned, dualTables=True)


def validate(row, code, stdout, stderr):
    if code != row['exit']:
        raise ValueError('Unexpected process exit: ' + row['name'])
    if row['control'] is not None:
        expected = ['CONTROL_TLC_ENTERED ' + str(len(ARGUMENTS))]
        expected += [str(index) + ':' + base64.b64encode(value.encode('utf-8')).decode('ascii')
                     for index, value in enumerate(ARGUMENTS)]
        if stdout != ('\n'.join(expected) + '\n').encode('utf-8'):
            raise ValueError('Forwarded argv order/UTF-8 bytes differ')
        if row['control'] == 'throw':
            if not stderr.startswith(b'CONTROL_STDERR\nException in thread "main" java.lang.IllegalStateException: CONTROL_THROW\n'):
                raise ValueError('Thrown checker error/stderr did not propagate')
        elif stderr != b'CONTROL_STDERR\n':
            raise ValueError('Checker stderr did not propagate')
        return dict(forwardingControl=True, exactArgvBytes=True, stderrPreserved=True)
    if b'CONTROL_TLC_ENTERED' in stdout or b'CONTROL_TLC_ENTERED' in stderr:
        raise ValueError('TLC control entered during socket/factory fixture')
    if row['name'] in ERRORS:
        if stdout or ERRORS[row['name']].encode('utf-8') not in stderr or b'AssertionError' in stderr:
            raise ValueError('Required explicit failure diagnostic missing')
        return dict(expectedFailure=True, diagnostic=ERRORS[row['name']], tlcControlEntered=False)
    marker = b'BOUND_EXCHANGE_OK numeric=41/42 localhost=43/44' if row['name'] == 'bound' else b'RMI_LOCAL_OK '
    if marker not in stdout or stderr:
        raise ValueError('Socket exchange/round trip evidence missing')
    return observe(stdout)


def invoke(output, name, argv, environment):
    (output / (name + '.argv.json')).write_text(json.dumps(dict(argv=argv, cwd='/work',
        control=environment.get('STATECHART_CONTROL_EXIT')), indent=2) + '\n', encoding='utf-8')
    with (output / (name + '.stdout')).open('xb') as stdout, (output / (name + '.stderr')).open('xb') as stderr:
        try:
            result = subprocess.run(argv, cwd='/work', env=environment, stdout=stdout, stderr=stderr, timeout=30)
        except (OSError, subprocess.TimeoutExpired) as error:
            (output / (name + '.result.json')).write_text(json.dumps(dict(error=type(error).__name__, message=str(error))), encoding='utf-8')
            raise
    (output / (name + '.result.json')).write_text(json.dumps(dict(exit=result.returncode)) + '\n', encoding='utf-8')
    return result.returncode, (output / (name + '.stdout')).read_bytes(), (output / (name + '.stderr')).read_bytes()


def main():
    if sys.platform != 'linux':
        raise RuntimeError('B2 worker requires the pinned owned Linux namespace; no desktop Java')
    output = Path('/output')
    pin = json.loads(Path('/input/inputs.json').read_bytes())
    for row in pin['containerInputs']:
        if hashlib.sha256(Path('/input', row['destination']).read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Changed isolated input: ' + row['destination'])
    spec = importlib.util.spec_from_file_location('_b2_jdk_verifier', '/input/build.py')
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    lock = json.loads(Path('/input/build-lock.json').read_bytes())
    facts = verifier.verify_jdk(Path(JDK), Path('/input/jdk.tar.gz'), lock)
    if facts != lock['jdk']['files']:
        raise ValueError('Extracted JDK differs from inspected B1c binaries')
    (output / 'jdk-verified.json').write_text(json.dumps(facts, indent=2) + '\n', encoding='utf-8')
    environment = dict(os.environ)
    for name in ['CLASSPATH', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS', 'STATECHART_CONTROL_EXIT']:
        environment.pop(name, None)
    environment['LANG'] = 'C.UTF-8'
    Path('/work/empty').mkdir()
    for name, argv in zip(['compile-probe', 'compile-control'], compiler_commands()):
        code, stdout, stderr = invoke(output, name, argv, environment)
        if code != 0 or stdout or stderr:
            raise ValueError('Fixture compiler failed/warned; no retry: ' + name)
    members = []
    for directory in [PROBE, CONTROL]:
        for path in sorted(Path(directory).rglob('*')):
            if path.is_file():
                raw = path.read_bytes()
                if path.suffix != '.class' or raw[:4] != b'\xca\xfe\xba\xbe' or int.from_bytes(raw[6:8], 'big') != 55:
                    raise ValueError('Unexpected fixture output')
                members.append(dict(path=str(path), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest(), major=55))
    expected = [PROBE + '/dydo/statechart/proof/' + name + '.class' for name in ['Echo', 'EchoServer', 'SocketProbe']]
    expected.append(CONTROL + '/tlc2/TLC.class')
    if sorted(row['path'] for row in members) != sorted(expected):
        raise ValueError('Unexpected fixture class inventory')
    (output / 'fixture-members.json').write_text(json.dumps(members, indent=2) + '\n', encoding='utf-8')
    results = []
    for row in runtime_commands():
        child = dict(environment)
        if row['control'] is not None:
            child['STATECHART_CONTROL_EXIT'] = row['control']
        code, stdout, stderr = invoke(output, row['name'], row['argv'], child)
        observation = validate(row, code, stdout, stderr)
        results.append(dict(name=row['name'], exit=code, observation=observation))
        (output / (row['name'] + '.verdict.json')).write_text(json.dumps(results[-1], indent=2) + '\n', encoding='utf-8')
    (output / 'finite-result.json').write_text(json.dumps(dict(processes=results, count=9,
        realTlcSemantics=False, managedRuntime=False, windows=False), indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()

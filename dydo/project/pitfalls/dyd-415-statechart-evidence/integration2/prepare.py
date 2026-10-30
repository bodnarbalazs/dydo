"""Declare identities from reviewed Git objects, never from candidate input bytes."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
OUT = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/integration2'
E = 'dydo/project/pitfalls/dyd-415-statechart-evidence/'
REVIEWED = 'b08ab4417aeb7fba61dd2e559379960a79b6b53d'
START = 'a18c2fcdbb9054880f5fa1179fdcf5e6f3309ffe'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def saved(name, value):
    path = OUT / name
    assert not path.exists(), 'Do not overwrite declarations'
    path.write_bytes((json.dumps(value, indent=2) + '\n').encode())


def reference(path):
    return git('show', REVIEWED + ':' + path)


def receipt(path):
    return json.loads(reference(E + path))


def main():
    rows = []
    runtime = receipt('fix4/source-before.json')
    for row in runtime:
        if row['path'].startswith('skills/engineering/statechart/'):
            rows.append(dict(group='runtime', location='source', **row,
                provenance=E + 'fix4/source-before.json@' + REVIEWED))
    for name in ['Directory.Build.props', '.editorconfig', 'DynaDocs.Tests/coverage/SonarLint.xml']:
        row = next(r for r in receipt('fix2/metric-tool-sources.json') if r['path'] == name)
        rows.append(dict(group='configuration', location='source', **row,
            provenance=E + 'fix2/metric-tool-sources.json@' + REVIEWED))
    inputs = ['extraction.json', 'StatechartJob.cs.exact', 'Adapter.csproj.template',
        'Adapter.Tests.csproj.template', 'AdapterTests.cs.template', 'native-selection.json']
    inputs += ['metric-tool-sources.json']
    for name in inputs:
        path = E + 'fix2/' + name
        rows.append(dict(group='native-input', location='source', path=path,
            sha256=sha(reference(path)), provenance=path + '@' + REVIEWED))
    # Pin the retained execution/report recipes and lock/tool receipts as inputs too.
    for folder, names in [('fix3', ['driver.py', 'finalize.py', 'report.py',
            'adapter-packages.lock.json', 'tests-packages.lock.json', 'compiler-binary.json',
            'metric-tool-reuse.json', 'coverlet-package.json', 'coverlet-payload-identity.json',
            'compiler-settings.stdout', 'native-identity-join.json', 'native-summary.json']),
            ('fix4/recipe', ['config.py', 'driver.py', 'finalize.py', 'report.py'])]:
        for name in names:
            path = E + folder + '/' + name
            rows.append(dict(group='proof-input', location='source', path=path,
                sha256=sha(reference(path)), provenance=path + '@' + REVIEWED))
    for row in receipt('fix2/metric-tool-sources.json'):
        rows.append(dict(group='metric-tool', location='metric', **row,
            provenance=E + 'fix2/metric-tool-sources.json@' + REVIEWED))
    for path, expected, origin in [
        ('metrics/bin/Release/net10.0/GateMetrics.dll', receipt('fix3/metric-tool-reuse.json')['sha256'],
            'fix3/metric-tool-reuse.json')]:
        rows.append(dict(group='metric-tool', location='metric', path=path, sha256=expected,
            provenance=E + origin + '@' + REVIEWED))
    compiler = receipt('fix3/compiler-binary.json')
    rows.append(dict(group='compiler', location='absolute', path=compiler['path'],
        sha256=compiler['sha256'], provenance=E + 'fix3/compiler-binary.json@' + REVIEWED))
    package = receipt('fix3/coverlet-package.json')
    rows.append(dict(group='instrumenter', location='absolute', path=package['path'],
        sha256=package['sha256'], provenance=E + 'fix3/coverlet-package.json@' + REVIEWED))
    for row in receipt('fix3/coverlet-payload-identity.json')['files']:
        rows.append(dict(group='instrumenter', location='absolute', path=row['installed'],
            sha256=row['sha256'], provenance=E + 'fix3/coverlet-payload-identity.json@' + REVIEWED))
    saved('dependencies.json', dict(reference=REVIEWED,
        runtimePin='0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6',
        runtimeTree='10e4fcb5e60d0fbb99f559b50df120bf90a7ef3d',
        classes=sorted({r['group'] for r in rows}), files=rows,
        limit='Preflight applicability only; no measurement or new toolchain equivalence claim'))
    history = []
    for folder in ['integration1', 'fix1', 'fix2', 'fix3', 'fix4']:
        for path in git('ls-tree', '-r', '--name-only', START, '--', E + folder).decode().splitlines():
            original = git('show', START + ':' + path)
            observed = (ROOT / path).read_bytes()
            assert observed == original, path
            history.append(dict(path=path, bytes=len(original), sha256=sha(original),
                workingSha256=sha(observed)))
    saved('historical-hashes.json', dict(reference=START, files=history))
    diagnosis = []
    for path in sorted((ROOT / E / 'integration1').iterdir()):
        if not path.is_file():
            continue
        raw = path.read_bytes()
        lines = raw.split(b'\n')
        real = [i for i, line in enumerate(lines, 1)
            if re.search(rb'[ \t]+$', line.removesuffix(b'\r'))]
        diagnosis.append(dict(path=path.name, bytes=len(raw), sha256=sha(raw),
            crlf=raw.count(b'\r\n'), realSpaceOrTabAtEolLines=real))
    failed = (ROOT / E / 'integration1/whitespace.stdout').read_bytes()
    saved('diagnosis.json', dict(files=diagnosis,
        historicalDiagnostics=failed.count(b': trailing whitespace.'),
        hypothesis='CR-at-EOL is treated as trailing whitespace under inherited -text; raw diagnostic echo also preserves whitespace',
        noHistoricalGateRerun=True))
    print(json.dumps(dict(dependencies=len(rows), classes=sorted({r['group'] for r in rows}),
        historicalFiles=len(history), diagnosis=diagnosis), indent=2))


if __name__ == '__main__':
    main()

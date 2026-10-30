"""Declare actual payload inputs before building. No downloads, executions or historical writes."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[6]
BASE = '7ba426604d7870f6596b2946c3255d1604618bfe'
HERE = Path(__file__).resolve().parent


def sha(data):
    return hashlib.sha256(data).hexdigest()


def payload_inputs(root):
    paths = ['setup-skills.mjs']
    skill = root / 'skills/engineering/statechart'
    paths += sorted(path.relative_to(root).as_posix() for path in skill.rglob('*')
        if path.is_file() and not any(part in ['test', 'corpus', 'node_modules'] for part in path.relative_to(root).parts)
        and path.suffix in ['.ts', '.json', '.ps1', '.md', '.yaml']
        and path.name != 'tsconfig.json')
    paths += ['skills/engineering/statechart/LICENSE', 'skills/engineering/statechart/scripts/statechart']
    rows = []
    for name in sorted(set(paths)):
        data = (root / name).read_bytes()
        rows.append(dict(path=name, resource='Statechart/payload/' + name, bytes=len(data), sha256=sha(data)))
    return rows


def write_items(target, rows):
    project = ET.Element('Project')
    group = ET.SubElement(project, 'ItemGroup')
    for row in rows:
        item = ET.SubElement(group, 'StatechartPayload', Include=row['path'])
        ET.SubElement(item, 'ExpectedHash').text = row['sha256']
        ET.SubElement(item, 'ResourceName').text = row['resource']
        ET.SubElement(group, 'EmbeddedResource', Include=row['path'], LogicalName=row['resource'])
    ET.indent(project)
    ET.ElementTree(project).write(target / 'payload.items.props', encoding='utf-8', xml_declaration=True)


def declare(root, arguments):
    target = root / 'Resources/Statechart'
    rows = payload_inputs(root)
    lock = root / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/commission/dependency-lock.json'
    assert sha(lock.read_bytes()) == '7bdbd8727178911b1b9501285d21c16edbc86ef87b7884433088fb4509c280ad'
    manifest = dict(schema=1, protocol=1, sourceBase=BASE, release='3.5.1',
        upstream='1ba91fc25c0977741dd0e267849e6234b037ed0a', dependencyLockSha256=sha(lock.read_bytes()),
        supportedLegacyAdaptersRetained=True, files=rows)
    if arguments == ['--items-only']:
        retained = json.loads((target / 'payload.json').read_bytes())
        assert retained == manifest, 'Never regenerate expected payload identities from drift'
    else:
        assert not arguments
        target.mkdir(parents=True, exist_ok=True)
        with (target / 'payload.json').open('xb') as output:
            output.write((json.dumps(manifest, indent=2) + '\n').encode())
        with (target / 'dependency-lock.json').open('xb') as output:
            output.write(lock.read_bytes())
    write_items(target, rows)
    return manifest


def main(arguments):
    assert subprocess.check_output(['git', '-c', 'core.longpaths=true', 'rev-parse', 'HEAD'], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip() == BASE
    if arguments == ['--author-installed-flow']:
        target = ROOT / 'Resources/Statechart'
        assert sha((target / 'payload.json').read_bytes()) == '1c4d8e56178134b2be3ed62dd7caa8a52210d99ff599046cfb04b02d7dd93c5c'
        receipt = HERE.parent / 'implementation3/payload-installed-flow'
        receipt.mkdir(parents=True, exist_ok=False)
        for name in ['payload.json', 'payload.items.props', 'dependency-lock.json']:
            with (receipt / ('before-' + name)).open('xb') as output:
                output.write((target / name).read_bytes())
        manifest = json.loads((target / 'payload.json').read_bytes())
        manifest['sourceBase'] = BASE
        manifest['files'] = payload_inputs(ROOT)
        (target / 'payload.json').write_bytes((json.dumps(manifest, indent=2) + '\n').encode())
        write_items(target, manifest['files'])
        with (receipt / 'declaration.json').open('x', encoding='utf-8') as output:
            json.dump(dict(sourceBase=BASE, reason='Authored native invocation, explicit installed-skill binding and single-skill projection before material proof',
                manifestSha256=sha((target / 'payload.json').read_bytes()), files=manifest['files']), output, indent=2)
        print('Authored installed-flow payload:', len(manifest['files']), 'resources')
        return
    if arguments in [['--author-public-flow'], ['--author-worker-flow']]:
        target = ROOT / 'Resources/Statechart'
        worker = arguments == ['--author-worker-flow']
        previous = '17f3e98908e6e28e6aea8a01ac7b851c8c296242276d73997713b9ef907564cc' if worker else 'a82419dbb9a352eeb4cee56de6bee905838df9e9b94be5d8036e2a498b621377'
        assert sha((target / 'payload.json').read_bytes()) == previous
        receipt = HERE / ('payload-worker-flow' if worker else 'payload-public-flow')
        receipt.mkdir(exist_ok=False)
        for name in ['payload.json', 'payload.items.props', 'dependency-lock.json']:
            with (receipt / ('before-' + name)).open('xb') as output:
                output.write((target / name).read_bytes())
        manifest = json.loads((target / 'payload.json').read_bytes())
        manifest['files'] = payload_inputs(ROOT)
        (target / 'payload.json').write_bytes((json.dumps(manifest, indent=2) + '\n').encode())
        write_items(target, manifest['files'])
        with (receipt / 'declaration.json').open('x', encoding='utf-8') as output:
            json.dump(dict(sourceBase=BASE, reason='Explicit authored public-flow payload, before build/proof; historical manifests/receipts retained',
                manifestSha256=sha((target / 'payload.json').read_bytes()), files=manifest['files']), output, indent=2)
        print('Authored public-flow payload before material proof:', len(manifest['files']), 'resources')
        return
    manifest = declare(ROOT, arguments)
    if not arguments:
        with (HERE / 'payload-source-before-build.json').open('x', encoding='utf-8') as output:
            json.dump(dict(base=BASE, manifestSha256=sha((ROOT / 'Resources/Statechart/payload.json').read_bytes()),
                files=manifest['files'], noMaterialExecution=True), output, indent=2)
    print('Declared', len(manifest['files']), 'payload resources')


if __name__ == '__main__':
    main(sys.argv[1:])

"""Build the pinned statechart helper inside the captain-approved Linux boundary only."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import subprocess
import stat
import sys
import tarfile
import zipfile

HERE = Path(__file__).resolve().parent
SOURCES = ['src/dydo/statechart/LoopbackTlc.java', 'src/dydo/statechart/LoopbackSockets.java']
CLASSES = ['dydo/statechart/LoopbackTlc.class', 'dydo/statechart/LoopbackSockets.class']
COMPILER_VIEW = 'byte-exact-exploded-tlc-v1'
MAX_ZIP_ENTRIES = 2048
MAX_ZIP_BYTES = 16 * 1024 * 1024
MAX_ZIP_MEMBER = 4 * 1024 * 1024
MAX_JAR_BYTES = 8 * 1024 * 1024


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def load_lock(root=HERE):
    lock = json.loads((root / 'build-lock.json').read_bytes())
    if (lock.get('schema') != 1 or lock.get('bytecodeMajor') != 55 or lock.get('classes') != CLASSES
            or lock.get('compilerInput', {}).get('representation') != COMPILER_VIEW):
        raise ValueError('Unsupported statechart Java build lock')
    return lock


def validate_sources(root, lock):
    if [row['path'] for row in lock['sources']] != SOURCES:
        raise ValueError('Unexpected statechart Java source list')
    for row in lock['sources']:
        path = root / row['path']
        if path.is_symlink() or not path.is_file() or sha(path) != row['sha256']:
            raise ValueError('Statechart Java source differs from the pre-code lock: ' + row['path'])


def archive_relative(name):
    path = PurePosixPath(name)
    if path.is_absolute() or '\\' in name or '..' in path.parts or len(path.parts) < 1:
        raise ValueError('Unsafe JDK archive member')
    return path.parts[0], Path(*path.parts[1:])


def verify_member(bundle, member, target, jdk):
    if not target.resolve().is_relative_to(jdk.resolve()):
        raise ValueError('Build JDK link escapes its owned root')
    if member.isdir():
        if target.is_symlink() or not target.is_dir():
            raise ValueError('Changed JDK directory')
    elif member.isfile():
        if target.is_symlink() or not target.is_file() or target.stat().st_size != member.size:
            raise ValueError('Changed JDK file: ' + member.name)
        with bundle.extractfile(member) as stream:
            expected_hash = hashlib.file_digest(stream, 'sha256').hexdigest()
        if sha(target) != expected_hash or target.stat().st_mode & 0o111 != member.mode & 0o111:
            raise ValueError('Changed JDK file bytes/execute permissions: ' + member.name)
    elif member.issym():
        if not target.is_symlink() or os.readlink(target) != member.linkname:
            raise ValueError('Changed JDK symbolic link')
    else:
        raise ValueError('Unsupported JDK archive member kind; return the actual member to captain')


def verify_inventory(jdk, archive):
    expected, top, total = set(), None, 0
    with tarfile.open(archive, 'r:gz') as bundle:
        for member in bundle:
            root, relative = archive_relative(member.name)
            name = PurePosixPath(member.name).as_posix()
            top = top or root
            if root != top or name in expected:
                raise ValueError('Unexpected or duplicate JDK archive root/member')
            expected.add(name)
            total += member.size
            if len(expected) > 40000 or member.size < 0 or total > 2 * 1024 * 1024 * 1024:
                raise ValueError('Build JDK archive entry/expanded-size bound exceeded')
            verify_member(bundle, member, jdk / relative, jdk)
    actual = {top + '/' + path.relative_to(jdk).as_posix() for path in jdk.rglob('*')} if top else set()
    if not top or actual != expected - {top}:
        raise ValueError('Extra or missing build JDK entries')


def verify_jdk(jdk, archive, lock):
    if jdk.is_symlink() or archive.is_symlink() or sha(archive) != lock['jdk']['archiveSha256']:
        raise ValueError('Build JDK archive checksum/root mismatch')
    verify_inventory(jdk, archive)
    release = (jdk / 'release').read_text()
    if 'JAVA_RUNTIME_VERSION="21.0.8+9-LTS"' not in release or 'IMPLEMENTOR="Eclipse Adoptium"' not in release:
        raise ValueError('Unexpected build JDK release metadata')
    return {name: sha(jdk / name) for name in ['release', 'bin/javac', 'bin/jar', 'bin/java', 'lib/modules']}


def validate_paths(inputs, outputs):
    if not all(path.is_absolute() for path in [*inputs, *outputs]):
        raise ValueError('Absolute owned build paths required')
    for output in outputs:
        if output.exists() or output.is_symlink():
            raise ValueError('Build outputs must be disjoint and new')
        for other in [*inputs, *[path for path in outputs if path != output]]:
            if output.resolve().is_relative_to(other.resolve()) or other.resolve().is_relative_to(output.resolve()):
                raise ValueError('Overlapping build input/output paths')
    if len({path.resolve() for path in outputs}) != len(outputs):
        raise ValueError('Duplicate build outputs')


def compiler_name(member):
    name = member.orig_filename
    if name != member.filename or not name or '\\' in name or ':' in name or any(ord(char) < 32 or ord(char) == 127 for char in name):
        raise ValueError('Unsafe TLC member name')
    relative = name[:-1] if member.is_dir() else name
    if any(part in ('', '.', '..') or part.rstrip(' .') != part for part in relative.split('/')):
        raise ValueError('Unsafe TLC member path')
    mode = stat.S_IFMT(member.external_attr >> 16)
    allowed = (0, stat.S_IFDIR) if member.is_dir() else (0, stat.S_IFREG)
    if mode not in allowed or member.flag_bits & 1:
        raise ValueError('Linked, special or encrypted TLC member')
    if member.file_size < 0 or member.file_size > MAX_ZIP_MEMBER or (member.is_dir() and member.file_size):
        raise ValueError('TLC member size bound exceeded')
    return relative


def register_compiler_path(tree, spellings, name, kind):
    key = name.casefold()
    if key in spellings and spellings[key] != name:
        raise ValueError('Case-colliding TLC member paths')
    if name in tree and tree[name] != kind:
        raise ValueError('TLC file/directory path conflict')
    spellings[key], tree[name] = name, kind


def compiler_tree(rows):
    tree, spellings = {}, {}
    for row in rows:
        path = PurePosixPath(row['name'].removesuffix('/'))
        for parent in reversed(path.parents):
            if parent != PurePosixPath('.'):
                register_compiler_path(tree, spellings, parent.as_posix(), 'directory')
        register_compiler_path(tree, spellings, path.as_posix(), row['kind'])
    return tree


def compiler_members(jar, expected_sha):
    if jar.is_symlink() or not jar.is_file():
        raise ValueError('Pinned TLC compile input is not a plain file')
    with jar.open('rb') as stream:
        raw = stream.read(MAX_JAR_BYTES + 1)
    if len(raw) > MAX_JAR_BYTES or hashlib.sha256(raw).hexdigest() != expected_sha:
        raise ValueError('Pinned TLC compile input changed or exceeds archive bound')
    rows, contents, total = [], {}, 0
    with zipfile.ZipFile(io.BytesIO(raw)) as bundle:
        members = bundle.infolist()
        if not members or len(members) > MAX_ZIP_ENTRIES:
            raise ValueError('TLC archive member-count bound exceeded')
        for member in members:
            compiler_name(member)
            if member.filename in contents:
                raise ValueError('Duplicate TLC archive member')
            total += member.file_size
            if total > MAX_ZIP_BYTES:
                raise ValueError('TLC expanded-size bound exceeded')
            data = bundle.read(member)
            if len(data) != member.file_size:
                raise ValueError('TLC archive member length changed')
            contents[member.filename] = data
            rows.append(dict(name=member.filename, kind='directory' if member.is_dir() else 'file',
                bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
    rows.sort(key=lambda row: row['name'])
    compiler_tree(rows)
    return rows, contents


def validate_compiler_members(rows, lock):
    expected = lock['compilerInput']
    canonical = json.dumps(rows, sort_keys=True, separators=(',', ':')).encode('utf-8')
    observed = dict(representation=COMPILER_VIEW, memberCount=len(rows),
        classCount=sum(row['name'].endswith('.class') for row in rows), expandedBytes=sum(row['bytes'] for row in rows),
        membersSha256=hashlib.sha256(canonical).hexdigest())
    if observed != {key: expected[key] for key in observed}:
        raise ValueError('TLC compiler members differ from the independently pinned inventory')


def verify_compiler_view(root, rows):
    if root.is_symlink() or not root.is_dir():
        raise ValueError('TLC compiler view is not a plain directory')
    expected = compiler_tree(rows)
    files = {row['name']: row for row in rows if row['kind'] == 'file'}
    actual = set()
    for path in root.rglob('*'):
        name = path.relative_to(root).as_posix()
        actual.add(name)
        if path.is_symlink() or name not in expected:
            raise ValueError('Extra or linked TLC compiler member')
        if expected[name] == 'directory':
            if not path.is_dir(): raise ValueError('Changed TLC compiler directory')
        elif not path.is_file() or path.stat().st_size != files[name]['bytes'] or sha(path) != files[name]['sha256']:
            raise ValueError('Changed TLC compiler member: ' + name)
    if actual != set(expected):
        raise ValueError('Missing TLC compiler member')


def prepare_compiler_view(jar, root, lock):
    validate_paths([jar], [root])
    rows, contents = compiler_members(jar, lock['tlc']['sha256'])
    validate_compiler_members(rows, lock)
    tree = compiler_tree(rows)
    root.mkdir(mode=0o700, exist_ok=False)
    directories = [name for name in tree if tree[name] == 'directory']
    for name in sorted(directories, key=lambda name: (name.count('/'), name)):
        (root / name).mkdir(exist_ok=False)
    for name, data in contents.items():
        if not name.endswith('/'):
            with (root / name).open('xb') as stream:
                stream.write(data)
    verify_compiler_view(root, rows)
    return rows


def compiler_argv(jdk, compiler_view, classes, root=HERE):
    return [str(jdk / 'bin/javac'), '--release', '11', '-encoding', 'UTF-8', '-g', '-Xlint:all', '-Werror',
        '-proc:none', '-classpath', str(compiler_view), '-d', str(classes), *[str(root / name) for name in SOURCES]]


def jar_argv(jdk, classes, output):
    return [str(jdk / 'bin/jar'), '--create', '--file', str(output), '--no-manifest',
        '--date=2026-01-01T00:00:00Z', *[part for name in CLASSES for part in ['-C', str(classes), name]]]


def child_environment():
    result = dict(os.environ)
    for key in ['CLASSPATH', 'JAVA_TOOL_OPTIONS', 'JDK_JAVA_OPTIONS', '_JAVA_OPTIONS']:
        result.pop(key, None)
    return result


def invoke(argv, cwd, name):
    with (cwd / (name + '.command.json')).open('x', encoding='utf-8') as stream:
        json.dump(dict(argv=argv), stream, indent=2)
    with (cwd / (name + '.stdout')).open('xb') as output, (cwd / (name + '.stderr')).open('xb') as errors:
        try:
            result = subprocess.run(argv, cwd=cwd, env=child_environment(), stdout=output, stderr=errors, timeout=120)
        except (OSError, subprocess.TimeoutExpired) as error:
            with (cwd / (name + '.result.json')).open('x', encoding='utf-8') as stream:
                json.dump(dict(error=type(error).__name__, message=str(error)), stream, indent=2)
            raise
    with (cwd / (name + '.result.json')).open('x', encoding='utf-8') as stream:
        json.dump(dict(exit=result.returncode), stream, indent=2)
    if result.returncode != 0:
        raise RuntimeError(name + ' failed; retain outputs, do not retry blindly')


def build_once(jdk, compiler_view, work, compiler_rows):
    work.mkdir(exist_ok=False)
    classes = work / 'classes'
    classes.mkdir()
    verify_compiler_view(compiler_view, compiler_rows)
    invoke(compiler_argv(jdk, compiler_view, classes), work, 'javac')
    actual = sorted(path.relative_to(classes).as_posix() for path in classes.rglob('*') if path.is_file())
    if actual != sorted(CLASSES):
        raise ValueError('Extra generated classes need a new finite captain pin')
    hashes = {}
    for name in CLASSES:
        raw = (classes / name).read_bytes()
        if raw[:4] != bytes.fromhex('cafebabe') or int.from_bytes(raw[6:8], 'big') != 55:
            raise ValueError('Expected Java11 class-file major55')
        hashes[name] = sha(classes / name)
    jar = work / 'statechart-tlc-bootstrap.jar'
    invoke(jar_argv(jdk, classes, jar), work, 'jar')
    with zipfile.ZipFile(jar) as bundle:
        if sorted(bundle.namelist()) != sorted(CLASSES):
            raise ValueError('Unexpected bootstrap jar members')
    return jar, hashes


def validate_artifact_identity(lock, raw):
    artifact = lock['artifact']
    if artifact['state'] == 'unbuilt' and artifact['sha256'] is None:
        return
    if artifact['state'] != 'sealed-build' or artifact['sha256'] != hashlib.sha256(raw).hexdigest():
        raise ValueError('Build differs from the separately sealed artifact identity')


def build(arguments):
    if sys.platform != 'linux':
        raise RuntimeError('Build only inside the captain-released isolated Linux boundary; never host javac')
    lock = load_lock()
    validate_sources(HERE, lock)
    jdk, archive, tlc, work, output, receipt = map(Path,
        [arguments.jdk, arguments.jdk_archive, arguments.tlc, arguments.work, arguments.output, arguments.receipt])
    validate_paths([HERE, jdk, archive, tlc], [work, output, receipt])
    if tlc.is_symlink() or sha(tlc) != lock['tlc']['sha256']:
        raise ValueError('Pinned TLC compile input changed')
    toolchain = verify_jdk(jdk, archive, lock)
    work.mkdir(exist_ok=False)
    compiler_view = work / 'compiler-tlc'
    compiler_rows = prepare_compiler_view(tlc, compiler_view, lock)
    with (work / 'compiler-input.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(schema=1, jarSha256=sha(tlc), representation=COMPILER_VIEW,
            path=str(compiler_view), members=compiler_rows, jdkFiles=toolchain,
            compiler=compiler_argv(jdk, compiler_view, work / 'first/classes')), stream, indent=2)
        stream.write('\n')
    first, first_classes = build_once(jdk, compiler_view, work / 'first', compiler_rows)
    second, second_classes = build_once(jdk, compiler_view, work / 'second', compiler_rows)
    if first.read_bytes() != second.read_bytes() or first_classes != second_classes:
        raise ValueError('Bootstrap byte reproducibility failed')
    raw = first.read_bytes()
    validate_artifact_identity(lock, raw)
    with output.open('xb') as stream:
        stream.write(raw)
    record = dict(schema=1, state='built-not-runtime-validated', sources=lock['sources'],
        buildLockSha256=sha(HERE / 'build-lock.json'), buildRecipeSha256=sha(Path(__file__)),
        jdkArchiveSha256=sha(archive), jdkFiles=toolchain, tlcSha256=sha(tlc),
        compiler=compiler_argv(jdk, compiler_view, work / 'first/classes'),
        compilerInputSha256=sha(work / 'compiler-input.json'), compilerMembers=compiler_rows,
        packaging=jar_argv(jdk, work / 'first/classes', first),
        classes=first_classes, members=sorted(CLASSES), artifactSha256=sha(output), artifactBytes=len(raw),
        twoFreshBuildsByteIdentical=True, runtimeProof=False)
    with receipt.open('x', encoding='utf-8') as stream:
        json.dump(record, stream, indent=2)
        stream.write('\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['jdk', 'jdk-archive', 'tlc', 'work', 'output', 'receipt']:
        parser.add_argument('--' + name, required=True)
    build(parser.parse_args())


if __name__ == '__main__':
    main()

"""One-off source-data inventory, commands and custody writer. No proof-module imports."""
import ast
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f')
TEMP = Path('C:/Users/User/AppData/Local/Temp')
BASE = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation4-rmi'
IMAGE = 'python@sha256:6a9220edda062e9223ea0784740a535987299420b96fbfd520b89bbd3e772485'
HEAD = '6e3fe7e8018130af6b8586ef0cf40c1474740864'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def row(path):
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=path.stat().st_size, sha256=sha(path))


def save(path, value):
    with path.open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def counterpart_rows(packet, slug):
    result = []
    for name, suffix in [('driver.py', 'driver'), ('launcher.py', 'launch')]:
        target = TEMP / ('dyd415-rmi-' + slug + '-' + suffix + '-20261031.py')
        assert target.read_bytes() == (packet / name).read_bytes()
        result.append(dict(path=str(target), bytes=target.stat().st_size, sha256=sha(target), source=row(packet / name)))
    return result


def commands(packet, slug):
    manifest = json.loads((packet / 'input-manifest.json').read_bytes())
    value = dict(schema=1, state='SOURCE_ONLY_NOT_RELEASED',
        launcherArgv=['python', '-B', str(TEMP / ('dyd415-rmi-' + slug + '-launch-20261031.py'))],
        driverArgv=['same-approved-host-python', '-B', str(TEMP / ('dyd415-rmi-' + slug + '-driver-20261031.py'))],
        sourceCounterparts=counterpart_rows(packet, slug), image=IMAGE,
        commonContainment=row(BASE / 'b3-source/driver.py'),
        resources=dict(lc='C:/Users/User/.t3/worktrees/LC/t3-f7ffe8ea',
            namespace='C:/Users/User/AppData/Local/lc-test/suite-slot', prefix='LC-Suite-Slot', ticket='LC_SUITE_SLOT_TICKET',
            order=['default live suite_slot.book FIFO', 'nonwaiting Heavy via refuse_heavy_wait',
                'exclusive C:/Users/User/AppData/Local/Temp/dydo-fullsuite.lock'], namespaceSpoof=False,
            actualHostNormalApprovalRequiredForFutureExecution=True),
        containment=dict(network='none', pull='never', rootReadOnly=True, inputReadOnly=True,
            droppedCapabilities=['ALL'], noNewPrivileges=True, pids=128, memoryBytes=1073741824, cpus=2,
            publicPorts=[], hostNamespaces=False, privileged=False, dockerSocket=False, hiddenHostChildren=True),
        imageInspection='Actual cached ID/Linux/amd64 and complete container ID/owner/settings inspection before every start; NOT_RUN here',
        output=(BASE / (slug + '-20261031a')).relative_to(ROOT).as_posix(),
        outerDockerAttachSeconds=1200, ownedDrainSeconds=5,
        cleanup='Inspect exact ID/owner before owned stop/remove; raw absence required, uncertain drain retains TEMP lock; scratch retained as DATA',
        workerArgv=['python', '-B', '/input/worker.py'],
        actualExecution='NOT_RUN', testDiscovery='NOT_RUN', executionReleaseAuthored=False,
        downloads=False, hostRuntimeCopies=False, productionChanges=False)
    if slug == 'b3-lifecycle':
        value.update(workerArgv=[['python', '-B', '/input/worker.py', '--extract'], ['python', '-B', '/input/worker.py']],
            nodeArgv=['/toolchain/node-v22.23.2-linux-x64/bin/node', '--disable-warning=ExperimentalWarning',
                '/toolchain/repo/skills/engineering/statechart/scripts/statechart.ts',
                '/toolchain/repo/skills/engineering/statechart/scripts/test/corpus/Counter/Counter.tla', '--out', '/work/result'],
            cwd='/toolchain/repo/skills/engineering/statechart/scripts', inputFiles=len(manifest['files']),
            containerNames=['dyd415-rmi-b3-lifecycle-NONCE-extract', 'dyd415-rmi-b3-lifecycle-NONCE-finite'],
            toolchainVolume='dyd415-rmi-b3-lifecycle-NONCE-node',
            mounts=['owned input at /input readonly', 'owned work at /work', 'owned output at /output',
                'owned /toolchain volume writable in extraction, readonly for replay'],
            futurePreparation='Proposed contained extraction of only pinned Node bin/node and unchanged source closure. No host runtime or JDK/npm/install. Captain inspection/release required.',
            childSeconds=120, observerIntervalSeconds=0.002, observerSamplesMaximum=60502,
            postExitObservationSeconds=1, hardBytesPerFile=16777216,
            expected=dict(rawCliExit=0, stderrBytes=0,
                report=next(item for item in manifest['files'] if item['path'].endswith('/Counter/report.md')),
                chart=next(item for item in manifest['files'] if item['path'].endswith('/Counter/chart.mmd')),
                stdout='Exact retained report bytes plus newline, Wrote /work/result and newline',
                scratchAbsent=['/work/.result.statechart-*', '/work/tmp/*'],
                namespaceResidue='Any other PID including Z causes worker failure. Unique janitor zombie joins origin, no root-cause claim. Absence/missed sample inconclusive.'),
            environment=dict(PATH='/toolchain/node-v22.23.2-linux-x64/bin:/usr/local/bin:/usr/bin:/bin',
                HOME='/work/home', TMPDIR='/work/tmp', LC_ALL='C.UTF-8', LANG='C.UTF-8',
                JAVA='/toolchain/repo/skills/engineering/statechart/scripts/test/java.ts',
                TLA2TOOLS='/input/tla2tools.jar',
                STATECHART_REPLAY='/toolchain/repo/skills/engineering/statechart/scripts/test/corpus/Counter',
                STATECHART_JAVA='/no-runtime/java-forbidden', STATECHART_LOG='/output/replayed-configs.log',
                NODE_OPTIONS='--disable-warning=ExperimentalWarning'),
            noJava=True, noJdk=True, noCompiler=True, noRecord=True, noInit=True, noSubreaper=True, noInstrumentation=True)
    else:
        value.update(childArgv=['/usr/local/bin/python', '-B', '-m', 'unittest', 'discover', '-v', '-f', '-s',
                '/input/repo/DynaDocs.Tests/coverage/tests', '-p', 'test_statechart_rmi_bc.py'], cwd='/input', inputFiles=4,
            containerNames=['dyd415-rmi-b3-bc-contract-envelope-NONCE-finite'],
            mounts=['owned input at /input readonly', 'owned work at /work', 'owned output at /output'],
            childSeconds=180, hardStdoutBytes=16777216, hardStderrBytes=16777216,
            toolchainVolumes=0, archiveInputs=[], runtimeInputs=[],
            expected=dict(rawExit=0, nativeCount=22, passed=22, skipped=0, names=manifest['names'],
                nativeClass='test_statechart_rmi_bc.RmiBcContracts', stdoutBytes=0, summary=['Ran 22 tests', 'OK'],
                missingOrDuplicateOrUnselectedOrNonOk='FAIL', namespaceResidue='FAIL'),
            producerApplicability='Existing Python producer for actual three B/C modules after captain admits source delta; coverage not launched. Mocks do not measure Java/real OS/runtime.',
            noNode=True, noJava=True, noNetwork=True, noExtraction=True)
    return value


def assurance(packet, slug):
    targets = []
    for name, methods, cases in [
        ('driver.py', ['validate_host', 'verify_inputs', 'execute', 'cleanup'],
            ['namespace/release/HEAD/source refusal before booking', 'exact copies and resource inspection', 'unknown/foreign owner never removed']),
        ('launcher.py', ['validate_host', 'validate_release'],
            ['exact source pin required', 'wrong namespace refused before child/output']),
        ('worker.py', ['census', 'environment', 'verify_files'] if slug == 'b3-lifecycle' else ['verify_inputs', 'interpret'],
            ['input/mode refusal before child', 'exact selected output verdict', 'owned cleanup and strict namespace guard'])]:
        targets.append(dict(module=row(packet / name),
            proposedTestFile='DynaDocs.Tests/coverage/tests/test_statechart_rmi_bc.py',
            proposedFutureDirectCalls=methods, proposedAssertions=cases,
            state='FUTURE_DIRECT_TEST_EXTENSION_NOT_AUTHORED_OR_ASSOCIATED',
            existing22TargetThisWrapper=False, nativeDiscovery='NOT_RUN'))
    if slug == 'b3-lifecycle':
        targets.append(dict(module=row(packet / 'prepare-data.py'), role='One-off source-data preparation, not measurement',
            proposedTestFile='DynaDocs.Tests/coverage/tests/test_statechart_rmi_bc.py',
            proposedFutureDirectCalls=['row', 'counterpart_rows', 'commands'],
            proposedAssertions=['bytes/hash mismatch refuses counterpart', 'exact proposed argv/count/resources'],
            state='FUTURE_TEST_SCOPE_REQUIRED_NOT_AUTHORED_OR_ASSOCIATED'))
    return dict(schema=1, state='PROPOSAL_ONLY_NO_MANIFEST_EDIT',
        baseManifest=row(ROOT / 'DynaDocs.Tests/coverage/test-associations.json'), targets=targets,
        existingAssociations='Three unchanged B/C successor modules have direct real source-grounded tests; those rows never apply to these wrappers',
        implementationScope='Only source packets/TEMP now; actual future extension of existing coverage/tests requires captain source ownership/release',
        noFabricatedAssociation=True, noRecursiveHarness=True,
        coverage='Existing Python stack later; native discovery, 80/60/HCRAP/static obligations owed; no producer launch',
        javaMetrics='UNAVAILABLE_NOT_WAIVED')


def main():
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip() == HEAD
    for folder in ['b3-lifecycle-source', 'b3-bc-contract-envelope-source']:
        packet = BASE / folder
        for name in ['driver.py', 'launcher.py', 'worker.py']:
            ast.parse((packet / name).read_text(encoding='utf-8'))
        slug = folder.removesuffix('-source')
        save(packet / 'commands.json', commands(packet, slug))
        save(packet / 'assurance-proposal.json', assurance(packet, slug))
        save(packet / 'temp-counterparts.json', counterpart_rows(packet, slug))
    print('Source command/assurance data authored; no proof imports/discovery/runtime')


if __name__ == '__main__':
    main()

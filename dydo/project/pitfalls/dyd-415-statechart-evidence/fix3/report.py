"""Join Coverlet's actual schema to native metadata/PDB and Roslyn source facts."""
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def execute(d):
    def read(name):
        return json.loads((d.OUT / name).read_text())

    before = read('native-before.stdout')
    instrumented = read('native-instrumented.stdout')
    extraction = read('extraction.json')
    coverage = read('coverage.json')
    assert len(coverage) == 1
    module_path, documents = next(iter(coverage.items()))
    assert Path(module_path).name == 'Adapter.dll'
    if Path(module_path).is_absolute():
        assert Path(module_path) == d.WORK / 'tests/bin/Debug/net10.0/Adapter.dll'
    assert len(documents) == 1
    document, classes = next(iter(documents.items()))
    assert document in before['documents']
    assert set(classes) == {'StatechartJob'}
    methods = classes['StatechartJob']
    assert len(methods) == len(before['methods']) == 1
    signature, counters = next(iter(methods.items()))
    native = before['methods'][0]
    assert signature == native['identity']
    altered = next(m for m in instrumented['methods'] if m['token'] == native['token'])
    assert altered['identity'] == signature and altered['key'] == native['key']
    assert before['sha256'] != instrumented['sha256']
    assert before['module_id'] == instrumented['module_id']
    point_fields = ['path', 'line', 'column', 'end_line', 'end_column', 'checksum_algorithm', 'checksum']
    points = lambda m: [{k: p[k] for k in point_fields} for p in m['points']]
    assert points(native) == points(altered)
    assert all(p['checksum_algorithm'] == 'SHA256' and
               p['checksum'] == extraction['excerptSha256'] for p in native['points'])
    assert d.sha(d.WORK / 'adapter/StatechartJob.cs') == extraction['excerptSha256']
    lines = {int(k): int(v) for k, v in counters['Lines'].items()}
    branches = counters['Branches']
    source_lines = {p['line'] for p in native['points']}
    assert lines and set(lines) == source_lines and sum(lines.values()) > 0
    assert branches and all(b['Line'] in source_lines for b in branches)
    assert all(b['Hits'] >= 0 and b['Offset'] >= 0 and b['EndOffset'] >= 0 for b in branches)
    assert sum(b['Hits'] for b in branches) > 0
    xml = ET.parse(d.OUT / 'coverage.opencover.xml')
    modules = xml.findall('.//Module')
    assert len(modules) == 1 and modules[0].findtext('ModuleName') == 'Adapter'
    xml_methods = modules[0].findall('.//Method')
    assert len(xml_methods) == 1 and xml_methods[0].findtext('Name') == signature
    xml_token = xml_methods[0].findtext('MetadataToken')
    if xml_token and int(xml_token) != 0:
        assert int(xml_token) == native['token']
    xml_branches = xml_methods[0].findall('.//BranchPoint')
    assert len(xml_branches) == len(branches)
    assert sum(int(b.attrib['vc']) > 0 for b in xml_branches) == sum(b['Hits'] > 0 for b in branches)
    facts = read('roslyn-project.stdout')
    semantic = facts['files'][0]['methods']
    assert len(semantic) == 1 and semantic[0]['member'] == 'Attach'
    method = semantic[0]
    assert min(source_lines) == method['line'] and max(source_lines) == method['end_line']
    old = json.loads((d.OLD / 'roslyn-project.stdout').read_text())['files'][0]['methods'][0]
    assert method == old
    covered_lines = sum(v > 0 for v in lines.values())
    covered_branches = sum(b['Hits'] > 0 for b in branches)
    line_fraction = covered_lines / len(lines)
    branch_fraction = covered_branches / len(branches)
    hcrap = method['policy_cc'] ** 2 * (1 - line_fraction) ** 3 + method['cognitive']
    parity = read('native-test-parity.json')
    counts = {name: {outcome: sum(r['outcome'] == outcome for r in parity[name])
             for outcome in ['Passed', 'Failed', 'NotExecuted']} for name in ['baseline', 'instrumented']}
    d.save('native-identity-join.json', dict(
        reportModule=module_path, document=document, signature=signature, nativeToken=native['token'],
        originalAssemblySha256=before['sha256'], instrumentedAssemblySha256=instrumented['sha256'],
        originalPdbSha256=before['pdb_sha256'], instrumentedPdbSha256=instrumented['pdb_sha256'],
        moduleId=before['module_id'], sourceChecksum=extraction['excerptSha256'],
        sourcePointsEqualIgnoringRewrittenIlOffsets=True, coverletJsonHasNativeToken=False,
        openCoverMetadataToken=xml_token, openCoverModuleHash=modules[0].attrib.get('hash'),
        join='Exact report module path + PDB document/checksum + physical method signature; native token verified before/during instrumentation',
        altcoverJoinerUsed=False, nativeBranchRows=branches,
        sourceLines=[dict(generatedLine=n, powershellLine=n + extraction['sourceStartLine'] - 1,
                         hits=hits) for n, hits in sorted(lines.items())]))
    d.save('native-summary.json', dict(module='Adapter', method=signature,
        token=native['token'], sourceSha256=extraction['excerptSha256'],
        linesCovered=covered_lines, linesTotal=len(lines), lineCoverage=line_fraction,
        branchesCovered=covered_branches, branchesTotal=len(branches), branchCoverage=branch_fraction,
        cyclomatic=method['policy_cc'], cognitive=method['cognitive'], hcrap=hcrap,
        thresholds=dict(line=0.8, branch=0.6, cognitive=20, hcrap=20),
        numericThresholdsSatisfied=line_fraction >= 0.8 and branch_fraction >= 0.6
            and method['cognitive'] <= 20 and hcrap <= 20,
        tests=counts, nativeInstrumentationParity=parity['sameOutcomes']
            and counts['baseline']['Passed'] == counts['instrumented']['Passed'] == 6,
        independentReview='NOT PERFORMED BY THIS CREW',
        powershellLineBranchCcCognitiveHcrap='UNAVAILABLE', changedCodeMutation='UNAVAILABLE'))
    assert line_fraction >= 0.8 and branch_fraction >= 0.6, 'Native coverage floor not met'
    assert method['cognitive'] <= 20 and hcrap <= 20, 'Native cognitive/HCRAP ceiling exceeded'

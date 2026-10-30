import json,hashlib,tarfile,xml.etree.ElementTree as ET
from pathlib import Path
R=Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-b35455fd')
E=R/'dydo/project/pitfalls/dyd-415-statechart-evidence'
S=R/'skills/engineering/statechart'
U=Path('C:/Users/User/AppData/Local/Temp/dyd415-upstream/plugins-1ba91fc25c0977741dd0e267849e6234b037ed0a/pstack/skills/statechart')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
result={}
m=read(E/'fix1/source-manifest.json')
result['source']={'vendored':len(m['vendored_files']),'unchanged':sum(x['vendorIdentical'] for x in m['vendored_files']),'added':len(m['added_files']),
 'vendoredMismatches':[x['path'] for x in m['vendored_files'] if sha(S/x['path'])!=x['candidate_sha256']],
 'upstreamMismatches':[x['path'] for x in m['vendored_files'] if sha(U/x['path'])!=x['upstream_sha256']],
 'addedMismatches':[x['path'] for x in m['added_files'] if sha(S/x['path'])!=x['sha256']]}
result['evidence']={}
for f in ['fix1','fix2','fix3']:
    rows=read(E/f/'proof-manifest.json')
    if isinstance(rows,dict):
        rows=[dict(path=k,sha256=v if isinstance(v,str) else v['sha256']) for k,v in rows.items()]
    result['evidence'][f]={'files':len(rows),'mismatches':[x['path'] for x in rows if sha(E/f/x['path'])!=x['sha256']]}
snap=read(E/'fix1/snapshot.json')['files']
win=read(E/'fix1/windows-source.json')
result['platformSource']={'snapshotFiles':len(snap),'snapshotMismatches':[p for p,v in snap.items() if sha(R/p)!=v['sha256']],
 'windowsFiles':len(win),'windowsMismatches':[p for p,h in win.items() if sha(R/p)!=h]}
walk=read(E/'fix1/walked.json')
def js_slice(s,a,b):return s.encode('utf-16-le')[2*a:2*b].decode('utf-16-le')
def outer(s,methods):
    offset=0;chunks=[]
    for x in sorted(methods,key=lambda m:(m['start'],-m['end'])):
        if x['start']<offset:continue
        chunks.extend([js_slice(s,offset,x['start']),'<callable>']);offset=x['end']
    return ''.join(chunks)+js_slice(s,offset,len(s.encode('utf-16-le'))//2)
scope={x['path'] for x in walk}
changed={x['path'] for x in m['vendored_files'] if not x['vendorIdentical'] and x['path'].endswith('.ts')}|{x['path'] for x in m['added_files'] if x['path'].endswith('.ts')}
result['staticScope']={'measured':len(scope),'changed':len(changed),'missing':sorted(changed-scope),'extra':sorted(scope-changed),'vendorClaims':0,'badVendorClaims':[],'modules':[]}
for row in walk:
    source=(S/row['path']).read_text(encoding='utf-8');old=(U/row['path']).read_text(encoding='utf-8') if (U/row['path']).exists() else ''
    for method in row['methods']:
        if method['vendorIdentical']:
            result['staticScope']['vendorClaims']+=1
            if js_slice(source,method['start'],method['end']) not in old:result['staticScope']['badVendorClaims'].append([row['path'],method['id']])
    result['staticScope']['modules'].append({'path':row['path'],'maintained':sum(not x['vendorIdentical'] for x in row['methods']), 'moduleVendorIdentical':row['moduleVendorIdentical'],'moduleCognitive':row['module']['cognitive']})
result['tsCoverage']=[]
for row in read(E/'fix1/coverage-final-methods.json'):
    local=[x for x in row['methods'] if not x['vendorIdentical']]
    result['tsCoverage'].append({'path':row['path'],'linesCovered':sum(x>0 for x in row['lines'].values()),'lines':len(row['lines']),
       'branchesCovered':sum(x>0 for x in row['branches'].values()),'branches':len(row['branches']),
       'maxHcrap':max(x['hcrap'] for x in local),'maxCognitive':max(x['cognitive'] for x in local)})
result['hiddenLaunches']={'count':0,'notHidden':[]}
for line in (E/'fix1/windows-spawns.jsonl').read_text().splitlines():
    row=json.loads(line);result['hiddenLaunches']['count']+=1
    if row['hidden'] is not True:result['hiddenLaunches']['notHidden'].append(row)
f=E/'fix3';ext=read(f/'extraction.json');src=(R/ext['source']).read_bytes();code=src[ext['excerptByteOffset']:ext['excerptByteOffset']+ext['excerptBytes']]
assert hashlib.sha256(src).hexdigest()==ext['sourceSha256']
assert code==(f/'StatechartJob.cs.exact').read_bytes()==(E/'fix2/StatechartJob.cs.exact').read_bytes()
assert hashlib.sha256(code).hexdigest()==ext['excerptSha256']
assert src[:ext['excerptByteOffset']].count(b'\n')+1==ext['sourceStartLine']
native=read(f/'native-before.stdout');altered=read(f/'native-instrumented.stdout');restored=read(f/'native-restored.stdout');cov=read(f/'coverage.json')
assert native==restored
module,docs=next(iter(cov.items()));doc,classes=next(iter(docs.items()));klass,methods=next(iter(classes.items()));sig,counters=next(iter(methods.items()))
assert doc in native['documents'] and sig==native['methods'][0]['identity']
assert native['module_id']==altered['module_id'] and native['sha256']!=altered['sha256']
keys=['path','line','column','end_line','end_column','checksum_algorithm','checksum']
assert [{k:p[k] for k in keys} for p in native['methods'][0]['points']]==[{k:p[k] for k in keys} for p in altered['methods'][0]['points']]
assert all(p['checksum']==ext['excerptSha256'] for p in native['methods'][0]['points'])
assert {p['line'] for p in native['methods'][0]['points']}=={int(k) for k in counters['Lines']}
trx={}
for campaign in ['baseline','instrumented']:
    tree=ET.parse(f/(campaign+'-results')/(campaign+'.trx'))
    rows=tree.findall('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}UnitTestResult')
    trx[campaign]={'count':len(rows),'outcomes':{r.attrib['testName']:r.attrib['outcome'] for r in rows}}
result['native']={'sourceBytes':len(src),'excerptBytes':len(code),'excerptOffset':ext['excerptByteOffset'],'startLine':ext['sourceStartLine'],'sha256':ext['excerptSha256'],
  'nativeIdentityAndRestoration':True,'lines':[sum(v>0 for v in counters['Lines'].values()),len(counters['Lines'])],
  'branches':[sum(b['Hits']>0 for b in counters['Branches']),len(counters['Branches'])], 'tests':trx}
result['executionReceipts']={}
for receipt,snapshot in [('driver-identity.json','executed-setup-driver.py'),('resume-driver-identity.json','executed-campaign-driver.py')]:
    result['executionReceipts'][receipt]=read(f/receipt)['sha256']==sha(f/snapshot)
result['producerHashes']={name:sha(R/'DynaDocs.Tests/coverage'/name)==h for name,h in read(E/'fix1/producer-hashes.json').items() if (R/'DynaDocs.Tests/coverage'/name).exists()}
out=Path('C:/Users/User/AppData/Local/Temp/dyd415-review-astra-audit.json')
out.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2))

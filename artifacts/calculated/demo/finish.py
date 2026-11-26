from pathlib import Path
import json,re,hashlib,subprocess,datetime,shlex
p=Path('artifacts/calculated/demo');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
r={'frozenImplementation':'c9fd3ed772eadc68119dab56b66b460fa88d73f2','project':'examples/calculated/CalculatedDemo.csproj','legs':[],'normalization':'Compare complete CALCULATED-DEMO result blocks BYTE-for-BYTE; exclude only separate provider/runtime header and build logs. No result rows/values sorted or removed by proof.'}
for v in [10,11]:
 for mode in ['jit','aot']:
  for provider in ['sqlite','postgres']:
   name='jit'+str(v)+'-'+provider if mode=='jit' else 'native'+str(v)
   d=json.loads((p/(name+'.json')).read_text());assert d['exitCode']==0 and not d['inputChanged']
   log=(p/(name+'.log')).read_text();pattern=r'provider='+provider+r' runtime=('+str(v)+r'\.[^\n]+)\n(CALCULATED-DEMO v1\n.*?assertions=18 PASS)'
   matches=re.findall(pattern,log,re.S);assert len(matches)==1,(name,provider)
   version,block=matches[0];r['legs'].append({'sdk':v,'mode':mode,'provider':provider,'runtime':version,'gate':name+'.json','log':name+'.log','result':block,'resultSha256':hashlib.sha256(block.encode()).hexdigest()})
assert len({x['result'] for x in r['legs']})==1
r['binaries']=[]
for v in [10,11]:
 f=p/('native'+str(v)+'-out/CalculatedDemo');r['binaries'].append({'path':str(f.relative_to(p)),'bytes':f.stat().st_size,'sha256':sha(f)})
images=subprocess.run(['docker','image','inspect','orml108-graph-native-sdk10:verified','orml108-graph-native-sdk11:verified','neemle/pg.lite:19'],capture_output=True,text=True,check=True)
r['images']=[{'tags':d['RepoTags'],'id':d['Id']} for d in json.loads(images.stdout)]
r['limits']=['Functional same-demo criterion only, not full item/QA/security/coverage/merge approval.','Full source/tests/goldens/census unchanged; old gates not rerun.','ASCII Lower/null/empty parity only; non-ASCII provider semantics not promised.','Virtual filter/order/projection are tested as preSQL refusals, not supported operations.','No history/soft-delete model in these cases; no new history execution claim.','Private packed frozen107 packages only; no newer108–117 implementation or release.']
cleanup=[]
for kind,file in [('container','postgres-id.txt'),('network','network-id.txt')]:
 i=(p/file).read_text().strip();argv=['docker','rm','-f',i] if kind=='container' else ['docker','network','rm',i]
 x=subprocess.run(argv,capture_output=True,text=True);assert x.returncode==0
 check=['docker','inspect',i] if kind=='container' else ['docker','network','inspect',i]
 y=subprocess.run(check,capture_output=True,text=True);assert y.returncode!=0
 cleanup.append({'kind':kind,'id':i,'argv':argv,'exitCode':x.returncode,'stdout':x.stdout,'absenceArgv':check,'absenceExitCode':y.returncode,'absenceStderr':y.stderr,'time':datetime.datetime.now(datetime.timezone.utc).isoformat()})
r['cleanup']=cleanup
(p/'matrix-results.json').write_text(json.dumps(r,indent=2))
sections=[]
for name in ['pack-frozen107','jit10-sqlite','jit10-postgres','jit11-sqlite','jit11-postgres','native10','native11']:
 d=json.loads((p/(name+'.json')).read_text());sections.append('TWELVE-EIGHT-GATE-EVIDENCE v1 '+name+' ORML-107\n\n'+'\n\n'.join(['**Gate** '+name,'**Command (full argv)**\n```sh\n'+shlex.join(d['argv'])+'\n```','**Working Directory** '+d['cwd'],'**Environment** Exact Docker argv pins images, fixture and private packages; image IDs in matrix-results.json.','**Scope** Same public CalculatedDemo, frozen107 implementation; no old unit/golden/coverage checks.','**Exit Code** '+str(d['exitCode']),'**Commit** '+d['commit']+'; source tree '+d['sourceTree'],'**Generated** Packed analyzer runs on these same models. Native publication plus actual DB execution, not render/map only.','**Run Position** '+d['started']+' -> '+d['finished']+'; functional inputChanged='+str(d['inputChanged']),'**Disposition** PASS for this command only. Full log '+name+'.log.']))
(p/'native-gate-home.md').write_text('\n\n---\n\n'.join(sections)+'\n')
print('8 exact-equal result blocks;18assertions each;2ownresources absent')

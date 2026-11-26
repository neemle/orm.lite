from pathlib import Path
import json,hashlib,re,subprocess,datetime
p=Path('artifacts/relation-loading/review-fixes');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
r={'implementation':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'review':'32ee3026-10f4-4a90-8ab5-44e17700f2c8','legs':[],'reproductions':{'before-fence':'before-publication-fence.json','before-cleanup':'before-cleanup.json','harnessCorrection':'harness-correction.txt','after-fence':'after-publication-fence.json','after-real-generated-mapper':'after-generated-mapper-cleanup.json'}}
for v in [10,11]:
 for mode in ['jit','aot']:
  for provider in ['sqlite','postgres']:
   name='jit'+str(v)+'-'+provider if mode=='jit' else 'native'+str(v)
   d=json.loads((p/(name+'.json')).read_text());assert d['exitCode']==0 and not d['sourceChangedDuringRun']
   log=(p/(name+'.log')).read_text();out='policy smoke '+provider+' assertions=126 PASS';assert log.count(out)==1
   assert 'runtime '+str(v)+'.' in log
   r['legs'].append({'sdk':v,'mode':mode,'provider':provider,'gate':name+'.json','outcome':out,'normalizedOutcome':out.replace(provider,'<provider>'),'logSha256':sha(p/(name+'.log')),'started':d['started'],'finished':d['finished']})
assert len({x['normalizedOutcome'] for x in r['legs']})==1
r['normalization']='Only provider token in final outcome; runtime versions retained in full logs. Same entire public project and scenarios, old cases preserved.'
r['binaries']=[]
for v in [10,11]:
 f=p/('native'+str(v)+'-out/PolicySmoke');r['binaries'].append({'file':str(f.relative_to(p)),'sha256':sha(f),'bytes':f.stat().st_size})
r['cleanup']=[]
for kind,file in [('container','postgres-id.txt'),('network','network-id.txt')]:
 i=(p/file).read_text().strip();args=['docker','rm','-f',i] if kind=='container' else ['docker','network','rm',i]
 x=subprocess.run(args,capture_output=True,text=True);assert x.returncode==0
 check=['docker','inspect',i] if kind=='container' else ['docker','network','inspect',i]
 y=subprocess.run(check,capture_output=True,text=True);assert y.returncode!=0
 r['cleanup'].append({'kind':kind,'id':i,'argv':args,'exitCode':x.returncode,'stdout':x.stdout,'absenceArgv':check,'absenceExitCode':y.returncode,'absenceStderr':y.stderr,'finished':datetime.datetime.now(datetime.timezone.utc).isoformat()})
r['limits']=['Only two review fixes and affected same-demo parity; no full QA/security/coverage/301/release/merge claim.','Initial custom Query mapper was unsupported; retained as harness history, not real-mapper evidence. Final real generated mapper case uses faulting Author.Name setter over actual DB row.','Existing unrelated proof retained; no old unit/golden/census or unaffected compatibility replay.','Private weak secondary-error retention limited to4 per primary, no logging or public exception Data.']
(p/'matrix-results.json').write_text(json.dumps(r,indent=2));print('8same-demo outcomes126PASS;2ownresources absent')

from pathlib import Path
import json,hashlib,re,subprocess,datetime
p=Path('artifacts/relation-loading/dev');sha=lambda f:hashlib.sha256(f.read_bytes()).hexdigest()
r={'sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sourceTree':subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip(),'testTree':subprocess.check_output(['git','rev-parse','HEAD:tests'],text=True).strip(),'consumerInputs':'same-consumer-inputs.json','consumerInputsSha256':sha(p/'same-consumer-inputs.json'),'legs':[]}
for v in [10,11]:
 for mode in ['jit','aot']:
  for provider in ['sqlite','postgres']:
   name='final-runtime'+str(v)+'-'+provider if mode=='jit' else 'final-native'+str(v)
   j=p/(name+'.json');log=p/(name+'.log');d=json.loads(j.read_text());assert d['exitCode']==0 and not d['sourceChangedDuringRun']
   lines=[x for x in log.read_text().splitlines() if x.startswith('policy smoke '+provider+' ')]
   assert len(lines)==1 and lines[0]=='policy smoke '+provider+' assertions=109 PASS'
   r['legs'].append({'sdk':v,'mode':mode,'provider':provider,'outcome':lines[0],'normalizedOutcome':lines[0].replace(provider,'<provider>'),'gate':str(j.relative_to(p)),'gateSha256':sha(j),'logSha256':sha(log),'started':d['started'],'finished':d['finished']})
assert len({x['normalizedOutcome'] for x in r['legs']})==1
r['normalization']='Only provider token is replaced. Runtime version line is separately retained in every full log; assertion count and PASS unchanged.'
r['binaries']=[]
for v in [10,11]:
 f=p/('final-native'+str(v)+'-out/PolicySmoke');r['binaries'].append({'file':str(f.relative_to(p)),'bytes':f.stat().st_size,'sha256':sha(f)})
r['assemblies']=[]
for v in [10,11]:
 for project,name in [('Runtime','Neemle.OrmLite.Runtime.dll'),('Generator','Neemle.OrmLite.Generator.dll')]:
  f=p/('final-native'+str(v)+'-build/bin')/project/'release'/name
  r['assemblies'].append({'file':str(f.relative_to(p)),'bytes':f.stat().st_size,'sha256':sha(f)})
r['limits']=['Same functional demo criterion proven; not whole item/stage/QA PASS.','No current raw coverage measurement; historical coverage FAIL is not relabeled.','No release/merge/public package claim. Project-built analyzer is used, not a newly packed package.','No new SDET tests or protected control/baseline edits. Independent SDET/security/review/docs remain separate.','Unobserved custom externally opened origins intentionally refuse; known custom Open/UoW and current gated leases proven.','Inherited ABI six-parameter OrmExec signatures unchanged; existing controls and full quality gate are not claimed PASS.','User narrowed verification to same-demo matrix; unrelated current-unit run was explicitly stopped, partial results retained as history.']
(p/'final-matrix.json').write_text(json.dumps(r,indent=2))
print('same demo matrix',len(r['legs']),'legs PASS')

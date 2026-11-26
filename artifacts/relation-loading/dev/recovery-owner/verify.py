from pathlib import Path
import hashlib,json,subprocess,datetime
p=Path('artifacts/relation-loading/dev');H=lambda x:hashlib.sha256(x.read_bytes()).hexdigest();G=lambda *a:subprocess.check_output(['git',*a],text=True).strip()
manifest=p/'checkpoint-manifest.json';j=json.loads(manifest.read_text());assert H(manifest)=='cf282a2a0301c2d5dd20a22b863e5b7767144b28cb70e1c6ed2b151113686a3a'
assert G('status','--short')==''
head=G('rev-parse','HEAD');assert head=='c0dbba7f6017e4ae2a39fdfb163a2b178102b843' and G('rev-parse','origin/codex/orml-109-relation-loading')==head
assert G('rev-parse','HEAD:src')=='272fde8b5e4bbd8bf452c96db47583a3de52d5ce'
assert G('rev-parse','HEAD:tests')=='21c166b86ebc3b4b3eeb7405a396cee31ff50f36'
for row in j['files']+j['buildAssemblies']:
 f=Path(row['path']);assert f.stat().st_size==row['size'] and H(f)==row['sha256']
allowed={'src/Runtime/'+x+'.cs' for x in ['Query','OrmContext','UnitOfWork','GraphPlanner','GraphExecutor','GraphReadContext','IGraphNavigationBinding','GraphNavigationBinding','GraphReferenceBinding','GraphCollectionBinding','GraphLinkBinding','OrmExec']}
allowed|={'src/Generator/'+x+'.cs' for x in ['OrmGenerator','EntityReader','EntityEmitter','EntityClassEmitter','LoaderEmitter']}
paths=G('diff','--name-only','61dffff','HEAD','--','src').splitlines();assert len(paths)==67
assert all(x in allowed or x.startswith(('src/Runtime/RelationLoading/','src/Generator/RelationLoading/')) for x in paths)
g=j['compileGate'];assert g['exitCode']==0 and not g['sourceChangedDuringRun'] and g['commit']=='b22f19a593aa243e202ae1381350eaf8e0423e7b'
assert g==json.loads((p/'build-checkpoint-committed.json').read_text())
assert all(label in (p/'build-checkpoint-committed.log').read_text() for label in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
checks=[]
for argv in [['docker','ps','-aq','--filter','label=orml.actor=dev-policy'],['docker','network','ls','-q','--filter','label=orml.task=ORML-109']]:
 r=subprocess.run(argv,text=True,capture_output=True);assert r.returncode==0 and r.stdout.strip()=='';checks.append(dict(argv=argv,exitCode=r.returncode,stdout=r.stdout,stderr=r.stderr))
report=dict(disposition='FINITE RECOVERY SUCCESS — checkpoint reconciliation only, NOT full producer delivery',verifiedCheckpoint=head,sourceTree=G('rev-parse','HEAD:src'),testTree=G('rev-parse','HEAD:tests'),originalManifestSha256=H(manifest),manifestFilesVerified=len(j['files']),assembliesVerified=j['buildAssemblies'],changedSourceCount=len(paths),allPathsContained=True,compileComment='4222173b-ffe4-4123-ae33-2836b5a69428 freshly read in full; compile-only scope retained',compileRecord=g,cleanupAbsence=checks,history='Both initial compile failure logs and source inputs retained unchanged in original23-file manifest',remaining=j['remaining'],noRerun='No build/test/provider/AOT/quality/coverage/security run or source/API edit in recovery',generated=datetime.datetime.now(datetime.timezone.utc).isoformat())
(p/'recovery-owner/verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))

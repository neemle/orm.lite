import datetime,hashlib,json,pathlib,subprocess,sys
P=pathlib.Path('artifacts/graph/current-contract-integration');name=sys.argv[1];argv=sys.argv[2:]
git=lambda *args:subprocess.check_output(['git',*args],text=True).strip()
diff=lambda:hashlib.sha256(subprocess.check_output(['git','diff','--','src','tests','scripts'])).hexdigest()
r={'gate':name,'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':git('rev-parse','HEAD'),'srcTree':git('rev-parse','HEAD:src'),'testTree':git('rev-parse','HEAD:tests'),'status':git('status','--short'),'trackedDiffSha256':diff(),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()}
with (P/(name+'.log')).open('w') as log:r['exitCode']=subprocess.call(argv,stdout=log,stderr=subprocess.STDOUT)
r.update(finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputsChangedDuringRun=diff()!=r['trackedDiffSha256'],commitChangedDuringRun=git('rev-parse','HEAD')!=r['commit']);(P/(name+'.json')).write_text(json.dumps(r,indent=2)+'\n');print(name,r['exitCode'],flush=True);sys.exit(r['exitCode'])

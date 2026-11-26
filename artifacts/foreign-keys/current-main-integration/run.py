import pathlib, subprocess, json, datetime, hashlib, sys
P=pathlib.Path('artifacts/foreign-keys/current-main-integration'); name=sys.argv[1]; argv=sys.argv[2:]
git=lambda *a:subprocess.check_output(['git',*a],text=True).strip()
inputs=lambda:{str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(pathlib.Path('examples/foreign-keys').glob('*')) if p.is_file()}
r={'gate':name,'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':git('rev-parse','HEAD'),'sourceTree':git('rev-parse','HEAD:src'),'testTree':git('rev-parse','HEAD:tests'),'status':git('status','--short'),'demoInputs':inputs(),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()}
with (P/(name+'.log')).open('w') as log:r['exitCode']=subprocess.call(argv,stdout=log,stderr=subprocess.STDOUT)
r.update(finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),demoChangedDuringRun=inputs()!=r['demoInputs'],commitChangedDuringRun=git('rev-parse','HEAD')!=r['commit']);(P/(name+'.json')).write_text(json.dumps(r,indent=2)+'\n');print(name,r['exitCode'],flush=True);sys.exit(r['exitCode'])

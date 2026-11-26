from pathlib import Path
import json,subprocess,sys,datetime,hashlib
r=Path.cwd();p=r/'artifacts/relation-loading/sdet-current-main54';name=sys.argv[1];argv=sys.argv[2:]
def git(*a):return subprocess.check_output(['git',*a],text=True).strip()
def inputs():return dict(srcTree=git('rev-parse','HEAD:src'),trackedDiffSha256=hashlib.sha256(subprocess.check_output(['git','diff','HEAD','--','src','tests'])).hexdigest())
x=dict(name=name,argv=argv,cwd=str(r),workspaceHead=git('rev-parse','HEAD'),productCommit='f5d521ea8ef8f16c377842239e274e384bc49fb8',testsTree=git('rev-parse','HEAD:tests'),inputs=inputs(),status=git('status','--short'),started=datetime.datetime.now(datetime.timezone.utc).isoformat())
z=subprocess.run(argv,capture_output=True,text=True);(p/(name+'.log')).write_text(z.stdout+z.stderr);x.update(exitCode=z.returncode,finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputChanged=inputs()!=x['inputs']);(p/(name+'.json')).write_text(json.dumps(x,indent=2)+'\n');print(json.dumps(dict(name=name,exitCode=z.returncode,tail=(z.stdout+z.stderr)[-6500:])));raise SystemExit(z.returncode)

import pathlib,subprocess,json,datetime,sys,hashlib
P=pathlib.Path('artifacts/graph/demo-current');name=sys.argv[1];a=sys.argv[2:];now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat()
def digest():
 files=[pathlib.Path('demo/Program.cs'),pathlib.Path('demo/OrmLiteDemo.csproj'),*pathlib.Path('demo/Graph').glob('*.cs')];return {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
d={'gate':name,'argv':a,'cwd':str(pathlib.Path.cwd()),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'status':subprocess.check_output(['git','status','--short'],text=True),'srcTree':subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip(),'sourceHashes':digest(),'started':now()};log=P/(name+'.log')
with log.open('w') as f:r=subprocess.run(a,stdout=f,stderr=subprocess.STDOUT)
d.update(finished=now(),exitCode=r.returncode,sourceChangedDuringRun=digest()!=d['sourceHashes'],log=str(log));(P/(name+'.json')).write_text(json.dumps(d,indent=2));print(json.dumps({'gate':name,'exitCode':r.returncode,'tail':log.read_text()[-2500:]}),flush=True);raise SystemExit(r.returncode)

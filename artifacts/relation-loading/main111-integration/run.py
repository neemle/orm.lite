import pathlib,subprocess,json,datetime,hashlib,sys
w=pathlib.Path.cwd();p=w/'artifacts/relation-loading/main111-integration';name=sys.argv[1];argv=sys.argv[2:]
def inputs():
 out={}
 for tree in ['src','examples/relation-loading',str(p.relative_to(w)/'probe')]:
  for f in (w/tree).rglob('*'):
   if f.is_file()and f.suffix in ['.cs','.csproj','.props','.targets','.Config']:out[str(f.relative_to(w))]=hashlib.sha256(f.read_bytes()).hexdigest()
 return out
r=dict(name=name,argv=argv,cwd=str(w),commit=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),status=subprocess.check_output(['git','status','--short'],text=True),started=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputs=inputs())
with(p/(name+'.log')).open('w')as f:r['exitCode']=subprocess.call(argv,stdout=f,stderr=subprocess.STDOUT)
r.update(finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputChanged=inputs()!=r['inputs']);(p/(name+'.json')).write_text(json.dumps(r,indent=2));print(json.dumps({k:v for k,v in r.items()if k not in ['inputs','argv']}));raise SystemExit(r['exitCode'])

import pathlib,subprocess,json,datetime,hashlib,sys
out=pathlib.Path(sys.argv[1]);argv=sys.argv[2:]
def inputs():
 return {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(pathlib.Path('examples/calculated').glob('*')) if p.is_file()}
r={'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sourceTree':subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip(),'inputs':inputs(),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()};out.with_suffix('.json').write_text(json.dumps(r,indent=2))
with out.with_suffix('.log').open('w') as f:r['exitCode']=subprocess.call(argv,stdout=f,stderr=subprocess.STDOUT)
r.update(finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),inputChanged=inputs()!=r['inputs']);out.with_suffix('.json').write_text(json.dumps(r,indent=2));print(out.name,r['exitCode'],flush=True);sys.exit(r['exitCode'])

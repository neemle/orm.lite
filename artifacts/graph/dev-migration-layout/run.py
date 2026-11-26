import pathlib,subprocess,json,datetime,hashlib,sys
out=pathlib.Path(sys.argv[1]);argv=sys.argv[2:]
def digest():
 h=hashlib.sha256()
 for name in sorted(set(subprocess.check_output(['git','ls-files','-co','--exclude-standard','-z']).split(b'\0'))):
  if name and pathlib.Path(name.decode()).is_file():h.update(name+b'\0'+pathlib.Path(name.decode()).read_bytes()+b'\0')
 return h.hexdigest()
r={'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'status':subprocess.check_output(['git','status','--short'],text=True),'sourceDigest':digest(),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()};out.with_suffix('.json').write_text(json.dumps(r,indent=2))
with out.with_suffix('.log').open('w') as f:r['exitCode']=subprocess.call(argv,stdout=f,stderr=subprocess.STDOUT)
r.update(finished=datetime.datetime.now(datetime.timezone.utc).isoformat(),sourceChangedDuringRun=digest()!=r['sourceDigest']);out.with_suffix('.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2));sys.exit(r['exitCode'])

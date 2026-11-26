import datetime,hashlib,json,pathlib,subprocess,sys
def source_digest():
 h=hashlib.sha256()
 for raw in sorted(subprocess.check_output(['git','ls-files','-co','--exclude-standard','-z','--','src']).split(b'\0')):
  if raw:
   p=pathlib.Path(raw.decode())
   if p.is_file():h.update(raw+b'\0'+p.read_bytes()+b'\0')
 return h.hexdigest()
def proof_digest():
 h=hashlib.sha256()
 for p in sorted(pathlib.Path('artifacts/schema-diff/dev/foundation-proof').glob('*')):
  if p.is_file():h.update(str(p).encode()+b'\0'+p.read_bytes()+b'\0')
 return h.hexdigest()
name=sys.argv[1];argv=sys.argv[2:];p=pathlib.Path('artifacts/schema-diff/dev')/name
r={'gate':name,'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'sourceTree':subprocess.check_output(['git','rev-parse','HEAD:src'],text=True).strip(),'testTree':subprocess.check_output(['git','rev-parse','HEAD:tests'],text=True).strip(),'sourceDigest':source_digest(),'proofDigest':proof_digest(),'worktreeStatus':subprocess.check_output(['git','status','--short'],text=True),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()}
p.with_suffix('.json').write_text(json.dumps(r,indent=2))
with p.with_suffix('.log').open('w') as f:r['exitCode']=subprocess.call(argv,stdout=f,stderr=subprocess.STDOUT)
r['sourceChangedDuringRun']=source_digest()!=r['sourceDigest'];r['proofChangedDuringRun']=proof_digest()!=r['proofDigest'];r['finished']=datetime.datetime.now(datetime.timezone.utc).isoformat();p.with_suffix('.json').write_text(json.dumps(r,indent=2));print(json.dumps(r));sys.exit(r['exitCode'])

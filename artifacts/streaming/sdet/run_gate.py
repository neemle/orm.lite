import argparse,datetime,hashlib,json,pathlib,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('gate');p.add_argument('output');p.add_argument('argv',nargs=argparse.REMAINDER);a=p.parse_args();argv=a.argv[1:] if a.argv[0]=='--' else a.argv
base=pathlib.Path(a.output);base.parent.mkdir(parents=True,exist_ok=True)
def digest():
 h=hashlib.sha256()
 for entry in sorted(set(subprocess.check_output(['git','ls-files','-co','--exclude-standard','-z']).split(b'\0'))):
  if entry and pathlib.Path(entry.decode()).is_file():h.update(entry+b'\0'+pathlib.Path(entry.decode()).read_bytes()+b'\0')
 return h.hexdigest()
r={'gate':a.gate,'argv':argv,'cwd':str(pathlib.Path.cwd()),'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'worktreeStatus':subprocess.check_output(['git','status','--short'],text=True),'sourceDigest':digest(),'started':datetime.datetime.now(datetime.timezone.utc).isoformat()}
base.with_suffix('.json').write_text(json.dumps(r,indent=2))
with base.with_suffix('.log').open('w') as f:r['exitCode']=subprocess.call(argv,stdout=f,stderr=subprocess.STDOUT)
r['finished']=datetime.datetime.now(datetime.timezone.utc).isoformat();r['sourceChangedDuringRun']=digest()!=r['sourceDigest'];r['log']=str(base.with_suffix('.log'));base.with_suffix('.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2));sys.exit(r['exitCode'])

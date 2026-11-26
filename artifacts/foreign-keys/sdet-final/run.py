import datetime,json,pathlib,subprocess,sys

root=pathlib.Path.cwd()
label=sys.argv[1]
argv=sys.argv[2:]
output=root/'artifacts/foreign-keys/sdet-final'/label
output.parent.mkdir(parents=True,exist_ok=True)
git=lambda *args:subprocess.check_output(['git',*args],text=True).strip()
record=dict(gate=label,argv=argv,cwd=str(root),commit=git('rev-parse','HEAD'),
 sourceTree=git('rev-parse','HEAD:src'),testTree=git('rev-parse','HEAD:tests'),
 started=datetime.datetime.now(datetime.timezone.utc).isoformat(),status=git('status','--short'))
output.with_suffix('.json').write_text(json.dumps(record,indent=2))
with output.with_suffix('.log').open('w') as log:
 record['exitCode']=subprocess.call(argv,stdout=log,stderr=subprocess.STDOUT)
record['finished']=datetime.datetime.now(datetime.timezone.utc).isoformat()
record['finalStatus']=git('status','--short')
record['sourceTreeAfter']=git('rev-parse','HEAD:src')
output.with_suffix('.json').write_text(json.dumps(record,indent=2))
print(json.dumps(record))
sys.exit(record['exitCode'])

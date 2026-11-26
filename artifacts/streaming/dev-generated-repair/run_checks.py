import pathlib,json,subprocess,concurrent.futures,time
base=pathlib.Path('artifacts/streaming/dev-generated-repair')
def invoke(name):
 old=json.loads(pathlib.Path('artifacts/streaming/dev-lock-repair/'+name+'.json').read_text())
 argv=[s.replace('artifacts/streaming/dev-lock-repair','artifacts/streaming/dev-generated-repair').replace('orml116-dev-lock','orml116-gen-repair').replace('orml.actor=dev-lock-repair','orml.actor=dev-generated-repair').replace('Host=lock-pg;','Host=gen-pg;') for s in old['argv']]
 for label in ['native10-build','native10-out','native11-build','native11-out','scoped10-results','scoped11-results']:
  argv=[a.replace(label,'verified-'+label) for a in argv]
 return subprocess.call(['python3','artifacts/streaming/run_gate.py','GEN_'+name.upper(),str(base/('verified-'+name)),'--',*argv])
for prerequisite in ['verified-unit','final-native11']:
 while True:
  path=base/(prerequisite+'.json')
  record=json.loads(path.read_text()) if path.exists() else {}
  if 'exitCode' in record:
   if prerequisite=='verified-unit' and record['exitCode']!=0:raise SystemExit(1)
   break
  time.sleep(1)
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 results=list(pool.map(invoke,['scoped10','scoped11']))
if any(results):raise SystemExit(1)
# Both consumers use the same isolated PostgreSQL table name; execute native gates sequentially.
for name in ['native10','native11']:
 if invoke(name):raise SystemExit(1)

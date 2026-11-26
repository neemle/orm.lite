import json,subprocess,time,datetime
from pathlib import Path
base=Path('artifacts/streaming/dev-generated-repair');records=[]
def run(argv,required=True):
 p=subprocess.run(argv,text=True,capture_output=True);records.append({'argv':argv,'exitCode':p.returncode,'stdout':p.stdout,'stderr':p.stderr,'time':datetime.datetime.now(datetime.timezone.utc).isoformat()});(base/'provider-setup-final.json').write_text(json.dumps(records,indent=2)+'\n')
 if required and p.returncode:raise RuntimeError(p.stderr)
 return p
net=run(['docker','network','create','--label','orml.task=ORML-116','--label','orml.actor=dev-generated-repair','orml116-gen-repair']).stdout.strip()
pg=run(['docker','run','-d','--rm','--name','orml116-gen-pg','--network',net,'--network-alias','gen-pg','--label','orml.task=ORML-116','--label','orml.actor=dev-generated-repair','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','neemle/pg.lite:19']).stdout.strip()
(base/'owned-resources-final.json').write_text(json.dumps({'network':net,'container':pg},indent=2)+'\n')
for _ in range(20):
 if run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--healthcheck'],False).returncode==0:break
 time.sleep(1)
else:raise RuntimeError('readiness')
run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--version'])
print(pg)

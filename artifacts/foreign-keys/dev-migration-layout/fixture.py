from pathlib import Path
import subprocess,json,time,datetime
b=Path('artifacts/foreign-keys/dev-migration-layout');records=[]
def run(a,ok=True):
 p=subprocess.run(a,text=True,capture_output=True);records.append({'argv':a,'exitCode':p.returncode,'stdout':p.stdout,'stderr':p.stderr,'time':datetime.datetime.now(datetime.timezone.utc).isoformat()});(b/'fixture-setup.json').write_text(json.dumps(records,indent=2)+'\n')
 if ok and p.returncode:raise RuntimeError(p.stderr)
 return p
net=run(['docker','network','create','--label','orml.task=ORML-110','--label','orml.actor=dev-migration-layout','orml110-migration-layout']).stdout.strip();containers=[]
for alias in ['mig-pg','demo-pg']:
 pg=run(['docker','run','-d','--rm','--name','orml110-'+alias,'--network',net,'--network-alias',alias,'--label','orml.task=ORML-110','--label','orml.actor=dev-migration-layout','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','neemle/pg.lite:19']).stdout.strip();containers.append(pg)
 for _ in range(20):
  if run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--healthcheck'],False).returncode==0:break
  time.sleep(1)
 else:raise RuntimeError('readiness')
 run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--version'])
(b/'owned-resources.json').write_text(json.dumps({'network':net,'containers':containers},indent=2)+'\n')

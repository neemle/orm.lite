import subprocess,json,pathlib,time,datetime
p=pathlib.Path('artifacts/relation-loading/dev');rows=[]
def run(argv,check=True):
 r=subprocess.run(argv,capture_output=True,text=True);rows.append(dict(argv=argv,exitCode=r.returncode,stdout=r.stdout,stderr=r.stderr,time=datetime.datetime.now(datetime.timezone.utc).isoformat()));(p/'provider-setup-v3.json').write_text(json.dumps(rows,indent=2))
 if check:r.check_returncode()
 return r
net=run(['docker','network','create','--label','orml.task=ORML-109','--label','orml.actor=dev109-policy-v3','orml109-policy-v3-2b794f24']).stdout.strip();(p/'network-v3-id.txt').write_text(net)
pg=run(['docker','run','-d','--rm','--name','orml109-policy-v3-pg-2b794f24','--network',net,'--network-alias','pg','--label','orml.task=ORML-109','--label','orml.actor=dev109-policy-v3','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','neemle/pg.lite:19']).stdout.strip();(p/'pg-v3-id.txt').write_text(pg)
for i in range(30):
 if run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--healthcheck'],False).returncode==0:break
 time.sleep(1)
else:raise RuntimeError('PG19 not ready')
run(['docker','exec',pg,'/usr/local/pgsql/bin/postgres','--version'])
run(['docker','inspect',pg])

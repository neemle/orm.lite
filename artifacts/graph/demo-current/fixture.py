import pathlib,json,subprocess,datetime,time
P=pathlib.Path('artifacts/graph/demo-current');now=lambda:datetime.datetime.now(datetime.timezone.utc).isoformat();records=[]
def cmd(a):
 r=subprocess.run(a,capture_output=True,text=True);records.append({'argv':a,'exitCode':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'at':now()});return r
n='orml108-graph-current-'+datetime.datetime.now().strftime('%H%M%S');r=cmd(['docker','network','create','--label','orml.task=ORML-108','--label','orml.actor=graph-demo-current',n]);assert r.returncode==0;net=r.stdout.strip();r=cmd(['docker','run','-d','--rm','--memory=320m','--cpus=1','--network',net,'--network-alias','graph-pg','--label','orml.task=ORML-108','--label','orml.actor=graph-demo-current','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','sha256:790e4afa39efa8fcb2edf47692c5330e7a36a5e89b0fa4f872848cbe4bf70117']);assert r.returncode==0;cid=r.stdout.strip();ready=False
for _ in range(20):
 r=cmd(['docker','exec',cid,'/usr/local/pgsql/bin/postgres','--healthcheck'])
 if r.returncode==0:ready=True;break
 time.sleep(.5)
assert ready;cmd(['docker','inspect',cid]);cmd(['docker','network','inspect',net]);(P/'fixture.json').write_text(json.dumps({'createdAt':now(),'containerId':cid,'networkId':net,'records':records},indent=2));print('own PG19 ready',cid,net)

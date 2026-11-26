import datetime,json,pathlib,subprocess,time
P=pathlib.Path('artifacts/foreign-keys/current-main-integration');records=[]
def run(argv):
 r=subprocess.run(argv,text=True,capture_output=True);records.append({'argv':argv,'exitCode':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'at':datetime.datetime.now(datetime.timezone.utc).isoformat()});return r
network=run(['docker','network','create','--label','orml.task=ORML-110','--label','orml.actor=fk-current-main-d06d5f0d','orml110-current-d06d5f0d']);assert network.returncode==0;net=network.stdout.strip()
pg=run(['docker','run','-d','--rm','--memory=320m','--cpus=1','--network',net,'--network-alias','fk-pg','--label','orml.task=ORML-110','--label','orml.actor=fk-current-main-d06d5f0d','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','sha256:790e4afa39efa8fcb2edf47692c5330e7a36a5e89b0fa4f872848cbe4bf70117']);assert pg.returncode==0;cid=pg.stdout.strip()
(P/'fixture.json').write_text(json.dumps({'networkId':net,'containerId':cid,'ready':False,'records':records},indent=2)+'\n')
for attempt in range(20):
 r=run(['docker','exec',cid,'/usr/local/pgsql/bin/postgres','--healthcheck'])
 if r.returncode==0:break
 time.sleep(2)
assert r.returncode==0
inspected=run(['docker','inspect',cid]);assert inspected.returncode==0
(P/'fixture.json').write_text(json.dumps({'networkId':net,'containerId':cid,'ready':True,'records':records},indent=2)+'\n')
print('OWN_FIXTURE_READY',cid,net,flush=True)

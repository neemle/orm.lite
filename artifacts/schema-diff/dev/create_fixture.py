import subprocess,pathlib,json,datetime
root=pathlib.Path('artifacts/schema-diff/dev');name='orml117-dev-c241f07'
def run(argv):
 r=subprocess.run(argv,text=True,capture_output=True);assert r.returncode==0,(argv,r.stderr);return r.stdout.strip()
net=run(['docker','network','create','--label','orml.task=ORML-117','--label','orml.actor=dev',name]);argv=['docker','run','-d','--name',name+'-pg','--network',name,'--network-alias','schema-pg','--label','orml.task=ORML-117','--label','orml.actor=dev','--tmpfs','/var/lib/postgresql/data:uid=70,gid=70,mode=0700','-e','POSTGRES_HOST_AUTH_METHOD=trust','-e','POSTGRES_USER=postgres','neemle/pg.lite:19'];cid=run(argv)
r={'at':datetime.datetime.now(datetime.timezone.utc).isoformat(),'networkId':net,'containerId':cid,'argv':argv,'inspect':json.loads(run(['docker','inspect',cid]))};(root/'owned-fixture.json').write_text(json.dumps(r,indent=2));print(json.dumps({'containerId':cid,'networkId':net}))

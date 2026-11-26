import datetime,json,pathlib,subprocess
P=pathlib.Path('artifacts/foreign-keys/current-main-integration');f=json.loads((P/'fixture.json').read_text());records=[]
def run(argv):
 r=subprocess.run(argv,text=True,capture_output=True);records.append({'argv':argv,'exitCode':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'at':datetime.datetime.now(datetime.timezone.utc).isoformat()});return r
assert json.loads((P/'current-jit10-postgres.json').read_text())['exitCode']==0
pg=f['containerId'];net=f['networkId'];r=run(['docker','inspect',pg]);assert r.returncode==0;c=json.loads(r.stdout)[0]
assert c['Id']==pg and c['Config']['Labels']['orml.task']=='ORML-110' and c['Config']['Labels']['orml.actor']=='fk-current-main-d06d5f0d'
r=run(['docker','network','inspect',net]);assert r.returncode==0;n=json.loads(r.stdout)[0]
assert n['Id']==net and n['Labels']['orml.task']=='ORML-110' and n['Labels']['orml.actor']=='fk-current-main-d06d5f0d' and set(n['Containers'])=={pg}
assert run(['docker','rm','-f',pg]).returncode==0
assert run(['docker','network','rm',net]).returncode==0
assert run(['docker','inspect',pg]).returncode==1
assert run(['docker','network','inspect',net]).returncode==1
roster=run(['docker','ps','-aq','--filter','label=orml.task=ORML-110','--filter','label=orml.actor=fk-current-main-d06d5f0d']);assert roster.returncode==0 and not roster.stdout.strip()
assert not list((P/'tmp').glob('fk-demo-*'))
(P/'cleanup.json').write_text(json.dumps({'generated':datetime.datetime.now(datetime.timezone.utc).isoformat(),'containerId':pg,'networkId':net,'records':records,'ownActorRosterEmpty':True,'sqliteScratchAbsent':True,'foreignResourcesUntouched':True},indent=2)+'\n')
print('CLEANUP_COMPLETE',pg,net)

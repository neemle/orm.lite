import datetime,json,pathlib,subprocess
P=pathlib.Path('artifacts/graph/current-contract-integration');f=json.loads((P/'fixture.json').read_text());records=[]
def run(argv):
 r=subprocess.run(argv,text=True,capture_output=True);records.append({'argv':argv,'exitCode':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'at':datetime.datetime.now(datetime.timezone.utc).isoformat()});return r
assert json.loads((P/'affected-after.json').read_text())['exitCode']==0
pg=f['containerId'];net=f['networkId'];r=run(['docker','inspect',pg]);assert r.returncode==0;c=json.loads(r.stdout)[0]
assert c['Id']==pg and c['Config']['Labels']['orml.task']=='ORML-108' and c['Config']['Labels']['orml.actor']=='graph-contract-4048'
r=run(['docker','network','inspect',net]);assert r.returncode==0;n=json.loads(r.stdout)[0]
assert n['Id']==net and n['Labels']['orml.task']=='ORML-108' and n['Labels']['orml.actor']=='graph-contract-4048' and set(n['Containers'])=={pg}
assert run(['docker','rm','-f',pg]).returncode==0
assert run(['docker','network','rm',net]).returncode==0
assert run(['docker','inspect',pg]).returncode==1
assert run(['docker','network','inspect',net]).returncode==1
roster=run(['docker','ps','-aq','--filter','label=orml.task=ORML-108','--filter','label=orml.actor=graph-contract-4048']);assert roster.returncode==0 and not roster.stdout.strip()
assert not list((P/'tmp').glob('orm-graph-*'))
assert not list((P/'tmp').glob('graph-secret-*'))
(P/'cleanup.json').write_text(json.dumps({'generated':datetime.datetime.now(datetime.timezone.utc).isoformat(),'containerId':pg,'networkId':net,'records':records,'ownActorRosterEmpty':True,'sqliteScratchAbsent':True,'foreignResourcesUntouched':True},indent=2)+'\n')
print('CLEANUP_COMPLETE',pg,net)

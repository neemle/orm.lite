from pathlib import Path
import subprocess,json,datetime
p=Path('artifacts/one-to-one/sdet-current');pg=json.loads((p/'custody-pg.json').read_text())[0];net=json.loads((p/'custody-network.json').read_text())[0];records=[]
def run(argv):
 z=subprocess.run(argv,capture_output=True,text=True);x=dict(argv=argv,exitCode=z.returncode,stdout=z.stdout,stderr=z.stderr,at=datetime.datetime.now(datetime.timezone.utc).isoformat());records.append(x);return x
a=run(['docker','inspect',pg['Id']]);b=run(['docker','network','inspect',net['Id']]);assert a['exitCode']==b['exitCode']==0
actual=json.loads(a['stdout'])[0];network=json.loads(b['stdout'])[0]
assert actual['Id']==pg['Id'] and actual['Image']==pg['Image'] and network['Id']==net['Id']
for labels in [actual['Config']['Labels'],network['Labels']]:assert labels['orml.task']=='ORML-111' and labels['orml.actor']=='one-to-one-dev'
assert set(network['Containers'])=={pg['Id']}
run(['docker','rm','-f',pg['Id']]);run(['docker','network','rm',net['Id']])
absent=run(['docker','inspect',pg['Id']])['exitCode']==1 and run(['docker','network','inspect',net['Id']])['exitCode']==1
rosters=[]
for actor in ['one-to-one-dev','one-to-one-sdet-3d75']:
 x=run(['docker','ps','-aq','--filter','label=orml.task=ORML-111','--filter','label=orml.actor='+actor]);rosters.append(x['exitCode']==0 and not x['stdout'].strip())
scratch=[str(x) for x in (p/'tmp').glob('*') if x.name.startswith(('one-to-one-','sdet-oto-'))]
result=dict(generated=datetime.datetime.now(datetime.timezone.utc).isoformat(),ownedContainer=pg['Id'],ownedNetwork=net['Id'],records=records,ownedIdsAbsent=absent,ownedActorRostersEmpty=all(rosters),sqliteScratchRemaining=scratch,foreignResourcesUntouched=True,warmNuGetVolumeUntouched=True)
(p/'cleanup.json').write_text(json.dumps(result,indent=2)+'\n');assert absent and all(rosters) and not scratch;print('Exact owned PG/network removed; own actor rosters and SQLite scratch empty')

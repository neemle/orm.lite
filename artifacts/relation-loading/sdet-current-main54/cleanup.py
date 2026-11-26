from pathlib import Path
import json,subprocess,datetime
p=Path('artifacts/relation-loading/sdet-current-main54');f=json.loads((p/'fixture.json').read_text());rows=[]
def run(a):
 z=subprocess.run(a,capture_output=True,text=True);x=dict(argv=a,exitCode=z.returncode,stdout=z.stdout,stderr=z.stderr);rows.append(x);return x
c=run(['docker','inspect',f['containerId']]);n=run(['docker','network','inspect',f['networkId']]);assert c['exitCode']==n['exitCode']==0
container=json.loads(c['stdout'])[0];network=json.loads(n['stdout'])[0]
assert container['Id']==f['containerId'] and network['Id']==f['networkId'] and set(network['Containers'])=={f['containerId']}
for labels in [container['Config']['Labels'],network['Labels']]:assert labels['orml.task']=='ORML-109' and labels['orml.actor']==f['actor']
assert container['Image']=='sha256:790e4afa39efa8fcb2edf47692c5330e7a36a5e89b0fa4f872848cbe4bf70117'
run(['docker','rm','-f',f['containerId']]);run(['docker','network','rm',f['networkId']])
absent=run(['docker','inspect',f['containerId']])['exitCode']==1 and run(['docker','network','inspect',f['networkId']])['exitCode']==1
empty=[]
for actor in [f['actor'],'sdet-main54-6e04']:
 a=run(['docker','ps','-aq','--filter','label=orml.task=ORML-109','--filter','label=orml.actor='+actor]);empty.append(a['exitCode']==0 and not a['stdout'].strip())
x=dict(generated=datetime.datetime.now(datetime.timezone.utc).isoformat(),ownedContainer=f['containerId'],ownedNetwork=f['networkId'],records=rows,actualMemoryLimit=container['HostConfig']['Memory'],actualTmpfs=container['HostConfig']['Tmpfs'],OOMKilled=container['State']['OOMKilled'],ownedIdsAbsent=absent,ownedActorRostersEmpty=all(empty),foreignUntouched=True,warmNuGetRO=True)
(p/'cleanup.json').write_text(json.dumps(x,indent=2)+'\n');assert absent and all(empty);print('ExactownedPG/net removed; bothownactorrosters empty.')

from pathlib import Path
import json,subprocess,sys
p=Path('artifacts/relation-loading/review-fixes');root=Path.cwd();net=(p/'network-id.txt').read_text().strip()
def gate(name,command,v):
 a=json.loads(Path('artifacts/relation-loading/dev/final-runtime10-postgres.json').read_text())['argv'];i=a.index('dotnet');a=a[:i];a[a.index('--network')+1]=net;a[a.index('ORMLITE_EXPECT_DOTNET=10')]='ORMLITE_EXPECT_DOTNET='+str(v);a[a.index('orml108-graph-native-sdk10:verified')]='orml108-graph-native-sdk'+str(v)+':verified';a[a.index('orml.actor=dev-policy')]='orml.actor=review-fixes';a+=command
 r=subprocess.run(['python3',str(p/'run.py'),str(p/name)]+a)
 if r.returncode:raise SystemExit(r.returncode)
if sys.argv[1]=='jit':
 for v in [10,11]:
  for provider in ['sqlite','postgres']:
   gate('jit'+str(v)+'-'+provider,['dotnet','run','--project','examples/relation-loading/RelationLoadingDemo.csproj','-c','Release','-p:PolicySmokeTfm=net'+str(v)+'.0','--artifacts-path',str((p/('jit'+str(v)+'-build')).resolve()),'-p:UseSharedCompilation=false','--',provider],v)
elif sys.argv[1]=='native':
 for v in [10,11]:
  out=(p/('native'+str(v)+'-out')).resolve();build=(p/('native'+str(v)+'-build')).resolve()
  cmd='dotnet publish examples/relation-loading/RelationLoadingDemo.csproj -c Release -r linux-arm64 -p:PolicySmokeNative=true -p:PolicySmokeTfm=net'+str(v)+'.0 --artifacts-path '+str(build)+' -o '+str(out)+' -p:UseSharedCompilation=false -m:1 && '+str(out/'PolicySmoke')+' sqlite && '+str(out/'PolicySmoke')+' postgres'
  gate('native'+str(v),['sh','-c',cmd],v)
else:raise ValueError('jit or native')

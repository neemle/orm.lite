from pathlib import Path
import subprocess,sys
p=Path('artifacts/foreign-keys/demo-parity').resolve();root=Path.cwd();net=(p/'network-id.txt').read_text().strip()
def gate(name,command,v):
 a=['docker','run','--rm','--label','orml.task=ORML-110','--label','orml.actor=fk-demo','--network',net,'-e','FOREIGN_KEYS_POSTGRES=Host=pg;Username=postgres;Database=postgres','-e','DOTNET_ROLL_FORWARD=Major','-e','DOTNET_CLI_USE_MSBUILD_SERVER=0','-e','MSBUILDUSESERVER=0','-v',str(root)+':'+str(root),'-v','/Users/para/work/tskr/libs/orm.lite/.git:/Users/para/work/tskr/libs/orm.lite/.git:ro','-v','orml-107-nuget:/root/.nuget/packages','-w',str(root),'-e','GIT_CONFIG_COUNT=1','-e','GIT_CONFIG_KEY_0=safe.directory','-e','GIT_CONFIG_VALUE_0='+str(root),'orml108-graph-native-sdk'+str(v)+':verified']+command
 r=subprocess.run(['python3',str(p/'run.py'),str(p/name)]+a)
 if r.returncode:raise SystemExit(r.returncode)
if sys.argv[1]=='jit':
 for v in [10,11]:
  for provider in ['sqlite','postgres']:
   gate('jit'+str(v)+'-'+provider,['dotnet','run','--project','examples/foreign-keys/ForeignKeysDemo.csproj','-c','Release','-p:DemoTfm=net'+str(v)+'.0','--artifacts-path',str(p/('jit'+str(v)+'-build')),'-p:UseSharedCompilation=false','--',provider],v)
elif sys.argv[1]=='native':
 for v in [10,11]:
  out=p/('native'+str(v)+'-out');build=p/('native'+str(v)+'-build')
  cmd='dotnet publish examples/foreign-keys/ForeignKeysDemo.csproj -c Release -r linux-arm64 -p:DemoNative=true -p:DemoTfm=net'+str(v)+'.0 --artifacts-path '+str(build)+' -o '+str(out)+' -p:UseSharedCompilation=false -m:1 && '+str(out/'ForeignKeysDemo')+' sqlite && '+str(out/'ForeignKeysDemo')+' postgres'
  gate('native'+str(v),['sh','-c',cmd],v)
else:raise ValueError('jit or native')
